# Flappy Voice — art asset prompts

Everything needed to generate the UI overhaul art, hand it to a processing agent, and drop it into
the game. References are in `Docs/art-refs/` — **attach `01`–`04` to every generation request**, or
the style will drift between assets.

- `00-pano-tuner-reference.png` — the real tuner app the top bar is modelled on
- `01-sing-to-play.png` — start overlay, drawn over the live game
- `02-gameplay-idle.png` — gameplay, not singing
- `03-gameplay-singing.png` — gameplay, singing the correct note
- `04-game-over.png` — game over panel

---

## How to use this file

1. **Set the sizes.** Every prompt block has a `SIZE:` line. Change it and nothing else needs to
   change; the numbers below are what the game's geometry actually wants (see *Why these sizes*).
2. **Image models do not honour exact pixel dimensions.** Ask for the *aspect*, generate at whatever
   the model gives you, then resize to the exact `SIZE:` in the ffmpeg step. Never let the model
   letterbox — that bakes bars into the sprite.
3. **Generate cutouts on flat magenta**, not on transparency. Models are unreliable at alpha;
   `#FF00FF` keys out cleanly and appears nowhere in this palette.
4. **One asset per request.** Sheets of several objects come back at inconsistent scale.
5. Run the matching ffmpeg recipe from *Processing* below, then drop the file at its `PATH:`.

### Global style preamble — prepend to every prompt

> Children's storybook mobile game art, soft painterly digital illustration with clean readable
> shapes and gentle cel shading. Warm daylight palette: sky blues, cream, soft greens, golden brass.
> Rounded friendly forms, no harsh outlines, no gradients banding, no text, no watermark, no drop
> shadow on transparent background. Consistent with the attached reference images.

---

## Why these sizes

The game is portrait, `1080×1920` reference canvas, `CanvasScaler` matching **height**. The camera is
orthographic with `orthographicSize 5.5`, so **11 world units fill the screen height** → `1920/11 ≈
175 px per world unit` on a 1080p phone. Authoring at **256 px per world unit** gives ~1.5× headroom
for tablets and for Unity's mip chain, which is why world-space sprite sizes below look large
relative to the reference screenshots.

Hard numbers the art has to respect (from `GameConfig`):

| Constraint | Value | Consequence for art |
|---|---|---|
| Pipe/trumpet body width | 1.4 world units | 358 px at 256 PPU |
| Playfield height | 9 units (`±4.5`) | trumpet tube must cover up to 7.5 units of run-off |
| Gap at run start | ~2.08 units | bell + flare must not visually intrude into the gap |
| Gap fully ramped | ~1.93 units | same, tighter |
| Bird collider radius | 0.42 units | art may exceed this; only the circle kills |
| Tuner dial | 216 px per semitone | letter cell ≈ 199 px wide at canvas scale |
| Tuner strip | 1080×300 px | full canvas width, 24 px below the top edge |

---

## Asset list

| # | Asset | `SIZE:` | Path | Notes |
|---|---|---|---|---|
| 1 | Trumpet bell — top | 512×384 | `Art/Trumpets/bell_top.png` | pivot at gap edge, flare points down |
| 2 | Trumpet bell — bottom | 512×384 | `Art/Trumpets/bell_bottom.png` | mirror of #1, flare points up |
| 3 | Trumpet tube | 384×384 | `Art/Trumpets/tube.png` | **tiles vertically**, seamless top↔bottom |
| 4 | Valve cluster | 256×384 | `Art/Trumpets/valves.png` | optional overlay, breaks up long tubes |
| 5 | Blow burst | 256×256 | `Art/Fx/blow_burst.png` | additive-friendly, white/cream rays |
| 6 | Bird — idle | 512×512 | `Art/Bird/bird_idle.png` | body ≈ 330 px wide, centred |
| 7 | Bird — singing | 512×512 | `Art/Bird/bird_sing.png` | same silhouette, beak open on mic |
| 8 | Bird — dead | 512×512 | `Art/Bird/bird_dead.png` | X eyes, slumped |
| 9 | Music note ×3 | 128×128 | `Art/Fx/note_a.png` … `note_c.png` | one glyph each, no stacks |
| 10 | Sky gradient | 512×2048 | `Art/Bg/sky.png` | stretches horizontally, no features |
| 11 | Far mountains | 2048×1024 | `Art/Bg/far.png` | **tiles horizontally** |
| 12 | Mid hills + castles | 2048×1024 | `Art/Bg/mid.png` | **tiles horizontally** |
| 13 | Near foliage | 2048×512 | `Art/Bg/near.png` | **tiles horizontally**, bottom-aligned |
| 14 | Cloud band | 1024×512 | `Art/Bg/clouds.png` | **tiles horizontally**, transparent |
| 15 | Tuner pill frame | 1024×256 | `Art/Ui/tuner_pill.png` | 9-slice, border 72 px |
| 16 | Tuner safe band | 128×256 | `Art/Ui/safe_band.png` | 9-slice, border 24 px, green, tileable centre |
| 17 | Tuner needle | 32×256 | `Art/Ui/needle.png` | 1 px-crisp vertical line, soft glow |
| 18 | Start sign | 1024×768 | `Art/Ui/start_sign.png` | parchment + vines, **no text** |
| 19 | Game over scroll | 1024×1536 | `Art/Ui/gameover_scroll.png` | parchment, **no text** |
| 20 | Button | 640×160 | `Art/Ui/button.png` | 9-slice, border 48 px, wood |

