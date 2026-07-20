using System.Windows.Input;
using Avalonia.Media;
using ContextSwitcher.Core.Configuration;

namespace ContextSwitcher.App.ViewModels;

/// <summary>
/// One row on the Profiles page. Clicking the row activates the profile; Edit, Duplicate, and
/// Delete are kept as separate actions so "switch to this" and "edit this" can never be
/// triggered by the same click (agent.md section 11.1.2). Delete requires a second click to
/// confirm rather than a modal dialog.
/// </summary>
public sealed class ProfileRowViewModel : ViewModelBase
{
    private bool isConfirmingDelete;

    public ProfileRowViewModel(
        ContextDefinition context,
        bool isActive,
        Func<ContextDefinition, Task> activate,
        Action<ContextDefinition> edit,
        Func<ContextDefinition, Task> duplicate,
        Func<ContextDefinition, Task> delete)
    {
        this.Context = context;
        this.IsActive = isActive;
        this.AccentBrush = AccentColorParser.ToBrush(context.AccentColor);

        this.ActivateCommand = new AsyncRelayCommand(() => activate(context), () => !this.IsActive);
        this.EditCommand = new RelayCommand(() => edit(context));
        this.DuplicateCommand = new AsyncRelayCommand(() => duplicate(context));
        this.DeleteCommand = new RelayCommand(() =>
        {
            if (this.IsConfirmingDelete)
            {
                this.IsConfirmingDelete = false;
                _ = delete(context);
            }
            else
            {
                this.IsConfirmingDelete = true;
            }
        });
        this.CancelDeleteCommand = new RelayCommand(() => this.IsConfirmingDelete = false);
    }

    public ContextDefinition Context { get; }

    public string DisplayName => this.Context.DisplayName;

    public IBrush AccentBrush { get; }

    public bool IsActive { get; }

    public bool IsConfirmingDelete
    {
        get => this.isConfirmingDelete;
        private set => this.SetProperty(ref this.isConfirmingDelete, value);
    }

    public ICommand ActivateCommand { get; }

    public ICommand EditCommand { get; }

    public ICommand DuplicateCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand CancelDeleteCommand { get; }
}
