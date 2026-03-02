using CommunityToolkit.Mvvm.ComponentModel;
using KungConnect.Client.Models;
using KungConnect.Client.Services;
using KungConnect.Shared.DTOs.Machines;
using KungConnect.Shared.Enums;

namespace KungConnect.Client.ViewModels;

/// <summary>
/// Shell view-model that owns navigation between Login → MachineList → Session.
/// When the app is launched via a <c>kungconnect://</c> URI, the login screen is
/// skipped and the appropriate view is shown immediately.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAuthService _auth;
    private readonly IMachineService _machines;
    private readonly ISessionService _sessions;
    private readonly ISignalingService _signaling;
    private readonly LaunchContext _launch;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    public MainWindowViewModel(
        IAuthService auth,
        IMachineService machines,
        ISessionService sessions,
        ISignalingService signaling,
        LaunchContext launch)
    {
        _auth      = auth;
        _machines  = machines;
        _sessions  = sessions;
        _signaling = signaling;
        _launch    = launch;

        if (_launch.Mode == LaunchMode.Normal)
        {
            // Normal startup — show login
            var login = new LoginViewModel(auth);
            login.LoginSucceeded += OnLoginSucceeded;
            _currentPage = login;
        }
        else
        {
            // URI launch — show a loading spinner while we connect
            _currentPage = new LoadingViewModel("Connecting…");
            _ = HandleLaunchAsync();
        }
    }

    // ── URI-launch flow ───────────────────────────────────────────────────────

    private async Task HandleLaunchAsync()
    {
        var loading = (LoadingViewModel)CurrentPage;
        try
        {
            switch (_launch.Mode)
            {
                case LaunchMode.Session:
                    await HandleSessionLaunchAsync();
                    break;
                case LaunchMode.AdHoc:
                    await HandleAdHocLaunchAsync();
                    break;
                case LaunchMode.Join:
                    await HandleJoinLaunchAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            loading.ErrorMessage = $"Launch failed: {ex.Message}";
        }
    }

    /// <summary>Operator launched with a specific registered machine.</summary>
    private async Task HandleSessionLaunchAsync()
    {
        _auth.SetTokens(_launch.AccessToken!);
        await _signaling.ConnectAsync(_launch.AccessToken!);

        var machine = _launch.MachineId is { } mid
            ? await _machines.GetMachineAsync(mid)
            : null;

        if (machine is null)
        {
            ((LoadingViewModel)CurrentPage).ErrorMessage = "Machine not found.";
            return;
        }

        var session = new SessionViewModel(_signaling, _sessions, machine);
        session.SessionEnded += OnSessionEnded;
        CurrentPage = session;
        await session.StartAsync();
    }

    /// <summary>Operator launched for an ad-hoc customer session.</summary>
    private async Task HandleAdHocLaunchAsync()
    {
        _auth.SetTokens(_launch.AccessToken!);
        await _signaling.ConnectAsync(_launch.AccessToken!);

        // Create a placeholder machine DTO for display — there is no registered machine
        var placeholder = new MachineDto(
            Guid.Empty, "Ad Hoc Session", "", OsType.Windows,
            MachineStatus.Online, "", DateTimeOffset.UtcNow);

        var session = new SessionViewModel(_signaling, _sessions, placeholder);
        session.SessionEnded += OnSessionEnded;
        CurrentPage = session;
        await session.StartAdHocAsync(_launch.TargetConnectionId!);
    }

    /// <summary>Customer launched to join a support session.</summary>
    private async Task HandleJoinLaunchAsync()
    {
        var vm = new CustomerSessionViewModel(_launch.JoinCode ?? "", _signaling);
        vm.SessionEnded += (_, _) => ShowLogin();
        CurrentPage = vm;
        await vm.ConnectAsync();
    }

    // ── Normal login flow ─────────────────────────────────────────────────────

    private async void OnLoginSucceeded(object? sender, EventArgs e)
    {
        await _signaling.ConnectAsync(_auth.AccessToken!);

        var list = new MachineListViewModel(_auth, _machines, _signaling);
        list.ConnectRequested += OnConnectRequested;
        CurrentPage = list;

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

    private void ShowLogin()
    {
        var login = new LoginViewModel(_auth);
        login.LoginSucceeded += OnLoginSucceeded;
        CurrentPage = login;
    }
}
