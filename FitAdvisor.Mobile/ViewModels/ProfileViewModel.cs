using FitAdvisor.Core;
namespace FitAdvisor.Mobile.ViewModels;
public sealed class ProfileViewModel : ViewModelBase
{
    private readonly IPersonalDiary diary;
    private bool loaded;
    private string name = "", weight = "", height = "", age = "", targetWeight = "", multiplier = "1.2", adjustment = "0", estimate = "", history = "";
    private int sexIndex = -1;
    public string Name { get => name; set => Set(ref name, value); }
    public string Weight { get => weight; set => Set(ref weight, value); }
    public string Height { get => height; set => Set(ref height, value); }
    public string Age { get => age; set => Set(ref age, value); }
    public string TargetWeight { get => targetWeight; set => Set(ref targetWeight, value); }
    public string Multiplier { get => multiplier; set => Set(ref multiplier, value); }
    public string Adjustment { get => adjustment; set => Set(ref adjustment, value); }
    public int SexIndex { get => sexIndex; set => Set(ref sexIndex, value); }
    public string EstimateText { get => estimate; private set => Set(ref estimate, value); }
    public string HistoryText { get => history; private set => Set(ref history, value); }
    public Command SaveCommand { get; }
    public ProfileViewModel(IPersonalDiary diary) { this.diary = diary; SaveCommand = Action(SaveAsync); }
    public async Task LoadAsync()
    {
        if (loaded) return;
        var revision = await diary.GetProfileAsync();
        if (revision != null)
        {
            var p = revision.Values; Name = p.Name; Weight = Display(p.WeightKg); Height = Display(p.HeightCm);
            Age = p.AgeYears.ToString(); SexIndex = (int)p.Sex; TargetWeight = p.TargetWeightKg is { } n ? Display(n) : "";
            Multiplier = Display(p.ActivityMultiplier); Adjustment = Display(p.GoalAdjustmentKcal); ShowEstimate(p);
        }
        else { Multiplier = Display(1.2); Adjustment = Display(0); }
        await ShowHistory(); loaded = true;
    }
    private void ShowEstimate(ProfileDraft p)
    {
        var result = p.Calculate();
        EstimateText = $"Estimated resting energy {result.RestingKcal:F0}; maintenance {result.MaintenanceKcal:F0}; target {result.TargetKcal:F0} kcal/day.";
    }
    private async Task ShowHistory()
    {
        var revisions = await diary.GetProfileHistoryAsync();
        HistoryText = string.Join(Environment.NewLine, revisions.Take(8).Select(p => $"{p.EffectiveDate:yyyy-MM-dd} · {p.Values.Calculate().TargetKcal:F0} kcal · {p.Values.WeightKg:g} kg · age {p.Values.AgeYears}"));
    }
    private async Task SaveAsync()
    {
        if (!int.TryParse(Age, out var years)) throw new ArgumentException("Enter age as whole years.");
        if (SexIndex is not (0 or 1)) throw new ArgumentException("Select the formula sex coefficient.");
        var profile = new ProfileDraft(Name, Number(Weight, "weight (kg)"), Number(Height, "height (cm)"), years,
            (FormulaSex)SexIndex, Number(Multiplier, "activity multiplier"), Number(Adjustment, "calorie adjustment"), OptionalNumber(TargetWeight, "target weight (kg)"));
        await diary.SaveProfileAsync(profile, Today); loaded = true; ShowEstimate(profile); await ShowHistory();
        Status = "Profile saved. Today's target updated; previous dates and entry contexts are preserved.";
    }
}
