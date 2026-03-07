using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;

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

    // ── Update notification banner ────────────────────────────────────────────

    /// <summary>Non-null when a newer release is available; drives the banner's visibility.</summary>
    [ObservableProperty]
    private string? _updateVersion;

    [ObservableProperty]
    private string? _updateUrl;

    /// <summary>
    /// True when the pending update is marked [REQUIRED] and the user is on a URI launch.
    /// Set to true to block session start and show a forced-update prompt instead.
    /// </summary>
    [ObservableProperty]
    private bool _updateIsRequired;

    [RelayCommand]
    private void OpenUpdate()
    {
        if (_updateUrl is null) return;
        Process.Start(new ProcessStartInfo(_updateUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private void DismissUpdate() => UpdateVersion = null;
}
