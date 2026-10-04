#!/usr/bin/env python3
"""Offline checker: proves every level in PackTheTrunkData.json can be packed
completely (required + bonus) under the in-game rules:
  * items stay inside the trunk and avoid blocked cells
  * every item rests on something (floor, blocked cell or another item)
  * nothing may sit directly on top of a fragile item

Usage: python3 Tools/solve_levels.py [level_id ...]
"""
import json
import sys
import time
from pathlib import Path

DATA = Path(__file__).resolve().parent.parent / "Assets/Resources/PackTheTrunkData.json"


def parse_layers(layers):
    """Layers are bottom-to-top; rows inside a layer are listed far (high z) to near (z=0)."""
    cells = []
    for y, layer in enumerate(layers):
        rows = layer.split("|")
        depth = len(rows)
        for r, row in enumerate(rows):
            z = depth - 1 - r
            for x, ch in enumerate(row):
                if ch not in ". ":
                    cells.append((x, y, z))
    return cells


def normalize(cells):
    mx = min(c[0] for c in cells)
    my = min(c[1] for c in cells)
    mz = min(c[2] for c in cells)
    return tuple(sorted((x - mx, y - my, z - mz) for x, y, z in cells))


def orientations(cells):
    rx = lambda c: (c[0], -c[2], c[1])
    ry = lambda c: (c[2], c[1], -c[0])
    seen = set()
    frontier = [normalize(cells)]
    while frontier:
        s = frontier.pop()
        if s in seen:
            continue
        seen.add(s)
        frontier.append(normalize([rx(c) for c in s]))
        frontier.append(normalize([ry(c) for c in s]))
    return sorted(seen)


def solve(level, items):
    W, H, D = level["w"], level["h"], level["d"]
    blocked = set(parse_layers(level["blocked"])) if level["blocked"] else set()
    free = W * H * D - len(blocked)
    ids = level["required"] + level["bonus"]
    volume = sum(len(parse_layers(items[i]["layers"])) for i in ids)
    slack = free - volume
    if slack < 0:
        return None, f"volume {volume} > free {free}"

    counts = {}
    for i in ids:
        counts[i] = counts.get(i, 0) + 1
    kinds = sorted(counts, key=lambda i: -len(parse_layers(items[i]["layers"])))
    # For each orientation, anchor = first cell in scan order (y, z, x)
    placements = {}
    for k in kinds:
        opts = []
        for o in orientations(parse_layers(items[k]["layers"])):
            first = min(o, key=lambda c: (c[1], c[2], c[0]))
            opts.append([(c[0] - first[0], c[1] - first[1], c[2] - first[2]) for c in o])
        placements[k] = opts

    order = [(x, y, z) for y in range(H) for z in range(D) for x in range(W)]
    grid = {}  # cell -> item kind (or '#', or '_' for deliberately empty)
    for b in blocked:
        grid[b] = "#"
    fragile = {k: items[k]["fragile"] for k in kinds}
    solution = []
    nodes = [0]

    def fits(cells, kind):
        supported = False
        for c in cells:
            x, y, z = c
            if not (0 <= x < W and 0 <= y < H and 0 <= z < D):
                return False
            if c in grid:
                return False
        cellset = set(cells)
        for x, y, z in cells:
            below = (x, y - 1, z)
            if below in cellset:
                continue
            if y == 0:
                supported = True
                continue
            b = grid.get(below)
            if b is None or b == "_":
                continue
            if b != "#" and fragile.get(b):
                return False
            supported = True
        if not supported:
            return False
        if fragile[kind]:
            for x, y, z in cells:
                above = (x, y + 1, z)
                if above not in cellset and grid.get(above) not in (None, "_", "#"):
                    return False
        return True

    def rec(idx, slack_left):
        nodes[0] += 1
        if nodes[0] > 5_000_000:
            raise TimeoutError
        while idx < len(order) and order[idx] in grid:
            idx += 1
        if all(v == 0 for v in counts.values()):
            return True
        if idx >= len(order):
            return False
        ax, ay, az = order[idx]
        for k in kinds:
            if counts[k] == 0:
                continue
            for o in placements[k]:
                cells = [(ax + dx, ay + dy, az + dz) for dx, dy, dz in o]
                if not fits(cells, k):
                    continue
                for c in cells:
                    grid[c] = k
                counts[k] -= 1
                solution.append((k, cells))
                if rec(idx + 1, slack_left):
                    return True
                solution.pop()
                counts[k] += 1
                for c in cells:
                    del grid[c]
        if slack_left > 0:
            grid[order[idx]] = "_"
            if rec(idx + 1, slack_left - 1):
                return True
            del grid[order[idx]]
        return False

    try:
        ok = rec(0, slack)
    except TimeoutError:
        return None, f"gave up after {nodes[0]} nodes (slack {slack})"
    if not ok:
        return None, f"no solution (slack {slack}, {nodes[0]} nodes)"
    return (solution, grid, W, H, D), f"solved, slack {slack}, {nodes[0]} nodes"


def render(sol):
    solution, grid, W, H, D = sol
    letters = {}
    names = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"
    owner = {}
    for n, (k, cells) in enumerate(solution):
        letters[names[n]] = k
        for c in cells:
            owner[c] = names[n]
    out = []
    for y in range(H):
        out.append(f"  y={y}")
        for z in reversed(range(D)):
            row = ""
            for x in range(W):
                g = grid.get((x, y, z))
                row += "#" if g == "#" else owner.get((x, y, z), ".")
            out.append("    " + row)
    out.append("  " + ", ".join(f"{l}={k}" for l, k in letters.items()))
    return "\n".join(out)


def main():
    data = json.loads(DATA.read_text())
    items = {i["id"]: i for i in data["items"]}
    args = sys.argv[1:]
    dump = None
    if "--dump" in args:
        i = args.index("--dump")
        dump = args[i + 1]
        del args[i:i + 2]
    want = set(args)
    failed = False
    dumped = {}
    for level in data["levels"]:
        if want and level["id"] not in want:
            continue
        for i in level["required"] + level["bonus"]:
            if i not in items:
                print(f"{level['id']}: unknown item {i}")
                failed = True
        t = time.time()
        sol, msg = solve(level, items)
        print(f"{level['id']:12s} {msg} ({time.time() - t:.1f}s)")
        if sol:
            print(render(sol))
            dumped[level["id"]] = [{"id": k, "cells": [list(c) for c in cells]} for k, cells in sol[0]]
        else:
            failed = True
    if dump:
        Path(dump).write_text(json.dumps(dumped))
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
