using FitAdvisor.Mobile.Services;
using FitAdvisor.Mobile.ViewModels;
using FitAdvisor.Storage;

namespace FitAdvisor.Mobile.Views;

public sealed class DataPage : ContentPage
{
    private readonly SqlitePersonalDiary diary;
    private readonly IBackupDocuments documents;
    private readonly ProfileViewModel profile;
    private readonly MealsViewModel meals;
    private readonly WeightViewModel weights;
    private readonly WorkoutsPage workouts;
    private readonly Entry password = new() { Placeholder = "Backup passphrase (at least 12 characters)", IsPassword = true };
    private readonly Entry confirmation = new() { Placeholder = "Repeat passphrase for export", IsPassword = true };
    private readonly Label status = new();
    private bool busy;
    private readonly Button export = new() { Text = "Export backup" };
    private readonly Button restore = new() { Text = "Restore" };
    public DataPage(SqlitePersonalDiary diary, IBackupDocuments documents, ProfileViewModel profile,
        MealsViewModel meals, WeightViewModel weights, WorkoutsPage workouts)
    {
        this.diary = diary; this.documents = documents; this.profile = profile;
        this.meals = meals; this.weights = weights; this.workouts = workouts; Title = "Backup / Restore";
        export.Clicked += async (_, _) => await RunAsync(ExportAsync);
        restore.Clicked += async (_, _) => await RunAsync(RestoreAsync);
        var layout = new VerticalStackLayout { Padding = 20, Spacing = 12, Children = {
            new Label { Text = "Your backup", FontSize = 26, FontAttributes = FontAttributes.Bold },
            new Label { Text = "Includes all profiles, daily targets, meals, weights and workouts. The backup is encrypted with your passphrase. Keep the passphrase somewhere safe: it cannot be recovered." },
            new Label { Text = "Choose on-device storage to keep the file on this phone. Android's document provider controls the destination; another provider may store the encrypted file elsewhere. FitAdvisor has no Internet permission." },
            new Label { Text = "Backup passphrase (at least 12 characters)" }, password, new Label { Text = "Repeat passphrase for export" }, confirmation, export,
            new Label { Text = "Restore replaces all current records with the backup. Export your current data first. A valid backup is previewed before you confirm replacement. Existing data stays intact if validation or restore fails." },
            restore, status } };
        var explanation = new VerticalStackLayout { Spacing = 10 };
        for (var i = 0; i < 3; i++)
        {
            var child = (View)layout.Children[0]; layout.Children.RemoveAt(0); explanation.Add(child);
        }
        layout.Children.Insert(0, DesignComponents.Card(explanation));
        Content = new ScrollView { Content = layout };
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return; busy = true; export.IsEnabled = restore.IsEnabled = false; status.Text = "";
        try { await action(); }
        catch (ArgumentException e) { status.Text = e.Message; }
        catch (Exception) { status.Text = "Unable to complete the file operation. Check storage and retry. A failed export may leave an incomplete file; delete it and export again."; }
        finally { password.Text = confirmation.Text = ""; busy = false; export.IsEnabled = restore.IsEnabled = true; }
    }
    private async Task ExportAsync()
    {
        var passphrase = password.Text ?? "";
        if (passphrase != confirmation.Text) throw new ArgumentException("Export passphrases must match.");
        var snapshot = await diary.CreateBackupAsync();
        var file = await Task.Run(() => BackupCodec.Encrypt(snapshot, passphrase));
        status.Text = await documents.SaveAsync(file) ? "Encrypted backup saved. " + snapshot.Summary : "Export cancelled.";
    }
    private async Task RestoreAsync()
    {
        var passphrase = password.Text ?? "";
        var file = await documents.OpenAsync(); if (file == null) { status.Text = "Restore cancelled."; return; }
        var backup = await Task.Run(() => BackupCodec.Decrypt(file, passphrase));
        if (!await DisplayAlertAsync("Replace all local records?", $"Backup exported {backup.ExportedAt.LocalDateTime:g}: {backup.Summary}. Replace the current diary?", "Replace records", "Cancel"))
        { status.Text = "Restore cancelled. Current data is unchanged."; return; }
        await diary.RestoreBackupAsync(backup); profile.Invalidate(); meals.ResetAfterRestore(); weights.ResetAfterRestore(); workouts.ResetDraft();
        status.Text = "Backup restored. Reopen tabs to view restored records.";
    }
}
