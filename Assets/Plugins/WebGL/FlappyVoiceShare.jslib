// Sharing a score out of a game that has nothing of its own to share it with.
//
// Three places a share can land, tried in the order the parent's canonical VariantOriginalsShare
// tries them: the Variant host, then navigator.share, then the clipboard. The host goes first
// because this build ships inside a WebView, which is the worst place for navigator.share - both
// it and the clipboard need transient user activation, and Unity dispatches a UI click from its
// own animation frame rather than from inside the DOM handler, so the call lands a frame after
// the tap. That is inside Chrome's activation window and at the mercy of stricter browsers. A
// native sheet raised by the host has neither problem.
//
// The catch, and the reason this is worth re-reading before trusting it: whether the shipped app
// implements action: "share" is not knowable from this repo and is not yet confirmed (issue #1).
// The parent's Unity bridge sends it; the parent's JS host package exports only quit and
// orientation. If the app does not handle it, the message is swallowed and the button reports a
// sheet that never opened.
//
// Off the host, navigator.share raises the OS sheet where it exists, and everywhere else the link
// goes on the clipboard, which is the whole reason the end screen has a "Link copied" state to
// show afterwards. A refusal is reported as a failure and the link is logged, so the player can
// still be given something to copy by hand rather than a button that silently did nothing.

const FlappyVoiceSharePlugin = {
    FV_Share_Link: function (receiverPointer, titlePointer, textPointer, urlPointer) {
        const receiver = UTF8ToString(receiverPointer);
        const title = UTF8ToString(titlePointer);
        const text = UTF8ToString(textPointer);
        const url = UTF8ToString(urlPointer);

        const report = function (outcome) {
            try {
                if (typeof SendMessage === 'function') {
                    SendMessage(receiver, 'OnWebShareResult', outcome);
                } else if (typeof Module !== 'undefined' && Module.SendMessage) {
                    Module.SendMessage(receiver, 'OnWebShareResult', outcome);
                }
            } catch (error) {
                console.warn('[FlappyVoiceShare] could not report the outcome:', error);
            }
        };

        try {
            const host = globalThis.VariantOriginalsHost;
            if (host && typeof host.postMessage === 'function') {
                host.postMessage(JSON.stringify({
                    schema_version: 1,
                    action: 'share',
                    title: title,
                    text: text,
                    url: url,
                }));
                // The host owns the sheet from here and sends nothing back, so this is the one
                // outcome reported without knowing it. 'shared' is deliberate: it is the outcome
                // the end screen says nothing about, which is right when the sheet is someone
                // else's and what the player does with it is not ours to claim.
                report('shared');
                return;
            }
        } catch (error) {
            console.warn('[FlappyVoiceShare] host share failed; trying the browser:', error);
        }

        // Deliberately not awaited by the caller: C# cannot block on a promise, and the end screen
        // is driven by the report above rather than by a return value.
        (async function () {
            try {
                if (navigator.share) {
                    await navigator.share({ title: title, text: text, url: url });
                    report('shared');
                    return;
                }
                await navigator.clipboard.writeText(url);
                report('copied');
            } catch (error) {
                // Dismissing the share sheet is a choice, not a fault. Everything else is worth a
                // line in the console with the link in it, because that is the only copy left.
                if (error && error.name === 'AbortError') {
                    report('cancelled');
                    return;
                }
                console.warn('[FlappyVoiceShare] share failed; the link is ' + url, error);
                report('failed');
            }
        })();
    },
};

mergeInto(LibraryManager.library, FlappyVoiceSharePlugin);
