using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Media;

namespace ThisWidget;

/// <summary>Owns the shared local journal, both views, persistence and application lifetime.</summary>
public sealed class AppController : IDisposable
{
    private readonly Store store;
    private readonly bool integrate;
    private readonly DispatcherTimer clock = new() { Interval = TimeSpan.FromSeconds(30) };
    private System.Windows.Forms.NotifyIcon? tray;
    public Journal Journal
    {
        get; private set;
    }
    public DateOnly Today { get; private set; } = DateOnly.FromDateTime(DateTime.Now);
    public MainWindow? Editor
    {
        get; private set;
    }
    public WidgetWindow Widget
    {
        get;
    }
    public bool IsExiting
    {
        get; private set;
    }
    public string SaveStatus { get; private set; } = "Saved on this device";
    public event Action? Changed;

    public AppController(Store store, Journal journal, bool integrate = false)
    {
        this.store = store;
        this.integrate = integrate;
        Journal = journal;
        Journal.Normalize();
        if (integrate)
        {
            Journal.Settings.DesktopMode = true;
            Journal.Settings.AlwaysOnTop = false;
        }
        Theme.Apply(Journal.Settings);
        Widget = new WidgetWindow(this);
        clock.Tick += (_, _) => Tick();
    }

    public void Start(bool background = true)
    {
        CreateTray();
        new System.Windows.Interop.WindowInteropHelper(Widget).EnsureHandle();
        if (Journal.Settings.WidgetEnabled)
            Widget.Present();
        if (!background)
            OpenEditor();
        if (integrate)
            Native.Startup(Journal.Settings.StartWithWindows);
        clock.Start();
        if (store.RecoveryMessage.Length > 0)
            MessageBox.Show(store.RecoveryMessage, "ThisWidget recovery");
    }

