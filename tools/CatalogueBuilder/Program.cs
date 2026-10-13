using System.IO.Compression;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

var archive = args[0];
using var zip = ZipFile.OpenRead(archive);
using var stream = zip.Entries.Single(e => e.Name.EndsWith(".json")).Open();
using var doc = JsonDocument.Parse(stream);
var foods = doc.RootElement.GetProperty("SRLegacyFoods");
// Search aids only: these labels never supply nutrition or translate preparation.
var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
    ["Apples"]="μήλο μήλα", ["Bananas"]="μπανάνα μπανάνες", ["Oranges"]="πορτοκάλι πορτοκάλια",
    ["Strawberries"]="φράουλα φράουλες", ["Grapes"]="σταφύλι σταφύλια", ["Pears"]="αχλάδι αχλάδια",
    ["Peaches"]="ροδάκινο ροδάκινα", ["Watermelon"]="καρπούζι", ["Kiwifruit"]="ακτινίδιο",
    ["Lemons"]="λεμόνι", ["Avocados"]="αβοκάντο", ["Tomatoes"]="ντομάτα ντομάτες",
    ["Cucumber"]="αγγούρι", ["Carrots"]="καρότο καρότα", ["Potatoes"]="πατάτα πατάτες",
    ["Onions"]="κρεμμύδι", ["Broccoli"]="μπρόκολο", ["Spinach"]="σπανάκι", ["Lettuce"]="μαρούλι",
    ["Egg"]="αυγό αυγά", ["Milk"]="γάλα", ["Yogurt"]="γιαούρτι", ["Cheese"]="τυρί",
    ["Butter"]="βούτυρο", ["Chicken"]="κοτόπουλο", ["Turkey"]="γαλοπούλα", ["Beef"]="μοσχάρι βοδινό",
    ["Pork"]="χοιρινό", ["Lamb"]="αρνί", ["Fish, salmon"]="σολομός", ["Fish, tuna"]="τόνος",
    ["Rice"]="ρύζι", ["Pasta"]="ζυμαρικά μακαρόνια", ["Bread"]="ψωμί", ["Oats"]="βρώμη",
    ["Lentils"]="φακές", ["Chickpeas"]="ρεβίθια", ["Beans"]="φασόλια", ["Nuts, almonds"]="αμύγδαλα",
    ["Nuts, walnuts"]="καρύδια", ["Peanuts"]="φιστίκια", ["Oil, olive"]="ελαιόλαδο",
    ["Honey"]="μέλι", ["Sugars"]="ζάχαρη", ["Coffee"]="καφές", ["Tea"]="τσάι"
};
var output = new StringBuilder(); int missingEnergy = 0, aliasFoods = 0;
var ids = new HashSet<int>();
foreach (var food in foods.EnumerateArray().OrderBy(f => f.GetProperty("fdcId").GetInt32()))
{
    int id = food.GetProperty("fdcId").GetInt32();
    if (!ids.Add(id)) throw new InvalidDataException("Duplicate FDC ID");
    string name = food.GetProperty("description").GetString()!;
    string Nutrient(string number, string unit) {
        var matches = food.GetProperty("foodNutrients").EnumerateArray().Where(n =>
            n.GetProperty("nutrient").GetProperty("number").GetString() == number &&
            string.Equals(n.GetProperty("nutrient").GetProperty("unitName").GetString(), unit, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length > 1) throw new InvalidDataException("Ambiguous nutrient");
        return matches.Length == 0 || !matches[0].TryGetProperty("amount", out var amount) ? "" : amount.GetDouble().ToString("R", CultureInfo.InvariantCulture);
    }
    var kcal = Nutrient("208", "kcal"); if (kcal == "") missingEnergy++;
    var greek = string.Join(" ", aliases.Where(a => name.Equals(a.Key, StringComparison.OrdinalIgnoreCase) || name.StartsWith(a.Key + ",", StringComparison.OrdinalIgnoreCase)).Select(a => a.Value));
    if (greek.Length > 0) aliasFoods++;
    string Clean(string s) => s.Replace('\t',' ').Replace('\r',' ').Replace('\n',' ');
    output.AppendLine(string.Join('\t', id.ToString(), Clean(name), kcal, Nutrient("203","g"), Nutrient("205","g"), Nutrient("204","g"), greek));
}
Directory.CreateDirectory(Path.GetDirectoryName(args[1])!);
File.WriteAllText(args[1], output.ToString(), new UTF8Encoding(false));
Console.WriteLine($"Foods={ids.Count}; alias-covered foods={aliasFoods}; curated alias groups={aliases.Count}; missing energy={missingEnergy}; archive SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive)))}");
