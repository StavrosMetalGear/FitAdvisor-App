using System.Net;
using System.Text.RegularExpressions;
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
var root = args.Length > 0 ? args[0] : "http://127.0.0.1:5187";
using var handler = new HttpClientHandler { CookieContainer = new CookieContainer() };
using var client = new HttpClient(handler) { BaseAddress = new Uri(root) };
async Task<string> Submit(Dictionary<string, string> inputs)
{
    var page = await client.GetStringAsync("/NutritionCalculator");
    var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
    Check(token.Success, "Antiforgery token missing");
    inputs["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
    using var response = await client.PostAsync("/NutritionCalculator", new FormUrlEncodedContent(inputs));
    Check(response.IsSuccessStatusCode, "Calculator request failed: " + response.StatusCode);
    return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
}
Dictionary<string, string> Valid() => new()
{
    ["Input.WeightKg"]="80", ["Input.HeightCm"]="180", ["Input.AgeYears"]="30", ["Input.Sex"]="1",
    ["Input.ActivityMultiplier"]="1.2", ["Input.GoalAdjustmentKcal"]="-300", ["Input.Accounting"]="1",
    ["Input.ActivityKcal"]="400", ["Input.ExerciseCreditFraction"]="0.5"
};
var good = await Submit(Valid());
Check(good.Contains("1836 kcal") && good.Contains("2036 kcal") && good.Contains("20.3%"), "Corrected calculator output incorrect");
var bad = Valid(); bad["Input.Accounting"]="0";
Check((await Submit(bad)).Contains("Exercise credit requires a baseline multiplier excluding exercise."), "Double-credit settings were accepted");
bad = Valid(); bad["Input.HeightCm"]="1.8";
var invalidHeight = await Submit(bad); Check(invalidHeight.Contains("between 100 and 250") && !invalidHeight.Contains("<h2>Estimates</h2>"), "Invalid meter input accepted");
bad = Valid(); bad["Input.Sex"]="";
Check(!(await Submit(bad)).Contains("<h2>Estimates</h2>"), "Missing formula sex accepted");
bad = Valid(); bad["Input.WeightKg"]="NaN";
Check(!(await Submit(bad)).Contains("<h2>Estimates</h2>"), "Non-finite input accepted");
using var missingToken = await client.PostAsync("/NutritionCalculator", new FormUrlEncodedContent(Valid()));
Check(missingToken.StatusCode == HttpStatusCode.BadRequest, "Antiforgery validation disabled");
Console.WriteLine("Web HTTP checks passed: valid calculations, invalid units/settings, missing coefficient, non-finite input and antiforgery. Visual browser checks remain separate.");
