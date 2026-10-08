using FitAdvisor.Mobile.ViewModels;
namespace FitAdvisor.Mobile.Views;
public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel model;
    public DashboardPage(DashboardViewModel model) { InitializeComponent(); this.model = model; BindingContext = model; }
    protected override async void OnAppearing() { base.OnAppearing(); await model.Guard(model.LoadAsync); }
    private async void MealsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//main/meals/mealsPage");
    private async void WeightClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//main/weight/weightPage");
    private async void ProfileClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//main/profile/profilePage");
}
