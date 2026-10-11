using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ThisWidget;

public static class Calendar
{
    public static List<DateOnly> Days(int year) => Enumerable.Range(0, DateTime.IsLeapYear(year) ? 366 : 365).Select(i => new DateOnly(year, 1, 1).AddDays(i)).ToList();
    public static int MondayOffset(DateOnly day) => ((int)day.DayOfWeek + 6) % 7;
    public static bool CanComplete(DateOnly date, DateOnly today) => date <= today;
    public static int WeekOffset(DateOnly day, bool sundayFirst) => sundayFirst ? (int)day.DayOfWeek : MondayOffset(day);
    public static DateOnly WeekStart(DateOnly day, bool sundayFirst) => DateOnly.FromDayNumber(Math.Max(0, day.DayNumber - WeekOffset(day, sundayFirst)));
}
public class Appearance
{
    public string Accent { get; set; } = "#CDFE85";
    public string Theme { get; set; } = "Obsidian";
    public double Scale { get; set; } = 1;
    public double Left { get; set; } = 280;
    public double Top { get; set; } = 110;
    public bool DesktopMode { get; set; } = true;
    public bool AlwaysOnTop
    {
        get; set;
    }
    public bool StartWithWindows { get; set; } = true;
    public double WidgetScale { get; set; } = 1;
    public double Opacity { get; set; } = 1;
    public double WidgetLeft { get; set; } = -1;
    public double WidgetTop { get; set; } = 60;
    public bool WidgetEnabled { get; set; } = true;
    public bool LockPosition { get; set; } = true;
    public bool SundayFirst
    {
        get; set;
    }
    public bool ShowDayNumbers { get; set; } = true;
    public bool ShowChecklist
    {
        get; set;
    }
    public int WeeklyGoal { get; set; } = 3;
    public bool ReminderEnabled
    {
        get; set;
    }
    public int ReminderHour { get; set; } = 18;
    public int ReminderMinute
    {
        get; set;
    }
    public string LastReminderDate { get; set; } = "";
}
public class DayRecord
{
    public bool Completed
    {
        get; set;
    }
    public List<string>? Tasks
    {
        get; set;
    }
    public List<string> Checked { get; set; } = new();
    public string Notes { get; set; } = "";
}
public class Journal
{
    public int Schema { get; set; } = 1;
    public Appearance Settings { get; set; } = new();
    public Dictionary<string, List<string>> Weekly { get; set; } = Enum.GetNames<DayOfWeek>().ToDictionary(x => x, x => new List<string>());
    public Dictionary<string, DayRecord> Days { get; set; } = new();
    public static string Key(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public List<string> Plan(DateOnly day) => Days.TryGetValue(Key(day), out var record) && record.Tasks != null ? new(record.Tasks) : Weekly.TryGetValue(day.DayOfWeek.ToString(), out var plan) ? new(plan) : new();
    public bool IsComplete(DateOnly day) => Days.TryGetValue(Key(day), out var r) && r.Completed;
    public DayRecord Record(DateOnly day)
    {
        if (!Days.TryGetValue(Key(day), out var r))
        {
            r = new DayRecord { Tasks = Plan(day) };
            Days[Key(day)] = r;
        }
        r.Tasks ??= Plan(day);
        return r;
    }
    public void CheckTask(DateOnly date, string task, bool value)
    {
        if (!Plan(date).Contains(task))
            return;
        var r = Record(date);
        r.Checked.RemoveAll(x => x == task);
        if (value)
            r.Checked.Add(task);
        r.Completed = r.Tasks!.Count > 0 && r.Tasks.All(x => r.Checked.Contains(x));
    }
    public void Complete(DateOnly date, bool value)
    {
        var r = Record(date);
        r.Completed = value;
        r.Checked = value ? new(r.Tasks!) : new();
    }
    public void Override(DateOnly date, List<string> tasks)
    {
        var r = Record(date);
        r.Tasks = ParsePlan(string.Join(',', tasks));
        r.Checked = r.Checked.Where(x => r.Tasks.Contains(x)).ToList();
        r.Completed = r.Tasks.Count > 0 && r.Tasks.All(x => r.Checked.Contains(x));
    }
    public static List<string> ParsePlan(string text) => text.Split(new[] { ',', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).Select(x => char.ToUpperInvariant(x[0]) + x.Substring(1)).Select(x => x.Length > 80 ? x.Substring(0, 80) : x).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
    public int Streak(DateOnly today)
    {
        var day = IsComplete(today) ? today : today.AddDays(-1);
        int count = 0;
        while (IsComplete(day))
        {
            count++;
            if (day == DateOnly.MinValue)
                break;
            day = day.AddDays(-1);
        }
        return count;
    }
    public int CompletedInYear(int year, DateOnly today) => Days.Count(x => x.Value.Completed && DateOnly.TryParseExact(x.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d.Year == year && d <= today);
    public int CompletedThisWeek(DateOnly today)
    {
        var first = Calendar.WeekStart(today, Settings.SundayFirst);
        return Enumerable.Range(0, today.DayNumber - first.DayNumber + 1).Count(i => IsComplete(first.AddDays(i)));
    }
    public void Normalize()
    {
        Settings ??= new();
        Weekly ??= new();
        Days ??= new();
        Settings.Accent = Regex.IsMatch(Settings.Accent ?? "", "^#[0-9a-fA-F]{6}$") ? Settings.Accent!.ToUpperInvariant() : "#CDFE85";
        Settings.Theme = Settings.Theme == "Paper" ? "Paper" : "Obsidian";
        Settings.Scale = double.IsFinite(Settings.Scale) ? Math.Clamp(Settings.Scale, .7, 1.25) : 1;
        if (!double.IsFinite(Settings.Left))
            Settings.Left = 280;
        if (!double.IsFinite(Settings.Top))
            Settings.Top = 110;
        Settings.WidgetScale = double.IsFinite(Settings.WidgetScale) ? Math.Clamp(Settings.WidgetScale, .7, 1.5) : 1;
        Settings.Opacity = double.IsFinite(Settings.Opacity) ? Math.Clamp(Settings.Opacity, .5, 1) : 1;
        Settings.WeeklyGoal = Math.Clamp(Settings.WeeklyGoal, 1, 7);
        Settings.ReminderHour = Math.Clamp(Settings.ReminderHour, 0, 23);
        Settings.ReminderMinute = Math.Clamp(Settings.ReminderMinute, 0, 59);
        if (!double.IsFinite(Settings.WidgetLeft))
            Settings.WidgetLeft = -1;
        if (!double.IsFinite(Settings.WidgetTop))
            Settings.WidgetTop = 60;
        Weekly = Enum.GetNames<DayOfWeek>().ToDictionary(x => x, x => Weekly.TryGetValue(x, out var p) && p != null ? ParsePlan(string.Join(',', p)) : new List<string>());
        Days = Days.Where(x => x.Value != null && DateOnly.TryParseExact(x.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)).ToDictionary(x => x.Key, x => x.Value);
        foreach (var r in Days.Values)
        {
            r.Tasks = r.Tasks == null ? null : ParsePlan(string.Join(',', r.Tasks));
            r.Checked = (r.Checked ?? new()).Where(x => r.Tasks?.Contains(x) == true).Distinct().ToList();
            r.Notes ??= "";
            if (r.Notes.Length > 8000)
                r.Notes = r.Notes.Substring(0, 8000);
        }
    }
}
public class Store
{
    // Apply one bound to both reads and writes so a successful save always remains readable.
    private const int MaximumJournalLength = 64_000_000;
    public string RecoveryMessage { get; private set; } = "";
    public string PathName
    {
        get;
    }
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public Store(string path)
    {
        PathName = path;
    }
    public static void MigrateLegacy(string source, string destination)
    {
        if (File.Exists(destination) || !File.Exists(source))
            return;
        new Store(destination).Save(ReadJson(File.ReadAllText(source)));
    }
    public Journal Load()
    {
        if (!File.Exists(PathName))
            return new();
        try
        {
            return ReadJson(File.ReadAllText(PathName));
        }
        catch (Exception e) when (e is JsonException or InvalidDataException)
        {
            var quarantine = PathName + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmssfff");
            File.Copy(PathName, quarantine);
            if (File.Exists(PathName + ".bak"))
            {
                try
                {
                    var r = ReadJson(File.ReadAllText(PathName + ".bak"));
                    SaveCore(r, false);
                    RecoveryMessage = "Recovered your previous backup. The damaged file was preserved.";
                    return r;
                }
                catch (Exception backupError) when (backupError is JsonException or InvalidDataException) { }
            }
            throw new InvalidDataException("Your journal could not be read. It was preserved at " + quarantine + ". No data was overwritten.", e);
        }
    }
    public void Save(Journal state)
        => SaveCore(state, true);
    void SaveCore(Journal state, bool rotateBackup)
    {
        state.Normalize();
        var serialized = JsonSerializer.Serialize(state, JsonOptions);
        if (serialized.Length > MaximumJournalLength)
            throw new InvalidDataException("Journal exceeds the 64-million-character limit. Export a backup before reducing older notes.");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(PathName))!);
        var tmp = PathName + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(tmp, serialized);
            if (File.Exists(PathName))
                File.Replace(tmp, PathName, rotateBackup ? PathName + ".bak" : null, true);
            else
                File.Move(tmp, PathName);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }
    public static Journal ReadJson(string json)
    {
        if (json.Length > MaximumJournalLength)
            throw new InvalidDataException("Journal is too large.");
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("Schema", out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt32(out int schema) || schema != 1)
            throw new InvalidDataException("This is not a supported ThisWidget backup.");
        var r = JsonSerializer.Deserialize<Journal>(json) ?? throw new InvalidDataException("Empty journal.");
        r.Normalize();
        return r;
    }
}
