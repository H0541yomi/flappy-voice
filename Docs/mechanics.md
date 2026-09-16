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

## Gap geometry: a gap is one note wide, centred on that note

Gaps are specified in **notes**, not world units, because widening the pitch range shortens a
semitone on screen and a fixed world-unit gap would silently change how many notes fit.

```
unitsPerSemitone = 9 / 24 = 0.375
gapSize          = (pipeGapNotes − 1)·unitsPerSemitone + clearance
                 = 0 + lerp(1.375, 1.075, difficulty)
                 → 1.375 units at run start, 1.075 fully ramped
```

The gap centre sits **exactly on** the note it admits (`PitchMath.HeightForOffset` → `offset / 24`),
so that note gets the same margin above it as below it, and it is the only note of the range that
threads the pipe. Offsets run `0..24` inclusive — 25 notes, both ends included, and the camera
carries a 1-unit margin past the playfield bounds so an edge gap is still fully on screen. The
bird's collider radius is `PlayerBodyRadiusUnits 0.36` — one number in `GameConfig`, read by the
collider `SceneBuilder` authors, by the tuner's safe band and by the tests, rather than written down
three times:

| | needs gap ≥ | so |
|---|---|---|
| the note the gap is on fits | 0.72 | satisfied at both ends of the ramp (1.375 → 1.075) |
| a neighbouring note would fit | 1.47 | never reached — 0.095 units of headroom at the widest |
| the anchored first note fits | 0.72 | always — the anchor puts it on the gap centre |

The margin each side of the note is `gapSize/2 − 0.36` = **0.3275 units** at run start (0.1775 fully
ramped), unchanged from the two-note gap this replaced: the note used to sit half a semitone off
centre in a gap half a semitone wider, which came to the same thing.

`PipeGapGeometryTests` and `DifficultyRampTests.NeighbourNoteHeadroomSurvivesTheWidestGap` pin all
of it. **Retune `_pipeGapClearanceUnits`, `_minPipeGapClearanceUnits`, `_playerBodyRadiusUnits` or
`_octaveWidthSemitones` together with those tests** — the window between "the note fits" and "a
neighbour fits" is only ~0.75 units wide, and the run-start gap sits near its top edge. The tests no
longer pin the radius as a literal; they read it from the config, so a radius change has to keep
satisfying the invariant rather than quietly retuning the assertion.

## Difficulty is accuracy and tempo, over pipes passed

`GameConfig.DifficultyForPipesPassed` ramps over `DifficultyRampPipes 100` **pipes passed**, along
`DifficultyRampCurve`, and holds flat past the cap so a player good enough to reach 500 is not
squeezed by a gap that keeps closing. Two reasons it counts pipes rather than seconds: hovering
between pipes should not tighten the gap under you, and two runs that reach the same score should
have been equally hard to get there.

Two things ride that one ramp, both through a `…AtDifficulty` method on `GameConfig`:

| | at 0 pipes | at 100+ pipes |
|---|---|---|
| `PipeGapSizeAtDifficulty` (clearance) | `1.375` | `1.075` |
| `PipeSpeedAtDifficulty` | `3` | `5` |

**Spacing (`PipeSpacingUnits 10.4`) is not on the ramp** and is fixed for the whole run. Pipes
never bunch up; a fully ramped run is one where the same spacing arrives sooner. Spacing is
authored in **world units**, not seconds, because it is what the player actually sees — the
interval falls back out of it as `SpawnIntervalSecAtSpeed = PipeSpacingUnits / speed`, which
runs from ≈3.5 s at the start of a run down to ≈2.1 s fully ramped.

Ramping speed narrows the note window for free: `MaxStepSemitones` (below) sizes its window from
that same interval, so as pipes arrive sooner, consecutive gaps are placed closer together in
pitch — there is less time to sing across the distance.

## Where pipes come from: a distance, at a random note

