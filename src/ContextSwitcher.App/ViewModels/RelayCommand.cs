using System.Windows.Input;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// A minimal synchronous <see cref="ICommand"/>, avoiding a dependency on an MVVM toolkit for one window.
/// </summary>
public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => execute();

    public void RaiseCanExecuteChanged() => this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// A minimal <see cref="ICommand"/> for an async operation that guards against re-entrancy while running.
/// </summary>
public sealed class AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) : ICommand
{
    private bool isExecuting;

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !this.isExecuting && (canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        this.isExecuting = true;
        this.RaiseCanExecuteChanged();
        try
        {
            await execute().ConfigureAwait(true);
        }
        finally
        {
            this.isExecuting = false;
            this.RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged() => this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
