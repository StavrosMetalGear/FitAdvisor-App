namespace FitAdvisor.Core;

// Local calendar dates are intentional: travelling never moves a logged day.
public sealed record HabitRecord(Guid Id, DateOnly Date, string Kind, string Name,
    double Amount, double? Abv = null, int? Craving = null, string? Trigger = null, Guid? GoalId = null)
{
    public double AlcoholGrams => Kind == "alcohol" ? Amount * Abv!.Value / 100 * 0.789 : 0;
    public void Validate()
    {
        if (Id == Guid.Empty) throw new ArgumentException("Record ID is required.");
        DiaryValidation.Date(Date);
        if (Date > DateOnly.FromDateTime(DateTime.Today)) throw new ArgumentException("Records cannot be in the future.");
        if (Kind is not ("smoking" or "alcohol" or "eating")) throw new ArgumentException("Unknown tracker.");
        DiaryValidation.Text(Name, 200, "Name");
        DiaryValidation.Number(Amount, 0, Kind == "alcohol" ? 10000 : 1000, "Amount");
        if (Kind == "smoking" && Amount != Math.Truncate(Amount)) throw new ArgumentException("Enter whole cigarettes.");
        if (Kind == "alcohol")
        {
            if (Abv == null || Amount <= 0) throw new ArgumentException("Volume and ABV are required.");
            DiaryValidation.Number(Abv.Value, 0, 100, "ABV (%)");
        }
        else if (Abv != null) throw new ArgumentException("ABV belongs to alcohol only.");
        if (Craving is < 0 or > 10 || (Kind != "smoking" && Craving != null)) throw new ArgumentException("Craving must be 0–10 for smoking.");
        if (Trigger?.Length > 1000) throw new ArgumentException("Notes must be at most 1000 characters.");
        if (Kind == "eating" && (GoalId == null || GoalId == Guid.Empty)) throw new ArgumentException("Choose an eating goal.");
        if (Kind != "eating" && GoalId != null) throw new ArgumentException("Eating goal belongs to eating records only.");
    }
}

public sealed record HabitGoal(Guid Id, string Kind, string Name, string Unit, double WeeklyTarget, bool AtMost = true)
{
    public void Validate()
    {
        if (Id == Guid.Empty || Kind is not ("smoking" or "alcohol" or "eating")) throw new ArgumentException("Invalid goal.");
        DiaryValidation.Text(Name, 200, "Goal"); DiaryValidation.Text(Unit, 50, "Unit");
        DiaryValidation.Number(WeeklyTarget, 0, 100000, "Weekly target");
        if (Kind == "smoking" && Unit != "cigarettes" || Kind == "alcohol" && Unit != "g alcohol") throw new ArgumentException("Use cigarettes or grams of alcohol.");
    }
}

public sealed record HabitSettings(bool Smoking = false, bool Alcohol = false, bool Eating = false,
    bool Education = false, DateOnly? QuitDate = null, double BaselineCigarettes = 0,
    double CostPerCigarette = 0, string Currency = "EUR")
{
    public void Validate()
    {
        if (QuitDate is { } date) DiaryValidation.Date(date);
        DiaryValidation.Number(BaselineCigarettes, 0, 1000, "Baseline cigarettes/day");
        DiaryValidation.Number(CostPerCigarette, 0, 1000, "Cost per cigarette");
        DiaryValidation.Text(Currency, 10, "Currency");
    }
}

public static class HabitCalculations
{
    public static DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    // Missing days are unknown, never assumed smoke-free. One smoking total per date.
    public static double MoneySaved(IEnumerable<HabitRecord> records, HabitSettings settings) =>
        records.Where(r => r.Kind == "smoking" && settings.QuitDate is { } quit && r.Date >= quit)
        .Sum(r => Math.Max(0, settings.BaselineCigarettes - r.Amount) * settings.CostPerCigarette);
    public static double Total(IEnumerable<HabitRecord> records, HabitGoal goal, DateOnly week) =>
        records.Where(r => r.Date >= week && r.Date <= week.AddDays(6) && r.Kind == goal.Kind &&
            (goal.Kind != "eating" || r.GoalId == goal.Id)).Sum(r => r.Kind == "alcohol" ? r.AlcoholGrams : r.Amount);
}
