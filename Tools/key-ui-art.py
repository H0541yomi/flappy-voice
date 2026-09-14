#!/usr/bin/env python3
"""Keys the magenta out of the raw UI generations and writes the game-ready PNGs.

Raw art is generated on flat #FF00FF (see Docs/asset-prompts.md) because models are
unreliable at alpha. This is the processing pass: key, despill, trim, resize.

    python3 Tools/key-ui-art.py

The key is not a colour-distance threshold. Magenta has G = 0 and R = B = 255, so for a
pixel P = a*F + (1-a)*MAGENTA the green channel alone carries the coverage:

    a = 1 - (min(R, B) - G) / 255

which is exact for a white-ish foreground and never eats the cream, brass or leaf greens
in this palette. The foreground is then un-mixed out of the key colour (despill), so soft
glow edges stay white instead of going pink over a dark backdrop.
"""

import os
from typing import NamedTuple, Optional

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW_DIR = os.path.join(ROOT, "Assets", "Art", "images")
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Ui")

KEY = np.array([255.0, 0.0, 255.0])

# JPEG ringing leaves the flat background a couple of levels off pure magenta and the
# solid interior a couple of levels short of opaque. Snapping both ends keeps the key
# from leaving a faint haze in the "empty" area or a veil over the artwork.
ALPHA_FLOOR = 0.06
ALPHA_CEIL = 0.94

# Drops that arrive with alpha were exported from a lossy source, so the empty area is not empty:
# a pink haze of up to a = 0.19 sits all round the artwork and would survive the trim as a border
# of nothing. Anything under this is not coverage.
HALO_FLOOR = 0.25


class Target(NamedTuple):
    """One raw drop and what it has to become.

    ``size`` is (width, height); the height is derived from the width when ``fit`` is set, so a
    sign is never squashed to hit a round number. ``pad`` instead scales the art to fit inside
    the box and centres it there, leaving the box the exact size. ``out`` defaults to the source
    stem - it exists because raw drops arrive hyphenated and Unity assets here are underscored.
    """

    size: tuple
    rotate: int = 0
    fit: bool = False
    pad: bool = False
    # Drops that already carry alpha are PNGs and skip the key entirely; only the magenta-matted
    # generations are JPEGs. The extension is what decides which path a drop takes.
    extension: str = "jpeg"
    out: Optional[str] = None
    # Overrides ALPHA_FLOOR for one drop. Needed when a generation comes back with a noisier
    # background than the rest: everything here keys its empty area to about a = 0.03, but a drop
    # that lands nearer 0.09 survives the shared floor and leaves a pale rectangle that is
    # invisible against a dark backdrop and obvious on parchment.
    alpha_floor: Optional[float] = None


# The button is rotated a quarter turn first. The raw plaque is portrait, so its brush
# grain runs across what becomes the button's LONG axis: squashed flat, that grain reads
# as vertical streaking down a wide button. Rotated, the grain runs along the length and
# the plank seams divide it crosswise, which is what a wide timber plaque looks like -
# and the squash needed afterwards is 2.2x instead of 7.2x.
TARGETS = {
    "needle": Target(size=(32, 256)),
    "start_sign": Target(size=(1024, None), fit=True),
    "button": Target(size=(640, 160), rotate=90),
    # Padded, not fitted. The two hearts are different drawings and trim to different aspects
    # (the empty outline is the taller of the two), so fitting each to its own bounds would give
    # the HUD two sprite sizes and a heart that changes shape as it empties. Centred inside one
    # box instead, both sprites are 128x128 and the row needs a single rect.
    "heart_full": Target(size=(128, 128), pad=True),
    "heart_empty": Target(size=(128, 128), pad=True),
    # Both of these arrived already cut out, so they only get trimmed and resized. Kept at their
    # own aspect: the small sign's corner flowers would smear under any stretch, and the button
    # is drawn wide already so there is no grain to rotate.
    "sign-small": Target(size=(1024, None), fit=True, extension="png", out="sign_small"),
    "button-primary": Target(size=(640, None), fit=True, extension="png", out="button_primary"),
    # Fitted, not padded: it is the only sprite of its kind, so there is no sibling whose box it
    # has to share, and the drop is already square.
    "X Button": Target(size=(128, None), fit=True, out="x_button", alpha_floor=0.18),
}


