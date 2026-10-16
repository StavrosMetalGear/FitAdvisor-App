using System.Globalization;
using FitAdvisor.Core;
using FitAdvisor.Mobile.Services;

namespace FitAdvisor.Mobile.Views;

public sealed class MacroChart : VerticalStackLayout
{
    private readonly Label legend = new() { AutomationId = "MacroLegend" };
    private readonly GraphicsView chart = new() { HeightRequest = 190, AutomationId = "MacroChart" };
    private readonly Entry protein = new() { Keyboard = Keyboard.Numeric };
    private readonly Entry carbs = new() { Keyboard = Keyboard.Numeric };
    private readonly Entry fat = new() { Keyboard = Keyboard.Numeric };
    private readonly Label targetStatus = new();
    private MacroEnergy current = MacroEnergy.From([]);
    private int language = -1;
    private bool Greek => AppearancePreferences.Language == 1;
    private string T(string english, string greek) => Greek ? greek : english;
    public MacroChart() => Build();
    private void Build()
    {
        Children.Clear(); language = AppearancePreferences.Language;
        Spacing = 10;
        Add(new Label { Text = T("Daily macro energy", "Ημερήσια ενέργεια μακροθρεπτικών"), FontSize = 22 });
        Add(chart); Add(legend);
        var options=new VerticalStackLayout {Spacing=10,IsVisible=false};
        var expand=new Button {Text=T("Macro details & targets","Λεπτομέρειες & στόχοι μακροθρεπτικών")};expand.Clicked+=(_,_)=>options.IsVisible=!options.IsVisible;Add(expand);
        options.Add(new Label { Text = T("Simplified energy: protein 4 kcal/g, carbohydrates 4 kcal/g, fat 9 kcal/g. Percentages refer to macro energy, not gram weight. Macro energy can differ from recorded calories due to rounding, fibre, alcohol or source conventions.", "Απλοποιημένη ενέργεια: πρωτεΐνη 4 kcal/g, υδατάνθρακες 4 kcal/g, λίπος 9 kcal/g. Ποσοστά ενέργειας, όχι βάρους. Διαφορές από τις καταχωρισμένες θερμίδες μπορεί να οφείλονται σε στρογγυλοποίηση, φυτικές ίνες, αλκοόλ ή συμβάσεις της πηγής.") });
        options.Add(new Label { Text = T("Optional percentage targets (total 100%)", "Προαιρετικοί στόχοι ποσοστών (σύνολο 100%)") });
        protein.Placeholder = T("Protein %", "Πρωτεΐνη %"); carbs.Placeholder = T("Carbohydrates %", "Υδατάνθρακες %"); fat.Placeholder = T("Fat %", "Λίπος %");
        foreach (var entry in new[] {protein, carbs, fat}) { SemanticProperties.SetDescription(entry, entry.Placeholder); options.Add(entry); }
        if (Preferences.Default.ContainsKey("macro.protein")) {
            protein.Text = Preferences.Default.Get("macro.protein", 0d).ToString(CultureInfo.CurrentCulture);
            carbs.Text = Preferences.Default.Get("macro.carbs", 0d).ToString(CultureInfo.CurrentCulture);
            fat.Text = Preferences.Default.Get("macro.fat", 0d).ToString(CultureInfo.CurrentCulture);
        }
        var apply = new Button { Text = T("Save targets", "Αποθήκευση στόχων") };
        apply.Clicked += (_,_) => {
            try {
                if (!double.TryParse(protein.Text, out var p) || !double.TryParse(carbs.Text, out var c) || !double.TryParse(fat.Text, out var f)) throw new ArgumentException(T("Enter three percentages.", "Συμπληρώστε τρία ποσοστά."));
                MacroEnergy.ValidateTargets(p,c,f);
                Preferences.Default.Set("macro.protein", p); Preferences.Default.Set("macro.carbs", c); Preferences.Default.Set("macro.fat", f);
                Update(current);
            } catch (ArgumentException) { targetStatus.Text = T("Enter percentages from 0 to 100 totaling 100%.", "Εισάγετε ποσοστά 0–100 με σύνολο 100%."); }
        };
        var clear = new Button { Text = T("Clear targets", "Διαγραφή στόχων") };
        clear.Clicked += (_,_) => { foreach (var key in new[] {"macro.protein", "macro.carbs", "macro.fat"}) Preferences.Default.Remove(key); protein.Text = carbs.Text = fat.Text = ""; Update(current); };
        options.Add(apply); options.Add(clear); options.Add(targetStatus);
        Add(options);
    }
    public void Update(MacroEnergy value)
    {
        if (language != AppearancePreferences.Language) Build();
        current = value; chart.IsVisible = value.CanChart;
        var names = new[] {T("Protein (green)", "Πρωτεΐνη (πράσινο)"), T("Carbohydrates (blue)", "Υδατάνθρακες (μπλε)"), T("Fat (orange)", "Λίπος (πορτοκαλί)")};
        var grams = new[] {value.Protein, value.Carbohydrates, value.Fat};
        var percentages = new[] {value.ProteinPercent, value.CarbohydratePercent, value.FatPercent};
        legend.Text = value.CanChart ? string.Join("\n", Enumerable.Range(0,3).Select(i=>$"{names[i]}: {grams[i]:0.##} g · {percentages[i]:0.##}%"))
            : value.Meals == 0 ? T("No meals on this day. No macro chart.", "Δεν υπάρχουν γεύματα. Δεν εμφανίζεται διάγραμμα.")
            : value.IncompleteMeals > 0 ? T($"Incomplete macro data: {value.IncompleteMeals} meal(s). Percentages unavailable. Known totals: protein {value.Protein:0.##} g, carbohydrates {value.Carbohydrates:0.##} g, fat {value.Fat:0.##} g.", $"Ελλιπή δεδομένα: {value.IncompleteMeals} γεύματα. Ποσοστά μη διαθέσιμα. Γνωστά σύνολα: πρωτεΐνη {value.Protein:0.##} g, υδατάνθρακες {value.Carbohydrates:0.##} g, λίπος {value.Fat:0.##} g.")
            : T("Zero macro energy. Percentages unavailable.", "Μηδενική ενέργεια. Ποσοστά μη διαθέσιμα.");
        SemanticProperties.SetDescription(chart, legend.Text);
        chart.Drawable = new Donut(percentages); chart.Invalidate();
        targetStatus.Text = Preferences.Default.ContainsKey("macro.protein")
            ? string.Join("\n", Enumerable.Range(0,3).Select(i => {
                var target = Preferences.Default.Get(new[] {"macro.protein", "macro.carbs", "macro.fat"}[i],0d);
                return value.CanChart ? $"{names[i]}: {percentages[i]:0.##}% / {target:0.##}% ({percentages[i]-target:+0.##;-0.##;0} pp)" : $"{names[i]}: {target:0.##}% — {T("comparison unavailable", "σύγκριση μη διαθέσιμη")}";
            })) : T("No macro targets set.", "Δεν έχουν οριστεί στόχοι.");
    }
    private sealed class Donut(double[] percentages) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF bounds)
        {
            var colors = new[] {Color.FromArgb("#187D71"), Color.FromArgb("#3977C3"), Color.FromArgb("#BF6919")};
            var size = Math.Min(bounds.Width, bounds.Height)-40; var x = (bounds.Width-size)/2; var y = (bounds.Height-size)/2;
            canvas.StrokeSize = 28; var angle = 90f;
            for (var i=0;i<3;i++) {
                var next = angle - (float)(percentages[i]*3.6);
                if (percentages[i]>0) {
                    canvas.StrokeColor = colors[i];
                    if (percentages[i] >= 100 - 1e-9) canvas.DrawEllipse(x,y,size,size);
                    else canvas.DrawArc(x,y,size,size,angle,next,true,false);
                }
                angle = next;
            }
        }
    }
}
