using FitAdvisor.Core;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
namespace FitAdvisor.Pages;
public class CaloriePlanModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    public CaloriePlanModel(UserManager<ApplicationUser> userManager) => _userManager = userManager;
    public string? CalculationError { get; set; }
    public double Weight { get; set; }
    public double Height { get; set; }
    public double TargetWeight { get; set; }
    public int EasyCalories { get; set; }
    public int MediumCalories { get; set; }
    public int HardCalories { get; set; }
    public double CurrentBodyFat { get; set; }
    public double TargetBodyFat { get; set; }
    public async Task OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) { CalculationError = "Sign in to view your saved profile."; return; }
        Weight = user.Weight; Height = user.Height; TargetWeight = user.TargetWeight;
        try
        {
            if (user.Gender is not ("Male" or "Female"))
                throw new ArgumentException("The saved profile must specify a recognized formula coefficient.");
            // Compatibility: the legacy profile has no age field.
            var inputs = new CalculationInputs(Weight, Height, 30,
                user.Gender == "Male" ? FormulaSex.Male : FormulaSex.Female);
            var plans = NutritionCalculations.LegacyCaloriePlans(inputs);
            EasyCalories = plans.Easy; MediumCalories = plans.Medium; HardCalories = plans.Hard;
            CurrentBodyFat = NutritionCalculations.BodyFat(inputs).Percentage ?? double.NaN;
            TargetBodyFat = NutritionCalculations.BodyFat(inputs with { WeightKg = TargetWeight }).Percentage ?? double.NaN;
        }
        catch (ArgumentException)
        {
            CalculationError = "Profile values cannot be calculated. Enter weight in kg and height in cm in the nutrition calculator.";
        }
    }
}
