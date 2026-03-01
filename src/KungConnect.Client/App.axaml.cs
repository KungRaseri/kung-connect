using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
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
        _services = BuildServices();

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

    private static ServiceProvider BuildServices()
    {
        var sc = new ServiceCollection();

        // HTTP client - base address is read from env var or falls back to localhost
        var serverUrl = System.Environment.GetEnvironmentVariable("KUNGCONNECT_SERVER_URL")
                        ?? "http://localhost:5000";

        sc.AddHttpClient("KungConnect", c => c.BaseAddress = new System.Uri(serverUrl));

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
