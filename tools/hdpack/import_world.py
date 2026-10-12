#!/usr/bin/env python3
# SPDX-License-Identifier: BSD-2-Clause
"""
HD pack, step 3 of 3: check upscaled world graphics and install them as an HD pack ClassicUO
loads from <client>/Data/HD/world (Options > "HD world textures when zoomed in").

For every image extract_art.py wrote (manifest.json in --src), the upscaled one in --hd must be
exactly 2x. Then:
  texmaps   keep the centre copy of the 3x3 tiling (seamless by construction)
  land      force the exact 88x88 diamond: alpha comes from the original, never from the upscaler
  statics   alpha from the original (no halo, no lost or extra pixels), as the game picks objects
            by the original silhouette anyway
  all       pixels that are exact gray (R = G = B) in the original are snapped back to exact gray,
            because the game recolors them (hues) by their gray level

Flat land tiles are then made from the texmaps (unless --keep-land): the game draws sloped land
with the texmap laid over the diamond, so a flat tile drawn the same way looks exactly like the
hills next to it and joins its neighbours like they do. Only where the original texmap has the
same pattern as the original land tile (not for water and other tiles without a texmap of their
own); the other land tiles keep their own upscaled image, if there is one. Texmaps that are not
upscaled yet are used as they are, so all flat tiles of a terrain get the same tone and no
patchwork of upscaled and original tiles appears while a pack is still incomplete.
Images that are missing or the wrong size are skipped and reported; the game then keeps using the
original for them. Identical originals ("same_as") share one file.

The pack is derived from EA's art: it stays on this machine and is never committed or published.

Usage:
  python import_world.py --src D:/uo-hd/world --hd D:/uo-hd/world_output \
      --pack "D:/Games/classicUO-T/ClassicUO/Data/HD/world"
"""

import argparse
import json
import os

import numpy as np
from PIL import Image, ImageFilter


def load_rgba(path):
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.int32)


def nearest2x(a):
    return np.repeat(np.repeat(a, 2, axis=0), 2, axis=1)


FLAT_TEXMAP_LIGHT = 0.85355339


def project_texmap(tex, size):
    """A texmap (N x N) drawn on a flat land diamond of size x size, like Batcher2D.DrawStretchedLand:
    texture u runs from the top corner to the right one, v from the top corner to the left one."""
    n = tex.shape[0]
    s = size / 44.0
    y, x = np.mgrid[0:size, 0:size].astype(float) + 0.5
    x, y = x / s, y / s
    u = (x - 22 + y) / 44 * n - 0.5
    v = (y - (x - 22)) / 44 * n - 0.5
    u0, v0 = np.floor(u).astype(int), np.floor(v).astype(int)
    fu, fv = (u - u0)[..., None], (v - v0)[..., None]
    t = tex.astype(float)

    def at(a, b):
        return t[b % n, a % n]

    return (at(u0, v0) * (1 - fu) * (1 - fv) + at(u0 + 1, v0) * fu * (1 - fv)
            + at(u0, v0 + 1) * (1 - fu) * fv + at(u0 + 1, v0 + 1) * fu * fv)


def box_blur(a, r=5):
    img = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8))
    return np.asarray(img.filter(ImageFilter.BoxBlur(r))).astype(float)