`PipeSpawner.Update` accumulates **distance travelled**, not time, and spawns every
`PipeSpacingUnits`; `PickNoteOffset` supplies the height. Distance rather than a timer because
the speed moves mid-run: accumulating time against an interval that is itself shrinking leaves
each pipe slightly closer to the last one, and the spacing is the thing that must not drift.
There is no music to sync to — the game is silent while the mic is open — so the note is drawn at
random from the range.

The draw is uniform over a reach-limited window around the previous gap, **with the previous offset
removed from the window rather than merely made unlikely**, so the same height can never come up
twice in a row:

```
lo   = max(0,  last − step)          step = MaxStepSemitones
hi   = min(24, last + step)
pick = lo + Random.Range(0, hi − lo) ; if (pick >= last) pick++
```

`MaxStepSemitones` converts `MaxVerticalSpeed × spawnInterval` into semitones and caps the jump
(`_maxNoteStepSemitones 7`, `_reachSafetyFactor 0.55`), so consecutive gaps stay within singing
reach of one another.

### The letter in the gap

Each gap carries the letter of the note it is centred on, drawn at its centre in ink with a gold
(#FBD97B) glow behind it, at 0.47 world units - under half the narrowest opening the ramp ever makes (1.075), so it never touches
the pipe it is naming. `PitchMath.NoteNameForOffset(floorMidi, offset)` names it, which means it
cannot be named at all until the first sung note has anchored the range: an offset is only a note
once there is a floor under it. So the letters are blank through attract mode, appear on
everything on screen the moment the anchor lands (`VoiceHeightSource.OnAnchorChanged`), and are
correct for the rest of the run because the floor is never re-derived mid-run.

A letter disappears when the bird reaches its pipe - through the gap or into the tube, both of
which set `Pipe.HasScored`. The score zone is 0.25 units wide at the gap centre, so with the
bird's own radius the letter goes about half a unit before the bird is on top of it.

## The tuner strip

`TunerBarUI`, canvas space, a **600×180 pill at the top centre** — not a full-width bar. At full
width it owned the entire top of the screen and the bird vanished behind it on every high note.
Hidden (`rootGroup.alpha = 0`) until the run starts.

### The playfield ends below the strip

The strip is opaque, so the camera is framed to keep the whole playfield clear of it rather than
the playfield being allowed to run underneath. `HudLayout.CameraForPlayfield` solves it in one
step, because the two quantities are circular: the strip is authored in canvas pixels and the
canvas matches on **height**, so it always covers the same *fraction* of the view — but the world
height that fraction stands for depends on the camera size being solved for.

```
f = (20 + 180) / 1920 = 0.1042        strip height as a fraction of the screen
2H(1 − f) = span + bottomMargin + topClearance
C         = playfieldMinY − bottomMargin + H
hudBottom = C + H(1 − 2f)
```

`topClearance` is `max(bodyRadius, gapSize/2) + 0.25` — the gap opening is the taller of the two,
so it is the gap that sets the framing. With the defaults that gives `H 6.105`, `C 0.605`, a strip
bottom at **y 5.438**, the highest gap's top edge at **5.188** and the bird's at **4.86**. Neither
a gap nor the bird can reach the strip, and `HudLayoutTests` pins both.

Note what did **not** change: `PlayfieldMinY/MaxY` are still ±4.5 and the gap geometry is
untouched. The room for the strip comes out of the camera, so every invariant in
`PipeGapGeometryTests` and `DifficultyRampTests` holds exactly as before — the view is ~11% wider
than it was, and the playfield now sits low in it.

The sky quad is centred on the **camera**, not the origin, for the same reason: a sky hung at
`y = 0` left a band of the camera's clear colour along the top.

- The dial — note letters **and** the ten-cent tick ruler — is one rigid strip that only moves
  horizontally: `dial.x = −(midi − nearestSemitone) · 216 px`. The needle is nailed to the centre,
  so the needle's distance from a letter *is* the deviation from that note.
- `TunerBarUI.PixelsPerSemitone = 216` sets the spacing of the letters **and** of the ruler ticks:
  `SceneBuilder` reads the constant when it lays the ticks out, so the two cannot drift apart.
  Changing it still means rebuilding the scene.
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

- The band **narrows as the score climbs**, because the gap does.
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
| Attract → Playing | anchor captured | `PipeSpawner` clears pipes on the bird; lives restored; score SFX armed |
| Playing → GameOver | pipe hit with **no lives left** | best score committed |
| GameOver → Attract | Play Again | anchor reset, spawner reset, bird re-centred |

Scoring is a trigger box at the gap centre (`Pipe.ScoreZone`), so a point — and its sound — lands as
the bird crosses the **middle** of a pipe.

## Lives: the first two hits do not end the run

`LivesManager` holds the count (`PlayerLives 3`, restored on every `Playing`) and is deliberately a
separate component from the collision code, so the HUD and the end screen can read it without
reaching into `PlayerController`. `TryConsumeLife` returns *survived* or *that was the last one*.

A survivable hit does three things:

- **Invincibility, scoped to the pipe that was hit.** It ends when that pipe's trailing edge is
  behind **the bird's trailing edge** — not its centre. Measuring from the centre used to end
  invincibility while the wall was still overlapping the back half of the collider, so
  `OnCollisionStay2D` fired again on the same pipe and one crash burned every remaining life.
  `MinInvincibleSec 0.75` holds it open a little past that so the hit reads as a hit; pipes are
  ~3.5 s apart, so it can never hand out a free pass through the next one. `MaxInvincibleSec 4` is
  only a backstop for the case where the pipe never gets past: recycled and respawned out in front,
  or a run that ends first.
- **The hit pipe is marked `HasScored`.** A pipe you crashed into does not also pay out, and the
  bird does still fly through its gap on the way past.
- **The bird alternates its dead and flash poses** for the whole window, so "I am hurt" and "I
  cannot be hurt again yet" are one signal. The flash is a second sprite, not a tint — the default
  sprite shader multiplies, so a tint cannot brighten.

There is no lives readout in the HUD yet; the flashing bird is the only feedback. `LivesTests` pins
the count, the "third hit ends it" boundary, and the `OnLivesChanged` event the HUD would use.

**The crash one-shot fires on every pipe hit, not just the fatal one.** `GameAudio` watches
`LivesManager.OnLivesChanged` and sounds a decrease — which is exactly the set of collisions that
counted, since an invincible contact never reaches `LivesManager` and `ResetLives` only ever
raises the count. `ApplyState(GameOver)` deliberately does **not** also play it: the hit that
ended the run spent a life like any other, so a second call there would double the last one.

`GameAudio` owns all sound, and it is only ever those two one-shots. **There is no music at all**,
not even on the game-over screen — the mic is open the whole time the game is on screen, so
anything out of the speaker feeds straight back into the pitch detector.

## Presentation, and what drives it

None of this touches the maths; all of it reads the same state the maths already publishes.

- **Bird poses** — idle / singing / dead / flash sprites on one `SpriteRenderer`, assigned only on a
  change. The singing pose is gated on `VoiceHeightSource.IsActive` but **held `0.12 s` past** the
  end of a note: the voice gate drops out on consonants and between syllables, and swapping the
  sprite on every one of those makes the bird flicker.
- **`SingingFx`** — music notes trailing off the beak, pooled, emitted on the same voice gate as the
  singing pose so the two cannot disagree. The beak offset is authored relative to the bird's
  centre.
- **`ParallaxBackground`** — four scenery rows (clouds / far / mid / near) sliding at a fraction of
  `PipeSpawner.CurrentSpeed` and wrapping by exactly one tile width, so there is no seam to line up.
  Coverage is built out to a 2.4 aspect: running out of it shows the camera's clear colour down the
  sides. Only death stops the scroll — attract keeps drifting so the menu is alive, but scenery
  moving under a dead bird would read as flight.
- **Trumpet bells** are children of the pipe *root*, not of the stretched tube sections — a section
  is a 1×1 quad scaled by `localScale` and anything parented to it inherits that stretch. Their
  pivots sit on the flare rim, so placing them on the gap edge keeps the flare out of the gap.

### The two parchment panels

Both the start screen (`HudUI`'s `SingToStart` group) and the end screen (`EndScreenUI`) are the
same sprite, `Art/Ui/start_sign.png`, at **1024×1866**.

It is deliberately **not 9-sliced**. The leaf-and-flower clusters sit too far into two of its
corners for any border to contain them, so a stretch would smear them — which is why both panels
are authored at the sprite's own aspect (`SceneBuilder.SignSize`, 860×1567) rather than sized to
their contents.

**Content lives between `SignFaceTop` and `SignFaceBottom`** — 0.219 and 0.812 of the sign's
height, measured off the sprite as the band where the cream face is at least 77% of the sign's
width. Outside that band the deckled edge is tapering in, which is what put the Share button half
over the torn bottom edge before: it was placed downward from the score rather than upward from
the face. Both buttons are now measured up from `SignFaceBottom`. Text is capped at ~700 px wide
for the same reason.

Nothing on parchment may keep `NewText`'s default white. `InkColor` / `MutedInkColor` /
`ButtonLabelColor` in `SceneBuilder` are the three that are readable there.

The **NEW BEST badge sits on the best-score line** rather than in a row of its own, and
`EndScreenUI` hides that line while it shows: a row would cost ~60 px of face that the Share
button needs, and on a new best "BEST 42" only repeats the 42 already above it.

**The buttons sit on the sign, but the shared score card stops above them.** `ShareService` points
an orthographic camera at `scoreCardRoot`'s world rect and renders *every UI-layer graphic inside
it*, children or not — so parenting alone would not keep "Play Again" out of the shared image. The
capture rect is therefore a separate empty `ScoreCard` child covering the sign's top 970 px.

**The tuner on the start sign is a still life, not the tuner.** The real strip stays hidden until a
run starts, because before the anchor exists it has nothing true to say. `BuildTunerLegend` draws a
static one from the same three sprites the live strip uses, so the legend cannot drift from the
thing it is explaining.

- **Titles use a material asset**, `Art/Ui/SignTitle.mat`, with `_FaceDilate 0.22`. `FontStyles.Bold`
  is as heavy as TMP goes without a real bold weight in the font, and anything beyond it is a
  material property — but a material *instance* is no good, because TMP marks the ones it creates
  `HideAndDontSave` and the weight would vanish on the scene save.
- **The needle is `Art/Ui/needle.png`** — a 6 px white core inside a soft glow, in a 26 px rect so
  the glow is more than one pixel wide. It is white because `TunerBarUI` tints it green or red
  every frame; a coloured sprite could not go green.
- **Buttons are `Art/Ui/button.png`**, 9-sliced with a `120/52` border. The raw plaque is portrait,
  so `Tools/key-ui-art.py` rotates it a quarter turn before squashing: unrotated, its brush grain
  runs across what becomes the button's long axis and reads as vertical streaking.

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
gated at `AmplitudeGateRms 0.03`. Detection runs 70–1200 Hz — wider than the anchor's 55–700 Hz
sanity clamp on purpose, since clamping *detection* at 700 Hz makes anything above ~F5 read as
unvoiced and freezes the bird mid-song.

`PitchTracker` tracks continuously within one semitone of the accepted pitch, but a jump larger than
that must be sustained for `SustainMs 80` before it is accepted, and up to 4 unvoiced frames are held
through consonants and breaths. So a deliberate leap lands ~80 ms late. That is hysteresis against
warble, **not** note quantisation — it never changes which pitch maps to which height.
