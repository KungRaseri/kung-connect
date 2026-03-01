// KungConnect Join-Code Browser Mini-Agent
// Provides screen capture and WebRTC plumbing for agentless sessions.

window.KungConnectJoin = (() => {
    let peerConnection = null;
    let dataChannel = null;
    let mediaStream = null;
    let dotnetRef = null;

    /** Request screen capture permission and return dimensions. */
    async function startCapture(dotNetObjRef) {
        dotnetRef = dotNetObjRef;
        try {
            mediaStream = await navigator.mediaDevices.getDisplayMedia({
                video: { frameRate: 30, cursor: "always" },
                audio: false
            });
            mediaStream.getVideoTracks()[0].onended = () => {
                dotnetRef.invokeMethodAsync("OnCaptureStopped");
            };
            const track = mediaStream.getVideoTracks()[0];
            const settings = track.getSettings();
            return { width: settings.width ?? 1280, height: settings.height ?? 720 };
        } catch (err) {
            console.error("getDisplayMedia failed:", err);
            throw err;
        }
    }

    /** Initialise RTCPeerConnection with provided ICE servers and start video track. */
    async function initPeer(iceServersJson) {
        const iceServers = JSON.parse(iceServersJson);
        peerConnection = new RTCPeerConnection({ iceServers });

        // Attach local video track
        if (mediaStream) {
            for (const track of mediaStream.getTracks())
                peerConnection.addTrack(track, mediaStream);
        }

        // Input data channel (receive from operator)
        peerConnection.ondatachannel = (evt) => {
            if (evt.channel.label === "input") {
                dataChannel = evt.channel;
                dataChannel.onmessage = (e) => {
                    dotnetRef.invokeMethodAsync("OnInputEvent", e.data);
                };
            }
        };

        peerConnection.onicecandidate = (evt) => {
            if (evt.candidate)
                dotnetRef.invokeMethodAsync("OnIceCandidate", JSON.stringify(evt.candidate));
        };

        peerConnection.onconnectionstatechange = () => {
            dotnetRef.invokeMethodAsync("OnConnectionStateChange", peerConnection.connectionState);
        };
    }

    /** Create an SDP offer and return it as a string. */
    async function createOffer() {
        const offer = await peerConnection.createOffer();
        await peerConnection.setLocalDescription(offer);
        return offer.sdp;
    }

    /** Apply remote SDP answer from operator. */
    async function setAnswer(sdp) {
        await peerConnection.setRemoteDescription({ type: "answer", sdp });
    }

    /** Add a trickled ICE candidate from the operator. */
    async function addIceCandidate(candidateJson) {
        const candidate = JSON.parse(candidateJson);
        await peerConnection.addIceCandidate(candidate);
    }

    /** Tear down everything. */
    function cleanup() {
        peerConnection?.close();
        peerConnection = null;
        mediaStream?.getTracks().forEach(t => t.stop());
        mediaStream = null;
        dataChannel = null;
        dotnetRef = null;
    }

    return { startCapture, initPeer, createOffer, setAnswer, addIceCandidate, cleanup };
})();
