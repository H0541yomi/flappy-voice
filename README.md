# Flappy Voice

An endless flyer controlled by singing. Pitch is height: sing higher and the bird rises, sing lower
and it falls. Pipes are trumpets whose gap is centred on a single note; hold that note to fly
through.

**It ships as a web game** — a Unity Web build embedded by a host app (Variant) that renders web
games. Portrait, mobile browser first. See [Deploying to the web](#deploying-to-the-web) for the
build and for what the host has to provide, the microphone especially.

- [PRD](PRD-flappy-voice.md) — original product spec. Historical; see below.
- [Docs/mechanics.md](Docs/mechanics.md) — **how the game actually works today**, with the maths
- [Docs/style-guide.md](Docs/style-guide.md) — **how it looks**: colour, typography, layout rhythm
- [Docs/build.md](Docs/build.md) — **how it ships**: the Web build and the host's contract

## Layout

The repo root *is* the Unity project, so every build service and CI action finds it without a
`projectPath` override.

```
flappy-voice-app/            this repo, and the Unity project
├── PRD-flappy-voice.md      product spec (+ rendered .html)
├── game.json                Variant Originals catalog metadata, see below
├── promo/promo.png          catalog thumbnail — GENERATED, see below
├── Docs/                    living documentation
├── Tools/                   generators for placeholder assets, and the magenta-key pass
├── Assets/Scripts/          game code (asmdef: FlappyVoice)
├── Assets/Editor/           SceneBuilder (asmdef: FlappyVoice.Editor)
├── Assets/Tests/            EditMode tests (asmdef: FlappyVoice.Tests.EditMode)
├── Assets/Plugins/WebGL/    FlappyVoiceMic.jslib — the Web microphone bridge, see below
├── Assets/Scenes/           Game.unity — GENERATED, see below
├── Assets/Prefabs/          Pipe.prefab — GENERATED, see below
├── Assets/Audio/            placeholder SFX and game-over bed, see below
├── Assets/Art/              Bird/ Trumpets/ Bg/ Fx/ Ui/ — real art; Placeholder/ is the fallback
├── Packages/
└── ProjectSettings/
```

Unity **6000.6.0f1**, URP 2D, portrait, `1080×1920` reference canvas, **Unity Web** build target.

## The scene is generated, not authored

`Assets/Scenes/Game.unity` and `Assets/Prefabs/Pipe.prefab` are **build output**. They are produced
by `Assets/Editor/SceneBuilder.cs` and rebuilt with:

> **Flappy Voice → Build Game Scene**

Consequences worth internalising before editing anything:

- **Hand-editing the scene in the Inspector is throwaway work.** The next rebuild overwrites it. UI
  layout, object hierarchy and wiring all live in `SceneBuilder.cs`.
- Every component keeps its dependencies in **non-serialized** fields, populated by `Configure(...)`.
  Those calls do not survive a scene save, which is why `GameBootstrap` holds serialized references
  and replays the whole wiring sequence at runtime. Add a dependency in three places: the
  component's `Configure`, `GameBootstrap`, and `SceneBuilder`.
- `SceneBuilder` regenerates the placeholder PNGs in `Assets/Art/Placeholder` too. Real art should
  go in sibling folders (see the asset brief) rather than overwrite those paths.

## Running it

Open the repo root in Unity and play `Assets/Scenes/Game.unity`. It needs microphone permission;
without a mic the game stays in attract mode forever, which is correct behaviour and not a bug.

## Deploying to the web

The Web player is the shipping target (Variant renders the game as a web build). Build it with
**Flappy Voice → Build Web Player**, or headlessly:

```sh
npm run build          # what the parent catalog calls
Tools/build-web.sh     # same thing; the shell wrapper execs it
```

Both land in `build.mjs`, which checks `game.json` against the parent's contract, finds the editor
the project pins (`ProjectSettings/ProjectVersion.txt`) wherever the Hub put it, **refuses to build
on any editor but that one**, refuses to start while the Editor holds the project, and then runs
`WebBuilder.BuildWeb`. `Tools/unity-path.sh` does
the editor lookup — the Hub's install root differs per OS, and is under `~` rather than
`/Applications` when the Hub was installed without admin rights. `UNITY_EDITOR_PATH` (or
`UNITY_PATH`) skips the search.

Output lands in `dist/`, Brotli-compressed with the decompression fallback **off**, matching the
parent catalog's settings — which is why the payload is `Build/dist.*.br` and why the host must
send `Content-Encoding: br`. `Tools/serve-web.py` does, which is what makes a local run match the
CDN.
[Docs/build.md](Docs/build.md) covers the rest: the module you need, the 75 MB ceiling, and why the
output is safe to mount under a subpath.

**The microphone does not come from `UnityEngine.Microphone` here.** That class compiles for Web
from 6000.4 on, but `AudioClip.GetData` fails while a recording is running, so the clip can only be
read after recording stops — useless for a game that needs the newest 2048 samples every frame.
Capture goes through `Assets/Plugins/WebGL/FlappyVoiceMic.jslib` instead: Web Audio fills a one
second ring buffer from an `AudioWorklet` (`ScriptProcessor` where a CSP blocks the worklet blob),
and `WebMicrophoneBackend` copies the newest samples out of it each frame.

Three things the *host* has to get right, or the game hears nothing no matter what the build does:

1. **HTTPS.** `getUserMedia` does not exist outside a secure context. `localhost` is exempt.
2. **`allow="microphone"` on the iframe**, if the page embeds the build in one. Permissions Policy
   denies the feature to cross-origin frames by default — the prompt never even appears.
3. **A user gesture.** Everything except desktop Chrome only honours `getUserMedia` from inside
   one, and in a WKWebView the host app also needs its own mic permission and a
   `WKUIDelegate` capture-permission handler (iOS 15+). The game copes: the bridge listens for
   the first tap or key press itself and the start sign reads "Tap to turn on the microphone"
   until capture is live.

### Sharing a score, and what the host bridge actually carries

`window.VariantOriginalsHost` accepts JSON **strings** and understands two actions today:

| Action | Payload |
|---|---|
| Quit | `{ schema_version: 1, action: "quit" }` — immediate, never behind a confirmation dialog |
| Orientation | `{ schema_version: 1, action: "orientation", value: "portrait" }` — sent before gameplay starts |

`HostBridge.RequestQuit` posts the first through
[`FlappyVoiceHost.jslib`](Assets/Plugins/WebGL/FlappyVoiceHost.jslib). The game does **not** send
the second: it is portrait-only and the app is portrait by default, so there is nothing to ask for.

**There is no share action on the bridge** — a native share sheet through the host still needs
app-side work. So the share button goes at the browser instead, through
[`FlappyVoiceShare.jslib`](Assets/Plugins/WebGL/FlappyVoiceShare.jslib): `navigator.share` where it
exists, `navigator.clipboard.writeText` where it does not, and the end screen's button reads
**"Link copied"** for two seconds afterwards so a clipboard write does not look like nothing
happening. Dismissing the sheet (`AbortError`) is treated as a choice and says nothing; anything
else reads **"Share failed"** and logs the link.

The link is `https://variantapp.us/<alias>`, built in
[`ShareLink`](Assets/Scripts/Platform/ShareLink.cs). **`GameSlug` is
`PLACEHOLDER-REPLACE-IN-PROD`** — Variant's publishing team registers an alias per game and
arbitrary slugs do not resolve, so it stays obviously broken until the real one is issued.
`https://getvariant.link/vrntapp` is the general app-download link, which this game does not use.

One caveat that cannot be fixed from inside the game: both browser APIs need transient user
activation, and Unity dispatches a UI click from its own animation frame rather than from inside
the DOM handler, so the call lands a frame after the tap. That is inside Chrome's activation window
and stricter elsewhere — the same class of problem as the microphone, which is why that one arms
its own window-level listeners.

## Catalog metadata for the Originals shelf

This repo is a submodule of `VariantExperiments/variant-originals`, pinned at
`games/flappy-voice/`. Two files at the root are that parent's contract rather than anything Unity
reads, and both stay valid on their own so a plain clone of this repo never needs the parent:

**The game is called *Flappy Song* to players; the slug and this repo stay `flappy-voice`.** The
catalog does not require them to match — `corn-maze` ships as *Bellwether Fields* — and the slug is
baked into the submodule path and the published CDN URL, so renaming it would cost a republish for
nothing. `name` in `game.json`, the promo title, Unity's `productName` and the share message are
the four places a player sees it; everything else (namespaces, menu items, log prefixes, these
docs) keeps the repo name.

| File | What it is |
|---|---|
| `game.json` | Catalog entry — name, one-line description, genres, lifecycle status, priority, orientation |
| `promo/promo.png` | Master thumbnail at **1400×900**. The parent compresses it to `preview.webp` at publish; never commit the WebP |

`game.json` is validated by `scripts/lib/game-catalog.mjs` in the parent, and **unknown fields are
errors**, misspelled optional ones included. `genres` is a closed list and `arcade` is the primary,
which is what shelves the game. `status` is `local`, meaning it publishes to no environment — the
issue keeps the game here until it is ready to rejoin the catalog, so nothing else in the contract
is enforced yet.

The thumbnail is **generated**, not drawn:

```sh
python3 Tools/make-promo.py
```

It composes the board from the game's own shipped sprites and the sign face, so re-running it
after an art change carries the change into the thumbnail. It also asserts what is easy to get
wrong and invisible while authoring: the host paints a **Play now** pill over `x 440–960,
y 680–872`, and the shelf card crops outside `x 80–1320`. The title and the bird have to clear
both; only scenery may run through them.

**What is not settled.** `engine: "unity"` is declared, and the parent ships a shared Unity toolkit
(`scripts/unity/`, the `com.variant.originals.web` UPM package) whose contract this project now
mirrors without consuming the package: it keeps its own `WebBuilder` and `HostBridge`, but the
build settings, the `Variant` Web template, the `dist/` output, the 75 MB ceiling and the
`package.json` build script all match. [Docs/build.md](Docs/build.md) has the requirement-by-
requirement table.

One item is deliberately left open. `assertUnityPackageReference` wants
`"com.variant.originals.web": "file:../../../scripts/unity/package"` in `Packages/manifest.json`,
and that relative path only resolves when this repo sits inside a parent checkout — in a standalone
clone Unity cannot resolve the package and the project will not open. Since nothing here consumes
the package, the reference would buy a publish-time check at the cost of the repo opening on its
own, so it waits on a decision from whoever owns `variant-originals`. At `status: "local"` nothing
enforces it.

The other known difference is cosmetic in the same way: quit goes through `HostBridge` rather than
`VariantWebBridge.RequestQuit()`. The *payload* already matches the parent's bridge exactly
(`{schema_version: 1, action: "quit"}` to `window.VariantOriginalsHost`); what differs is which
code posts it.

## Tests

EditMode only — the game logic that matters (pitch mapping, anchoring, gap geometry, YIN detection)
is pure functions, deliberately.

```sh
# in the Editor: Window → General → Test Runner → EditMode → Run All
# headless:
UNITY="$(Tools/unity-path.sh)"
"$UNITY" -batchmode -nographics -projectPath . \
  -runTests -testPlatform EditMode -testResults /tmp/results.xml -logFile /tmp/unity.log
```

Unity locks a project while the Editor has it open, so the headless run needs the Editor closed —
or a copy of the project (`rsync -a --exclude Library --exclude Temp --exclude Logs --exclude
UserSettings`) somewhere else.

`PipeGapGeometryTests` is the one to watch: it pins the promise that a gap admits exactly the one
note it was placed on and rejects both its neighbours, at both ends of the difficulty ramp. Retuning
`_pipeGapClearanceUnits`, `_playerBodyRadiusUnits`, or the pitch range will fail it on purpose —
along with `DifficultyRampTests`, which pins the headroom before the *neighbouring* note would clear
the gap.

The rest: `PitchMathTests` (continuous pitch → height, no snapping), `OctaveAnchorTests`,
`SafeBandAccuracyTests` (the tuner's green band is the real geometry), `YinPitchDetectorTests`,
`LivesTests`, `HudLayoutTests` (the playfield stays clear of the tuner strip),
`AdaptiveRecentererTests`.

## Placeholder assets

Both sets are **placeholders with real hooks** — the game is fully playable and audible, and
swapping in final assets is a file drop plus import settings, no code.

- **Audio** — `Tools/make-placeholder-audio.py` synthesises the two clips in `Assets/Audio`
  (stdlib only, no ffmpeg needed). Re-run it after editing the script:
  ```sh
  python3 Tools/make-placeholder-audio.py
  ```
  Real files go onto `GameAudio`'s two clip fields. There is deliberately no music anywhere — the
  mic is open the whole time the game is on screen — so the two one-shots are all there is.
- **Art** — the bird, trumpets, parallax scenery, note FX, tuner chrome and the two parchment
  panels are real art, loaded by path from `Assets/Art/{Bird,Trumpets,Bg,Fx,Ui}`. Raw generations
  land in `Assets/Art/images/` on flat magenta; `python3 Tools/key-ui-art.py` keys, despills, trims
  and resizes them into `Assets/Art/Ui/`. `SceneBuilder` still paints its procedural
  placeholders into `Assets/Art/Placeholder` and falls back to them (`LoadSpriteOr`) when a real
  file is missing, so a half-populated art folder builds rather than breaks.

## The PRD is partly out of date

The PRD is kept as written. The mechanic has since moved on in ways it does not describe:

| PRD says | Actually |
|---|---|
| Octave wrap: crossing an octave wraps to the bottom | No wrap. Out-of-range pitch **clamps** to floor/ceiling |
| One octave (`octaveWidthSemitones: 12`) | **Two** octaves (24) |
| `height = (semitoneOffset mod 12) / 12` | `height = (midi − floor) / 24`, continuous, no rounding |
| First sung note sets the octave **floor** | It is placed at the **height of the gap ahead**, which sets the floor |
| Pipe gap sized in world units | Sized in **notes** (1), centred on the note it admits |
| Colour-coded pitch meter with octave bands | Chromatic tuner strip with a derived safe-note band |
| Difficulty ramps speed and/or gap over time | **Gap only, over pipes passed.** Speed and spacing are fixed for the run |
| Pipes spawn on a timer | Still a timer; gap heights are random notes, never twice the same in a row |
| A pipe hit ends the run | **Three lives**, with invincibility until the pipe that was hit is fully behind you |

[Docs/mechanics.md](Docs/mechanics.md) is authoritative for current behaviour.
