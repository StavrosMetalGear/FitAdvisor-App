namespace FitAdvisor.Core;

public sealed record MacroEnergy(double Protein, double Carbohydrates, double Fat, int Meals, int IncompleteMeals)
{
    public double Energy => Protein * 4 + Carbohydrates * 4 + Fat * 9;
    public bool CanChart => Meals > 0 && IncompleteMeals == 0 && Energy > 0;
    public double ProteinPercent => Energy > 0 ? Protein * 4 / Energy * 100 : 0;
    public double CarbohydratePercent => Energy > 0 ? Carbohydrates * 4 / Energy * 100 : 0;
    public double FatPercent => Energy > 0 ? Fat * 9 / Energy * 100 : 0;
    public static MacroEnergy From(IEnumerable<MealRecord> meals)
    {
        var rows = meals.ToArray();
        return new(rows.Sum(m=>m.ProteinG ?? 0), rows.Sum(m=>m.CarbohydratesG ?? 0), rows.Sum(m=>m.FatG ?? 0), rows.Length,
            rows.Count(m=>m.ProteinG == null || m.CarbohydratesG == null || m.FatG == null));
    }
    public static void ValidateTargets(double protein, double carbs, double fat)
    {
        foreach (var value in new[] {protein, carbs, fat}) DiaryValidation.Number(value, 0, 100, "Macro percentage target");
        if (Math.Abs(protein + carbs + fat - 100) > .001) throw new ArgumentException("Macro targets must total 100%.");
    }
}
