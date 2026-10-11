using FitAdvisor.Core;

namespace FitAdvisor.Mobile.Services;

public static class AppearancePreferences
{
    public static bool Animate
    {
        get => Preferences.Default.Get("design.animation", true);
        set => Preferences.Default.Set("design.animation", value);
    }
    public static bool CanAnimate
    {
        get
        {
            if (!Animate) return false;
#if ANDROID
            var resolver = Android.App.Application.Context.ContentResolver;
            return Android.Provider.Settings.Global.GetFloat(resolver, "animator_duration_scale", 1) > 0 &&
                Android.Provider.Settings.Global.GetFloat(resolver, "transition_animation_scale", 1) > 0;
#else
            return false;
#endif
        }
    }
    public static bool ShowMessage
    {
        get => Preferences.Default.Get("design.message.visible", true);
        set => Preferences.Default.Set("design.message.visible", value);
    }
    public static int Language
    {
        get => Preferences.Default.Get("design.message.language", 0);
        set => Preferences.Default.Set("design.message.language", value == 1 ? 1 : 0);
    }
    public static int Theme
    {
        get => Preferences.Default.Get("design.theme", 0);
        set { Preferences.Default.Set("design.theme", Math.Clamp(value, 0, 2)); ApplyTheme(); }
    }
    public static void ApplyTheme()
    {
        if (Application.Current is { } app)
            app.UserAppTheme = Theme switch { 1 => AppTheme.Light, 2 => AppTheme.Dark, _ => AppTheme.Unspecified };
    }
    public static string MessageForToday()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var snapshot = Preferences.Default.Get("design.message.snapshot", "");
        if (!DailyEncouragement.TryRead(snapshot, today, out var message))
        {
            snapshot = DailyEncouragement.Snapshot(today);
            Preferences.Default.Set("design.message.snapshot", snapshot);
            DailyEncouragement.TryRead(snapshot, today, out message);
        }
        return Language == 1 ? message.Greek : message.English;
    }
}