def same_pattern(land, tex):
    """True when the original texmap, laid over the diamond, shows the same large-scale pattern as
    the original land tile (colors may differ, transitions must be in the same place)."""
    p = project_texmap(tex[..., :3], 44)
    mask = land[..., 3] > 0
    inner = np.asarray(Image.fromarray((mask * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(11))) > 0
    a = box_blur(p) - p[mask].mean(0)
    b = box_blur(land[..., :3]) - land[..., :3][mask].mean(0)
    color = np.abs(p[mask].mean(0) - land[..., :3][mask].mean(0)).max()
    return np.percentile(np.abs(a - b)[inner].mean(-1), 95) <= 15 and color <= 35


def snap_gray(out, src2x):
    """Where the original pixel is opaque exact gray, make the result exact gray too."""
    gray = (src2x[..., 3] > 0) & (src2x[..., 0] == src2x[..., 1]) & (src2x[..., 1] == src2x[..., 2])
    luma = np.clip(np.round(out[..., 0] * 0.299 + out[..., 1] * 0.587 + out[..., 2] * 0.114), 0, 255)
    for c in range(3):
        out[..., c] = np.where(gray, luma, out[..., c])
    return int(gray.sum())


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--src", required=True, help="folder written by extract_art.py (has manifest.json)")
    ap.add_argument("--hd", required=True, help="upscaled images, same sub folders and names")
    ap.add_argument("--pack", required=True, help="<ClassicUO folder>/Data/HD/world")
    ap.add_argument("--keep-land", action="store_true",
                    help="use the upscaled land tiles as they are, do not make them from texmaps")
    args = ap.parse_args()

    manifest = json.load(open(os.path.join(args.src, "manifest.json"), encoding="utf-8"))
    index = {"version": 1, "scale": 2, "land": {}, "texmaps": {}, "statics": {}}
    installed = {}  # source rel -> pack rel
    problems = []

    def install(kind, entry):
        rel = entry["file"]
        if rel in installed:
            return installed[rel]
        hd_path = os.path.join(args.hd, rel)
        if not os.path.exists(hd_path):
            return None
        src = load_rgba(os.path.join(args.src, rel))
        out = load_rgba(hd_path)
        if out.shape[:2] != (src.shape[0] * 2, src.shape[1] * 2):
            problems.append(f"{rel}: size {out.shape[1]}x{out.shape[0]}, expected {src.shape[1]*2}x{src.shape[0]*2}")
            return None

        if kind == "texmaps":
            n = entry["size"]
            src = src[n:2 * n, n:2 * n]
            out = out[2 * n:4 * n, 2 * n:4 * n].copy()
            out[..., 3] = 255
        src2x = nearest2x(src)
        if kind != "texmaps":
            out[..., 3] = np.where(src2x[..., 3] > 0, 255, 0)
            out[..., :3] = np.where(out[..., 3:4] > 0, out[..., :3], 0)
        snap_gray(out, src2x)

        name = f"{entry['id']:04X}.png"
        pack_rel = f"{kind}/{name}"
        os.makedirs(os.path.join(args.pack, kind), exist_ok=True)
        Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA").save(os.path.join(args.pack, pack_rel), optimize=True)
        installed[rel] = pack_rel
        return pack_rel

    for kind in ("texmaps", "land", "statics"):
        entries = manifest[kind]
        first_file = {}
        for e in entries:
            if "file" in e:
                first_file[e["file"]] = e
        done = 0
        for e in entries:
            src_entry = e if "file" in e else first_file.get(e.get("same_as"))
            if src_entry is None:
                continue
            pack_rel = install(kind, src_entry)
            if pack_rel:
                index[kind][str(e["id"])] = pack_rel
                done += 1
        print(f"{kind}: {done} of {len(entries)} ids have an HD image")

    if not args.keep_land:
        land_from_texmaps(args, manifest, index)

    os.makedirs(args.pack, exist_ok=True)
    with open(os.path.join(args.pack, "index.json"), "w", encoding="utf-8") as f:
        json.dump(index, f)

    if problems:
        print(f"{len(problems)} problem(s), originals stay in use for these:")
        for p in problems[:50]:
            print("  " + p)


def land_from_texmaps(args, manifest, index):
    tex_entry = {e["id"]: e for e in manifest["texmaps"]}
    tex_first = {e["file"]: e for e in manifest["texmaps"] if "file" in e}
    land_first = {e["file"]: e for e in manifest["land"] if "file" in e}
    os.makedirs(os.path.join(args.pack, "land"), exist_ok=True)
    made = {}  # texmap (pack file, or source file when not upscaled) -> land pack file
    checked = {}  # (land file, texmap file) -> same pattern
    done = from_original = 0
    for e in manifest["land"]:
        tex_id = e.get("texmap", 0)
        tex_pack = index["texmaps"].get(str(tex_id))
        land_file = e.get("file") or e.get("same_as")
        if tex_id == 0 or tex_id not in tex_entry or land_file not in land_first:
            continue
        t = tex_entry[tex_id]
        tex_file = t.get("file") or t.get("same_as")
        key = (land_file, tex_file)
        if key not in checked:
            n = tex_first[tex_file]["size"]
            src_tex = load_rgba(os.path.join(args.src, tex_file))[n:2 * n, n:2 * n]
            checked[key] = same_pattern(load_rgba(os.path.join(args.src, land_file)), src_tex)
        if not checked[key]:
            continue
        key_tex = tex_pack or tex_file
        if key_tex not in made:
            if tex_pack:
                tex = load_rgba(os.path.join(args.pack, tex_pack))
                pack_rel = "land/t" + os.path.basename(tex_pack)
            else:
                n = tex_first[tex_file]["size"]
                tex = load_rgba(os.path.join(args.src, tex_file))[n:2 * n, n:2 * n]
                pack_rel = f"land/o{tex_first[tex_file]['id']:04X}.png"
            out = np.zeros((88, 88, 4), dtype=np.int32)
            # IsometricWorld.fx get_light: land drawn from a texmap is lit, a level tile at
            # (cos(45) / 2) + 0.5 whatever the terrain shadow level; flat art is not lit at all
            out[..., :3] = np.round(project_texmap(tex[..., :3], 88) * FLAT_TEXMAP_LIGHT).astype(np.int32)
            # every land tile has the same diamond outline
            diamond = nearest2x(load_rgba(os.path.join(args.src, land_file)))[..., 3] > 0
            out[..., 3] = np.where(diamond, 255, 0)
            out[..., :3] = np.where(diamond[..., None], out[..., :3], 0)
            Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA").save(os.path.join(args.pack, pack_rel), optimize=True)
            made[key_tex] = pack_rel
        index["land"][str(e["id"])] = made[key_tex]
        done += 1
        from_original += tex_pack is None
    print(f"land from texmaps: {done} ids ({len(made)} images, {from_original} ids from texmaps not upscaled yet); "
          f"{len(index['land'])} land ids in total")


if __name__ == "__main__":
    main()
