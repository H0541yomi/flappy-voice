# Working in this repo

Unity 6000.6.0f1 game, portrait, controlled by singing pitch.

**This ships as a web game.** The build target is Unity Web, embedded by a host app (Variant) that
renders web games, so the player is on a mobile browser or a WebView — not a native app install.
Anything that only works on a native player is not shipped: check it against the Web build before
calling it done. `Assets/Editor/WebBuilder.cs` (**Flappy Voice → Build Web Player**, or `npm run build`
headlessly) is the build, and its settings mirror the parent catalog's `OriginalsWebBuild` rather
than being chosen here; output is `dist/`, which is the parent's `publishDir` and not a preference.
[Docs/build.md](Docs/build.md) has the pinned editor version, the module it needs, the
requirement-by-requirement table against the parent's contract, and why the output is safe to mount
under a subpath.

Read [Docs/mechanics.md](Docs/mechanics.md) before changing gameplay — the maths has invariants that
are easy to break silently. Read [Docs/style-guide.md](Docs/style-guide.md) before changing how
anything looks — it is authoritative for colour, typography and the layout rhythm, and every value
in it is a `SceneBuilder` constant rather than something to pick by eye.

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
U=$(Tools/unity-path.sh)   # resolves the Hub install root per OS; UNITY_PATH overrides
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
- **Difficulty is gap and speed, over pipes passed.** `PipeGapSizeAtDifficulty` (clearance
  1.375 → 1.075) and `PipeSpeedAtDifficulty` (3 → 5) share one ramp. **Spacing is fixed**
  (`PipeSpacingUnits 10.4`) — `PipeSpawner` spawns on distance travelled, not a timer, so a
  speed that moves mid-run cannot let the pipes drift closer together. `DifficultyRampTests`
  pins both ends of the ramp and that `interval * speed == spacing` at each.
- **Height is continuous in pitch.** Nothing may round pitch to a semitone on the way to a position.
  Two `PitchMathTests` cases exist purely to catch a reintroduction.
- **`TunerBarUI.PixelsPerSemitone` also sets the tick ruler** — `SceneBuilder` reads the constant
  when it draws it. Change it in one place, then rebuild the scene. The start sign's still-life
  legend (`BuildTunerLegend`) does the same thing at its own scale: one `semitoneWidth` places the
  letters *and* steps off the ticks, because a legend whose tall ticks sit a few pixels wide of its
  letters teaches the dial wrong. Do not give the two their own fractions of `size.x` again.
- **The strip reads by position only — no note name, no cents number.** Both readouts were removed
  on request; the letter by the needle is the note and its distance from the needle is the error,
  which is the whole point of a sliding dial. `TunerDialBottomPadPx` is what that row became: it is
  now the pad that keeps the ticks-and-letters block optically centred in the pill, not a row with
  content in it. `PitchMath.CentsFromNearestSemitone` is left in place and still pinned by
  `PitchMathTests`.
- **The in-run score hangs off the tuner strip.** `ScoreLabelTopFromTop` is
  `TunerTopMarginPx + TunerBarHeightPx + ScoreLabelGapPx` (40 px), not an authored offset: the
  strip and the score are one block at the top of the screen — the note you are holding and what
  it has won you — so retuning the strip carries the score instead of quietly opening or closing
  the gap.
- **The strip's contents are clipped to the pill's face, not to its rect.** `tuner_pill.png` is a
  raised rim around a dark face with strongly rounded ends, so a ruler drawn to the rect's edge
  hangs its outer ticks in the sky and the safe band squares off over a rounded end — which is
  exactly what it used to do. The `Viewport`'s `RectMask2D` is inset by `TunerFaceInsetXPx` /
  `TunerFaceInsetYPx` (28 / 18), measured off the sprite through its own 9-slice: the borders are
  72 sprite px at 256 px/unit against the canvas's 100 reference, so they stay 28 rect px wide
  whatever the rect is and the inset does not move with the strip's size. Everything inside —
  band, dial, ticks, letters, needle — is a child of the viewport and measured against
  `TunerFaceHeightPx`, which is why **`TunerBarHeightPx` is derived** (`face + 2 × inset`): the
  face is the content box and the pill is that plus its rim, so a rim redrawn thicker makes the
  pill taller instead of quietly cropping the ruler.
