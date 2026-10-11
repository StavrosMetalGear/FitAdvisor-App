using FitAdvisor.Mobile.Views;
namespace FitAdvisor.Mobile;
public partial class AppShell : Shell
{
    private readonly WorkoutsPage workouts;
    public AppShell(HomePage home, DashboardPage dashboard, ProfilePage profile, MealsPage meals, WeightPage weight, WorkoutsPage workouts, DataPage data, SettingsPage settings)
    {
        InitializeComponent();
        this.workouts = workouts;
        FlyoutBehavior = FlyoutBehavior.Flyout;
        this.SetAppThemeColor(FlyoutBackgroundColorProperty, Color.FromArgb("#F3F6F5"), Color.FromArgb("#111B18"));
        ItemTemplate = new DataTemplate(() => {
            var label = new Label { FontSize = 16, Padding = new Thickness(20, 16), MinimumHeightRequest = 56 };
            label.SetBinding(Label.TextProperty, "Title");
            label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#182E27"), Color.FromArgb("#E7F2EC"));
            return label;
        });
        SetTabBarIsVisible(this, false);
        var tabs = new FlyoutItem { Route = "main", FlyoutDisplayOptions = FlyoutDisplayOptions.AsMultipleItems };
        Add(tabs, "Home", "home", home);
        Add(tabs, "Dashboard", "today", dashboard); Add(tabs, "Profile", "profile", profile);
        Add(tabs, "Food Diary", "meals", meals); Add(tabs, "Weight", "weight", weight);
        Add(tabs, "Workouts", "workouts", workouts); Add(tabs, "Backup / Restore", "data", data);
        Add(tabs, "Settings", "settings", settings); Items.Add(tabs);
        Navigating += (_, _) => FlyoutIsPresented = false;
        PropertyChanged += (_, e) => {
            if (e.PropertyName != nameof(FlyoutIsPresented)) return;
            if (FlyoutIsPresented) home.Suspend(); else home.Resume();
#if ANDROID
            if (Platform.CurrentActivity is MainActivity activity) activity.UpdateStatusIcons();
#endif
        };
    }
    public void RequestHealthImport() => workouts.RequestImportSection();
    protected override bool OnBackButtonPressed()
    {
        if (FlyoutIsPresented) { FlyoutIsPresented = false; return true; }
        if (CurrentPage is not HomePage) { _ = GoToAsync("//main/home/homePage"); return true; }
        return base.OnBackButtonPressed();
    }
    private static void Add(FlyoutItem bar, string title, string route, ContentPage page)
    {
        if (page is not HomePage)
            page.ToolbarItems.Add(new ToolbarItem { Text = "Menu", Command = new Command(() => Current.FlyoutIsPresented = true) });
        var tab = new Tab { Title = title, Route = route };
        tab.Items.Add(new ShellContent { Title = title, Route = route + "Page", Content = page });
        bar.Items.Add(tab);
    }
}
