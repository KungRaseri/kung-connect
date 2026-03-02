using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using KungConnect.Client.Models;
using KungConnect.Client.Services;
using KungConnect.Client.ViewModels;
using KungConnect.Client.Views;
using Microsoft.Extensions.DependencyInjection;

namespace KungConnect.Client;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Parse the kungconnect:// URI from the command-line args (if any)
        var args = (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Args ?? [];
        var launch = LaunchContext.Parse(args);

        // Server URL: URI launch takes priority, then env var, then localhost default
        var serverUrl = launch.ServerUrl
            ?? System.Environment.GetEnvironmentVariable("KUNGCONNECT_SERVER_URL")
            ?? "http://localhost:5000";

        _services = BuildServices(serverUrl, launch);

        // Register the URI scheme with the OS so future browser launches work
        UriSchemeRegistrar.EnsureRegistered();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<MainWindowViewModel>()
            };

            desktop.Exit += (_, _) => _services.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
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
