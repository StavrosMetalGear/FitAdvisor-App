using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using FitAdvisor.Core;
using Microsoft.Data.Sqlite;

namespace FitAdvisor.Storage;

// Each operation uses a short-lived, unpooled connection on a worker thread.
// No credentials, network service or web database are involved.
public sealed partial class SqlitePersonalDiary : IPersonalDiary, IWorkoutDiary
{
    public const string TargetFormulaVersion = "mifflin-st-jeor-goal-adjustment-v1";
    private readonly string connectionString;
    private readonly SemaphoreSlim gate = new(1, 1);
    public SqlitePersonalDiary(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath, ForeignKeys = true,
            Pooling = false, DefaultTimeout = 10 }.ToString();
    }
    private async Task<T> Execute<T>(Func<SqliteConnection, T> action)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                using var connection = new SqliteConnection(connectionString);
                connection.Open();
                Migrate(connection);
                return action(connection);
            }).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
    private static SqliteCommand Command(SqliteConnection db, string sql, SqliteTransaction? tx = null,
        params (string Name, object? Value)[] args)
    {
        var command = db.CreateCommand(); command.CommandText = sql; command.Transaction = tx;
        foreach (var (name, value) in args) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }
    private static void Migrate(SqliteConnection db)
    {
        using var tx = db.BeginTransaction();
        using var version = Command(db, "PRAGMA user_version", tx);
        var current = Convert.ToInt32(version.ExecuteScalar(), CultureInfo.InvariantCulture);
        if (current > 5) throw new InvalidOperationException("This database was created by a newer FitAdvisor version. Update the app; data has not been changed.");
        if (current == 0)
        {
            using var create = Command(db, """
                CREATE TABLE profile_revisions (
                    id TEXT PRIMARY KEY, effective_date TEXT NOT NULL, created_at TEXT NOT NULL, payload TEXT NOT NULL);
                CREATE INDEX profile_effective ON profile_revisions(effective_date);
                CREATE TABLE daily_targets (
                    local_date TEXT PRIMARY KEY, profile_id TEXT NOT NULL REFERENCES profile_revisions(id),
                    payload TEXT NOT NULL);
                CREATE TABLE meals (
                    id TEXT PRIMARY KEY, local_date TEXT NOT NULL, name TEXT NOT NULL CHECK(length(trim(name)) > 0),
                    quantity REAL NOT NULL CHECK(quantity > 0), unit TEXT NOT NULL CHECK(length(trim(unit)) > 0),
                    kcal REAL NOT NULL CHECK(kcal >= 0), protein_g REAL CHECK(protein_g >= 0),
                    carbs_g REAL CHECK(carbs_g >= 0), fat_g REAL CHECK(fat_g >= 0),
                    profile_id TEXT REFERENCES profile_revisions(id), created_at TEXT NOT NULL, updated_at TEXT NOT NULL);
                CREATE INDEX meals_date ON meals(local_date);
                CREATE TABLE weights (
                    id TEXT PRIMARY KEY, local_date TEXT NOT NULL, weight_kg REAL NOT NULL CHECK(weight_kg BETWEEN 20 AND 500),
                    notes TEXT, source TEXT NOT NULL DEFAULT 'manual' CHECK(source = 'manual'),
                    profile_id TEXT REFERENCES profile_revisions(id), created_at TEXT NOT NULL, updated_at TEXT NOT NULL);
                CREATE INDEX weights_date ON weights(local_date);
                PRAGMA user_version = 1;
                """, tx);
            create.ExecuteNonQuery();
        }
        if (current < 2)
        {
            using var workouts = Command(db, """
                CREATE TABLE workouts (
                    id TEXT PRIMARY KEY, local_date TEXT NOT NULL, source TEXT NOT NULL,
                    source_package TEXT, external_id TEXT, payload TEXT NOT NULL);
                CREATE INDEX workouts_date ON workouts(local_date);
                CREATE UNIQUE INDEX workouts_import ON workouts(source,source_package,external_id)
                    WHERE external_id IS NOT NULL;
                PRAGMA user_version = 2;
                """, tx);
            workouts.ExecuteNonQuery();
        }
        if (current < 3)
        {
            using var habits = Command(db, """
                CREATE TABLE habit_goals(id TEXT PRIMARY KEY, payload TEXT NOT NULL);
                CREATE TABLE habits(id TEXT PRIMARY KEY, local_date TEXT NOT NULL, kind TEXT NOT NULL,
                    goal_id TEXT REFERENCES habit_goals(id), payload TEXT NOT NULL);
                CREATE INDEX habits_date ON habits(local_date);
                CREATE UNIQUE INDEX smoking_day ON habits(local_date) WHERE kind='smoking';
                CREATE TABLE habit_settings(id INTEGER PRIMARY KEY CHECK(id=1), payload TEXT NOT NULL);
                PRAGMA user_version=3;
                """, tx);
            habits.ExecuteNonQuery();
        }
        if (current < 4)
        {
            using var food = Command(db, "ALTER TABLE meals ADD COLUMN nutrition_snapshot TEXT; PRAGMA user_version=4;", tx);
            food.ExecuteNonQuery();
        }
        if (current < 5)
        {
            using var gym = Command(db, "CREATE TABLE gym_sessions(id TEXT PRIMARY KEY,payload TEXT NOT NULL); CREATE TABLE gym_exercises(id TEXT PRIMARY KEY,payload TEXT NOT NULL); CREATE TABLE gym_preferences(id TEXT PRIMARY KEY,payload TEXT NOT NULL); PRAGMA user_version=5;", tx);
            gym.ExecuteNonQuery();
        }
        tx.Commit();
    }
    private static string Day(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static JsonTypeInfo<T> TypeInfo<T>() => (JsonTypeInfo<T>)(DiaryJsonContext.Default.GetTypeInfo(typeof(T))
        ?? throw new InvalidOperationException("Unsupported diary serialization type."));
    private static string Json<T>(T value) => JsonSerializer.Serialize(value, TypeInfo<T>());
    private static T ReadJson<T>(string json) => JsonSerializer.Deserialize(json, TypeInfo<T>())
        ?? throw new InvalidDataException("A saved record is invalid.");
    private static ProfileRevision? ProfileFor(SqliteConnection db, DateOnly? date = null, SqliteTransaction? tx = null)
    {
        using var command = Command(db, date == null
            ? "SELECT payload FROM profile_revisions ORDER BY effective_date DESC, rowid DESC LIMIT 1"
            : "SELECT payload FROM profile_revisions WHERE effective_date <= $date ORDER BY effective_date DESC, rowid DESC LIMIT 1",
            tx, ("$date", date == null ? null : Day(date.Value)));
        return command.ExecuteScalar() is string payload ? ReadJson<ProfileRevision>(payload) : null;
    }
    public Task<ProfileRevision?> GetProfileAsync() => Execute(db => ProfileFor(db));
    public Task<IReadOnlyList<ProfileRevision>> GetProfileHistoryAsync() => Execute<IReadOnlyList<ProfileRevision>>(db =>
    {
        using var command = Command(db, "SELECT payload FROM profile_revisions ORDER BY effective_date DESC, rowid DESC");
        using var reader = command.ExecuteReader(); var history = new List<ProfileRevision>();
        while (reader.Read()) history.Add(ReadJson<ProfileRevision>(reader.GetString(0)));
        return history;
    });
    public Task<ProfileRevision> SaveProfileAsync(ProfileDraft profile, DateOnly effectiveDate)
    {
        profile.Validate(); DiaryValidation.Date(effectiveDate);
        return Execute(db =>
        {
            var revision = new ProfileRevision(Guid.NewGuid(), effectiveDate, DateTimeOffset.UtcNow,
                profile with { Name = profile.Name.Trim() });
            using var tx = db.BeginTransaction();
            using var command = Command(db, "INSERT INTO profile_revisions(id,effective_date,created_at,payload) VALUES($id,$date,$time,$payload)",
                tx, ("$id", revision.Id.ToString()), ("$date", Day(effectiveDate)), ("$time", revision.CreatedAt.ToString("O")), ("$payload", Json(revision)));
            command.ExecuteNonQuery();
            // An explicit profile edit updates that day's target only. Earlier snapshots/entry contexts remain intact.
            SaveTarget(db, revision, effectiveDate, tx);
            tx.Commit(); return revision;
        });
    }
    private static DailyTargetSnapshot SaveTarget(SqliteConnection db, ProfileRevision profile, DateOnly date, SqliteTransaction tx)
    {
        var values = profile.Values;
        var snapshot = new DailyTargetSnapshot(date, profile.Id, TargetFormulaVersion, values.Inputs,
            values.ActivityMultiplier, values.GoalAdjustmentKcal, values.Calculate().TargetKcal);
        using var command = Command(db, "INSERT INTO daily_targets(local_date,profile_id,payload) VALUES($date,$id,$payload) ON CONFLICT(local_date) DO UPDATE SET profile_id=excluded.profile_id,payload=excluded.payload",
            tx, ("$date", Day(date)), ("$id", profile.Id.ToString()), ("$payload", Json(snapshot)));
        command.ExecuteNonQuery(); return snapshot;
    }
    private static DailyTargetSnapshot? TargetFor(SqliteConnection db, DateOnly date, SqliteTransaction tx)
    {
        using var query = Command(db, "SELECT payload FROM daily_targets WHERE local_date=$date", tx, ("$date", Day(date)));
        if (query.ExecuteScalar() is string payload) return ReadJson<DailyTargetSnapshot>(payload);
        var profile = ProfileFor(db, date, tx);
        return profile == null ? null : SaveTarget(db, profile, date, tx);
    }
    private static Guid? EntryProfile(SqliteConnection db, string table, Guid id, DateOnly date, SqliteTransaction tx)
    {
        // Preserve the context on same-day edits. Re-dating uses the context of the new day.
        using var query = Command(db, $"SELECT local_date,profile_id FROM {table} WHERE id=$id", tx, ("$id", id.ToString()));
        using var reader = query.ExecuteReader();
        if (reader.Read() && reader.GetString(0) == Day(date)) return reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1));
        reader.Close();
        return TargetFor(db, date, tx)?.ProfileRevisionId;
    }
    public Task<IReadOnlyList<MealRecord>> GetMealsAsync(DateOnly date)
    {
        DiaryValidation.Date(date);
        return Execute<IReadOnlyList<MealRecord>>(db =>
        {
            using var command = Command(db, "SELECT id,local_date,name,quantity,unit,kcal,protein_g,carbs_g,fat_g,profile_id,nutrition_snapshot FROM meals WHERE local_date=$date ORDER BY created_at,id",
                null, ("$date", Day(date)));
            using var reader = command.ExecuteReader(); var list = new List<MealRecord>();
            while (reader.Read()) list.Add(new(Guid.Parse(reader.GetString(0)), DateOnly.ParseExact(reader.GetString(1), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                reader.GetString(2), reader.GetDouble(3), reader.GetString(4), reader.GetDouble(5), OptionalNumber(reader, 6), OptionalNumber(reader, 7), OptionalNumber(reader, 8), OptionalId(reader, 9), reader.IsDBNull(10) ? null : ReadJson<FoodNutritionSnapshot>(reader.GetString(10))));
            return list;
        });
    }
    private static double? OptionalNumber(SqliteDataReader reader, int field) => reader.IsDBNull(field) ? null : reader.GetDouble(field);
    private static Guid? OptionalId(SqliteDataReader reader, int field) => reader.IsDBNull(field) ? null : Guid.Parse(reader.GetString(field));
    public Task SaveMealAsync(MealRecord meal)
    {
        meal.Validate(); if (meal.Id == Guid.Empty) throw new ArgumentException("A meal ID is required.");
        return Execute(db =>
        {
            using var tx = db.BeginTransaction(); var profileId = EntryProfile(db, "meals", meal.Id, meal.Date, tx);
            var now = DateTimeOffset.UtcNow.ToString("O");
            using var command = Command(db, """
                INSERT INTO meals(id,local_date,name,quantity,unit,kcal,protein_g,carbs_g,fat_g,profile_id,created_at,updated_at,nutrition_snapshot)
                VALUES($id,$date,$name,$quantity,$unit,$kcal,$protein,$carbs,$fat,$profile,$now,$now,$snapshot)
                ON CONFLICT(id) DO UPDATE SET local_date=excluded.local_date,name=excluded.name,quantity=excluded.quantity,
                unit=excluded.unit,kcal=excluded.kcal,protein_g=excluded.protein_g,carbs_g=excluded.carbs_g,
                fat_g=excluded.fat_g,profile_id=excluded.profile_id,updated_at=excluded.updated_at,nutrition_snapshot=excluded.nutrition_snapshot
                """, tx, ("$id", meal.Id.ToString()), ("$date", Day(meal.Date)), ("$name", meal.Name.Trim()),
                ("$quantity", meal.Quantity), ("$unit", meal.Unit.Trim()), ("$kcal", meal.CaloriesKcal),
                ("$protein", meal.ProteinG), ("$carbs", meal.CarbohydratesG), ("$fat", meal.FatG), ("$profile", profileId?.ToString()), ("$now", now), ("$snapshot", meal.NutritionSnapshot == null ? null : Json(meal.NutritionSnapshot)));
            command.ExecuteNonQuery(); tx.Commit(); return true;
        });
    }
    public Task DeleteMealAsync(Guid id) => Delete("meals", id);
    private Task Delete(string table, Guid id) => Execute(db =>
    {
        using var command = Command(db, $"DELETE FROM {table} WHERE id=$id", null, ("$id", id.ToString()));
        return command.ExecuteNonQuery();
    });
    public Task<IReadOnlyList<WeightRecord>> GetWeightsAsync() => Execute<IReadOnlyList<WeightRecord>>(db =>
    {
        using var command = Command(db, "SELECT id,local_date,weight_kg,notes,profile_id FROM weights ORDER BY local_date DESC,created_at DESC,id");
        using var reader = command.ExecuteReader(); var list = new List<WeightRecord>();
        while (reader.Read()) list.Add(new(Guid.Parse(reader.GetString(0)), DateOnly.ParseExact(reader.GetString(1), "yyyy-MM-dd", CultureInfo.InvariantCulture),
            reader.GetDouble(2), reader.IsDBNull(3) ? null : reader.GetString(3), OptionalId(reader, 4)));
        return list;
    });
    public Task SaveWeightAsync(WeightRecord weight)
    {
        weight.Validate(); if (weight.Id == Guid.Empty) throw new ArgumentException("A weight ID is required.");
        return Execute(db =>
        {
            using var tx = db.BeginTransaction(); var profileId = EntryProfile(db, "weights", weight.Id, weight.Date, tx);
            using var command = Command(db, """
                INSERT INTO weights(id,local_date,weight_kg,notes,profile_id,created_at,updated_at)
                VALUES($id,$date,$weight,$notes,$profile,$now,$now)
                ON CONFLICT(id) DO UPDATE SET local_date=excluded.local_date,weight_kg=excluded.weight_kg,
                notes=excluded.notes,profile_id=excluded.profile_id,updated_at=excluded.updated_at
                """, tx, ("$id", weight.Id.ToString()), ("$date", Day(weight.Date)), ("$weight", weight.WeightKg),
                ("$notes", weight.Notes?.Trim()), ("$profile", profileId?.ToString()), ("$now", DateTimeOffset.UtcNow.ToString("O")));
            command.ExecuteNonQuery(); tx.Commit(); return true;
        });
    }
    public Task DeleteWeightAsync(Guid id) => Delete("weights", id);
    public Task<DailyDashboard> GetDashboardAsync(DateOnly date)
    {
        DiaryValidation.Date(date);
        return Execute(db =>
        {
            using var tx = db.BeginTransaction(); var target = TargetFor(db, date, tx);
            using var query = Command(db, "SELECT COALESCE(SUM(kcal),0),COUNT(*) FROM meals WHERE local_date=$date", tx, ("$date", Day(date)));
            using var reader = query.ExecuteReader(); reader.Read();
            var result = new DailyDashboard(date, target, reader.GetDouble(0), reader.GetInt32(1));
            reader.Close(); tx.Commit(); return result;
        });
    }
}