def key_magenta(rgb, alpha_floor=ALPHA_FLOOR):
    """RGBA float array in 0..1, magenta matted out and despilled."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    alpha = 1.0 - (np.minimum(r, b) - g) / 255.0
    alpha = np.clip(alpha, 0.0, 1.0)

    alpha = np.where(alpha <= alpha_floor, 0.0, alpha)
    alpha = np.where(alpha >= ALPHA_CEIL, 1.0, alpha)

    # Un-mix the key colour back out: P = a*F + (1-a)*K  =>  F = (P - (1-a)*K) / a
    safe = np.maximum(alpha, 1e-4)[..., None]
    fg = (rgb - (1.0 - alpha[..., None]) * KEY) / safe
    fg = np.clip(fg, 0.0, 255.0)

    # Fully transparent pixels have no recoverable colour; park them on the mean of what
    # survived so bilinear filtering at the edge cannot pull a stray hue in.
    opaque = alpha > 0.0
    fill = fg[opaque].mean(axis=0) if opaque.any() else np.zeros(3)
    fg[~opaque] = fill

    return np.dstack([fg / 255.0, alpha])


def trim(rgba):
    alpha = rgba[..., 3]
    rows = np.where(alpha.max(axis=1) > 0.01)[0]
    cols = np.where(alpha.max(axis=0) > 0.01)[0]
    if len(rows) == 0 or len(cols) == 0:
        return rgba
    return rgba[rows[0]:rows[-1] + 1, cols[0]:cols[-1] + 1]


def load_rgba(path, extension, alpha_floor=ALPHA_FLOOR):
    """Raw drop as an RGBA float array in 0..1, keyed if it needs keying."""
    if extension == "png":
        rgba = np.asarray(Image.open(path).convert("RGBA"), dtype=np.float64) / 255.0
        rgba[..., 3] = np.where(rgba[..., 3] < HALO_FLOOR, 0.0, rgba[..., 3])
        return rgba
    rgb = np.asarray(Image.open(path).convert("RGB"), dtype=np.float64)
    return key_magenta(rgb, alpha_floor)


def process(name, target):
    source_path = os.path.join(RAW_DIR, f"{name}.{target.extension}")
    rgba = trim(load_rgba(source_path, target.extension,
                          target.alpha_floor if target.alpha_floor is not None else ALPHA_FLOOR))

    image = Image.fromarray((rgba * 255.0 + 0.5).astype(np.uint8), mode="RGBA")
    if target.rotate:
        image = image.rotate(target.rotate, expand=True, resample=Image.BICUBIC)

    source_size = image.size
    target_w, target_h = target.size

    if target.pad:
        scale = min(target_w / image.width, target_h / image.height)
        inner = image.resize((round(image.width * scale), round(image.height * scale)), Image.LANCZOS)
        image = Image.new("RGBA", (target_w, target_h), (0, 0, 0, 0))
        image.paste(inner, ((target_w - inner.width) // 2, (target_h - inner.height) // 2))
    else:
        if target.fit:
            target_h = int(round(target_w * image.height / image.width))
        image = image.resize((target_w, target_h), Image.LANCZOS)

    out = os.path.join(OUT_DIR, f"{target.out or name}.png")
    image.save(out)
    print(f"{name}: {source_size[0]}x{source_size[1]} -> {target_w}x{target_h}  {out}")


if __name__ == "__main__":
    os.makedirs(OUT_DIR, exist_ok=True)
    for asset, target in TARGETS.items():
        process(asset, target)
