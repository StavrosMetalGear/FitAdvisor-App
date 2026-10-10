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
        using (var cmd = Command(db, "SELECT id,local_date,name,quantity,unit,kcal,protein_g,carbs_g,fat_g,profile_id,created_at,updated_at FROM meals ORDER BY rowid", tx))
        using (var r = cmd.ExecuteReader()) while (r.Read()) meals.Add(new(new(Guid.Parse(r.GetString(0)), DateOnly.Parse(r.GetString(1)),
            r.GetString(2), r.GetDouble(3), r.GetString(4), r.GetDouble(5), OptionalNumber(r,6), OptionalNumber(r,7), OptionalNumber(r,8), OptionalId(r,9)),
            DateTimeOffset.Parse(r.GetString(10)), DateTimeOffset.Parse(r.GetString(11))));
        using (var cmd = Command(db, "SELECT id,local_date,weight_kg,notes,profile_id,created_at,updated_at FROM weights ORDER BY rowid", tx))
        using (var r = cmd.ExecuteReader()) while (r.Read()) weights.Add(new(new(Guid.Parse(r.GetString(0)), DateOnly.Parse(r.GetString(1)),
            r.GetDouble(2), r.IsDBNull(3) ? null : r.GetString(3), OptionalId(r,4)), DateTimeOffset.Parse(r.GetString(5)), DateTimeOffset.Parse(r.GetString(6))));
        using (var cmd = Command(db, "SELECT payload FROM workouts ORDER BY rowid", tx))
        using (var r = cmd.ExecuteReader()) while (r.Read()) workouts.Add(ReadJson<WorkoutRecord>(r.GetString(0)));
        var backup = new DiaryBackup(1, DateTimeOffset.UtcNow, profiles.ToArray(), targets.ToArray(), meals.ToArray(), weights.ToArray(), workouts.ToArray());
        backup.Validate(); tx.Commit(); return backup;
    });

    public Task RestoreBackupAsync(DiaryBackup backup)
    {
        backup.Validate();
        return Execute(db =>
        {
            // Replacing all records is atomic, including IDs, timestamps and historical targets.
            using var tx = db.BeginTransaction();
            using (var clear = Command(db, "DELETE FROM workouts; DELETE FROM meals; DELETE FROM weights; DELETE FROM daily_targets; DELETE FROM profile_revisions;", tx))
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
                    INSERT INTO meals(id,local_date,name,quantity,unit,kcal,protein_g,carbs_g,fat_g,profile_id,created_at,updated_at)
                    VALUES($id,$date,$name,$quantity,$unit,$kcal,$protein,$carbs,$fat,$profile,$created,$updated)
                    """, tx, ("$id", m.Id.ToString()), ("$date", Day(m.Date)), ("$name", m.Name), ("$quantity", m.Quantity),
                    ("$unit", m.Unit), ("$kcal", m.CaloriesKcal), ("$protein", m.ProteinG), ("$carbs", m.CarbohydratesG),
                    ("$fat", m.FatG), ("$profile", m.ProfileRevisionId?.ToString()), ("$created", entry.CreatedAt.ToString("O")), ("$updated", entry.UpdatedAt.ToString("O")));
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
            tx.Commit(); return true;
        });
    }
}
