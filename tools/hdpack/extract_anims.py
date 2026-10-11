#!/usr/bin/env python3
# SPDX-License-Identifier: BSD-2-Clause
"""
Extract Ultima Online animation frames from the player's own AnimationFrame*.uop files into
PNGs for an external upscaler (HD pack, step 1 of 3: extract -> upscale -> import).

The output stays on the local machine. It is derived from EA's art: do not commit it or publish
it (the repository and its releases are public).

Layout of <out>/<name>_b<body>_a<action>/:
  dir<d>/frame<ii>.png  single frame, transparent background, original size
  dir<d>_sheet.png      all frames of that direction side by side; every frame is placed so its
                        anchor (the point ClassicUO pins to the tile) is at the same spot of its
                        cell, so the sheet plays back without jitter
  manifest.json         frame sizes, centers and sheet cells, used by the importer

Usage:
  python extract_anims.py --uo "D:/Games/Electronic Arts/Ultima Online Classic" --out D:/uo-hd/input \
      400:0:human_male 28:0:giant_spider 469:0:robe 527:0:platemail
"""

import argparse
import json
import os
import struct
import zlib

from PIL import Image

MAX_DIRECTIONS = 5


def uop_hash(s: str) -> int:
    """ClassicUO.IO.UOFileUop.CreateHash (Bob Jenkins' hashlittle2)."""
    m = 0xFFFFFFFF
    length = len(s)
    ebx = edi = esi = (length + 0xDEADBEEF) & m
    eax = ecx = edx = 0
    b = [ord(c) for c in s]
    i = 0

    def rd(j):
        return b[j] | (b[j + 1] << 8) | (b[j + 2] << 16) | (b[j + 3] << 24)

    while i + 12 < length:
        edi = (rd(i + 4) + edi) & m
        esi = (rd(i + 8) + esi) & m
        edx = (rd(i) - esi) & m
        edx = ((edx + ebx) & m) ^ (esi >> 28) ^ ((esi << 4) & m)
        esi = (esi + edi) & m
        edi = ((edi - edx) & m) ^ (edx >> 26) ^ ((edx << 6) & m)
        edx = (edx + esi) & m
        esi = ((esi - edi) & m) ^ (edi >> 24) ^ ((edi << 8) & m)
        edi = (edi + edx) & m
        ebx = ((edx - esi) & m) ^ (esi >> 16) ^ ((esi << 16) & m)
        esi = (esi + edi) & m
        edi = ((edi - ebx) & m) ^ (ebx >> 13) ^ ((ebx << 19) & m)
        ebx = (ebx + esi) & m
        esi = ((esi - edi) & m) ^ (edi >> 28) ^ ((edi << 4) & m)
        edi = (edi + ebx) & m
        i += 12

    rem = length - i
    if rem <= 0:
        return (esi << 32) | eax

    shifts = [0, 8, 16, 24]
    for k in range(rem - 1, -1, -1):
        v = b[i + k]
        if k >= 8:
            esi = (esi + (v << shifts[k - 8])) & m
        elif k >= 4:
            edi = (edi + (v << shifts[k - 4])) & m
        else:
            ebx = (ebx + (v << shifts[k])) & m

    esi = ((esi ^ edi) - ((edi >> 18) ^ ((edi << 14) & m))) & m
    ecx = ((esi ^ ebx) - ((esi >> 21) ^ ((esi << 11) & m))) & m
    edi = ((edi ^ ecx) - ((ecx >> 7) ^ ((ecx << 25) & m))) & m
    esi = ((esi ^ edi) - ((edi >> 16) ^ ((edi << 16) & m))) & m
    edx = ((esi ^ ecx) - ((esi >> 28) ^ ((esi << 4) & m))) & m
    edi = ((edi ^ edx) - ((edx >> 18) ^ ((edx << 14) & m))) & m
    eax = ((esi ^ edi) - ((edi >> 8) ^ ((edi << 24) & m))) & m

    return (edi << 32) | eax


