using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KungConnect.Client.Views;

public partial class MachineListView : UserControl
{
    public MachineListView()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
