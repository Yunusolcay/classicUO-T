#!/usr/bin/env python3
# SPDX-License-Identifier: BSD-2-Clause
"""
HD pack, optional step between 1 and 2: rank the extracted world graphics by how often they appear
on the maps, and copy the most used ones into a smaller batch folder to upscale first.

Counts every land cell, every sloped (texmap) land cell and every static of all maps in the UO
folder. Graphics that extract_art.py wrote once for several ids ("same_as") are counted together.
Writes <src>/usage.json (all images, most used first) and copies the batch into --out with the same
sub folders, so the upscaled batch can be imported with import_world.py like the full set. Images
in --skip (an earlier batch or already upscaled output) are left out of the batch.

Usage:
  python rank_world.py --uo "C:/Games/Ultima Online Classic" --src D:/uo-hd/world \
      --out D:/uo-hd/world_batch1 --coverage 0.95
"""

import argparse
import json
import os
import shutil
import struct
from collections import Counter

import numpy as np

from extract_anims import read_uop_index, uop_hash
from extract_art import read_tiledata

# MapLoader.MapsDefaultSize
MAP_SIZES = [(7168, 4096), (7168, 4096), (2304, 1600), (2560, 2048), (1448, 1448), (1280, 4096)]
WET = 0x80


def read_map(uo, i):
    """The map as land ids and z, shape (width, height); None if the map is missing."""
    path = os.path.join(uo, f"map{i}LegacyMUL.uop")
    if os.path.exists(path):
        table = read_uop_index(path)
        parts = []
        with open(path, "rb") as f:
            for n in range(len(table)):
                e = table.get(uop_hash(f"build/map{i}legacymul/{n:08d}.dat"))
                if e is None:
                    break
                f.seek(e[0])
                parts.append(f.read(e[1]))
        data = b"".join(parts)
    elif os.path.exists(os.path.join(uo, f"map{i}.mul")):
        data = open(os.path.join(uo, f"map{i}.mul"), "rb").read()
    else:
        return None

    w, h = MAP_SIZES[i]
    bw, bh = w // 8, h // 8
    blocks = min(len(data) // 196, bw * bh)
    raw = np.frombuffer(data[:blocks * 196], dtype=np.uint8).reshape(blocks, 196)[:, 4:]
    cells = raw.reshape(blocks, 64, 3)
    ids = (cells[..., 0].astype(np.uint16) | (cells[..., 1].astype(np.uint16) << 8))
    zs = cells[..., 2].view(np.int8)
    bw = blocks // bh
    # block index = bx * bh + by, cell index = cy * 8 + cx
    ids = ids[:bw * bh].reshape(bw, bh, 8, 8).transpose(0, 3, 1, 2).reshape(bw * 8, bh * 8)
    zs = zs[:bw * bh].reshape(bw, bh, 8, 8).transpose(0, 3, 1, 2).reshape(bw * 8, bh * 8)
    return ids, zs


def count_statics(uo, i, counter):
    idx_path = os.path.join(uo, f"staidx{i}.mul")
    st_path = os.path.join(uo, f"statics{i}.mul")
    if not (os.path.exists(idx_path) and os.path.exists(st_path)):
        return
    idx = np.frombuffer(open(idx_path, "rb").read(), dtype=np.int32)
    idx = idx[:len(idx) // 3 * 3].reshape(-1, 3)
    st = open(st_path, "rb").read()
    ids = []
    for lookup, length, _ in idx:
        if lookup < 0 or length <= 0 or lookup + length > len(st):
            continue
        rec = np.frombuffer(st, dtype=np.uint8, count=length // 7 * 7, offset=lookup).reshape(-1, 7)
        ids.append(rec[:, 0].astype(np.uint16) | (rec[:, 1].astype(np.uint16) << 8))
    if ids:
        all_ids = np.concatenate(ids)
        for g, c in zip(*np.unique(all_ids, return_counts=True)):
            counter[int(g)] += int(c)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--uo", required=True, help="Ultima Online folder")
    ap.add_argument("--src", required=True, help="folder written by extract_art.py (has manifest.json)")
    ap.add_argument("--out", help="batch folder to copy the most used images into")
    ap.add_argument("--coverage", type=float, default=0.95,
                    help="share of all appearances of each group the batch must cover (default 0.95)")
    ap.add_argument("--skip", action="append", default=[], help="folder of images already done (repeatable)")
    args = ap.parse_args()

    land_td, _ = read_tiledata(args.uo)
    texmap_valid = set()
    tex_idx = open(os.path.join(args.uo, "texidx.mul"), "rb").read()
    for t in range(len(tex_idx) // 12):
        lookup, length, _ = struct.unpack_from("<iii", tex_idx, t * 12)
        if lookup >= 0 and length > 0:
            texmap_valid.add(t)

    land_count, tex_count, static_count = Counter(), Counter(), Counter()
    for i in range(len(MAP_SIZES)):
        m = read_map(args.uo, i)
        if m is None:
            continue
        ids, zs = m
        # Land.ApplyStretch: sloped cells (any of the right, lower and lower-right neighbours at
        # another z) are drawn with the land tile's texmap, flat cells with the land art
        z = zs.astype(np.int16)
        zr = np.roll(z, -1, 0)
        zd = np.roll(z, -1, 1)
        zrd = np.roll(zd, -1, 0)
        sloped = (z != zr) | (z != zd) | (z != zrd)
        for g, c in zip(*np.unique(ids[~sloped], return_counts=True)):
            land_count[int(g)] += int(c)
        for g, c in zip(*np.unique(ids[sloped], return_counts=True)):
            g = int(g)
            flags, tex, _ = land_td[g] if g < len(land_td) else (0, 0, "")
            if tex in texmap_valid and not (tex == 0 and flags & WET):
                tex_count[tex] += int(c)
            else:
                land_count[g] += int(c)
        count_statics(args.uo, i, static_count)
        print(f"map{i}: {ids.shape[0]}x{ids.shape[1]}, {int(sloped.sum())} sloped cells")

    manifest = json.load(open(os.path.join(args.src, "manifest.json"), encoding="utf-8"))
    counters = {"land": land_count, "texmaps": tex_count, "statics": static_count}
    usage = {}  # file -> uses
    groups = {}  # file -> group name
    for kind, counter in counters.items():
        first = {e["file"]: e for e in manifest[kind] if "file" in e}
        for e in manifest[kind]:
            f = e.get("file") or e.get("same_as")
            if f not in first:
                continue
            n = counter.get(e["id"], 0)
            if n:
                usage[f] = usage.get(f, 0) + n
            groups[f] = kind if kind != "statics" else "statics/" + first[f].get("group", "other")

    ranked = sorted(usage.items(), key=lambda kv: -kv[1])
    with open(os.path.join(args.src, "usage.json"), "w", encoding="utf-8") as f:
        json.dump([{"file": k, "uses": v, "group": groups[k]} for k, v in ranked], f, indent=0)

    skip = set()
    for folder in args.skip:
        for root, _, files in os.walk(folder):
            for name in files:
                skip.add(os.path.relpath(os.path.join(root, name), folder).replace("\\", "/"))

    batch = []
    print(f"\n{'group':18s} {'used':>6s} {'unused':>7s} {'batch':>6s}  coverage")
    for group in ("land", "texmaps", "statics/ground", "statics/nature", "statics/other"):
        items = [(k, v) for k, v in ranked if groups[k] == group]
        total = sum(v for _, v in items)
        all_files = sum(1 for g in groups.values() if g == group)
        covered, picked = 0, 0
        for k, v in items:
            if covered >= total * args.coverage:
                break
            covered += v
            picked += 1
            if k not in skip:
                batch.append(k)
        share = covered / total if total else 0
        print(f"{group:18s} {len(items):6d} {all_files - len(items):7d} {picked:6d}  {share:.1%}")
    print(f"batch: {len(batch)} images (without {len(skip)} already done)")

    if args.out:
        for rel in batch:
            dst = os.path.join(args.out, rel)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            shutil.copy2(os.path.join(args.src, rel), dst)
        print(f"copied to {args.out}")


if __name__ == "__main__":
    main()
