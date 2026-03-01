using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using KungConnect.Client.ViewModels;

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
    // Phase 2: these handlers will serialise InputEvent and send over the
    // RTCPeerConnection data channel. Stubbed here so the plumbing is in place.

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_vm is null) return;
        var pos = e.GetPosition(this);
        var nx = pos.X / Bounds.Width;
        var ny = pos.Y / Bounds.Height;
        // TODO Phase 2: signalingService.SendInputAsync(new InputEvent { EventType = InputEventType.MouseMove, X = nx, Y = ny });
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_vm is null) return;
        var point = e.GetCurrentPoint(this);
        var button = point.Properties.IsLeftButtonPressed ? KungConnect.Shared.Enums.MouseButton.Left
                   : point.Properties.IsRightButtonPressed ? KungConnect.Shared.Enums.MouseButton.Right
                   : KungConnect.Shared.Enums.MouseButton.Middle;
        // TODO Phase 2: send MouseDown event
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        // TODO Phase 2: send MouseUp event
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        // TODO Phase 2: send Scroll event (e.Delta.Y)
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // TODO Phase 2: send KeyDown event (e.Key)
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        // TODO Phase 2: send KeyUp event (e.Key)
    }
}
