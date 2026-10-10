using System.Collections.ObjectModel;
using FitAdvisor.Core;
namespace FitAdvisor.Mobile.ViewModels;
public sealed class WeightViewModel : ViewModelBase
{
    private readonly IPersonalDiary diary;
    private readonly IUserDialogs dialogs;
    private Guid? editing;
    private DateTime date = DateTime.Today;
    private string weight = "", notes = "";
    public DateTime Date { get => date; set => Set(ref date, value); }
    public string Weight { get => weight; set => Set(ref weight, value); }
    public string Notes { get => notes; set => Set(ref notes, value); }
    public string SaveLabel => editing == null ? "Add weight" : "Save weight changes";
    public ObservableCollection<WeightRecord> Weights { get; } = new();
    public Command SaveCommand { get; }
    public Command CancelCommand { get; }
    public Command EditCommand { get; }
    public Command DeleteCommand { get; }
    public WeightViewModel(IPersonalDiary diary, IUserDialogs dialogs)
    {
        this.diary = diary; this.dialogs = dialogs;
        SaveCommand = Action(SaveAsync); CancelCommand = Action(() => { Clear(); return Task.CompletedTask; });
        EditCommand = ItemAction<WeightRecord>(EditAsync); DeleteCommand = ItemAction<WeightRecord>(DeleteAsync);
    }
    public async Task LoadAsync()
    {
        var list = await diary.GetWeightsAsync(); Weights.Clear(); foreach (var item in list) Weights.Add(item);
    }
    private Task EditAsync(WeightRecord value)
    {
        editing = value.Id; Date = value.Date.ToDateTime(TimeOnly.MinValue); Weight = Display(value.WeightKg); Notes = value.Notes ?? "";
        Notify(nameof(SaveLabel)); Status = "Editing the selected weight entry."; return Task.CompletedTask;
    }
    private async Task SaveAsync()
    {
        await diary.SaveWeightAsync(new(editing ?? Guid.NewGuid(), DateOnly.FromDateTime(Date), Number(Weight, "weight (kg)"), Notes));
        Clear(); await LoadAsync(); Status = "Weight saved locally. Profile calculation weight is unchanged; update it in Profile when needed.";
    }
    private async Task DeleteAsync(WeightRecord value)
    {
        if (!await dialogs.ConfirmDeleteAsync(value.Summary)) return;
        await diary.DeleteWeightAsync(value.Id); if (editing == value.Id) Clear(); await LoadAsync(); Status = "Weight deleted.";
    }
    private void Clear() { editing = null; Weight = ""; Notes = ""; Notify(nameof(SaveLabel)); }
    public void ResetAfterRestore() { Clear(); Weights.Clear(); Status = ""; }
}
