namespace FitAdvisor.Core;

public sealed record StrengthSet(Guid Id, string Exercise, int Reps, double LoadKg, bool Warmup = false)
{
    public void Validate()
    {
        if (Id == Guid.Empty) throw new ArgumentException("A set ID is required.");
        DiaryValidation.Text(Exercise, 100, "Exercise");
        DiaryValidation.Number(Reps, 1, 1000, "Reps");
        DiaryValidation.Number(LoadKg, 0, 1000, "External load (kg)");
    }
}

public sealed record WorkoutRecord(Guid Id, DateOnly Date, int UtcOffsetMinutes, string Kind,
    string Title, double DurationSeconds, double? DistanceKm, string? Notes, StrengthSet[] Sets,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string Source = "manual",
    string? SourcePackage = null, string? ExternalId = null, DateTimeOffset? StartTime = null,
    int? ExternalExerciseType = null)
{
    public bool IsManual => Source == "manual";
    public string Summary => $"{Date:yyyy-MM-dd} · {Title} · {DurationSeconds / 60:g} min · {Source}";
    public string Details => DistanceKm is > 0
        ? $"{DistanceKm:g} km · pace {DurationSeconds / 60 / DistanceKm.Value:g3} min/km"
        : Sets.Length > 0 ? $"{Sets.Length} strength sets" : "Distance not recorded";
    public void Validate()
    {
        if (Id == Guid.Empty) throw new ArgumentException("A workout ID is required.");
        DiaryValidation.Date(Date);
        DiaryValidation.Number(UtcOffsetMinutes, -840, 840, "UTC offset");
        if (Kind is not ("run" or "gym" or "other")) throw new ArgumentException("Invalid workout type.");
        DiaryValidation.Text(Title, 200, "Workout title");
        DiaryValidation.Number(DurationSeconds, 1, 604800, "Duration (seconds)");
        if (DistanceKm is { } km) DiaryValidation.Number(km, 0.001, 2000, "Distance (km)");
        if (IsManual && Kind == "run" && DistanceKm == null) throw new ArgumentException("Enter run distance.");
        if (Notes?.Length > 2000) throw new ArgumentException("Notes must be at most 2000 characters.");
        if (Sets == null || Sets.Length > 1000) throw new ArgumentException("Invalid set count.");
        if (Sets.Select(s => s?.Id).Distinct().Count() != Sets.Length) throw new ArgumentException("Duplicate sets.");
        foreach (var set in Sets) { ArgumentNullException.ThrowIfNull(set); set.Validate(); }
        if (Kind != "gym" && Sets.Length != 0) throw new ArgumentException("Sets belong to gym sessions.");
        if (CreatedAt == default || UpdatedAt < CreatedAt) throw new ArgumentException("Invalid workout timestamps.");
        if (IsManual)
        {
            if (SourcePackage != null || ExternalId != null || ExternalExerciseType != null)
                throw new ArgumentException("Manual workouts cannot carry import identifiers.");
        }
        else
        {
            if (Source != "health-connect") throw new ArgumentException("Unknown workout source.");
            DiaryValidation.Text(SourcePackage, 255, "Source package");
            DiaryValidation.Text(ExternalId, 255, "Health Connect record ID");
            if (StartTime == null || ExternalExerciseType == null) throw new ArgumentException("Missing import metadata.");
        }
    }
}

public interface IWorkoutDiary
{
    Task<IReadOnlyList<WorkoutRecord>> GetWorkoutsAsync();
    Task SaveWorkoutAsync(WorkoutRecord workout);
    Task DeleteWorkoutAsync(Guid id);
    Task<int> ImportWorkoutsAsync(IReadOnlyList<WorkoutRecord> workouts);
}
