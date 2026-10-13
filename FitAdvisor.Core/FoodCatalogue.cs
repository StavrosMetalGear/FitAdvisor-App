using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FitAdvisor.Core;

public sealed record CatalogueFood(int FdcId, string Name, double? CaloriesPer100G, double? ProteinPer100G,
    double? CarbohydratesPer100G, double? FatPer100G, string GreekAliases)
{
    public const string Source = "USDA FoodData Central — SR Legacy, April 2018";
    public string Attribution => $"{Source} · FDC {FdcId}";
    public bool IsRaw => Regex.IsMatch(Name, @"\b(raw|uncooked)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public bool IsCooked => Regex.IsMatch(Name, @"\bcooked\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public string Preparation => IsRaw ? "Raw / ωμό — use raw weight. See full description."
        : IsCooked ? "Cooked / μαγειρεμένο — use cooked weight. See full description."
        : "Use the exact preparation in the full English description; raw/cooked is not always specified.";
    public FoodNutritionSnapshot Portion(double grams)
    {
        DiaryValidation.Number(grams, .001, 100000, "Grams");
        if (CaloriesPer100G is null) throw new ArgumentException("This food has no reported kcal value. Choose another food or enter known nutrition manually.");
        return new(Source, FdcId, Name, grams, CaloriesPer100G.Value, ProteinPer100G, CarbohydratesPer100G, FatPer100G);
    }
}

public sealed record FoodNutritionSnapshot(string Source, int FdcId, string Description, double Grams,
    double CaloriesPer100G, double? ProteinPer100G, double? CarbohydratesPer100G, double? FatPer100G)
{
    public double Calories => CaloriesPer100G * Grams / 100;
    public double? Protein => ProteinPer100G * Grams / 100;
    public double? Carbs => CarbohydratesPer100G * Grams / 100;
    public double? Fat => FatPer100G * Grams / 100;
    public string Attribution => $"{Source} · FDC {FdcId} · {Grams:g} g · nutrition snapshot";
    public MealRecord Meal(DateOnly date) => new(Guid.NewGuid(), date, Description, Grams, "g", Calories, Protein, Carbs, Fat, NutritionSnapshot: this);
    public void Validate()
    {
        DiaryValidation.Text(Source, 200, "Food source"); DiaryValidation.Text(Description, 200, "Food description");
        if (FdcId <= 0) throw new ArgumentException("Invalid FDC ID.");
        DiaryValidation.Number(Grams, .001, 100000, "Grams");
        foreach (var n in new[] { (double?)CaloriesPer100G, ProteinPer100G, CarbohydratesPer100G, FatPer100G })
            if (n is { } v) DiaryValidation.Number(v, 0, 10000, "Nutrition per 100 g");
    }
}

public static class FoodCatalogue
{
    private static readonly Lazy<IReadOnlyList<CatalogueFood>> foods = new(Load);
    public static IReadOnlyList<CatalogueFood> Foods => foods.Value;
    private static IReadOnlyList<CatalogueFood> Load()
    {
        using var stream = typeof(FoodCatalogue).Assembly.GetManifestResourceStream("FitAdvisor.Core.food-catalogue.tsv")
            ?? throw new InvalidDataException("Offline catalogue missing.");
        using var reader = new StreamReader(stream); var list = new List<CatalogueFood>();
        while (reader.ReadLine() is { } line) {
            var p = line.Split('\t');
            double? Number(int i) => p[i] == "" ? null : double.Parse(p[i], CultureInfo.InvariantCulture);
            list.Add(new(int.Parse(p[0], CultureInfo.InvariantCulture), p[1], Number(2), Number(3), Number(4), Number(5), p[6]));
        }
        return list.AsReadOnly();
    }
    public static string Normalize(string text) => string.Concat(text.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).ToLowerInvariant().Replace('ς', 'σ');
    public static IReadOnlyList<CatalogueFood> Search(string query, int limit = 60)
    {
        var terms = Normalize(query.Trim()).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length == 0) return [];
        return Foods.Where(f => terms.All(t => Normalize(f.Name + " " + f.GreekAliases +
            (f.IsRaw ? " ωμό ωμή ωμά" : f.IsCooked ? " μαγειρεμένο μαγειρεμένη μαγειρεμένα" : "")).Contains(t)))
            .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.FdcId).Take(limit).ToArray();
    }
}
