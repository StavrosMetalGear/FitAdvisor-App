using FitAdvisor.Mobile.ViewModels;
using FitAdvisor.Core;
namespace FitAdvisor.Mobile.Views;
public partial class MealsPage : ContentPage
{
    private readonly MealsViewModel model;
    public MealsViewModel Model => model;
    public MealsPage(MealsViewModel model) { InitializeComponent(); this.model = model; BindingContext = model;
        model.Meals.CollectionChanged += (_, _) => DailyMacros.Update(MacroEnergy.From(model.Meals));
        DailyMacros.Update(MacroEnergy.From(model.Meals));
    }
    protected override async void OnAppearing() { base.OnAppearing(); await model.Guard(model.LoadAsync); }
    private async void CatalogueClicked(object? sender, EventArgs e) => await Navigation.PushAsync(new FoodCataloguePage(model.Diary, DateOnly.FromDateTime(model.Date)));
    private async void EditClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: MealRecord { NutritionSnapshot: not null } meal })
            await Navigation.PushAsync(new FoodCataloguePage(model.Diary, meal.Date, meal));
        else await Scroller.ScrollToAsync(0, 0, true);
    }
}
