using FitAdvisor.Mobile.ViewModels;
using FitAdvisor.Mobile.Services;
namespace FitAdvisor.Mobile.Views;
public partial class DashboardPage : ContentPage
{
    private readonly DashboardViewModel model;
    private readonly IDispatcherTimer encouragementTimer;
    public DashboardPage(DashboardViewModel model)
    {
        InitializeComponent(); this.model = model; BindingContext = model;
        encouragementTimer = Dispatcher.CreateTimer(); encouragementTimer.Interval = TimeSpan.FromSeconds(30);
        encouragementTimer.Tick += (_, _) => RefreshEncouragement();
    }
    protected override async void OnAppearing() { base.OnAppearing(); RefreshEncouragement(); encouragementTimer.Start(); await model.Guard(model.LoadAsync); }
    protected override void OnDisappearing() { encouragementTimer.Stop(); base.OnDisappearing(); }
    public void Suspend() => encouragementTimer.Stop();
    public void Resume()
    {
        RefreshEncouragement();
        if (Shell.Current?.CurrentPage == this) encouragementTimer.Start();
    }
    public void RefreshEncouragement()
    {
        MessageCard.IsVisible = AppearancePreferences.ShowMessage;
        if (MessageCard.IsVisible) DailyMessage.Text = AppearancePreferences.MessageForToday();
    }
    private async void MealsClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//main/meals/mealsPage");
    private async void WeightClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//main/weight/weightPage");
    private async void ProfileClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//main/profile/profilePage");
}
