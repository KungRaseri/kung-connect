namespace KungConnect.Shared.Signaling;

/// <summary>
/// Base envelope for all real-time signaling messages exchanged over
/// the SignalR hub between Client ↔ Server ↔ Agent.
/// </summary>
public class SignalMessage
{
    /// <summary>Type discriminator: "offer" | "answer" | "ice" | "session-state"</summary>
    public string Type { get; set; } = string.Empty;
    public Guid SessionId { get; set; }
    public string? Payload { get; set; }
}

/// <summary>WebRTC SDP offer or answer.</summary>
public class SdpMessage : SignalMessage
{
    /// <summary>"offer" or "answer"</summary>
    public string Sdp { get; set; } = string.Empty;
    /// <summary>Connection ID of the sender (filled in by SignalingService on receive).</summary>
    public string SenderConnectionId { get; set; } = string.Empty;
}

/// <summary>WebRTC ICE candidate trickle message.</summary>
public class IceCandidateMessage : SignalMessage
{
    public string Candidate { get; set; } = string.Empty;
    public string SdpMid { get; set; } = string.Empty;
    public int? SdpMLineIndex { get; set; }
}

/// <summary>Notification that the remote session state has changed.</summary>
public class SessionStateMessage : SignalMessage
{
    public string State { get; set; } = string.Empty; // matches SessionState enum
}

/// <summary>Input event forwarded from Client → Agent via data channel (JSON).</summary>
public class InputEvent
{
    /// <summary>"mouse-move" | "mouse-down" | "mouse-up" | "key-down" | "key-up" | "scroll" | "clipboard"</summary>
    public string EventType { get; set; } = string.Empty;
    public int? X { get; set; }
    public int? Y { get; set; }
    public int? DeltaX { get; set; }
    public int? DeltaY { get; set; }
    public int? Button { get; set; }   // MouseButton enum value
    public int? KeyCode { get; set; }
    public bool? Ctrl { get; set; }
    public bool? Shift { get; set; }
    public bool? Alt { get; set; }
    public bool? Meta { get; set; }
    public string? Text { get; set; }  // For clipboard
}
