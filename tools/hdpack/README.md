# HD pack tools

Build a local HD pack for "HD world textures when zoomed in" (Options). Requires Python 3 with
Pillow and NumPy (`pip install pillow numpy`).

**The pack is made from EA's Ultima Online art. Every player builds it from their own client
files; it is never committed to this repository or published in its releases.**

1. Extract the world graphics from your UO folder:

   ```
   python extract_art.py --uo "C:/Games/Ultima Online Classic" --out D:/uo-hd/world
   ```

   Writes `texmaps/` (each texture tiled 3x3), `land/<kind>/`, `statics/ground|nature|other/` and
   `manifest.json`. Identical images are written once.

   Optional: there are ~42,000 images, but a small part covers most of the world. This counts how
   often each image appears on the maps (all land, slopes and statics) and copies the most used
   ones, 95% of all appearances per group (~1,400 images), into a batch folder to upscale first:

   ```
   python rank_world.py --uo "C:/Games/Ultima Online Classic" --src D:/uo-hd/world \
       --out D:/uo-hd/world_batch1 --coverage 0.95
   ```

   A later batch with `--coverage 0.98 --skip D:/uo-hd/world_batch1` takes the next ones.

2. Upscale every PNG to exactly 2x with the tool of your choice into a second folder with the same
   sub folders and file names (keep transparency, keep gray areas neutral gray, keep tile edges).

3. Check and install the result into the ClassicUO folder:

   ```
   python import_world.py --src D:/uo-hd/world --hd D:/uo-hd/world_upscaled \
       --pack "<ClassicUO folder>/Data/HD/world"
   ```

   Wrong sizes or missing files are reported and keep using the original graphic. Texmaps keep
   the centre of the 3x3 tiling, land tiles and statics get the original transparency back, and
   exact gray pixels are snapped back to gray so hues still work.

   Flat land tiles are made from the upscaled texmaps, the way the game draws hills, so flat
   ground and slopes look the same and join without lines (`--keep-land` turns this off). Land
   tiles whose texmap has another pattern (water, for example) keep their own upscaled image, so
   for land only the texmaps really need upscaling.

4. In game: Options > "HD world textures when zoomed in". The HD graphics are used from 1.25x
   magnification; at the default zoom one art pixel is one screen pixel and the originals are drawn.

5. Optional, for a group of players: publish the pack to a **private** GitHub repository and let the
   launcher install it (Settings > HD pack repository and access key; a fine-grained token with
   read-only Contents access to that repository):

   ```
   python publish_pack.py --src D:/uo-hd/world --hd D:/uo-hd/world_upscaled --repo owner/private-repo
   ```

   The script refuses a repository that is not private. Run it again after new images are ready;
   the launcher only downloads the changed files.

`extract_anims.py` does the same for animation frames (not used by the client yet).