- **The playfield is framed to sit under the tuner strip.** `HudLayout.CameraForPlayfield` derives
  the camera size and centre from the playfield, the margins and the strip's screen fraction;
  `HudLayoutTests` pins that neither the bird nor the highest gap can reach the strip. Change
  `TunerFaceHeightPx`/`TunerTopMarginPx` in `SceneBuilder` and the camera follows — do not
  hand-tune `orthographicSize`. Playfield bounds and gap geometry were deliberately left alone.
- **A pipe's tube stops at its bell's neck; its collider does not.** The bell sprite's top edge is
  drawn at the tube's exact width, so the two join invisibly there — and drawn any further the
  tube's flat end reappears below the flare as a stub hanging into the gap, which is what it used
  to do. `Pipe.Setup` stretches each section to the pipe's full extent because the section is
  what carries the `BoxCollider2D` that lines the gap, and shortens the `Tube` **child** that
  carries the sprite: `InsetTube` works in fractions of that stretch, and `BellDepth` reads the
  distance off the bell's sprite bounds either side of its pivot, so redrawing a bell moves the
  tube's end with it. `TubeBellOverlapUnits` is the few pixels of overlap that keep the seam from
  opening into a hairline of sky where the neck's outermost column is antialiased. Note that
  `WirePipeSections` matches `tube` **before** the plain top/bottom test, or the field gets handed
  its own section.
- **The parchment sign is not 9-sliced.** Its corner flowers would smear, so both panels are
  authored at the sprite's aspect (`SceneBuilder.SignSize`). Content must sit between
  `SignFaceTop` and `SignFaceBottom` — outside that band the deckled edge is tapering in and
  things hang off the parchment. Text is capped at ~700 px and must use `InkColor` /
  `MutedInkColor`; white is invisible on parchment.
- **`ShareService` captures by world rect, not by hierarchy.** Anything on the UI layer inside
  `scoreCardRoot` lands in the shared image, children or not — which is why the end screen's
  buttons sit below a capture rect that stops short of them.
- **Share is browser APIs, not the host bridge.** The Variant bridge carries `quit` and
  `orientation` and nothing else, so there is no share action to post: `WebNativeShare` goes
  straight at `navigator.share`, falling back to `navigator.clipboard.writeText`, which is the only
  reason the end screen has a **"Link copied"** state to show. Both APIs need transient user
  activation and Unity dispatches a UI click from its own animation frame rather than from inside
  the DOM handler, so the call lands a frame after the tap — inside Chrome's activation window, and
  at the mercy of stricter browsers. A refusal comes back as `failed`, the button says so, and the
  jslib logs the link, because that is the only copy left. Dismissing the sheet is `cancelled` and
  deliberately says nothing.
  **`WantsScoreCard` is false on Web.** The browser share takes title, text and a URL; the captured
  PNG is written into Unity's own filesystem where the page cannot reach it, so rendering one would
  be a full-resolution readback and a PNG encode thrown away on every tap. `ShareService` skips the
  capture when the backend does not want it — the card is still captured in the Editor, so a broken
  one stays visible.
  **`ShareLink.GameSlug` is `PLACEHOLDER-REPLACE-IN-PROD`.** Variant registers an alias per game and
  arbitrary slugs do not resolve under `variantapp.us`, so it stays obviously wrong rather than a
  plausible guess that ships as a dead link.
- **Heavier title text is a material asset** (`Art/Ui/SignTitle.mat`), never a material instance —
  TMP marks instances `HideAndDontSave`, so an instance silently reverts on scene save.
- **The consent flow is UI only.** `ConsentFlowUI` calls no permission API at all: it raises
  `OnMicrophoneRequest` / `OnCameraRequest` on the frame of the tap, and `GameBootstrap`
  subscribes with named methods (never lambdas — `-=` has nothing to match on an anonymous one)
  so its `StartMicrophoneRoutine` / `StartCameraRoutine` do the asking inside that same tap. The
  panel exists because getUserMedia needs a user gesture outside desktop Chrome — without a
  button there is no tap to spend.
  While it is up it holds `HudUI.SetStartScreenSuppressed`, so the start sign does not stack
  behind it.
