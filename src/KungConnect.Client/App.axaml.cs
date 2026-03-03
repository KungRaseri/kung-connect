using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Input;
using KungConnect.Client.Models;
using KungConnect.Client.Services;
using KungConnect.Client.ViewModels;
using KungConnect.Client.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KungConnect.Client;

public partial class App : Application
{
    private ServiceProvider? _services;
    private MainWindow? _mainWindow;
    private IClassicDesktopStyleApplicationLifetime? _lifetime;

    public ICommand ShowWindowCommand { get; }
    public ICommand QuitCommand { get; }

    public App()
    {
        ShowWindowCommand = new RelayCommand(ShowWindow);
        QuitCommand       = new RelayCommand(Quit);
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        // DataContext = this lets TrayIcon menu bindings resolve against this App instance
        DataContext = this;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Parse the kungconnect:// URI from the command-line args (if any)
        var args = (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Args ?? [];
        var launch = LaunchContext.Parse(args);

        // Server URL priority: URI-launch arg > env var > saved settings file
        var serverUrl = launch.ServerUrl
            ?? System.Environment.GetEnvironmentVariable("KUNGCONNECT_SERVER_URL")
            ?? SettingsService.Load().ServerUrl;

        // Register the URI scheme with the OS so future browser launches work
        UriSchemeRegistrar.EnsureRegistered();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _lifetime = desktop;

            // Keep the app alive when the window is closed (minimize to tray)
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            DisableAvaloniaDataAnnotationValidation();

            if (serverUrl is null)
            {
                // First run — prompt for server URL before anything else
                var setupVm = new ServerSetupViewModel();
                setupVm.Confirmed += url =>
                {
                    SettingsService.Save(url);
                    LaunchWithUrl(url, launch, desktop);
                };

                _mainWindow = new MainWindow { DataContext = new SetupShellViewModel(setupVm) };
                desktop.MainWindow = _mainWindow;
                _mainWindow.Show();
            }
            else
            {
                LaunchWithUrl(serverUrl, launch, desktop);
            }

            desktop.Exit += (_, _) => _services?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    // ── Tray icon commands ────────────────────────────────────────────────────

    private void ShowWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void Quit() => _lifetime?.Shutdown();

    // ── Internal helpers ─────────────────────────────────────────────────────

    private void LaunchWithUrl(string serverUrl, LaunchContext launch, IClassicDesktopStyleApplicationLifetime desktop)
    {
        _services?.Dispose();
        _services = BuildServices(serverUrl, launch);
        var mainVm = _services.GetRequiredService<MainWindowViewModel>();

        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow { DataContext = mainVm };
            desktop.MainWindow = _mainWindow;
        }
        else
        {
            _mainWindow.DataContext = mainVm;
        }

        _mainWindow.Show();
    }

    private static ServiceProvider BuildServices(string serverUrl, LaunchContext launch)
    {
        var sc = new ServiceCollection();

        // HTTP client — base address resolved from launch URI or env var
        sc.AddHttpClient("KungConnect", c => c.BaseAddress = new System.Uri(serverUrl));

        // Make the launch context available for injection into MainWindowViewModel
        sc.AddSingleton(launch);

        // Services
        sc.AddSingleton<IAuthService, AuthService>();
        sc.AddSingleton<IMachineService, MachineService>();
        sc.AddSingleton<ISessionService, SessionService>();
        sc.AddSingleton<ISignalingService, SignalingService>();

        // Shell view-model
        sc.AddTransient<MainWindowViewModel>();

        return sc.BuildServiceProvider();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var toRemove = BindingPlugins.DataValidators
            .OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in toRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
