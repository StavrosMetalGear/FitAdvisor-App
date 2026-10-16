using FitAdvisor.Mobile.ViewModels;
namespace FitAdvisor.Mobile.Views;
public partial class ManualMealPage : ContentPage { public ManualMealPage(MealsViewModel model) { InitializeComponent(); BindingContext=model; } }
