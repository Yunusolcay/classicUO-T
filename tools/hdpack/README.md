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

4. In game: Options > "HD world textures when zoomed in". The HD graphics are used from 1.25x
   magnification; at the default zoom one art pixel is one screen pixel and the originals are drawn.

`extract_anims.py` does the same for animation frames (not used by the client yet).
