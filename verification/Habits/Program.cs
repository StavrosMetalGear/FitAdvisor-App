using FitAdvisor.Core;
using FitAdvisor.Storage;
using Microsoft.Data.Sqlite;
using System.Globalization;

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static async Task Reject(Func<Task> action) { try { await action(); } catch (ArgumentException) { return; } throw new Exception("Invalid operation succeeded"); }
static void Sql(string path, string sql) { using var db = new SqliteConnection($"Data Source={path};Pooling=False"); db.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery(); }
var root = Path.Combine(Path.GetTempPath(), "FitAdvisor-habits-" + Guid.NewGuid()); Directory.CreateDirectory(root);
try
{
    var day = new DateOnly(2026, 10, 5); var path = Path.Combine(root, "diary.db"); var diary = new SqlitePersonalDiary(path);
    await diary.SaveMealAsync(new(Guid.NewGuid(), day, "Existing meal", 1, "portion", 300));
    var old = await diary.CreateBackupAsync();
    Sql(path, "ALTER TABLE meals DROP COLUMN nutrition_snapshot; DROP TABLE habits; DROP TABLE habit_goals; DROP TABLE habit_settings; PRAGMA user_version=2;");
    Check((await diary.GetMealsAsync(day)).Count == 1 && !(await diary.GetHabitSettingsAsync()).Smoking, "v2 migration lost data or enabled tracker");
    var settings = new HabitSettings(true, true, true, false, day, 20, 0.25, "EUR"); await diary.SaveHabitSettingsAsync(settings);
    var goal = new HabitGoal(Guid.NewGuid(), "eating", "Takeaway", "times", 2); await diary.SaveHabitGoalAsync(goal);
    var zero = new HabitRecord(Guid.NewGuid(), day, "smoking", "Smoking", 0, Craving: 3, Trigger: "Coffee");
    var lapse = zero with { Id = Guid.NewGuid(), Date = day.AddDays(1), Amount = 5 };
    var beer = new HabitRecord(Guid.NewGuid(), day, "alcohol", "Beer", 500, 5);
    var eating = new HabitRecord(Guid.NewGuid(), day, "eating", "Takeaway", 1, GoalId: goal.Id);
    foreach (var record in new[] { zero, lapse, beer, eating }) await diary.SaveHabitAsync(record);
    Check(Math.Abs(beer.AlcoholGrams - 19.725) < 1e-10, "Alcohol calculation");
    Check(Math.Abs((beer with { Amount = 150, Abv = 12 }).AlcoholGrams - 14.202) < 1e-10, "Wine calculation");
    Check((beer with { Abv = 0 }).AlcoholGrams == 0, "Zero ABV");
    Check(HabitCalculations.MoneySaved(await diary.GetHabitsAsync(), settings) == 8.75, "Lapse reset savings or missing days counted");
    Check(HabitCalculations.WeekStart(new(2026, 10, 11)) == day, "Sunday week boundary");
    Check(HabitCalculations.WeekStart(new(2026, 10, 12)) == day.AddDays(7), "Monday week boundary");
    Check(HabitCalculations.WeekStart(new(2027, 1, 1)) == new DateOnly(2026, 12, 28), "Year boundary");
    Check(HabitCalculations.WeekStart(new(2024, 2, 29)) == new DateOnly(2024, 2, 26), "Leap date");
    Check(HabitCalculations.MoneySaved([zero], settings with { QuitDate = day.AddDays(10) }) == 0, "Planned quit date counted early savings");
    Check(HabitCalculations.MoneySaved([lapse with { Amount = 25 }], settings) == 0, "Savings went negative");
    Check(HabitCalculations.Total(await diary.GetHabitsAsync(), goal, day) == 1, "Goal totals");
    await Reject(() => diary.SaveHabitAsync(zero with { Id = Guid.NewGuid() }));
    await Reject(() => diary.SaveHabitAsync(lapse with { Date = day }));
    await Reject(() => diary.SaveHabitSettingsAsync(settings with { CostPerCigarette = -1 }));
    await Reject(() => diary.SaveHabitAsync(beer with { Amount = double.NaN }));
    await Reject(() => diary.SaveHabitAsync(beer with { Abv = 101 }));
    await Reject(() => diary.SaveHabitAsync(beer with { Abv = null }));
    await Reject(() => diary.SaveHabitAsync(zero with { Amount = 1.5 }));
    await Reject(() => diary.SaveHabitAsync(zero with { Craving = 11 }));
    await Reject(() => diary.SaveHabitAsync(zero with { Date = DateOnly.FromDateTime(DateTime.Today).AddDays(1) }));
    await Reject(() => diary.SaveHabitAsync(eating with { GoalId = Guid.NewGuid() }));
    await Reject(() => diary.DeleteHabitGoalAsync(goal.Id));
    await Reject(() => diary.SaveHabitGoalAsync(goal with { Unit = "ml" }));
    await diary.SaveHabitAsync(beer with { Amount = 330 });
    await diary.SaveHabitSettingsAsync(settings with { Alcohol = false });
    Check((await new SqlitePersonalDiary(path).GetHabitsAsync()).Length == 4, "Reopen/edit/disable lost records");
    var snapshot = await diary.CreateBackupAsync(); const string pass = "synthetic-habit-backup-password";
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("el-GR");
    var decoded = BackupCodec.Decrypt(BackupCodec.Encrypt(snapshot, pass), pass);
    var fresh = new SqlitePersonalDiary(Path.Combine(root, "fresh.db")); await fresh.RestoreBackupAsync(decoded);
    var restored = await fresh.CreateBackupAsync();
    Check(snapshot.Habits.SequenceEqual(restored.Habits) && snapshot.HabitGoals.SequenceEqual(restored.HabitGoals) && snapshot.HabitSettings == restored.HabitSettings && snapshot.Meals.SequenceEqual(restored.Meals), "Backup roundtrip");
    await Reject(() => fresh.RestoreBackupAsync(snapshot with { Habits = [eating with { GoalId = Guid.NewGuid() }] }));
    await Reject(() => fresh.RestoreBackupAsync(snapshot with { Habits = [zero, zero] }));
    await Reject(() => fresh.RestoreBackupAsync(snapshot with { HabitSettings = null! }));
    Sql(path, "CREATE TRIGGER reject_habit_restore BEFORE INSERT ON habits BEGIN SELECT RAISE(ABORT,'test'); END;");
    try { await diary.RestoreBackupAsync(snapshot); throw new Exception("Fault ignored"); } catch (SqliteException) { }
    Check((await diary.CreateBackupAsync()).Habits.SequenceEqual(snapshot.Habits), "Restore failure not atomic");
    Sql(path, "DROP TRIGGER reject_habit_restore;");
    await fresh.RestoreBackupAsync(old with { FormatVersion = 1 });
    Check((await fresh.GetHabitsAsync()).Length == 0 && !(await fresh.GetHabitSettingsAsync()).Smoking && (await fresh.GetMealsAsync(day)).Count == 1, "Legacy backup restore");
    await diary.DeleteHabitAsync(eating.Id); await diary.DeleteHabitGoalAsync(goal.Id);
    Check((await diary.GetHabitsAsync()).Length == 3 && (await diary.GetHabitGoalsAsync()).Length == 0, "Delete");
    var rollbackPath = Path.Combine(root, "rollback.db"); var rollback = new SqlitePersonalDiary(rollbackPath);
    await rollback.RestoreBackupAsync(old);
    Sql(rollbackPath, "DROP TABLE habits; DROP TABLE habit_goals; DROP TABLE habit_settings; CREATE TABLE habit_settings(sentinel TEXT); INSERT INTO habit_settings VALUES('keep'); PRAGMA user_version=2;");
    try { await rollback.GetHabitsAsync(); throw new Exception("Migration fault ignored"); } catch (SqliteException) { }
    using (var db = new SqliteConnection($"Data Source={rollbackPath};Pooling=False"))
    {
        db.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "PRAGMA user_version"; Check(Convert.ToInt32(cmd.ExecuteScalar()) == 2, "Failed v3 migration changed version");
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name='habits'"; Check(Convert.ToInt32(cmd.ExecuteScalar()) == 0, "Failed migration left tables");
        cmd.CommandText = "SELECT COUNT(*) FROM meals"; Check(Convert.ToInt32(cmd.ExecuteScalar()) == 1, "Failed migration lost meals");
    }
    Check(HabitEducation.Cards.Count >= 8 && HabitEducation.Cards.All(c => !string.IsNullOrWhiteSpace(c.Greek) && !string.IsNullOrWhiteSpace(c.English) && !string.IsNullOrWhiteSpace(c.Date) && !string.IsNullOrWhiteSpace(c.SourceTitle) && Uri.TryCreate(c.Url, UriKind.Absolute, out var url) && url.Scheme == "https"), "Education source metadata missing");
    Console.WriteLine("Habit checks passed: alcohol grams, savings after lapse, missing days, dates/week/year boundaries, validation, CRUD, disable/reopen, v2 migration, Greek locale, encrypted backup roundtrip, legacy restore, atomic failure.");
}
finally
{
    var resolved = Path.GetFullPath(root); var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!resolved.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("FitAdvisor-habits-")) throw new Exception("Unsafe cleanup");
    Directory.Delete(resolved, true);
}
