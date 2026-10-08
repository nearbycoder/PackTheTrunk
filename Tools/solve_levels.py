#!/usr/bin/env python3
"""Offline checker: proves every level in PackTheTrunkData.json can be packed
completely (required + bonus) under the in-game rules:
  * items stay inside the trunk and avoid blocked cells
  * every item rests on something (floor, blocked cell or another item)
  * nothing may sit directly on top of a fragile item

Usage: python3 Tools/solve_levels.py [level_id ...] [--dump <file>]
       python3 Tools/solve_levels.py --check-favours <favours.json>
  --dump x.json   every solution as JSON (the autopilot and the recorders read this)
  --dump x.txt    the same as tab-separated lines (level, item, x,y,z;...), the format the game
                  ships for Grandpa's hints:
                  python3 Tools/solve_levels.py --dump Assets/Resources/PackTheTrunkSolutions.txt
  --check-favours favours made by the game (the autopilot writes them to <out>/favours.json): checks
                  that each one's packing is complete and follows these rules in that trip's trunk, so
                  every favour can be packed 100%, independently of the game's own rule code, and that
                  no pile has one of the family's own things ("family": true in the data).
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


def check_favours(path, data, items):
    """Each favour: a complete, legal packing of its pile in its car's trunk, drawn from its pool, nothing of the family's."""
    levels = {l["id"]: l for l in data["levels"]}
    favours = json.loads(Path(path).read_text())
    shapes = {}
    bad = 0
    fills = []
    for f in favours:
        problems = []
        level = levels.get(f["car"])
        if level is None:
            print(f"favour {f['number']}: unknown car {f['car']}")
            bad += 1
            continue
        W, H, D = level["w"], level["h"], level["d"]
        blocked = set(parse_layers(level["blocked"])) if level["blocked"] else set()
        free = W * H * D - len(blocked)
        pile = sorted(f["required"] + f["bonus"])
        packed = sorted(p["id"] for p in f["packing"])
        if pile != packed:
            problems.append("the packing packs different things than the pile")
        pool = set(f["pool"])
        outside = sorted(set(pile) - pool)
        if outside:
            problems.append("not from a closed trip: " + ", ".join(outside))
        family = sorted(k for k in set(pile) if items.get(k, {}).get("family"))
        if family:
            problems.append("the family's own things: " + ", ".join(family))
        owner = {}
        for n, p in enumerate(f["packing"]):
            k = p["id"]
            if k not in items:
                problems.append(f"unknown item {k}")
                continue
            if k not in shapes:
                shapes[k] = set(orientations(parse_layers(items[k]["layers"])))
            cells = [tuple(c) for c in p["cells"]]
            if normalize(cells) not in shapes[k]:
                problems.append(f"{k} isn't any turn of its shape")
            for c in cells:
                x, y, z = c
                if not (0 <= x < W and 0 <= y < H and 0 <= z < D) or c in blocked:
                    problems.append(f"{k} is outside the trunk or in a wall at {c}")
                elif c in owner:
                    problems.append(f"{k} overlaps at {c}")
                owner[c] = n
        for n, p in enumerate(f["packing"]):
            k = p["id"]
            if k not in items:
                continue
            cells = set(tuple(c) for c in p["cells"])
            supported = False
            for x, y, z in cells:
                below = (x, y - 1, z)
                if below in cells:
                    continue
                if y == 0 or below in blocked:
                    supported = True
                elif below in owner:
                    under = f["packing"][owner[below]]["id"]
                    if items[under]["fragile"]:
                        problems.append(f"{k} sits on the fragile {under}")
                    supported = True
            if not supported:
                problems.append(f"{k} floats")
        fill = len(owner) / free
        fills.append(fill)
        count = len(f["packing"])
        if not (0.82 <= fill <= 0.95):
            problems.append(f"fill {fill:.2f} outside 0.82-0.95")
        if not (6 <= count <= 22):
            problems.append(f"{count} things (6-22 expected)")
        if len(f["bonus"]) < 2:
            problems.append("fewer than two extras")
        if problems:
            bad += 1
            print(f"favour {f['number']} ({f['car']}): " + "; ".join(sorted(set(problems))[:6]))
    cars = sorted(set(f["car"] for f in favours))
    if fills:
        print(f"checked {len(favours)} favours in {len(cars)} cars: {len(favours) - bad} complete and legal, {bad} not; "
              f"fill {min(fills):.2f}-{sum(fills) / len(fills):.2f}-{max(fills):.2f}")
    return bad == 0 and len(favours) > 0


def main():
    data = json.loads(DATA.read_text())
    items = {i["id"]: i for i in data["items"]}
    args = sys.argv[1:]
    if "--check-favours" in args:
        sys.exit(0 if check_favours(args[args.index("--check-favours") + 1], data, items) else 1)
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
    if dump and dump.endswith(".txt"):
        Path(dump).write_text("".join(
            f"{level}\t{p['id']}\t" + ";".join(",".join(map(str, c)) for c in p["cells"]) + "\n"
            for level, placements in dumped.items() for p in placements))
    elif dump:
        Path(dump).write_text(json.dumps(dumped))
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
