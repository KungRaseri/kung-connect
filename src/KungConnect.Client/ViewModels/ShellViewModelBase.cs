using CommunityToolkit.Mvvm.ComponentModel;

namespace KungConnect.Client.ViewModels;

/// <summary>
/// Base class for shell view-models that own a <see cref="CurrentPage"/> navigation slot.
/// Both <see cref="MainWindowViewModel"/> (normal startup) and
/// <see cref="SetupShellViewModel"/> (first-run setup) extend this so that
/// <see cref="Views.MainWindow"/> can be data-typed to a single stable type.
/// </summary>
public abstract partial class ShellViewModelBase : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase _currentPage = null!;
}