def read_uop_index(path):
    table = {}
    with open(path, "rb") as f:
        magic, _version, _sig, next_block = struct.unpack("<IIIQ", f.read(20))
        if magic != 0x50594D:
            raise ValueError(f"{path} is not a UOP file")
        while next_block:
            f.seek(next_block)
            count, next_block = struct.unpack("<iQ", f.read(12))
            for _ in range(count):
                off, hlen, clen, dlen, h, _adler, flag = struct.unpack("<qiiiQIh", f.read(34))
                if off:
                    table[h] = (off + hlen, clen, dlen, flag)
    return table


def color16to32(c):
    r, g, b = (c >> 10) & 0x1F, (c >> 5) & 0x1F, c & 0x1F
    return ((r << 3) | (r >> 2), (g << 3) | (g >> 2), (b << 3) | (b >> 2), 255)


def decode_frames(buf):
    """Same as AnimationsLoader.ReadUOPAnimationFrames + ReadSpriteData."""
    fc, data_start = struct.unpack_from("<iI", buf, 32)
    headers = []
    pos = data_start
    for _ in range(fc):
        _group, frame_id = struct.unpack_from("<HH", buf, pos)
        pixel_offset = struct.unpack_from("<I", buf, pos + 12)[0]
        headers.append((pos, frame_id, pixel_offset))
        pos += 16

    max_id = max((h[1] for h in headers), default=0)
    per_dir = max(1, round(max_id / MAX_DIRECTIONS))
    frames = {}

    for start, frame_id, pixel_offset in headers:
        p = start + pixel_offset
        palette = struct.unpack_from("<256H", buf, p)
        sprite = decode_sprite(buf, p + 512, palette, True)
        direction = (frame_id - 1) // per_dir
        index = (frame_id - 1) % per_dir
        if sprite and direction < MAX_DIRECTIONS:
            frames[(direction, index)] = sprite

    return frames, per_dir


def decode_sprite(buf, p, palette, alpha_check):
    """AnimationsLoader.ReadSpriteData. MUL frames keep palette index 0 as opaque black."""
    cx, cy, w, h = struct.unpack_from("<hhhh", buf, p)
    p += 8
    if w <= 0 or h <= 0:
        return None
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    px = img.load()
    header = struct.unpack_from("<I", buf, p)[0]
    p += 4
    while header != 0x7FFF7FFF and p < len(buf):
        run = header & 0x0FFF
        x = (header >> 22) & 0x3FF
        y = (header >> 12) & 0x3FF
        if x & 0x200:
            x -= 0x400
        if y & 0x200:
            y -= 0x400
        x += cx
        y += cy + h
        for k in range(run):
            v = palette[buf[p]]
            p += 1
            if (v or not alpha_check) and 0 <= x + k < w and 0 <= y < h:
                px[x + k, y] = color16to32(v)
        header = struct.unpack_from("<I", buf, p)[0]
        p += 4
    return img, cx, cy


def mul_block_index(body):
    """anim.mul layout (AnimationsLoader): monsters 22 actions, animals 13, people 35; x5 directions."""
    if body < 200:
        return body * 110
    if body < 400:
        return 22000 + (body - 200) * 65
    return 35000 + (body - 400) * 175


def read_mul_frames(uo_dir, body, action):
    idx_path, mul_path = os.path.join(uo_dir, "anim.idx"), os.path.join(uo_dir, "anim.mul")
    if not (os.path.exists(idx_path) and os.path.exists(mul_path)):
        return {}, 0
    frames, per_dir = {}, 0
    with open(idx_path, "rb") as fi, open(mul_path, "rb") as fm:
        for d in range(MAX_DIRECTIONS):
            fi.seek((mul_block_index(body) + action * MAX_DIRECTIONS + d) * 12)
            entry = fi.read(12)
            if len(entry) < 12:
                continue
            pos, size, _ = struct.unpack("<iii", entry)
            if pos < 0 or size <= 0:
                continue
            fm.seek(pos)
            buf = fm.read(size)
            palette = struct.unpack_from("<256H", buf, 0)
            data_start = 512
            count = struct.unpack_from("<I", buf, data_start)[0]
            offsets = struct.unpack_from(f"<{count}I", buf, data_start + 4)
            per_dir = max(per_dir, count)
            for i, o in enumerate(offsets):
                sprite = decode_sprite(buf, data_start + o, palette, False)
                if sprite:
                    frames[(d, i)] = sprite
    return frames, per_dir


