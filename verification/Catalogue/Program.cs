using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using FitAdvisor.Core;
using FitAdvisor.Storage;
using Microsoft.Data.Sqlite;

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new Exception("Invalid input accepted"); }
var foods = FoodCatalogue.Foods;
Check(FoodCatalogue.Search("chicken").Count > 0 && FoodCatalogue.Search("ΚΟΤΌΠΟΥΛΟ").Count > 0, "Chicken bilingual search");
Check(FoodCatalogue.Suggest("chichen").Any(f => f.Name.Contains("Chicken", StringComparison.OrdinalIgnoreCase)), "Chicken typo suggestion");
Check(FoodCatalogue.Suggest("xyzxyzxyz").Count == 0, "Unreasonable typo suggestion");
var macroMeal = new MealRecord(Guid.NewGuid(), new(2026,10,14), "Macro fixture", 1, "serving", 999, 10, 20, 10);
var macro = MacroEnergy.From([macroMeal]);
Check(macro.CanChart && macro.Energy == 210 && Math.Abs(macro.ProteinPercent - 100d*40/210) < 1e-10 && Math.Abs(macro.FatPercent - 100d*90/210) < 1e-10, "4/4/9 energy fractions");
Check(Math.Abs(macro.ProteinPercent + macro.CarbohydratePercent + macro.FatPercent - 100) < 1e-10, "Percentage total");
Check(!MacroEnergy.From([]).CanChart && !MacroEnergy.From([macroMeal with { ProteinG = null }]).CanChart && !MacroEnergy.From([macroMeal with { ProteinG=0, CarbohydratesG=0, FatG=0 }]).CanChart, "Empty/incomplete/zero chart");
Check(MacroEnergy.From([macroMeal with { CarbohydratesG=0, FatG=0 }]).ProteinPercent == 100, "Single macro energy");
MacroEnergy.ValidateTargets(30,40,30); Reject(()=>MacroEnergy.ValidateTargets(30,40,20)); Reject(()=>MacroEnergy.ValidateTargets(double.NaN,40,60));
Check(foods.Count == 7793 && foods.Select(f => f.FdcId).Distinct().Count() == foods.Count, "Catalogue completeness/IDs");
// Independently compare every bundled nutrient and description to the original JSON.
using (var zip = ZipFile.OpenRead(args[0]))
using (var stream = zip.Entries.Single().Open())
using (var json = JsonDocument.Parse(stream)) {
    var source = json.RootElement.GetProperty("SRLegacyFoods").EnumerateArray().ToDictionary(f => f.GetProperty("fdcId").GetInt32());
    Check(source.Count == foods.Count, "Source coverage");
    foreach (var f in foods) {
        var sourceFood = source[f.FdcId];
        Check(sourceFood.GetProperty("description").GetString() == f.Name, "Source name changed");
        double? Get(string number, string unit) {
            foreach (var n in sourceFood.GetProperty("foodNutrients").EnumerateArray())
                if (n.GetProperty("nutrient").GetProperty("number").GetString() == number && n.GetProperty("nutrient").GetProperty("unitName").GetString()!.Equals(unit, StringComparison.OrdinalIgnoreCase))
                    return n.TryGetProperty("amount", out var amount) ? amount.GetDouble() : null;
            return null;
        }
        Check(f.CaloriesPer100G == Get("208", "kcal") && f.ProteinPer100G == Get("203", "g") && f.CarbohydratesPer100G == Get("205", "g") && f.FatPer100G == Get("204", "g"), "Source nutrient mismatch");
        f.Portion(100).Meal(new(2026,10,13)).Validate();
    }
}
Check(FoodCatalogue.Search("μήλο").Select(f => f.FdcId).SequenceEqual(FoodCatalogue.Search("μηλο").Select(f => f.FdcId)), "Accent search");
Check(FoodCatalogue.Search("ΜΗΛΟ").Count > 0 && FoodCatalogue.Search("φακές").Count > 0, "Greek aliases");
Check(FoodCatalogue.Search("rice raw").Count > 0 && FoodCatalogue.Search("rice cooked").Count > 0, "Preparation search");
Check(FoodCatalogue.Search("ρύζι ωμό").Count > 0 && FoodCatalogue.Search("ρύζι μαγειρεμένο").Count > 0, "Greek preparation search");
Check(foods.Where(f => f.Name.Contains("uncooked")).All(f => f.IsRaw && !f.IsCooked), "Uncooked classified as cooked");
Check(FoodCatalogue.Search("not-a-real-food-xyz").Count == 0 && FoodCatalogue.Search(" ").Count == 0, "Empty search");
var raw = foods.First(f => f.Name == "Rice, white, long-grain, regular, raw, unenriched");
var cooked = foods.First(f => f.Name == "Rice, white, long-grain, regular, enriched, cooked");
Check(raw.CaloriesPer100G != cooked.CaloriesPer100G, "Raw/cooked distinction");
var snapshot = raw.Portion(150);
Check(snapshot.Calories == raw.CaloriesPer100G * 1.5 && snapshot.Protein == raw.ProteinPer100G * 1.5, "Portion scaling");
Reject(() => raw.Portion(0)); Reject(() => raw.Portion(double.NaN)); Reject(() => raw.Portion(double.PositiveInfinity));
var synthetic = new CatalogueFood(1,"Missing-nutrient test fixture",null,null,null,null,"");
Reject(() => synthetic.Portion(100)); Check((synthetic with { CaloriesPer100G = 1 }).Portion(100).Protein == null, "Unknown became zero");
var prior = CultureInfo.CurrentCulture; CultureInfo.CurrentCulture = new("el-GR");
Check(double.Parse("150,5", CultureInfo.CurrentCulture) == 150.5, "Greek gram parsing"); CultureInfo.CurrentCulture = prior;
var root = Path.Combine(Path.GetTempPath(), "FitAdvisor-catalogue-" + Guid.NewGuid()); Directory.CreateDirectory(root);
try {
    var path = Path.Combine(root,"diary.db"); var diary = new SqlitePersonalDiary(path); var day = new DateOnly(2026,10,13);
    var manual = new MealRecord(Guid.NewGuid(),day,"Existing manual meal",1,"serving",200);
    await diary.SaveMealAsync(manual);
    // Simulate v0.3.0's schema-v3 database without the catalogue column.
    using (var db = new SqliteConnection($"Data Source={path};Pooling=False")) { db.Open(); using var cmd = db.CreateCommand(); cmd.CommandText = "DROP TABLE gym_sessions; DROP TABLE gym_exercises; DROP TABLE gym_preferences; ALTER TABLE meals DROP COLUMN nutrition_snapshot; PRAGMA user_version=3;"; cmd.ExecuteNonQuery(); }
    Check((await diary.GetMealsAsync(day)).Single().CaloriesKcal == 200, "v3 upgrade lost data");
    var meal = snapshot.Meal(day); await diary.SaveMealAsync(meal);
    var saved = (await new SqlitePersonalDiary(path).GetMealsAsync(day)).Single(m=>m.Id==meal.Id);
    Check(saved.NutritionSnapshot == snapshot, "Snapshot lost on reopen");
    Check((await diary.GetDashboardAsync(day)).ConsumedKcal == 200 + snapshot.Calories, "Dashboard total");
    Reject(() => (saved with { CaloriesKcal = 0 }).Validate());
    var backup = BackupCodec.Decrypt(BackupCodec.Encrypt(await diary.CreateBackupAsync(),"catalogue-test-passphrase"),"catalogue-test-passphrase");
    var restored = new SqlitePersonalDiary(Path.Combine(root,"restore.db")); await restored.RestoreBackupAsync(backup);
    Check((await restored.GetMealsAsync(day)).Single(m=>m.Id==meal.Id).NutritionSnapshot == snapshot, "Encrypted snapshot roundtrip");
    var edited = (new CatalogueFood(snapshot.FdcId,snapshot.Description,snapshot.CaloriesPer100G,snapshot.ProteinPer100G,snapshot.CarbohydratesPer100G,snapshot.FatPer100G,"")).Portion(75).Meal(day) with { Id=meal.Id };
    await restored.SaveMealAsync(edited); Check((await restored.GetMealsAsync(day)).Count==2, "Edit duplicated meal");
    Check((await restored.GetMealsAsync(day)).Single(m=>m.Id==meal.Id).CaloriesKcal==snapshot.Calories/2,"Frozen edit calculation");
    await restored.DeleteMealAsync(meal.Id); Check((await restored.GetMealsAsync(day)).Single().Id==manual.Id,"Delete affected manual meal");
    Console.WriteLine($"Catalogue checks passed: all {foods.Count} source records/nutrients, aliases, raw/cooked, gram scaling, missing/invalid values, schema-v3 upgrade, snapshots/reopen, encrypted restore, edit/delete, totals. Alias coverage={foods.Count(f=>f.GreekAliases.Length>0)}; longest description={foods.Max(f=>f.Name.Length)}.");
} finally {
    var resolved = Path.GetFullPath(root);
    if (!resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(resolved).StartsWith("FitAdvisor-catalogue-")) throw new Exception("Unsafe test cleanup");
    Directory.Delete(resolved,true);
}
