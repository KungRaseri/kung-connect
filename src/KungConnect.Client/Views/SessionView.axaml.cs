using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using KungConnect.Client.ViewModels;
using KungConnect.Shared.Signaling;

namespace KungConnect.Client.Views;

public partial class SessionView : UserControl
{
    private SessionViewModel? _vm;

    public SessionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _vm = DataContext as SessionViewModel;
    }

    // ── Mouse input forwarding ─────────────────────────────────────────────
    // Normalise position to 0-1 relative to the view bounds, then scale to
    // remote screen dimensions inside SessionViewModel.SendInput().

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_vm is null) return;
        var pos = e.GetPosition(this);
        var x = (int)(pos.X / Bounds.Width  * _vm.RemoteWidth);
        var y = (int)(pos.Y / Bounds.Height * _vm.RemoteHeight);
        _vm.SendInput(new InputEvent { EventType = "mouse-move", X = x, Y = y });
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_vm is null) return;
        var point  = e.GetCurrentPoint(this);
        var button = point.Properties.IsLeftButtonPressed   ? KungConnect.Shared.Enums.MouseButton.Left
                   : point.Properties.IsRightButtonPressed  ? KungConnect.Shared.Enums.MouseButton.Right
                   : KungConnect.Shared.Enums.MouseButton.Middle;
        var pos = point.Position;
        var x = (int)(pos.X / Bounds.Width  * _vm.RemoteWidth);
        var y = (int)(pos.Y / Bounds.Height * _vm.RemoteHeight);
        _vm.SendInput(new InputEvent { EventType = "mouse-down", X = x, Y = y, Button = (int)button });
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_vm is null) return;
        var point  = e.GetCurrentPoint(this);
        var button = e.InitialPressMouseButton switch
        {
            MouseButton.Left   => KungConnect.Shared.Enums.MouseButton.Left,
            MouseButton.Right  => KungConnect.Shared.Enums.MouseButton.Right,
            _                  => KungConnect.Shared.Enums.MouseButton.Middle
        };
        var pos = point.Position;
        var x = (int)(pos.X / Bounds.Width  * _vm.RemoteWidth);
        var y = (int)(pos.Y / Bounds.Height * _vm.RemoteHeight);
        _vm.SendInput(new InputEvent { EventType = "mouse-up", X = x, Y = y, Button = (int)button });
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_vm is null) return;
        _vm.SendInput(new InputEvent
        {
            EventType = "scroll",
            DeltaX    = (int)(e.Delta.X * 120),
            DeltaY    = (int)(e.Delta.Y * 120)
        });
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_vm is null) return;
        // Avalonia's Key enum values align with Win32 VK codes for standard keys
        _vm.SendInput(new InputEvent
        {
            EventType = "key-down",
            KeyCode   = (int)e.Key,
            Ctrl      = e.KeyModifiers.HasFlag(KeyModifiers.Control),
            Shift     = e.KeyModifiers.HasFlag(KeyModifiers.Shift),
            Alt       = e.KeyModifiers.HasFlag(KeyModifiers.Alt),
            Meta      = e.KeyModifiers.HasFlag(KeyModifiers.Meta)
        });
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (_vm is null) return;
        _vm.SendInput(new InputEvent
        {
            EventType = "key-up",
            KeyCode   = (int)e.Key,
            Ctrl      = e.KeyModifiers.HasFlag(KeyModifiers.Control),
            Shift     = e.KeyModifiers.HasFlag(KeyModifiers.Shift),
            Alt       = e.KeyModifiers.HasFlag(KeyModifiers.Alt),
            Meta      = e.KeyModifiers.HasFlag(KeyModifiers.Meta)
        });
    }
}

