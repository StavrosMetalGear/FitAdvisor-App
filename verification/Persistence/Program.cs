using System.Diagnostics;
using System.Reflection;
using FitAdvisor.Core;
using FitAdvisor.Storage;
using Microsoft.Data.Sqlite;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
static void Equal(double expected, double actual) => Check(double.IsFinite(actual) && Math.Abs(expected - actual) < 0.000001, $"Expected {expected}, got {actual}");
static async Task Reject(Func<Task> action)
{
    try { await action(); } catch (ArgumentException) { return; }
    throw new Exception("Invalid data was accepted");
}
var day = new DateOnly(2026, 10, 8);
var profile = new ProfileDraft("Stavros", 80, 180, 30, FormulaSex.Male, 1.2, -300, 75);
var mealId = Guid.Parse("10000000-0000-0000-0000-000000000001");
var weightId = Guid.Parse("20000000-0000-0000-0000-000000000001");
if (args.Length == 2 && args[0] == "--seed")
{
    var seeded = new SqlitePersonalDiary(args[1]);
    await seeded.SaveProfileAsync(profile, day);
    await seeded.SaveMealAsync(new(mealId, day, "Oats", 80, "g", 300, 10, 45, 8));
    await seeded.SaveWeightAsync(new(weightId, day, 80.2, "Morning"));
    Console.WriteLine("Seed process committed data."); return;
}
if (args.Length == 2 && args[0] == "--reopen")
{
    var reopened = new SqlitePersonalDiary(args[1]);
    Check((await reopened.GetProfileAsync())?.Values.Name == "Stavros", "Profile lost on process restart");
    var meals = await reopened.GetMealsAsync(day);
    Check(meals.Count == 1 && meals[0].Name == "Oats" && meals[0].ProteinG == 10, "Meal lost on process restart");
    Check((await reopened.GetWeightsAsync()).Single().WeightKg == 80.2, "Weight lost on process restart");
    var dashboard = await reopened.GetDashboardAsync(day); Equal(1836, dashboard.Target!.TargetKcal); Equal(1536, dashboard.RemainingKcal!.Value);
    Console.WriteLine("Separate process reopened and verified profile, meal, weight and dashboard."); return;
}
async Task ProcessCheck(string mode, string file)
{
    var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location); start.ArgumentList.Add(mode); start.ArgumentList.Add(file);
    using var process = Process.Start(start) ?? throw new Exception("Could not start persistence verification child process");
    var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync(); Console.Write(await stdout); Check(process.ExitCode == 0, await stderr);
}
// Only owned temporary test files are removed, never the application database.
var temporary = Path.Combine(Path.GetTempPath(), "FitAdvisor-checks-" + Guid.NewGuid());
Directory.CreateDirectory(temporary);
try
{
    var file = Path.Combine(temporary, "restart.db");
    await ProcessCheck("--seed", file); await ProcessCheck("--reopen", file);
    var diary = new SqlitePersonalDiary(Path.Combine(temporary, "crud.db"));
    Check(await diary.GetProfileAsync() == null, "Empty database should have no profile");
    var empty = await diary.GetDashboardAsync(day); Check(empty.Target == null && empty.RemainingKcal == null && empty.MealCount == 0, "Unknown target must remain unknown");
    var original = await diary.SaveProfileAsync(profile, day);
    var meal = new MealRecord(mealId, day, "Oats '); DROP TABLE meals; --", 80, "g", 300, 10, null, 8);
    await diary.SaveMealAsync(meal);
    await diary.SaveMealAsync(new(Guid.NewGuid(), day.AddDays(1), "Tomorrow", 1, "serving", 700));
    var saved = (await diary.GetMealsAsync(day)).Single();
    Check(saved.ProfileRevisionId == original.Id && saved.CarbohydratesG == null && saved.Name == meal.Name, "Context, null macros or SQL parameterization failed");
    Equal(300, (await diary.GetDashboardAsync(day)).ConsumedKcal);
    await diary.SaveMealAsync(saved with { CaloriesKcal = 450, ProteinG = 15 });
    Equal(450, (await diary.GetMealsAsync(day)).Single().CaloriesKcal);
    Check((await diary.GetMealsAsync(day)).Count == 1, "Editing duplicated a meal");
    await Reject(() => diary.SaveMealAsync(meal with { Quantity = 0 }));
    await Reject(() => diary.SaveMealAsync(meal with { CaloriesKcal = double.NaN }));
    await Reject(() => diary.SaveMealAsync(meal with { ProteinG = -1 }));
    await Reject(() => diary.SaveProfileAsync(profile with { HeightCm = 1.8 }, day));
    Equal(450, (await diary.GetMealsAsync(day)).Single().CaloriesKcal);
    var revised = await diary.SaveProfileAsync(profile with { GoalAdjustmentKcal = -100 }, day.AddDays(1));
    Equal(1836, (await diary.GetDashboardAsync(day)).Target!.TargetKcal);
    Equal(2036, (await diary.GetDashboardAsync(day.AddDays(1))).Target!.TargetKcal);
    Check((await diary.GetProfileHistoryAsync()).Count == 2, "Profile revision overwritten");
    Check((await diary.GetMealsAsync(day)).Single().ProfileRevisionId == original.Id, "Historical meal context rewritten");
    await diary.SaveMealAsync(saved with { CaloriesKcal = 500 });
    Check((await diary.GetMealsAsync(day)).Single().ProfileRevisionId == original.Id, "Meal edit lost original context");
    await diary.SaveMealAsync(saved with { Date = day.AddDays(1) });
    Check((await diary.GetMealsAsync(day.AddDays(1))).Single(m => m.Id == saved.Id).ProfileRevisionId == revised.Id, "Re-dating did not adopt destination day's context");
    await diary.SaveWeightAsync(new(weightId, day, 80.2, "Morning"));
    await diary.SaveWeightAsync(new(weightId, day, 79.8, "Corrected"));
    Check((await diary.GetWeightsAsync()).Count == 1, "Weight edit duplicated a record");
    Equal(79.8, (await diary.GetWeightsAsync()).Single().WeightKg);
    await Reject(() => diary.SaveWeightAsync(new(Guid.NewGuid(), day, 0)));
    await diary.DeleteWeightAsync(weightId); await diary.DeleteMealAsync(mealId);
    var reopen = new SqlitePersonalDiary(Path.Combine(temporary, "crud.db"));
    Check((await reopen.GetWeightsAsync()).Count == 0 && !(await reopen.GetMealsAsync(day.AddDays(1))).Any(m => m.Id == mealId), "Deleted records returned after reopen");
    Check((await reopen.GetProfileHistoryAsync()).Count == 2, "Revision history lost after reopen");
    // Same-day profile edits update today's target but preserve the original entry context.
    var sameDay = new SqlitePersonalDiary(Path.Combine(temporary, "same-day.db"));
    var firstRevision = await sameDay.SaveProfileAsync(profile, day);
    await sameDay.SaveMealAsync(new(mealId, day, "Lunch", 1, "serving", 600));
    var secondRevision = await sameDay.SaveProfileAsync(profile with { GoalAdjustmentKcal = -100 }, day);
    Equal(2036, (await sameDay.GetDashboardAsync(day)).Target!.TargetKcal);
    Check((await sameDay.GetMealsAsync(day)).Single().ProfileRevisionId == firstRevision.Id, "Same-day goal edit rewrote entry context");
    var sameMeal = (await sameDay.GetMealsAsync(day)).Single();
    await sameDay.SaveMealAsync(sameMeal with { CaloriesKcal = 650 });
    Check((await sameDay.GetMealsAsync(day)).Single().ProfileRevisionId == firstRevision.Id, "Same-day entry edit rewrote profile context");
    Check((await sameDay.GetProfileAsync())?.Id == secondRevision.Id, "Latest same-day profile not selected");
    // Future-schema refusal must not downgrade or alter data.
    var futureFile = Path.Combine(temporary, "future.db");
    using (var connection = new SqliteConnection($"Data Source={futureFile};Pooling=False"))
    {
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "PRAGMA user_version=99; CREATE TABLE sentinel(value TEXT); INSERT INTO sentinel VALUES('keep');"; command.ExecuteNonQuery();
    }
    try { await new SqlitePersonalDiary(futureFile).GetProfileAsync(); throw new Exception("Future schema accepted"); }
    catch (InvalidOperationException) { }
    using (var connection = new SqliteConnection($"Data Source={futureFile};Pooling=False"))
    {
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "SELECT value FROM sentinel";
        Check((string?)command.ExecuteScalar() == "keep", "Future database modified");
        command.CommandText = "PRAGMA user_version"; Check(Convert.ToInt32(command.ExecuteScalar()) == 99, "Future schema downgraded");
    }
    // Failing migration must roll back tables/version together.
    var badFile = Path.Combine(temporary, "migration.db");
    using (var connection = new SqliteConnection($"Data Source={badFile};Pooling=False"))
    {
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "CREATE TABLE meals(existing TEXT)"; command.ExecuteNonQuery();
    }
    try { await new SqlitePersonalDiary(badFile).GetProfileAsync(); throw new Exception("Conflicting schema accepted"); }
    catch (SqliteException) { }
    using (var connection = new SqliteConnection($"Data Source={badFile};Pooling=False"))
    {
        connection.Open(); using var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name='profile_revisions'";
        Check(Convert.ToInt32(command.ExecuteScalar()) == 0, "Failed migration partially committed");
    }
    Console.WriteLine("Persistence checks passed: process restart, CRUD, validation, day totals, revisions, future schema and migration rollback.");
}
finally
{
    var resolved = Path.GetFullPath(temporary);
    var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!resolved.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("FitAdvisor-checks-", StringComparison.Ordinal))
        throw new InvalidOperationException("Refusing cleanup outside the owned temporary test directory.");
    Directory.Delete(resolved, recursive: true);
}
