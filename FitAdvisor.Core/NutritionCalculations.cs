namespace FitAdvisor.Core;

public enum FormulaSex { Female, Male }
public enum ActivityAccounting { IncludesExercise, BaselineExcludingExercise }
public sealed record CalculationInputs(double WeightKg, double HeightCm, int AgeYears, FormulaSex Sex);
public sealed record NutritionEstimate(double RestingKcal, double MaintenanceKcal, double TargetKcal, double ActivityKcal, double FoodAllowanceKcal);
public sealed record BodyFatEstimate(string AlgorithmVersion, CalculationInputs Inputs, double? Percentage);
public sealed record LegacyPlans(int Easy, int Medium, int Hard);

public static class NutritionCalculations
{
    public const string BodyFatVersion = "deurenberg-adult-1991-v1";
    public static void Validate(CalculationInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Range(input.WeightKg, 20, 500, nameof(input.WeightKg));
        Range(input.HeightCm, 100, 250, nameof(input.HeightCm));
        Range(input.AgeYears, 18, 120, nameof(input.AgeYears));
        if (!Enum.IsDefined(input.Sex)) throw new ArgumentOutOfRangeException(nameof(input.Sex));
    }
    private static void Range(double value, double min, double max, string name)
    {
        if (!double.IsFinite(value) || value < min || value > max)
            throw new ArgumentOutOfRangeException(name, $"Value must be finite and between {min} and {max}.");
    }
    public static double RestingCalories(CalculationInputs input)
    {
        Validate(input);
        return 10 * input.WeightKg + 6.25 * input.HeightCm - 5 * input.AgeYears + (input.Sex == FormulaSex.Male ? 5 : -161);
    }
    public static NutritionEstimate Estimate(CalculationInputs input, double activityMultiplier,
        double goalAdjustmentKcal, ActivityAccounting accounting, double activityKcal = 0, double exerciseCreditFraction = 0)
    {
        Range(activityMultiplier, 1, 2.5, nameof(activityMultiplier));
        Range(goalAdjustmentKcal, -1000, 1000, nameof(goalAdjustmentKcal));
        Range(activityKcal, 0, 10000, nameof(activityKcal));
        Range(exerciseCreditFraction, 0, 1, nameof(exerciseCreditFraction));
        if (!Enum.IsDefined(accounting)) throw new ArgumentOutOfRangeException(nameof(accounting));
        if (accounting == ActivityAccounting.IncludesExercise && exerciseCreditFraction != 0)
            throw new ArgumentException("Exercise credit requires a baseline multiplier excluding exercise.");
        var resting = RestingCalories(input);
        var maintenance = resting * activityMultiplier;
        var target = maintenance + goalAdjustmentKcal;
        if (target <= 0) throw new ArgumentException("The resulting calorie target must be positive.");
        return new(resting, maintenance, target, activityKcal, target + activityKcal * exerciseCreditFraction);
    }
    public static BodyFatEstimate BodyFat(CalculationInputs input)
    {
        Validate(input);
        var bmi = input.WeightKg / Math.Pow(input.HeightCm / 100, 2);
        var percentage = 1.2 * bmi + 0.23 * input.AgeYears - 10.8 * (input.Sex == FormulaSex.Male ? 1 : 0) - 5.4;
        // Do not clamp an implausible result into a seemingly valid measurement.
        return new(BodyFatVersion, input, percentage is >= 0 and <= 100 ? Math.Round(percentage, 1) : null);
    }
    public static LegacyPlans LegacyCaloriePlans(CalculationInputs input)
    {
        // Preserve historical truncation and both reductions for the existing screen only.
        var maintenance = (int)(RestingCalories(input) * 1.5 * 0.8);
        var adjusted = (int)(maintenance * 0.8);
        return new((int)(adjusted * 1.1), adjusted, (int)(adjusted * 0.9));
    }
}
