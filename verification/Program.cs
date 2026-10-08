using FitAdvisor.Core;
static void Equal(double expected, double actual) { if (!double.IsFinite(actual) || Math.Abs(expected - actual) > 0.000001) throw new Exception($"Expected {expected}, got {actual}"); }
static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Invalid input accepted"); }
var male = new CalculationInputs(80, 180, 30, FormulaSex.Male);
Equal(1780, NutritionCalculations.RestingCalories(male));
Equal(1614, NutritionCalculations.RestingCalories(male with { Sex = FormulaSex.Female }));
Equal(1680, NutritionCalculations.RestingCalories(male with { AgeYears = 50 }));
Equal(20.3, NutritionCalculations.BodyFat(male).Percentage!.Value);
var estimate = NutritionCalculations.BodyFat(male);
if (estimate.Inputs != male || estimate.AlgorithmVersion != "deurenberg-adult-1991-v1") throw new Exception("Estimate provenance lost");
var plan = NutritionCalculations.Estimate(male, 1.2, -300, ActivityAccounting.BaselineExcludingExercise, 400, 0.5);
Equal(2136, plan.MaintenanceKcal); Equal(1836, plan.TargetKcal); Equal(2036, plan.FoodAllowanceKcal);
Equal(1836, NutritionCalculations.Estimate(male, 1.2, -300, ActivityAccounting.IncludesExercise, 400).FoodAllowanceKcal);
Reject(() => NutritionCalculations.Estimate(male, 1.2, 0, ActivityAccounting.IncludesExercise, 400, 1));
foreach (var value in new[] { 0d, -1d, double.NaN, double.PositiveInfinity }) Reject(() => NutritionCalculations.RestingCalories(male with { WeightKg = value }));
Reject(() => NutritionCalculations.RestingCalories(male with { HeightCm = 1.8 }));
Reject(() => NutritionCalculations.RestingCalories(male with { AgeYears = 17 }));
Reject(() => NutritionCalculations.RestingCalories(male with { Sex = (FormulaSex)99 }));
Reject(() => NutritionCalculations.Estimate(male, 1.2, 0, (ActivityAccounting)99));
Reject(() => NutritionCalculations.Estimate(male, double.NaN, 0, ActivityAccounting.IncludesExercise));
Reject(() => NutritionCalculations.Estimate(male, 1.2, 0, ActivityAccounting.BaselineExcludingExercise, -1));
Reject(() => NutritionCalculations.Estimate(male, 1.2, 0, ActivityAccounting.BaselineExcludingExercise, 400, 1.1));
if (NutritionCalculations.BodyFat(new(500, 100, 120, FormulaSex.Female)).Percentage != null) throw new Exception("Implausible body fat should be unavailable");
foreach (var sex in Enum.GetValues<FormulaSex>())
foreach (var weight in new[] { 55d, 80d, 103.7d })
{
    var inputs = male with { WeightKg = weight, Sex = sex };
    var bmr = 10 * weight + 6.25 * 180 - 5 * 30 + (sex == FormulaSex.Male ? 5 : -161);
    var adjusted = (int)((int)(bmr * 1.5 * 0.8) * 0.8);
    var legacy = NutritionCalculations.LegacyCaloriePlans(inputs);
    Equal((int)(adjusted * 1.1), legacy.Easy); Equal(adjusted, legacy.Medium); Equal((int)(adjusted * 0.9), legacy.Hard);
}
var corrected = NutritionCalculations.Estimate(male, 1.5, -534, ActivityAccounting.IncludesExercise);
Equal(2136, corrected.TargetKcal); // One 20% deficit from 2670 maintenance.
Equal(1708, NutritionCalculations.LegacyCaloriePlans(male).Medium); // Historical double reduction/truncation.
Console.WriteLine("Calculation checks passed.");
