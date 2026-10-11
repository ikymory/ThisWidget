using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Text.RegularExpressions;

namespace ThisWidget;

public class Dialog : Window
{
    protected StackPanel Body = new();
    protected StackPanel Actions = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
    protected Brush Brush(string key) => (Brush)FindResource(key);
    public Dialog(Window owner, string title, string subtitle, double height = 540)
    {
        Owner = owner;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Width = 540;
        Height = height;
        Title = title;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 12;
        Foreground = owner.Foreground;
        Resources.MergedDictionaries.Add(owner.Resources);
        var root = new Grid { Margin = new Thickness(28) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };
        heading.Children.Add(new TextBlock { Text = title, FontSize = 26, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = subtitle, TextWrapping = TextWrapping.Wrap, Foreground = Brush("Muted"), LineHeight = 19, Margin = new Thickness(0, 7, 0, 0) });
        heading.MouseLeftButtonDown += (s, e) => { if (e.ClickCount == 1) DragMove(); };
        root.Children.Add(heading);
        var scroll = new ScrollViewer { Content = Body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);
        Grid.SetRow(Actions, 2);
        Actions.Margin = new Thickness(0, 20, 0, 0);
        root.Children.Add(Actions);
        Content = new Border { Child = root, Background = Brush("Surface"), BorderBrush = Brush("Line"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18) };
        var cancel = Action("Cancel", () => DialogResult = false);
        cancel.IsCancel = true;
    }
    protected TextBlock Label(string text)
    {
        var b = new TextBlock { Text = text, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brush("Muted"), Margin = new Thickness(0, 14, 0, 7) };
        Body.Children.Add(b);
        return b;
    }
    protected Button Action(string label, Action action, bool primary = false)
    {
        var b = new Button { Content = label, Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(18, 10, 18, 10) };
        if (primary)
        {
            b.Background = Brush("Accent");
            b.Foreground = Brush("AccentInk");
            b.BorderThickness = new Thickness(0);
        }
        b.Click += (s, e) => action();
        Actions.Children.Add(b);
        return b;
    }
}
public class WeeklyDialog : Dialog
{
    readonly Dictionary<string, TextBox> inputs = new();
    public Dictionary<string, List<string>> Result { get; private set; } = new();
    public WeeklyDialog(Window owner, Dictionary<string, List<string>> current) : base(owner, "Your weekly rhythm.", "Set what you train on each weekday. This repeats automatically; previously logged sessions keep their original plan.", 665)
    {
        foreach (var day in new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" })
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.Children.Add(new TextBlock { Text = day, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold });
            var box = new TextBox { Text = current.TryGetValue(day, out var p) ? string.Join(", ", p) : "", MaxLength = 1700, ToolTip = "Separate workout focus with commas. Leave empty for a rest day." };
            System.Windows.Automation.AutomationProperties.SetName(box, day + " workout plan");
            Grid.SetColumn(box, 1);
            row.Children.Add(box);
            inputs[day] = box;
            Body.Children.Add(row);
        }
        Body.Children.Add(new TextBlock { Text = "Example: Back, Chest, Shoulders\nLeave a weekday empty for rest / no planned workout.", Foreground = Brush("Muted"), LineHeight = 20 });
        var preset = new Button { Content = "Load example split", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0) };
        preset.Click += (s, e) => { string[] p = { "Chest, Triceps", "Back, Biceps", "Legs", "Shoulders, Core", "Full body", "Walk, Mobility", "" }; int i = 0; foreach (var box in inputs.Values) box.Text = p[i++]; };
        Body.Children.Add(preset);
        Action("Save weekly split", () => { Result = inputs.ToDictionary(x => x.Key, x => Journal.ParsePlan(x.Value.Text)); DialogResult = true; }, true);
    }
}
public class DayDialog : Dialog
{
    public TextBox Input { get; } = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 175, MaxLength = 1700, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    public bool UseWeekly
    {
        get; private set;
    }
    public DayDialog(Window owner, DateOnly date, List<string> tasks) : base(owner, "Make it your session.", date.ToString("dddd, MMMM d, yyyy", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + " · only this date changes", 440)
    {
        Label("WORKOUT FOCUS · ONE PER LINE OR SEPARATED BY COMMAS");
        Input.Text = string.Join(Environment.NewLine, tasks);
        Body.Children.Add(Input);
        var reset = new Button { Content = "Use recurring weekday plan", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0) };
        reset.Click += (s, e) => { UseWeekly = true; DialogResult = true; };
        Body.Children.Add(reset);
        Action("Save day", () => DialogResult = true, true);
    }
}
public class SettingsDialog : Dialog
{
    public Appearance Result
    {
        get; private set;
    }
    public SettingsDialog(Window owner, Appearance settings, Action export, Action import, Action image, Action resetPosition) : base(owner, "Make it yours.", "Fine-tune your calendar and desktop widget. Your workouts stay saved on this device.", 735)
    {
        Result = JsonSerializer.Deserialize<Appearance>(JsonSerializer.Serialize(settings))!;
        Label("ACCENT COLOR");
        var swatches = new StackPanel { Orientation = Orientation.Horizontal };
        var accent = new TextBox { Text = settings.Accent, Width = 110, Margin = new Thickness(16, 0, 0, 0), MaxLength = 7 };
        foreach (var color in new[] { "#CDFE85", "#6DE3C3", "#85BBFF", "#C4A2FF", "#FFB68A", "#FB7A92" })
        {
            var b = new Button { Width = 34, Height = 34, Margin = new Thickness(0, 0, 7, 0), Padding = new Thickness(0), Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)), BorderThickness = new Thickness(0), ToolTip = color };
            b.Click += (s, e) => accent.Text = color;
            swatches.Children.Add(b);
        }
        swatches.Children.Add(accent);
        Body.Children.Add(swatches);
        Label("BACKGROUND");
        var theme = new StackPanel { Orientation = Orientation.Horizontal };
        var dark = new RadioButton { Content = "Obsidian", GroupName = "Theme", IsChecked = settings.Theme != "Paper", Margin = new Thickness(0, 0, 26, 0), Foreground = Brush("Ink") };
        var light = new RadioButton { Content = "Paper", GroupName = "Theme", IsChecked = settings.Theme == "Paper", Foreground = Brush("Ink") };
        theme.Children.Add(dark);
        theme.Children.Add(light);
        Body.Children.Add(theme);
        var sizeLabel = Label("CALENDAR SIZE · " + Math.Round(settings.Scale * 100) + "%");
        var slider = new Slider { Minimum = .7, Maximum = 1.25, Value = settings.Scale, TickFrequency = .05, IsSnapToTickEnabled = true };
        slider.ValueChanged += (s, e) => sizeLabel.Text = "CALENDAR SIZE · " + Math.Round(slider.Value * 100) + "%";
        Body.Children.Add(slider);
        var widgetLabel = Label("DESKTOP WIDGET SIZE · " + Math.Round(settings.WidgetScale * 100) + "%");
        var widgetSize = new Slider { Minimum = .7, Maximum = 1.5, Value = settings.WidgetScale, TickFrequency = .05, IsSnapToTickEnabled = true };
        widgetSize.ValueChanged += (s, e) => widgetLabel.Text = "DESKTOP WIDGET SIZE · " + Math.Round(widgetSize.Value * 100) + "%";
        Body.Children.Add(widgetSize);
        var opacityLabel = Label("WIDGET OPACITY · " + Math.Round(settings.Opacity * 100) + "%");
        var opacity = new Slider { Minimum = .5, Maximum = 1, Value = settings.Opacity, TickFrequency = .05, IsSnapToTickEnabled = true };
        opacity.ValueChanged += (s, e) => opacityLabel.Text = "WIDGET OPACITY · " + Math.Round(opacity.Value * 100) + "%";
        Body.Children.Add(opacity);
        Label("DESKTOP BEHAVIOR");
        var dock = new CheckBox { Content = "Attach to desktop, behind other windows", IsChecked = settings.DesktopMode, Margin = new Thickness(0, 0, 0, 12) };
        var top = new CheckBox { Content = "Keep floating widget on top", IsChecked = settings.AlwaysOnTop, Margin = new Thickness(0, 0, 0, 12) };
        var startup = new CheckBox { Content = "Launch when I sign in to Windows", IsChecked = settings.StartWithWindows, IsEnabled = Program.Integrate, Margin = new Thickness(0, 0, 0, 12) };
        Body.Children.Add(dock);
        Body.Children.Add(top);
        Body.Children.Add(startup);
        Body.Children.Add(new TextBlock { Text = "The widget stays fixed behind apps by default. Unlock its position\nto move it, then lock again. Ctrl+Alt+W opens the calendar editor.", TextWrapping = TextWrapping.Wrap, Foreground = Brush("Muted"), FontSize = 11, LineHeight = 18 });
        var enabled = new CheckBox { Content = "Show desktop widget", IsChecked = settings.WidgetEnabled, Margin = new Thickness(0, 12, 0, 12) };
        var locked = new CheckBox { Content = "Lock widget position", IsChecked = settings.LockPosition, Margin = new Thickness(0, 0, 0, 12) };
        var checklist = new CheckBox { Content = "Show exercise checklist in widget", IsChecked = settings.ShowChecklist, Margin = new Thickness(0, 0, 0, 12) };
        Body.Children.Add(enabled);
        Body.Children.Add(locked);
        Body.Children.Add(checklist);
        var reset = new Button { Content = "Reset widget position", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 4) };
        reset.Click += (s, e) => { resetPosition(); Result.WidgetLeft = -1; Result.WidgetTop = 60; };
        Body.Children.Add(reset);
        Label("CALENDAR & GOALS");
        var sunday = new CheckBox { Content = "Start weeks on Sunday", IsChecked = settings.SundayFirst, Margin = new Thickness(0, 0, 0, 12) };
        var numbers = new CheckBox { Content = "Show day numbers in calendar boxes", IsChecked = settings.ShowDayNumbers, Margin = new Thickness(0, 0, 0, 12) };
        Body.Children.Add(sunday);
        Body.Children.Add(numbers);
        var goalLabel = Label("WEEKLY WORKOUT GOAL · " + settings.WeeklyGoal);
        var goal = new Slider { Minimum = 1, Maximum = 7, Value = settings.WeeklyGoal, TickFrequency = 1, IsSnapToTickEnabled = true };
        goal.ValueChanged += (s, e) => goalLabel.Text = "WEEKLY WORKOUT GOAL · " + (int)goal.Value;
        Body.Children.Add(goal);
        Label("OPTIONAL REMINDER");
        var remind = new CheckBox { Content = "Remind me if today's planned workout is unfinished", IsChecked = settings.ReminderEnabled, Margin = new Thickness(0, 0, 0, 10) };
        Body.Children.Add(remind);
        var reminderTime = new TextBox { Text = $"{settings.ReminderHour:00}:{settings.ReminderMinute:00}", Width = 90, HorizontalAlignment = HorizontalAlignment.Left, MaxLength = 5 };
        Body.Children.Add(reminderTime);
        Body.Children.Add(new TextBlock { Text = "Local time in HH:mm. Reminders require ThisWidget to be running.\nWindows notification preferences may suppress them.", Foreground = Brush("Muted"), FontSize = 11, LineHeight = 18, Margin = new Thickness(0, 7, 0, 0) });
        Label("YOUR DATA");
        var data = new WrapPanel();
        foreach (var item in new[] { ("Export backup", export), ("Restore backup", import), ("Save image", image) })
        {
            var b = new Button { Content = item.Item1, Margin = new Thickness(0, 0, 8, 8), FontSize = 11, Padding = new Thickness(10, 8, 10, 8) };
            b.Click += (s, e) => item.Item2();
            data.Children.Add(b);
        }
        Body.Children.Add(data);
        var error = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(255, 140, 130)), TextWrapping = TextWrapping.Wrap };
        Body.Children.Add(error);
        Action("Apply changes", () => { if (!Regex.IsMatch(accent.Text, "^#[0-9A-Fa-f]{6}$")) { error.Text = "Enter a six-digit hex color, for example #CDFE85."; return; } if (!TimeOnly.TryParseExact(reminderTime.Text, "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var time)) { error.Text = "Use HH:mm for reminder time, for example 18:30."; return; } Result.Accent = accent.Text.ToUpperInvariant(); Result.Theme = light.IsChecked == true ? "Paper" : "Obsidian"; Result.Scale = slider.Value; Result.WidgetScale = widgetSize.Value; Result.Opacity = opacity.Value; Result.DesktopMode = dock.IsChecked == true; Result.AlwaysOnTop = top.IsChecked == true; Result.StartWithWindows = startup.IsChecked == true; Result.WidgetEnabled = enabled.IsChecked == true; Result.LockPosition = locked.IsChecked == true; Result.ShowChecklist = checklist.IsChecked == true; Result.SundayFirst = sunday.IsChecked == true; Result.ShowDayNumbers = numbers.IsChecked == true; Result.WeeklyGoal = (int)goal.Value; Result.ReminderEnabled = remind.IsChecked == true; Result.ReminderHour = time.Hour; Result.ReminderMinute = time.Minute; DialogResult = true; }, true);
    }
}
