using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace KungConnect.Client.ViewModels;

/// <summary>
/// First-run prompt: asks the user for the KungConnect server URL, verifies reachability
/// by hitting <c>/api/setup/status</c>, then fires <see cref="Confirmed"/> with the URL.
/// </summary>
public partial class ServerSetupViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _serverUrl = string.Empty;

    /// <summary>Raised when the user has confirmed a reachable server URL.</summary>
    public event Action<string>? Confirmed;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync()
    {
        ClearMessages();

        var url = ServerUrl.Trim().TrimEnd('/');

        IsBusy = true;
        StatusMessage = "Testing connection…";
        try
        {
            using var http = new System.Net.Http.HttpClient
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
            var response = await http.GetAsync($"{url}/api/setup/status");
            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = $"Server returned {(int)response.StatusCode} — check the URL.";
                return;
            }
            Confirmed?.Invoke(url);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not reach server: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            StatusMessage = null;
        }
    }

    private bool CanConfirm() =>
        !string.IsNullOrWhiteSpace(ServerUrl) &&
        (ServerUrl.StartsWith("http://",  StringComparison.OrdinalIgnoreCase) ||
         ServerUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
}
