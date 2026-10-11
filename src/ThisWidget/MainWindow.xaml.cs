using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ThisWidget;

public partial class MainWindow : Window
{
    private readonly AppController controller;
    private Journal Journal => controller.Journal;
    private DateOnly selected, previousToday;
    private int year;
    private bool refreshing, notesDirty;
    private readonly DispatcherTimer noteTimer = new() { Interval = TimeSpan.FromMilliseconds(650) };
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public MainWindow(AppController controller)
    {
        this.controller = controller;
        selected = previousToday = controller.Today;
        year = selected.Year;
        InitializeComponent();
        ApplyAppearance();
        controller.Changed += Render;
        noteTimer.Tick += (_, _) => FlushNotes();
        Closing += (_, e) => { FlushNotes(); if (!controller.IsExiting) { e.Cancel = true; Hide(); } };
        Closed += (_, _) => { noteTimer.Stop(); controller.Changed -= Render; };
        Render();
    }

    public void ApplyAppearance()
    {
        Width = 1000 * Journal.Settings.Scale;
        Height = 690 * Journal.Settings.Scale;
        var area = SystemParameters.WorkArea;
        Left = Math.Clamp(Journal.Settings.Left, area.Left, Math.Max(area.Left, area.Right - Width));
        Top = Math.Clamp(Journal.Settings.Top, area.Top, Math.Max(area.Top, area.Bottom - Height));
    }

    public void SelectDate(DateOnly date)
    {
        FlushNotes();
        selected = date;
        year = date.Year;
        Render();
    }

