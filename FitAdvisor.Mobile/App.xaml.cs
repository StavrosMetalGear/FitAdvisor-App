using Microsoft.Extensions.DependencyInjection;
namespace FitAdvisor.Mobile;
public partial class App : Application
{
    private readonly IServiceProvider services;
    public App(IServiceProvider services) { InitializeComponent(); this.services = services; Services.AppearancePreferences.ApplyTheme(); }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        Services.AppearancePreferences.ApplyTheme();
        var window = new Window(services.GetRequiredService<AppShell>());
        window.Activated += (_, _) => {
            services.GetRequiredService<Views.HomePage>().Resume();
            services.GetRequiredService<Views.DashboardPage>().Resume();
        };
        window.Deactivated += (_, _) => {
            services.GetRequiredService<Views.HomePage>().Suspend();
            services.GetRequiredService<Views.DashboardPage>().Suspend();
        };
        return window;
    }
}
