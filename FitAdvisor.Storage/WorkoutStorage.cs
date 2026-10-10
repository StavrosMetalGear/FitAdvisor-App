using FitAdvisor.Core;
using Microsoft.Data.Sqlite;

namespace FitAdvisor.Storage;

public sealed partial class SqlitePersonalDiary
{
    public Task<IReadOnlyList<WorkoutRecord>> GetWorkoutsAsync() => Execute<IReadOnlyList<WorkoutRecord>>(db =>
    {
        using var cmd = Command(db, "SELECT payload FROM workouts ORDER BY local_date DESC,rowid DESC");
        using var reader = cmd.ExecuteReader(); var records = new List<WorkoutRecord>();
        while (reader.Read()) records.Add(ReadJson<WorkoutRecord>(reader.GetString(0)));
        return records;
    });

    private static void WriteWorkout(SqliteConnection db, WorkoutRecord record, SqliteTransaction tx)
    {
        using var cmd = Command(db, """
            INSERT INTO workouts(id,local_date,source,source_package,external_id,payload)
            VALUES($id,$date,$source,$package,$external,$payload)
            ON CONFLICT(id) DO UPDATE SET local_date=excluded.local_date,payload=excluded.payload
            """, tx, ("$id", record.Id.ToString()), ("$date", Day(record.Date)), ("$source", record.Source),
            ("$package", record.SourcePackage), ("$external", record.ExternalId), ("$payload", Json(record)));
        cmd.ExecuteNonQuery();
    }

    public Task SaveWorkoutAsync(WorkoutRecord record)
    {
        record.Validate();
        if (!record.IsManual) throw new ArgumentException("Imported sessions are read-only. Edit them in the source app.");
        return Execute(db =>
        {
            using var tx = db.BeginTransaction();
            using var query = Command(db, "SELECT payload FROM workouts WHERE id=$id", tx, ("$id", record.Id.ToString()));
            if (query.ExecuteScalar() is string json)
            {
                var old = ReadJson<WorkoutRecord>(json);
                if (!old.IsManual) throw new ArgumentException("Cannot overwrite an imported session.");
                record = record with { CreatedAt = old.CreatedAt, UpdatedAt = DateTimeOffset.UtcNow };
            }
            WriteWorkout(db, record, tx); tx.Commit(); return true;
        });
    }

    public Task DeleteWorkoutAsync(Guid id) => Delete("workouts", id);

    public Task<int> ImportWorkoutsAsync(IReadOnlyList<WorkoutRecord> records)
    {
        if (records.Count > 10000) throw new ArgumentException("Import is too large. Select a shorter date range.");
        foreach (var record in records)
        {
            record.Validate();
            if (record.IsManual) throw new ArgumentException("Expected Health Connect sessions.");
        }
        return Execute(db =>
        {
            using var tx = db.BeginTransaction(); var changed = 0;
            foreach (var incoming in records)
            {
                using var query = Command(db, "SELECT payload FROM workouts WHERE source=$source AND source_package=$package AND external_id=$external", tx,
                    ("$source", incoming.Source), ("$package", incoming.SourcePackage), ("$external", incoming.ExternalId));
                var record = incoming;
                if (query.ExecuteScalar() is string json)
                {
                    var old = ReadJson<WorkoutRecord>(json);
                    if (incoming.UpdatedAt <= old.UpdatedAt) continue;
                    record = incoming with { Id = old.Id, CreatedAt = old.CreatedAt };
                }
                else
                {
                    using var collision = Command(db, "SELECT COUNT(*) FROM workouts WHERE id=$id", tx, ("$id", record.Id.ToString()));
                    if (Convert.ToInt32(collision.ExecuteScalar()) != 0) throw new ArgumentException("Import record ID conflicts with an existing workout.");
                }
                WriteWorkout(db, record, tx); changed++;
            }
            tx.Commit(); return changed;
        });
    }
}
