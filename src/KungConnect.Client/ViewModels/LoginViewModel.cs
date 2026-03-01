using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KungConnect.Client.Services;
using KungConnect.Shared.DTOs.Auth;

namespace KungConnect.Client.ViewModels;

public partial class LoginViewModel(IAuthService authService) : ViewModelBase
{
    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    public event EventHandler? LoginSucceeded;

    [RelayCommand]
    private async Task LoginAsync()
    {
        ClearMessages();
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Username and password are required.";
            return;
        }

        IsBusy = true;
        try
        {
            await authService.LoginAsync(new LoginRequest(Username, Password));
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            Password = string.Empty;
        }
    }
}
