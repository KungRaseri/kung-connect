using CommunityToolkit.Mvvm.ComponentModel;

namespace KungConnect.Client.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _statusMessage;

    protected void ClearMessages()
    {
        ErrorMessage = null;
        StatusMessage = null;
    }
}

