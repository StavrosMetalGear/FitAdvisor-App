using FitAdvisor.Mobile.ViewModels;
namespace FitAdvisor.Mobile.Views;
public partial class MealsPage : ContentPage
{
    private readonly MealsViewModel model;
    public MealsViewModel Model => model;
    public MealsPage(MealsViewModel model) { InitializeComponent(); this.model = model; BindingContext = model; }
    protected override async void OnAppearing() { base.OnAppearing(); await model.Guard(model.LoadAsync); }
    private async void EditClicked(object? sender, EventArgs e) => await Scroller.ScrollToAsync(0, 0, true);
}
