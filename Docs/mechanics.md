# Mechanics — how Flappy Voice actually works

Authoritative for current behaviour; the [PRD](../PRD-flappy-voice.md) is the original spec and has
drifted (see the table at the end of the [README](../README.md)). Values quoted are the defaults in
`GameConfig` / `Assets/Settings/GameConfig.asset`.

## Pitch to height

```
midi   = 69 + 12·log2(hz / 440)          PitchMath.HzToMidi, continuous
height = (midi − floorMidi) / 24         PitchMath.ClampToOctaveHeight, clamped to 0..1
y      = lerp(−4.5, +4.5, height)        playfield bounds
```

Nothing in this chain rounds to a note. 450 Hz is A4 + 39 cents and puts the bird 39 cents of a
semitone above A — pinned by `PitchMathTests.ClampToOctaveHeight_DoesNotSnapToTheNearestNote` and
`…_IsStrictlyMonotonicInPitch`, because a note-quantised mapping is an easy thing to reintroduce by
accident.

Out-of-range pitch **clamps**. Sing above the ceiling and the bird sits at the ceiling; there is no
octave wrap. The tuner tints out-of-range letters amber to say so.

Movement is `SmoothDamp` toward the target Y with `HeightSmoothTimeSec 0.06` and a hard step clamp
of `MaxVerticalSpeed 9` units/sec. The bird has **no** flap bob: its rendered position is its pitch
position, exactly.

## Anchoring: the first note is aimed at the first gap

`OctaveAnchor` captures the first sustained note (`AnchorCaptureWindowMs 350` held within
`AnchorStabilityToleranceSemitones 1.5`, clamped into 55–700 Hz so a cough cannot anchor an octave
away) and derives the floor so **that note lands at the height of the gap ahead**:

```
floorMidi = sungMidi − height01·24        PitchMath.FloorMidiForNoteAtHeight
```

So whatever the player happens to sing to start a run threads the first pipe, wherever that pipe is
— high gap, high note. With no pipe on screen yet (a run starting before the first spawn) it falls
back to mid-range. `PipeGapGeometryTests.FirstSungNoteThreadsTheGapItWasAnchoredAt` checks this
for every gap position and for sung pitches deliberately nowhere near a semitone. The floor is not
re-derived mid-run: `AdaptiveRecenterer` exists and is deliberately **not** wired, because drifting
the floor would silently move every gap already on screen.

Before the anchor exists, `AttractPilot` flies the bird through gaps as a demo.

## Gap geometry: a gap is two notes wide

Gaps are specified in **notes**, not world units, because widening the pitch range shortens a
semitone on screen and a fixed world-unit gap would silently change how many notes fit.

```
unitsPerSemitone = 9 / 24 = 0.375
gapSize          = (pipeGapNotes − 1)·unitsPerSemitone + clearance
                 = 0.375 + lerp(1.375, 1.075, difficulty)
                 → 1.75 units at run start, 1.45 fully ramped
```

The gap centre sits on the **boundary between** the two notes it admits
(`PitchMath.HeightForNotePair` → `(lowerOffset + 0.5) / 24`), so both notes get the same margin. With
a bird collider radius of 0.42:

| | needs gap ≥ | so |
|---|---|---|
| both notes of the pair fit | 1.215 | satisfied at both ends of the ramp |
| a third note would fit | 1.965 | never reached — rejected |
| the anchored first note fits | 1.59 | satisfied at run start (difficulty 0) |

`PipeGapGeometryTests` pins all of it. **Retune `_pipeGapClearanceUnits`, `_minPipeGapClearanceUnits`,
the collider radius, or `_octaveWidthSemitones` together with that test** — the window between "the
pair fits" and "a third fits" is only ~0.75 units wide and it is easy to fall out of.

Difficulty ramps over `DifficultyRampDurationSec 90` along `DifficultyRampCurve`: speed 3 → 5.5,
spawn interval 2 s → 1.1 s, clearance as above. Consecutive gaps are kept within singing reach by
`PipeSpawner.MaxStepSemitones`, which converts `MaxVerticalSpeed × spawnInterval` into semitones and
caps the jump (`_maxNoteStepSemitones 7`, `_reachSafetyFactor 0.55`).

## The tuner strip

