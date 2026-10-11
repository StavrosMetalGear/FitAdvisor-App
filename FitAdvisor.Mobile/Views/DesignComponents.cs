namespace FitAdvisor.Mobile.Views;

internal static class DesignComponents
{
    public static void Theme(BindableObject view, BindableProperty property, string light, string dark) =>
        view.SetAppThemeColor(property, Color.FromArgb(light), Color.FromArgb(dark));

    public static Border Card(View content, int padding = 16)
    {
        var card = new Border { Content = content, Padding = padding,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 } };
        Theme(card, VisualElement.BackgroundColorProperty, "#FFFFFF", "#1C2B24");
        card.SetAppTheme<Brush>(Border.StrokeProperty, new SolidColorBrush(Color.FromArgb("#CEDAD3")), new SolidColorBrush(Color.FromArgb("#42584B")));
        return card;
    }
    public static Label Caption(string text)
    {
        var label = new Label { Text = text, FontSize = 14 };
        Theme(label, Label.TextColorProperty, "#52665C", "#B4C8BC");
        return label;
    }
    public static Image Decoration(string source, double size = 48)
    {
        var image = new Image { Source = source, HeightRequest = size, WidthRequest = size,
            InputTransparent = true, Aspect = Aspect.AspectFit };
        AutomationProperties.SetIsInAccessibleTree(image, false);
        return image;
    }
    public static Button Secondary(string text)
    {
        var button = new Button { Text = text, FontSize = 16, Padding = new Thickness(16, 12),
            CornerRadius = 14, MinimumHeightRequest = 52 };
        Theme(button, VisualElement.BackgroundColorProperty, "#E2ECE6", "#2E4538");
        Theme(button, Button.TextColorProperty, "#145E50", "#EDF5F0");
        return button;
    }
}
