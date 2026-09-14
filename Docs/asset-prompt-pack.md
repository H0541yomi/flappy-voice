# Flappy Voice — ChatGPT prompt pack (route B)

Sampled from `Docs/art-refs/01–04`. **Attach `01`–`04` to every request.** Paste one block at a
time (one asset per request). The PREAMBLE already contains the palette — always send it first.

---

## PREAMBLE — prepend to every prompt

> Children's storybook mobile game art, soft painterly digital illustration with clean readable
> shapes and gentle cel shading. Rounded friendly forms, no harsh outlines, no gradient banding, no
> text, no watermark, no drop shadow. Match the style, palette and character look of the four
> attached reference images.
>
> PALETTE — use only these families:
> - Sky: cornflower `#5B8FD1` → mid blue `#86B4DC` → pale cream `#F4E7C8`
> - Clouds: white `#F7F1E2`, cool underside `#C9D6E0`
> - Brass (trumpets): highlight `#F2D678`, base `#E4C05C`, mid `#C4A24E`, amber shadow `#8A6A2E`
> - Greens: saturated foliage `#4FA24E`, foliage shadow `#3C7A45`, soft hills `#7FA96B`
> - Bird: body yellow `#F6D34A`, underside `#E7B93A`, orange beak `#E8963C`, black eye `#1A1A1A`
> - Ink / notes: navy `#26324A`
> - UI slate (tuner): face `#3E5162`, light bevel `#5B7181`
> - Parchment: face `#EFE3C2`, edge shadow `#C9B589`; wood button: base `#8A5A32`, light edge
>   `#A9743F`, dark edge `#5E3B20`; safe-band green `#6FD06A`
> - Cutout background is flat magenta `#FF00FF` only — it appears nowhere else in the palette.

---

## Cutouts (flat magenta background)

**1. bell_top** — brass palette
> A single golden brass trumpet bell, side view, angled slightly toward viewer, wide flared opening
> facing DOWNWARD filling the bottom edge. Polished warm brass with a soft vertical highlight down
> the middle, darker amber rim inside the bell mouth, short straight tube leaving the top edge.
> Chunky toy proportions, thick rounded rim. Centred, isolated on flat magenta `#FF00FF`, no shadow.

**2. bell_bottom** — same as #1 but flared opening faces UPWARD filling the TOP edge, tube leaves
bottom edge. Check the highlight still reads right way up.

**3. tube** (seamless vertical tile)
> A straight vertical section of polished golden brass tube filling the full frame height, ~70% of
> frame width, centred. Perfectly uniform along its whole length — identical width, highlight and
> shading at the very top and very bottom edges so stacked copies form one unbroken pipe. Single
> soft specular highlight just left of centre, warm amber shadow on the right. No rim, no flare, no
> valves. Flat magenta `#FF00FF`.

**4. valves** — dropped, do not regenerate. The generated one had hard black outlines that
matched nothing else, and long tubes read fine without it.

**5. blow_burst**
> A fan of short straight motion-lines radiating from bottom-centre, cream and pale gold, thick
> rounded strokes of varying length, like a puff of air from a cartoon trumpet. Bright at origin,
> fading at tips. No smoke, no cloud, no sparkles. Flat magenta `#FF00FF`.

**6. bird_idle**
> A small round chubby yellow (`#F6D34A`) songbird in side profile facing right, beak closed. Big
> black dot eye, small orange beak (`#E8963C`), short tail, soft cel shading, deeper yellow
> underside. Both wings tucked neatly against its sides. Calm, cute, mascot-like. ~65% of frame,
> centred, isolated on flat magenta `#FF00FF`, no shadow.

**7. bird_sing** — same bird & scale, but: beak open wide singing upward, wings spread mid-flap, eye
happily squinted. Energetic.

**8. bird_dead** — same bird & scale, but: eyes closed as two small X marks, beak open, body slumped
and tilted backwards, both wings limp at its sides. Comically defeated, not gory.

> Generate 6–8 in one session (same seed if possible) so silhouette and scale match.

**9. note_a**
> A single dark navy (`#26324A`) musical eighth note glyph, thick rounded strokes, slight 3D
> softness, tilted ~15°, centred, filling most of the frame. One note only, no staff. Flat magenta
> `#FF00FF`.

**note_b** → "a single quarter note"; **note_c** → "a single beamed pair of sixteenth notes". Same
navy, same style.

**15. tuner_pill** (9-slice, uniform middle)
> A horizontal rounded-rectangle instrument display panel, like a tuner readout window: dark slate
> blue-grey face (`#3E5162`), softly inset, subtle lighter bevel (`#5B7181`) around the edge, slight
> inner glow. Empty face — no dial, text, ticks or needle. Ornamental left/right caps; the middle
> third perfectly uniform so it can stretch. Flat magenta `#FF00FF`.

