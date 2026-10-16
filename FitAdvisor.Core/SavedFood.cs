namespace FitAdvisor.Core;

// Source datasets remain separate; a selection freezes its own basis and source.
public sealed record SavedFood(string Id, FoodNutritionSnapshot Nutrition, bool Favorite = false, DateTimeOffset? LastUsed = null)
{
    public void Validate() { DiaryValidation.Text(Id, 100, "Product ID"); Nutrition.Validate(); }
    public bool Matches(string query) => query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(t =>
        FoodCatalogue.Normalize(Nutrition.Description + " " + Nutrition.ProductCode).Contains(FoodCatalogue.Normalize(t)));
}
