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
    int? ExternalExerciseType = null, string Status = "completed")
{
    // Old stored payloads and backups have no Status: they remain completed.
    public bool IsPlanned => Status == "planned";
    public bool IsCompleted => Status == "completed";
    public bool IsManual => Source == "manual";
    public Guid? GymDetailId { get; init; }
    public bool ExcludedFromTotals { get; init; }
    public bool CanEdit => IsManual && GymDetailId == null;
    public bool CanOpenGymSession => Kind == "gym" && (IsPlanned || GymDetailId != null);
    public string Summary => $"{(IsPlanned ? "Planned" : "Completed")} · {Date:yyyy-MM-dd} · {Title} · {DurationSeconds / 60:0.#} min · {Source}";
    public string Details => (ExcludedFromTotals ? "Linked Gym Session import (excluded from duplicate totals) · " : GymDetailId != null ? "Gym Session: open detailed sets · " : "") + (IsPlanned ? $"Planned duration: {DurationSeconds / 60:g} min (excluded from completed totals)" :
        (DistanceKm is > 0
        ? $"{DistanceKm:g} km · pace {DurationSeconds / 60 / DistanceKm.Value:g3} min/km"
        : Sets.Length > 0 ? $"{Sets.Length} strength sets" : GymDetailId != null ? "Manual strength log; heart rate and calories unavailable" : "Distance not recorded")
        + (IsManual ? "" : $" · Source: {SourcePackage} · exercise type {ExternalExerciseType}"));
    public void Validate()
    {
        if (Id == Guid.Empty) throw new ArgumentException("A workout ID is required.");
        DiaryValidation.Date(Date);
        if (Status is not ("planned" or "completed")) throw new ArgumentException("Invalid workout status.");
        if (IsPlanned && !IsManual) throw new ArgumentException("Imported sessions cannot be planned workouts.");
        DiaryValidation.Number(UtcOffsetMinutes, -840, 840, "UTC offset");
        if (Kind is not ("run" or "gym" or "other")) throw new ArgumentException("Invalid workout type.");
        DiaryValidation.Text(Title, 200, "Workout title");
        DiaryValidation.Number(DurationSeconds, 1, 604800, "Duration (seconds)");
        if (DistanceKm is { } km) DiaryValidation.Number(km, 0.001, 2000, "Distance (km)");
        if (IsManual && IsCompleted && Kind == "run" && DistanceKm == null) throw new ArgumentException("Enter actual run distance.");
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

    public WorkoutRecord Complete(DateOnly actualDate, double actualDurationSeconds, double? actualDistanceKm, DateOnly today)
    {
        if (!IsManual || !IsPlanned) throw new ArgumentException("Only a manual planned workout can be marked completed.");
        if (actualDate > today) throw new ArgumentException("The actual workout date cannot be in the future.");
        var result = this with { Status = "completed", Date = actualDate, DurationSeconds = actualDurationSeconds,
            DistanceKm = actualDistanceKm, UpdatedAt = DateTimeOffset.UtcNow };
        result.Validate(); return result;
    }
}

public sealed record WorkoutTotals(int Count, double DurationSeconds)
{
    public static WorkoutTotals Completed(IEnumerable<WorkoutRecord> records) =>
        new(records.Count(w => w.IsCompleted && !w.ExcludedFromTotals), records.Where(w => w.IsCompleted && !w.ExcludedFromTotals).Sum(w => w.DurationSeconds));
}

public interface IWorkoutDiary
{
    Task<IReadOnlyList<WorkoutRecord>> GetWorkoutsAsync();
    Task SaveWorkoutAsync(WorkoutRecord workout);
    Task DeleteWorkoutAsync(Guid id);
    Task<int> ImportWorkoutsAsync(IReadOnlyList<WorkoutRecord> workouts);
}
