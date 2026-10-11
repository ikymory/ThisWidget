using ThisWidget;

int passed = 0, failed = 0;
void Test(string name, Action action) { try { action(); Console.WriteLine("PASS " + name); passed++; } catch (Exception e) { Console.WriteLine("FAIL " + name + ": " + e.Message); failed++; } }
void Eq<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}"); }
void Yes(bool value) { if (!value) throw new Exception("Expected true"); }
var today = new DateOnly(2026, 10, 11);
Test("Calendar has every date once, including leap day", () => { var y = Calendar.Days(2026); Eq(365, y.Count); Eq(365, y.Distinct().Count()); Eq(new DateOnly(2026, 12, 31), y.Last()); var l = Calendar.Days(2028); Eq(366, l.Count); Yes(l.Contains(new DateOnly(2028, 2, 29))); });
Test("Monday alignment crosses Sunday correctly", () => { Eq(3, Calendar.MondayOffset(new DateOnly(2026, 1, 1))); Eq(6, Calendar.MondayOffset(today)); Eq(0, Calendar.MondayOffset(today.AddDays(1))); });
Test("Weekday plan automatically applies to Tuesday", () => { var s = new Journal(); s.Weekly["Tuesday"] = new() { "Back", "Chest", "Shoulders" }; Eq("Back,Chest,Shoulders", string.Join(',', s.Plan(new DateOnly(2026, 10, 13)))); Eq(0, s.Plan(today).Count); });
Test("Checking all exercises fills day, unchecking clears day", () => { var s = new Journal(); s.Weekly["Sunday"] = new() { "Gym", "Walk" }; s.CheckTask(today, "Gym", true); Yes(!s.IsComplete(today)); s.CheckTask(today, "Walk", true); Yes(s.IsComplete(today)); s.CheckTask(today, "Gym", false); Yes(!s.IsComplete(today)); });
Test("Manual complete works on unplanned day and can be undone", () => { var s = new Journal(); s.Complete(today, true); Yes(s.IsComplete(today)); s.Complete(today, false); Yes(!s.IsComplete(today)); });
Test("Recorded history freezes recurring plan", () => { var s = new Journal(); s.Weekly["Sunday"] = new() { "Chest" }; s.CheckTask(today, "Chest", true); s.Weekly["Sunday"] = new() { "Legs" }; Eq("Chest", s.Plan(today).Single()); Eq("Legs", s.Plan(today.AddDays(7)).Single()); });
Test("Empty daily override clears inherited plan", () => { var s = new Journal(); s.Weekly["Sunday"] = new() { "Gym" }; s.Override(today, new()); Eq(0, s.Plan(today).Count); });
Test("Editing one day does not change other dates", () => { var s = new Journal(); s.Weekly["Sunday"] = new() { "Gym" }; s.Override(today, new() { "Walk" }); Eq("Gym", s.Plan(today.AddDays(7)).Single()); Eq("Walk", s.Plan(today).Single()); });
Test("Normalization removes blank and duplicate exercises", () => { Eq("Back,Chest,Shoulders", string.Join(',', Journal.ParsePlan(" Back, chest, BACK, , Shoulders "))); });
Test("Streak tolerates unfinished today but does not count future dates", () => { var s = new Journal(); s.Complete(today.AddDays(-1), true); s.Complete(today.AddDays(-2), true); s.Complete(today.AddDays(1), true); Eq(2, s.Streak(today)); s.Complete(today, true); Eq(3, s.Streak(today)); });
Test("Future completion rejected by interactive validation", () => { Yes(!Calendar.CanComplete(today.AddDays(1), today)); Yes(Calendar.CanComplete(today, today)); });
Test("Year statistics exclude other years and future workouts", () => { var s = new Journal(); s.Complete(today, true); s.Complete(new(2025, 1, 1), true); s.Complete(today.AddDays(1), true); Eq(1, s.CompletedInYear(2026, today)); });
var dir = Path.Combine(Path.GetTempPath(), "ThisWidgetTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
Test("Atomic save restores tasks notes and appearance", () => { var store = new Store(Path.Combine(dir, "roundtrip.json")); var s = new Journal(); s.Weekly["Tuesday"] = new() { "Back" }; s.Weekly["Sunday"] = new() { "Gym" }; s.CheckTask(today, "Gym", true); s.Days[today.ToString("yyyy-MM-dd")].Notes = "Good session"; s.Settings.Accent = "#FFAA44"; store.Save(s); var loaded = store.Load(); Eq("Back", loaded.Weekly["Tuesday"].Single()); Yes(loaded.IsComplete(today)); Eq("Good session", loaded.Days["2026-10-11"].Notes); Eq("#FFAA44", loaded.Settings.Accent); });
Test("Corrupt primary recovers previous valid backup", () => { var p = Path.Combine(dir, "recover.json"); var st = new Store(p); var s = new Journal(); s.Weekly["Tuesday"] = new() { "Back" }; st.Save(s); s.Weekly["Tuesday"] = new() { "Chest" }; st.Save(s); File.WriteAllText(p, "broken"); var l = st.Load(); Eq("Back", l.Weekly["Tuesday"].Single()); Yes(st.RecoveryMessage.Length > 0); });
Test("Invalid import cannot replace journal", () => { var st = new Store(Path.Combine(dir, "valid.json")); var s = new Journal(); s.Complete(today, true); st.Save(s); bool rejected = false; try { Store.ReadJson("{\"Schema\":999}"); } catch { rejected = true; } Yes(rejected); Yes(st.Load().IsComplete(today)); });
Test("Appearance and malformed records normalize safely", () => { var s = Store.ReadJson("{\"Schema\":1,\"Settings\":{\"Scale\":999,\"Accent\":\"bad\"},\"Days\":{\"invalid\":{\"Completed\":true}},\"Weekly\":null}"); Eq(1.25, s.Settings.Scale); Eq("#CDFE85", s.Settings.Accent); Eq(0, s.Days.Count); Eq(7, s.Weekly.Count); });
Test("Migration preserves legacy progress without modifying the original", () => { var source = Path.Combine(dir, "old.json"); var dest = Path.Combine(dir, "new.json"); var s = new Journal(); s.Complete(today, true); new Store(source).Save(s); var before = File.ReadAllText(source); Store.MigrateLegacy(source, dest); Eq(true, new Store(dest).Load().IsComplete(today)); Eq(before, File.ReadAllText(source)); });
Test("Migration cannot overwrite an existing journal", () => { var source = Path.Combine(dir, "old2.json"); var dest = Path.Combine(dir, "new2.json"); var legacy = new Journal(); legacy.Complete(today, true); new Store(source).Save(legacy); new Store(dest).Save(new Journal()); Store.MigrateLegacy(source, dest); Eq(false, new Store(dest).Load().IsComplete(today)); });
Test("Sunday week start and Monday week start differ correctly", () => { Eq(0, Calendar.WeekOffset(new DateOnly(2026, 10, 11), true)); Eq(6, Calendar.WeekOffset(new DateOnly(2026, 10, 11), false)); Eq(new DateOnly(2026, 10, 5), Calendar.WeekStart(new DateOnly(2026, 10, 11), false)); });
Test("Weekly goal counts across a year boundary without future days", () => { var s = new Journal(); s.Complete(new(2025, 12, 29), true); s.Complete(new(2026, 1, 1), true); s.Complete(new(2026, 1, 3), true); Eq(2, s.CompletedThisWeek(new(2026, 1, 2))); });
Test("Widget preferences clamp malformed values", () => { var s = new Journal(); s.Settings.WidgetScale = 20; s.Settings.Opacity = .01; s.Settings.WeeklyGoal = 99; s.Settings.ReminderHour = 50; s.Settings.ReminderMinute = -2; s.Normalize(); Eq(1.5, s.Settings.WidgetScale); Eq(.5, s.Settings.Opacity); Eq(7, s.Settings.WeeklyGoal); Eq(23, s.Settings.ReminderHour); Eq(0, s.Settings.ReminderMinute); });
Test("Clearing a checkbox does not mark unknown exercises complete", () => { var s = new Journal(); s.CheckTask(today, "Invented", true); Eq(0, s.Days.Count); });
Test("Fresh widget is fixed to the desktop by default", () => { var s = new Journal(); Yes(s.Settings.DesktopMode); Yes(s.Settings.LockPosition); Yes(s.Settings.WidgetEnabled); Yes(!s.Settings.AlwaysOnTop); });
Test("Malformed schema types recover the previous backup", () =>
{
    foreach (var schema in new[] { "null", "\"1\"", "true", "[]" })
    {
        var path = Path.Combine(dir, "schema-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new Store(path);
        var journal = new Journal();
        journal.Complete(today, true);
        store.Save(journal);
        store.Save(journal);
        File.WriteAllText(path, "{\"Schema\":" + schema + "}");
        Yes(store.Load().IsComplete(today));
        Yes(store.RecoveryMessage.Length > 0);
    }
});
Test("Long-term notes round-trip beyond the old five-million-character limit", () =>
{
    var journal = new Journal();
    string note = new('x', 8000);
    for (int i = 0; i < 660; i++)
        journal.Record(today.AddDays(-i)).Notes = note;
    var store = new Store(Path.Combine(dir, "long-history.json"));
    store.Save(journal);
    var restored = store.Load();
    Eq(660, restored.Days.Count);
    Eq(note, restored.Days[Journal.Key(today.AddDays(-659))].Notes);
});
Directory.Delete(dir, true);
Console.WriteLine($"{passed} passed, {failed} failed");
return failed == 0 ? 0 : 1;
