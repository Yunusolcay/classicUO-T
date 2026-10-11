#!/usr/bin/env python3
# SPDX-License-Identifier: BSD-2-Clause
"""
Extract Ultima Online world graphics (land tiles, texmaps, statics) from the player's own files
into PNGs for an external upscaler (HD pack: extract -> upscale -> import).

The output is derived from EA's art: keep it on this machine, never commit or publish it.

<out>/
  texmaps/tex_XXXX_3x3.png    texmap tiled 3x3 (192x192 or 384x384): the upscaler sees the texture
                              surrounded by itself, the importer keeps the centre, so the result
                              still tiles without seams
  land/<kind>/land_XXXX.png   44x44 land diamond, transparent corners
  statics/ground/static_XXXX.png   walkable floors: dungeons, mines, caves, houses, paths, lava
  statics/nature/static_XXXX.png   trees, leaves, grass, flowers (same rules as NatureTiles.cs)
  statics/other/<range>/static_XXXX.png   everything else, 1000 ids per folder
  manifest.json

Usage:
  python extract_art.py --uo "D:/Games/Electronic Arts/Ultima Online Classic" --out D:/uo-hd/world
      [--only texmaps,land,ground,nature,other]
"""

import argparse
import hashlib
import json
import os
import re
import struct

from PIL import Image

from extract_anims import color16to32, read_uop_index, uop_hash

TILE_FOLIAGE_IGNORED = True  # the foliage flag is mostly ship sails; names decide (see NatureTiles.cs)

TREE_WORDS = {"tree", "trees", "leaves", "foliage"}
PLANT_WORDS = {"grass", "grasses", "fern", "ferns", "flowers", "weed", "weeds", "reeds", "cattails",
               "bulrushes", "wheat", "bush", "shrub", "shrubs"}
EXCLUDED_WORDS = {"sail", "sails", "ship", "pot", "flowerpot", "bed", "flowerbed", "mat", "tile", "dried",
                  "bushel", "sheaf", "sheaves", "wall", "stump", "log", "logs", "vase", "urn", "painting",
                  "deed", "seed", "seeds", "mosaic", "xmas", "christmas", "holiday", "pillow", "bonsai",
                  "hair", "coral", "frame", "fram", "ornament", "ornam", "ribon", "ribbon", "light", "lights",
                  "angel", "snowflake", "artisan", "garland", "bowl", "clippings", "planter", "pedestal",
                  "pedes", "statue", "softened", "partialhue", "decorative", "bark", "mini", "potted",
                  "pumpkin", "shea"}
LAND_KINDS = ["grass", "forest", "jungle", "dirt", "sand", "snow", "rock", "cave", "swamp", "lava", "water",
              "acid", "obsidian", "brick", "wood", "stone", "marble", "tile", "cobble", "void"]
WET = 0x80
SURFACE = 0x200
GROUND_PATTERN = re.compile(r"floor|cave|mine|flagston|cobble|paver|plank|tile|dirt|sand|swamp|mud|lava|ice|"
                            r"rock|stone|brick|marble|ground|path|road|stairs|steps|crack|rubble|grass mat", re.I)
NOT_GROUND_PATTERN = re.compile(r"table|roof|counter|chest|shelf|bench|chair|altar|pedestal|statue|sign|door", re.I)


def read_tiledata(uo):
    d = open(os.path.join(uo, "tiledata.mul"), "rb").read()
    land, statics = [], []
    off = 0
    for _ in range(512):
        off += 4
        for _ in range(32):
            flags, tex = struct.unpack_from("<QH", d, off)
            land.append((flags, tex, d[off + 10:off + 30].split(b"\0")[0].decode("latin1").strip()))
            off += 30
    while off + 4 <= len(d):
        off += 4
        for _ in range(32):
            if off + 41 > len(d):
                break
            flags = struct.unpack_from("<Q", d, off)[0]
            statics.append((flags, d[off + 21:off + 41].split(b"\0")[0].decode("latin1").strip()))
            off += 41
    return land, statics


def is_nature(flags, name):
    if not name or "hair" in name.lower() or flags & WET:
        return False
    words = {w.lower() for w in re.split(r"[ _\-%/\\.,']+", name) if w}
    if words & EXCLUDED_WORDS:
        return False
    return bool(words & TREE_WORDS or words & PLANT_WORDS)


def is_ground(flags, name):
    """Walkable floor statics: dungeon and mine floors, house floors, paths, lava and so on."""
    return bool(name) and bool(flags & SURFACE) and bool(GROUND_PATTERN.search(name))         and not NOT_GROUND_PATTERN.search(name)


def land_kind(name):
    n = name.lower()
    for k in LAND_KINDS:
        if k in n:
            return k
    return "other"


def decode_land(raw):
    """ArtLoader.Diamond: 1012 pixels in a 44x44 diamond, colour 0 is black (no transparency inside)."""
    img = Image.new("RGBA", (44, 44), (0, 0, 0, 0))
    px = img.load()
    at = 0
    for i in range(22):
        start = 22 - (i + 1)
        for j in range(start, start + ((i + 1) << 1)):
            px[j, i] = color16to32(raw[at] | (raw[at + 1] << 8))
            at += 2
    for i in range(22):
        for j in range(i, i + ((22 - i) << 1)):
            px[j, i + 22] = color16to32(raw[at] | (raw[at + 1] << 8))
            at += 2
    return img