Text on #18–20 is TextMeshPro at runtime — the arched "SING TO PLAY" lettering in ref 01 is the one
thing to render as art only if you want that exact curve, in which case ask for it as a separate
transparent PNG at 768×256 and I'll place it as an image instead of text.

---

## Prompts

### 1–2. Trumpet bells

```
SIZE: 512×384 px  (aspect 4:3)
PATH: Art/Trumpets/bell_top.png
PROMPT:
<global preamble>
A single golden brass trumpet bell, viewed from the side, angled slightly toward the viewer, the
wide flared opening facing DOWNWARD and filling the bottom edge of the frame. Polished warm brass
with a soft vertical highlight down the middle, a darker amber rim inside the bell mouth, and a
short section of straight tube leaving the top edge. Cute chunky proportions, thick rounded rim,
like a toy instrument. Centred, isolated on a flat magenta #FF00FF background, no shadow.
```

For `bell_bottom.png` swap `DOWNWARD` → `UPWARD` and `bottom edge` → `top edge`. Do **not** just
flip #1 in ffmpeg unless the highlight reads correctly upside down — check it.

### 3. Trumpet tube

```
SIZE: 384×384 px  (square)
PATH: Art/Trumpets/tube.png
PROMPT:
<global preamble>
A straight vertical section of polished golden brass tube, filling the full height of a square
frame, roughly 70% of the frame width, centred. Uniform along its entire length: the same
highlight, the same shading, the same width at the very top and the very bottom edge, so that
stacking copies vertically produces one continuous unbroken pipe. A single soft vertical specular
highlight slightly left of centre, warm amber shadow on the right. No rim, no flare, no valves, no
variation. Flat magenta #FF00FF background.
```

**Verify the vertical loop** (recipe below) — this is the asset most likely to come back with a
seam.

### 4. Valve cluster

```
SIZE: 256×384 px  (aspect 2:3)
PATH: Art/Trumpets/valves.png
PROMPT:
<global preamble>
Three small brass trumpet valve casings with rounded finger buttons, in a row, seen side-on,
attached to nothing — just the valve cluster and its short crossbars, as it would sit on the side
of a trumpet tube. Warm brass, chunky toy proportions, soft highlights. Isolated on flat magenta
#FF00FF, no shadow.
```

### 5. Blow burst

```
SIZE: 256×256 px  (square)
PATH: Art/Fx/blow_burst.png
PROMPT:
<global preamble>
A burst of short straight motion-lines radiating outward in a fan from the bottom-centre of the
frame, cream and pale gold, thick rounded strokes of varying length, like the puff of air leaving a
cartoon trumpet. Bright at the origin, fading at the tips. No smoke, no cloud, no sparkles. On flat
magenta #FF00FF.
```

### 6–8. Bird

```
SIZE: 512×512 px  (square)
PATH: Art/Bird/bird_idle.png
PROMPT:
<global preamble>
A small round chubby yellow songbird in side profile facing right, holding a tiny dark grey
handheld microphone up to its beak with one wing. Big friendly black dot eye, small orange beak,
short tail, soft cel shading, a slightly deeper yellow on the underside. Wings tucked. Calm, cute,
mascot-like. The bird occupies about 65% of the frame, centred, isolated on flat magenta #FF00FF,
no shadow.
```

