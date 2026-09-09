# Flappy Voice

An endless flyer controlled by singing. Pitch is height: sing higher and the bird rises, sing lower
and it falls. Pipes are trumpets with a gap two notes wide; hold the right note to fly through.

- [PRD](PRD-flappy-voice.md) — original product spec. Historical; see below.
- [Docs/mechanics.md](Docs/mechanics.md) — **how the game actually works today**, with the maths
- [Docs/asset-prompts.md](Docs/asset-prompts.md) — art brief for the UI overhaul, with prompts
- [Docs/art-refs/](Docs/art-refs/) — style references the overhaul is based on

## Layout

```
flappy-voice-app/            this repo
├── PRD-flappy-voice.md      product spec (+ rendered .html)
├── Docs/                    living documentation and art references
├── Tools/                   generators for placeholder assets
└── Flappy Voice/            the Unity project
    ├── Assets/Scripts/      game code (asmdef: FlappyVoice)
    ├── Assets/Editor/       SceneBuilder (asmdef: FlappyVoice.Editor)
    ├── Assets/Tests/        EditMode tests (asmdef: FlappyVoice.Tests.EditMode)
    ├── Assets/Scenes/       Game.unity — GENERATED, see below
    ├── Assets/Prefabs/      Pipe.prefab — GENERATED, see below
    ├── Assets/Audio/        placeholder music and SFX, see below
    └── Assets/Art/          placeholder sprites, painted procedurally
```

Unity **6000.6.0f1**, URP 2D, portrait, `1080×1920` reference canvas.

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

Open `Flappy Voice/` in Unity and play `Assets/Scenes/Game.unity`. It needs microphone permission;
without a mic the game stays in attract mode forever, which is correct behaviour and not a bug.

## Tests

EditMode only — the game logic that matters (pitch mapping, anchoring, gap geometry, YIN detection)
is pure functions, deliberately.

```sh
# in the Editor: Window → General → Test Runner → EditMode → Run All
# headless:
/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath "Flappy Voice" \
  -runTests -testPlatform EditMode -testResults /tmp/results.xml -logFile /tmp/unity.log
```

Unity locks a project while the Editor has it open, so the headless run needs the Editor closed —
or a copy of the project (`rsync -a --exclude Library --exclude Temp --exclude Logs --exclude
UserSettings`) somewhere else.

`PipeGapGeometryTests` is the one to watch: it pins the promise that a gap admits exactly the two
notes it was placed for and rejects their neighbours, at both ends of the difficulty ramp. Retuning
`_pipeGapClearanceUnits`, the bird's collider radius, or the pitch range will fail it on purpose.

## Placeholder assets

Both sets are **placeholders with real hooks** — the game is fully playable and audible, and
swapping in final assets is a file drop plus import settings, no code.

- **Audio** — `Tools/make-placeholder-audio.py` synthesises the four clips in `Assets/Audio`
  (stdlib only, no ffmpeg needed). Re-run it after editing the script:
  ```sh
  python3 Tools/make-placeholder-audio.py
  ```
  Real files go onto `GameAudio`'s four clip fields.
- **Art** — painted procedurally by `SceneBuilder` into `Assets/Art/Placeholder`. Replace per
  [Docs/asset-prompts.md](Docs/asset-prompts.md).

## The PRD is partly out of date

The PRD is kept as written. The mechanic has since moved on in ways it does not describe:

| PRD says | Actually |
|---|---|
| Octave wrap: crossing an octave wraps to the bottom | No wrap. Out-of-range pitch **clamps** to floor/ceiling |
| One octave (`octaveWidthSemitones: 12`) | **Two** octaves (24) |
| `height = (semitoneOffset mod 12) / 12` | `height = (midi − floor) / 24`, continuous, no rounding |
| First sung note sets the octave **floor** | It is placed at the **height of the gap ahead**, which sets the floor |
| Pipe gap sized in world units | Sized in **notes** (2), converted to world units per range width |
| Colour-coded pitch meter with octave bands | Chromatic tuner strip with a derived safe-note band |

[Docs/mechanics.md](Docs/mechanics.md) is authoritative for current behaviour.
