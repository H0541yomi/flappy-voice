#!/usr/bin/env python3
"""Render promo/promo.png, the master thumbnail the Variant Originals catalog publishes from.

Run: python3 Tools/make-promo.py

The board is composed from the game's own shipped sprites and its own font rather than authored
by hand, so it cannot drift from what the game actually looks like: re-run it after an art change
and the thumbnail follows. Output is 1400x900 -- a 350x225 CSS artboard at 4x, which is the size
the Originals shelf publishes from.

Two constraints from the catalog are load-bearing and are asserted rather than eyeballed:
the host draws a "Play now" pill over PLAY_PILL, and the card's crop can clip outside SAFE_X, so
the title and the action beat have to stay clear of both. Only atmosphere may run through them.
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

REPO_ROOT = Path(__file__).resolve().parent.parent
ART = REPO_ROOT / "Assets" / "Art"
OUTPUT = REPO_ROOT / "promo" / "promo.png"

# 350x225 CSS at 4x device scale, per scripts/promo/README.md in the parent catalog.
WIDTH, HEIGHT = 1400, 900

# The host's "Play now" pill, in board pixels. Nothing essential may sit inside it.
PLAY_PILL = (440, 680, 960, 872)
# The shelf card crops a hair off each side; essential art stays inside this band.
SAFE_X = (80, 1320)

# Assets/Editor/SceneBuilder.cs is the source of truth for both of these.
INK = (90, 58, 28)  # InkColor #5A3A1C
PARCHMENT = (237, 224, 189)  # the parchment the signs are drawn on

TITLE = "Flappy Song"
TITLE_FONT = ART / "Fonts" / "IMFellGreatPrimerSC-Regular.ttf"


def load(relative_path):
    """Load one of the game's sprites as RGBA, so every layer composites the same way."""
    return Image.open(ART / relative_path).convert("RGBA")


def scaled_to_width(image, width):
    """Resize a sprite to a target width, keeping its aspect. Layers are placed by width because
    the parallax art is authored as wide strips and only its horizontal coverage varies."""
    height = max(1, round(image.height * width / image.width))
    return image.resize((width, height), Image.LANCZOS)


def paste_layer(board, image, center_x, bottom_y):
    """Composite a parallax strip by its bottom edge, repeating it to both board edges.

    Placed by the bottom because the strips are drawn with their content on the bottom and
    transparency above, and tiled because they are authored to repeat -- the game scrolls them,
    and a strip merely centred here leaves a bare wedge in whichever corner it falls short of.
    """
    top_y = round(bottom_y - image.height)
    left_x = round(center_x - image.width / 2)
    while left_x > 0:
        left_x -= image.width
    for x in range(left_x, WIDTH, image.width):
        board.alpha_composite(image, (x, top_y))


def build_sky():
    """Stretch the sky gradient across the board. The sprite is a tall portrait ramp; the board
    only ever shows a band of it in game, so a slice is truer than squashing the whole ramp."""
    sky = load("Bg/sky.png")
    top = round(sky.height * 0.18)
    bottom = round(sky.height * 0.80)
    return sky.crop((0, top, sky.width, bottom)).resize((WIDTH, HEIGHT), Image.LANCZOS)


def draw_trumpet(board, center_x, gap_top, gap_bottom, tube_width, bell_width):
    """Draw one trumpet pair the way the game builds it: a tube tiled off both board edges with a
    flared bell seated on each gap rim. The gap is the mechanic -- it admits exactly one note."""
    tube = scaled_to_width(load("Trumpets/tube.png"), tube_width)
    tube_x = round(center_x - tube_width / 2)
    for top_edge, height in ((0, gap_top), (gap_bottom, HEIGHT - gap_bottom)):
        # Tiled into an exact-height column rather than straight onto the board: a final tile
        # allowed to overrun would close the gap the bird is supposed to fly through.
        column = Image.new("RGBA", (tube_width, height), (0, 0, 0, 0))
        for offset_y in range(0, height, tube.height):
            column.alpha_composite(tube.crop((0, 0, tube_width, min(tube.height, height - offset_y))),
                                   (0, offset_y))
        board.alpha_composite(column, (tube_x, top_edge))

    # The bells overhang the tube on both sides, so they go on last and cover the tube's cut end.
    for relative_path, bell_top_edge in (
        ("Trumpets/bell_top.png", None),
        ("Trumpets/bell_bottom.png", gap_bottom),
    ):
        bell = scaled_to_width(load(relative_path), bell_width)
        top_edge = gap_top - bell.height if bell_top_edge is None else bell_top_edge
        board.alpha_composite(bell, (round(center_x - bell_width / 2), top_edge))


