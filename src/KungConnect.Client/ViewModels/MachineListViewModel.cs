using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KungConnect.Client.Services;
using KungConnect.Shared.DTOs.Machines;

namespace KungConnect.Client.ViewModels;

public partial class MachineListViewModel(
    IAuthService _authService,
    IMachineService machineService,
    ISignalingService _signalingService) : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<MachineDto> _machines = [];

    [ObservableProperty]
    private MachineDto? _selectedMachine;

    public event EventHandler<MachineDto>? ConnectRequested;

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        ClearMessages();
        try
        {
            var list = await machineService.GetMachinesAsync();
            Machines = new ObservableCollection<MachineDto>(list);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load machines: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void Connect(MachineDto machine)
    {
        SelectedMachine = machine;
        ConnectRequested?.Invoke(this, machine);
    }

    [RelayCommand]
    private async Task ConnectWithCodeAsync()
    {
        // Opens the join-code entry dialog
        // TODO: show InputDialog, then call signalingService.ConnectWithCodeAsync(code)
        StatusMessage = "Enter a join code to connect.";
        await Task.CompletedTask;
    }
}
