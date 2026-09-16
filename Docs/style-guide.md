# Style guide — colour, type and layout

Authoritative for how the game **looks**. Every value here is a constant in
`Assets/Editor/SceneBuilder.cs` unless the table says otherwise, and the scene is generated from
it (see [the scene is generated](../CLAUDE.md#the-scene-is-generated)) — so this document
describes the source, not the `.unity` file. Change the constant, rebuild, commit.

Two things make the whole look hang together, and both are one-lookup rules rather than
conventions you have to remember at each call site:

- `NewText` resolves the font, so **every** string in the app is in the sign face.
- `InkColor` / `MutedInkColor` / `ButtonLabelColor` are the only text colours. There is one
  deliberate exception, below.

## Colour

The art is drawn in one ink on one parchment, so the UI uses the art's own colours rather than a
palette of its own.

### Text

| Constant | Hex | Used for |
|---|---|---|
| `InkColor` | `#501713` | Everything on parchment: titles, bodies, consent copy, the final score |
| `MutedInkColor` | `#7B423E` | Quiet lines — `SCORE` / `BEST` captions, the start hint |
| `ButtonLabelColor` | `#FBD97B` | Plaque labels on **timber** |
| — | `#FFFFFF` | The in-run `ScoreLabel`, and only that |

Two rules that are easy to break:

- **White is invisible on parchment.** `NewText` leaves text white, so anything placed on a sign
  must set a colour. Check a new label against the sign, not the editor's dark background.
- **The in-run score is the one exception**, because it floats over the playfield rather than
  sitting on a sign. The brass `YES!` plaque is light, so *its* label is ink, not gold — gold is
  for dark timber.

### Surfaces

| What | Hex / alpha | Notes |
|---|---|---|
| Parchment placeholder | `#EDE0BD` | Under `start_sign.png` / `sign_small.png`; visible only if a sprite fails to load |
| Plaque placeholder | `#8C6138` | Same idea under `button.png` |
| Panel dim | black, `0.55` | Behind every panel. Blocks raycasts: a tap that misses a button must not reach the game |
| `SkyColor` | `#293357` | Camera clear, behind the painted parallax |
| New-best badge | `#FFC738`, label `#1F1A0D` | The one saturated accent in the app |

### Tuner (`TunerBarUI` serialized fields, not `SceneBuilder`)

| Field | Hex | Meaning |
|---|---|---|
| `safeColor` | `#5CD982` | Needle in tune, and the safe band |
| `offTuneColor` | `#ED454F` | Needle off tune, and silence |
| `inRangeLabelColor` | `#FFFFFF` | A note the player can reach |
| `outOfRangeLabelColor` | `#FFB847` | A note outside the playable octave |
| Strip backdrop / legend pill | `#0A0D17` at `0.72` / `#1A1F2E` at `0.94` | Dark because the strip sits over the sky, not over parchment |

## Typography

Two generated TMP font assets, both output of `Assets/Editor/FontBuilder.cs`
(**Flappy Voice → Build Font Asset**) from committed `.ttf` files:

| Asset | Face | Vocabulary | Used by |
|---|---|---|---|
| `IMFellGreatPrimerSC SDF` | IM FELL Great Primer SC | printable ASCII | every string, via `SceneBuilder.NewText` |
| `Gulzar SDF` | Gulzar (Latin lining figures) | `0123456789` | the three scores, via `ApplyNumberFace` |

Both atlases are baked and left in `AtlasPopulationMode.Static`: the vocabularies are known up
front, and Dynamic would rasterise glyphs on the player's main thread on Web.

**Numerals are Gulzar; words are not.** `ApplyNumberFace` is applied after `NewText` on exactly
three labels — the in-run `ScoreLabel`, the end screen's `FinalScore`, and the `Value` half of the
best-score row. Gulzar's digits are lining where IM Fell's are old-style, so the two inside one
string read as a mistake; that is why `BEST 108` is a **row of two labels** (`Caption` in the sign
face, `Value` in the number face, baseline-aligned) rather than one formatted string. A mixed
string that needs a numeral — the tuner's `A4`, the cents readout — stays wholly in the sign face.

### Sizes

Canvas is 1080×1920 and matches on **height**, so these are real pixels on a portrait phone.

| Element | Size | Notes |
|---|---|---|
| `ScoreLabel` (in-run) | 170 | Gulzar, white |
| `FinalScore` | 145 | Gulzar, `FontStyles.Bold` |
| Sign titles (`GAME OVER`, `SING TO PLAY`) | 74 / 70 | `ApplyTitleFace`, tracking 2 |
| Consent / notice title | `56 × ConsentDesignScale` | ≈ 73 |
| Start hint | 62 | Muted ink |
| Tuner dial letters | 68 | Over the strip |
| `BEST` + value | 50 + 50 | Tracking 4 on the word only |
| Tuner legend letters | 50 | On the start sign |
| Mic notice message | `38 × ConsentDesignScale` | ≈ 50; three lines where the consent panel has one |
| `SCORE` caption | 46 | Tracking 6 |
| Badge label | 40 | On the accent colour |
| Consent / notice plaques | `32 × ConsentDesignScale` | ≈ 42 |
| Sign plaques (`Play Again`, `Share`) | 56 | `NewButton`'s default |
| Tuner readouts | 32 | Note name and cents |

### Weight

TMP cannot synthesise a real bold from a single-weight face, so heavier text is **a material
asset** — `Assets/Art/Ui/SignTitle.mat`, created by `EnsureTitleMaterial` with
`_FaceDilate 0.22`, applied through `ApplyTitleFace`. Never a material *instance*: TMP marks the
ones it creates `HideAndDontSave`, so an instance reverts on scene save and the titles silently go
back to normal weight.

## Layout

### Signs

The parchment is **not 9-sliced** — its corner flowers would smear — so both panels are authored
at their sprite's own aspect and content lives inside a measured face band:

| Sign | Size | Face band (from the sign's top) |
|---|---|---|
| `start_sign.png` (`SignSize`) | 860 × 1567 (aspect 1866/1024) | `SignFaceTop` 0.219 → `SignFaceBottom` 0.812 |
| `sign_small.png` (`SmallSignSize`) | 820 × 973 (aspect 1215/1024) | `SmallSignFaceTop` 0.110 → `SmallSignFaceBottom` 0.928 |

Outside that band the deckled edge is tapering in and things hang off the parchment. Text is also
capped at ~700 px wide (620 on the small sign) because the leaf clusters cut into the top-right and
bottom-left corners of the face.

### Consent panels and the microphone notice

The layout came from Figma; its proportions did not. Widths and type sizes scale straight across by
`ConsentDesignScale` (`820/626`), but **heights come across as fractions of the design frame** —
`ConsentTitleCenterFromTop` 0.3255, `ConsentBodyTopFromTop` 0.4037,
`ConsentButtonRowCenterFromTop` 0.6847, `MicNoticeMessageCenterFromTop` 0.432 — because the design
stretches the parchment to a 1.401 aspect where the sprite's own is 1.187 and it is not 9-sliced.

Plaques sit in a `HorizontalLayoutGroup` sized for two (`ConsentButtonSize`, 228 × 57 design
pixels, on `button.png`'s own 4:1 aspect), so a lone button centres itself. **A headless build
never ticks a canvas**, so every such row needs
`LayoutRebuilder.ForceRebuildLayoutImmediate` or the saved scene keeps the children stacked at the
row's centre.

Which plaque sprite a button gets tracks **whether there is a competing option**, not whether the
answer is yes: timber (`button.png`) for `OK`, `NO` and `EXIT`; brass (`button_primary.png`) only
where there is another answer beside it to outweigh — the consent flow's `YES!` and the microphone
notice's `OK`.

### The top of the screen

The tuner strip owns it, and everything else is measured off the strip rather than authored:

| Constant | Value |
|---|---|
| `TunerTopMarginPx` | 20 |
| `TunerBarHeightPx` | 180 |
| `TunerBarWidthPx` | 600 (a centred pill, not a full-width bar) |
| `ScoreLabelGapPx` | 40 — the gap below the strip to the in-run score |
| `ScoreLabelTopFromTop` | derived: `20 + 180 + 40` |

`HudLayout.CameraForPlayfield` also derives the camera from `TunerScreenFraction`, so retuning the
strip's height or margin moves the score **and** reframes the playfield. Do not hand-tune
`orthographicSize`, and do not replace `ScoreLabelTopFromTop` with a literal.

### The quit button

`QuitButtonSizePx` 88 at `QuitButtonMarginPx` 36 in the canvas's top-right corner — of the
**screen**, never on a panel. Two clearances are load-bearing and neither has slack: the tuner pill
leaves it ~38 px at 9:19.5 (the tightest aspect a phone ships), and the end screen's share capture
rect stops 53 px below it, which is the only reason the shared card has no X in its corner.
`SceneBuilder` builds it last so it stays above the panels' blocking dim.

## Art

Raw drops are keyed, never hand-edited: `python3 Tools/key-ui-art.py` turns the magenta JPEGs in
`Assets/Art/images/` into the PNGs in `Assets/Art/Ui/`, writing each sprite `.meta` by hand
alongside the PNG so a headless build and the open Editor agree on the GUID. `m_DefaultBehaviorMode`
is 3D, so a meta that omits `textureType: 8` imports as a plain texture and
`LoadAssetAtPath<Sprite>` quietly returns null.

`ALPHA_FLOOR` is per-drop overridable, because a noisier generation keys its empty area to ~0.09
rather than the usual ~0.03 and survives the shared 0.06 floor — invisible on a dark backdrop, an
obvious pale rectangle on parchment. Which is the standing rule for anything new: **check it on the
parchment.**

## The loading screen

The one surface that is not Unity's. `Assets/WebGLTemplates/Variant/index.html` is the parent
catalog's shared Web shell with its colour and type replaced, because it is the first thing a
player sees and the shared one is a neutral dark page in a monospace face. It borrows from the
tables above rather than inventing anything:

| Element | Value | Taken from |
|---|---|---|
| Page and canvas background | `#293357` | `SkyColor`, what the camera clears to |
| Wordmark | `#EDE0BD`, the sign face at `clamp(30px, 8vw, 52px)` | Parchment, and `sign-face.ttf` — the same TTF `FontBuilder` bakes |
| Progress track | `rgba(10,13,23,.72)`, pill | The tuner strip backdrop |
| Progress fill | `#5CD982` | `TunerBarUI.safeColor` — in tune |
| Load failure, and the loader's warning banner | `#EDE0BD` card, `#501713` text, `#7B423E` detail | Parchment, `InkColor`, `MutedInkColor` |

Two things this buys, both of which are the point rather than decoration: the last frame the page
paints is the colour Unity's first frame clears to, so the handover is a wordmark going out rather
than a background changing; and the wordmark is title case, not upper, because the face is
small-caps already and `toUpperCase()` throws that away.

Changing a colour here means changing it in the table above too — the shell cannot read
`SceneBuilder`. And `build.mjs` warns on every build that this file has drifted from the canonical
shared one, which is expected: port upstream changes in by hand.