def draw_title(board):
    """Set the game's name in the sign face. Parchment on ink rather than the app's ink on
    parchment: the title floats over the sky here, where ink alone would disappear."""
    draw = ImageDraw.Draw(board)
    font = ImageFont.truetype(str(TITLE_FONT), 134)
    left, top, right, bottom = draw.textbbox((0, 0), TITLE, font=font)
    x = (WIDTH - (right - left)) / 2 - left
    y = 62 - top

    # A heavy outline, not a drop shadow: the board is cropped and scaled down to 350x225, where a
    # soft shadow turns to mud but a hard edge still separates the letters from the sky.
    for offset_x in range(-7, 8):
        for offset_y in range(-7, 8):
            if offset_x * offset_x + offset_y * offset_y <= 49:
                draw.text((x + offset_x, y + offset_y), TITLE, font=font, fill=INK)
    draw.text((x, y), TITLE, font=font, fill=PARCHMENT)
    return draw.textbbox((x, y), TITLE, font=font)


def assert_clear_of_overlays(name, box):
    """Fail the render rather than ship a board whose focal element lands under the host's chrome.
    The pill and the crop are invisible while authoring and only show up on a real shelf."""
    left, top, right, bottom = box
    pill_left, pill_top, pill_right, pill_bottom = PLAY_PILL
    if left < pill_right and right > pill_left and top < pill_bottom and bottom > pill_top:
        raise SystemExit(f"{name} {box} overlaps the Play now pill {PLAY_PILL}")
    if left < SAFE_X[0] or right > SAFE_X[1]:
        raise SystemExit(f"{name} {box} leaves the safe band x{SAFE_X}")


def main():
    """Compose the board and write it. One action beat and one title, per the catalog's house
    style: the bird singing its way into a trumpet's gap says the whole mechanic without copy."""
    board = build_sky()

    # Same vertical order the parallax uses in game, compressed into a landscape band: clouds
    # high, mountains behind the hills, bushes cropped along the bottom edge.
    paste_layer(board, scaled_to_width(load("Bg/clouds.png"), 1300), 660, 400)
    paste_layer(board, scaled_to_width(load("Bg/far.png"), 2400), 700, 645)
    # Offset so a castle does not sit directly behind the bird and break its silhouette.
    paste_layer(board, scaled_to_width(load("Bg/mid.png"), 2000), 300, 830)
    paste_layer(board, scaled_to_width(load("Bg/near.png"), 1300), 700, 1010)

    # A tight gap, near the proportion the game plays at: opened up it stops reading as one slot
    # the bird has to thread and turns into two unrelated bells.
    draw_trumpet(board, center_x=1130, gap_top=300, gap_bottom=520, tube_width=185, bell_width=470)

    # The notes lead the eye along the line the bird is about to fly, and climb as they go: the
    # game's one idea is that pitch is height, so they may as well say it.
    for relative_path, (note_x, note_y, size) in zip(
        ("Fx/note_c.png", "Fx/note_b.png", "Fx/note_a.png"),
        ((760, 398, 54), (838, 370, 64), (918, 340, 76)),
    ):
        board.alpha_composite(scaled_to_width(load(relative_path), size), (note_x, note_y))

    bird = scaled_to_width(load("Bird/bird_sing.png"), 430)
    bird_box = (350, 235, 350 + bird.width, 235 + bird.height)
    board.alpha_composite(bird, (bird_box[0], bird_box[1]))

    title_box = draw_title(board)

    assert_clear_of_overlays("bird", bird_box)
    assert_clear_of_overlays("title", title_box)

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    board.convert("RGB").save(OUTPUT, "PNG", optimize=True)
    print(f"wrote {OUTPUT.relative_to(REPO_ROOT)} ({board.width}x{board.height})")


if __name__ == "__main__":
    main()
