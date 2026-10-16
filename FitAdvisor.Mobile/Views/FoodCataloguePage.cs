using System.Globalization;
using FitAdvisor.Core;
using FitAdvisor.Storage;
using FitAdvisor.Mobile.Services;
namespace FitAdvisor.Mobile.Views;
public sealed class FoodCataloguePage : ContentPage
{
    private readonly IPersonalDiary diary;
    private readonly Entry query=new() {AutomationId="FoodSearch"};
    private readonly Entry amount=new() {Keyboard=Keyboard.Numeric,AutomationId="FoodGrams"};
    private readonly VerticalStackLayout onlineArea=new() {Spacing=10};
    private readonly VerticalStackLayout searchArea=new() {Spacing=10};
    private readonly VerticalStackLayout results=new() {Spacing=8};
    private readonly VerticalStackLayout portion=new() {Spacing=10,IsVisible=false};
    private readonly Label detail=new(), preview=new() {AutomationId="FoodPreview"},status=new() {AutomationId="FoodStatus"};
    private readonly Button save=new() {AutomationId="SaveCatalogueMeal",IsEnabled=false};
    private readonly DatePicker date=new();
    private FoodNutritionSnapshot? selected, ready;
    private SavedFood[] saved=[];
    private MealRecord[] recent=[];
    private readonly MealRecord? original;
    private readonly Guid mealId;
    private bool busy;
    private int version;
    private static string T(string en,string el)=>AppearancePreferences.Language==1?el:en;
    public FoodCataloguePage(IPersonalDiary diary,DateOnly day,MealRecord? original=null)
    {
        this.diary=diary;this.original=original;mealId=original?.Id??Guid.NewGuid();Title=T("Find food","Αναζήτηση τροφίμου");date.Date=day.ToDateTime(TimeOnly.MinValue);
        query.Placeholder=T("Food, brand or barcode · Greek / English","Τρόφιμο, μάρκα ή barcode · Ελληνικά / Αγγλικά");
        SemanticProperties.SetDescription(query,query.Placeholder);
        var online=new Button {Text=T("Find branded products online","Αναζήτηση επώνυμων προϊόντων online"),AutomationId="SearchBrandedFoods"};
        online.Clicked+=async(_,_)=> {if(busy)return;busy=true;online.IsEnabled=false;var search=query.Text??"";var current=++version;status.Text=T("Looking up Open Food Facts…","Αναζήτηση Open Food Facts…");try {
            var foods=await new OpenFoodFactsClient().LookupAsync(search);if(current!=version)return;
            results.Clear();selected=null;portion.IsVisible=false;Invalidate();
            status.Text=foods.Length==0?T("No products found. Not every supermarket product is available. Check the barcode or add the package label.","Δεν βρέθηκαν προϊόντα. Δεν είναι διαθέσιμα όλα τα προϊόντα. Δοκίμασε barcode ή πρόσθεσε την ετικέτα."):T("Check brand, variant, barcode and nutrition basis. Select explicitly.","Έλεγξε μάρκα, παραλλαγή, barcode και βάση θρεπτικών. Επίλεξε ρητά.");
            foreach(var food in foods)AddResult(food);
        }catch(Exception e){if(current==version)status.Text=e is ArgumentException?e.Message:T("Product lookup unavailable. Local foods remain usable.","Η online αναζήτηση δεν είναι διαθέσιμη. Τα τοπικά τρόφιμα λειτουργούν.");}finally{busy=false;online.IsEnabled=true;}};
        query.TextChanged+=(_,_)=>{++version;selected=null;searchArea.IsVisible=true;onlineArea.IsVisible=true;portion.IsVisible=false;Invalidate();LocalSearch();};
        query.Completed+=(_,_)=>{query.Unfocus();LocalSearch();};
        amount.TextChanged+=(_,_)=>Recalculate();
        save.Text=T("Save previewed meal","Αποθήκευση γεύματος");save.Clicked+=async(_,_)=>{
            if(busy||ready==null)return;busy=true;save.IsEnabled=false;
            try {var meal=ready.Meal(DateOnly.FromDateTime(date.Date??DateTime.Today));meal=meal with {Id=mealId};
                if(diary is SqlitePersonalDiary local) {var id=Key(ready);var prior=saved.FirstOrDefault(f=>f.Id==id);await local.SaveFoodAsync(new(id,ready with {Grams=100},prior?.Favorite??false,DateTimeOffset.UtcNow));}
                await diary.SaveMealAsync(meal);await Navigation.PopAsync();
            }catch(Exception e){status.Text=e is ArgumentException?e.Message:T("Could not save locally. Retry.","Αποτυχία αποθήκευσης. Δοκίμασε ξανά.");}finally{busy=false;save.IsEnabled=ready!=null;}
        };
        var favorite=new Button {Text=T("Favorite / unfavorite this food","Αγαπημένο / αφαίρεση αγαπημένου")};favorite.Clicked+=async(_,_)=>{if(selected==null||diary is not SqlitePersonalDiary local)return;var id=Key(selected);var prior=saved.FirstOrDefault(f=>f.Id==id);await local.SaveFoodAsync(new(id,selected with {Grams=100},!(prior?.Favorite??false),prior?.LastUsed));saved=await local.GetSavedFoodsAsync();status.Text=T("Favorites updated locally.","Ενημερώθηκαν τα αγαπημένα.");};
        var correct=new Button {Text=T("Create local version from package label","Τοπική έκδοση από την ετικέτα")};correct.Clicked+=async(_,_)=>await Navigation.PushAsync(new CustomFoodPage(diary,selected));
        var another=new Button {Text=T("Choose another food","Επιλογή άλλου τροφίμου"),IsVisible=original==null};another.Clicked+=(_,_)=>{selected=null;searchArea.IsVisible=true;onlineArea.IsVisible=true;portion.IsVisible=false;Invalidate();LocalSearch();};portion.Add(another);
        portion.Add(detail);portion.Add(date);portion.Add(amount);portion.Add(preview);portion.Add(save);portion.Add(favorite);portion.Add(correct);
        var custom=new Button {Text=T("Add custom food","Προσθήκη δικού μου τροφίμου")};custom.Clicked+=async(_,_)=>await Navigation.PushAsync(new CustomFoodPage(diary));
        var privacyButton=new Button {Text=T("Sources & online privacy","Πηγές & απόρρητο online")};
        privacyButton.Clicked+=async(_,_)=>await DisplayAlertAsync(T("Food sources & privacy","Πηγές τροφίμων & απόρρητο"),T("INTERNET enables optional Open Food Facts lookup. Only the search/barcode and app identifier leave this device; the service sees your IP. No diary, health records or backups are sent. Open Food Facts is community data, ODbL 1.0, individual contents DbCL 1.0 (world.openfoodfacts.org/terms-of-use). No product photos downloaded. The bundled USDA SR Legacy (April 2018) catalogue remains separate; selected OFF products are cached separately with their source. Custom label corrections are local. Check the exact variant and label; not every supermarket product is available.","Το INTERNET επιτρέπει προαιρετική αναζήτηση Open Food Facts. Στέλνονται μόνο όρος/barcode και ταυτότητα εφαρμογής· η υπηρεσία βλέπει την IP. Δεν στέλνονται γεύματα, υγεία ή αντίγραφα. Open Food Facts: κοινοτικά δεδομένα, ODbL 1.0, περιεχόμενα DbCL 1.0 (world.openfoodfacts.org/terms-of-use). Δεν κατεβαίνουν φωτογραφίες προϊόντων. USDA SR Legacy Απριλίου 2018 και αποθηκευμένα OFF παραμένουν διακριτά. Διορθώσεις ετικέτας τοπικά. Έλεγξε ακριβή παραλλαγή και ετικέτα· δεν υπάρχουν όλα τα προϊόντα."),"OK");
        searchArea.Add(query);onlineArea.Add(online);onlineArea.Add(new Label {Text=T("Online sends only your search/barcode to Open Food Facts. Local suggestions work offline.","Online: μόνο όρος/barcode στο Open Food Facts. Τοπικές προτάσεις offline.")});
        var body=new VerticalStackLayout {Padding=16,Spacing=12,Children={searchArea,status,portion,results,onlineArea,custom,privacyButton}};
        Content=new ScrollView {Content=body};
        if(original?.NutritionSnapshot is {} frozen){query.IsVisible=online.IsVisible=false;Select(frozen);amount.Text=frozen.Grams.ToString("R",CultureInfo.CurrentCulture);}
    }
    protected override async void OnAppearing() {base.OnAppearing();if(diary is SqlitePersonalDiary local)saved=await local.GetSavedFoodsAsync();recent=(await diary.GetMealsAsync(DateOnly.FromDateTime(DateTime.Today))).ToArray();if(original==null){selected=null;searchArea.IsVisible=true;onlineArea.IsVisible=true;portion.IsVisible=false;Invalidate();LocalSearch();}}
    private static string Key(FoodNutritionSnapshot f)=>f.Kind=="usda"?$"usda:{f.FdcId}":f.Kind=="off"?$"off:{f.ProductCode}":f.LibraryId??"custom:"+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(f.Description+f.CaloriesPer100G.ToString("R",CultureInfo.InvariantCulture))));
    private void LocalSearch()
    {
        results.Clear();var text=query.Text??"";
        var local=saved.Where(f=>f.Matches(text)||SearchSuggestions.Near(f.Nutrition.Description,text)).OrderByDescending(f=>f.Favorite).ThenByDescending(f=>f.LastUsed).Take(12).ToArray();
        foreach(var f in local)AddResult(f.Nutrition,f.Favorite?"★ ":"");
        if(string.IsNullOrWhiteSpace(text)) {foreach(var f in recent.Where(m=>m.NutritionSnapshot!=null).Select(m=>m.NutritionSnapshot!).DistinctBy(Key).Where(f=>!local.Any(s=>s.Id==Key(f))).Take(6))AddResult(f);status.Text=T("Favorites & recent · type to search locally.","Αγαπημένα & πρόσφατα · τοπική αναζήτηση.");return;}
        var matches=FoodCatalogue.Search(text,16);var typo=matches.Count==0;if(typo)matches=FoodCatalogue.Suggest(text,8);
        foreach(var f in matches)AddResult(new(CatalogueFood.Source,f.FdcId,f.Name,100,f.CaloriesPer100G??0,f.ProteinPer100G,f.CarbohydratesPer100G,f.FatPer100G) {Usable=f.CaloriesPer100G!=null,Basis=f.Preparation});
        status.Text=matches.Count+local.Length==0?T("No local matches. Try online branded lookup or add a custom food.","Χωρίς τοπικό αποτέλεσμα. Δοκίμασε online ή πρόσθεσε τρόφιμο."):typo&&matches.Count>0?T("Possible spelling matches. Check and select an exact food.","Πιθανές ορθογραφικές αντιστοιχίες. Έλεγξε και επίλεξε τρόφιμο."):T("Select an exact food.","Επίλεξε ακριβές τρόφιμο.");
    }
    private void AddResult(FoodNutritionSnapshot food,string prefix="")
    {
        var label=food.Kind switch {"off"=>"Branded / Επώνυμο","custom"=>"User-created / Δικό μου",_=>"Generic USDA / Γενικό USDA"};
        var pick=new Button {Text=$"{prefix}{food.Description}\n{label} · {food.ProductCode}\n{(food.Usable?$"{food.CaloriesPer100G:0.##} kcal / 100 {food.Unit}":"Check label / Έλεγχος ετικέτας")}",LineBreakMode=LineBreakMode.WordWrap};
        SemanticProperties.SetDescription(pick,pick.Text);pick.Clicked+=(_,_)=>{query.Unfocus();Select(food);results.Clear();};results.Add(pick);
    }
    private void Select(FoodNutritionSnapshot food) {++version;selected=food;searchArea.IsVisible=false;onlineArea.IsVisible=false;portion.IsVisible=true;detail.Text=$"{food.Description}\n{food.Attribution}";amount.Placeholder=$"Portion / Ποσότητα ({food.Unit})";amount.Text="100";Recalculate();}
    private void Invalidate(){ready=null;save.IsEnabled=false;preview.Text="";status.Text="";}
    private void Recalculate()
    {
        Invalidate();if(selected==null)return;
        try {if(!selected.Usable)throw new ArgumentException("Nutrition missing, questionable or basis unclear. Create a local corrected version from the package label.");
            if(!double.TryParse(amount.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out var value))throw new ArgumentException(T("Enter a portion.","Γράψε ποσότητα."));
            var next=selected with {Grams=value};next.Meal(DateOnly.FromDateTime(date.Date??DateTime.Today)).Validate();ready=next;detail.Text=$"{next.Description}\n{next.Attribution}";
            string N(double? x)=>x is {} v?$"{v:0.##} g":T("unavailable","μη διαθέσιμο");
            preview.Text=$"{next.Grams:g} {next.Unit} · {next.Calories:0.##} kcal\n{T("Protein","Πρωτεΐνη")}: {N(next.Protein)} · {T("Carbs","Υδατάνθρακες")}: {N(next.Carbs)} · {T("Fat","Λίπος")}: {N(next.Fat)}";save.IsEnabled=true;
        }catch(ArgumentException e){status.Text=e.Message;}
    }
}
