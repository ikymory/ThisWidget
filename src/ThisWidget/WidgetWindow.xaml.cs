using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ThisWidget;

public partial class WidgetWindow : Window
{
    private readonly AppController controller;
    private IntPtr handle, desktop;
    private bool applying;
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    public bool IsDesktopAttached => desktop != IntPtr.Zero && Native.HostAlive(desktop) && Native.HasParent(handle);

    public void Present()
    {
        new WindowInteropHelper(this).EnsureHandle();
        ApplyLayout();
        // Desktop mode never silently becomes an overlay. Retry when Explorer is ready.
        if (controller.Journal.Settings.DesktopMode && !IsDesktopAttached)
            Hide();
        else
            Show();
    }

    public WidgetWindow(AppController controller)
    {
        this.controller = controller;
        InitializeComponent();
        ApplyLayout();
        controller.Changed += Render;
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            HwndSource.FromHwnd(handle).AddHook(Messages);
            Native.MarkProfile(handle);
            Native.RegisterWake(handle);
        };
        Loaded += (_, _) => ApplyLayout();
        Closing += (_, e) =>
        {
            if (!controller.IsExiting)
            {
                e.Cancel = true;
                controller.RemoveWidget();
            }
        };
        Closed += (_, _) => { Native.UnregisterWake(handle); controller.Changed -= Render; };
        Render();
    }

    public void ApplyLayout()
    {
        applying = true;
        try
        {
            if (desktop != IntPtr.Zero)
            {
                Native.Detach(handle);
                desktop = IntPtr.Zero;
            }
            var settings = controller.Journal.Settings;
            double designHeight = settings.ShowChecklist ? 450 : 360;
            Frame.Height = designHeight;
            Width = 460 * settings.WidgetScale;
            Height = designHeight * settings.WidgetScale;
            Opacity = settings.Opacity;
            var area = SystemParameters.WorkArea;
            Left = Math.Clamp(settings.WidgetLeft < 0 ? area.Right - Width - 24 : settings.WidgetLeft,
                area.Left, Math.Max(area.Left, area.Right - Width));
            Top = Math.Clamp(settings.WidgetTop, area.Top, Math.Max(area.Top, area.Bottom - Height));
            Topmost = settings.AlwaysOnTop && !settings.DesktopMode;
            if (handle != IntPtr.Zero && settings.DesktopMode)
            {
                var host = Native.DesktopHost();
                if (Native.Attach(handle, host))
                    desktop = host;
            }
            Render();
        }
        finally { applying = false; }
    }

    private void Render()
    {
        var today = controller.Today;
        var journal = controller.Journal;
        var settings = journal.Settings;
        var tasks = journal.Plan(today);
        TodayLabel.Text = today.ToString("dddd", English);
        DateLabel.Text = today.ToString("MMM d, yyyy", English);
        FocusLabel.Text = tasks.Count > 0 ? string.Join(" · ", tasks) : "Rest day / no focus set";
        FocusLabel.ToolTip = tasks.Count > 0 ? string.Join(" · ", tasks) : "Open the calendar to set your weekly split.";
        LockLabel.Visibility = settings.LockPosition ? Visibility.Visible : Visibility.Collapsed;
        YearLabel.Text = today.Year + "  ·  EVERY DAY COUNTS";
        WorkoutLabel.Text = journal.CompletedInYear(today.Year, today) + " workouts";
        StreakLabel.Text = journal.Streak(today) + " day streak";
        GoalLabel.Text = $"This week  {journal.CompletedThisWeek(today)} / {settings.WeeklyGoal}";
        CompleteButton.Content = journal.IsComplete(today) ? "✓  Done · undo" : "✓  Mark today done";
        ChecklistScroll.Visibility = settings.ShowChecklist ? Visibility.Visible : Visibility.Collapsed;
        Checklist.Children.Clear();
        journal.Days.TryGetValue(Journal.Key(today), out var record);
        foreach (string task in tasks)
        {
            var check = new CheckBox
            {
                Content = task,
                IsChecked = record?.Checked.Contains(task) == true,
                Margin = new Thickness(0, 0, 0, 8),
                MinHeight = 23
            };
            check.Click += (_, _) => controller.Change(() => journal.CheckTask(today, task, check.IsChecked == true));
            Checklist.Children.Add(check);
        }
        BuildGrid();
    }

    private void BuildGrid()
    {
        YearGrid.Children.Clear();
        var today = controller.Today;
        var journal = controller.Journal;
        var first = new DateOnly(today.Year, 1, 1);
        int offset = Calendar.WeekOffset(first, journal.Settings.SundayFirst);
        var days = Calendar.Days(today.Year);
        int columns = (days.Count + offset + 6) / 7;
        double pitch = 392d / columns;
        string[] names = journal.Settings.SundayFirst
            ? new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" }
            : new[] { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };
        for (int row = 0; row < 7; row += 2)
        {
            var label = new TextBlock { Text = names[row], FontSize = 7, Foreground = Theme.Brush("Muted") };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, 19 + row * 9);
            YearGrid.Children.Add(label);
        }
        foreach (var date in days)
        {
            int position = date.DayOfYear - 1 + offset;
            int column = position / 7, row = position % 7;
            if (date.Day == 1)
            {
                var month = new TextBlock { Text = date.ToString("MMM", English), FontSize = 7, Foreground = Theme.Brush("Muted") };
                Canvas.SetLeft(month, 24 + column * pitch);
                YearGrid.Children.Add(month);
            }
            bool complete = journal.IsComplete(date);
            var button = new Button
            {
                Style = (Style)FindResource("DayCell"),
                Width = Math.Max(4, pitch - 1.7),
                Height = 7.3,
                Background = Theme.Brush(complete ? "Accent" : "Cell"),
                BorderThickness = new Thickness(date == today ? 1 : 0),
                BorderBrush = Theme.Brush("Accent"),
                Opacity = date > today ? .55 : 1,
                Padding = new Thickness(0),
                Tag = date,
                ToolTip = date.ToString("dddd, MMMM d", English) + "\n" +
                    (journal.Plan(date).Count > 0 ? string.Join(" · ", journal.Plan(date)) : "No focus planned") +
                    (complete ? "\n✓ Completed" : "")
            };
            AutomationProperties.SetName(button, date.ToString("dddd, MMMM d, yyyy", English) + (complete ? ", complete" : ""));
            button.Click += (_, _) => controller.OpenEditor(date);
            button.MouseRightButtonUp += (_, e) =>
            {
                if (date <= today)
                    controller.Change(() => journal.Complete(date, !journal.IsComplete(date)));
                e.Handled = true;
            };
            Canvas.SetLeft(button, 24 + column * pitch);
            Canvas.SetTop(button, 18 + row * 9);
            YearGrid.Children.Add(button);
        }
    }

    private IntPtr Messages(IntPtr h, int message, IntPtr w, IntPtr l, ref bool handled)
    {
        if (message == Native.WakeMessage || (message == 0x0312 && w.ToInt32() == 71))
        {
            controller.OpenEditor();
            handled = true;
        }
        if (message == Native.ExitMessage)
        {
            controller.Exit();
            handled = true;
        }
        if (message == Native.WidgetMessage)
        {
            controller.AddWidget();
            handled = true;
        }
        if (message == Native.HomeWidgetMessage)
        {
            controller.AddHomeWidget();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void OpenClick(object sender, RoutedEventArgs e) => controller.OpenEditor(controller.Today);
    private void SettingsClick(object sender, RoutedEventArgs e) => controller.OpenSettings();
    private void RemoveClick(object sender, RoutedEventArgs e) => controller.RemoveWidget();
    private void CompleteClick(object sender, RoutedEventArgs e) => controller.Change(() =>
        controller.Journal.Complete(controller.Today, !controller.Journal.IsComplete(controller.Today)));

    private void DragHeaderDown(object sender, MouseButtonEventArgs e)
    {
        if (controller.Journal.Settings.LockPosition || applying || IsButton(e.OriginalSource as DependencyObject))
            return;
        if (desktop == IntPtr.Zero)
        {
            DragMove();
            controller.Journal.Settings.WidgetLeft = Left;
            controller.Journal.Settings.WidgetTop = Top;
            controller.Save(false);
            return;
        }
        var start = System.Windows.Forms.Cursor.Position;
        var origin = Native.Position(handle);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        Header.CaptureMouse();
        timer.Tick += (_, _) =>
        {
            var cursor = System.Windows.Forms.Cursor.Position;
            Native.Move(handle, desktop, new Point(origin.X + cursor.X - start.X, origin.Y + cursor.Y - start.Y));
            if (Mouse.LeftButton == MouseButtonState.Pressed)
                return;
            timer.Stop();
            Header.ReleaseMouseCapture();
            var end = Native.Position(handle);
            var dpi = VisualTreeHelper.GetDpi(this);
            controller.Journal.Settings.WidgetLeft = end.X / dpi.DpiScaleX;
            controller.Journal.Settings.WidgetTop = end.Y / dpi.DpiScaleY;
            controller.Save(false);
        };
        timer.Start();
    }

    private static bool IsButton(DependencyObject? item)
    {
        while (item != null)
        {
            if (item is ButtonBase)
                return true;
            item = VisualTreeHelper.GetParent(item);
        }
        return false;
    }
}
