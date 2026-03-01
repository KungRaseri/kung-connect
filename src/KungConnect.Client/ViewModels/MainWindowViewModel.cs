using CommunityToolkit.Mvvm.ComponentModel;
using KungConnect.Client.Services;
using KungConnect.Shared.DTOs.Machines;

namespace KungConnect.Client.ViewModels;

/// <summary>
/// Shell view-model that owns navigation between Login → MachineList → Session.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAuthService _auth;
    private readonly IMachineService _machines;
    private readonly ISessionService _sessions;
    private readonly ISignalingService _signaling;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    public MainWindowViewModel(
        IAuthService auth,
        IMachineService machines,
        ISessionService sessions,
        ISignalingService signaling)
    {
        _auth     = auth;
        _machines = machines;
        _sessions = sessions;
        _signaling = signaling;

        var login = new LoginViewModel(auth);
        login.LoginSucceeded += OnLoginSucceeded;
        _currentPage = login;
    }

    private async void OnLoginSucceeded(object? sender, EventArgs e)
    {
        // Connect SignalR hub with the fresh access token
        await _signaling.ConnectAsync(_auth.AccessToken!);

        var list = new MachineListViewModel(_auth, _machines, _signaling);
        list.ConnectRequested += OnConnectRequested;
        CurrentPage = list;

        // Kick off initial load
        await list.RefreshCommand.ExecuteAsync(null);
    }

    private void OnConnectRequested(object? sender, MachineDto machine)
    {
        var session = new SessionViewModel(_signaling, _sessions, machine);
        session.SessionEnded += OnSessionEnded;
        CurrentPage = session;
        _ = session.StartAsync();
    }

    private async void OnSessionEnded(object? sender, EventArgs e)
    {
        if (sender is SessionViewModel vm)
            await vm.DisposeAsync();

        var list = new MachineListViewModel(_auth, _machines, _signaling);
        list.ConnectRequested += OnConnectRequested;
        CurrentPage = list;
        await list.RefreshCommand.ExecuteAsync(null);
    }
}
