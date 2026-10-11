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
from PIL import Image


def load_rgba(path):
    return np.asarray(Image.open(path).convert("RGBA")).astype(np.int32)


def nearest2x(a):
    return np.repeat(np.repeat(a, 2, axis=0), 2, axis=1)


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

    os.makedirs(args.pack, exist_ok=True)
    with open(os.path.join(args.pack, "index.json"), "w", encoding="utf-8") as f:
        json.dump(index, f)

    if problems:
        print(f"{len(problems)} problem(s), originals stay in use for these:")
        for p in problems[:50]:
            print("  " + p)


if __name__ == "__main__":
    main()
