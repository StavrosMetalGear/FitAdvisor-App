using FitAdvisor.Core;
using Microsoft.Data.Sqlite;

namespace FitAdvisor.Storage;

public sealed partial class SqlitePersonalDiary
{
    public Task<GymSession[]> GetGymSessionsAsync() => Execute(db => HabitRead<GymSession>(db, "gym_sessions"));
    public Task<GymExercise[]> GetCustomExercisesAsync() => Execute(db => HabitRead<GymExercise>(db, "gym_exercises"));
    public Task<GymPreferences> GetGymPreferencesAsync() => Execute(db => HabitRead<GymPreferences>(db, "gym_preferences").SingleOrDefault() ?? new());
    private static void GymWrite<T>(SqliteConnection db, string table, string id, T record, SqliteTransaction tx)
    {
        using var cmd = Command(db, $"INSERT INTO {table}(id,payload) VALUES($id,$payload) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload", tx,
            ("$id", id), ("$payload", Json(record))); cmd.ExecuteNonQuery();
    }
    public Task SaveCustomExerciseAsync(GymExercise e)
    {
        e.Validate(); if (!e.Custom || GymCatalogue.Starter.Any(x => x.Id == e.Id)) throw new ArgumentException("Custom exercise identity required.");
        return Execute(db => { using var tx = db.BeginTransaction();
            using var existing = Command(db, "SELECT payload FROM gym_exercises WHERE id=$id", tx, ("$id", e.Id));
            if (existing.ExecuteScalar() is string old && ReadJson<GymExercise>(old).Key != e.Key) throw new ArgumentException("Create a new variation instead of changing record conventions.");
            GymWrite(db,"gym_exercises",e.Id,e,tx); tx.Commit(); return true; });
    }
    public Task SaveGymPreferencesAsync(GymPreferences p)
    {
        p.Validate(); return Execute(db => { using var tx = db.BeginTransaction(); GymWrite(db,"gym_preferences","1",p,tx); tx.Commit(); return true; });
    }
    public Task SaveGymSessionAsync(GymSession session)
    {
        session.Validate();
        return Execute(db => {
            using var tx = db.BeginTransaction();
            using var existing = Command(db, "SELECT payload FROM gym_sessions WHERE id=$id", tx, ("$id", session.Id.ToString()));
            var old = existing.ExecuteScalar() is string json ? ReadJson<GymSession>(json) : null;
            if (old != null && (old.WorkoutId != session.WorkoutId || old.StartedAt != session.StartedAt)) throw new ArgumentException("Session identity cannot change.");
            if (session.Status == "active") {
                using var active = Command(db, "SELECT payload FROM gym_sessions WHERE id<>$id", tx, ("$id",session.Id.ToString()));
                using var reader = active.ExecuteReader(); while(reader.Read()) if(ReadJson<GymSession>(reader.GetString(0)).Status == "active") throw new ArgumentException("Resume or cancel the existing active session first.");
            }
            WorkoutRecord? plan = null;
            using (var workout = Command(db,"SELECT payload FROM workouts WHERE id=$id",tx,("$id", session.WorkoutId.ToString())))
                if (workout.ExecuteScalar() is string payload) plan = ReadJson<WorkoutRecord>(payload);
            if(old==null && session.PlanId==null && plan!=null)throw new ArgumentException("Session ID conflicts with an existing workout.");
            if (session.PlanId != null && old == null && (plan == null || !plan.IsPlanned || !plan.IsManual || plan.Kind != "gym")) throw new ArgumentException("Choose an existing gym plan.");
            if (session.Status == "finished") {
                if (plan != null && !plan.IsManual) throw new ArgumentException("Workout ID collision.");
                WriteWorkout(db, session.Workout() with { CreatedAt = plan?.CreatedAt ?? session.StartedAt }, tx);
            } else if (old?.Status == "finished") {
                if (session.PlanId != null) {
                    // Undo completion restores the plan without introducing another workout entry.
                    if (plan != null) WriteWorkout(db, plan with { Status = "planned" }, tx);
                } else { using var remove = Command(db,"DELETE FROM workouts WHERE id=$id",tx,("$id",session.WorkoutId.ToString())); remove.ExecuteNonQuery(); }
            }
            if (session.ImportedWorkoutId is {} importedId) {
                using var query = Command(db,"SELECT payload FROM workouts WHERE id=$id",tx,("$id", importedId.ToString()));
                var imported = query.ExecuteScalar() is string payload ? ReadJson<WorkoutRecord>(payload) : null;
                if (imported == null || imported.IsManual || imported.Kind != "gym" || imported.StartTime == null || session.FinishedAt == null ||
                    imported.StartTime >= session.FinishedAt || imported.StartTime.Value.AddSeconds(imported.DurationSeconds) <= session.StartedAt)
                    throw new ArgumentException("Select an overlapping imported strength session.");
                using var others = Command(db,"SELECT payload FROM gym_sessions WHERE id<>$id",tx,("$id",session.Id.ToString()));
                using var reader = others.ExecuteReader(); while(reader.Read()) if(ReadJson<GymSession>(reader.GetString(0)).ImportedWorkoutId == importedId) throw new ArgumentException("This import is already linked.");
            }
            GymWrite(db,"gym_sessions",session.Id.ToString(),session,tx); tx.Commit(); return true;
        });
    }
    public Task<WorkoutTotals> GetWorkoutTotalsAsync() => Execute(db => {
        var sessions = HabitRead<GymSession>(db,"gym_sessions");
        var excluded = sessions.Where(s => s.Status == "finished" && s.ImportedWorkoutId != null).Select(s => s.ImportedWorkoutId!.Value).ToHashSet();
        return WorkoutTotals.Completed(HabitRead<WorkoutRecord>(db,"workouts").Where(w => !excluded.Contains(w.Id)));
    });
}