    public bool Save(bool notify = true)
    {
        try
        {
            store.Save(Journal);
            SaveStatus = "Saved locally · " + DateTime.Now.ToString("HH:mm");
            if (notify)
                Changed?.Invoke();
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            SaveStatus = "Unsaved changes · export a backup";
            Program.Log(e);
            MessageBox.Show("Could not save. Keep ThisWidget open and export a backup from Settings.\n\n" + e.Message,
                "Save failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    public void Change(Action action)
    {
        Editor?.FlushNotes();
        action();
        Save();
    }

    public void ApplySettings(Appearance settings)
    {
        Journal.Settings = settings;
        Journal.Normalize();
        if (integrate)
        {
            settings.DesktopMode = true;
            settings.AlwaysOnTop = false;
        }
        Theme.Apply(settings);
        Editor?.ApplyAppearance();
        if (integrate)
            Native.Startup(settings.StartWithWindows);
        if (settings.WidgetEnabled)
            Widget.Present();
        else
        {
            Widget.ApplyLayout();
            Widget.Hide();
        }
        Save();
    }

    public void AddWidget()
    {
        Journal.Settings.WidgetEnabled = true;
        Widget.Present();
        Save();
    }

    public void RemoveWidget()
    {
        Journal.Settings.WidgetEnabled = false;
        Widget.Hide();
        Save();
    }

    public void AddHomeWidget()
    {
        Journal.Settings.DesktopMode = true;
        Journal.Settings.AlwaysOnTop = false;
        Journal.Settings.LockPosition = true;
        AddWidget();
    }

    public void OpenWidgetSetup()
    {
        OpenEditor();
        new WidgetSetupDialog(Editor!, this).ShowDialog();
    }

    public void ResetWidgetPosition()
    {
        Journal.Settings.WidgetLeft = -1;
        Journal.Settings.WidgetTop = 60;
        Widget.ApplyLayout();
        Save();
    }

    public void OpenEditor(DateOnly? day = null)
    {
        Editor ??= new MainWindow(this);
        if (day.HasValue)
            Editor.SelectDate(day.Value);
        Editor.Show();
        Editor.WindowState = WindowState.Normal;
        Editor.Activate();
    }

    public void OpenSettings()
    {
        OpenEditor();
        Editor!.ShowSettings();
    }

    private void Tick()
    {
        var now = DateTime.Now;
        var date = DateOnly.FromDateTime(now);
        if (date != Today)
        {
            Today = date;
            Changed?.Invoke();
        }
        var s = Journal.Settings;
        if (s.WidgetEnabled && s.DesktopMode && !Widget.IsDesktopAttached)
            Widget.Present();
        if (s.ReminderEnabled && s.LastReminderDate != Journal.Key(date) &&
            now.TimeOfDay >= new TimeSpan(s.ReminderHour, s.ReminderMinute, 0) &&
            !Journal.IsComplete(date) && Journal.Plan(date).Count > 0)
        {
            s.LastReminderDate = Journal.Key(date);
            if (Save(false))
                tray?.ShowBalloonTip(6000, "Today's training", string.Join(" · ", Journal.Plan(date)),
                System.Windows.Forms.ToolTipIcon.Info);
        }
    }

    private void CreateTray()
    {
        tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "ThisWidget · Workout calendar",
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Application,
            Visible = true
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open calendar   Ctrl+Alt+W", null, (_, _) => Application.Current.Dispatcher.Invoke(() => OpenEditor()));
        menu.Items.Add("Add Widget to home screen", null, (_, _) => Application.Current.Dispatcher.Invoke(AddHomeWidget));
        menu.Items.Add("Settings", null, (_, _) => Application.Current.Dispatcher.Invoke(OpenSettings));
        menu.Items.Add("Exit ThisWidget", null, (_, _) => Application.Current.Dispatcher.Invoke(Exit));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Application.Current.Dispatcher.Invoke(() => OpenEditor());
    }

    public void Export()
    {
        Editor?.FlushNotes();
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export ThisWidget backup",
            Filter = "ThisWidget journal (*.json)|*.json",
            FileName = "ThisWidget-backup-" + DateTime.Now.ToString("yyyy-MM-dd") + ".json"
        };
        if (dialog.ShowDialog(Editor) == true)
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(Journal, Store.JsonOptions));
    }

    public void Import()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Title = "Restore ThisWidget backup", Filter = "Journal (*.json)|*.json" };
        if (dialog.ShowDialog(Editor) != true)
            return;
        try
        {
            var restored = Store.ReadJson(File.ReadAllText(dialog.FileName));
            if (MessageBox.Show(Editor!, $"Restore {restored.Days.Count} recorded days? The current journal will be kept in a safety backup.",
                "Restore backup", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
            Editor?.FlushNotes();
            File.WriteAllText(store.PathName + ".before-import-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"),
                JsonSerializer.Serialize(Journal, Store.JsonOptions));
            restored.Settings = Journal.Settings;
            store.Save(restored);
            Journal = restored;
            Changed?.Invoke();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            MessageBox.Show("Could not restore this file. Your current journal was kept.\n\n" + e.Message, "Invalid backup");
        }
    }

    public void SaveImage()
    {
        if (Editor == null)
            return;
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "PNG image (*.png)|*.png", FileName = "ThisWidget-calendar.png" };
        if (dialog.ShowDialog(Editor) != true)
            return;
        var bitmap = new RenderTargetBitmap((int)Editor.ActualWidth * 2, (int)Editor.ActualHeight * 2, 192, 192, PixelFormats.Pbgra32);
        bitmap.Render(Editor);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(dialog.FileName);
        encoder.Save(stream);
    }

    public void Exit()
    {
        Editor?.FlushNotes();
        if (!Save(false))
            return;
        IsExiting = true;
        Widget.Close();
        Editor?.Close();
        Dispose();
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        clock.Stop();
        tray?.Dispose();
        tray = null;
        if (!IsExiting)
        {
            IsExiting = true;
            Widget.Close();
            Editor?.Close();
        }
    }
}