def is_gray(c):
    return c[3] and c[0] == c[1] == c[2]


def export(uo_dir, out_dir, body, action, name, tables):
    key = uop_hash(f"build/animationlegacyframe/{body:06d}/{action:02d}.bin")
    source = None
    for path, table in tables:
        if key in table:
            off, clen, dlen, flag = table[key]
            if flag not in (0, 1):
                print(f"  {name}: compression {flag} (BWT) not supported by this tool yet")
                return
            with open(path, "rb") as f:
                f.seek(off)
                raw = f.read(clen)
            frames, per_dir = decode_frames(zlib.decompress(raw) if flag == 1 else raw)
            source = os.path.basename(path)
            break
    else:
        # classic bodies (humans, most monsters, equipment) live in anim.mul
        frames, per_dir = read_mul_frames(uo_dir, body, action)
        source = "anim.mul"

    if not frames:
        print(f"  {name}: body {body} action {action} not found")
        return

    folder = os.path.join(out_dir, f"{name}_b{body}_a{action:02d}")
    manifest = {"body": body, "action": action, "name": name, "source": source, "scale_expected": 2, "directions": []}

    for d in range(MAX_DIRECTIONS):
        items = sorted((i, v) for (dd, i), v in frames.items() if dd == d)
        if not items:
            continue
        os.makedirs(os.path.join(folder, f"dir{d}"), exist_ok=True)

        # anchor = (CenterX, Height + CenterY) in frame pixels (see MobileView.DrawInternal)
        left = max(cx for _, (img, cx, cy) in items)
        right = max(img.width - cx for _, (img, cx, cy) in items)
        up = max(img.height + cy for _, (img, cx, cy) in items)
        down = max(-cy for _, (img, cx, cy) in items)
        cell_w, cell_h = left + right + 2, up + max(down, 0) + 2  # 1 px gutter around each cell

        sheet = Image.new("RGBA", (cell_w * len(items), cell_h), (0, 0, 0, 0))
        entries = []

        for slot, (i, (img, cx, cy)) in enumerate(items):
            file = f"dir{d}/frame{i:02d}.png"
            img.save(os.path.join(folder, file))
            fx = slot * cell_w + 1 + (left - cx)
            fy = 1 + (up - (img.height + cy))
            sheet.paste(img, (fx, fy))
            gray = sum(1 for c in img.getdata() if is_gray(c))
            entries.append({
                "frame": i, "file": file, "width": img.width, "height": img.height,
                "center_x": cx, "center_y": cy, "sheet_x": fx, "sheet_y": fy, "gray_pixels": gray,
            })

        sheet_file = f"dir{d}_sheet.png"
        sheet.save(os.path.join(folder, sheet_file))
        manifest["directions"].append({
            "direction": d, "sheet": sheet_file, "cell_width": cell_w, "cell_height": cell_h,
            "frames_per_direction": per_dir, "frames": entries,
        })

    with open(os.path.join(folder, "manifest.json"), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)

    total = sum(len(d["frames"]) for d in manifest["directions"])
    print(f"  {name}: {total} frames in {len(manifest['directions'])} directions -> {folder}")


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--uo", required=True, help="Ultima Online folder")
    ap.add_argument("--out", required=True, help="output folder (outside the repository)")
    ap.add_argument("anims", nargs="+", help="body:action[:name]")
    args = ap.parse_args()

    tables = []
    for n in range(1, 7):
        p = os.path.join(args.uo, f"AnimationFrame{n}.uop")
        if os.path.exists(p):
            tables.append((p, read_uop_index(p)))

    for spec in args.anims:
        parts = spec.split(":")
        body, action = int(parts[0]), int(parts[1])
        name = parts[2] if len(parts) > 2 else f"body{body}"
        export(args.uo, args.out, body, action, name, tables)


if __name__ == "__main__":
    main()
