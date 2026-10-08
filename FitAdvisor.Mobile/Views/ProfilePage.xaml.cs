using FitAdvisor.Mobile.ViewModels;
namespace FitAdvisor.Mobile.Views;
public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel model;
    public ProfilePage(ProfileViewModel model) { InitializeComponent(); this.model = model; BindingContext = model; }
    protected override async void OnAppearing() { base.OnAppearing(); await model.Guard(model.LoadAsync); }
}
