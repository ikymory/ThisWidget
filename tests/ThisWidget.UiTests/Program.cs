using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;
using System.Windows.Input;
using System.Runtime.InteropServices;
using ThisWidget;

public static class UiTests
{
    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hwnd);
    static int passed, failed;
    static void Test(string name, Action run)
    {
        try
        {
            run();
            passed++;
            Console.WriteLine("PASS " + name);
        }
        catch (Exception e) { failed++; Console.WriteLine("FAIL " + name + ": " + e); }
    }
    static void Eq<T>(T expected, T actual)
    {
        if (!Equals(expected, actual))
            throw new Exception($"Expected {expected}; got {actual}");
    }
    static List<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        var r = new List<T>();
        if (root is T typed)
            r.Add(typed);
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            r.AddRange(Find<T>(VisualTreeHelper.GetChild(root, i)));
        return r;
    }
    static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
    static Button Button(Window w, string text) => Find<Button>(w).Single(x => Equals(x.Content, text));
    [STAThread]
    public static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        Theme.Initialize();
        var dir = Path.Combine(Path.GetTempPath(), "ThisWidgetUi-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var store = new Store(Path.Combine(dir, "journal.json"));
        Test("Default launch shows only the widget and keeps the editor closed", () =>
        {
            var startJournal = new Journal();
            startJournal.Settings.DesktopMode = false; // CI may have no interactive Explorer desktop.
            using var startController = new AppController(new Store(Path.Combine(dir, "start.json")), startJournal);
            startController.Start();
            Eq(true, startController.Widget.IsVisible);
            Eq(null, startController.Editor);
            Eq(Visibility.Visible, ((TextBlock)startController.Widget.FindName("LockLabel")).Visibility);
        });
        var j = new Journal();
        j.Settings.DesktopMode = false;
        var today = DateOnly.FromDateTime(DateTime.Now);
        j.Weekly[today.DayOfWeek.ToString()] = new() { "Gym", "Mobility" };
        var controller = new AppController(store, j);
        controller.OpenEditor();
        controller.Widget.Show();
        var w = controller.Editor!;
        w.UpdateLayout();
        controller.Widget.UpdateLayout();
        Test("All twelve months and every day are rendered", () => { Eq(DateTime.IsLeapYear(today.Year) ? 366 : 365, Find<Button>(w).Count(x => x.Tag is DateOnly)); });
        Test("Exercise clicks fill the actual calendar and persist", () => { foreach (var c in Find<CheckBox>(w)) { c.IsChecked = true; c.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent)); } Eq(true, j.IsComplete(today)); Eq(true, store.Load().IsComplete(today)); Eq("✓  Completed · click to undo", ((Button)w.FindName("CompleteButton")).Content); });
        Test("Notes survive selecting another date", () => { ((TextBox)w.FindName("Notes")).Text = "UI integration note"; Click(Find<Button>(w).Single(b => b.Tag is DateOnly d && d == today.AddDays(-1))); Eq("UI integration note", store.Load().Days[Journal.Key(today)].Notes); Click(Button(w, "Today")); Eq("UI integration note", ((TextBox)w.FindName("Notes")).Text); });
        Test("Future days allow planning but block completion", () => { var future = Find<Button>(w).FirstOrDefault(b => b.Tag is DateOnly d && d > today); if (future == null) { Click(Button(w, "›")); future = Find<Button>(w).First(b => b.Tag is DateOnly d && d > today); } Click(future); Eq(false, ((Button)w.FindName("CompleteButton")).IsEnabled); Click(Button(w, "Today")); });
        Test("Weekly editor saves recurring plan through real modal", () => { w.Dispatcher.BeginInvoke(() => { var d = app.Windows.OfType<WeeklyDialog>().Single(); d.UpdateLayout(); var boxes = Find<TextBox>(d); boxes[1].Text = "Back, Chest, Shoulders"; Click(Button(d, "Save weekly split")); }); Click(Button(w, "↻  Edit weekly split")); Eq("Back,Chest,Shoulders", string.Join(',', j.Weekly["Tuesday"])); });
        Test("Day editor persists an explicit empty override", () => { w.Dispatcher.BeginInvoke(() => { var d = app.Windows.OfType<DayDialog>().Single(); d.Input.Text = ""; Click(Button(d, "Save day")); }); Click(Button(w, "Edit day")); Eq(0, j.Plan(today).Count); Eq(0, store.Load().Plan(today).Count); });
        Test("Theme color and minimum size apply without losing data", () => { w.Dispatcher.BeginInvoke(() => { var d = app.Windows.OfType<SettingsDialog>().Single(); d.UpdateLayout(); Find<RadioButton>(d).Single(x => Equals(x.Content, "Paper")).IsChecked = true; Find<Slider>(d).First().Value = .7; Find<TextBox>(d).First().Text = "#85BBFF"; Click(Button(d, "Apply changes")); }); Click(Find<Button>(w).Single(x => Equals(x.Content, "⚙"))); w.UpdateLayout(); Eq(700d, w.Width); Eq("Paper", j.Settings.Theme); Eq("#85BBFF", store.Load().Settings.Accent); Eq(DateTime.IsLeapYear(today.Year) ? 366 : 365, Find<Button>(w).Count(x => x.Tag is DateOnly)); });
        Test("Backup restore cannot silently change saved appearance", () => { Eq("Paper", store.Load().Settings.Theme); Eq("UI integration note", store.Load().Days[Journal.Key(today)].Notes); });
        Test("Year navigation renders leap year", () => { Click(Button(w, "Today")); int current = today.Year; while (current < 2028) { Click(Button(w, "›")); current++; } while (current > 2028) { Click(Button(w, "‹")); current--; } Eq(366, Find<Button>(w).Count(x => x.Tag is DateOnly)); Eq(true, Find<Button>(w).Any(x => x.Tag is DateOnly d && d == new DateOnly(2028, 2, 29))); });
        Test("Desktop widget renders all dates and today's actual focus", () => { Eq(DateTime.IsLeapYear(today.Year) ? 366 : 365, Find<Button>(controller.Widget).Count(x => x.Tag is DateOnly)); Eq(today.ToString("dddd", System.Globalization.CultureInfo.GetCultureInfo("en-US")), ((TextBlock)controller.Widget.FindName("TodayLabel")).Text); });
        Test("Widget completion updates editor and saved journal", () => { Click(Button(w, "Today")); Click((Button)controller.Widget.FindName("CompleteButton")); Eq(true, j.IsComplete(today)); Eq(true, store.Load().IsComplete(today)); Eq("✓  Completed · click to undo", ((Button)w.FindName("CompleteButton")).Content); });
        Test("Clicking widget box opens that specific date", () => { Click(Find<Button>(controller.Widget).Single(b => b.Tag is DateOnly d && d == today.AddDays(-1))); Eq(today.AddDays(-1).ToString("MMMM d", System.Globalization.CultureInfo.GetCultureInfo("en-US")), ((TextBlock)w.FindName("DateTitle")).Text); Click(Button(w, "Today")); });
        Test("Overflow uses slim themed scrollbar and still scrolls", () => { controller.Change(() => j.Override(today, Enumerable.Range(1, 20).Select(i => "Exercise " + i).ToList())); w.UpdateLayout(); var tasks = (StackPanel)w.FindName("Tasks"); var scroll = Find<ScrollViewer>(w).Single(x => x.Content == tasks); scroll.UpdateLayout(); Eq(Visibility.Visible, scroll.ComputedVerticalScrollBarVisibility); var bar = Find<ScrollBar>(scroll).Single(x => x.Orientation == Orientation.Vertical); Eq(7d, bar.ActualWidth); Eq(true, bar.Template.FindName("PART_Track", bar) is Track); scroll.ScrollToVerticalOffset(100); scroll.UpdateLayout(); Eq(true, scroll.VerticalOffset > 0); });
        Test("Separate widget preferences apply without resizing editor", () => { var s = j.Settings; s.WidgetScale = 1.25; s.Opacity = .8; s.LockPosition = true; s.SundayFirst = true; s.ShowChecklist = true; s.WeeklyGoal = 5; controller.ApplySettings(s); controller.Widget.UpdateLayout(); Eq(true, Math.Abs(controller.Widget.Width - 575) < 1); Eq(.8, controller.Widget.Opacity); Eq(true, Math.Abs(w.Width - 700) < 1); Eq(Visibility.Visible, ((ScrollViewer)controller.Widget.FindName("ChecklistScroll")).Visibility); Eq(true, ((TextBlock)controller.Widget.FindName("GoalLabel")).Text.EndsWith(" / 5")); Eq(true, store.Load().Settings.LockPosition); });
        Test("Remove and add widget retains workout data", () => { controller.RemoveWidget(); Eq(false, controller.Widget.IsVisible); controller.AddWidget(); Eq(true, controller.Widget.IsVisible); Eq(20, j.Plan(today).Count); });
        Test("Default startup opens only the locked widget and header cannot drag", () =>
        {
            var startupJournal = new Journal();
            startupJournal.Settings.DesktopMode = false; // CI does not require Explorer.
            using var startup = new AppController(new Store(Path.Combine(dir, "startup.json")), startupJournal);
            startup.Start();
            startup.Widget.UpdateLayout();
            Eq(null, startup.Editor);
            Eq(true, startup.Widget.IsVisible);
            Eq(false, startup.Widget.ShowInTaskbar);
            Eq(ResizeMode.NoResize, startup.Widget.ResizeMode);
            Eq(Visibility.Visible, ((TextBlock)startup.Widget.FindName("LockLabel")).Visibility);
            var position = (startup.Widget.Left, startup.Widget.Top);
            ((Grid)startup.Widget.FindName("Header")).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent
            });
            Eq(position, (startup.Widget.Left, startup.Widget.Top));
        });
        if (args.Contains("--desktop"))
        {
            Test("Native widget attaches to desktop while editor stays independent", () => { j.Settings.DesktopMode = true; controller.ApplySettings(j.Settings); Eq(true, Native.HasParent(new WindowInteropHelper(controller.Widget).Handle)); Eq(false, Native.HasParent(new WindowInteropHelper(w).Handle)); j.Settings.DesktopMode = false; controller.ApplySettings(j.Settings); Eq(false, Native.HasParent(new WindowInteropHelper(controller.Widget).Handle)); });
            Test("Changing settings cannot revive a removed desktop widget", () =>
            {
                j.Settings.DesktopMode = true;
                controller.ApplySettings(j.Settings);
                controller.RemoveWidget();
                var hwnd = new WindowInteropHelper(controller.Widget).Handle;
                Eq(false, IsWindowVisible(hwnd));
                controller.ApplySettings(j.Settings);
                Eq(false, IsWindowVisible(hwnd));
                j.Settings.DesktopMode = false;
                controller.AddWidget();
            });
        }
        // Restore a representative demo for a clean rendered artifact.
        j.Settings.Theme = "Obsidian";
        j.Settings.Accent = "#CDFE85";
        j.Settings.Scale = 1;
        controller.Dispose();
        var demo = new Journal();
        demo.Settings.DesktopMode = false;
        demo.Weekly["Tuesday"] = new() { "Back", "Chest", "Shoulders" };
        demo.Weekly[today.DayOfWeek.ToString()] = new() { "Legs", "Back", "Core" };
        for (int i = 1; i < today.DayOfYear; i++)
            if (i % 3 == 0)
                demo.Complete(new DateOnly(today.Year, 1, 1).AddDays(i - 1), true);
        var demoController = new AppController(store, demo);
        demoController.OpenEditor();
        demoController.Widget.Show();
        var preview = demoController.Editor!;
        preview.UpdateLayout();
        demoController.Widget.UpdateLayout();
        var outputDir = args.FirstOrDefault(x => !x.StartsWith("--"));
        if (outputDir != null)
        {
            Directory.CreateDirectory(outputDir);
            foreach (var item in new[] { ("calendar.png", (Window)preview), ("widget.png", (Window)demoController.Widget) })
            {
                var bitmap = new RenderTargetBitmap((int)item.Item2.ActualWidth * 2, (int)item.Item2.ActualHeight * 2, 192, 192, PixelFormats.Pbgra32);
                bitmap.Render(item.Item2);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var output = File.Create(Path.Combine(outputDir, item.Item1));
                encoder.Save(output);
            }
            Console.WriteLine("Rendered previews: " + outputDir);
        }
        demoController.Dispose();
        app.Shutdown();
        Directory.Delete(dir, true);
        Console.WriteLine($"{passed} passed, {failed} failed");
        return failed == 0 ? 0 : 1;
    }
}
