using FitAdvisor.Core;
using FitAdvisor.Storage;
using Microsoft.Data.Sqlite;
using System.Globalization;

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static async Task Reject(Func<Task> action)
{
    try { await action(); } catch (ArgumentException) { return; }
    throw new Exception("Invalid operation succeeded.");
}
static void Sql(string path, string text)
{
    using var db = new SqliteConnection($"Data Source={path};Pooling=False"); db.Open();
    using var cmd = db.CreateCommand(); cmd.CommandText = text; cmd.ExecuteNonQuery();
}
var root = Path.Combine(Path.GetTempPath(), "FitAdvisor-offline-" + Guid.NewGuid()); Directory.CreateDirectory(root);
try
{
    var path = Path.Combine(root, "diary.db"); var diary = new SqlitePersonalDiary(path);
    var day = new DateOnly(2026, 10, 10); var now = DateTimeOffset.UtcNow;
    var p = await diary.SaveProfileAsync(new("Synthetic only", 80, 180, 30, FormulaSex.Male, 1.2, -300, 75), day);
    await diary.SaveMealAsync(new(Guid.NewGuid(), day, "Oats", 80.5, "g", 300, 10, null, 8));
    await diary.SaveWeightAsync(new(Guid.NewGuid(), day, 80.2, "Test", p.Id));
    var preWorkout = await diary.CreateBackupAsync();
    var run = new WorkoutRecord(Guid.NewGuid(), day, 180, "run", "Morning run", 1800, 5, "Manual", Array.Empty<StrengthSet>(), now, now);
    var gym = new WorkoutRecord(Guid.NewGuid(), day, 180, "gym", "Gym", 2700, null, "Notes",
        new[] { new StrengthSet(Guid.NewGuid(), "Squat", 8, 60, true), new StrengthSet(Guid.NewGuid(), "Squat", 6, 80) }, now, now);
    await diary.SaveWorkoutAsync(run); await diary.SaveWorkoutAsync(gym);
    await Reject(() => diary.SaveWorkoutAsync(run with { DistanceKm = null }));
    await Reject(() => diary.SaveWorkoutAsync(gym with { DurationSeconds = double.NaN }));
    await Reject(() => diary.SaveWorkoutAsync(gym with { Sets = new[] { gym.Sets[0], gym.Sets[0] } }));
    await diary.SaveWorkoutAsync(run with { DurationSeconds = 1600 });
    Check((await diary.GetWorkoutsAsync()).Count == 2, "Manual edit duplicated workout");
    Check((await diary.GetWorkoutsAsync()).Single(w => w.Id == gym.Id).Sets[1].Id == gym.Sets[1].Id, "Set ID lost");
    var imported = new WorkoutRecord(Guid.NewGuid(), day, 180, "other", "Imported", 1800, null, "Source note",
        Array.Empty<StrengthSet>(), now.AddHours(-1), now, "health-connect", "test.source", "session-1", now.AddHours(-1), 56);
    Check(await diary.ImportWorkoutsAsync(new[] { imported }) == 1, "First import failed");
    Check(await diary.ImportWorkoutsAsync(new[] { imported with { Id = Guid.NewGuid() } }) == 0, "Repeat import duplicated session");
    Check(await diary.ImportWorkoutsAsync(new[] { imported with { Id = Guid.NewGuid(), Title = "Updated", UpdatedAt = now.AddMinutes(1) } }) == 1, "Update not imported");
    Check((await diary.GetWorkoutsAsync()).Single(w => w.ExternalId == "session-1").Id == imported.Id, "Import changed stable ID");
    await Reject(() => diary.SaveWorkoutAsync(imported));
    await Reject(() => diary.SaveWorkoutAsync(run with { Id = imported.Id }));
    await Reject(() => diary.ImportWorkoutsAsync(new[] { imported with { Id = run.Id, ExternalId = "different" } }));
    Check(await diary.ImportWorkoutsAsync(new[] { imported with { Id = Guid.NewGuid(), SourcePackage = "second.source" } }) == 1, "Origin collision");
    var dashboard = await diary.GetDashboardAsync(day);
    Check(dashboard.Target!.TargetKcal == 1836 && dashboard.ConsumedKcal == 300, "Workout changed target or allowance");
    var snapshot = await diary.CreateBackupAsync();
    const string pass = "synthetic-long-backup-passphrase";
    var encrypted = BackupCodec.Encrypt(snapshot, pass);
    Check(!System.Text.Encoding.UTF8.GetString(encrypted).Contains("Synthetic only"), "Plaintext exported");
    await Reject(() => Task.Run(() => BackupCodec.Decrypt(encrypted, "incorrect-long-passphrase")));
    var corrupt = encrypted.ToArray(); corrupt[^1] ^= 1;
    await Reject(() => Task.Run(() => BackupCodec.Decrypt(corrupt, pass)));
    await Reject(() => Task.Run(() => BackupCodec.Decrypt(encrypted[..40], pass)));
    await Reject(() => diary.RestoreBackupAsync(snapshot with { FormatVersion = 99 }));
    await Reject(() => diary.RestoreBackupAsync(snapshot with { Profiles = Array.Empty<ProfileRevision>() }));
    var sourceCulture = CultureInfo.CurrentCulture;
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("el-GR");
    var decoded = BackupCodec.Decrypt(encrypted, pass);
    var fresh = new SqlitePersonalDiary(Path.Combine(root, "fresh.db")); await fresh.RestoreBackupAsync(decoded);
    CultureInfo.CurrentCulture = sourceCulture;
    var restored = await fresh.CreateBackupAsync();
    Check(restored.Profiles.SequenceEqual(snapshot.Profiles) && restored.Targets.SequenceEqual(snapshot.Targets), "Profile/target snapshots lost");
    Check(restored.Meals.SequenceEqual(snapshot.Meals) && restored.Weights.SequenceEqual(snapshot.Weights), "IDs, timestamps, notes or macros lost");
    Check(restored.Workouts.Count() == snapshot.Workouts.Length && restored.Workouts.All(w => snapshot.Workouts.Any(s => s.Id == w.Id && s.Sets.SequenceEqual(w.Sets) && s.SourcePackage == w.SourcePackage)), "Workouts/sets/provenance lost");
    // Restore a valid earlier empty-workout backup, then restore the full backup.
    await fresh.RestoreBackupAsync(preWorkout); Check((await fresh.GetWorkoutsAsync()).Count == 0, "Restore merged instead of replacing");
    await fresh.RestoreBackupAsync(decoded);
    // A write failure halfway through restore must roll back the earlier deletes/inserts.
    Sql(path, "CREATE TRIGGER reject_restore BEFORE INSERT ON meals BEGIN SELECT RAISE(ABORT,'synthetic failure'); END;");
    try { await diary.RestoreBackupAsync(preWorkout); throw new Exception("Restore fault was ignored"); } catch (SqliteException) { }
    var unchanged = await diary.CreateBackupAsync();
    Check(unchanged.Meals.SequenceEqual(snapshot.Meals) && unchanged.Profiles.SequenceEqual(snapshot.Profiles) && unchanged.Workouts.Length == snapshot.Workouts.Length, "Failed restore changed data");
    Sql(path, "DROP TRIGGER reject_restore;");
    // Simulate the exact v1 schema by dropping only the new workout table and resetting its version.
    var upgradePath = Path.Combine(root, "upgrade.db"); var upgrading = new SqlitePersonalDiary(upgradePath);
    await upgrading.RestoreBackupAsync(preWorkout); Sql(upgradePath, "DROP TABLE habits; DROP TABLE habit_goals; DROP TABLE habit_settings; DROP TABLE workouts; PRAGMA user_version=1;");
    var upgraded = await upgrading.CreateBackupAsync();
    Check(upgraded.Meals.SequenceEqual(preWorkout.Meals) && upgraded.Profiles.SequenceEqual(preWorkout.Profiles) && upgraded.Targets.SequenceEqual(preWorkout.Targets), "v1 upgrade lost diary history");
    await upgrading.SaveWorkoutAsync(gym); Check((await upgrading.GetWorkoutsAsync()).Count == 1, "v2 table missing");
    await fresh.DeleteWorkoutAsync(imported.Id); Check(!(await fresh.GetWorkoutsAsync()).Any(w => w.Id == imported.Id), "Delete failed");
    Check(await fresh.ImportWorkoutsAsync(new[] { imported }) == 1, "Explicit reimport after local deletion failed");
    Console.WriteLine("Offline checks passed: authenticated backup, wrong password/tamper/truncation, Greek locale, full history roundtrip, atomic failed restore, v1 upgrade, manual CRUD, sets, import deduplication/provenance, calorie isolation.");
}
finally
{
    var resolved = Path.GetFullPath(root); var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    if (!resolved.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("FitAdvisor-offline-", StringComparison.Ordinal))
        throw new InvalidOperationException("Refusing cleanup outside owned test directory.");
    Directory.Delete(resolved, true);
}
