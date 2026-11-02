"""Checks that the white held-block outline shows in every press screenshot from device_session.py --shots.

Counts pure-white pixels (all channels >= 245) in a box around the held block and compares each press with a
screenshot taken while nothing is touched: the outline is pure white, while the arrow marker and the board are not.
On 2026-09-29 every press showed ~12.5k outline pixels in a 400 x 420 box and the idle baseline 0.

Usage: python outline_check.py <shots-folder> <baseline.png> --box x0,y0,x1,y1 [--min 1500]
Take the baseline with `adb exec-out screencap -p > baseline.png` while the level idles. Needs Pillow.
Exit code 1 when a press shows no outline.
"""
import argparse
import glob
import os
import sys

from PIL import Image


def white_pixels(path, box):
    image = Image.open(path).convert("RGB").crop(box)
    pixels = image.load()
    width, height = image.size
    return sum(1 for y in range(height) for x in range(width) if min(pixels[x, y]) >= 245)


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("shots")
    parser.add_argument("baseline")
    parser.add_argument("--box", required=True, help="x0,y0,x1,y1 in device pixels around the held block")
    parser.add_argument("--min", type=int, default=1500, help="extra white pixels that count as an outline")
    options = parser.parse_args()
    box = tuple(int(v) for v in options.box.split(","))

    base = white_pixels(options.baseline, box)
    print(f"baseline: {base} white px")
    missing = 0
    for path in sorted(glob.glob(os.path.join(options.shots, "press_*.png"))):
        extra = white_pixels(path, box) - base
        shown = extra >= options.min
        missing += 0 if shown else 1
        print(f"{os.path.basename(path)}: {extra:+d} white px -> {'outline' if shown else 'NO OUTLINE'}")
    sys.exit(1 if missing else 0)


if __name__ == "__main__":
    main()
