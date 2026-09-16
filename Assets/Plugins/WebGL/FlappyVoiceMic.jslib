// Realtime microphone capture for Unity Web.
//
// Unity's own Microphone class does compile for Web in 6000.4+, but AudioClip.GetData fails while
// a recording is active, so the clip can only be read once recording has stopped. This game needs
// the newest ~2048 samples every frame, so it talks to Web Audio directly instead.
//
// The data path deliberately has no function pointers in it: JS fills a ring buffer, C# polls it
// with FV_Mic_Read. dynCall-style callbacks are the part of jslib that breaks across Emscripten
// versions, and polling is what the existing MicrophoneInput.ReadLatest API wants anyway.

const FlappyVoiceMicPlugin = {
    $FVMic: {
        // Mirrored by FlappyVoice.Platform.WebMicStatus.
        IDLE: 0,
        PENDING: 1,
        RUNNING: 2,
        BLOCKED: 3,
        UNSUPPORTED: 4,

        // ~10ms at 48kHz. Small enough that the worklet hop adds no audible tracking lag.
        CHUNK: 512,
        // A backgrounded tab or an interrupted stream stops filling the ring without any event to
        // say so, and the newest samples would then read as a held note forever. Well over one
        // worklet hop, so a 120Hz game loop outrunning a 48kHz stream is not mistaken for a stall.
        STALE_MS: 250,

        status: 0,
        sampleRate: 0,
        lastWriteMs: 0,
        ring: null,
        ringWrite: 0,
        ringFilled: 0,
        ctx: null,
        stream: null,
        source: null,
        node: null,
        sink: null,
        gestureHandler: null,

        GESTURES: ['pointerdown', 'touchend', 'mousedown', 'keydown'],

        supported: function () {
            return typeof navigator !== 'undefined' &&
                !!navigator.mediaDevices &&
                !!navigator.mediaDevices.getUserMedia &&
                (typeof AudioContext !== 'undefined' || typeof webkitAudioContext !== 'undefined');
        },

        allocRing: function (sampleRate) {
            // One second of history. ReadLatest never asks for more than a couple of thousand
            // samples, but a stalled Unity frame must not lose the window it was about to read.
            const size = Math.max(8192, sampleRate | 0);
            if (!this.ring || this.ring.length !== size) {
                this.ring = new Float32Array(size);
            }
            this.ringWrite = 0;
            this.ringFilled = 0;
        },

        push: function (samples) {
            const ring = this.ring;
            if (!ring) return;

            const size = ring.length;
            let n = samples.length;
            // A burst longer than the ring can only ever leave its own tail behind.
            let from = n > size ? n - size : 0;
            n -= from;

            let write = this.ringWrite;
            const firstRun = Math.min(n, size - write);
            ring.set(samples.subarray(from, from + firstRun), write);
            if (firstRun < n) {
                ring.set(samples.subarray(from + firstRun, from + n), 0);
            }

            this.ringWrite = (write + n) % size;
            this.ringFilled = Math.min(size, this.ringFilled + n);
            this.lastWriteMs = performance.now();
        },

        // Re-armed after every rejection, with no cap on how many times: a player who keeps
        // tapping keeps getting asked. Nothing here can run away, because one arming buys exactly
        // one attempt - the handler disarms itself before it calls open(), and only the catch a
        // rejection lands in arms it again - so the ask is paced by the browser's own prompt.
        // Safari's refusal of a second concurrent getUserMedia is handled by the PENDING guard in
        // open(), which is why this needs no attempt budget of its own.
        armGesture: function () {
            if (this.gestureHandler || typeof window === 'undefined') return;

            const handler = function () {
                FVMic.disarmGesture();
                FVMic.open();
            };
            this.gestureHandler = handler;
            // Capture phase and on window: the Unity canvas stops the event before it bubbles.
            for (let i = 0; i < this.GESTURES.length; ++i) {
                window.addEventListener(this.GESTURES[i], handler, true);
            }
        },

        disarmGesture: function () {
            const handler = this.gestureHandler;
            if (!handler) return;
            this.gestureHandler = null;
            for (let i = 0; i < this.GESTURES.length; ++i) {
                window.removeEventListener(this.GESTURES[i], handler, true);
            }
        },

        // The worklet lives in a Blob so this plugin stays a single file. A Content-Security-Policy
        // that forbids blob: workers rejects addModule, and start() falls back to ScriptProcessor.
        workletSource: function () {
            return 'class FVCapture extends AudioWorkletProcessor{' +
                'constructor(o){super();this.buf=new Float32Array(o.processorOptions.chunk);this.n=0;}' +
                'process(inputs){const ch=inputs[0]&&inputs[0][0];if(!ch)return true;' +
                'for(let i=0;i<ch.length;++i){this.buf[this.n++]=ch[i];' +
                'if(this.n===this.buf.length){this.port.postMessage(this.buf.slice(0));this.n=0;}}' +
                'return true;}}' +
                'registerProcessor("fv-capture",FVCapture);';
        },

        attachWorklet: async function () {
            if (!this.ctx.audioWorklet) return false;

            const url = URL.createObjectURL(new Blob([this.workletSource()], { type: 'text/javascript' }));
            try {
                await this.ctx.audioWorklet.addModule(url);
            } finally {
                URL.revokeObjectURL(url);
            }

            const node = new AudioWorkletNode(this.ctx, 'fv-capture', {
                numberOfInputs: 1,
                numberOfOutputs: 0,
                channelCount: 1,
                processorOptions: { chunk: this.CHUNK },
            });
            node.port.onmessage = function (event) { FVMic.push(event.data); };
            this.source.connect(node);
            this.node = node;
            return true;
        },

        attachScriptProcessor: function () {
            // Deprecated and main-thread, so it drops audio whenever Unity hitches; only ever a
            // fallback for browsers or CSPs that refuse the worklet.
            const node = this.ctx.createScriptProcessor(2048, 1, 1);
            node.onaudioprocess = function (event) {
                FVMic.push(event.inputBuffer.getChannelData(0));
            };
            this.source.connect(node);
            // Chrome stops servicing a ScriptProcessor that is not connected to a sink; the gain
            // node is muted so the player never hears themselves.
            const sink = this.ctx.createGain();
            sink.gain.value = 0;
            node.connect(sink);
            sink.connect(this.ctx.destination);
            this.node = node;
            this.sink = sink;
        },

        open: async function () {
            if (this.status === this.RUNNING || this.status === this.PENDING) return;
            if (!this.supported()) {
                this.status = this.UNSUPPORTED;
                return;
            }
            this.status = this.PENDING;

            try {
                // The browser's own processing fights pitch detection: AGC pumps the amplitude
                // gate and noise suppression eats sustained vowels it mistakes for steady noise.
                this.stream = await navigator.mediaDevices.getUserMedia({
                    audio: {
                        echoCancellation: false,
                        noiseSuppression: false,
                        autoGainControl: false,
                        channelCount: 1,
                    },
                    video: false,
                });

                const Ctor = typeof AudioContext !== 'undefined' ? AudioContext : webkitAudioContext;
                this.ctx = new Ctor();
                if (this.ctx.state === 'suspended') {
                    // Only succeeds inside a user gesture; harmless otherwise, and capture from a
                    // MediaStreamSource still runs on Chrome with the context suspended.
                    try { await this.ctx.resume(); } catch (e) { /* not fatal */ }
                }

                this.sampleRate = this.ctx.sampleRate;
                this.allocRing(this.sampleRate);
                this.source = this.ctx.createMediaStreamSource(this.stream);

                let attached = false;
                try {
                    attached = await this.attachWorklet();
                } catch (err) {
                    console.warn('[FlappyVoiceMic] AudioWorklet unavailable, falling back:', err);
                }
                if (!attached) {
                    this.attachScriptProcessor();
                }

                this.status = this.RUNNING;
            } catch (err) {
                console.warn('[FlappyVoiceMic] getUserMedia failed:', err && err.name ? err.name : err);
                this.close();
                this.status = this.BLOCKED;
                // iOS Safari and most WebViews only honour getUserMedia inside a user gesture, and
                // a denial there is indistinguishable from a real refusal, so wait for a tap.
                this.armGesture();
            }
        },

        close: function () {
            if (this.node) {
                this.node.disconnect();
                if (this.node.port) this.node.port.onmessage = null;
                this.node.onaudioprocess = null;
                this.node = null;
            }
            if (this.sink) {
                this.sink.disconnect();
                this.sink = null;
            }
            if (this.source) {
                this.source.disconnect();
                this.source = null;
            }
            if (this.stream) {
                this.stream.getTracks().forEach(function (track) { track.stop(); });
                this.stream = null;
            }
            if (this.ctx) {
                const ctx = this.ctx;
                this.ctx = null;
                ctx.close().catch(function () { /* already closing */ });
            }
            this.ringFilled = 0;
            this.ringWrite = 0;
        },
    },

    FV_Mic_IsSupported: function () {
        return FVMic.supported() ? 1 : 0;
    },

    // The caller polls and re-asks while it waits, so a request that is already parked on a tap
    // must not re-run getUserMedia: the browser would reject every one of those without ever
    // prompting, and each rejection would re-arm the listener the real tap is waiting on.
    FV_Mic_Request: function () {
        if (FVMic.gestureHandler) return;
        FVMic.open();
    },

    FV_Mic_Stop: function () {
        FVMic.disarmGesture();
        FVMic.close();
        FVMic.status = FVMic.IDLE;
    },

    FV_Mic_GetStatus: function () {
        return FVMic.status;
    },

    FV_Mic_GetSampleRate: function () {
        return FVMic.sampleRate | 0;
    },

    // Writes the newest `count` samples to `ptr` and returns how many were written, or 0 when the
    // ring has not filled that far yet.
    FV_Mic_Read: function (ptr, count) {
        const ring = FVMic.ring;
        if (!ring || FVMic.status !== FVMic.RUNNING) return 0;
        if (count <= 0 || count > FVMic.ringFilled) return 0;
        if (performance.now() - FVMic.lastWriteMs > FVMic.STALE_MS) return 0;

        const size = ring.length;
        const start = (FVMic.ringWrite - count + size) % size;
        const firstRun = Math.min(count, size - start);

        HEAPF32.set(ring.subarray(start, start + firstRun), ptr >> 2);
        if (firstRun < count) {
            HEAPF32.set(ring.subarray(0, count - firstRun), (ptr >> 2) + firstRun);
        }
        return count;
    },
};

autoAddDeps(FlappyVoiceMicPlugin, '$FVMic');
mergeInto(LibraryManager.library, FlappyVoiceMicPlugin);
