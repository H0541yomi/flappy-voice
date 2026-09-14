# Working in this repo

Unity 6000.6.0f1 game, portrait, controlled by singing pitch.

**This ships as a web game.** The build target is Unity Web, embedded by a host app (Variant) that
renders web games, so the player is on a mobile browser or a WebView — not a native app install.
Anything that only works on a native player is not shipped: check it against the Web build before
calling it done. `Assets/Editor/WebBuilder.cs` (**Flappy Voice → Build Web Player**) is the build.

Read [Docs/mechanics.md](Docs/mechanics.md) before changing gameplay — the maths has invariants that
are easy to break silently.

## The scene is generated

`Assets/Scenes/Game.unity` and `Assets/Prefabs/Pipe.prefab` are output of
`Assets/Editor/SceneBuilder.cs` (menu: **Flappy Voice → Build Game Scene**). Editing them by hand is
throwaway work. Change `SceneBuilder`, rebuild, commit the regenerated files.

Adding a dependency between components means touching **three** places, because every component
keeps its dependencies in non-serialized fields that a scene save discards:

1. the component's `Configure(...)`
2. `GameBootstrap` — serialized field + the `Configure` call + `ReportMissingReferences`
3. `SceneBuilder` — build it, add to `candidates`, `AutoWireByType`, `WireBootstrap`

`AutoWireByType` fills only null reference fields and only by type, so explicit `SetRef` in the
builder wins. Array fields need `SetRefArray`.

## Verifying changes headlessly

The user usually has the Editor open, which locks the project. Work in a copy:

```sh
rsync -a --exclude Library --exclude Temp --exclude Logs --exclude obj --exclude UserSettings \
  ./ /tmp/fv/
U=/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity
$U -batchmode -nographics -projectPath /tmp/fv -runTests -testPlatform EditMode \
   -testResults /tmp/fv/results.xml -logFile /tmp/fv/tests.log
$U -batchmode -nographics -projectPath /tmp/fv \
   -executeMethod FlappyVoice.Editor.SceneBuilder.BuildGameScene -quit -logFile /tmp/fv/scene.log
```

First run rebuilds `Library` (~3 min); later runs are fast. Parse `results.xml` for failures — the
log alone will not tell you.

**Screenshots of the HUD** are worth taking for any layout change: drop a throwaway editor method
into the copy that opens the scene, points `Camera.main` at a `1080×1920` RenderTexture, calls
`Render()` and writes a PNG. Must run **without** `-nographics`. Component `Update` never runs, so
runtime-positioned objects appear at their authored transforms.

**Copying the built scene back:** if the user's Editor has already imported new files, its `.meta`
GUIDs differ from the copy's, and the built scene references the copy's. Remap rather than
overwriting their metas — extract each `guid:` from both metas and substitute in the scene text
before writing it into the repo. Unresolved GUIDs in the scene are normal: uGUI, TMP, URP and Input
System assets live in `Library/PackageCache`, not in `Assets`.

## Invariants worth not breaking

- **Gap = one note, centred on it.** `PipeGapGeometryTests` pins that a gap admits exactly the note
  it was placed on and rejects both neighbours. The safe window between "the note fits" and "a
  neighbour fits" is ~0.75 units wide, and the run-start gap sits near its top edge. Retune
  `_pipeGapClearanceUnits` / `_playerBodyRadiusUnits` / `_octaveWidthSemitones` *with* that test and
  `DifficultyRampTests`.
- **Gap heights are random, never repeating.** `PipeSpawner.PickNoteOffset` draws from a
  reach-limited window with the previous offset *removed*, so a repeat is unrepresentable rather
  than unlikely. There is no music to sync to; that was removed on request.
- **Difficulty is gap only, over pipes passed.** Speed and spawn interval are fixed for the run.
- **Height is continuous in pitch.** Nothing may round pitch to a semitone on the way to a position.
  Two `PitchMathTests` cases exist purely to catch a reintroduction.
- **`TunerBarUI.PixelsPerSemitone` also sets the tick ruler** — `SceneBuilder` reads the constant
  when it draws it. Change it in one place, then rebuild the scene.
