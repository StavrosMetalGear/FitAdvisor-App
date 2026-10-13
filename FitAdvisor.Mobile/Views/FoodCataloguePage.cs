using System.Globalization;
using FitAdvisor.Core;

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
    public FoodCataloguePage(IPersonalDiary diary, DateOnly day, MealRecord? original = null)
    {
        this.diary = diary; this.original = original;
        Title = original == null ? "Offline food catalogue" : "Edit food portion";
        date.Date = day.ToDateTime(TimeOnly.MinValue);
        var search = new Button { Text = "Search offline", AutomationId = "SearchFoods" };
        search.Clicked += async (_, _) => await SearchAsync();
        query.Completed += async (_, _) => await SearchAsync();
        query.TextChanged += (_, _) => { results.Clear(); selected = null; portion.IsVisible = false; Invalidate(); };
        grams.TextChanged += (_, _) => Invalidate();
        var calculate = new Button { Text = "Preview nutrition", AutomationId = "PreviewNutrition" };
        calculate.Clicked += (_, _) => {
            Invalidate();
            try {
                if (selected == null) throw new ArgumentException("Select a matching food first.");
                if (!double.TryParse(grams.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value))
                    throw new ArgumentException("Enter grams using your phone's decimal separator.");
                snapshot = selected.Portion(value);
                if (original?.NutritionSnapshot is { } frozen) snapshot = snapshot with { Source = frozen.Source };
                snapshot.Meal(DateOnly.FromDateTime(date.Date ?? DateTime.Today)).Validate();
                string Show(double? n) => n is { } v ? $"{v:0.##} g" : "unknown (not reported)";
                preview.Text = $"Estimated nutrition for {value:g} g\n{snapshot.Calories:0.##} kcal\nProtein: {Show(snapshot.Protein)}\nCarbohydrates: {Show(snapshot.Carbs)}\nFat: {Show(snapshot.Fat)}\n{snapshot.Attribution}\nCheck the exact food and preparation before saving.";
                save.IsEnabled = true;
            } catch (ArgumentException e) { status.Text = e.Message; }
        };
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
        portion.Add(detail); portion.Add(new Label { Text = "Local meal date" }); portion.Add(date);
        portion.Add(new Label { Text = "Weight of this exact preparation (grams)" }); portion.Add(grams);
        portion.Add(calculate); portion.Add(preview); portion.Add(save);
        var body = new VerticalStackLayout { Padding = 20, Spacing = 16, Children = {
            new Label { Text = "Offline USDA food search", FontSize = 24 },
            new Label { Text = "SR Legacy, April 2018. English source descriptions with curated Greek search aliases for common foods. This catalogue does not cover every food or Greek dish. Choose the exact preparation; raw and cooked weights are not interchangeable. Values are estimates per 100 g of edible food. Missing macros remain unknown." },
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
        busy = true; results.Clear(); selected = null; portion.IsVisible = false; Invalidate();
        try {
            var text = query.Text ?? "";
            if (string.IsNullOrWhiteSpace(text)) { status.Text = "Enter a food name."; return; }
            var matches = await Task.Run(() => FoodCatalogue.Search(text, 61));
            if (query.Text != text) return;
            status.Text = matches.Count == 0 ? "No match. Try an English name or log known nutrition manually."
                : matches.Count > 60 ? "Showing the first 60 matches. Add preparation or food details to narrow the search." : $"{matches.Count} matches. Select the food you actually ate.";
            foreach (var food in matches.Take(60)) {
                var pick = new Button { Text = "Select this food" };
                pick.Clicked += async (_, _) => { Select(food); results.Clear(); await scroller.ScrollToAsync(portion, ScrollToPosition.Start, true); };
                results.Add(DesignComponents.Card(new VerticalStackLayout { Spacing = 8, Children = {
                    new Label { Text = food.Name, FontAttributes = FontAttributes.Bold },
                    new Label { Text = food.Preparation }, new Label { Text = food.Attribution }, pick } }));
            }
        } catch (Exception) { status.Text = "Unable to read the bundled catalogue."; }
        finally { busy = false; }
    }
    private void Select(CatalogueFood food)
    {
        selected = food; Invalidate(); portion.IsVisible = true;
        detail.Text = $"{food.Name}\n{food.Preparation}\n{food.Attribution}\n{food.GreekAliases}";
    }
}