`TunerBarUI`, canvas space, top of screen, hidden (`rootGroup.alpha = 0`) until the run starts.

- The dial — note letters **and** the ten-cent tick ruler — is one rigid strip that only moves
  horizontally: `dial.x = −(midi − nearestSemitone) · 216 px`. The needle is nailed to the centre,
  so the needle's distance from a letter *is* the deviation from that note.
- `TunerBarUI.PixelsPerSemitone = 216` is **coupled to the ruler SceneBuilder draws**. Changing one
  without the other puts letters out of step with their own ticks.
- Letters cover ±3 semitones (`NoteSlotCount 7`); text is only rewritten when the nearest semitone
  changes, and cents strings are interned, so a frame allocates nothing.

### The green band is derived, not a tolerance

`PitchMath.TrySafePitchWindow` answers "which pitches clear the gap ahead" from the real geometry —
that pipe's actual `GapSize`, the bird's actual collider, the live floor:

```
reach   = gapSize/2 − bodyRadius
lowMidi  = floor + (gapCenterY − reach − minY)·(24/9)
highMidi = floor + (gapCenterY + reach − minY)·(24/9)
```

Its edges are the pitches that *just barely* miss the walls, so if 440 Hz scrapes through, 440 Hz is
the edge of the green. Consequences that fall out of this and are intentional:

- The band **narrows as difficulty ramps**, because the gap does.
- Sing badly and the band slides off the strip entirely — that is the honest reading.
- If the window extends past the range floor/ceiling it is **extended outward**, because pitch beyond
  the range clamps the bird to the same screen position, which really is still safe.
- The query looks `GapLookBehindUnits 1.2` behind the bird, so while flying *through* a gap the band
  still describes that gap rather than jumping to the next one.
- The needle turns green only while a note is actually held. Silence is not safety.

## States, scoring, audio

`GameStateManager`: `Attract → Playing → GameOver → Attract`.

| Transition | Trigger | Effects |
|---|---|---|
| Attract → Playing | anchor captured | `PipeSpawner` clears pipes on the bird; score SFX armed |
| Playing → GameOver | collision with a pipe | best score committed, crash SFX, game-over music |
| GameOver → Attract | Play Again | anchor reset, spawner reset, bird re-centred, game music |

Scoring is a trigger box at the gap centre (`Pipe.ScoreZone`), so a point — and its sound — lands as
the bird crosses the **middle** of a pipe.

`GameAudio` owns all sound: a looping music bed plus a one-shot channel. The start screen and the run
share `bgm_game.wav` and `PlayMusic` early-outs if the clip is already playing, so the handoff is not
audible as a restart. Music sits at 0.4 volume deliberately: the mic is open while the game plays, so
speaker output feeds the pitch detector.

## Two subtleties that were bugs

**Death while overlapping.** Unity raises `OnCollisionEnter2D` once, when an overlap begins. Attract
mode can fly the bird into a pipe while death is switched off, so when the run starts the overlap is
already in progress and no fresh Enter ever arrives — the pipe used to slide straight through the
bird. Fixed at both ends: `PipeSpawner.ClearPipesOnPlayer` recycles pipes sitting on the bird at the
handoff, and `PlayerController.OnCollisionStay2D` closes the hole for anything that slips past.

**Kinematic contacts.** Both the bird and the pipes are kinematic bodies with
`useFullKinematicContacts = true`. Without it a kinematic body raises no collision events against
static or kinematic colliders and nothing ever kills the player.

## Pitch detection

YIN (`YinPitchDetector`, `PitchBufferSize 2048`, `YinThreshold 0.15`) over the newest mic samples,
gated at `AmplitudeGateRms 0.015`. Detection runs 70–1200 Hz — wider than the anchor's 55–700 Hz
sanity clamp on purpose, since clamping *detection* at 700 Hz makes anything above ~F5 read as
unvoiced and freezes the bird mid-song.

`PitchTracker` tracks continuously within one semitone of the accepted pitch, but a jump larger than
that must be sustained for `SustainMs 80` before it is accepted, and up to 4 unvoiced frames are held
through consonants and breaths. So a deliberate leap lands ~80 ms late. That is hysteresis against
warble, **not** note quantisation — it never changes which pitch maps to which height.
