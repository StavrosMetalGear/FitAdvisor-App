using System.Globalization;
using FitAdvisor.Core;
using FitAdvisor.Storage;
namespace FitAdvisor.Mobile.Views;
public sealed class CustomFoodPage : ContentPage
{
    public CustomFoodPage(IPersonalDiary diary, FoodNutritionSnapshot? label = null)
    {
        Title="Add custom food / Δικό μου τρόφιμο";
        var stack=new VerticalStackLayout {Padding=20,Spacing=12};
        stack.Add(new Label {Text="Save the exact package label once. Nutrition per 100 g. Leave unknown macros blank. This creates a local product; it never changes the source or past meals. / Τιμές ανά 100 g."});
        Entry Field(string text,string? value=null) {stack.Add(new Label {Text=text});var e=new Entry {Text=value};stack.Add(e);return e;}
        var name=Field("Exact name, brand, variant / Όνομα, μάρκα, παραλλαγή",label?.Description);
        var barcode=Field("Barcode (optional) / Barcode",label?.ProductCode);
        var basis=new Picker {Title="Nutrition basis / Βάση θρεπτικών",ItemsSource=new[]{"Per 100 g / Ανά 100 g","Per 100 ml / Ανά 100 ml"},SelectedIndex=label?.Unit=="ml"?1:0};stack.Add(basis);
        var kcal=Field("kcal / 100 (selected unit)",label?.Usable==true?label.CaloriesPer100G.ToString(CultureInfo.CurrentCulture):null);kcal.Keyboard=Keyboard.Numeric;
        var protein=Field("Protein g / 100 g (optional)");var carbs=Field("Carbohydrates g / 100 g (optional)");var fat=Field("Fat g / 100 g (optional)");
        foreach(var entry in new[]{protein,carbs,fat})entry.Keyboard=Keyboard.Numeric;
        var status=new Label();stack.Add(status);var save=new Button {Text="Save reusable food / Αποθήκευση τροφίμου"};stack.Add(save);
        save.Clicked+=async(_,_)=> {save.IsEnabled=false;try {
            double? N(Entry e)=>string.IsNullOrWhiteSpace(e.Text)?null:double.TryParse(e.Text,NumberStyles.Float,CultureInfo.CurrentCulture,out var n)?n:throw new ArgumentException("Enter valid nutrition numbers.");
            if(diary is not SqlitePersonalDiary local)throw new ArgumentException("Local storage unavailable.");
            var id=Guid.NewGuid().ToString();var snapshot=new FoodNutritionSnapshot("User-created package label / Τοπική ετικέτα",0,name.Text?.Trim()??"",100,N(kcal)??throw new ArgumentException("kcal required"),N(protein),N(carbs),N(fat)) {Unit=basis.SelectedIndex==1?"ml":"g",Kind="custom",LibraryId=id,ProductCode=barcode.Text?.Trim(),Basis=$"User-entered per 100 {(basis.SelectedIndex==1?"ml":"g")}; check preparation"};
            await local.SaveFoodAsync(new(id,snapshot));await Navigation.PopAsync();
        }catch(Exception e){status.Text=e is ArgumentException?e.Message:"Could not save locally. Retry.";}finally{save.IsEnabled=true;}};
        Content=new ScrollView {Content=stack};
    }
}
