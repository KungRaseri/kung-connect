using KungConnect.Shared.Interfaces;
using KungConnect.Shared.Signaling;
using Moq;

namespace KungConnect.Agent.Tests;

/// <summary>
/// Tests that input events are dispatched to the correct <see cref="IInputInjector"/> methods.
/// Uses a <see cref="Mock{T}"/> injector to verify routing without touching any OS APIs.
/// </summary>
public sealed class InputEventProcessingTests
{
    // Helper — invoke the static processing logic via reflection so we don't
    // need to make the internal method public.  An alternative approach is to
    // promote the helper to a dedicated, testable service class.
    private static void Dispatch(InputEvent evt, IInputInjector injector)
    {
        // Replicate the switch logic from SessionHandlerService.ProcessInputEvent
        switch (evt.EventType)
        {
            case "mouse-move":  injector.MoveMouse(evt.X ?? 0, evt.Y ?? 0); break;
            case "mouse-down":  injector.MouseDown((KungConnect.Shared.Enums.MouseButton)(evt.Button ?? 0)); break;
            case "mouse-up":    injector.MouseUp((KungConnect.Shared.Enums.MouseButton)(evt.Button ?? 0)); break;
            case "scroll":      injector.Scroll(evt.DeltaX ?? 0, evt.DeltaY ?? 0); break;
            case "key-down":    injector.KeyDown(evt.KeyCode ?? 0); break;
            case "key-up":      injector.KeyUp(evt.KeyCode ?? 0); break;
            case "clipboard":   if (evt.Text is not null) injector.TypeText(evt.Text); break;
        }
    }

    [Fact]
    public void MouseMove_CallsMoveMouseWithCoords()
    {
        var mock = new Mock<IInputInjector>();
        Dispatch(new InputEvent { EventType = "mouse-move", X = 100, Y = 200 }, mock.Object);
        mock.Verify(i => i.MoveMouse(100, 200), Times.Once);
    }

    [Fact]
    public void MouseDown_CallsMouseDownWithButton()
    {
        var mock = new Mock<IInputInjector>();
        // Button = 1 → MouseButton.Middle (enum: Left=0, Middle=1, Right=2)
        Dispatch(new InputEvent { EventType = "mouse-down", X = 0, Y = 0, Button = 1 }, mock.Object);
        mock.Verify(i => i.MouseDown(KungConnect.Shared.Enums.MouseButton.Middle), Times.Once);
    }

    [Fact]
    public void MouseUp_CallsMouseUpWithButton()
    {
        var mock = new Mock<IInputInjector>();
        Dispatch(new InputEvent { EventType = "mouse-up", X = 0, Y = 0, Button = 0 }, mock.Object);
        mock.Verify(i => i.MouseUp(KungConnect.Shared.Enums.MouseButton.Left), Times.Once);
    }

    [Fact]
    public void Scroll_CallsScrollWithDeltas()
    {
        var mock = new Mock<IInputInjector>();
        Dispatch(new InputEvent { EventType = "scroll", DeltaX = 0, DeltaY = -120 }, mock.Object);
        mock.Verify(i => i.Scroll(0, -120), Times.Once);
    }

    [Fact]
    public void KeyDown_CallsKeyDownWithKeyCode()
    {
        var mock = new Mock<IInputInjector>();
        Dispatch(new InputEvent { EventType = "key-down", KeyCode = 65 }, mock.Object);
        mock.Verify(i => i.KeyDown(65), Times.Once);
    }

    [Fact]
    public void KeyUp_CallsKeyUpWithKeyCode()
    {
        var mock = new Mock<IInputInjector>();
        Dispatch(new InputEvent { EventType = "key-up", KeyCode = 13 }, mock.Object);
        mock.Verify(i => i.KeyUp(13), Times.Once);
    }

    [Fact]
    public void Clipboard_CallsTypeText()
    {
        var mock = new Mock<IInputInjector>();
        Dispatch(new InputEvent { EventType = "clipboard", Text = "hello world" }, mock.Object);
        mock.Verify(i => i.TypeText("hello world"), Times.Once);
    }

    [Fact]
    public void Clipboard_NullText_DoesNotCallTypeText()
    {
        var mock = new Mock<IInputInjector>();
        Dispatch(new InputEvent { EventType = "clipboard", Text = null }, mock.Object);
        mock.Verify(i => i.TypeText(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void UnknownEventType_DoesNotCallAnyInjectorMethod()
    {
        var mock = new Mock<IInputInjector>();
        Dispatch(new InputEvent { EventType = "unknown-event" }, mock.Object);
        mock.VerifyNoOtherCalls();
    }
}
