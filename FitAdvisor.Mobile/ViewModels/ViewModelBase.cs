using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using FitAdvisor.Core;

namespace FitAdvisor.Mobile.ViewModels;
public interface IUserDialogs { Task<bool> ConfirmDeleteAsync(string description); }
public sealed class MauiUserDialogs : IUserDialogs
{
    public Task<bool> ConfirmDeleteAsync(string description) =>
        Application.Current!.Windows[0].Page!.DisplayAlertAsync("Delete entry?", description + " This cannot be undone.", "Delete", "Cancel");
}
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private bool busy;
    private string status = "";
    private readonly List<Command> commands = new();
    public bool IsBusy { get => busy; private set { Set(ref busy, value); foreach (var command in commands) command.ChangeCanExecute(); } }
    public string Status { get => status; protected set => Set(ref status, value); }
    protected bool Set<T>(ref T storage, T value, [CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value; PropertyChanged?.Invoke(this, new(property)); return true;
    }
    protected void Notify(string property) => PropertyChanged?.Invoke(this, new(property));
    protected Command Action(Func<Task> action)
    {
        var command = new Command(async () => await Guard(action), () => !IsBusy); commands.Add(command); return command;
    }
    protected Command ItemAction<T>(Func<T, Task> action) where T : class
    {
        var command = new Command(async value => { if (value is T item) await Guard(() => action(item)); }, _ => !IsBusy);
        commands.Add(command); return command;
    }
    public async Task Guard(Func<Task> action)
    {
        if (IsBusy) return;
        IsBusy = true; Status = "";
        try { await action(); }
        catch (ArgumentException error) { Status = error.Message; }
        catch (Exception) { Status = "Unable to read or save local data. Check free storage and retry."; }
        finally { IsBusy = false; }
    }
    protected static double Number(string text, string label)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) || !double.IsFinite(value))
            throw new ArgumentException($"Enter a valid {label} using your phone's decimal separator.");
        return value;
    }
    protected static double? OptionalNumber(string text, string label) => string.IsNullOrWhiteSpace(text) ? null : Number(text, label);
    protected static string Display(double value) => value.ToString("0.###", CultureInfo.CurrentCulture);
    protected static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}
