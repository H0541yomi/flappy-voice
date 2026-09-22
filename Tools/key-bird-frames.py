#!/usr/bin/env python3
"""Keys the white matte out of the bird's idle frames and writes the game-ready sprites.

    python3 Tools/key-bird-frames.py

The idle cycle arrives as opaque RGB on flat white, one PNG per frame, in RAW_DIR. This is
the processing pass: key, despill, and write the sprite `.meta` beside each PNG.

White cannot be keyed the way `key-ui-art.py` keys magenta. Magenta has G = 0, so the green
channel alone carries coverage; white is 255 in every channel, so no single channel separates
it from a bright foreground. Two things stand in for that:

* **The background is found by connectivity, not by colour.** The bird's eye carries a pure
  white catchlight, and a colour test keys it to nothing - a transparent hole in the eye.
  matte is grown inward from the border, so only white joined to the edge is keyed and an
  interior highlight is foreground however white it is.
* **Coverage comes from the darkest channel.** For a pixel P = a*F + (1-a)*WHITE the minimum
  channel falls as coverage rises, and every colour in this bird (yellow B = 83, the beak's
  orange B = 53, the eye darker still) sits well below white there. MIN_FOREGROUND is the
  body yellow's blue channel, so the body solves to a = 1 exactly and anything darker clamps
  to it.

The foreground is then un-mixed out of white (despill), or the soft edges stay pale and read
as a halo against the dark sky the game clears to.
"""

import hashlib
import os

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RAW_DIR = os.path.join(ROOT, "Assets", "Art", "images")
OUT_DIR = os.path.join(ROOT, "Assets", "Art", "Bird")

# Frame 05 is absent from the drop. The cycle is played by index rather than by name, so a
# missing frame is one slightly longer step and not a gap - and the 04 -> 06 delta measures
# the same as an ordinary neighbouring pair, so there is nothing to interpolate.
FRAMES = ["01", "02", "03", "04", "06", "07", "08", "09", "10"]

# Blue channel of the body yellow (#F5CB53). See the module docstring: this is the divisor
# that puts solid body at a = 1.
MIN_FOREGROUND = 83.0

# A pixel this close to white on every channel is a candidate for the flood fill. Kept just
# off 255 because the drop is PNG - there is no JPEG ringing to allow for, only the odd
# rounded level.
WHITE_LEVEL = 250

# How far the soft edge is allowed to reach in from the matte. The drop does not stop at the
# silhouette: it ramps out through ~7 px of salmon before it reaches white, which is a soft
# glow baked into the generation rather than the bird's own colour. A narrower ring leaves
# that ramp fully opaque and the bird wears a pink fringe over the dark sky - which is what
# the first cut of this script did. The shipped bird_idle.png ramps its alpha across the same
# span, so this matches what is already on screen rather than inventing an edge.
EDGE_RING_PX = 8

# Coverage below this is the glow's pale tail, not the artwork, and is thrown away before the
# alpha is rescaled over what is left - the same trick as key-ui-art.py's HALO_FLOOR. Without
# it the outermost couple of pixels keep a low-alpha haze that reads as a halo on parchment
# and as a pale outline against the sky.
HALO_FLOOR = 0.25

# Matches bird_idle.png, which every other bird sprite is authored against. Feeding the
# frames a different value would resize the bird between poses.
SPRITE_PIXELS_TO_UNITS = 330

META = """fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 0
  spriteMeshType: 0
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: {pixels_to_units}
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationMethod: 0
  spriteTessellationDetail: -1
  spriteGeometrySubdivision: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 0
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: WebGL
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID:
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


def stable_guid(name):
    """Unity asset GUID for an output file, derived from its name rather than randomised.

    The scene records the GUID, so a rerun of this script - or a run on another machine, or
    inside the throwaway project copy the headless build uses - has to produce the same one
    or every reference in the scene breaks.
    """
    return hashlib.md5(("flappyvoice/bird/" + name).encode()).hexdigest()


def background_mask(rgb):
    """The flat white matte, as a boolean mask, grown inward from the image border.

    Colour alone cannot do this: the eye's catchlight is pure white too, and keying it leaves
    a transparent hole in the middle of the eye. Only white that joins the border is matte.

    Grown by repeated dilation clipped to the white region rather than by PIL's `floodfill`,
    which silently does nothing on an image built with `Image.fromarray`.
    """
    near_white = rgb.min(axis=2) >= WHITE_LEVEL
    if not near_white[0, 0]:
        raise SystemExit("top-left pixel is not the white matte - is this a raw frame?")

    reached = np.zeros_like(near_white)
    reached[0, :] = near_white[0, :]
    reached[-1, :] = near_white[-1, :]
    reached[:, 0] = near_white[:, 0]
    reached[:, -1] = near_white[:, -1]

    while True:
        grown = dilate(reached, 1) & near_white
        if grown.sum() == reached.sum():
            return reached
        reached = grown


def dilate(mask, iterations):
    """Grows a boolean mask by one pixel in the four directions, `iterations` times.

    Only used to find the antialiased ring just inside the matte, which is a handful of
    pixels wide - small enough that shifting the array beats pulling in scipy for it.
    """
    grown = mask
    for _ in range(iterations):
        padded = np.zeros_like(grown)
        padded[1:, :] |= grown[:-1, :]
        padded[:-1, :] |= grown[1:, :]
        padded[:, 1:] |= grown[:, :-1]
        padded[:, :-1] |= grown[:, 1:]
        grown = grown | padded
    return grown


def key(path):
    """One raw frame to straight-alpha RGBA, with white un-mixed out of the soft edges."""
    rgb = np.asarray(Image.open(path).convert("RGB")).astype(np.float64)

    matte = background_mask(rgb)
    ring = dilate(matte, EDGE_RING_PX) & ~matte

    alpha = np.ones(matte.shape, dtype=np.float64)
    alpha[matte] = 0.0
    coverage = (255.0 - rgb.min(axis=2)) / (255.0 - MIN_FOREGROUND)
    alpha[ring] = np.clip(coverage[ring], 0.0, 1.0)

    # Un-mix white: P = a*F + (1-a)*255, so F = (P - (1-a)*255) / a. Done on the true mix
    # fraction, before the halo floor rescales it - the floor is cosmetic, the mix is physics.
    # Guarded because at low coverage the division amplifies the pale tail into dark specks.
    out = rgb.copy()
    solvable = ring & (alpha > 0.15)
    divisor = alpha[solvable][:, None]
    out[solvable] = (rgb[solvable] - (1.0 - divisor) * 255.0) / divisor

    alpha[ring] = np.clip((alpha[ring] - HALO_FLOOR) / (1.0 - HALO_FLOOR), 0.0, 1.0)

    rgba = np.zeros(rgb.shape[:2] + (4,), dtype=np.uint8)
    rgba[..., :3] = np.clip(out, 0, 255).astype(np.uint8)
    rgba[..., 3] = np.clip(alpha * 255.0, 0, 255).astype(np.uint8)
    return Image.fromarray(rgba, mode="RGBA")


def main():
    for frame in FRAMES:
        name = "bird_idle_{0}.png".format(frame)
        source = os.path.join(RAW_DIR, name)
        if not os.path.exists(source):
            raise SystemExit("missing raw frame: " + source)

        destination = os.path.join(OUT_DIR, name)
        key(source).save(destination)

        with open(destination + ".meta", "w") as handle:
            handle.write(META.format(guid=stable_guid(name),
                                     pixels_to_units=SPRITE_PIXELS_TO_UNITS))

        print("keyed {0}".format(name))


if __name__ == "__main__":
    main()
