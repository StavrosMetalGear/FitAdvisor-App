using System.Text.Json;
using System.Globalization;
using FitAdvisor.Core;
namespace FitAdvisor.Mobile.Services;
public sealed class OpenFoodFactsClient
{
    private static readonly HttpClient defaultHttp = new() {Timeout=TimeSpan.FromSeconds(15)};
    private readonly HttpClient http;
    public OpenFoodFactsClient(HttpClient? http = null) { this.http=http??defaultHttp; }
    private static readonly SemaphoreSlim gate=new(1,1);
    private static DateTimeOffset next;
    private const string Fields="code,product_name,product_name_el,brands,quantity,nutrition_data_per,nutrition_data_prepared_per,nutriments,serving_size,product_quantity_unit,data_quality_errors_tags,data_quality_warnings_tags";
    public async Task<FoodNutritionSnapshot[]> LookupAsync(string query)
    {
        query=query.Trim();if(query.Length<3||query.Length>120)throw new ArgumentException("Enter 3–120 characters or a barcode.");
        if(!await gate.WaitAsync(0))throw new ArgumentException("A product lookup is already running.");
        try {
            if(DateTimeOffset.UtcNow<next)throw new ArgumentException($"Please wait {Math.Ceiling((next-DateTimeOffset.UtcNow).TotalSeconds)} seconds before another online lookup.");
            // Conservative shared gate: at most 8 requests/minute, including product reads.
            next=DateTimeOffset.UtcNow.AddSeconds(8);
            var barcode=query.All(char.IsAsciiDigit);
            var url=barcode?$"https://world.openfoodfacts.org/api/v3.6/product/{Uri.EscapeDataString(query)}.json?fields={Fields}":$"https://world.openfoodfacts.org/cgi/search.pl?search_terms={Uri.EscapeDataString(query)}&search_simple=1&action=process&json=1&page_size=20&fields={Fields}";
            using var request=new HttpRequestMessage(HttpMethod.Get,url);
            request.Headers.UserAgent.ParseAdd("FitAdvisor/0.6.0 (https://github.com/StavrosMetalGear/FitAdvisor-In-Progress-)");
            using var response=await http.SendAsync(request);
            if((int)response.StatusCode is 429 or 503) {next=DateTimeOffset.UtcNow.AddMinutes(1);throw new ArgumentException("Open Food Facts is busy or rate limited. Wait at least one minute; local foods remain available.");}
            if(response.StatusCode==System.Net.HttpStatusCode.NotFound)return [];
            response.EnsureSuccessStatusCode();
            using var document=JsonDocument.Parse(await response.Content.ReadAsStringAsync());var root=document.RootElement;
            var products=barcode?(root.TryGetProperty("product",out var p)?new[]{p}:[]):root.TryGetProperty("products",out var items)?items.EnumerateArray().ToArray():[];
            return products.Select(Parse).ToArray();
        }catch(HttpRequestException){throw new ArgumentException("Offline or product service unavailable. Search local foods or retry later.");}
        catch(TaskCanceledException){throw new ArgumentException("Product lookup timed out. Local foods remain available.");}
        catch(JsonException){throw new ArgumentException("Product service returned unreadable data. Use the package label.");}
        finally{gate.Release();}
    }
    public static FoodNutritionSnapshot Parse(JsonElement p)
    {
        string S(string key)=>p.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
        var n=p.TryGetProperty("nutriments",out var nutrients)?nutrients:default;
        double? N(string key)=>n.ValueKind==JsonValueKind.Object&&n.TryGetProperty(key,out var v)&&double.TryParse(v.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var x)&&double.IsFinite(x)&&x>=0&&x<=10000?x:null;
        // OFF *_100g fields mean per 100 g OR 100 ml. Only accept a basis confirmed by explicit unit evidence.
        var serving=S("serving_size").ToLowerInvariant();var quantity=S("quantity").Trim().ToLowerInvariant();var unit=S("product_quantity_unit").ToLowerInvariant();
        var unitMatch=System.Text.RegularExpressions.Regex.Match(quantity,@"\d\s*(kg|g|ml|l)\s*$");
        if(unit is not ("g" or "ml")) unit=unitMatch.Success?(unitMatch.Groups[1].Value is "ml" or "l"?"ml":"g"):"unknown";
        var servingMatch=System.Text.RegularExpressions.Regex.Match(serving,@"([0-9]+(?:[.,][0-9]+)?)\s*(kg|g|ml|l)\b");
        double? servingSize=null;
        if(servingMatch.Success && double.TryParse(servingMatch.Groups[1].Value.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out var servingValue) && servingValue>0) {
            var servingUnit=servingMatch.Groups[2].Value is "ml" or "l"?"ml":"g";
            if(unit=="unknown")unit=servingUnit;
            if(unit==servingUnit)servingSize=servingValue*(servingMatch.Groups[2].Value is "kg" or "l"?1000:1);
        }
        double? Per100(string nutrient)=>N(nutrient+"_100g")??(servingSize is {} portion?N(nutrient+"_serving")*100/portion:null);
        var reportedKcal=Per100("energy-kcal");var reportedKj=Per100("energy-kj");
        if(reportedKj==null && n.ValueKind==JsonValueKind.Object && n.TryGetProperty("energy_unit",out var energyUnit) && energyUnit.ValueKind==JsonValueKind.String && energyUnit.GetString()=="kJ")reportedKj=Per100("energy");
        var kcal=reportedKcal??(reportedKj is {} kj?kj/4.184:null);
        var warning=unit=="unknown"?"Nutrition basis unclear — create a corrected local food from label":kcal==null?"Energy unavailable — create a local food from label":"Community data: check exact variant and label";
        var protein=Per100("proteins");var carbs=Per100("carbohydrates");var fat=Per100("fat");
        var quality=(p.TryGetProperty("data_quality_errors_tags",out var errors)&&errors.ValueKind==JsonValueKind.Array&&errors.GetArrayLength()>0)
            ||(p.TryGetProperty("data_quality_warnings_tags",out var warnings)&&warnings.ValueKind==JsonValueKind.Array&&warnings.GetArrayLength()>0)
            ||new[]{protein,carbs,fat}.Any(v=>v>100)||kcal>1000
            ||(reportedKcal is {} kc && reportedKj is {} kj2 && Math.Abs(kc-kj2/4.184)>Math.Max(5,kc*.1));
        if(quality)warning="Questionable nutrition flagged by source — check label";
        var name=string.Join(" · ",new[]{S("brands"),S("product_name_el")==""?S("product_name"):S("product_name_el"),S("quantity")}.Where(s=>s!=""));
        return new("Open Food Facts — ODbL 1.0",0,name.Length>200?name[..200]:name,100,kcal??0,protein,carbs,fat) {Kind="off",ProductCode=S("code"),Unit=unit=="ml"?"ml":"g",Basis=$"As sold; {warning}; source basis: {S("nutrition_data_per")}; serving: {serving}", Usable=unit!="unknown"&&kcal!=null&&!quality&&name.Length>0&&S("code").Length>0};
    }
}
