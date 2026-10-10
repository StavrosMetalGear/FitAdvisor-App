using System.Collections.ObjectModel;
using FitAdvisor.Core;
namespace FitAdvisor.Mobile.ViewModels;
public sealed class MealsViewModel : ViewModelBase
{
    private readonly IPersonalDiary diary;
    private readonly IUserDialogs dialogs;
    private Guid? editing;
    private DateTime date = DateTime.Today;
    private DateTime? listDate;
    private string name = "", quantity = "1", unit = "serving", calories = "", protein = "", carbs = "", fat = "";
    public DateTime Date { get => date; set { if (Set(ref date, value)) { listDate = null; Meals.Clear(); Notify(nameof(DayText)); } } }
    public string DayText => listDate is { } loaded ? $"Meals for {loaded:yyyy-MM-dd}" : "Tap Show meals to load the selected date.";
    public string Name { get => name; set => Set(ref name, value); }
    public string Quantity { get => quantity; set => Set(ref quantity, value); }
    public string Unit { get => unit; set => Set(ref unit, value); }
    public string Calories { get => calories; set => Set(ref calories, value); }
    public string Protein { get => protein; set => Set(ref protein, value); }
    public string Carbs { get => carbs; set => Set(ref carbs, value); }
    public string Fat { get => fat; set => Set(ref fat, value); }
    public string SaveLabel => editing == null ? "Add meal" : "Save meal changes";
    public ObservableCollection<MealRecord> Meals { get; } = new();
    public Command SaveCommand { get; }
    public Command CancelCommand { get; }
    public Command LoadCommand { get; }
    public Command EditCommand { get; }
    public Command DeleteCommand { get; }
    public MealsViewModel(IPersonalDiary diary, IUserDialogs dialogs)
    {
        this.diary = diary; this.dialogs = dialogs;
        SaveCommand = Action(SaveAsync); CancelCommand = Action(() => { Clear(); return Task.CompletedTask; });
        LoadCommand = Action(LoadAsync); EditCommand = ItemAction<MealRecord>(EditAsync); DeleteCommand = ItemAction<MealRecord>(DeleteAsync);
    }
    public async Task LoadAsync()
    {
        var selected = Date;
        var list = await diary.GetMealsAsync(DateOnly.FromDateTime(selected));
        if (Date != selected) return;
        listDate = selected; Meals.Clear(); foreach (var item in list) Meals.Add(item); Notify(nameof(DayText));
    }
    private Task EditAsync(MealRecord meal)
    {
        editing = meal.Id; Date = meal.Date.ToDateTime(TimeOnly.MinValue); Name = meal.Name; Quantity = Display(meal.Quantity); Unit = meal.Unit;
        Calories = Display(meal.CaloriesKcal); Protein = meal.ProteinG is { } p ? Display(p) : "";
        Carbs = meal.CarbohydratesG is { } c ? Display(c) : ""; Fat = meal.FatG is { } f ? Display(f) : "";
        Notify(nameof(SaveLabel)); Status = "Editing the selected meal. Change the date to move it to another day."; return Task.CompletedTask;
    }
    private async Task SaveAsync()
    {
        var meal = new MealRecord(editing ?? Guid.NewGuid(), DateOnly.FromDateTime(Date), Name,
            Number(Quantity, "quantity"), Unit, Number(Calories, "total calories"), OptionalNumber(Protein, "protein"), OptionalNumber(Carbs, "carbohydrates"), OptionalNumber(Fat, "fat"));
        await diary.SaveMealAsync(meal); Clear(); await LoadAsync(); Status = "Meal saved locally.";
    }
    private async Task DeleteAsync(MealRecord meal)
    {
        if (!await dialogs.ConfirmDeleteAsync(meal.Name)) return;
        await diary.DeleteMealAsync(meal.Id); if (editing == meal.Id) Clear(); await LoadAsync(); Status = "Meal deleted.";
    }
    private void Clear()
    {
        editing = null; Name = ""; Quantity = "1"; Unit = "serving"; Calories = ""; Protein = ""; Carbs = ""; Fat = ""; Notify(nameof(SaveLabel));
    }
    public void ResetAfterRestore() { Clear(); listDate = null; Meals.Clear(); Notify(nameof(DayText)); Status = ""; }
}
