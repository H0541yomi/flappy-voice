// Bridge to the app that embeds this game.
//
// The build ships inside Variant, which renders web games in a WebView, so the page has no chrome
// of its own and nothing the player opened to go back to: window.close() is a no-op for a document
// the script did not open, and the only real way out is to ask the host to take the game down,
// which it listens for on VariantOriginalsHost. The same build also runs in a plain browser tab
// for testing, where there is no host and the way out is the way in - so this falls back to
// history.back() rather than returning, which left the X dead on every tap.
//
// Mirrors VariantOriginalsQuit in the parent's scripts/unity/package/Runtime/Plugins/WebGL/
// VariantBridge.jslib. This repo reimplements that file instead of referencing
// com.variant.originals.web, whose only reference form is a path into a parent checkout, so the
// two drift silently - see Docs/build.md.

const FlappyVoiceHostPlugin = {
    FV_Host_Quit: function () {
        // Posting quit twice is harmless; going back twice walks two pages out of the game, so
        // the second tap during a teardown that has not finished yet has to do nothing.
        if (globalThis.__variantOriginalsQuitRequested) return;

        // The publisher injects a visit runtime into every published game page, and it reports
        // foreground time as cumulative checkpoints 30s apart. Quitting takes the page down
        // before the next one, and a host teardown is not required to fire pagehide, so whatever
        // was played since the last checkpoint is only recorded if it is flushed here.
        // Fire-and-forget in its own try: closing the game never waits on analytics, and the
        // runtime is absent everywhere but the published CDN hosts.
        try { globalThis.__variantOriginalsVisit?.quit(); } catch (error) { /* never fatal */ }

        try {
            const host = globalThis.VariantOriginalsHost;
            if (host && typeof host.postMessage === 'function') {
                host.postMessage(JSON.stringify({ schema_version: 1, action: 'quit' }));
                globalThis.__variantOriginalsQuitRequested = true;
                return;
            }
        } catch (error) {
            console.warn('[FlappyVoiceHost] quit message failed:', error);
        }

        try {
            if (typeof history === 'undefined' || typeof history.back !== 'function') {
                console.warn('[FlappyVoiceHost] no host and no history to leave by; staying put.');
                return;
            }
            history.back();
            globalThis.__variantOriginalsQuitRequested = true;
        } catch (error) {
            console.warn('[FlappyVoiceHost] history.back() failed:', error);
        }
    },
};

mergeInto(LibraryManager.library, FlappyVoiceHostPlugin);
