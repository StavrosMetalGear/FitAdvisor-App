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
        var sessions=HabitRead<GymSession>(db,"gym_sessions");
        return records.Select(w => {
            var gym=sessions.FirstOrDefault(s=>s.Status!="cancelled" && (s.WorkoutId==w.Id || s.ImportedWorkoutId==w.Id));
            return w with {GymDetailId=gym?.Id,ExcludedFromTotals=gym?.ImportedWorkoutId==w.Id};
        }).ToArray();
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
        if (record.IsCompleted && record.Date > DateOnly.FromDateTime(DateTime.Today))
            throw new ArgumentException("Future manual workouts must have Planned status.");
        return Execute(db =>
        {
            using var tx = db.BeginTransaction();
            if (HabitRead<GymSession>(db,"gym_sessions",tx).Any(s=>s.WorkoutId==record.Id && s.Status != "cancelled")) throw new ArgumentException("Edit this workout through Gym Session to preserve detailed sets and recovery.");
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

    public Task DeleteWorkoutAsync(Guid id) => Execute(db => {
        using var tx=db.BeginTransaction();
        if(HabitRead<GymSession>(db,"gym_sessions",tx).Any(s=>s.WorkoutId==id || s.ImportedWorkoutId==id)) throw new ArgumentException("This workout belongs to a Gym Session or linked import. Undo completion or unlink in Gym Session first.");
        using var cmd=Command(db,"DELETE FROM workouts WHERE id=$id",tx,("$id",id.ToString()));cmd.ExecuteNonQuery();tx.Commit();return true;
    });

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
            var gymSessions=HabitRead<GymSession>(db,"gym_sessions",tx);
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
                var linked=gymSessions.FirstOrDefault(s=>s.ImportedWorkoutId==record.Id);
                if(linked!=null && (record.Kind!="gym" || record.StartTime==null || record.StartTime>=linked.FinishedAt || record.StartTime.Value.AddSeconds(record.DurationSeconds)<=linked.StartedAt))
                    throw new ArgumentException("An edited source session is no longer compatible with its Gym Session link. Unlink it before importing this change; nothing was imported.");
                if(linked?.Enrichment is {} sensor && (sensor.From<record.StartTime || sensor.Through>record.StartTime!.Value.AddSeconds(record.DurationSeconds)))
                    throw new ArgumentException("Source time change invalidates the sensor snapshot. Unlink and reimport explicitly; existing records retained.");
                WriteWorkout(db, record, tx); changed++;
            }
            tx.Commit(); return changed;
        });
    }
}
