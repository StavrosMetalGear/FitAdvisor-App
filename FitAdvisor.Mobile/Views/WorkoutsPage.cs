using System.Collections.ObjectModel;
using System.Globalization;
using FitAdvisor.Core;
using FitAdvisor.Mobile.Services;

namespace FitAdvisor.Mobile.Views;

public sealed class WorkoutsPage : ContentPage
{
    private readonly IWorkoutDiary diary;
    private readonly IHealthSessionReader health;
    private readonly DatePicker date = new() { Date = DateTime.Today };
    private readonly Picker kind = new() { Title = "Workout type", ItemsSource = new[] { "Run", "Gym" }, SelectedIndex = 0 };
    private readonly Entry title = new() { Placeholder = "Workout title" };
    private readonly Entry minutes = new() { Placeholder = "Duration in minutes", Keyboard = Keyboard.Numeric };
    private readonly Entry distance = new() { Placeholder = "Run distance (km)", Keyboard = Keyboard.Numeric };
    private readonly Editor notes = new() { Placeholder = "Notes", AutoSize = EditorAutoSizeOption.TextChanges };
    private readonly Entry exercise = new() { Placeholder = "Exercise name" };
    private readonly Entry reps = new() { Placeholder = "Reps", Keyboard = Keyboard.Numeric };
    private readonly Entry load = new() { Placeholder = "External load (kg)", Keyboard = Keyboard.Numeric };
    private readonly CheckBox warmup = new();
    private readonly VerticalStackLayout setsView = new() { Spacing = 6 };
    private readonly VerticalStackLayout gymForm = new() { Spacing = 8 };
    private readonly ObservableCollection<WorkoutRecord> records = new();
    private readonly List<StrengthSet> sets = new();
    private readonly DatePicker importFrom = new() { Date = DateTime.Today.AddDays(-6) };
    private readonly DatePicker importThrough = new() { Date = DateTime.Today };
    private readonly Label status = new();
    private readonly Button save = new() { Text = "Add workout" };
    private readonly VerticalStackLayout form;
    private ScrollView scroller = null!;
    private Label importHeading = null!;
    private bool focusImport;
    public void RequestImportSection() => focusImport = true;
    private WorkoutRecord? editing;
    private Guid? editingSet;
    private bool busy;
    public WorkoutsPage(IWorkoutDiary diary, IHealthSessionReader health)
    {
        this.diary = diary; this.health = health; Title = "Workouts";
        var addSet = new Button { Text = "Add strength set" };
        addSet.Clicked += (_, _) => {
            try
            {
                if (!int.TryParse(reps.Text, out var count)) throw new ArgumentException("Enter whole-number reps.");
                var set = new StrengthSet(editingSet ?? Guid.NewGuid(), exercise.Text?.Trim() ?? "", count, Number(load.Text, "external load"), warmup.IsChecked);
                set.Validate();
                var index = sets.FindIndex(s => s.Id == set.Id);
                if (index >= 0) sets[index] = set; else sets.Add(set);
                editingSet = null; RenderSets(); exercise.Text = reps.Text = load.Text = ""; warmup.IsChecked = false;
            }
            catch (ArgumentException e) { status.Text = e.Message; }
        };
        gymForm.Add(new Label { Text = "Strength sets (external load only)" });
        gymForm.Add(new Label { Text = "Exercise name" }); gymForm.Add(exercise); gymForm.Add(new Label { Text = "Repetitions" }); gymForm.Add(reps); gymForm.Add(new Label { Text = "External load (kg)" }); gymForm.Add(load);
        var warmupRow = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 8 };
        warmupRow.Add(warmup); warmupRow.Add(new Label { Text = "Warm-up set", VerticalOptions = LayoutOptions.Center }, 1);
        SemanticProperties.SetDescription(warmup, "Warm-up set");
        gymForm.Add(warmupRow);
        addSet.Text = "Save set"; gymForm.Add(addSet); gymForm.Add(setsView);
        kind.SelectedIndexChanged += (_, _) => { distance.IsVisible = kind.SelectedIndex == 0; gymForm.IsVisible = kind.SelectedIndex == 1; };
        gymForm.IsVisible = false;
        save.Clicked += async (_, _) => await RunAsync(SaveAsync);
        var cancel = new Button { Text = "Clear form" }; cancel.Clicked += (_, _) => ResetDraft();
        var list = new CollectionView { ItemsSource = records, HeightRequest = 360, SelectionMode = SelectionMode.None, IsVisible = false };
        var empty = DesignComponents.Caption("No workouts saved yet.");
        records.CollectionChanged += (_, _) => { list.IsVisible = records.Count > 0; empty.IsVisible = records.Count == 0; };
        list.ItemTemplate = new DataTemplate(() =>
        {
            var summary = new Label(); summary.SetBinding(Label.TextProperty, nameof(WorkoutRecord.Summary));
            var detail = new Label(); detail.SetBinding(Label.TextProperty, nameof(WorkoutRecord.Details));
            var edit = new Button { Text = "Edit" }; edit.SetBinding(IsVisibleProperty, nameof(WorkoutRecord.IsManual));
            edit.Clicked += (_, _) => { if (edit.BindingContext is WorkoutRecord record) BeginEdit(record); };
            var delete = new Button { Text = "Delete entry" };
            delete.Clicked += async (_, _) => { if (delete.BindingContext is WorkoutRecord record) await RunAsync(() => DeleteAsync(record)); };
            return DesignComponents.Card(new VerticalStackLayout { Spacing = 8, Children = { summary, detail, new VerticalStackLayout { Spacing = 8, Children = { edit, delete } } } });
        });
        var import = new Button { Text = "Import" };
        import.Clicked += async (_, _) => await RunAsync(ImportAsync);
        var importStatus = new Label();
        importStatus.SetBinding(Label.TextProperty, new Binding(nameof(Label.Text), source: status));
        var refresh = new Button { Text = "Refresh" }; refresh.Clicked += async (_, _) => await RunAsync(LoadAsync);
        importHeading = new Label { Text = "Health Connect import", FontSize = 24 };
        form = new VerticalStackLayout { Padding = 20, Spacing = 16, Children = {
            new Label { Text = "Manual workout", FontSize = 24 }, new Label { Text = "Local workout date" }, date, new Label { Text = "Workout type" }, kind, new Label { Text = "Title" }, title, new Label { Text = "Duration (minutes)" }, minutes, distance, new Label { Text = "Notes (optional)" }, notes, gymForm, save, cancel,
            new Label { Text = "Workouts do not change calorie targets or meal allowance." }, status, refresh, empty, list,
            importHeading,
            new Label { Text = "Android 14+. Reads exercise sessions only after you tap Import and grant access. Select dates within the last 30 days. No routes, GPS, heart rate, distance records, calories, steps or background sync. Imported entries are read-only; repeat imports update matching source IDs without duplicating them. Deleting a local imported copy lets a later import bring it back." },
            importStatus, new Label { Text = "From" }, importFrom, new Label { Text = "Through" }, importThrough, import } };
        // Group the existing controls without changing their bindings or event handlers.
        var manual = new VerticalStackLayout { Spacing = 10 };
        while (form.Children.Count > 0 && form.Children[0] != save)
        {
            var child = (View)form.Children[0]; form.Children.RemoveAt(0); manual.Add(child);
        }
        form.Children.Insert(0, DesignComponents.Card(manual));
        Content = scroller = new ScrollView { Content = form };
    }
    protected override async void OnAppearing() { base.OnAppearing(); await RunAsync(LoadAsync); if (focusImport) { focusImport = false; await scroller.ScrollToAsync(importHeading, ScrollToPosition.Start, false); } }
    private static double Number(string? text, string label)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) || !double.IsFinite(value))
            throw new ArgumentException($"Enter a valid {label} using your phone's decimal separator.");
        return value;
    }
    private async Task RunAsync(Func<Task> action)
    {
        if (busy) return; busy = true; form.IsEnabled = false; status.Text = "";
        try { await action(); }
        catch (ArgumentException e) { status.Text = e.Message; }
        catch (TimeoutException) { status.Text = "Health Connect timed out. Nothing was imported; retry when the service is available."; }
        catch (Exception) { status.Text = "Unable to complete the local operation. Check storage or Health Connect availability and retry."; }
        finally { busy = false; form.IsEnabled = true; }
    }
    private async Task LoadAsync()
    { var items = await diary.GetWorkoutsAsync(); records.Clear(); foreach (var item in items) records.Add(item); }
    private void BeginEdit(WorkoutRecord record)
    {
        if (!record.IsManual) return;
        editingSet = null; exercise.Text = reps.Text = load.Text = ""; warmup.IsChecked = false;
        editing = record; date.Date = record.Date.ToDateTime(TimeOnly.MinValue); kind.SelectedIndex = record.Kind == "run" ? 0 : 1;
        title.Text = record.Title; minutes.Text = (record.DurationSeconds / 60).ToString("G17", CultureInfo.CurrentCulture);
        distance.Text = record.DistanceKm?.ToString("G17", CultureInfo.CurrentCulture); notes.Text = record.Notes;
        sets.Clear(); sets.AddRange(record.Sets); RenderSets(); save.Text = "Save changes";
        status.Text = "Editing the selected workout.";
    }
    private void RenderSets()
    {
        setsView.Clear();
        foreach (var set in sets)
        {
            var label = new Label { Text = $"{set.Exercise}: {set.Reps} × {set.LoadKg:g} kg{(set.Warmup ? " (warm-up)" : "")}" };
            var edit = new Button { Text = "Edit set" };
            edit.Clicked += (_, _) => { editingSet = set.Id; exercise.Text = set.Exercise; reps.Text = set.Reps.ToString(CultureInfo.CurrentCulture); load.Text = set.LoadKg.ToString("G17", CultureInfo.CurrentCulture); warmup.IsChecked = set.Warmup; };
            var remove = new Button { Text = "Remove set" };
            remove.Clicked += (_, _) => { sets.Remove(set); if (editingSet == set.Id) { editingSet = null; exercise.Text = reps.Text = load.Text = ""; warmup.IsChecked = false; } RenderSets(); };
            setsView.Add(new VerticalStackLayout { Children = { label, new VerticalStackLayout { Spacing = 8, Children = { edit, remove } } } });
        }
    }
    public void ResetDraft()
    {
        editing = null; editingSet = null; title.Text = minutes.Text = distance.Text = notes.Text = exercise.Text = reps.Text = load.Text = "";
        warmup.IsChecked = false; sets.Clear(); RenderSets(); save.Text = "Add workout"; status.Text = ""; records.Clear();
    }
    private async Task SaveAsync()
    {
        if (!string.IsNullOrWhiteSpace(exercise.Text) || !string.IsNullOrWhiteSpace(reps.Text) || !string.IsNullOrWhiteSpace(load.Text))
            throw new ArgumentException("Save the pending strength set or clear its fields before saving the workout.");
        var now = DateTimeOffset.UtcNow;
        var selectedDate = date.Date ?? DateTime.Today;
        var selectedDay = DateOnly.FromDateTime(selectedDate);
        var offset = editing?.Date == selectedDay ? editing.UtcOffsetMinutes : (int)TimeZoneInfo.Local.GetUtcOffset(selectedDate).TotalMinutes;
        var run = kind.SelectedIndex == 0;
        var record = new WorkoutRecord(editing?.Id ?? Guid.NewGuid(), DateOnly.FromDateTime(selectedDate), offset,
            run ? "run" : "gym", title.Text?.Trim() ?? "", Number(minutes.Text, "duration") * 60,
            run ? Number(distance.Text, "distance") : null, notes.Text, run ? Array.Empty<StrengthSet>() : sets.ToArray(),
            editing?.CreatedAt ?? now, now);
        await diary.SaveWorkoutAsync(record); ResetDraft(); await LoadAsync(); status.Text = "Workout saved locally.";
    }
    private async Task DeleteAsync(WorkoutRecord record)
    {
        if (!await DisplayAlertAsync("Delete local workout?", record.Summary + " This does not delete the source Health Connect session.", "Delete", "Cancel")) return;
        await diary.DeleteWorkoutAsync(record.Id); if (editing?.Id == record.Id) ResetDraft(); await LoadAsync(); status.Text = "Local workout deleted.";
    }
    private async Task ImportAsync()
    {
        var from = DateOnly.FromDateTime(importFrom.Date ?? DateTime.Today);
        var through = DateOnly.FromDateTime(importThrough.Date ?? DateTime.Today);
        var items = await health.ReadAsync(from, through);
        if (items.Count == 0) { status.Text = "No readable sessions in the selected dates. Check Health Connect access and source-app data."; return; }
        if (!await DisplayAlertAsync("Import exercise sessions?", $"Read {items.Count} sessions. Save or update local copies? This does not change Health Connect or calorie targets.", "Import", "Cancel"))
        { status.Text = "Import cancelled. No local data changed."; return; }
        var count = await diary.ImportWorkoutsAsync(items); await LoadAsync(); status.Text = $"Imported or updated {count} sessions; matching unchanged sessions were skipped.";
    }
}
