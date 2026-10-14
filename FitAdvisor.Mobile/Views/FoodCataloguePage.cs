using System.Globalization;
using FitAdvisor.Core;
using FitAdvisor.Mobile.Services;

namespace FitAdvisor.Mobile.Views;

public sealed class FoodCataloguePage : ContentPage
{
    private readonly IPersonalDiary diary;
    private readonly Entry query = new() { Placeholder = "English or Greek food name", AutomationId = "FoodSearch" };
    private readonly Entry grams = new() { Placeholder = "Grams (g)", Keyboard = Keyboard.Numeric, AutomationId = "FoodGrams" };
    private readonly DatePicker date = new();
    private readonly VerticalStackLayout results = new() { Spacing = 10 };
    private readonly Label detail = new();
    private readonly Label preview = new() { AutomationId = "FoodPreview" };
    private readonly Label status = new() { AutomationId = "FoodStatus" };
    private readonly Button save = new() { Text = "Save previewed meal", IsEnabled = false, AutomationId = "SaveCatalogueMeal" };
    private readonly VerticalStackLayout portion = new() { Spacing = 10, IsVisible = false };
    private readonly ScrollView scroller;
    private readonly MealRecord? original;
    private CatalogueFood? selected;
    private FoodNutritionSnapshot? snapshot;
    private bool busy;
    private int searchVersion;
    private Action recalculate = () => { };
    private static string T(string english, string greek) => AppearancePreferences.Language == 1 ? greek : english;
    public FoodCataloguePage(IPersonalDiary diary, DateOnly day, MealRecord? original = null)
    {
        this.diary = diary; this.original = original;
        Title = original == null ? T("Search foods", "Αναζήτηση τροφίμων") : T("Edit food portion", "Επεξεργασία ποσότητας");
        query.Placeholder = T("English or Greek food name", "Όνομα τροφίμου στα ελληνικά ή αγγλικά");
        grams.Placeholder = T("Grams (g)", "Γραμμάρια (g)");
        save.Text = T("Save previewed meal", "Αποθήκευση γεύματος");
        date.Date = day.ToDateTime(TimeOnly.MinValue);
        var search = new Button { Text = T("Search foods", "Αναζήτηση τροφίμων"), AutomationId = "SearchFoods" };
        search.Clicked += async (_, _) => await SearchAsync();
        query.Completed += async (_, _) => await SearchAsync();
        query.TextChanged += async (_, _) => {
            results.Clear(); selected = null; portion.IsVisible = false; Invalidate();
            var version = ++searchVersion;
            await Task.Delay(300);
            if (version == searchVersion && !string.IsNullOrWhiteSpace(query.Text)) await SearchAsync();
        };
        grams.TextChanged += (_, _) => Recalculate();
        var calculate = new Button { Text = T("Preview nutrition", "Προεπισκόπηση θρεπτικών"), AutomationId = "PreviewNutrition" };
        recalculate = Recalculate;
        calculate.Clicked += (_, _) => Recalculate();
        void Recalculate() {
            Invalidate();
            try {
                if (selected == null) throw new ArgumentException("Select a matching food first.");
                if (!double.TryParse(grams.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value))
                    throw new ArgumentException("Enter grams using your phone's decimal separator.");
                snapshot = selected.Portion(value);
                if (original?.NutritionSnapshot is { } frozen) snapshot = snapshot with { Source = frozen.Source };
                snapshot.Meal(DateOnly.FromDateTime(date.Date ?? DateTime.Today)).Validate();
                string Show(double? n) => n is { } v ? $"{v:0.##} g" : T("unavailable (not reported)", "μη διαθέσιμο");
                preview.Text = $"{T("Nutrition preview", "Προεπισκόπηση θρεπτικών")} · {value:g} g\n{snapshot.Calories:0.##} kcal\n{T("Protein", "Πρωτεΐνη")}: {Show(snapshot.Protein)}\n{T("Carbohydrates", "Υδατάνθρακες")}: {Show(snapshot.Carbs)}\n{T("Fat", "Λίπος")}: {Show(snapshot.Fat)}\n{snapshot.Attribution}\n{T("Check food and preparation before saving.", "Ελέγξτε τρόφιμο και παρασκευή πριν την αποθήκευση.")}";
                save.IsEnabled = true;
            } catch (ArgumentException e) { snapshot = null; status.Text = e.Message; }
        }
        save.Clicked += async (_, _) => {
            if (busy || snapshot == null) return;
            busy = true; save.IsEnabled = false;
            try {
                var meal = snapshot.Meal(DateOnly.FromDateTime(date.Date ?? DateTime.Today));
                if (original != null) meal = meal with { Id = original.Id };
                await diary.SaveMealAsync(meal); await Navigation.PopAsync();
            } catch (Exception e) { status.Text = e is ArgumentException ? e.Message : "Unable to save locally. Retry after checking storage."; }
            finally { busy = false; save.IsEnabled = snapshot != null; }
        };
        portion.Add(detail); portion.Add(new Label { Text = T("Local meal date", "Ημερομηνία γεύματος") }); portion.Add(date);
        portion.Add(new Label { Text = T("Weight of this exact preparation (grams)", "Βάρος συγκεκριμένης παρασκευής (γραμμάρια)") }); portion.Add(grams);
        portion.Add(calculate); portion.Add(preview); portion.Add(save);
        var body = new VerticalStackLayout { Padding = 20, Spacing = 16, Children = {
            new Label { Text = T("Offline USDA food search", "Αναζήτηση τροφίμων USDA εκτός σύνδεσης"), FontSize = 24 },
            new Label { Text = T("SR Legacy, April 2018. English descriptions with Greek aliases for common foods. Choose the exact preparation; raw and cooked weights are not interchangeable. Estimates per 100 g. Missing nutrients are unavailable.", "SR Legacy, Απρίλιος 2018. Αγγλικές περιγραφές με ελληνικά ονόματα για κοινά τρόφιμα. Επιλέξτε τη σωστή παρασκευή: ωμό και μαγειρεμένο βάρος διαφέρουν. Εκτιμήσεις ανά 100 g. Τα ελλιπή θρεπτικά είναι μη διαθέσιμα.") },
            query, search, status, portion, results } };
        Content = scroller = new ScrollView { Content = body };
        if (original?.NutritionSnapshot is { } frozen) {
            query.IsVisible = search.IsVisible = false;
            Select(new(frozen.FdcId, frozen.Description, frozen.CaloriesPer100G, frozen.ProteinPer100G, frozen.CarbohydratesPer100G, frozen.FatPer100G, ""));
            grams.Text = frozen.Grams.ToString("R", CultureInfo.CurrentCulture);
            status.Text = "Editing uses the original nutrition snapshot, even if the catalogue changes.";
        }
    }
    private void Invalidate() { snapshot = null; preview.Text = ""; save.IsEnabled = false; status.Text = ""; }
    private async Task SearchAsync()
    {
        if (busy) return;
        var version = ++searchVersion;
        results.Clear(); selected = null; portion.IsVisible = false; Invalidate();
        try {
            var text = query.Text ?? "";
            if (string.IsNullOrWhiteSpace(text)) { status.Text = T("Enter a food name.", "Εισάγετε όνομα τροφίμου."); return; }
            var matches = await Task.Run(() => FoodCatalogue.Search(text, 61));
            var suggested = matches.Count == 0;
            if (suggested) matches = await Task.Run(() => FoodCatalogue.Suggest(text));
            if (query.Text != text || version != searchVersion) return;
            status.Text = matches.Count == 0 ? T("No match. Try an English name or enter known nutrition manually.", "Χωρίς αποτέλεσμα. Δοκιμάστε αγγλικό όνομα ή χειροκίνητη καταχώριση.")
                : suggested ? T("Possible spelling matches. Check the description and explicitly select a food.", "Πιθανές ορθογραφικές αντιστοιχίες. Ελέγξτε την περιγραφή και επιλέξτε τρόφιμο.")
                : matches.Count > 60 ? T("First 60 matches. Add preparation details to narrow search.", "Πρώτα 60 αποτελέσματα. Προσθέστε λεπτομέρειες παρασκευής.") : T($"{matches.Count} matches. Select the food you ate.", $"{matches.Count} αποτελέσματα. Επιλέξτε το τρόφιμο.");
            foreach (var food in matches.Take(60)) {
                var pick = new Button { Text = T("Select this food", "Επιλογή τροφίμου") };
                pick.Clicked += async (_, _) => { Select(food); results.Clear(); await scroller.ScrollToAsync(portion, ScrollToPosition.Start, true); };
                results.Add(DesignComponents.Card(new VerticalStackLayout { Spacing = 8, Children = {
                    new Label { Text = food.Name, FontAttributes = FontAttributes.Bold },
                    new Label { Text = food.Preparation }, new Label { Text = food.CaloriesPer100G is { } kcal ? $"{kcal:0.##} kcal / 100 g" : "kcal unavailable" }, new Label { Text = food.Attribution }, pick } }));
            }
        } catch (Exception) { status.Text = "Unable to read the bundled catalogue."; }
    }
    private void Select(CatalogueFood food)
    {
        ++searchVersion;
        selected = food; Invalidate(); portion.IsVisible = true;
        detail.Text = $"{food.Name}\n{food.Preparation}\n{food.Attribution}\n{food.GreekAliases}";
        if (string.IsNullOrWhiteSpace(grams.Text)) grams.Text = "100";
        recalculate();
    }
}
