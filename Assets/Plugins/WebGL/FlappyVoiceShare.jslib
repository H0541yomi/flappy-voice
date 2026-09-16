// Sharing a score out of a game that has nothing of its own to share it with.
//
// The Variant host bridge carries quit and orientation and nothing else - there is no share action
// to post, so this goes straight at the browser. navigator.share raises the OS share sheet where
// it exists; everywhere else the link goes on the clipboard, which is the whole reason the end
// screen has a "Link copied" state to show afterwards.
//
// Both APIs need transient user activation, and Unity dispatches a UI click from its own animation
// frame rather than from inside the DOM handler, so the call lands a frame after the tap. That is
// inside Chrome's activation window and at the mercy of stricter browsers. A refusal is reported
// as a failure and the link is logged, so the player can still be given something to copy by hand
// rather than a button that silently did nothing.

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
