namespace FitAdvisor.Core;

public sealed record ProfileDraft(string Name, double WeightKg, double HeightCm, int AgeYears,
    FormulaSex Sex, double ActivityMultiplier, double GoalAdjustmentKcal, double? TargetWeightKg = null)
{
    public CalculationInputs Inputs => new(WeightKg, HeightCm, AgeYears, Sex);
    public NutritionEstimate Calculate() => NutritionCalculations.Estimate(Inputs, ActivityMultiplier,
        GoalAdjustmentKcal, ActivityAccounting.IncludesExercise);
    public void Validate()
    {
        DiaryValidation.Text(Name, 100, nameof(Name));
        Calculate();
        if (TargetWeightKg is { } weight) DiaryValidation.Number(weight, 20, 500, nameof(TargetWeightKg));
    }
}
public sealed record ProfileRevision(Guid Id, DateOnly EffectiveDate, DateTimeOffset CreatedAt, ProfileDraft Values);
public sealed record MealRecord(Guid Id, DateOnly Date, string Name, double Quantity, string Unit,
    double CaloriesKcal, double? ProteinG = null, double? CarbohydratesG = null, double? FatG = null,
    Guid? ProfileRevisionId = null, FoodNutritionSnapshot? NutritionSnapshot = null)
{
    public string Summary => $"{Date:yyyy-MM-dd} · {Name}: {Quantity:g} {Unit} · {CaloriesKcal:g} kcal";
    public string Macros => $"Protein {Show(ProteinG)}, carbs {Show(CarbohydratesG)}, fat {Show(FatG)} (totals)";
    public string FoodSource => NutritionSnapshot?.Attribution ?? "Manually entered nutrition totals";
    private static string Show(double? value) => value is { } n ? $"{n:g} g" : "unknown";
    public void Validate()
    {
        DiaryValidation.Date(Date);
        DiaryValidation.Text(Name, 200, nameof(Name));
        DiaryValidation.Text(Unit, 30, nameof(Unit));
        DiaryValidation.Number(Quantity, 0.001, 100000, nameof(Quantity));
        DiaryValidation.Number(CaloriesKcal, 0, 50000, nameof(CaloriesKcal));
        foreach (var macro in new[] { ProteinG, CarbohydratesG, FatG })
            if (macro is { } value) DiaryValidation.Number(value, 0, 10000, "Macros");
        if (NutritionSnapshot is { } snapshot)
        {
            snapshot.Validate();
            if(!snapshot.Usable)throw new ArgumentException("Check missing or questionable nutrition against the label before logging.");
            if (Name != snapshot.Description || Unit != snapshot.Unit || Quantity != snapshot.Grams ||
                CaloriesKcal != snapshot.Calories || ProteinG != snapshot.Protein || CarbohydratesG != snapshot.Carbs || FatG != snapshot.Fat)
                throw new ArgumentException("Meal totals must match the saved food nutrition snapshot.");
        }
    }
}
public sealed record WeightRecord(Guid Id, DateOnly Date, double WeightKg, string? Notes = null, Guid? ProfileRevisionId = null)
{
    public string Summary => $"{Date:yyyy-MM-dd} · {WeightKg:g} kg · manual";
    public void Validate()
    {
        DiaryValidation.Date(Date);
        DiaryValidation.Number(WeightKg, 20, 500, nameof(WeightKg));
        if (Notes?.Length > 1000) throw new ArgumentException("Notes must be at most 1000 characters.");
    }
}
public sealed record DailyTargetSnapshot(DateOnly Date, Guid ProfileRevisionId, string FormulaVersion,
    CalculationInputs Inputs, double ActivityMultiplier, double GoalAdjustmentKcal, double TargetKcal);
public sealed record DailyDashboard(DateOnly Date, DailyTargetSnapshot? Target, double ConsumedKcal, int MealCount)
{
    public double? RemainingKcal => Target?.TargetKcal - ConsumedKcal;
}
public static class DiaryValidation
{
    public static void Text(string? value, int limit, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit)
            throw new ArgumentException($"{name} is required and must be at most {limit} characters.");
    }
    public static void Number(double value, double min, double max, string name)
    {
        if (!double.IsFinite(value) || value < min || value > max)
            throw new ArgumentException($"{name} must be finite and between {min} and {max}.");
    }
    public static void Date(DateOnly date)
    {
        if (date == default) throw new ArgumentException("A valid local date is required.");
    }
}
public interface IPersonalDiary
{
    Task<ProfileRevision?> GetProfileAsync();
    Task<ProfileRevision> SaveProfileAsync(ProfileDraft profile, DateOnly effectiveDate);
    Task<IReadOnlyList<ProfileRevision>> GetProfileHistoryAsync();
    Task<IReadOnlyList<MealRecord>> GetMealsAsync(DateOnly date);
    Task SaveMealAsync(MealRecord meal);
    Task DeleteMealAsync(Guid id);
    Task<IReadOnlyList<WeightRecord>> GetWeightsAsync();
    Task SaveWeightAsync(WeightRecord weight);
    Task DeleteWeightAsync(Guid id);
    Task<DailyDashboard> GetDashboardAsync(DateOnly date);
}
