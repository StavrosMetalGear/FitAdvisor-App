using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FitAdvisor.Core;

namespace FitAdvisor.Storage;

public sealed record BackupMeal(MealRecord Record, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record BackupWeight(WeightRecord Record, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record DiaryBackup(int FormatVersion, DateTimeOffset ExportedAt, ProfileRevision[] Profiles,
    DailyTargetSnapshot[] Targets, BackupMeal[] Meals, BackupWeight[] Weights, WorkoutRecord[] Workouts)
{
    public SavedFood[] SavedFoods { get; init; } = [];
    public GymRoutine[] GymRoutines { get; init; } = [];
    public HabitRecord[] Habits { get; init; } = [];
    public GymSession[] GymSessions { get; init; } = [];
    public GymExercise[] GymExercises { get; init; } = [];
    public GymPreferences GymPreferences { get; init; } = new();
    public HabitGoal[] HabitGoals { get; init; } = [];
    public HabitSettings HabitSettings { get; init; } = new();
    public string Summary => $"{Profiles.Length} profile revisions, {Targets.Length} daily targets, " +
        $"{Meals.Length} meals, {Weights.Length} weights, {Workouts.Count(w => w.IsCompleted && !GymSessions.Any(s=>s.ImportedWorkoutId==w.Id))} completed workouts (linked imports counted once), {Workouts.Count(w => w.IsPlanned)} planned workouts, {Habits.Length} habit records, {HabitGoals.Length} habit goals, {GymSessions.Length} gym sessions, {GymExercises.Length} custom exercises";
    public void Validate()
    {
        if (Profiles == null || Targets == null || Meals == null || Weights == null || Workouts == null) throw new ArgumentException("Incomplete backup.");
        if (FormatVersion is not (1 or 2 or 3 or 4 or 5 or 6) || ExportedAt == default) throw new ArgumentException("Unsupported backup version.");
        if (GymSessions == null || GymExercises == null || GymPreferences == null || GymSessions.Length > 10000 || GymExercises.Length > 10000) throw new ArgumentException("Invalid gym backup.");
        if (SavedFoods == null || GymRoutines == null || SavedFoods.Length > 10000 || GymRoutines.Length > 1000) throw new ArgumentException("Invalid saved products/routines.");
        foreach (var food in SavedFoods) food.Validate();
        foreach (var routine in GymRoutines) routine.Validate();
        if (SavedFoods.Select(f=>f.Id).Distinct().Count()!=SavedFoods.Length || GymRoutines.Select(r=>r.Id).Distinct().Count()!=GymRoutines.Length) throw new ArgumentException("Duplicate product/routine IDs.");
        if (FormatVersion < 6 && (SavedFoods.Length > 0 || GymRoutines.Length > 0)) throw new ArgumentException("Products and routines require format 6.");
        GymPreferences.Validate();
        foreach (var e in GymExercises) { e.Validate(); if (!e.Custom || GymCatalogue.Starter.Any(s => s.Id == e.Id)) throw new ArgumentException("Invalid custom exercise."); }
        foreach (var s in GymSessions) s.Validate();
        if (GymSessions.Select(s => s.Id).Distinct().Count() != GymSessions.Length || GymExercises.Select(e => e.Id).Distinct().Count() != GymExercises.Length || GymSessions.Count(s => s.Status == "active") > 1) throw new ArgumentException("Duplicate gym identities or active sessions.");
        if(GymSessions.Where(s=>s.Status!="cancelled").Select(s=>s.WorkoutId).Distinct().Count()!=GymSessions.Count(s=>s.Status!="cancelled")) throw new ArgumentException("Duplicate gym workout references.");
        if(GymSessions.Any(s=>s.Status=="active" && s.PlanId!=null && !Workouts.Any(w=>w.Id==s.PlanId && w.IsPlanned && w.IsManual && w.Kind=="gym")))throw new ArgumentException("Broken active gym plan reference.");
        if(FormatVersion<5 && (GymSessions.Length>0 || GymExercises.Length>0)) throw new ArgumentException("Gym records require backup format 5.");
        if (GymSessions.Where(s => s.Status == "finished").Any(s => !Workouts.Any(w => w.Id == s.WorkoutId && w.IsCompleted && w.IsManual && w.Kind == "gym"))) throw new ArgumentException("Broken gym workout reference.");
        var links = GymSessions.Where(s => s.ImportedWorkoutId != null).ToArray();
        if (links.Select(s => s.ImportedWorkoutId).Distinct().Count() != links.Length || links.Any(s => !Workouts.Any(w => w.Id == s.ImportedWorkoutId && !w.IsManual && w.Kind == "gym" && w.StartTime < s.FinishedAt && w.StartTime!.Value.AddSeconds(w.DurationSeconds) > s.StartedAt))) throw new ArgumentException("Broken imported gym link.");
        foreach(var session in GymSessions.Where(s=>s.Enrichment!=null)) {
            var sensor=session.Enrichment!;var import=Workouts.Single(w=>w.Id==session.ImportedWorkoutId);
            if(sensor.SourcePackage!=import.SourcePackage || sensor.From<session.StartedAt || sensor.Through>session.FinishedAt || sensor.From<import.StartTime || sensor.Through>import.StartTime!.Value.AddSeconds(import.DurationSeconds))throw new ArgumentException("Sensor snapshot origin/time mismatch.");
        }
        if (Habits == null || HabitGoals == null || HabitSettings == null) throw new ArgumentException("Incomplete habit backup.");
        HabitSettings.Validate();
        foreach (var goal in HabitGoals) goal.Validate();
        foreach (var habit in Habits) habit.Validate();
        if (HabitGoals.Select(g => g.Id).Distinct().Count() != HabitGoals.Length || Habits.Select(h => h.Id).Distinct().Count() != Habits.Length) throw new ArgumentException("Duplicate habit IDs.");
        if (Habits.Where(h => h.Kind == "smoking").GroupBy(h => h.Date).Any(g => g.Count() > 1)) throw new ArgumentException("Duplicate smoking dates.");
        if (Habits.Any(h => h.Kind == "eating" && !HabitGoals.Any(g => g.Id == h.GoalId && g.Kind == "eating"))) throw new ArgumentException("Broken eating goal reference.");
        if (Profiles == null || Targets == null || Meals == null || Weights == null || Workouts == null)
            throw new ArgumentException("Incomplete backup.");
        if ((long)Profiles.Length + Targets.Length + Meals.Length + Weights.Length + Workouts.Length + Habits.Length + HabitGoals.Length > 100000)
            throw new ArgumentException("Backup contains too many records.");
        static HashSet<Guid> Ids(IEnumerable<Guid> values)
        {
            var list = values.ToArray(); var set = list.ToHashSet();
            if (set.Contains(Guid.Empty) || list.Length != set.Count) throw new ArgumentException("Invalid or duplicate record IDs.");
            return set;
        }
        var profileIds = Ids(Profiles.Select(p => p.Id));
        Ids(Meals.Select(m => m.Record.Id)); Ids(Weights.Select(w => w.Record.Id)); Ids(Workouts.Select(w => w.Id));
        if (Targets.Select(t => t.Date).Distinct().Count() != Targets.Length) throw new ArgumentException("Duplicate target dates.");
        void Reference(Guid? id) { if (id is { } value && !profileIds.Contains(value)) throw new ArgumentException("Broken profile reference."); }
        static void Times(DateTimeOffset created, DateTimeOffset updated)
        { if (created == default || updated < created) throw new ArgumentException("Invalid record timestamps."); }
        foreach (var p in Profiles) { p.Values.Validate(); DiaryValidation.Date(p.EffectiveDate); Times(p.CreatedAt, p.CreatedAt); }
        foreach (var t in Targets)
        {
            DiaryValidation.Date(t.Date); Reference(t.ProfileRevisionId); NutritionCalculations.Validate(t.Inputs);
            DiaryValidation.Text(t.FormulaVersion, 200, "Formula version");
            DiaryValidation.Number(t.ActivityMultiplier, 1, 2.5, "Activity multiplier");
            DiaryValidation.Number(t.GoalAdjustmentKcal, -1000, 1000, "Calorie adjustment");
            DiaryValidation.Number(t.TargetKcal, 1, 100000, "Target calories");
        }
        foreach (var m in Meals) { m.Record.Validate(); Reference(m.Record.ProfileRevisionId); Times(m.CreatedAt, m.UpdatedAt); }
        foreach (var w in Weights) { w.Record.Validate(); Reference(w.Record.ProfileRevisionId); Times(w.CreatedAt, w.UpdatedAt); }
        var imports = new HashSet<(string?, string?)>();
        foreach (var w in Workouts)
        {
            w.Validate();
            if (FormatVersion < 4 && w.IsPlanned) throw new ArgumentException("Planned workouts require backup format 4.");
            if (!w.IsManual && !imports.Add((w.SourcePackage, w.ExternalId))) throw new ArgumentException("Duplicate imported sessions.");
        }
    }
}

// Portable, authenticated, passphrase-encrypted envelope. No account, server or key file.
public static class BackupCodec
{
    public const int MaxFileBytes = 32 * 1024 * 1024;
    private static readonly byte[] Header = Encoding.ASCII.GetBytes("FITADV01");
    private const int Iterations = 600000;
    public static byte[] Encrypt(DiaryBackup backup, string passphrase)
    {
        if (passphrase.Length < 12 || passphrase.Length > 1024) throw new ArgumentException("Use a passphrase of 12–1024 characters.");
        backup.Validate();
        var plain = JsonSerializer.SerializeToUtf8Bytes(backup, DiaryJsonContext.Default.DiaryBackup);
        if (plain.Length > MaxFileBytes - 52) throw new ArgumentException("Backup exceeds the 32 MB limit.");
        var salt = RandomNumberGenerator.GetBytes(16); var nonce = RandomNumberGenerator.GetBytes(12);
        var key = Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, Iterations, HashAlgorithmName.SHA256, 32);
        try
        {
            var output = new byte[52 + plain.Length]; Header.CopyTo(output, 0); salt.CopyTo(output, 8); nonce.CopyTo(output, 24);
            using var aes = new AesGcm(key, 16);
            aes.Encrypt(nonce, plain, output.AsSpan(52), output.AsSpan(36, 16), output.AsSpan(0, 36));
            return output;
        }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }
    public static DiaryBackup Decrypt(byte[] file, string passphrase)
    {
        if (file.Length < 52 || file.Length > MaxFileBytes || !file.AsSpan(0, 8).SequenceEqual(Header))
            throw new ArgumentException("Not a supported FitAdvisor encrypted backup.");
        if (passphrase.Length < 12 || passphrase.Length > 1024) throw new ArgumentException("Enter the backup passphrase (12–1024 characters).");
        var key = Rfc2898DeriveBytes.Pbkdf2(passphrase, file.AsSpan(8, 16), Iterations, HashAlgorithmName.SHA256, 32);
        var plain = new byte[file.Length - 52];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(file.AsSpan(24, 12), file.AsSpan(52), file.AsSpan(36, 16), plain, file.AsSpan(0, 36));
            var backup = JsonSerializer.Deserialize(plain, DiaryJsonContext.Default.DiaryBackup)
                ?? throw new ArgumentException("Empty backup.");
            backup.Validate(); return backup;
        }
        catch (Exception error) when (error is CryptographicException or JsonException or NullReferenceException)
        { throw new ArgumentException("Incorrect passphrase, damaged or invalid backup. Existing data was not changed."); }
        finally { CryptographicOperations.ZeroMemory(key); CryptographicOperations.ZeroMemory(plain); }
    }
}
