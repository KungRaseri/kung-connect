using CommunityToolkit.Mvvm.ComponentModel;

namespace KungConnect.Client.ViewModels;

/// <summary>
/// Transitional view model shown while an async URI-launch is initializing
/// (connecting to SignalR, fetching machine info, etc.).
/// </summary>
public sealed partial class LoadingViewModel(string message) : ViewModelBase
{
    public string Message { get; } = message;
}
