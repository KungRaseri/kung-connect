namespace KungConnect.Client.ViewModels;

/// <summary>
/// Thin shell used on first run: hosts <see cref="ServerSetupViewModel"/> as the
/// <see cref="ShellViewModelBase.CurrentPage"/> until the user confirms the server URL,
/// after which <see cref="App"/> rebuilds the real <see cref="MainWindowViewModel"/>
/// and swaps the window's DataContext.
/// </summary>
public sealed class SetupShellViewModel : ShellViewModelBase
{
    public SetupShellViewModel(ServerSetupViewModel setupVm)
    {
        CurrentPage = setupVm;
    }
}
