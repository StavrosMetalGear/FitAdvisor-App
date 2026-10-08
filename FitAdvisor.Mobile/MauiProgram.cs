using FitAdvisor.Core;
using FitAdvisor.Storage;
using FitAdvisor.Mobile.ViewModels;
using FitAdvisor.Mobile.Views;
using Microsoft.Extensions.Logging;
namespace FitAdvisor.Mobile;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddSingleton<IPersonalDiary>(_ => new SqlitePersonalDiary(Path.Combine(FileSystem.AppDataDirectory, "fitadvisor.db")));
        builder.Services.AddSingleton<IUserDialogs, MauiUserDialogs>();
        builder.Services.AddSingleton<DashboardViewModel>(); builder.Services.AddSingleton<ProfileViewModel>();
        builder.Services.AddSingleton<MealsViewModel>(); builder.Services.AddSingleton<WeightViewModel>();
        builder.Services.AddSingleton<DashboardPage>(); builder.Services.AddSingleton<ProfilePage>();
        builder.Services.AddSingleton<MealsPage>(); builder.Services.AddSingleton<WeightPage>();
        builder.Services.AddSingleton<AppShell>();
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
}
