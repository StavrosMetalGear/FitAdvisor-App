using FitAdvisor.Core;
namespace FitAdvisor.Mobile.Views;
public sealed class ExercisePickerPage : ContentPage
{
    public ExercisePickerPage(GymExercise[] exercises, Func<GymExercise,Task> select)
    {
        Title="Add exercise / Προσθήκη άσκησης";
        var query=new Entry {Placeholder="Name, muscle, equipment / Όνομα, μύες, εξοπλισμός",AutomationId="ExerciseSearch"};
        var muscle=new Picker {Title="Muscle group / Μυϊκή ομάδα"};var equipment=new Picker {Title="Equipment / Εξοπλισμός"};
        muscle.ItemsSource=exercises.Select(e=>e.Muscle).Distinct().Order().ToArray();equipment.ItemsSource=exercises.Select(e=>e.Equipment).Distinct().Order().ToArray();
        var filterPanel=new VerticalStackLayout {Spacing=8,IsVisible=false,Children={muscle,equipment}};
        var filtersButton=new Button {Text="Muscle & equipment filters / Φίλτρα μυών & εξοπλισμού"};filtersButton.Clicked+=(_,_)=>{query.Unfocus();filterPanel.IsVisible=!filterPanel.IsVisible;};
        query.Focused+=(_,_)=>filterPanel.IsVisible=false;
        var filters=new Label();var results=new VerticalStackLayout {Spacing=8};var status=new Label();
        void Render() {
            results.Clear();filters.Text=$"{muscle.SelectedItem??"All muscles"} · {equipment.SelectedItem??"All equipment"}";
            var filtered=exercises.Where(e=>(muscle.SelectedIndex<0||e.Muscle==muscle.SelectedItem as string)&&(equipment.SelectedIndex<0||e.Equipment==equipment.SelectedItem as string)).ToArray();
            var matches=filtered.Where(e=>GymCatalogue.Matches(e,query.Text??"")).ToArray();var typo=matches.Length==0;
            if(typo)matches=filtered.Where(e=>SearchSuggestions.Near(e.Display,query.Text??"")).ToArray();
            status.Text=matches.Length==0?"No matches. Clear filters or try another term. / Χωρίς αποτέλεσμα· καθάρισε φίλτρα.":typo?"Possible spelling matches; select exact variation. / Πιθανές αντιστοιχίες.":$"{matches.Length} matches · Select exact variation / Επίλεξε παραλλαγή";
            foreach(var e in matches) {var b=new Button {Text=e.Display,LineBreakMode=LineBreakMode.WordWrap};SemanticProperties.SetDescription(b,e.Display);b.Clicked+=async(_,_)=>{b.IsEnabled=false;query.Unfocus();try{await select(e);await Navigation.PopAsync();}catch(ArgumentException error){status.Text=error.Message;}finally{b.IsEnabled=true;}};results.Add(b);}
        }
        var clear=new Button {Text="Clear filters / Καθαρισμός φίλτρων"};clear.Clicked+=(_,_)=>{query.Text="";muscle.SelectedIndex=equipment.SelectedIndex=-1;Render();};
        query.TextChanged+=(_,_)=>Render();muscle.SelectedIndexChanged+=(_,_)=>Render();equipment.SelectedIndexChanged+=(_,_)=>Render();
        Content=new ScrollView {Content=new VerticalStackLayout {Padding=16,Spacing=10,Children={query,filtersButton,filterPanel,filters,clear,status,results}}};Render();
    }
}
