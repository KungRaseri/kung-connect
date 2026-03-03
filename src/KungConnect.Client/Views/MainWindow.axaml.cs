using Avalonia.Controls;

namespace KungConnect.Client.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // When the user clicks X, hide to system tray instead of closing.
        // The app is only fully exited via the tray menu "Quit" → Shutdown().
        // ApplicationShutdown close reason means Shutdown() was already called, so allow it.
        if (e.CloseReason != WindowCloseReason.ApplicationShutdown)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }
}