using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Content;

namespace FitAdvisor.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private TaskCompletionSource<Android.Net.Uri?>? documentResult;
    private TaskCompletionSource<bool>? healthPermissionResult;
    private TaskCompletionSource<bool> resumed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool IsForeground { get; private set; }
    public Task<bool> WaitUntilResumedAsync() => IsForeground ? Task.FromResult(true) : resumed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    protected override void OnResume() { base.OnResume(); IsForeground = true; resumed.TrySetResult(true); }
    protected override void OnPause()
    {
        IsForeground = false; resumed = new(TaskCreationOptions.RunContinuationsAsynchronously); base.OnPause();
    }
    public Task<Android.Net.Uri?> PickDocumentAsync(bool save)
    {
        if (documentResult != null) throw new InvalidOperationException("A document picker is already open.");
        documentResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = documentResult.Task;
        var intent = new Intent(save ? Intent.ActionCreateDocument : Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable); intent.SetType("application/octet-stream");
        if (save) intent.PutExtra(Intent.ExtraTitle, $"FitAdvisor-{DateTime.Now:yyyyMMdd-HHmmss}.fitbackup");
        else intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraLocalOnly, true);
        try { StartActivityForResult(intent, 901); }
        catch { documentResult = null; throw; }
        return task;
    }
    public Task<bool> RequestExerciseReadAsync()
    {
        if (healthPermissionResult != null) throw new InvalidOperationException("A permission request is already open.");
        healthPermissionResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = healthPermissionResult.Task;
        try { RequestPermissions(new[] { Services.AndroidHealthSessionReader.ReadPermission }, 902); }
        catch { healthPermissionResult = null; throw; }
        return task;
    }
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (requestCode != 901) return;
        var pending = documentResult; documentResult = null;
        pending?.TrySetResult(resultCode == Result.Ok ? data?.Data : null);
    }
    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode != 902) return;
        var pending = healthPermissionResult; healthPermissionResult = null;
        pending?.TrySetResult(grantResults.Length == 1 && grantResults[0] == Permission.Granted);
    }
    protected override void OnDestroy()
    {
        resumed.TrySetResult(false); IsForeground = false;
        documentResult?.TrySetResult(null); documentResult = null;
        healthPermissionResult?.TrySetResult(false); healthPermissionResult = null;
        base.OnDestroy();
    }
}
