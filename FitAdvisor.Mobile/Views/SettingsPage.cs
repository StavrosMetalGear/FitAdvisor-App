using FitAdvisor.Mobile.Services;

namespace FitAdvisor.Mobile.Views;

public sealed class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        Title = "Settings";
        var theme = new Picker { Title = "Appearance", ItemsSource = new[] { "Follow device", "Light", "Dark" }, SelectedIndex = AppearancePreferences.Theme };
        theme.SelectedIndexChanged += (_, _) => { if (theme.SelectedIndex >= 0) AppearancePreferences.Theme = theme.SelectedIndex; };
        var language = new Picker { Title = "Food, macro chart and message language", ItemsSource = new[] { "English", "Ελληνικά" }, SelectedIndex = AppearancePreferences.Language };
        language.SelectedIndexChanged += (_, _) => { if (language.SelectedIndex >= 0) AppearancePreferences.Language = language.SelectedIndex; };
        var visible = new Switch { IsToggled = AppearancePreferences.ShowMessage, MinimumHeightRequest = 48, MinimumWidthRequest = 48 };
        visible.Toggled += (_, e) => AppearancePreferences.ShowMessage = e.Value;
        var animation = new Switch { IsToggled = AppearancePreferences.Animate, MinimumHeightRequest = 48, MinimumWidthRequest = 48 };
        animation.Toggled += (_, e) => AppearancePreferences.Animate = e.Value;
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 20, Spacing = 16, Children = {
            new Label { Text = "Make it feel like yours", FontSize = 26, FontAttributes = FontAttributes.Bold },
            DesignComponents.Card(new VerticalStackLayout { Spacing = 8, Children = {
                new Label { Text = "Theme", FontAttributes = FontAttributes.Bold }, theme } }),
            DesignComponents.Card(new VerticalStackLayout { Spacing = 8, Children = {
                ToggleRow("Show daily message", visible), new Label { Text = "Food, macro chart and message language" }, language,
                DesignComponents.Caption("Original messages saved locally. The same message pair stays for the local calendar day; switching language shows its translation. No alerts or connection needed.") } }),
            DesignComponents.Card(new VerticalStackLayout { Spacing = 8, Children = {
                ToggleRow("Animate Home art", animation),
                DesignComponents.Caption("One decorative activity at a time. Pauses away from Home and respects the device's Remove animations setting. When disabled, a static runner is shown.") } }),
            DesignComponents.Caption("Preferences are saved on this device separately from your diary and encrypted diary backups.") } } };
    }
    private static Grid ToggleRow(string text, Switch control)
    {
        SemanticProperties.SetDescription(control, text);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 12 };
        row.Add(new Label { Text = text, VerticalOptions = LayoutOptions.Center }); row.Add(control, 1); return row;
    }
}
