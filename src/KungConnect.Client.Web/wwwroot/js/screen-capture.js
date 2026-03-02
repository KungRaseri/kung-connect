// KungConnect Join-Code Browser Mini-Agent
// Provides screen capture and WebRTC plumbing for agentless sessions.
// Video is transported as JPEG frames over a "video" WebRTC data channel
// (opened by the operator) rather than a media track — this avoids the need
// for VP8/H264 encoding/decoding in the Avalonia desktop client.

window.KungConnectJoin = (() => {
    let peerConnection = null;
    let inputChannel = null;       // "input" dc — operator sends, we receive
    let mediaStream = null;
    let captureCanvas = null;
    let captureCtx = null;
    let captureVideo = null;
    let captureInterval = null;
    let dotnetRef = null;
    const CAPTURE_FPS = 10;
    const JPEG_QUALITY = 0.6;

    /** Request screen capture permission and prepare canvas. */
    async function startCapture(dotNetObjRef) {
        dotnetRef = dotNetObjRef;
        try {
            mediaStream = await navigator.mediaDevices.getDisplayMedia({
                video: { frameRate: 15, cursor: "always" },
                audio: false
            });

            const track = mediaStream.getVideoTracks()[0];
            const settings = track.getSettings();
            const width = settings.width ?? 1280;
            const height = settings.height ?? 720;

            // Hidden video element feeds the canvas
            captureVideo = document.createElement("video");
            captureVideo.srcObject = mediaStream;
            captureVideo.playsInline = true;
            await captureVideo.play();

            captureCanvas = document.createElement("canvas");
            captureCanvas.width = width;
            captureCanvas.height = height;
            captureCtx = captureCanvas.getContext("2d");

            track.onended = () => {
                stopVideoCapture();
                dotnetRef?.invokeMethodAsync("OnCaptureStopped");
            };

            return { width, height };
        } catch (err) {
            console.error("getDisplayMedia failed:", err);
            throw err;
        }
    }

    /** Initialise RTCPeerConnection. Data channels are OPENED BY THE OPERATOR in the offer. */
    async function initPeer(iceServersJson) {
        const iceServers = JSON.parse(iceServersJson);
        peerConnection = new RTCPeerConnection({ iceServers });

        // Operator creates "video" and "input" data channels in the offer.
        // We receive them here and wire them up.
        peerConnection.ondatachannel = (evt) => {
            const ch = evt.channel;
            if (ch.label === "video") {
                // When open, start pumping JPEG frames
                ch.onopen  = () => startVideoCapture(ch);
                ch.onclose = () => stopVideoCapture();
                ch.onerror = (e) => console.error("video dc error:", e);
            } else if (ch.label === "input") {
                inputChannel = ch;
                ch.onmessage = (e) => {
                    dotnetRef?.invokeMethodAsync("OnInputEvent", e.data);
                };
            }
        };

        peerConnection.onicecandidate = (evt) => {
            if (evt.candidate)
                dotnetRef?.invokeMethodAsync("OnIceCandidate", JSON.stringify(evt.candidate));
        };

        peerConnection.onconnectionstatechange = () => {
            dotnetRef?.invokeMethodAsync("OnConnectionStateChange", peerConnection.connectionState);
        };
    }

    /** Start capturing and sending JPEG frames on the "video" data channel. */
    function startVideoCapture(videoDc) {
        if (captureInterval) clearInterval(captureInterval);
        captureInterval = setInterval(() => {
            if (!captureCtx || !captureVideo || videoDc.readyState !== "open") return;
            try {
                captureCtx.drawImage(captureVideo, 0, 0);
                captureCanvas.toBlob(blob => {
                    if (!blob || videoDc.readyState !== "open") return;
                    blob.arrayBuffer().then(buf => {
                        if (videoDc.readyState === "open") videoDc.send(buf);
                    }).catch(() => {});
                }, "image/jpeg", JPEG_QUALITY);
            } catch (err) {
                // Stale frame — ignore
            }
        }, 1000 / CAPTURE_FPS);
    }

    /** Stop the capture interval without tearing down the peer. */
    function stopVideoCapture() {
        if (captureInterval) {
            clearInterval(captureInterval);
            captureInterval = null;
        }
    }

    /**
     * Set the remote SDP offer from the operator (called BEFORE createOffer on our side).
     * Unlike a standard browser peer, we are the ANSWERER here.
     */
    async function setOffer(sdp) {
        await peerConnection.setRemoteDescription({ type: "offer", sdp });
    }

    /** Create an SDP answer after setOffer(). */
    async function createAnswer() {
        const answer = await peerConnection.createAnswer();
        await peerConnection.setLocalDescription(answer);
        return answer.sdp;
    }

    // Keep old names as aliases so existing C# interop calls still work
    async function createOffer() { return await createAnswer(); }
    async function setAnswer(sdp) { await setOffer(sdp); }

    /** Add a trickled ICE candidate from the operator. */
    async function addIceCandidate(candidateJson) {
        try {
            const candidate = JSON.parse(candidateJson);
            await peerConnection.addIceCandidate(candidate);
        } catch (err) {
            console.warn("addIceCandidate failed:", err);
        }
    }

    /** Tear down everything. */
    function cleanup() {
        stopVideoCapture();
        peerConnection?.close();
        peerConnection = null;
        mediaStream?.getTracks().forEach(t => t.stop());
        mediaStream = null;
        if (captureVideo) { captureVideo.pause(); captureVideo.srcObject = null; }
        captureVideo = null;
        captureCanvas = null;
        captureCtx = null;
        inputChannel = null;
        dotnetRef = null;
    }

    return { startCapture, initPeer, createOffer, setAnswer, addIceCandidate, cleanup, setOffer, createAnswer };
})();
