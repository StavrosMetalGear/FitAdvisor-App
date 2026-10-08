using FitAdvisor.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FitAdvisor.Pages;
public class NutritionCalculatorModel : PageModel
{
    [BindProperty] public CalculatorInput Input { get; set; } = new();
    public NutritionEstimate? Result { get; private set; }
    public BodyFatEstimate? BodyFat { get; private set; }
    public class CalculatorInput
    {
        [Range(20d, 500d)] public double WeightKg { get; set; }
        [Range(100d, 250d)] public double HeightCm { get; set; }
        [Range(18, 120)] public int AgeYears { get; set; }
        [Required] public FormulaSex? Sex { get; set; }
        [Range(1d, 2.5d)] public double ActivityMultiplier { get; set; } = 1.2;
        [Range(-1000d, 1000d)] public double GoalAdjustmentKcal { get; set; }
        public ActivityAccounting Accounting { get; set; } = ActivityAccounting.IncludesExercise;
        [Range(0d, 10000d)] public double ActivityKcal { get; set; }
        [Range(0d, 1d)] public double ExerciseCreditFraction { get; set; }
    }
    public void OnGet() { }
    public void OnPost()
    {
        if (!ModelState.IsValid) return;
        try
        {
            var inputs = new CalculationInputs(Input.WeightKg, Input.HeightCm, Input.AgeYears, Input.Sex!.Value);
            Result = NutritionCalculations.Estimate(inputs, Input.ActivityMultiplier, Input.GoalAdjustmentKcal,
                Input.Accounting, Input.ActivityKcal, Input.ExerciseCreditFraction);
            BodyFat = NutritionCalculations.BodyFat(inputs);
        }
        catch (ArgumentException error) { ModelState.AddModelError(string.Empty, error.Message); }
    }
}