- **The playfield is framed to sit under the tuner strip.** `HudLayout.CameraForPlayfield` derives
  the camera size and centre from the playfield, the margins and the strip's screen fraction;
  `HudLayoutTests` pins that neither the bird nor the highest gap can reach the strip. Change
  `TunerBarHeightPx`/`TunerTopMarginPx` in `SceneBuilder` and the camera follows — do not hand-tune
  `orthographicSize`. Playfield bounds and gap geometry were deliberately left alone.
- **The parchment sign is not 9-sliced.** Its corner flowers would smear, so both panels are
  authored at the sprite's aspect (`SceneBuilder.SignSize`). Content must sit between
  `SignFaceTop` and `SignFaceBottom` — outside that band the deckled edge is tapering in and
  things hang off the parchment. Text is capped at ~700 px and must use `InkColor` /
  `MutedInkColor`; white is invisible on parchment.
- **`ShareService` captures by world rect, not by hierarchy.** Anything on the UI layer inside
  `scoreCardRoot` lands in the shared image, children or not — which is why the end screen's
  buttons sit below a capture rect that stops short of them.
- **Heavier title text is a material asset** (`Art/Ui/SignTitle.mat`), never a material instance —
  TMP marks instances `HideAndDontSave`, so an instance silently reverts on scene save.
- **Raw art is keyed, not hand-edited** — `python3 Tools/key-ui-art.py` turns the magenta JPEGs in
  `Assets/Art/images/` into the PNGs in `Assets/Art/Ui/`. Sprite `.meta` files are written by hand
  alongside the PNG, before either Unity sees it, so a headless build and the user's Editor agree
  on the GUID. `m_DefaultBehaviorMode` is 3D, so a meta that omits `textureType: 8` imports as a
  plain texture and `LoadAssetAtPath<Sprite>` quietly returns null.
- **The Web build never touches `UnityEngine.Microphone`.** The class does compile for Web in
  6000.4+, but `AudioClip.GetData` fails while a recording is active, so a live signal is
  unreadable through it. `MicrophoneInput` picks a backend instead: `UnityMicrophoneBackend`
  everywhere else, `WebMicrophoneBackend` on Web, which polls the ring buffer that
  `Assets/Plugins/WebGL/FlappyVoiceMic.jslib` fills from an `AudioWorklet`. Adding a second
  capture path on Web means a second `getUserMedia` stream — do not "restore" the Unity one.
- **Mic permission on Web arrives on a tap, not at startup.** Every browser but desktop Chrome
  only runs `getUserMedia` inside a user gesture, and the game reads no input, so the jslib arms
  its own window-level listeners and `GameBootstrap` keeps retrying while
  `MicPermission.RetriesOnUserGesture`. That retry loop is why the start sign's hint is swappable
  (`HudUI.SetStartHint`) — it is the only feedback the player gets.
- **Deliberately absent:** background music during attract/play and everything that read it
  (`MusicDirector`, `SongAnalyzer`, `BeatNotePlanner`, beat-synced spawning), dev height source and
  dev panel, the flap bob, the world-space note line, the mic level meter, in-gap note labels,
  `PitchMeterUI`. All removed on request — do not reintroduce them as "helpful".
- `AdaptiveRecenterer` is intentionally not wired; drifting the floor mid-run would move every gap
  already on screen.

## Conventions

- Comments explain **why**, not what. Match the surrounding density — this codebase comments the
  non-obvious decision and stays silent elsewhere.
- Per-frame UI code allocates nothing: cache a "rendered" key and early-out, intern strings up front
  (see `TunerBarUI`).
- Private serialized fields, exposed through read-only properties. `GameConfig` is the single source
  of tuning values; derive, do not duplicate (e.g. `PipeGapSizeAtDifficulty`).
- Tests are EditMode and pure — the gameplay maths is deliberately free of MonoBehaviour.
- Placeholder audio is regenerated by `python3 Tools/make-placeholder-audio.py`, not hand-edited.
- Commits: Conventional Commits, subject ≤50 chars, body explains the why.
