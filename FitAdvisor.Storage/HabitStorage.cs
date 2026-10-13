using FitAdvisor.Core;
using Microsoft.Data.Sqlite;

namespace FitAdvisor.Storage;

public sealed partial class SqlitePersonalDiary
{
    private static T[] HabitRead<T>(SqliteConnection db, string table, SqliteTransaction? tx = null)
    {
        using var cmd = Command(db, $"SELECT payload FROM {table} ORDER BY rowid", tx);
        using var reader = cmd.ExecuteReader(); var list = new List<T>();
        while (reader.Read()) list.Add(ReadJson<T>(reader.GetString(0)));
        return list.ToArray();
    }
    public Task<HabitRecord[]> GetHabitsAsync() => Execute(db => HabitRead<HabitRecord>(db, "habits"));
    public Task<HabitGoal[]> GetHabitGoalsAsync() => Execute(db => HabitRead<HabitGoal>(db, "habit_goals"));
    public Task<HabitSettings> GetHabitSettingsAsync() => Execute(db => HabitRead<HabitSettings>(db, "habit_settings").SingleOrDefault() ?? new());
    private static void WriteHabit(SqliteConnection db, HabitRecord record, SqliteTransaction? tx = null)
    {
        using var cmd = Command(db, "INSERT INTO habits(id,local_date,kind,goal_id,payload) VALUES($id,$date,$kind,$goal,$payload) ON CONFLICT(id) DO UPDATE SET local_date=excluded.local_date,kind=excluded.kind,goal_id=excluded.goal_id,payload=excluded.payload", tx,
            ("$id", record.Id.ToString()), ("$date", Day(record.Date)), ("$kind", record.Kind), ("$goal", record.GoalId?.ToString()), ("$payload", Json(record)));
        cmd.ExecuteNonQuery();
    }
    private static void WriteGoal(SqliteConnection db, HabitGoal goal, SqliteTransaction? tx = null)
    {
        using var cmd = Command(db, "INSERT INTO habit_goals(id,payload) VALUES($id,$payload) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload", tx, ("$id", goal.Id.ToString()), ("$payload", Json(goal))); cmd.ExecuteNonQuery();
    }
    private static void WriteSettings(SqliteConnection db, HabitSettings settings, SqliteTransaction? tx = null)
    {
        using var cmd = Command(db, "INSERT INTO habit_settings(id,payload) VALUES(1,$payload) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload", tx, ("$payload", Json(settings))); cmd.ExecuteNonQuery();
    }
    public Task SaveHabitAsync(HabitRecord record)
    {
        record.Validate();
        return Execute(db => {
            using var tx = db.BeginTransaction();
            if (record.Kind == "smoking" && HabitRead<HabitRecord>(db, "habits", tx).Any(r => r.Kind == "smoking" && r.Date == record.Date && r.Id != record.Id))
                throw new ArgumentException("A smoking total exists for this date. Edit that record.");
            if (record.Kind == "eating" && !HabitRead<HabitGoal>(db, "habit_goals", tx).Any(g => g.Id == record.GoalId && g.Kind == "eating"))
                throw new ArgumentException("Eating goal is missing.");
            WriteHabit(db, record, tx); tx.Commit(); return true;
        });
    }
    public Task SaveHabitGoalAsync(HabitGoal goal)
    {
        goal.Validate(); return Execute(db => {
            var existing = HabitRead<HabitGoal>(db, "habit_goals").FirstOrDefault(g => g.Id == goal.Id);
            if (existing != null && (existing.Kind != goal.Kind || existing.Unit != goal.Unit)) throw new ArgumentException("Keep the original tracker and unit; create another goal instead.");
            WriteGoal(db, goal); return true;
        });
    }
    public Task SaveHabitSettingsAsync(HabitSettings settings)
    { settings.Validate(); return Execute(db => { WriteSettings(db, settings); return true; }); }
    public Task DeleteHabitAsync(Guid id) => Delete("habits", id);
    public Task DeleteHabitGoalAsync(Guid id) => Execute(db => {
        using var tx = db.BeginTransaction();
        using var count = Command(db, "SELECT COUNT(*) FROM habits WHERE goal_id=$id", tx, ("$id", id.ToString()));
        if (Convert.ToInt32(count.ExecuteScalar()) > 0) throw new ArgumentException("This goal has records. Delete or move those records first.");
        using var cmd = Command(db, "DELETE FROM habit_goals WHERE id=$id", tx, ("$id", id.ToString())); cmd.ExecuteNonQuery(); tx.Commit(); return true;
    });
}
