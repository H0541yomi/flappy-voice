// Bridge to the app that embeds this game.
//
// The build ships inside Variant, which renders web games in a WebView, so the page has no chrome
// of its own and nothing the player opened to go back to: history.back() leaves the game running
// and window.close() is a no-op for a document the script did not open. The only real way out is
// to ask the host to take the game down, which it listens for on VariantOriginalsHost.
//
// Silent by design when the host is absent. The same build runs in a plain browser tab for
// testing, where there is nobody to post to and a throw would take the quit button's click
// handler down with it.

const FlappyVoiceHostPlugin = {
    FV_Host_Quit: function () {
        try {
            const host = typeof window !== 'undefined' ? window.VariantOriginalsHost : null;
            if (!host || typeof host.postMessage !== 'function') {
                console.warn('[FlappyVoiceHost] no VariantOriginalsHost to quit to; staying put.');
                return;
            }
            host.postMessage(JSON.stringify({ schema_version: 1, action: 'quit' }));
        } catch (error) {
            console.warn('[FlappyVoiceHost] quit message failed:', error);
        }
    },
};

mergeInto(LibraryManager.library, FlappyVoiceHostPlugin);