- **One consent panel, not two — and one button per answer.** Both steps are the same parchment,
  so the step changes only the words and which buttons are active. The three buttons are
  `microphoneAcceptButton` ("OK"), `cameraDeclineButton` ("NO") and `cameraAcceptButton` ("YES!"),
  each with a fixed label, plaque and verdict, so no handler branches on `Current` to decide
  *whether* the answer was yes. Do not re-merge "OK" and "NO" into one plaque because they share a
  sprite: an earlier cut did, and its handler had to read the step to know which verdict to send.
  The design's plaque choice tracks whether there is a competing option, not the verdict — timber
  for "OK" and "NO", brass only for the "YES!" that has a "NO" beside it to outweigh.
  **Both request events fire only for a grant.** The microphone step has no refuse button at all,
  and refusing the camera just calls `GoTo(Step.Done)` without reporting, so neither event carries
  a verdict — a refusal shows up only as `OnCompleted`. The catch to remember: the
  browser can still hold a grant from an earlier visit, so `CameraPermission.HasPermission` may
  read `true` immediately after the player tapped **NO**. Nothing may start the feed off
  `HasPermission` alone; only `OnCameraRequest` means the player asked for it.
  They sit in a `HorizontalLayoutGroup` sized
  for two (the most ever up at once), so the lone microphone button centres itself — but a
  headless build never ticks a canvas, so `SceneBuilder` must call
  `LayoutRebuilder.ForceRebuildLayoutImmediate` or the saved scene keeps them stacked at the row's
  centre.
