using FitAdvisor.Core;
using FitAdvisor.Storage;
using FitAdvisor.Mobile.ViewModels;
using FitAdvisor.Mobile.Views;
using Microsoft.Extensions.Logging;
using FitAdvisor.Mobile.Services;
using Microsoft.Maui.Platform;
namespace FitAdvisor.Mobile;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
#if ANDROID
        Microsoft.Maui.Handlers.SwitchHandler.Mapper.AppendToMapping(nameof(Microsoft.Maui.ISwitch.TrackColor), (handler, view) => {
            var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
            var on = view.TrackColor ?? Color.FromArgb(dark ? "#A7D8BC" : "#145E50");
            var off = Color.FromArgb(dark ? "#738C7D" : "#6F8178");
            handler.PlatformView.TrackTintList = new Android.Content.Res.ColorStateList(
                [ [Android.Resource.Attribute.StateChecked], [] ],
                [ on.ToPlatform().ToArgb(), off.ToPlatform().ToArgb() ]);
        });
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping(nameof(Entry.TextColor), (handler, view) => TintInput(handler.PlatformView, view.TextColor));
        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping(nameof(Picker.TextColor), (handler, view) => TintInput(handler.PlatformView, view.TextColor));
        Microsoft.Maui.Handlers.DatePickerHandler.Mapper.AppendToMapping(nameof(DatePicker.TextColor), (handler, view) => TintInput(handler.PlatformView, view.TextColor));
        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping(nameof(Editor.TextColor), (handler, view) => TintInput(handler.PlatformView, view.TextColor));
        Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("ReadableLabels", (handler, _) => {
            handler.PlatformView.SetSingleLine(false);
            handler.PlatformView.SetMaxLines(4);
        });
#endif
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        builder.Services.AddSingleton(_ => new SqlitePersonalDiary(Path.Combine(FileSystem.AppDataDirectory, "fitadvisor.db")));
        builder.Services.AddSingleton<IPersonalDiary>(s => s.GetRequiredService<SqlitePersonalDiary>());
        builder.Services.AddSingleton<IWorkoutDiary>(s => s.GetRequiredService<SqlitePersonalDiary>());
        builder.Services.AddSingleton<IBackupDocuments, AndroidBackupDocuments>();
        builder.Services.AddSingleton<IHealthSessionReader, AndroidHealthSessionReader>();
        builder.Services.AddSingleton<WorkoutsPage>(); builder.Services.AddSingleton<DataPage>();
        builder.Services.AddSingleton<IUserDialogs, MauiUserDialogs>();
        builder.Services.AddSingleton<DashboardViewModel>(); builder.Services.AddSingleton<ProfileViewModel>();
        builder.Services.AddSingleton<MealsViewModel>(); builder.Services.AddSingleton<WeightViewModel>();
        builder.Services.AddSingleton<DashboardPage>(); builder.Services.AddSingleton<ProfilePage>();
        builder.Services.AddSingleton<MealsPage>(); builder.Services.AddSingleton<WeightPage>();
        builder.Services.AddSingleton<HomePage>(); builder.Services.AddSingleton<SettingsPage>();
        builder.Services.AddSingleton<HabitsPage>();
        builder.Services.AddSingleton<AppShell>();
#if DEBUG
        builder.Logging.AddDebug();
#endif
        return builder.Build();
    }
#if ANDROID
    private static void TintInput(Android.Views.View view, Color? color)
    {
        if (color is not null) view.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(color.ToPlatform());
    }
#endif
}
