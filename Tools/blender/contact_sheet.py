#!/usr/bin/env python3
"""Lays out preview renders (from build_models.py --preview) into labelled contact sheets.

    python3 Tools/blender/contact_sheet.py PREVIEW_DIR [items|vehicles|props] [--per-sheet N] [--only id,id]

Each tile shows the game-camera view and the reverse view side by side, captioned with the
item's display name and id. Requires ImageMagick (montage).
"""
import json
import os
import subprocess
import sys
import tempfile

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FONT = "/usr/share/fonts/noto/NotoSans-Bold.ttf"


def main():
    args = sys.argv[1:]
    preview = args[0]
    kind = args[1] if len(args) > 1 and not args[1].startswith("--") else "items"
    per_sheet = int(args[args.index("--per-sheet") + 1]) if "--per-sheet" in args else 12
    only = set(args[args.index("--only") + 1].split(",")) if "--only" in args else None

    data = json.load(open(os.path.join(ROOT, "Assets/Resources/PackTheTrunkData.json")))
    if kind == "items":
        names = {i["id"]: i["name"] for i in data["items"]}
    elif kind == "vehicles":
        names = {l["id"]: f'{l["vehicle"]} ({l["title"]})' for l in data["levels"]}
    else:
        names = {}

    folder = os.path.join(preview, kind)
    ids = sorted(f[:-4] for f in os.listdir(folder) if f.endswith(".png") and not f.endswith("_b.png"))
    if only:
        ids = [i for i in ids if i in only]

    tmp = tempfile.mkdtemp()
    tiles = []
    for iid in ids:
        a = os.path.join(folder, iid + ".png")
        b = os.path.join(folder, iid + "_b.png")
        tile = os.path.join(tmp, iid + ".png")
        sources = [a, b] if os.path.exists(b) else [a]
        subprocess.run(["montage", *sources, "-tile", f"{len(sources)}x1", "-geometry", "+0+0", tile], check=True)
        tiles.append((iid, tile))

    for n in range(0, len(tiles), per_sheet):
        chunk = tiles[n:n + per_sheet]
        cmd = ["montage", "-font", FONT, "-pointsize", "20", "-fill", "white", "-background", "#23252B"]
        for iid, tile in chunk:
            cmd += ["-label", f"{names.get(iid, iid)}  ·  {iid}", tile]
        out = os.path.join(preview, f"{kind}_sheet_{n // per_sheet + 1}.png")
        cmd += ["-tile", "3x", "-geometry", "+6+6", out]
        subprocess.run(cmd, check=True)
        print(out)


main()