- **All text is a generated font asset — two of them.** `Assets/Art/Fonts/IMFellGreatPrimerSC
  SDF.asset` (the sign face, every string in the app) and `Assets/Art/Fonts/Gulzar SDF.asset`
  (digits only, the scores) are both output of `Assets/Editor/FontBuilder.cs` (**Flappy Voice →
  Build Font Asset**) from the committed `.ttf`s. `SceneBuilder.ResolveFont` loads the sign face
  and every `NewText` call goes through there, so that one lookup is what puts the whole app in
  the face the art is designed in; `ApplyNumberFace` is the single exception and is applied after
  `NewText` on exactly three labels — `ScoreLabel`, `FinalScore` and the `Value` half of the
  end screen's best-score row. Both atlases are baked and the
  assets left in `AtlasPopulationMode.Static` — the vocabularies (printable ASCII, and `0123456789`)
  are known up front, and Dynamic would rasterise on the player's main thread on Web.
  `EnsureFontAsset` / `EnsureNumberFontAsset` return an existing asset untouched, so a scene
  rebuild does not churn an atlas or its GUID; the **menu item does** rebuild both, and the sign
  atlas repacks non-deterministically, so do not commit a rebuilt
  `IMFellGreatPrimerSC SDF.asset` you did not mean to change (its GUID and sub-object fileIDs are
  stable, so the scene survives either way). Gulzar is a Nastaliq face with a lining Latin set:
  only pure-numeral labels use it, because its digits next to IM Fell's old-style figures inside
  one string would read as a mistake — which is why the best line is a **row of two labels**
  (`Caption` in the sign face, `Value` in the number face, both `TextAlignmentOptions.Baseline` so
  the digits sit on the word's feet) instead of one `BEST {n}` string, and why `EndScreenUI`
  toggles `bestScoreRow` rather than the label when the new-best badge takes that line. Any mixed
  string that needs a numeral stays wholly in the sign face. The TMP default (LiberationSans) remains only as a backstop for the sign face; the number
  face has none, so a missing asset leaves the scores in IM Fell rather than blank.
- **One ink for the whole app.** `InkColor` is #501713, the brown the signs are drawn in, and
  `MutedInkColor` is a lifted version of it for quiet lines; `ButtonLabelColor` is #FBD97B,
  because ink on dark timber would be unreadable. The **only** text that is not ink is the in-run
  `ScoreLabel`, which floats over the playfield rather than sitting on parchment and keeps
  `NewText`'s white. The brass `YES!` plaque is light, so its label is ink, not gold.
- **Consent layout came from Figma, its proportions did not.** The design stretches the parchment
  to a 1.401 aspect where the sprite's own is 1.187; since it is not 9-sliced, the consent heights
  come across as *fractions* of the design frame (`ConsentTitleCenterFromTop` and friends) rather
  than scaled pixels. Widths and type sizes do scale straight across, by `ConsentDesignScale`.
- **The X button quits to the host, and belongs to the screen.** `QuitButtonUI` sits in the
  canvas's top-right corner, never on a panel — the three panels are three sizes in three places.
  It shows whenever `GameState != Playing`, which is one subscription instead of three because
  Attract always has exactly one sign out (consent, microphone notice, or start) and GameOver
  always has the end screen. The tap calls `HostBridge.RequestQuit`, which posts
  `{schema_version: 1, action: "quit"}` to `window.VariantOriginalsHost` through
  `Assets/Plugins/WebGL/FlappyVoiceHost.jslib`; there is nothing to close from inside the game,
  because Variant owns the frame. `RequestQuit` logs the exact payload before posting, and logs
  it off the web too, so a tap in the Editor is visible proof the button reaches the bridge.
  Two clearances are load-bearing and neither has much slack:
  the tuner pill (600 px, centred) leaves ~38 px at 9:19.5, the tightest aspect a phone ships,
  and the end screen's capture rect stops 53 px below the button, which is the only reason the
  shared card does not have an X in its corner. `SceneBuilder` builds it **last** so it stays
  above the consent flow's blocking dim.
- **The microphone notice is not a consent step.** `MicrophoneNoticeUI` is the same parchment as
  the consent panels and follows their vertical rhythm, but it is its own object: the consent
  flow's job is to spend a tap on `getUserMedia`, and this reports the answer, which has to be
  able to arrive long after that flow is `Done`. `GameBootstrap.ReportMicrophoneMissing` raises
  it at most once per deliberate ask — `RequestMicrophone` guards on `microphoneRoutineRunning`,
  so the web's 0.5 s retry loop cannot nag — and `ShowMicrophoneNoticeRoutine` waits on
  `consentFlow.IsComplete`, not on `IsShowing`, because two parchments stacked read as one broken
  one and a browser that refuses inside the microphone tap raises this mid-transition to the
  camera step, when no parchment is up but the flow is not through.
  **OK does not dismiss it.** The tap raises `OnRetryRequested`, which is `RequestMicrophone`
  again and nothing else; the panel stays up, and `StartMicrophoneRoutine` calls `Hide()` from
  the one branch where recording actually starts. A second refusal therefore leaves the notice
  where it was (the re-entered loop's `Show()` is a no-op on an already-showing panel), which is
  the point: it is the only sign that the game still cannot hear the player, so a button of its
  own must not be able to clear it. Asking again changes nothing else about the microphone — the
  retry loop is still running underneath, and on the web the tap is itself a gesture the jslib
  bridge is listening for.
  **Two answers, not one.** The row is the consent flow's, with `exitButton` ("EXIT", timber) on
  the left and `retryButton` ("OK", brass) on the right — the plaque rule again: brass only
  where there is a competing option to outweigh. EXIT calls `HostBridge.RequestQuit` and leaves
  the panel up, because the ask is for something the game cannot grant itself and a site-level
  denial will survive any number of OK taps.
  **`SetStartScreenSuppressed` now has two owners.** The notice touches it only on a real
  transition, never from `Awake`, or whichever of the two ran second would drop the other's hold.
- **A sprite's trim bounds are only as good as its border.** JPEG ringing along a drop's own edge
  keys to a few percent of coverage — the same magnitude as a soft glow, so no `ALPHA_FLOOR` can
  separate them — and one speck of it in a corner drags `trim` out to the image edge. The sprite
  then resizes off-centre: needle.png used to put its bright core 9 px left of centre in a 32 px
  rect, which is a tuner whose line is drawn ~3 cents sharp of what it reads. `Target.trim_border`
  clears a border ring before the trim; set it only on drops that have margin to spare.
- **Raw art is keyed, not hand-edited** — `python3 Tools/key-ui-art.py` turns the magenta JPEGs in
  `Assets/Art/images/` into the PNGs in `Assets/Art/Ui/`. Sprite `.meta` files are written by hand
  alongside the PNG, before either Unity sees it, so a headless build and the user's Editor agree
  on the GUID. `m_DefaultBehaviorMode` is 3D, so a meta that omits `textureType: 8` imports as a
  plain texture and `LoadAssetAtPath<Sprite>` quietly returns null.
  Drops that arrive already cut out are PNGs and skip the key: `Target.extension` picks the
  path, and `HALO_FLOOR` throws away the a≤0.19 haze they carry so the trim finds the artwork.
  `ALPHA_FLOOR` is per-drop overridable (`Target.alpha_floor`) because a noisier generation keys
  its empty area to ~0.09 rather than the usual ~0.03 and survives the shared 0.06 floor — which
  is invisible against a dark backdrop and an obvious pale rectangle on parchment. Check a new
  sprite *on the parchment*, not against the editor's dark background.
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
  **There is no cap on the asking.** `FVMic.armGesture` re-arms after every rejection, forever, so
  the nth tap prompts exactly as the first did — which is what makes the notice's OK work on Web,
  where `RequestMicrophone` is a no-op against the running loop and the tap itself is the retry.
  One arming buys one attempt (the handler disarms before calling `open()`, and only the catch
  re-arms), so nothing can outrun the browser's own prompt and the old `MAX_ATTEMPTS` budget is
  gone: it could only ever run out on a player who was still trying.
- **Deliberately absent:** music of any kind, including the game-over bed, and everything that
  read it (`MusicDirector`, `SongAnalyzer`, `BeatNotePlanner`, beat-synced spawning, `GameAudio`'s
  music source). `GameAudio` is two one-shots and nothing else. Also gone: dev height source and
  dev panel, the flap bob, the world-space note line, the mic level meter, in-gap note labels,
  `PitchMeterUI`. All removed on request — do not reintroduce them as "helpful".
- **`GameConfig.UseCameraBackground` is the camera kill switch.** Off means `GameBootstrap` never
  runs `StartCameraRoutine` (no prompt at all) and `WebCam` tears the feed down live, so the
  painted parallax sky shows — the same picture a player who refuses the prompt gets. It lives on
  the config asset, not on `GameBootstrap`, because the scene is generated and a flag flipped on
  the component is lost at the next `SceneBuilder` run.
- **The loading screen is the game's, not the shared shell's.**
  `Assets/WebGLTemplates/Variant/index.html` is the parent's canonical Web template with its
  colour and type replaced: `SkyColor` page, the product name in the sign face
  (`sign-face.ttf`, a copy of the same TTF `FontBuilder` bakes, with its OFL beside it), and the
  progress bar drawn as the tuner strip. Because the page ends on the colour Unity clears to, the
  handover is a wordmark going out rather than a background changing. `build.mjs` diffs this file
  against the canonical one and **will warn on every build** — that is expected; port an upstream
  change in by hand rather than copying the canonical file over it. Everything below the `<style>`
  block is the shared shell untouched, which is what keeps that cheap.
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
- The catalog thumbnail is regenerated by `python3 Tools/make-promo.py`, not hand-edited. It builds
  `promo/promo.png` out of the shipped sprites and the sign face, and asserts the two overlays that
  are invisible while authoring — the host's **Play now** pill and the shelf card's crop. `game.json`
  beside it is the parent catalog's contract, validated by `scripts/lib/game-catalog.mjs` in
  `variant-originals`; **unknown fields are errors** and `genres` is a closed list. See
  [Catalog metadata for the Originals shelf](README.md#catalog-metadata-for-the-originals-shelf)
  for what is declared and what is still unsettled.
- Commits: Conventional Commits, subject ≤50 chars, body explains the why.