- `bird_sing.png`: same sentence, replacing *Wings tucked. Calm* with **"Beak open wide singing into
  the microphone, wings spread mid-flap, eye happily squinted. Energetic"**.
- `bird_dead.png`: replace with **"Eyes closed as two small X marks, beak open, body slumped and
  tilted backwards, wings limp, microphone slipping from its wing. Comically defeated, not gory"**.

The three must share one silhouette and one scale — generate them in one session, and if the model
supports it, from the same seed.

### 9. Music notes

```
SIZE: 128×128 px  (square)
PATH: Art/Fx/note_a.png
PROMPT:
<global preamble>
A single dark navy musical eighth note glyph, thick rounded strokes, slight 3D softness, tilted
about 15 degrees, centred and filling most of the frame. Just one note, no staff, no extra marks.
Flat magenta #FF00FF background.
```

`note_b.png` → "a single quarter note"; `note_c.png` → "a single beamed pair of sixteenth notes".

### 10. Sky

```
SIZE: 512×2048 px  (aspect 1:4, tall)
PATH: Art/Bg/sky.png
PROMPT:
<global preamble>
A vertical gradient sky only: deep cornflower blue at the top easing to pale warm cream at the
bottom, perfectly smooth, no clouds, no objects, no horizon line, no texture. The gradient must be
identical across the full width of the image so that it can be stretched horizontally.
```

### 11–13. Parallax layers

```
SIZE: 2048×1024 px  (aspect 2:1)
PATH: Art/Bg/far.png
PROMPT:
<global preamble>
A horizontal band of distant hazy blue-grey mountain silhouettes with soft snow-lit peaks, arranged
as a repeating panorama. Atmospheric, low contrast, no detail — this sits far behind everything.
CRITICAL: the left and right edges must match exactly so the image tiles seamlessly when repeated
horizontally; the mountain at the left edge must be the exact continuation of the mountain at the
right edge. The bottom 15% of the frame is empty, the top 20% is empty. Transparent background
where there are no mountains.
```

- `mid.png`: **"rolling green hills with clusters of small purple-roofed fairytale castle towers and
  flags, and a winding pale blue river"**, medium contrast, same tiling sentence.
- `near.png` at **2048×512**: **"a band of dense rounded treetop foliage in saturated grass green
  with cream flower dots, occupying the bottom two thirds, silhouette-like"**, high contrast, same
  tiling sentence, bottom-aligned.

### 14. Clouds

```
SIZE: 1024×512 px  (aspect 2:1)
PATH: Art/Bg/clouds.png
PROMPT:
<global preamble>
A band of soft fluffy white cumulus clouds spread across the frame with generous empty gaps between
them, painted storybook style with cream highlights and pale blue-grey undersides. CRITICAL: left
and right edges must match exactly for seamless horizontal tiling. Fully transparent everywhere
there is no cloud — no sky colour, no background.
```

### 15–17. Tuner chrome

```
SIZE: 1024×256 px  (aspect 4:1)
PATH: Art/Ui/tuner_pill.png
9-SLICE: 72 px border, all sides
PROMPT:
<global preamble>
A horizontal rounded-rectangle instrument display panel, like the readout window of a tuner: dark
slate blue-grey face, softly inset, with a subtle lighter bevel around the outside edge and a very
slight inner glow. Empty face — no dial, no text, no ticks, no needle. The left and right rounded
caps are ornamental; the middle third must be perfectly uniform so it can be stretched. Isolated on
flat magenta #FF00FF.
```

```
SIZE: 128×256 px
PATH: Art/Ui/safe_band.png
9-SLICE: 24 px border, all sides
PROMPT:
<global preamble>
A vertical band of translucent grass-green light, brightest in the middle and softly faded at its
left and right edges, on flat magenta #FF00FF. Uniform from top to bottom. No border, no outline,
no gradient along the height.
```

```
SIZE: 32×256 px
PATH: Art/Ui/needle.png
PROMPT:
<global preamble>
A single crisp vertical white line, 6 pixels wide, running the full height of a very narrow tall
frame, dead centre, with a faint soft white glow either side of it. Nothing else. Flat magenta
#FF00FF background.
```

