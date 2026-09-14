# Flappy Voice

An endless flyer controlled by singing. Pitch is height: sing higher and the bird rises, sing lower
and it falls. Pipes are trumpets whose gap is centred on a single note; hold that note to fly
through.

**It ships as a web game** — a Unity Web build embedded by a host app (Variant) that renders web
games. Portrait, mobile browser first. See [Deploying to the web](#deploying-to-the-web) for the
build and for what the host has to provide, the microphone especially.

- [PRD](PRD-flappy-voice.md) — original product spec. Historical; see below.
- [Docs/mechanics.md](Docs/mechanics.md) — **how the game actually works today**, with the maths
- [Docs/asset-prompts.md](Docs/asset-prompts.md) — art brief for the UI overhaul, with prompts
- [Docs/asset-prompt-pack.md](Docs/asset-prompt-pack.md) — per-asset prompt sheet (+ `.html`)
- [Docs/art-refs/](Docs/art-refs/) — style references the overhaul is based on

## Layout

The repo root *is* the Unity project, so every build service and CI action finds it without a
`projectPath` override.

```
flappy-voice-app/            this repo, and the Unity project
├── PRD-flappy-voice.md      product spec (+ rendered .html)
├── Docs/                    living documentation and art references
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
UNITY="$(Tools/unity-path.sh)"
"$UNITY" -batchmode -nographics -projectPath . \
  -executeMethod FlappyVoice.Editor.WebBuilder.BuildWeb -quit -logFile /tmp/web.log
```

`Tools/unity-path.sh` finds the editor the project pins (`ProjectSettings/ProjectVersion.txt`)
wherever the Hub put it — the install root differs per OS, and is under `~` rather than
`/Applications` when the Hub was installed without admin rights. `UNITY_PATH=/path/to/binary`
overrides the search.

Output lands in `Build/Web` (Brotli, with the decompression fallback on so it works on hosts that
serve the files without a `Content-Encoding` header).

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
  file is missing, so a half-populated art folder builds rather than breaks. Briefs live in
  [Docs/asset-prompts.md](Docs/asset-prompts.md).

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
