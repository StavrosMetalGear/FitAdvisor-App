using FitAdvisor.Mobile.Views;
namespace FitAdvisor.Mobile;
public partial class AppShell : Shell
{
    public AppShell(DashboardPage dashboard, ProfilePage profile, MealsPage meals, WeightPage weight)
    {
        InitializeComponent();
        var tabs = new TabBar { Route = "main" };
        Add(tabs, "Today", "today", dashboard); Add(tabs, "Profile", "profile", profile);
        Add(tabs, "Meals", "meals", meals); Add(tabs, "Weight", "weight", weight); Items.Add(tabs);
    }
    private static void Add(TabBar bar, string title, string route, ContentPage page)
    {
        var tab = new Tab { Title = title, Route = route };
        tab.Items.Add(new ShellContent { Title = title, Route = route + "Page", Content = page });
        bar.Items.Add(tab);
    }
}
