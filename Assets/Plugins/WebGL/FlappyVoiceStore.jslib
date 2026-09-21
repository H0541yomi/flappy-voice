// The handful of numbers that have to survive a reload - the best score and the two settings -
// kept in localStorage rather than in PlayerPrefs.
//
// Unity's own PlayerPrefs on Web writes into an IndexedDB-backed emscripten filesystem that only
// this build can read and that only reaches the disk when a flush happens to run. localStorage is
// the browser's own store, synchronous, and readable by the page around us - which matters here
// because the game is embedded, not standalone.
//
// Numbers only, in both directions. Returning a string would mean allocating into the Unity heap
// and handing C# a pointer to free, and every value this game persists is a number, so the whole
// marshalling problem is avoidable rather than solvable.
//
// Every access is wrapped: localStorage throws outright in Safari's private mode and in a
// third-party iframe with storage blocked, and this build runs inside someone else's frame.
// A store that is not there means the best score does not persist, which is not worth a crash.

const FlappyVoiceStorePlugin = {
    FV_Store_GetNumber: function (keyPointer, fallback) {
        try {
            const raw = localStorage.getItem(UTF8ToString(keyPointer));
            if (raw === null) return fallback;
            const parsed = Number(raw);
            return Number.isFinite(parsed) ? parsed : fallback;
        } catch (error) {
            return fallback;
        }
    },

    FV_Store_SetNumber: function (keyPointer, value) {
        try {
            localStorage.setItem(UTF8ToString(keyPointer), String(value));
        } catch (error) {
            console.warn('[FlappyVoiceStore] localStorage is unavailable; nothing will persist.', error);
        }
    },
};

mergeInto(LibraryManager.library, FlappyVoiceStorePlugin);
