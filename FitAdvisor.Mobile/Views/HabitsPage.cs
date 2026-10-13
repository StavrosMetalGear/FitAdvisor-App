using System.Globalization;
using FitAdvisor.Core;
using FitAdvisor.Storage;
using FitAdvisor.Mobile.Services;

namespace FitAdvisor.Mobile.Views;

public sealed class HabitsPage : ContentPage
{
    private readonly SqlitePersonalDiary diary;
    private readonly VerticalStackLayout body = new() { Padding = 20, Spacing = 16 };
    private readonly Label status = new();
    private readonly ScrollView scroll;
    private HabitSettings settings = new();
    private HabitRecord[] records = [];
    private HabitGoal[] goals = [];
    private bool busy;
    private string T(string en, string el) => AppearancePreferences.Language == 1 ? el : en;
    public HabitsPage(SqlitePersonalDiary diary)
    { this.diary = diary; Content = scroll = new ScrollView { Content = body }; }
    protected override async void OnAppearing() { base.OnAppearing(); await Run(Load); }
    private async Task Run(Func<Task> action)
    {
        if (busy) return; busy = true; body.IsEnabled = false;
        try { await action(); }
        catch (ArgumentException e) { status.Text = T("Check your entries: ", "Ελέγξτε τις τιμές: ") + e.Message; await scroll.ScrollToAsync(0, 0, false); }
        catch (Exception) { status.Text = T("Unable to save locally. Your previous data remains available; retry.", "Δεν έγινε αποθήκευση. Τα προηγούμενα δεδομένα παραμένουν διαθέσιμα· δοκιμάστε ξανά."); }
        finally { busy = false; body.IsEnabled = true; }
    }
    private Label Text(string value, double size = 16) => new() { Text = value, FontSize = size };
    private Button Action(string text, Func<Task> action)
    { var b = DesignComponents.Secondary(text); b.Clicked += async (_, _) => await Run(action); return b; }
    private void Field(VerticalStackLayout stack, string label, View control)
    { stack.Add(Text(label)); SemanticProperties.SetDescription(control, label); control.MinimumHeightRequest = 52; stack.Add(control); }
    private Entry Input(string? value = null) => new() { Text = value, Keyboard = Keyboard.Numeric };
    private static double Number(Entry entry) => double.TryParse(entry.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var n) && double.IsFinite(n)
        ? n : throw new ArgumentException("Enter a finite number using the phone's decimal separator.");
    private static string N(double n) => n.ToString("G17", CultureInfo.CurrentCulture);
    private async Task Load()
    {
        settings = await diary.GetHabitSettingsAsync(); records = await diary.GetHabitsAsync(); goals = await diary.GetHabitGoalsAsync();
        Title = T("Habits & Recovery", "Συνήθειες & Αποκατάσταση"); body.Clear(); status.Text = "";
        body.Add(Text(Title, 28));
        body.Add(DesignComponents.Caption(T("One achievable choice today. Returning after a setback is progress; earlier achievements still count.", "Μία εφικτή επιλογή σήμερα. Η επιστροφή μετά από μια δυσκολία είναι πρόοδος· όσα πετύχατε παραμένουν.")));
        body.Add(DesignComponents.Caption(T("Optional, offline and private. This app records choices; it does not diagnose illness or treat addiction. Backup / Restore includes these records and settings.", "Προαιρετικά, εκτός σύνδεσης και ιδιωτικά. Η εφαρμογή καταγράφει επιλογές· δεν διαγιγνώσκει ασθένειες ούτε θεραπεύει εξαρτήσεις. Το αντίγραφο ασφαλείας περιλαμβάνει τις καταγραφές και τις ρυθμίσεις.")));
        body.Add(status);
        var options = new VerticalStackLayout { Spacing = 8 };
        Toggle(options, T("Smoking tracker", "Καταγραφή καπνίσματος"), settings.Smoking, v => settings with { Smoking = v });
        Toggle(options, T("Alcohol tracker", "Καταγραφή αλκοόλ"), settings.Alcohol, v => settings with { Alcohol = v });
        Toggle(options, T("Eating habits tracker", "Διατροφικές συνήθειες"), settings.Eating, v => settings with { Eating = v });
        Toggle(options, T("Show educational cards", "Εμφάνιση ενημερωτικών καρτών"), settings.Education, v => settings with { Education = v });
        options.Add(DesignComponents.Caption(T("Disabling hides a tracker and retains its records.", "Η απενεργοποίηση κρύβει την καταγραφή και διατηρεί τα δεδομένα.")));
        body.Add(DesignComponents.Card(options));
        if (settings.Smoking) SmokingSettings();
        var today = DateOnly.FromDateTime(DateTime.Today); var week = HabitCalculations.WeekStart(today);
        body.Add(Text(T("Weekly trends · Monday–Sunday", "Εβδομαδιαία τάση · Δευτέρα–Κυριακή"), 22));
        body.Add(DesignComponents.Caption(T("Totals include logged records only. Missing days are unknown. Current week is partial; goals are personal choices, not safety thresholds.", "Τα σύνολα αφορούν μόνο καταγραφές. Οι ημέρες χωρίς καταγραφή είναι άγνωστες. Η τρέχουσα εβδομάδα είναι μερική· οι στόχοι είναι προσωπικές επιλογές, όχι όρια ασφάλειας.")));
        foreach (var kind in new[] { "smoking", "alcohol", "eating" }.Where(Enabled))
        {
            var section = new VerticalStackLayout { Spacing = 10 }; section.Add(Text(KindName(kind), 22));
            if (kind != "eating")
            {
                for (var i = 3; i >= 0; i--)
                {
                    var start = week.AddDays(-7 * i); var items = records.Where(r => r.Kind == kind && r.Date >= start && r.Date <= start.AddDays(6)).ToArray();
                    var total = items.Sum(r => kind == "alcohol" ? r.AlcoholGrams : r.Amount);
                    section.Add(Text($"{start:yyyy-MM-dd} – {start.AddDays(6):yyyy-MM-dd}: {total:0.##} {Unit(kind)} · {items.Select(r => r.Date).Distinct().Count()} " + T("logged days", "ημέρες καταγραφής")));
                }
            }
            if (kind == "smoking")
            {
                section.Add(Text(T("Logged smoke-free days (all history): ", "Καταγεγραμμένες ημέρες χωρίς κάπνισμα (όλο το ιστορικό): ") + records.Count(r => r.Kind == kind && r.Amount == 0)));
                section.Add(Text(T("Estimated money saved: ", "Εκτιμώμενη εξοικονόμηση: ") + $"{HabitCalculations.MoneySaved(records, settings):0.##} {settings.Currency}"));
                section.Add(DesignComponents.Caption(T("Only logged days on/after the quit date: max(0, baseline − cigarettes) × entered cost. Editing baseline or cost recalculates the estimate. A lapse does not reset previous days.", "Μόνο καταγεγραμμένες ημέρες από την ημερομηνία διακοπής: max(0, αρχική ποσότητα − τσιγάρα) × κόστος. Η αλλαγή ποσότητας ή κόστους επανυπολογίζει την εκτίμηση. Μια δυσκολία δεν μηδενίζει τις προηγούμενες ημέρες.")));
            }
            foreach (var goal in goals.Where(g => g.Kind == kind))
            {
                section.Add(Text($"{goal.Name} · {(goal.AtMost ? "≤" : "≥")} {goal.WeeklyTarget:g} {goal.Unit}"));
                for (var i = 3; i >= 0; i--)
                {
                    var start = week.AddDays(-7 * i); var total = HabitCalculations.Total(records, goal, start);
                    section.Add(Text($"{start:yyyy-MM-dd}: {total:0.##}/{goal.WeeklyTarget:g} {goal.Unit}"));
                }
                section.Add(Action(T("Edit goal", "Επεξεργασία στόχου"), () => GoalForm(kind, goal)));
                section.Add(Action(T("Delete goal", "Διαγραφή στόχου"), async () => {
                    if (await DisplayAlertAsync(T("Delete goal?", "Διαγραφή στόχου;"), goal.Name, T("Delete", "Διαγραφή"), T("Cancel", "Ακύρωση"))) { await diary.DeleteHabitGoalAsync(goal.Id); await Load(); }
                }));
            }
            section.Add(Action(T("Add personal goal", "Προσθήκη προσωπικού στόχου"), () => GoalForm(kind)));
            section.Add(Action(T("Add record", "Προσθήκη καταγραφής"), () => RecordForm(kind)));
            body.Add(DesignComponents.Card(section));
            body.Add(Action(T("View / edit history · ", "Προβολή / επεξεργασία ιστορικού · ") + KindName(kind), () => History(kind)));
        }
        if (settings.Education) foreach (var card in HabitEducation.Cards)
        {
            if (!Enabled(card.Kind)) continue;
            body.Add(DesignComponents.Card(new VerticalStackLayout { Spacing = 10, Children = {
                Text(T(card.Title, card.GreekTitle), 20), Text(T(card.English, card.Greek)),
                DesignComponents.Caption($"{card.SourceTitle}\n{card.Date}\n{card.Url}") } }));
        }
    }
    private bool Enabled(string kind) => kind switch { "smoking" => settings.Smoking, "alcohol" => settings.Alcohol, _ => settings.Eating };
    private string KindName(string kind) => kind switch { "smoking" => T("Smoking", "Κάπνισμα"), "alcohol" => T("Alcohol", "Αλκοόλ"), _ => T("Eating habits", "Διατροφικές συνήθειες") };
    private string Unit(string kind) => kind == "smoking" ? T("cigarettes", "τσιγάρα") : T("g alcohol", "g αλκοόλης");
    private void Toggle(VerticalStackLayout layout, string label, bool value, Func<bool, HabitSettings> update)
    {
        var toggle = new Switch { IsToggled = value }; Field(layout, label, toggle);
        toggle.Toggled += async (_, e) => await Run(async () => { await diary.SaveHabitSettingsAsync(update(e.Value)); await Load(); });
    }
    private void SmokingSettings()
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        var hasQuit = new Switch { IsToggled = settings.QuitDate != null };
        var quit = new DatePicker { Date = (settings.QuitDate ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue) };
        var baseline = Input(N(settings.BaselineCigarettes)); var cost = Input(N(settings.CostPerCigarette)); var currency = new Entry { Text = settings.Currency };
        Field(stack, T("Use a quit date (past or planned)", "Ημερομηνία διακοπής (παλαιότερη ή προγραμματισμένη)"), hasQuit);
        Field(stack, T("Quit date", "Ημερομηνία διακοπής"), quit);
        Field(stack, T("Baseline cigarettes per day", "Αρχικά τσιγάρα ανά ημέρα"), baseline);
        Field(stack, T("Cost per cigarette (pack price ÷ pack size)", "Κόστος ανά τσιγάρο (τιμή πακέτου ÷ τεμάχια)"), cost);
        Field(stack, T("Currency label", "Νόμισμα"), currency);
        stack.Add(Action(T("Save smoking settings", "Αποθήκευση ρυθμίσεων καπνίσματος"), async () => {
            await diary.SaveHabitSettingsAsync(settings with { QuitDate = hasQuit.IsToggled ? DateOnly.FromDateTime(quit.Date ?? DateTime.Today) : null,
                BaselineCigarettes = Number(baseline), CostPerCigarette = Number(cost), Currency = currency.Text?.Trim() ?? "" }); await Load();
        })); body.Add(DesignComponents.Card(stack));
    }
    private Task ShowForm(VerticalStackLayout form)
    { body.Clear(); status.Text = ""; body.Add(status); body.Add(DesignComponents.Card(form)); body.Add(Action(T("Cancel / back", "Ακύρωση / επιστροφή"), Load)); return scroll.ScrollToAsync(0, 0, false); }
    private Task RecordForm(string kind, HabitRecord? edit = null)
    {
        var form = new VerticalStackLayout { Spacing = 10 };
        form.Add(Text(KindName(kind), 24));
        var date = new DatePicker { Date = (edit?.Date ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue), MaximumDate = DateTime.Today };
        Field(form, T("Local date", "Τοπική ημερομηνία"), date);
        var name = new Entry { Text = edit?.Name ?? (kind == "smoking" ? KindName(kind) : "") };
        var amount = Input(edit == null ? null : N(edit.Amount)); var abv = Input(edit?.Abv is { } a ? N(a) : null);
        var craving = Input(edit?.Craving?.ToString(CultureInfo.CurrentCulture)); var notes = new Entry { Text = edit?.Trigger };
        var available = goals.Where(g => g.Kind == "eating").ToArray();
        var goal = new Picker { ItemsSource = available.Select(g => g.Name + " · " + g.Unit).ToArray(), SelectedIndex = edit == null ? -1 : Array.FindIndex(available, g => g.Id == edit.GoalId) };
        if (kind == "eating") Field(form, T("Choose your goal and unit", "Επιλέξτε στόχο και μονάδα"), goal);
        else if (kind == "alcohol") Field(form, T("Drink type (e.g. beer, wine)", "Είδος ποτού (π.χ. μπίρα, κρασί)"), name);
        Field(form, kind == "smoking" ? T("Total cigarettes for this day (0 allowed)", "Σύνολο τσιγάρων ημέρας (επιτρέπεται 0)") : kind == "alcohol" ? T("Volume consumed (ml)", "Όγκος που καταναλώθηκε (ml)") : T("Amount in the selected goal's unit (0 allowed)", "Ποσότητα στη μονάδα του επιλεγμένου στόχου (επιτρέπεται 0)"), amount);
        if (kind == "alcohol")
        {
            Field(form, "ABV (%)", abv); var estimate = Text(""); form.Add(estimate);
            void Preview() { try { estimate.Text = $"{Number(amount) * Number(abv) / 100 * 0.789:0.##} " + Unit(kind); } catch (ArgumentException) { estimate.Text = T("Enter volume and ABV to calculate grams.", "Συμπληρώστε όγκο και ABV για υπολογισμό γραμμαρίων."); } }
            amount.TextChanged += (_, _) => Preview(); abv.TextChanged += (_, _) => Preview(); Preview();
            form.Add(DesignComponents.Caption(T("grams = ml × ABV/100 × 0.789. Approximation, not blood alcohol or a safe-driving estimate. No generic drink counts.", "γραμμάρια = ml × ABV/100 × 0,789. Προσέγγιση, όχι εκτίμηση αλκοόλ αίματος ή ασφάλειας οδήγησης.")));
        }
        if (kind == "smoking") Field(form, T("Craving 0–10 (optional)", "Έντονη επιθυμία 0–10 (προαιρετικά)"), craving);
        Field(form, kind == "smoking" ? T("Triggers / notes (optional)", "Ερεθίσματα / σημειώσεις (προαιρετικά)") : T("Notes / portion context (optional)", "Σημειώσεις / πλαίσιο μερίδας (προαιρετικά)"), notes);
        form.Add(Action(T("Save locally", "Αποθήκευση στη συσκευή"), async () => {
            if (kind == "eating" && goal.SelectedIndex < 0) throw new ArgumentException("Create and select an eating goal first.");
            int? rating = null;
            if (kind == "smoking" && !string.IsNullOrWhiteSpace(craving.Text))
            { if (!int.TryParse(craving.Text, out var value)) throw new ArgumentException("Craving must be a whole number 0–10."); rating = value; }
            await diary.SaveHabitAsync(new(edit?.Id ?? Guid.NewGuid(), DateOnly.FromDateTime(date.Date ?? DateTime.Today), kind,
                kind == "eating" ? available[goal.SelectedIndex].Name : name.Text?.Trim() ?? "", Number(amount), kind == "alcohol" ? Number(abv) : null,
                rating, notes.Text, kind == "eating" ? available[goal.SelectedIndex].Id : null)); await Load();
        })); return ShowForm(form);
    }
    private Task GoalForm(string kind, HabitGoal? edit = null)
    {
        var form = new VerticalStackLayout { Spacing = 10 };
        var name = new Entry { Text = edit?.Name }; var unit = new Entry { Text = edit?.Unit ?? (kind == "eating" ? "times" : kind == "smoking" ? "cigarettes" : "g alcohol"), IsEnabled = edit == null && kind == "eating" };
        var target = Input(edit == null ? null : N(edit.WeeklyTarget)); var direction = new Picker { ItemsSource = new[] { T("At most", "Το πολύ"), T("At least", "Τουλάχιστον") }, SelectedIndex = edit?.AtMost == false ? 1 : 0 };
        form.Add(Text(T("Personal weekly goal", "Προσωπικός εβδομαδιαίος στόχος"), 24));
        Field(form, T("Your goal (e.g. takeaway frequency, sugary drinks, snacks)", "Ο στόχος σας (π.χ. συχνότητα έτοιμου φαγητού, ζαχαρούχα ποτά, σνακ)"), name);
        Field(form, T("Unit (e.g. times, ml, portions)", "Μονάδα (π.χ. φορές, ml, μερίδες)"), unit);
        Field(form, T("Weekly target", "Εβδομαδιαίος στόχος"), target); Field(form, T("Direction", "Κατεύθυνση"), direction);
        form.Add(Action(T("Save goal", "Αποθήκευση στόχου"), async () => { await diary.SaveHabitGoalAsync(new(edit?.Id ?? Guid.NewGuid(), kind, name.Text?.Trim() ?? "", unit.Text?.Trim() ?? "", Number(target), direction.SelectedIndex == 0)); await Load(); }));
        return ShowForm(form);
    }
    private Task History(string kind, int page = 0)
    {
        var history = new VerticalStackLayout { Spacing = 12 }; var sorted = records.Where(r => r.Kind == kind).OrderByDescending(r => r.Date).ThenBy(r => r.Id).ToArray();
        history.Add(Text(T("History", "Ιστορικό"), 24));
        foreach (var r in sorted.Skip(page * 20).Take(20))
        {
            var unit = kind == "eating" ? goals.First(g => g.Id == r.GoalId).Unit : kind == "alcohol" ? "ml" : Unit(kind);
            history.Add(DesignComponents.Card(new VerticalStackLayout { Spacing = 8, Children = {
                Text($"{r.Date:yyyy-MM-dd} · {r.Name}: {r.Amount:g} {unit}"),
                Text(kind == "alcohol" ? $"{r.Abv:g}% ABV · {r.AlcoholGrams:0.##} {Unit(kind)}" : kind == "smoking" ? T("Craving: ", "Επιθυμία: ") + (r.Craving?.ToString() ?? "—") : ""), Text(r.Trigger ?? ""),
                Action(T("Edit record", "Επεξεργασία καταγραφής"), () => RecordForm(kind, r)),
                Action(T("Delete record", "Διαγραφή καταγραφής"), async () => {
                    if (await DisplayAlertAsync(T("Delete this record?", "Διαγραφή αυτής της καταγραφής;"), $"{r.Date:yyyy-MM-dd} · {r.Name}", T("Delete", "Διαγραφή"), T("Cancel", "Ακύρωση"))) { await diary.DeleteHabitAsync(r.Id); await Load(); }
                }) } }));
        }
        if (page > 0) history.Add(Action(T("Previous records", "Προηγούμενες καταγραφές"), () => History(kind, page - 1)));
        if (sorted.Length > (page + 1) * 20) history.Add(Action(T("More records", "Περισσότερες καταγραφές"), () => History(kind, page + 1)));
        if (sorted.Length == 0) history.Add(Text(T("No records yet. Start when it suits you.", "Δεν υπάρχουν καταγραφές. Ξεκινήστε όταν σας ταιριάζει.")));
        return ShowForm(history);
    }
}
