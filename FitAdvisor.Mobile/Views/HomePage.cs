using FitAdvisor.Mobile.Services;

namespace FitAdvisor.Mobile.Views;

public sealed class HomePage : ContentPage
{
    private static readonly string[] Activities = ["running.png", "gym_training.png", "muay_thai.png", "cycling.png", "hiking.png", "programming.png", "music.png", "pull_ups.png"];
    private readonly Image character = DesignComponents.Decoration(Activities[0], 64);
    private readonly Label message = new() { FontSize = 14 };
    private readonly Border messageCard;
    private readonly IDispatcherTimer timer;
    private bool active, transitioning;
    private int activity, generation;

    public HomePage()
    {
        Title = "Home";
        var menu = DesignComponents.Secondary("Menu");
        menu.AutomationId = "HomeMenu";
        menu.Clicked += (_, _) => Shell.Current.FlyoutIsPresented = true;
        menu.VerticalOptions = LayoutOptions.Start;
        var header = new Grid { ColumnDefinitions = new ColumnDefinitionCollection {
            new(GridLength.Star), new(new GridLength(64)), new(GridLength.Auto) }, RowDefinitions = new RowDefinitionCollection {
                new(GridLength.Auto), new(GridLength.Auto) }, ColumnSpacing = 12, RowSpacing = 8 };
        var brand = new Label { Text = "FitAdvisor", FontSize = 28, FontAttributes = FontAttributes.Bold };
        header.Add(brand); Grid.SetColumnSpan(brand, 3);
        var subtitle = DesignComponents.Caption("At your pace"); subtitle.VerticalOptions = LayoutOptions.Center;
        header.Add(subtitle, 0, 1); header.Add(character, 1, 1); header.Add(menu, 2, 1);
        messageCard = DesignComponents.Card(new VerticalStackLayout { Spacing = 4, Children = {
            new Label { Text = "Today's encouragement", FontSize = 14, FontAttributes = FontAttributes.Bold }, message } }, 14);
        var stack = new VerticalStackLayout { Padding = 20, Spacing = 12, Children = {
            header, messageCard } };
        AddDestination(stack, "Dashboard", "Today's target and logged meals", "today", "HomeDashboard");
        AddDestination(stack, "Food Diary", "Log meals and review your food diary", "meals", "HomeFood");
        AddDestination(stack, "Weight", "Measurements and history", "weight", "HomeWeight");
        AddDestination(stack, "Workouts", "Running and gym sessions", "workouts", "HomeWorkouts");
        AddDestination(stack, "Health Connect Import", "Review dates and import exercise sessions", "workouts", "HomeHealth", true);
        AddDestination(stack, "Backup / Restore", "Encrypted backup and local restore", "data", "HomeBackup");
        AddDestination(stack, "Settings", "Theme, message language and animation", "settings", "HomeSettings");
        stack.Add(DesignComponents.Caption("On this device · decorative artwork does not detect your activity"));
        Content = new ScrollView { Content = stack };
        timer = Dispatcher.CreateTimer(); timer.Interval = TimeSpan.FromSeconds(6);
        timer.Tick += async (_, _) => await TickAsync();
    }

    private static void AddDestination(VerticalStackLayout stack, string title, string description, string route, string id, bool import = false)
    {
        var button = DesignComponents.Secondary("Open");
        button.Padding = new Thickness(4, 12);
        button.MinimumWidthRequest = 52;
        button.VerticalOptions = LayoutOptions.Start;
        SemanticProperties.SetDescription(button, $"Open {title}");
        button.AutomationId = id;
        button.Clicked += async (_, _) => {
            if (import && Shell.Current is AppShell shell) shell.RequestHealthImport();
            await Shell.Current.GoToAsync($"//main/{route}/{route}Page");
        };
        var heading = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 4 };
        heading.Add(new Label { Text = title, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center });
        heading.Add(button, 1);
        stack.Add(DesignComponents.Card(new VerticalStackLayout { Spacing = 4, Children = {
            heading, DesignComponents.Caption(description) } }, 10));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing(); active = true; RefreshPreferences(); timer.Start();
    }
    protected override void OnDisappearing()
    {
        Suspend(); base.OnDisappearing();
    }
    public void Suspend()
    {
        active = false; generation++; timer.Stop(); character.CancelAnimations(); character.Opacity = 1;
    }
    public void Resume()
    {
        if (Shell.Current?.CurrentPage != this) return;
        active = true; RefreshPreferences(); timer.Start();
    }
    private void RefreshPreferences()
    {
        messageCard.IsVisible = AppearancePreferences.ShowMessage;
        if (messageCard.IsVisible) message.Text = AppearancePreferences.MessageForToday();
        if (!AppearancePreferences.CanAnimate)
        {
            character.CancelAnimations(); character.Opacity = 1; activity = 0; character.Source = Activities[0];
        }
    }
    private async Task TickAsync()
    {
        if (!active) return;
        RefreshPreferences();
        if (!AppearancePreferences.CanAnimate || transitioning) return;
        transitioning = true; var token = generation;
        try
        {
            await character.FadeToAsync(0, 220, Easing.SinInOut);
            if (!active || token != generation || !AppearancePreferences.CanAnimate) return;
            activity = (activity + 1) % Activities.Length; character.Source = Activities[activity];
            await character.FadeToAsync(1, 220, Easing.SinInOut);
        }
        finally { character.Opacity = 1; transitioning = false; }
    }
}
