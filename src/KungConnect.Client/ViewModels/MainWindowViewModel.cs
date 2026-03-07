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
public partial class MainWindowViewModel : ShellViewModelBase
{
    private readonly IAuthService _auth;
    private readonly IMachineService _machines;
    private readonly ISessionService _sessions;
    private readonly ISignalingService _signaling;
    private readonly LaunchContext _launch;

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
            CurrentPage = login;
        }
        else
        {
            // URI launch — show a loading spinner while we connect
            CurrentPage = new LoadingViewModel("Connecting…");
            _ = HandleLaunchAsync();
        }

        // Check for a newer release in the background (non-blocking, fails silently)
        _ = CheckForClientUpdateAsync();
    }

    // ── URI-launch flow ───────────────────────────────────────────────────────

    private async Task HandleLaunchAsync()
    {
        var loading = (LoadingViewModel)CurrentPage;
        try
        {
            // Run update check concurrently with a 5-second cap so it never
            // meaningfully delays the launch on a fast connection.
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            var updateTask = ClientUpdateChecker.CheckAsync(cts.Token);

            switch (_launch.Mode)
            {
                case LaunchMode.Session:
                    // Await the update check before connecting — but only block
                    // if the release is marked [REQUIRED].  Otherwise continue
                    // regardless of the outcome (non-blocking).
                    var update = await updateTask.ConfigureAwait(false);
                    if (update is not null)
                    {
                        UpdateVersion    = update.Version;
                        UpdateUrl        = update.Url;
                        UpdateIsRequired = update.IsRequired;

                        if (update.IsRequired)
                        {
                            loading.ErrorMessage =
                                $"Update {update.Version} is required before connecting. " +
                                "Please download and install the latest version.";
                            return;   // ← block the session until updated
                        }
                    }
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
        if (string.IsNullOrEmpty(_launch.AccessToken))
            throw new InvalidOperationException("No access token in launch URI.");

        _auth.SetTokens(_launch.AccessToken);

        try { await _signaling.ConnectAsync(_launch.AccessToken); }
        catch (Exception ex) { throw new Exception($"SignalR connect failed: {ex.Message}", ex); }

        MachineDto? machine;
        try
        {
            machine = _launch.MachineId is { } mid
                ? await _machines.GetMachineAsync(mid)
                : null;
        }
        catch (Exception ex) { throw new Exception($"Get machine failed: {ex.Message}", ex); }

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

    // ── Update check ─────────────────────────────────────────────────────────

    private async Task CheckForClientUpdateAsync()
    {
        // URI-launch modes run the check inside HandleLaunchAsync (so it can
        // gate on IsRequired).  This path covers Normal (login screen) startup.
        if (_launch.Mode != LaunchMode.Normal) return;

        var update = await ClientUpdateChecker.CheckAsync();
        if (update is not null)
        {
            UpdateVersion    = update.Version;
            UpdateUrl        = update.Url;
            UpdateIsRequired = update.IsRequired;
        }
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
