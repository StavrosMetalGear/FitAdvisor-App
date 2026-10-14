using FitAdvisor.Core;

namespace FitAdvisor.Storage;

public sealed partial class SqlitePersonalDiary
{
    public Task<DiaryBackup> CreateBackupAsync() => Execute(db =>
    {
        // A single read transaction gives every table the same consistent snapshot.
        using var tx = db.BeginTransaction();
        var profiles = new List<ProfileRevision>(); var targets = new List<DailyTargetSnapshot>();
        var meals = new List<BackupMeal>(); var weights = new List<BackupWeight>(); var workouts = new List<WorkoutRecord>();
        using (var cmd = Command(db, "SELECT payload FROM profile_revisions ORDER BY rowid", tx))
        using (var r = cmd.ExecuteReader()) while (r.Read()) profiles.Add(ReadJson<ProfileRevision>(r.GetString(0)));
        using (var cmd = Command(db, "SELECT payload FROM daily_targets ORDER BY local_date", tx))
        using (var r = cmd.ExecuteReader()) while (r.Read()) targets.Add(ReadJson<DailyTargetSnapshot>(r.GetString(0)));
        using (var cmd = Command(db, "SELECT id,local_date,name,quantity,unit,kcal,protein_g,carbs_g,fat_g,profile_id,created_at,updated_at,nutrition_snapshot FROM meals ORDER BY rowid", tx))
        using (var r = cmd.ExecuteReader()) while (r.Read()) meals.Add(new(new(Guid.Parse(r.GetString(0)), DateOnly.Parse(r.GetString(1)),
            r.GetString(2), r.GetDouble(3), r.GetString(4), r.GetDouble(5), OptionalNumber(r,6), OptionalNumber(r,7), OptionalNumber(r,8), OptionalId(r,9), r.IsDBNull(12) ? null : ReadJson<FoodNutritionSnapshot>(r.GetString(12))),
            DateTimeOffset.Parse(r.GetString(10)), DateTimeOffset.Parse(r.GetString(11))));
        using (var cmd = Command(db, "SELECT id,local_date,weight_kg,notes,profile_id,created_at,updated_at FROM weights ORDER BY rowid", tx))
        using (var r = cmd.ExecuteReader()) while (r.Read()) weights.Add(new(new(Guid.Parse(r.GetString(0)), DateOnly.Parse(r.GetString(1)),
            r.GetDouble(2), r.IsDBNull(3) ? null : r.GetString(3), OptionalId(r,4)), DateTimeOffset.Parse(r.GetString(5)), DateTimeOffset.Parse(r.GetString(6))));
        using (var cmd = Command(db, "SELECT payload FROM workouts ORDER BY rowid", tx))
        using (var r = cmd.ExecuteReader()) while (r.Read()) workouts.Add(ReadJson<WorkoutRecord>(r.GetString(0)));
        var backup = new DiaryBackup(5, DateTimeOffset.UtcNow, profiles.ToArray(), targets.ToArray(), meals.ToArray(), weights.ToArray(), workouts.ToArray()) {
            GymSessions = HabitRead<GymSession>(db,"gym_sessions",tx), GymExercises = HabitRead<GymExercise>(db,"gym_exercises",tx),
            GymPreferences = HabitRead<GymPreferences>(db,"gym_preferences",tx).SingleOrDefault() ?? new(),
            Habits = HabitRead<HabitRecord>(db, "habits", tx), HabitGoals = HabitRead<HabitGoal>(db, "habit_goals", tx),
            HabitSettings = HabitRead<HabitSettings>(db, "habit_settings", tx).SingleOrDefault() ?? new() };
        backup.Validate(); tx.Commit(); return backup;
    });

    public Task RestoreBackupAsync(DiaryBackup backup)
    {
        backup.Validate();
        return Execute(db =>
        {
            // Replacing all records is atomic, including IDs, timestamps and historical targets.
            using var tx = db.BeginTransaction();
            using (var clear = Command(db, "DELETE FROM gym_sessions; DELETE FROM gym_exercises; DELETE FROM gym_preferences; DELETE FROM habits; DELETE FROM habit_goals; DELETE FROM habit_settings; DELETE FROM workouts; DELETE FROM meals; DELETE FROM weights; DELETE FROM daily_targets; DELETE FROM profile_revisions;", tx))
                clear.ExecuteNonQuery();
            foreach (var p in backup.Profiles)
            {
                using var cmd = Command(db, "INSERT INTO profile_revisions(id,effective_date,created_at,payload) VALUES($id,$date,$created,$payload)", tx,
                    ("$id", p.Id.ToString()), ("$date", Day(p.EffectiveDate)), ("$created", p.CreatedAt.ToString("O")), ("$payload", Json(p)));
                cmd.ExecuteNonQuery();
            }
            foreach (var t in backup.Targets)
            {
                using var cmd = Command(db, "INSERT INTO daily_targets(local_date,profile_id,payload) VALUES($date,$profile,$payload)", tx,
                    ("$date", Day(t.Date)), ("$profile", t.ProfileRevisionId.ToString()), ("$payload", Json(t))); cmd.ExecuteNonQuery();
            }
            foreach (var entry in backup.Meals)
            {
                var m = entry.Record;
                using var cmd = Command(db, """
                    INSERT INTO meals(id,local_date,name,quantity,unit,kcal,protein_g,carbs_g,fat_g,profile_id,created_at,updated_at,nutrition_snapshot)
                    VALUES($id,$date,$name,$quantity,$unit,$kcal,$protein,$carbs,$fat,$profile,$created,$updated,$snapshot)
                    """, tx, ("$id", m.Id.ToString()), ("$date", Day(m.Date)), ("$name", m.Name), ("$quantity", m.Quantity),
                    ("$unit", m.Unit), ("$kcal", m.CaloriesKcal), ("$protein", m.ProteinG), ("$carbs", m.CarbohydratesG),
                    ("$fat", m.FatG), ("$profile", m.ProfileRevisionId?.ToString()), ("$created", entry.CreatedAt.ToString("O")), ("$updated", entry.UpdatedAt.ToString("O")), ("$snapshot", m.NutritionSnapshot == null ? null : Json(m.NutritionSnapshot)));
                cmd.ExecuteNonQuery();
            }
            foreach (var entry in backup.Weights)
            {
                var w = entry.Record;
                using var cmd = Command(db, "INSERT INTO weights(id,local_date,weight_kg,notes,profile_id,created_at,updated_at) VALUES($id,$date,$weight,$notes,$profile,$created,$updated)", tx,
                    ("$id", w.Id.ToString()), ("$date", Day(w.Date)), ("$weight", w.WeightKg), ("$notes", w.Notes), ("$profile", w.ProfileRevisionId?.ToString()),
                    ("$created", entry.CreatedAt.ToString("O")), ("$updated", entry.UpdatedAt.ToString("O"))); cmd.ExecuteNonQuery();
            }
            foreach (var w in backup.Workouts) WriteWorkout(db, w, tx);
            foreach (var g in backup.HabitGoals) WriteGoal(db, g, tx);
            foreach (var h in backup.Habits) WriteHabit(db, h, tx);
            WriteSettings(db, backup.HabitSettings, tx);
            foreach (var s in backup.GymSessions) GymWrite(db,"gym_sessions",s.Id.ToString(),s,tx);
            foreach (var e in backup.GymExercises) GymWrite(db,"gym_exercises",e.Id,e,tx);
            GymWrite(db,"gym_preferences","1",backup.GymPreferences,tx);
            tx.Commit(); return true;
        });
    }
}
