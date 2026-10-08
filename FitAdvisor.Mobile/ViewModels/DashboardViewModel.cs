using FitAdvisor.Core;
namespace FitAdvisor.Mobile.ViewModels;
public sealed class DashboardViewModel : ViewModelBase
{
    private readonly IPersonalDiary diary;
    private string date = "", target = "Create your profile", consumed = "0 kcal logged", remaining = "Target unavailable", context = "";
    public string DateText { get => date; private set => Set(ref date, value); }
    public string TargetText { get => target; private set => Set(ref target, value); }
    public string ConsumedText { get => consumed; private set => Set(ref consumed, value); }
    public string RemainingText { get => remaining; private set => Set(ref remaining, value); }
    public string ContextText { get => context; private set => Set(ref context, value); }
    public Command RefreshCommand { get; }
    public DashboardViewModel(IPersonalDiary diary) { this.diary = diary; RefreshCommand = Action(LoadAsync); }
    public async Task LoadAsync()
    {
        var summary = await diary.GetDashboardAsync(Today);
        DateText = $"Today · {summary.Date:ddd, dd MMM yyyy}";
        TargetText = summary.Target == null ? "Create your profile" : $"{summary.Target.TargetKcal:F0} kcal";
        ConsumedText = $"{summary.ConsumedKcal:F0} kcal logged";
        RemainingText = summary.RemainingKcal is { } left ? (left < 0 ? $"{Math.Abs(left):F0} kcal over allowance" : $"{left:F0} kcal remaining") : "Target unavailable";
        ContextText = summary.MealCount == 0 ? "No meals logged today. An empty diary does not mean no food was consumed." : $"Based on {summary.MealCount} logged meal entries.";
    }
}
