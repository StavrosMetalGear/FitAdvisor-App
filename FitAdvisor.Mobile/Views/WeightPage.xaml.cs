using FitAdvisor.Mobile.ViewModels;
namespace FitAdvisor.Mobile.Views;
public partial class WeightPage : ContentPage
{
    private readonly WeightViewModel model;
    public WeightViewModel Model => model;
    public WeightPage(WeightViewModel model) { InitializeComponent(); this.model = model; BindingContext = model; }
    protected override async void OnAppearing() { base.OnAppearing(); await model.Guard(model.LoadAsync); }
    private async void EditClicked(object? sender, EventArgs e) => await Scroller.ScrollToAsync(0, 0, true);
}