def decode_static(buf):
    """ArtLoader.LoadArt + Runs: per-row gap/run pairs; gaps are transparency, colour 0 too."""
    _flags, width, height = struct.unpack_from("<Ihh", buf, 0)
    if width <= 0 or height <= 0 or width > 1024 or height > 1024:
        return None
    body = buf[8:]
    line_offsets = struct.unpack_from(f"<{height}H", body, 0)
    data_start = height * 2
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    px = img.load()
    y = 0
    p = data_start + line_offsets[0] * 2
    x = 0
    while y < height and p + 4 <= len(body):
        xoffs, run = struct.unpack_from("<HH", body, p)
        p += 4
        if xoffs + run >= 2048:
            break
        if xoffs + run != 0:
            x += xoffs
            for _ in range(run):
                v = body[p] | (body[p + 1] << 8)
                p += 2
                if v and x < width:
                    px[x, y] = color16to32(v)
                x += 1
        else:
            x = 0
            y += 1
            if y < height:
                p = data_start + line_offsets[y] * 2
    return img


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--uo", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--only", default="texmaps,land,ground,nature,other")
    args = ap.parse_args()
    parts = set(args.only.split(","))

    land_td, static_td = read_tiledata(args.uo)
    manifest = {"scale_expected": 2, "texmaps": [], "land": [], "statics": []}
    # identical images are upscaled once; later copies point at the first file ("same_as")
    seen = {}

    def unique(kind, img):
        key = (kind, img.size, hashlib.sha1(img.tobytes()).hexdigest())
        return seen.setdefault(key, None)

    def remember(kind, img, rel):
        seen[(kind, img.size, hashlib.sha1(img.tobytes()).hexdigest())] = rel

    if "texmaps" in parts:
        idx = open(os.path.join(args.uo, "texidx.mul"), "rb").read()
        with open(os.path.join(args.uo, "texmaps.mul"), "rb") as f:
            for i in range(len(idx) // 12):
                off, length, _ = struct.unpack_from("<iii", idx, i * 12)
                if off < 0 or length <= 0:
                    continue
                size = 64 if length == 0x2000 else 128
                f.seek(off)
                raw = f.read(size * size * 2)
                tex = Image.new("RGBA", (size, size))
                tex.putdata([color16to32(raw[k] | (raw[k + 1] << 8)) for k in range(0, len(raw), 2)])
                first = unique("tex", tex)
                if first:
                    manifest["texmaps"].append({"id": i, "same_as": first, "size": size, "tiled": 3})
                    continue
                tiled = Image.new("RGBA", (size * 3, size * 3))
                for ty in range(3):
                    for tx in range(3):
                        tiled.paste(tex, (tx * size, ty * size))
                rel = f"texmaps/tex_{i:04X}_3x3.png"
                os.makedirs(os.path.join(args.out, "texmaps"), exist_ok=True)
                tiled.save(os.path.join(args.out, rel))
                remember("tex", tex, rel)
                manifest["texmaps"].append({"id": i, "file": rel, "size": size, "tiled": 3})
        print(f"texmaps: {sum(1 for t in manifest['texmaps'] if 'file' in t)} unique of {len(manifest['texmaps'])}")

    uop = None
    if parts & {"land", "ground", "nature", "other"}:
        uop_path = os.path.join(args.uo, "artLegacyMUL.uop")
        uop = (open(uop_path, "rb"), read_uop_index(uop_path))

    def read_art(index):
        f, table = uop
        e = table.get(uop_hash(f"build/artlegacymul/{index:08d}.tga"))
        if not e:
            return None
        off, clen, dlen, flag = e
        f.seek(off)
        return f.read(clen)

    if "land" in parts:
        for i in range(0x4000):
            raw = read_art(i)
            if not raw or len(raw) < 2024:
                continue
            name = land_td[i][2] if i < len(land_td) else ""
            img = decode_land(raw)
            first = unique("land", img)
            if first:
                manifest["land"].append({"id": i, "same_as": first, "name": name, "texmap": land_td[i][1]})
                continue
            kind = land_kind(name)
            rel = f"land/{kind}/land_{i:04X}.png"
            os.makedirs(os.path.join(args.out, "land", kind), exist_ok=True)
            img.save(os.path.join(args.out, rel))
            remember("land", img, rel)
            manifest["land"].append({"id": i, "file": rel, "name": name, "texmap": land_td[i][1]})
        print(f"land: {sum(1 for t in manifest['land'] if 'file' in t)} unique of {len(manifest['land'])}")

    if parts & {"ground", "nature", "other"}:
        count = {"ground": 0, "nature": 0, "other": 0}
        for g in range(len(static_td)):
            flags, name = static_td[g]
            group = "ground" if is_ground(flags, name) else "nature" if is_nature(flags, name) else "other"
            if group not in parts:
                continue
            raw = read_art(g + 0x4000)
            if not raw:
                continue
            img = decode_static(raw)
            if img is None or img.getbbox() is None:
                continue
            first = unique("static", img)
            if first:
                manifest["statics"].append({"id": g, "same_as": first, "name": name, "group": group})
                continue
            sub = group if group != "other" else f"other/{(g // 1000) * 1000:05X}"
            rel = f"statics/{sub}/static_{g:04X}.png"
            os.makedirs(os.path.join(args.out, "statics", sub), exist_ok=True)
            img.save(os.path.join(args.out, rel))
            remember("static", img, rel)
            manifest["statics"].append({"id": g, "file": rel, "name": name, "group": group,
                                        "width": img.width, "height": img.height})
            count[group] += 1
        print(f"statics: {count}")

    with open(os.path.join(args.out, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=1)


if __name__ == "__main__":
    main()
