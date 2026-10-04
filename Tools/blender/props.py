"""Scenery: trees, bushes, a house at the end of the driveway, mailbox, fence, street lamp.
Origins sit on the ground at the prop's footprint centre (Unity space, y up)."""
import math
import random

from ptt_lib import Model, box, col, cyl, empty, glass, glow, hull, ico, metal, rod, shade, sphere

PROPS = {}


def prop(fn):
    PROPS[fn.__name__] = fn
    return fn


def _finish(m):
    root = empty(m.name)
    m.build(parent=root)
    return root


@prop
def tree_round():
    rng = random.Random(3)
    m = Model("Oak Tree")
    m.add(cyl((0, 1.2, 0), 0.28, 2.4, "y", radius2=0.18, segments=10), col("6D4C41"))
    for _ in range(7):
        x, y, z = rng.uniform(-0.9, 0.9), rng.uniform(2.6, 3.9), rng.uniform(-0.9, 0.9)
        m.add(ico((x, y, z), rng.uniform(0.9, 1.3), 1), col(rng.choice(["4CAF50", "43A047", "66BB6A", "388E3C"])))
    return _finish(m)


@prop
def tree_pine():
    m = Model("Pine Tree")
    m.add(cyl((0, 0.6, 0), 0.22, 1.2, "y", segments=8), col("5D4037"))
    for k, (r, y) in enumerate(((1.5, 1.0), (1.2, 2.0), (0.9, 2.9), (0.55, 3.7))):
        m.add(cyl((0, y + 0.7, 0), r, 1.4, "y", radius2=0.05, segments=9), col(shade("2E7D32", 0.85 + k * 0.08)))
    return _finish(m)


@prop
def bush():
    rng = random.Random(5)
    m = Model("Flowering Bush")
    for _ in range(5):
        m.add(ico((rng.uniform(-0.5, 0.5), rng.uniform(0.35, 0.6), rng.uniform(-0.4, 0.4)), rng.uniform(0.4, 0.6), 1),
              col(rng.choice(["558B2F", "689F38", "7CB342"])))
    for _ in range(4):
        m.add(sphere((rng.uniform(-0.6, 0.6), rng.uniform(0.5, 0.9), rng.uniform(-0.55, -0.3)), 0.07), col("F06292"))
    return _finish(m)


@prop
def house():
    """Two-storey house with a garage, facing -z (towards the camera)."""
    m = Model("Suburban House")
    W, D, H = 12.0, 8.0, 6.0
    wall, trim, roof = col("F3E5AB"), col("FFFFFF"), col("8D3B2F")
    m.add(box((-W / 2, 0, 0), (W / 2 - 4, H, D)), wall)
    m.add(box((W / 2 - 4, 0, 0.5), (W / 2, 3.6, D)), wall)
    m.add(hull([(-W / 2 - 0.4, H, -0.5), (W / 2 - 3.6, H, -0.5), (-W / 2 - 0.4, H, D + 0.5), (W / 2 - 3.6, H, D + 0.5),
                (-W / 2 - 0.4, H + 3.0, D / 2), (W / 2 - 3.6, H + 3.0, D / 2)]), roof)
    m.add(hull([(W / 2 - 4.2, 3.6, 0.1), (W / 2 + 0.3, 3.6, 0.1), (W / 2 - 4.2, 3.6, D + 0.3), (W / 2 + 0.3, 3.6, D + 0.3),
                (W / 2 - 4.2, 5.2, D / 2 + 0.2), (W / 2 + 0.3, 5.2, D / 2 + 0.2)]), roof)
    # garage door
    m.add(box((W / 2 - 3.6, 0, 0.42), (W / 2 - 0.4, 2.8, 0.5)), trim)
    for k in range(5):
        m.add(box((W / 2 - 3.5, 0.15 + k * 0.53, 0.38), (W / 2 - 0.5, 0.2 + k * 0.53, 0.42)), col("E0E0E0"))
    # front door + steps
    m.add(box((-1.6, 0, -0.08), (-0.4, 2.3, 0.02)), col("2E5E8E"))
    m.add(sphere((-0.6, 1.15, -0.1), 0.06), metal("D4AF37"))
    m.add(box((-1.9, 0, -0.9), (-0.1, 0.2, 0.0)), col("BDBDBD"))
    m.add(box((-2.1, 2.3, -0.7), (0.1, 2.45, 0.0)), trim)
    # windows
    for x, y in ((-4.5, 1.0), (-4.5, 3.8), (-2.2, 3.8), (0.3, 3.8), (0.8, 1.0)):
        m.add(box((x - 0.05, y - 0.05, -0.06), (x + 1.35, y + 1.45, 0.0)), trim)
        m.add(box((x + 0.05, y + 0.05, -0.08), (x + 1.25, y + 1.35, -0.04)), glass("90CAF9"))
        m.add(box((x + 0.62, y + 0.05, -0.1), (x + 0.68, y + 1.35, -0.06)), trim)
        m.add(box((x - 0.4, y, -0.07), (x - 0.05, y + 1.4, -0.02)), col("4E7A4E"))
        m.add(box((x + 1.35, y, -0.07), (x + 1.7, y + 1.4, -0.02)), col("4E7A4E"))
    m.add(box((-W / 2 + 1, H + 0.8, D / 2 + 0.5), (-W / 2 + 1.8, H + 3.2, D / 2 + 1.3)), col("8D6E63"))
    return _finish(m)


@prop
def mailbox():
    m = Model("Mailbox")
    m.add(box((-0.06, 0, -0.06), (0.06, 1.0, 0.06)), col("6D4C41"))
    m.add(box((-0.2, 1.0, -0.35), (0.2, 1.28, 0.35), 0.03), col("1565C0"))
    m.add(cyl((0, 1.28, 0), 0.2, 0.7, "z", segments=16), col("1565C0"))
    m.add(box((0.2, 1.1, 0.1), (0.24, 1.45, 0.16)), col("E53935"))
    return _finish(m)


@prop
def fence():
    """4-unit picket fence segment along x."""
    m = Model("Picket Fence")
    for k in range(9):
        x = -2 + k * 0.5
        m.add(hull([(x - 0.08, 0, -0.03), (x + 0.08, 0, -0.03), (x - 0.08, 0, 0.03), (x + 0.08, 0, 0.03),
                    (x - 0.08, 0.95, -0.03), (x + 0.08, 0.95, -0.03), (x - 0.08, 0.95, 0.03), (x + 0.08, 0.95, 0.03),
                    (x, 1.08, -0.03), (x, 1.08, 0.03)]), col("FAFAFA"))
    for y in (0.3, 0.75):
        m.add(box((-2.1, y, 0.03), (2.1, y + 0.1, 0.09)), col("EEEEEE"))
    return _finish(m)


@prop
def street_lamp():
    m = Model("Street Lamp")
    m.add(cyl((0, 0.15, 0), 0.2, 0.3, "y", segments=12), col("37474F"))
    m.add(rod((0, 0.3, 0), (0, 4.2, 0), 0.07), col("37474F"))
    m.add(rod((0, 4.2, 0), (0.0, 4.4, -0.7), 0.05), col("37474F"))
    m.add(cyl((0, 4.35, -0.85), 0.28, 0.2, "y", radius2=0.12, segments=12), col("37474F"))
    m.add(sphere((0, 4.22, -0.85), 0.16), glow("FFF3C4"))
    return _finish(m)
