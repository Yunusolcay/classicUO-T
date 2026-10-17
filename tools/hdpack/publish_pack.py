"""Publish the HD world pack to a private GitHub release the launcher installs from.

    python publish_pack.py --src D:/uo-hd/world --hd D:/uo-hd/world_upscaled \
        --repo owner/private-repo [--tag hd-world]

Runs import_world.py into a staging folder laid out like the ClassicUO folder
(Data/HD/world/...), writes manifest.xml (the ManifestCreator format the launcher reads) and
hd-world.zip, and uploads both to the release (created if missing) with the GitHub CLI (`gh`,
logged in with access to the repository). The launcher then downloads only changed images.

The pack is derived from EA's art: the repository must be private and is never this one.
"""

import argparse
import hashlib
import os
import shutil
import subprocess
import sys
import tempfile
import time
import zipfile
from xml.sax.saxutils import quoteattr

ZIP_NAME = "hd-world.zip"
PACK_DIR = os.path.join("Data", "HD", "world")


def md5(path):
    h = hashlib.md5()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def gh(*args, check=True):
    result = subprocess.run(["gh", *args], capture_output=True, text=True)
    if check and result.returncode != 0:
        sys.exit(f"gh {' '.join(args[:2])} failed: {result.stderr.strip()}")
    return result


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--src", required=True, help="folder written by extract_art.py (has manifest.json)")
    ap.add_argument("--hd", required=True, help="upscaled images, same sub folders and names")
    ap.add_argument("--repo", required=True, help="private GitHub repository, owner/name")
    ap.add_argument("--tag", default="hd-world", help="release tag the launcher reads (default hd-world)")
    ap.add_argument("--keep-land", action="store_true", help="passed on to import_world.py")
    ap.add_argument("--no-upload", action="store_true", help="only build manifest.xml and the zip")
    ap.add_argument("--out", help="keep manifest.xml and the zip in this folder")
    args = ap.parse_args()

    view = gh("repo", "view", args.repo, "--json", "visibility", "-q", ".visibility", check=False)
    if not args.no_upload and view.stdout.strip().upper() != "PRIVATE":
        sys.exit(f"{args.repo} is not a private repository (visibility: {view.stdout.strip() or view.stderr.strip()})")

    work = tempfile.mkdtemp(prefix="hdpack_")
    try:
        root = os.path.join(work, "client")
        pack = os.path.join(root, PACK_DIR)
        cmd = [sys.executable, os.path.join(os.path.dirname(os.path.abspath(__file__)), "import_world.py"),
               "--src", args.src, "--hd", args.hd, "--pack", pack]
        if args.keep_land:
            cmd.append("--keep-land")
        subprocess.run(cmd, check=True)

        files = []
        for dirpath, _, names in os.walk(root):
            for name in names:
                full = os.path.join(dirpath, name)
                files.append((os.path.relpath(full, root).replace(os.sep, "/"), full))
        files.sort()
        if not files:
            sys.exit("import_world.py wrote no files")

        version = time.strftime("%Y.%m.%d.%H%M")
        out = args.out or work
        os.makedirs(out, exist_ok=True)
        manifest = os.path.join(out, "manifest.xml")
        archive = os.path.join(out, ZIP_NAME)

        with open(manifest, "w", encoding="utf-8") as f:
            f.write('<?xml version="1.0" encoding="utf-8" standalone="yes"?>\n<releases>\n')
            f.write(f"\t<release name={quoteattr(ZIP_NAME)} version={quoteattr(version)}>\n\t\t<files>\n")
            for rel, full in files:
                f.write(f"\t\t\t<file filename={quoteattr(rel)} hash={quoteattr(md5(full))} />\n")
            f.write("\t\t</files>\n\t</release>\n</releases>\n")

        # PNGs are already compressed: store them
        with zipfile.ZipFile(archive, "w", zipfile.ZIP_STORED) as z:
            for rel, full in files:
                z.write(full, rel)

        size = os.path.getsize(archive) / (1 << 20)
        print(f"{len(files)} files, {size:.0f} MB, version {version}")

        if args.no_upload:
            return

        if gh("release", "view", args.tag, "-R", args.repo, check=False).returncode != 0:
            gh("release", "create", args.tag, "-R", args.repo, "--title", "HD world pack",
               "--notes", "Installed by the classicUO-T launcher. Private: derived from EA's Ultima Online art.")

        # the archive first: a launcher reading in between sees the old manifest and at worst
        # reports a hash mismatch for a changed image, never installs a wrong one
        gh("release", "upload", args.tag, archive, "-R", args.repo, "--clobber")
        gh("release", "upload", args.tag, manifest, "-R", args.repo, "--clobber")
        print(f"published to {args.repo} release {args.tag}")
    finally:
        shutil.rmtree(work, ignore_errors=True)


if __name__ == "__main__":
    main()