    private void Render()
    {
        refreshing = true;
        try
        {
            var today = controller.Today;
            if (today != previousToday && selected == previousToday)
            {
                selected = today;
                year = today.Year;
            }
            previousToday = today;
            YearLabel.Text = year.ToString();
            Subtitle.Text = $"{(DateTime.IsLeapYear(year) ? 366 : 365)} small steps. One stronger you.";
            Sessions.Text = Journal.CompletedInYear(year, today).ToString();
            Streak.Text = Journal.Streak(today).ToString();
            Months.Children.Clear();
            for (int month = 1; month <= 12; month++)
                Months.Children.Add(BuildMonth(month));
            DateCaption.Text = selected == today ? "TODAY'S TRAINING" : selected > today ? "UPCOMING TRAINING" : "YOUR TRAINING";
            DateTitle.Text = selected.ToString("MMMM d", English);
            DateWeekday.Text = selected.ToString("dddd · yyyy", English);
            Tasks.Children.Clear();
            var plan = Journal.Plan(selected);
            Journal.Days.TryGetValue(Journal.Key(selected), out var record);
            if (plan.Count == 0)
            {
                Tasks.Children.Add(new TextBlock { Text = "A fresh start.", FontSize = 17, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 4, 0, 7) });
                Tasks.Children.Add(new TextBlock
                {
                    Text = "Add a focus for this date, or set your repeating weekly split.",
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = Theme.Brush("Muted"),
                    LineHeight = 18
                });
            }
            foreach (string task in plan)
            {
                var check = new CheckBox
                {
                    Content = new TextBlock { Text = task, TextWrapping = TextWrapping.Wrap, MaxWidth = 180 },
                    IsChecked = record?.Checked.Contains(task) == true,
                    IsEnabled = selected <= today,
                    Margin = new Thickness(0, 0, 0, 10),
                    MinHeight = 28
                };
                AutomationProperties.SetName(check, "Exercise: " + task);
                check.Click += (_, _) => controller.Change(() => Journal.CheckTask(selected, task, check.IsChecked == true));
                Tasks.Children.Add(check);
            }
            if (Notes.Text != (record?.Notes ?? ""))
                Notes.Text = record?.Notes ?? "";
            CompleteButton.IsEnabled = selected <= today;
            CompleteButton.Content = selected > today ? "Available on this day" : Journal.IsComplete(selected) ? "✓  Completed · click to undo" : "✓  Mark workout complete";
            CompleteButton.Background = Theme.Brush(Journal.IsComplete(selected) ? "Cell" : "Accent");
            CompleteButton.Foreground = Theme.Brush(Journal.IsComplete(selected) ? "Accent" : "AccentInk");
            SaveStatus.Text = controller.SaveStatus;
        }
        finally { refreshing = false; }
    }

    private Border BuildMonth(int month)
    {
        var first = new DateOnly(year, month, 1);
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.Children.Add(new TextBlock { Text = first.ToString("MMM", English).ToUpperInvariant(), FontSize = 10, FontWeight = FontWeights.SemiBold });
        int count = Enumerable.Range(1, DateTime.DaysInMonth(year, month)).Count(day => Journal.IsComplete(new(year, month, day)));
        if (count > 0)
            grid.Children.Add(new TextBlock { Text = count + " ✓", Foreground = Theme.Brush("Accent"), HorizontalAlignment = HorizontalAlignment.Right, FontSize = 9 });
        var weekday = new UniformGrid { Columns = 7 };
        string[] labels = Journal.Settings.SundayFirst ? new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" } : new[] { "Mo", "Tu", "We", "Th", "Fr", "Sa", "Su" };
        foreach (string label in labels)
            weekday.Children.Add(new TextBlock { Text = label, FontSize = 7.5, Foreground = Theme.Brush("Muted"), HorizontalAlignment = HorizontalAlignment.Center });
        Grid.SetRow(weekday, 1);
        grid.Children.Add(weekday);
        var days = new UniformGrid { Columns = 7, Rows = 6 };
        int offset = Calendar.WeekOffset(first, Journal.Settings.SundayFirst);
        for (int i = 0; i < 42; i++)
        {
            int day = i - offset + 1;
            if (day < 1 || day > DateTime.DaysInMonth(year, month))
            {
                days.Children.Add(new Border());
                continue;
            }
            var date = new DateOnly(year, month, day);
            bool complete = Journal.IsComplete(date);
            var button = new Button
            {
                Style = (Style)FindResource("DayCell"),
                Content = Journal.Settings.ShowDayNumbers ? day.ToString() : "",
                FontSize = 9,
                FontWeight = complete ? FontWeights.Bold : FontWeights.Normal,
                Padding = new Thickness(0),
                Margin = new Thickness(1),
                BorderThickness = new Thickness(date == selected ? 1.8 : date == controller.Today ? 1 : 0),
                BorderBrush = Theme.Brush(date == selected ? "Ink" : "Accent"),
                Background = Theme.Brush(complete ? "Accent" : "Cell"),
                Foreground = Theme.Brush(complete ? "AccentInk" : date > controller.Today ? "Muted" : "Ink"),
                ToolTip = date.ToString("dddd, MMMM d, yyyy", English) + "\n" +
                    (Journal.Plan(date).Count > 0 ? string.Join(" · ", Journal.Plan(date)) : "No workout planned") +
                    (complete ? "\n✓ Workout complete" : ""),
                Tag = date
            };
            AutomationProperties.SetName(button, date.ToString("dddd, MMMM d, yyyy", English) + (complete ? ", complete" : ""));
            button.Click += (_, _) => SelectDate(date);
            button.MouseRightButtonUp += (_, e) =>
            {
                if (date <= controller.Today)
                    controller.Change(() => Journal.Complete(date, !Journal.IsComplete(date)));
                e.Handled = true;
            };
            days.Children.Add(button);
        }
        Grid.SetRow(days, 2);
        grid.Children.Add(days);
        return new Border
        {
            Background = Theme.Brush("Card"),
            BorderBrush = Theme.Brush("Line"),
            BorderThickness = new Thickness(.7),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8),
            Margin = new Thickness(4),
            Child = grid
        };
    }

    public void FlushNotes()
    {
        noteTimer.Stop();
        if (!notesDirty)
            return;
        if (controller.Save(false))
            notesDirty = false;
        SaveStatus.Text = controller.SaveStatus;
    }

    private void NotesChanged(object sender, TextChangedEventArgs e)
    {
        if (refreshing)
            return;
        Journal.Record(selected).Notes = Notes.Text;
        notesDirty = true;
        SaveStatus.Text = "Saving…";
        noteTimer.Stop();
        noteTimer.Start();
    }
    private void NotesLostFocus(object sender, RoutedEventArgs e) => FlushNotes();
    private void TodayClick(object sender, RoutedEventArgs e) => SelectDate(controller.Today);
    private void PreviousYear(object sender, RoutedEventArgs e)
    {
        if (year > 1900)
            SelectDate(new(year - 1, 1, 1));
    }
    private void NextYear(object sender, RoutedEventArgs e)
    {
        if (year < 2100)
            SelectDate(new(year + 1, 1, 1));
    }
    private void CompleteClick(object sender, RoutedEventArgs e)
    {
        if (selected <= controller.Today)
            controller.Change(() => Journal.Complete(selected, !Journal.IsComplete(selected)));
    }
    private void WeeklyClick(object sender, RoutedEventArgs e)
    {
        FlushNotes();
        var dialog = new WeeklyDialog(this, Journal.Weekly);
        if (dialog.ShowDialog() == true)
            controller.Change(() => Journal.Weekly = dialog.Result);
    }
    private void EditDayClick(object sender, RoutedEventArgs e)
    {
        FlushNotes();
        var dialog = new DayDialog(this, selected, Journal.Plan(selected));
        if (dialog.ShowDialog() != true)
            return;
        controller.Change(() =>
        {
            if (!dialog.UseWeekly)
                Journal.Override(selected, Journal.ParsePlan(dialog.Input.Text));
            else
            {
                var record = Journal.Record(selected);
                record.Tasks = Journal.Weekly[selected.DayOfWeek.ToString()].ToList();
                record.Checked.Clear();
                record.Completed = false;
            }
        });
    }
    public void ShowSettings()
    {
        FlushNotes();
        var dialog = new SettingsDialog(this, Journal.Settings, controller.Export, controller.Import, controller.SaveImage, controller.ResetWidgetPosition);
        if (dialog.ShowDialog() == true)
            controller.ApplySettings(dialog.Result);
    }
    private void SettingsClick(object sender, RoutedEventArgs e) => ShowSettings();
    private void WidgetClick(object sender, RoutedEventArgs e) => controller.AddWidget();
    private void HideClick(object sender, RoutedEventArgs e)
    {
        FlushNotes();
        WindowState = WindowState.Minimized;
    }
    private void CloseClick(object sender, RoutedEventArgs e) => Close();
    private void DragHeaderDown(object sender, MouseButtonEventArgs e)
    {
        var item = e.OriginalSource as DependencyObject;
        while (item != null)
        {
            if (item is ButtonBase)
                return;
            item = VisualTreeHelper.GetParent(item);
        }
        DragMove();
        Journal.Settings.Left = Left;
        Journal.Settings.Top = Top;
        controller.Save(false);
    }
}
