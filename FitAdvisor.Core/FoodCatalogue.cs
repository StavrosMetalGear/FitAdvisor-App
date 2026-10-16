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
    public string? LibraryId { get; init; }
    public bool Usable { get; init; } = true;
    public string Kind { get; init; } = "usda";
    public string? ProductCode { get; init; }
    public string Unit { get; init; } = "g";
    public string Basis { get; init; } = "as sold";
    public double Calories => CaloriesPer100G * Grams / 100;
    public double? Protein => ProteinPer100G * Grams / 100;
    public double? Carbs => CarbohydratesPer100G * Grams / 100;
    public double? Fat => FatPer100G * Grams / 100;
    public string Attribution => $"{Source} · {(Kind == "usda" ? $"FDC {FdcId}" : ProductCode)} · {Grams:g} {Unit} · {Basis} · nutrition snapshot";
    public MealRecord Meal(DateOnly date) => new(Guid.NewGuid(), date, Description, Grams, Unit, Calories, Protein, Carbs, Fat, NutritionSnapshot: this);
    public void Validate()
    {
        DiaryValidation.Text(Source, 200, "Food source"); DiaryValidation.Text(Description, 200, "Food description");
        if (Kind is not ("usda" or "off" or "custom") || Unit is not ("g" or "ml")) throw new ArgumentException("Invalid food kind or portion unit.");
        if (Kind == "usda" && FdcId <= 0) throw new ArgumentException("Invalid FDC ID.");
        DiaryValidation.Number(Grams, .001, 100000, "Grams");
        foreach (var n in new[] { (double?)CaloriesPer100G, ProteinPer100G, CarbohydratesPer100G, FatPer100G })
            if (n is { } v) DiaryValidation.Number(v, 0, 10000, "Nutrition per 100 g");
    }
}

public static class FoodCatalogue
{
    public static IReadOnlyList<CatalogueFood> Suggest(string query, int limit = 20)
    {
        var term = Normalize(query.Trim());
        if (term.Length < 4 || term.Length > 40 || term.Contains(' ')) return [];
        int Distance(string word) {
            var previous = Enumerable.Range(0, word.Length + 1).ToArray();
            for (var i = 1; i <= term.Length; i++) {
                var next = new int[word.Length + 1]; next[0] = i;
                for (var j = 1; j <= word.Length; j++) next[j] = Math.Min(Math.Min(next[j-1]+1, previous[j]+1), previous[j-1]+(term[i-1]==word[j-1]?0:1));
                previous = next;
            }
            return previous[word.Length];
        }
        return Foods.Select(f => (Food: f, Score: Normalize(f.Name + " " + f.GreekAliases)
            .Split(new[] {' ', ',', ';', '/'}, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => Math.Abs(w.Length-term.Length)<=1).Select(Distance).DefaultIfEmpty(99).Min()))
            .Where(x => x.Score <= 1).OrderBy(x=>x.Score).ThenBy(x=>x.Food.Name).Take(limit).Select(x=>x.Food).ToArray();
    }
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