**16. safe_band**
> A vertical band of translucent grass-green (`#6FD06A`) light, brightest in the middle, softly
> faded at left and right edges, on flat magenta `#FF00FF`. Uniform top to bottom. No border, no
> outline, no vertical gradient.

**17. needle**
> A single crisp vertical white line 6 px wide, full frame height, dead centre, with a faint soft
> white glow on each side. Nothing else. Flat magenta `#FF00FF`.

**18. start_sign**
> An aged cream parchment sign (`#EFE3C2`) with soft torn deckled edges (shadow `#C9B589`) and
> rounded corners, hanging slightly askew, decorated top-left and bottom-right with green leaves
> (`#4FA24E`) and small white five-petal flowers. Face completely BLANK — no text, lettering, lines
> or illustration. Soft paper grain. Flat magenta `#FF00FF`.

**19. gameover_scroll** — tall hanging parchment scroll banner, two small dark nail-heads at top
corners, torn deckled edges, leaves and white flowers along the bottom edge, same BLANK face.

**20. button** (9-slice)
> A wide rounded rectangular wooden button plaque in warm mid-brown timber (`#8A5A32`), softly
> bevelled with a lighter top edge (`#A9743F`) and darker lower edge (`#5E3B20`), horizontal plank
> grain, empty face, no text. Flat magenta `#FF00FF`.

**21. heart_full** (lives HUD)
> A single plump storybook heart, seen straight on, filling ~80% of a SQUARE frame, centred. Soft
> painterly cherry red — highlight `#F07A6A`, body `#D9544C`, deeper shadow `#9C3630` on the lower
> right — gentle cel shading, one small soft cream highlight on the upper left lobe. Chunky rounded
> toy proportions, wide lobes, short blunt bottom point, symmetrical. No outline, no gloss streak,
> no sparkles, no face, no ribbon. Flat magenta `#FF00FF`.

**22. heart_empty** — same heart, same size, position and silhouette, but spent: no red at all, flat
muted slate blue-grey (face `#5B7181`, shadow `#3E5162` lower right), clearly darker and lower
contrast than #21 so the two read apart at thumbnail size. No crack, no X, no dashed outline.

> Generate 21 and 22 back to back in one session (same seed) — the two sit side by side in the HUD,
> so any silhouette drift shows.

> On **Gemini Flash**: attach `01`–`04`, ask for a **1:1 square**, and send the two as two turns of
> one chat — "now the same heart, drained of colour" — rather than two fresh prompts, so it reuses
> the shape. It renders at 1024 px; the processing pass downsizes to 256.

---

## Backgrounds & tiles

**10. sky** (transparent/opaque, no magenta)
> A vertical gradient sky only: cornflower `#5B8FD1` at top easing to pale cream `#F4E7C8` at bottom,
> perfectly smooth, no clouds, objects, horizon or texture. Identical across the full width so it
> stretches horizontally.

**11. far** (seamless horizontal tile, transparent)
> A horizontal band of distant hazy blue-grey mountain silhouettes with soft snow-lit peaks, as a
> repeating panorama. Atmospheric, low contrast, no detail. CRITICAL: left and right edges match
> exactly for seamless horizontal tiling — the mountain at the left edge continues the one at the
> right edge. Bottom 15% and top 20% empty. Transparent where there are no mountains.

**12. mid** — same tiling rule: rolling green hills (`#7FA96B`) with clusters of small purple-roofed
fairytale castle towers and flags, and a winding pale blue river. Medium contrast, transparent
background.

**13. near** — same tiling rule, bottom-aligned: a band of dense rounded treetop foliage in
saturated grass green (`#4FA24E`) with cream flower dots, occupying the bottom two thirds,
silhouette-like, high contrast, transparent.

**14. clouds** (seamless horizontal tile, transparent)
> A band of soft fluffy white cumulus clouds (`#F7F1E2`, cool underside `#C9D6E0`) with generous
> empty gaps, storybook style. CRITICAL: left and right edges match exactly for seamless horizontal
> tiling. Fully transparent everywhere there is no cloud — no sky colour.

> Tiles #3/#11/#12/#13/#14 are the risky ones. If ChatGPT can't loop them, ask for one wide
> panorama each (`4096×1024`) and I'll mirror-scroll instead.

---

When you have the raw PNGs, save them anywhere and tell me the folder. I run all ffmpeg keying /
trim / resize / seam-check / QC and place each at its `PATH:` from `asset-prompts.md`.