### 18–20. Panels

```
SIZE: 1024×768 px  (aspect 4:3)
PATH: Art/Ui/start_sign.png
PROMPT:
<global preamble>
An aged cream parchment sign with soft torn deckled edges and gently rounded corners, hanging
slightly askew, decorated at the top-left and bottom-right corners with clusters of green leaves
and small white five-petal flowers. The parchment face is EMPTY — completely blank, no text, no
lettering, no lines, no illustration, leaving the middle clear for text to be added later. Soft
paper grain. Isolated on flat magenta #FF00FF.
```

- `gameover_scroll.png` at **1024×1536**: **"a tall hanging parchment scroll banner with two small
  dark nail-heads at the top corners, torn deckled edges, decorated along the bottom edge with
  leaves and white flowers"**, same EMPTY-face sentence.
- `button.png` at **640×160**, 9-slice border 48: **"a wide rounded rectangular wooden button plaque
  in warm mid-brown timber, softly bevelled with a lighter top edge and a darker lower edge, plank
  grain running horizontally, empty face, no text"**.

---

## Processing (ffmpeg)

An ffmpeg agent is the right tool for these steps — and only these. It cannot generate art, and it
cannot cleanly matte a soft edge that was drawn against a busy background, which is why the prompts
insist on flat magenta.

**Key out the magenta and premultiply-safe despill:**

```sh
ffmpeg -i raw.png -vf "colorkey=0xFF00FF:0.30:0.12,format=rgba" -y keyed.png
```

Raise `0.30` (similarity) if magenta fringes survive; raise `0.12` (blend) for softer edges. Check
the result over a dark *and* a light backdrop before accepting it.

**Trim to the drawn pixels, then re-pad centred** (so pivots are predictable):

```sh
# 1. find the alpha bounding box — read the crop=W:H:X:Y line it prints
ffmpeg -i keyed.png -vf "alphaextract,cropdetect=limit=0.02:round=2" -f null - 2>&1 | tail -3
# 2. apply it, then scale to the SIZE: from this file
ffmpeg -i keyed.png -vf "crop=W:H:X:Y,scale=512:384:flags=lanczos,format=rgba" -y final.png
```

**Exact size without distortion** (fit inside, pad transparent):

```sh
ffmpeg -i keyed.png -vf \
  "scale=512:384:force_original_aspect_ratio=decrease:flags=lanczos,\
pad=512:384:(ow-iw)/2:(oh-ih)/2:color=0x00000000,format=rgba" -y final.png
```

**Prove a horizontal tile actually loops** — swap the halves; any seam lands in the middle of the
output where it is obvious:

```sh
ffmpeg -i far.png -vf "split[a][b];[a]crop=iw/2:ih:0:0[l];[b]crop=iw/2:ih:iw/2:0[r];[r][l]hstack" \
  -y seamcheck.png
```

Vertical version for `tube.png`: use `vstack` and crop by `ih/2`.

**Sprite sheet** for the three bird poses (Unity slices it by grid):

```sh
ffmpeg -i bird_%d.png -vf "tile=3x1" -y bird_sheet.png
```

### QC gate — reject and regenerate if any of these fail

- any magenta pixel left: `ffmpeg -i final.png -vf "colorkey=0xFF00FF:0.30:0.0,alphaextract" -f null -`
  should report no change in mean brightness vs. the keyed source
- alpha touches all four edges on a cutout → the subject was cropped by the model
- seam visible in `seamcheck.png` for #3, #11, #12, #13, #14
- `tuner_pill.png` / `button.png` / `safe_band.png` middle third not uniform → 9-slice will ripple
- any text, watermark, or drop shadow anywhere

### Fallback if tiling keeps failing

Ask for **one wide panorama per layer** (`4096×1024`) instead of a tile, and I'll scroll it with
mirrored wrap — soft mountains and clouds mirror invisibly and the seam problem disappears
entirely. Say the word and I'll wire that mode instead.

---

## Handing the results back

Drop the finished PNGs at their `PATH:` under `Flappy Voice/Assets/` and tell me. I'll set import
settings (PPU per folder, Full Rect for the 9-slices, borders, pivots, atlas membership), replace
the procedural placeholders, wire the parallax scroller, the singing/idle bird swap, the note and
blow-burst FX, and the two panels — and re-run the QC list above so nothing lands broken.
