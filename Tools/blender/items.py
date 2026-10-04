"""Item models. Each builder receives a Model and the item's palette {'a': 'RRGGBB', ...}.

Coordinates are Unity-space cell units with the item's minimum corner at the origin, matching the
voxel layout in PackTheTrunkData.json (layers = y, rows far->near = z, chars = x). Models stay
inside their occupied cells (with a small margin) so packing remains readable.
"""
import math

from mathutils import Matrix, Vector

from ptt_lib import (arc_points, box, col, cyl, ellipsoid, glass, glow, hull, ico, lathe, metal, rod,
                     rotate_about, rounded_rect_points, shade, shell, sphere, sweep, torus, transformed)

BLACK = "1E1E22"
WHITE = "F4F4F0"
SKIN = "F1C27D"
DARK = "2B2B30"
STEEL = "B8BCC2"
GOLD = "D4AF37"
WOOD = "8B5A2B"
CARD = "C49A6C"

ITEMS = {}


def item(fn):
    ITEMS[fn.__name__] = fn
    return fn


# ------------------------------------------------------------------ luggage

@item
def suitcase_big(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.06, 0.04, 0.08), (2.94, 0.9, 1.92), 0.16, 4), col(a))
    m.add(sweep(rounded_rect_points((1.5, 0.47, 1.0), 1.45, 0.93, 0.16, 4), 0.025, closed=True, segments=6), col(shade(a, 0.55)))
    for x in (0.85, 2.15):
        m.add(box((x - 0.1, 0.02, 0.06), (x + 0.1, 0.92, 1.94), 0.03), col(b))
        m.add(box((x - 0.13, 0.36, 0.03), (x + 0.13, 0.56, 0.08), 0.02), metal(STEEL))
    m.add(sweep([(1.15, 0.47, 0.07), (1.2, 0.47, -0.0), (1.8, 0.47, -0.0), (1.85, 0.47, 0.07)], 0.045, segments=8), col(DARK))
    m.add(sweep([(1.3, 0.44, 0.0), (1.33, 0.25, -0.0)], 0.008, segments=4), col(DARK))
    m.add(box((1.25, 0.08, -0.0), (1.45, 0.26, 0.03), 0.02), col("FFD54F"))
    for x, z in ((0.12, 0.14), (2.88, 0.14), (0.12, 1.86), (2.88, 1.86)):
        m.add(sphere((x, 0.47, z), 0.08), col(DARK))
    for z in (0.35, 1.65):
        m.add(cyl((2.95, 0.15, z), 0.1, 0.08, "x", segments=14), col(BLACK))
    m.add(box((0.25, 0.9, 0.3), (0.85, 0.93, 0.7), 0.02), col("FFFFFF"))
    m.add(box((0.3, 0.925, 0.35), (0.8, 0.935, 0.65)), col("E53935"))


@item
def suitcase_red(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.06, 0.04, 0.08), (2.94, 0.9, 1.92), 0.22, 5), col(a))
    for z in (0.42, 0.76, 1.1, 1.44):
        m.add(box((0.25, 0.86, z - 0.06), (2.75, 0.93, z + 0.06), 0.03), col(shade(a, 1.12)))
    m.add(sweep(rounded_rect_points((1.5, 0.47, 1.0), 1.45, 0.93, 0.22, 5), 0.03, closed=True, segments=6), col(b))
    m.add(sweep([(1.2, 0.47, 0.06), (1.25, 0.47, 0.0), (1.75, 0.47, 0.0), (1.8, 0.47, 0.06)], 0.045, segments=8), col(BLACK))
    for x, z in ((0.22, 0.12), (2.78, 0.12), (0.22, 1.88), (2.78, 1.88)):
        m.add(cyl((x, 0.06, z), 0.07, 0.06, "y", segments=12), col(BLACK))
    m.add(box((2.3, 0.6, 0.04), (2.6, 0.75, 0.07), 0.01), metal(STEEL))
    m.add(box((2.36, 0.64, 0.03), (2.54, 0.71, 0.04)), col(BLACK))


@item
def carry_on(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.1, 0.14, 0.1), (1.9, 1.72, 0.9), 0.16, 4), col(a))
    m.add(box((0.26, 0.3, 0.05), (1.74, 1.34, 0.14), 0.08, 3), col(shade(a, 1.3)))
    m.add(sweep(arc_points((1.0, 1.0, 0.06), 0.5, 200, 340, "xy", 12), 0.012, segments=5), metal(STEEL))
    m.add(sweep(rounded_rect_points((1.0, 0.95, 0.5), 0.905, 0.405, 0.16, 5), 0.025, closed=True, segments=6), col(b))
    for x in (0.6, 1.4):
        m.add(rod((x, 1.7, 0.78), (x, 1.93, 0.78), 0.035), metal(STEEL))
    m.add(box((0.52, 1.88, 0.71), (1.48, 1.96, 0.85), 0.03), col(BLACK))
    m.add(sweep(arc_points((1.0, 1.72, 0.5), 0.16, 0, 180, "xy", 10), 0.03, segments=8), col(BLACK))
    for x in (0.22, 1.78):
        for z in (0.22, 0.78):
            m.add(box((x - 0.06, 0.08, z - 0.06), (x + 0.06, 0.16, z + 0.06), 0.02), col(BLACK))
            m.add(cyl((x, 0.06, z), 0.055, 0.06, "x", segments=12), col(DARK))


@item
def duffel(m, P):
    a, b = P["a"], P["b"]
    m.add(cyl((1.5, 0.44, 0.5), 0.42, 2.6, "x", segments=28, bevel=0.14), col(a))
    for x in (0.18, 2.82):
        m.add(cyl((x, 0.44, 0.5), 0.43, 0.08, "x", segments=28), col(b))
        m.add(box((x - 0.06, 0.25, 0.25), (x + 0.06, 0.62, 0.75), 0.05), col(shade(a, 0.8)))
    m.add(box((0.35, 0.83, 0.46), (2.65, 0.88, 0.54)), col(b))
    m.add(box((1.6, 0.85, 0.44), (1.7, 0.9, 0.56), 0.01), metal(STEEL))
    for x in (1.05, 1.95):
        m.add(sweep(arc_points((x, 0.82, 0.5), 0.16, 0, 180, "zy", 12), 0.035, segments=8), col(b))
        m.add(box((x - 0.05, 0.6, 0.06), (x + 0.05, 0.82, 0.1)), col(b))
        m.add(box((x - 0.05, 0.6, 0.9), (x + 0.05, 0.82, 0.94)), col(b))
    m.add(sweep([(0.35, 0.6, 0.07), (0.9, 0.98, 0.08), (2.1, 0.98, 0.08), (2.65, 0.6, 0.07)], 0.03, segments=6), col(DARK))
    m.add(box((1.25, 0.3, 0.06), (1.75, 0.55, 0.09), 0.02), col(b))
    m.add(box((1.35, 0.38, 0.05), (1.65, 0.47, 0.06)), col(WHITE))


@item
def backpack(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.12, 0.04, 0.24), (0.88, 1.5, 0.86), 0.2, 4), col(a))
    m.add(ellipsoid((0.5, 1.48, 0.55), (0.38, 0.32, 0.31), 24, 12), col(a))
    m.add(torus((0.5, 1.47, 0.55), 0.37, 0.012, "y", 28, 5, scale=(1, 0.82, 1)), metal(STEEL))
    m.add(box((0.2, 0.12, 0.1), (0.8, 0.88, 0.32), 0.12, 4), col(b))
    m.add(sweep(arc_points((0.5, 0.6, 0.095), 0.27, 10, 170, "xy", 14), 0.012, segments=6), metal(STEEL))
    m.add(box((0.48, 0.82, 0.08), (0.52, 0.9, 0.1)), metal(STEEL))
    m.add(box((0.4, 0.3, 0.085), (0.6, 0.4, 0.1), 0.01), col(WHITE))
    for x in (0.08, 0.92):
        m.add(box((x - 0.06, 0.12, 0.35), (x + 0.06, 0.6, 0.75), 0.05), col(b))
    m.add(cyl((0.06, 0.62, 0.55), 0.09, 0.42, "y", segments=14), col("29B6F6"))
    m.add(cyl((0.06, 0.86, 0.55), 0.05, 0.06, "y", segments=10), col(BLACK))
    for x in (0.3, 0.7):
        m.add(sweep([(x, 1.42, 0.86), (x, 1.2, 0.97), (x, 0.6, 0.97), (x + (0.1 if x < 0.5 else -0.1), 0.2, 0.88)],
                    0.045, segments=8), col(b))
    m.add(sweep(arc_points((0.5, 1.76, 0.6), 0.1, 0, 180, "xy", 10), 0.025, segments=8), col(b))


@item
def grocery_bag(m, P):
    a = P["a"]
    paper = col(a)
    m.add(hull([(0.15, 0.02, 0.2), (0.85, 0.02, 0.2), (0.15, 0.02, 0.8), (0.85, 0.02, 0.8),
                (0.12, 0.72, 0.17), (0.88, 0.72, 0.17), (0.12, 0.72, 0.83), (0.88, 0.72, 0.83)]), paper)
    for (mn, mx) in (((0.1, 0.68, 0.15), (0.9, 0.78, 0.19)), ((0.1, 0.68, 0.81), (0.9, 0.78, 0.85)),
                     ((0.1, 0.68, 0.15), (0.14, 0.78, 0.85)), ((0.86, 0.68, 0.15), (0.9, 0.78, 0.85))):
        m.add(box(mn, mx, 0.01), col(shade(a, 0.82)))
    m.add(box((0.32, 0.25, 0.16), (0.68, 0.52, 0.175), 0.01), col("4E7D3A"))
    m.add(sweep(arc_points((0.5, 0.385, 0.155), 0.09, 0, 360, "xy", 16), 0.012, segments=5), col(WHITE))
    m.add(lathe([(0.01, 0), (0.06, 0.08), (0.075, 0.4), (0.05, 0.52), (0.01, 0.56)], (0.3, 0.48, 0.62), "y", 12),
          col("D4A055"), transform=rotate_about((0.3, 0.48, 0.62), "Z", 14))
    for k, (dx, dz) in enumerate(((0.0, 0.0), (0.05, 0.04), (-0.04, 0.05))):
        x, z = 0.66 + dx, 0.38 + dz
        m.add(rod((x, 0.55, z), (x + 0.03, 0.9, z - 0.02), 0.025), col("8BC34A"))
        m.add(ellipsoid((x + 0.03, 0.93, z - 0.02), (0.06, 0.05, 0.04)), col("558B2F"))
    m.add(sphere((0.45, 0.78, 0.3), 0.1), col("D32F2F"))
    m.add(rod((0.45, 0.88, 0.3), (0.46, 0.93, 0.3), 0.01), col("5D4037"))
    m.add(ellipsoid((0.48, 0.92, 0.27), (0.04, 0.012, 0.025)), col("43A047"))
    m.add(rod((0.7, 0.7, 0.68), (0.78, 0.92, 0.72), 0.045, radius2=0.01), col("FF8F00"))
    m.add(sphere((0.69, 0.68, 0.68), 0.05), col("43A047"))


@item
def eggs(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.1, 0.02, 0.14), (1.9, 0.12, 0.86), 0.03), col(b))
    egg = [(0.01, 0.0), (0.07, 0.03), (0.1, 0.1), (0.095, 0.19), (0.06, 0.26), (0.01, 0.29)]
    for i in range(6):
        for j in range(3):
            x, z = 0.27 + i * 0.29, 0.3 + j * 0.2
            m.add(shell([(0.05, 0.0), (0.1, 0.16)], (x, 0.1, z), "y", thickness=0.015, segments=12), col(b))
            m.add(lathe(egg, (x, 0.15, z), "y", 12), col(a))
    hinge = (1.0, 0.12, 0.88)
    lid = box((0.1, 0.12, 0.88), (1.9, 0.18, 1.58), 0.03)
    m.add(transformed(lid, rotate_about(hinge, "X", -100)), col(b))
    for i in range(6):
        bump = sphere((0.27 + i * 0.29, 0.2, 1.23), 0.09, (1, 0.6, 1.4), 10, 6)
        m.add(transformed(bump, rotate_about(hinge, "X", -100)), col(b))
    m.add(box((0.6, 0.04, 0.12), (1.4, 0.1, 0.14)), col("2E7D32"))


@item
def cake(m, P):
    a, b = P["a"], P["b"]
    m.add(cyl((1, 0.03, 1), 0.95, 0.06, "y", segments=32), metal(STEEL))
    m.add(cyl((1, 0.27, 1), 0.82, 0.42, "y", segments=32, bevel=0.04), col(a))
    m.add(torus((1, 0.48, 1), 0.8, 0.05, "y", 32), col(b))
    m.add(cyl((1, 0.6, 1), 0.52, 0.26, "y", segments=32, bevel=0.03), col(b))
    for i in range(8):
        ang = i / 8 * 2 * math.pi
        m.add(sphere((1 + 0.68 * math.cos(ang), 0.52, 1 + 0.68 * math.sin(ang)), 0.06), col("C62828"))
    colors = ["42A5F5", "FFCA28", "66BB6A", "EF5350", "AB47BC"]
    for i in range(5):
        ang = i / 5 * 2 * math.pi
        x, z = 1 + 0.3 * math.cos(ang), 1 + 0.3 * math.sin(ang)
        m.add(cyl((x, 0.82, z), 0.025, 0.16, "y", segments=8), col(colors[i]))
        m.add(sphere((x, 0.93, z), 0.035, (1, 1.6, 1), 8, 6), glow("FFB300"))


@item
def water(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.06, 0.02, 0.06), (1.94, 0.5, 1.94), 0.06), col(shade(a, 1.25)))
    m.add(box((0.05, 0.14, 0.05), (1.95, 0.34, 1.95), 0.03), col(b))
    m.add(box((0.6, 0.18, 0.04), (1.4, 0.3, 0.05)), col(WHITE))
    bottle = [(0.2, 0.0), (0.21, 0.42), (0.17, 0.55), (0.08, 0.66), (0.06, 0.72)]
    for i in range(4):
        for j in range(4):
            x, z = 0.27 + i * 0.487, 0.27 + j * 0.487
            m.add(lathe(bottle, (x, 0.2, z), "y", 14), glass(shade(a, 1.15)))
            m.add(cyl((x, 0.94, z), 0.065, 0.06, "y", segments=12), col(b))


@item
def watermelon(m, P):
    a = P["a"]
    dark = col("1B5E20")
    light = col(a)
    bm = ellipsoid((0.5, 0.45, 0.5), (0.44, 0.42, 0.47), 24, 14)

    def pick(c, n):
        ang = math.atan2(c.z - 0.5, c.x - 0.5)
        return dark if int((ang + math.pi) / (2 * math.pi) * 12) % 2 == 0 else light
    m.add_faces_by(bm, pick)
    m.add(cyl((0.5, 0.88, 0.5), 0.03, 0.06, "y", segments=8), col("6D4C41"))


@item
def baguette(m, P):
    a, b = P["a"], P["b"]
    loaf = [(0.01, 0.0), (0.08, 0.12), (0.15, 0.45), (0.17, 1.4), (0.15, 2.4), (0.08, 2.75), (0.01, 2.86)]
    for y, z, tilt in ((0.19, 0.3, 2), (0.19, 0.7, -2), (0.49, 0.5, 0)):
        m.add(lathe(loaf, (0.07, y, z), "x", 16), col(a), transform=rotate_about((1.5, y, z), "Y", tilt))
        for k in range(6):
            x = 0.5 + k * 0.38
            m.add(ellipsoid((x, y + 0.14, z), (0.13, 0.025, 0.045)), col(shade(b, 1.25)),
                  transform=rotate_about((x, y + 0.14, z), "Y", 35))
    m.add(shell([(0.4, 0.0), (0.4, 1.0)], (1.0, 0.36, 0.5), "x", thickness=0.015, segments=28), col("F5F0E1"))
    m.add(shell([(0.405, 0.35), (0.405, 0.65)], (1.0, 0.36, 0.5), "x", thickness=0.012, segments=28), col("C0392B"))


@item
def toilet_paper(m, P):
    a, b = P["a"], P["b"]
    for layer in range(3):
        for i in range(4):
            for j in range(4):
                x, y, z = 0.26 + i * 0.49, 0.32 + layer * 0.62, 0.26 + j * 0.49
                m.add(cyl((x, y, z), 0.22, 0.58, "y", segments=16, bevel=0.03), col(a))
                if layer == 2:
                    m.add(cyl((x, y + 0.29, z), 0.07, 0.01, "y", segments=10), col("A1887F"))
    m.add(box((0.035, 0.84, 0.035), (1.965, 1.16, 1.965), 0.02), col(b))
    m.add(box((0.5, 0.86, 0.02), (1.5, 1.14, 0.03)), col(WHITE))
    m.add(sphere((0.75, 1.0, 0.02), 0.08, (1, 1, 0.2)), col("64B5F6"))
    m.add(box((0.55, 1.88, 0.55), (1.45, 1.9, 1.45)), col(b))


@item
def cereal(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.1, 0.02, 0.2), (0.9, 1.88, 0.8), 0.02), col(a))
    m.add(box((0.1, 1.86, 0.2), (0.9, 1.9, 0.8), 0.01), col(shade(a, 0.75)))
    m.add(box((0.15, 1.3, 0.17), (0.85, 1.7, 0.2), 0.02), col(b))
    for k, cl in enumerate(("E53935", "1E88E5", "43A047", "FB8C00", "8E24AA")):
        m.add(box((0.2 + k * 0.125, 1.38, 0.16), (0.3 + k * 0.125, 1.62, 0.175), 0.015), col(cl))
    m.add(cyl((0.5, 0.82, 0.18), 0.27, 0.03, "z", segments=24), col(WHITE))
    m.add(shell([(0.06, 0.0), (0.22, 0.14)], (0.5, 0.58, 0.16), "y", 0.015, 20), col("90CAF9"))
    for k in range(7):
        m.add(torus((0.38 + (k % 4) * 0.08, 0.72 + (k // 4) * 0.03, 0.16), 0.035, 0.014, "z", 10, 5),
              col(("FFCA28", "EF5350", "66BB6A")[k % 3]))
    m.add(sphere((0.58, 1.02, 0.16), 0.13, (1, 1, 0.3)), col("FFB74D"))
    for x in (0.54, 0.62):
        m.add(sphere((x, 1.06, 0.15), 0.02), col(BLACK))
    m.add(box((0.9, 0.3, 0.3), (0.905, 1.4, 0.7)), col(WHITE))
    for k in range(6):
        m.add(box((0.905, 0.4 + k * 0.15, 0.34), (0.908, 0.42 + k * 0.15, 0.66)), col("9E9E9E"))


@item
def tent(m, P):
    a, b = P["a"], P["b"]
    sack = [(0.18, 0.0), (0.34, 0.06), (0.4, 0.2), (0.4, 2.45), (0.3, 2.68), (0.1, 2.82), (0.05, 2.86)]
    m.add(lathe(sack, (0.08, 0.42, 0.5), "x", 22), col(a))
    m.add(cyl((0.08, 0.42, 0.5), 0.18, 0.02, "x", segments=18), col(b))
    for x in (0.8, 2.1):
        m.add(torus((x, 0.42, 0.5), 0.405, 0.035, "x"), col(b))
        m.add(box((x - 0.06, 0.78, 0.44), (x + 0.06, 0.86, 0.56)), metal(STEEL))
    m.add(sweep([(2.94, 0.42, 0.5), (2.97, 0.3, 0.55), (2.95, 0.18, 0.6)], 0.012, segments=5), col(BLACK))
    m.add(sphere((2.95, 0.16, 0.6), 0.035), col(BLACK))
    m.add(box((1.25, 0.3, 0.08), (1.75, 0.55, 0.1), 0.02), col(b))
    for dy, dz in ((0.12, 0.08), (-0.06, -0.12), (0.08, -0.14)):
        m.add(rod((0.02, 0.42 + dy, 0.5 + dz), (0.12, 0.42 + dy, 0.5 + dz), 0.03), metal(STEEL))


@item
def cooler(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.06, 0.02, 0.08), (1.94, 1.45, 0.92), 0.12, 3), col(a))
    m.add(box((0.1, 0.02, 0.06), (1.9, 0.1, 0.94), 0.03), col(shade(a, 0.7)))
    m.add(box((0.04, 1.42, 0.06), (1.96, 1.9, 0.94), 0.12, 3), col(b))
    m.add(box((0.2, 1.88, 0.2), (1.8, 1.92, 0.8), 0.05), col(shade(b, 0.92)))
    for x0, x1 in ((-0.02, 0.07), (1.93, 2.02)):
        m.add(box((x0, 1.0, 0.32), (x1, 1.12, 0.68), 0.03), col(shade(a, 0.7)))
    for x in (0.45, 1.55):
        m.add(box((x - 0.12, 1.3, 0.02), (x + 0.12, 1.55, 0.08), 0.03), col(WHITE))
    m.add(cyl((0.25, 0.25, 0.06), 0.06, 0.06, "z", segments=12), col(WHITE))
    m.add(sweep([(0.55, 1.92, 0.5), (0.65, 1.97, 0.5), (1.35, 1.97, 0.5), (1.45, 1.92, 0.5)], 0.035, segments=8), col(shade(a, 0.7)))
    for k in range(4):
        m.add(cyl((0.35 + k * 0.08, 1.9, 0.75), 0.03, 0.02, "y", segments=10), col(shade(b, 0.8)))
    m.add(box((1.25, 0.4, 0.06), (1.75, 0.75, 0.08), 0.03), col(WHITE))
    m.add(ellipsoid((1.5, 0.58, 0.05), (0.14, 0.07, 0.01)), col("1565C0"))


@item
def sleeping_bag(m, P):
    a, b = P["a"], P["b"]
    m.add(cyl((0.5, 0.44, 1.02), 0.42, 1.8, "z", segments=24, bevel=0.1), col(a))
    m.add(torus((0.5, 0.44, 0.13), 0.22, 0.06, "z"), col(b))
    m.add(cyl((0.5, 0.44, 0.12), 0.18, 0.04, "z"), col(b))
    for z in (0.55, 1.5):
        m.add(torus((0.5, 0.44, z), 0.43, 0.04, "z"), col(DARK))


@item
def camp_chair(m, P):
    a, b = P["a"], P["b"]
    bag = [(0.06, 0.0), (0.3, 0.06), (0.4, 0.18), (0.4, 2.2), (0.38, 2.32), (0.33, 2.42)]
    m.add(lathe(bag, (0.1, 0.42, 0.5), "x", 22), col(a))
    m.add(cyl((0.1, 0.42, 0.5), 0.07, 0.04, "x", segments=12), col(b))
    m.add(sweep([(0.12, 0.42, 0.5), (0.02, 0.3, 0.5), (0.04, 0.12, 0.45)], 0.015, segments=6), col(BLACK))
    m.add(sphere((0.05, 0.1, 0.45), 0.035), col(BLACK))
    m.add(shell([(0.33, 0.0), (0.34, 0.12)], (2.5, 0.42, 0.5), "x", thickness=0.02, segments=22), col(shade(a, 0.85)))
    for dz in (-0.17, 0.17):
        m.add(rod((2.45, 0.6, 0.5 + dz), (2.95, 0.62, 0.5 + dz * 0.9), 0.035), metal(STEEL))
        m.add(cyl((2.93, 0.62, 0.5 + dz * 0.9), 0.05, 0.06, "x"), col(BLACK))
    m.add(hull([(2.5, 0.25, 0.3), (2.92, 0.3, 0.32), (2.5, 0.25, 0.7), (2.92, 0.3, 0.68),
                (2.5, 0.52, 0.5), (2.9, 0.52, 0.5)]), col(b))
    m.add(rod((2.6, 0.18, 0.4), (2.95, 0.2, 0.4), 0.03), metal(STEEL))
    m.add(sweep([(0.5, 0.82, 0.5), (1.3, 0.95, 0.5), (2.1, 0.82, 0.5)], 0.025, segments=8, caps=False), col(BLACK))
    for x in (0.5, 2.1):
        m.add(torus((x, 0.42, 0.5), 0.405, 0.03, "x", 22, 6), col(BLACK))


@item
def paddle(m, P):
    a, b = P["a"], P["b"]
    m.add(rod((0.6, 0.5, 0.5), (4.4, 0.5, 0.5), 0.05, segments=14), col(b))
    blade = [(0.02, 0.0), (0.12, 0.05), (0.22, 0.2), (0.27, 0.42), (0.25, 0.55), (0.12, 0.62)]
    # Blades are flattened lathes: wide, thin and feathered 90 degrees apart like a real paddle.
    for x0, direction, flat_axis in ((0.65, -1, "z"), (4.35, 1, "y")):
        from mathutils import Matrix, Vector
        prim = lathe(blade, (0, 0, 0), "x", 20)
        squash = Matrix.Diagonal(Vector((1, 1.5 if flat_axis == "z" else 0.12, 0.12 if flat_axis == "z" else 1.5, 1)))
        flip = Matrix.Rotation(math.pi, 4, "Y") if direction < 0 else Matrix.Identity(4)
        m.add(prim, col(a), transform=Matrix.Translation((x0, 0.5, 0.5)) @ flip @ squash)
    for x in (1.3, 3.7):
        m.add(cyl((x, 0.5, 0.5), 0.16, 0.025, "x", segments=16), col(BLACK))
    for x in (1.9, 3.1):
        m.add(cyl((x, 0.5, 0.5), 0.065, 0.45, "x", segments=14), col("37474F"))


@item
def lantern(m, P):
    a = P["a"]
    m.add(cyl((0.5, 0.1, 0.5), 0.3, 0.16, "y", segments=20, bevel=0.03), col(DARK))
    m.add(cyl((0.5, 0.42, 0.5), 0.24, 0.48, "y", segments=20), glow(a))
    for i in range(4):
        ang = i * math.pi / 2 + math.pi / 4
        x, z = 0.5 + 0.25 * math.cos(ang), 0.5 + 0.25 * math.sin(ang)
        m.add(rod((x, 0.18, z), (x, 0.68, z), 0.02), col(DARK))
    m.add(cyl((0.5, 0.74, 0.5), 0.3, 0.12, "y", radius2=0.14, segments=20), col(DARK))
    m.add(torus((0.5, 0.8, 0.5), 0.16, 0.02, "z", arc=0.5), metal(STEEL))


@item
def firewood(m, P):
    a, b = P["a"], P["b"]
    spots = [(0.17, 0.2), (0.17, 0.5), (0.17, 0.8), (0.43, 0.35), (0.43, 0.65), (0.69, 0.5), (0.69, 0.22)]
    for k, (y, z) in enumerate(spots):
        r = 0.15 if k % 3 else 0.14
        m.add(cyl((1.0, y, z), r, 1.88 - k * 0.03, "x", segments=10), col(a if k % 2 else b))
        for side in (-1, 1):
            x = 1.0 + side * (0.94 - k * 0.015)
            m.add(cyl((x, y, z), r - 0.03, 0.012, "x", segments=10), col("D7B98E"))
            m.add(torus((x, y, z), r * 0.45, 0.01, "x", 10, 4), col("B08A5C"))
    from mathutils import Matrix
    for x in (0.55, 1.45):
        loop = rounded_rect_points((0, 0, 0), 0.43, 0.46, 0.28, 6)
        rope = transformed(sweep(loop, 0.022, closed=True, segments=6),
                           Matrix.Translation((x, 0.43, 0.5)) @ Matrix.Rotation(math.pi / 2, 4, "Z"))
        m.add(rope, col("D2B48C"))
    m.add(sweep([(1.45, 0.86, 0.5), (1.3, 0.9, 0.45), (1.2, 0.87, 0.35)], 0.02, segments=6), col("D2B48C"))


@item
def marshmallows(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.08, 0.02, 0.16), (1.92, 0.84, 1.84), 0.36, 6), col(a))
    for z0, z1 in ((0.04, 0.2), (1.8, 1.96)):
        m.add(box((0.14, 0.32, z0), (1.86, 0.52, z1), 0.04), col(shade(a, 0.93)))
        for k in range(9):
            x = 0.2 + k * 0.2
            m.add(box((x, 0.33, z0 - 0.004), (x + 0.04, 0.51, z1 + 0.004)), col(shade(a, 0.88)))
    m.add(ellipsoid((1.0, 0.8, 1.0), (0.62, 0.06, 0.48)), col("F48FB1"))
    m.add(ellipsoid((1.0, 0.83, 1.0), (0.4, 0.05, 0.3)), col("42A5F5"))
    for x, z in ((0.85, 1.0), (1.05, 0.92), (1.12, 1.1)):
        m.add(cyl((x, 0.87, z), 0.07, 0.05, "y", segments=14, bevel=0.015), col(b))
    m.add(box((0.3, 0.25, 0.12), (1.7, 0.55, 0.15), 0.05), col("42A5F5"))
    m.add(box((0.45, 0.33, 0.1), (1.55, 0.47, 0.12), 0.02), col(WHITE))


@item
def flamingo(m, P):
    a, b = P["a"], P["b"]
    m.add(ellipsoid((0.98, 0.44, 0.5), (0.9, 0.4, 0.42), 28, 14), col(a))
    for z in (0.12, 0.88):
        m.add(ellipsoid((1.15, 0.55, z), (0.5, 0.2, 0.06)), col(shade(a, 0.85)),
              transform=rotate_about((1.15, 0.55, z), "Z", 10))
    m.add(hull([(1.75, 0.45, 0.42), (1.75, 0.45, 0.58), (1.96, 0.75, 0.5), (1.6, 0.6, 0.5)], 0.03), col(shade(a, 0.85)))
    neck = [(0.45, 0.62, 0.5), (0.32, 0.95, 0.5), (0.32, 1.35, 0.5), (0.62, 1.7, 0.5), (0.65, 2.0, 0.5), (0.5, 2.3, 0.5)]
    m.add(sweep(neck, 0.17, radius_end=0.13, segments=16), col(a))
    m.add(sphere((0.5, 2.45, 0.5), 0.3, segments=24, rings=14), col(a))
    m.add(sweep([(0.74, 2.45, 0.5), (1.15, 2.42, 0.5), (1.45, 2.3, 0.5), (1.6, 2.12, 0.5)], 0.12, radius_end=0.05, segments=14), col(b))
    m.add(sphere((1.6, 2.1, 0.5), 0.06), col(BLACK))
    for z in (0.26, 0.74):
        m.add(sphere((0.66, 2.58, z), 0.07), col(WHITE))
        m.add(sphere((0.7, 2.6, z + (0.03 if z < 0.5 else -0.03)), 0.04), col(BLACK))
    m.add(cyl((0.08, 0.5, 0.5), 0.06, 0.08, "x", segments=10), col(WHITE))
    m.add(sweep(rounded_rect_points((0.98, 0.44, 0.5), 0.86, 0.38, 0.3, 6), 0.012, closed=True, segments=5), col(shade(a, 1.2)))


@item
def gnome(m, P):
    a, b = P["a"], P["b"]
    m.add(cyl((0.5, 0.04, 0.5), 0.36, 0.08, "y", segments=24, bevel=0.02), col("6B8E3A"))
    for x in (0.38, 0.62):
        m.add(ellipsoid((x, 0.13, 0.4), (0.1, 0.07, 0.15)), col(BLACK))
    m.add(lathe([(0.29, 0.1), (0.31, 0.3), (0.27, 0.62), (0.2, 0.8)], (0.5, 0, 0.5), "y", 24), col(a))
    m.add(torus((0.5, 0.42, 0.5), 0.295, 0.035, "y", 24, 8), col("4E342E"))
    m.add(box((0.44, 0.38, 0.17), (0.56, 0.46, 0.21), 0.01), metal(GOLD))
    for x in (0.2, 0.8):
        m.add(ellipsoid((x, 0.58, 0.5), (0.08, 0.17, 0.09)), col(a))
        m.add(sphere((x, 0.42, 0.43), 0.07), col(SKIN))
    m.add(sphere((0.5, 0.98, 0.5), 0.2), col(SKIN))
    m.add(ellipsoid((0.5, 0.78, 0.36), (0.2, 0.26, 0.13)), col(WHITE))
    m.add(hull([(0.32, 0.88, 0.32), (0.68, 0.88, 0.32), (0.5, 0.48, 0.3), (0.5, 0.88, 0.45)]), col(WHITE))
    m.add(sphere((0.5, 0.96, 0.29), 0.07), col("F4A38C"))
    for x in (0.42, 0.58):
        m.add(sphere((x, 1.05, 0.33), 0.028), col(BLACK))
        m.add(ellipsoid((x, 1.1, 0.33), (0.05, 0.015, 0.02)), col(WHITE))
    m.add(torus((0.5, 1.1, 0.5), 0.21, 0.035, "y", 24, 8), col(shade(b, 0.85)))
    hat = [(0.22, 1.1), (0.2, 1.3), (0.15, 1.55), (0.09, 1.75), (0.02, 1.96)]
    m.add(lathe(hat, (0.5, 0, 0.5), "y", 24), col(b), transform=rotate_about((0.5, 1.1, 0.5), "X", -8))


@item
def duck(m, P):
    a, b = P["a"], P["b"]
    m.add(ellipsoid((1.0, 0.5, 1.0), (0.94, 0.48, 0.9), 28, 16), col(a))
    m.add(ellipsoid((0.12, 0.72, 1.0), (0.18, 0.18, 0.28)), col(a), transform=rotate_about((0.12, 0.72, 1.0), "Z", -30))
    for z in (0.2, 1.8):
        m.add(ellipsoid((0.95, 0.62, z), (0.48, 0.24, 0.06)), col(shade(a, 0.88)),
              transform=rotate_about((0.95, 0.62, z), "Z", 12))
    m.add(sphere((0.55, 1.42, 1.5), 0.46, segments=24, rings=14), col(a))
    m.add(ellipsoid((1.3, 1.36, 1.5), (0.4, 0.11, 0.27)), col(b))
    m.add(ellipsoid((1.27, 1.27, 1.5), (0.32, 0.06, 0.22)), col(shade(b, 0.85)))
    for z in (1.27, 1.73):
        m.add(sphere((0.88, 1.6, z), 0.08), col(BLACK))
        m.add(sphere((0.93, 1.64, z + (0.02 if z > 1.5 else -0.02)), 0.03), col(WHITE))
    m.add(ellipsoid((0.55, 1.86, 1.5), (0.08, 0.05, 0.03)), col(a))


@item
def clock(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.06, 0.02, 0.1), (0.94, 0.32, 0.9), 0.03), col(shade(a, 0.8)))
    m.add(box((0.14, 0.3, 0.16), (0.86, 3.45, 0.84), 0.03), col(a))
    for x in (0.16, 0.84):
        m.add(cyl((x, 1.9, 0.18), 0.045, 3.0, "y", segments=10), col(shade(a, 1.25)))
        m.add(cyl((x, 0.42, 0.18), 0.07, 0.08, "y", segments=10), metal(GOLD))
        m.add(cyl((x, 3.38, 0.18), 0.07, 0.08, "y", segments=10), metal(GOLD))
    m.add(box((0.08, 3.45, 0.12), (0.92, 3.72, 0.88), 0.04), col(shade(a, 0.8)))
    m.add(hull([(0.12, 3.72, 0.16), (0.88, 3.72, 0.16), (0.12, 3.72, 0.84), (0.88, 3.72, 0.84),
                (0.3, 3.86, 0.3), (0.7, 3.86, 0.3), (0.3, 3.86, 0.7), (0.7, 3.86, 0.7)], 0.02), col(a))
    for x in (0.18, 0.5, 0.82):
        m.add(sphere((x, 3.9 if x == 0.5 else 3.78, 0.2 if x != 0.5 else 0.5), 0.05), metal(GOLD))
    m.add(cyl((0.5, 2.6, 0.15), 0.3, 0.04, "z", segments=32), col(b))
    m.add(torus((0.5, 2.6, 0.13), 0.31, 0.025, "z", 32, 6), metal(GOLD))
    for k in range(12):
        ang = k / 12 * 2 * math.pi
        m.add(box((0.5 + 0.24 * math.cos(ang) - 0.012, 2.6 + 0.24 * math.sin(ang) - 0.012, 0.125),
                  (0.5 + 0.24 * math.cos(ang) + 0.012, 2.6 + 0.24 * math.sin(ang) + 0.012, 0.13)), col(BLACK))
    m.add(box((0.488, 2.6, 0.12), (0.512, 2.8, 0.125)), col(BLACK))
    m.add(box((0.5, 2.588, 0.12), (0.66, 2.612, 0.125)), col(BLACK))
    m.add(sphere((0.5, 2.6, 0.12), 0.02), metal(GOLD))
    m.add(box((0.26, 0.55, 0.13), (0.74, 2.15, 0.16)), col(shade(a, 0.6)))
    m.add(box((0.28, 0.57, 0.12), (0.72, 2.13, 0.125)), glass("3E2A1A"))
    m.add(rod((0.5, 2.1, 0.135), (0.5, 0.95, 0.135), 0.012), metal(GOLD))
    m.add(cyl((0.5, 0.9, 0.135), 0.11, 0.025, "z", segments=20), metal(GOLD))
    for x in (0.4, 0.6):
        m.add(rod((x, 2.05, 0.14), (x, 1.5, 0.14), 0.008), metal(GOLD))
        m.add(cyl((x, 1.42, 0.14), 0.035, 0.16, "y", segments=10), metal(GOLD))


@item
def armchair(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.08, 0.12, 0.08), (1.92, 0.5, 1.92), 0.08), col(a))
    m.add(box((0.08, 0.12, 0.06), (1.92, 0.24, 0.1)), col(shade(a, 0.75)))
    m.add(box((0.3, 0.48, 0.12), (1.7, 0.78, 1.55), 0.14, 3), col(shade(a, 1.15)))
    for x0, x1 in ((0.05, 0.32), (1.68, 1.95)):
        m.add(box((x0, 0.45, 0.08), (x1, 0.88, 1.92), 0.12, 3), col(a))
        m.add(cyl(((x0 + x1) / 2, 0.88, 0.95), 0.15, 1.82, "z", segments=16), col(a))
    m.add(box((0.08, 0.45, 1.5), (1.92, 1.9, 1.95), 0.18, 3), col(b))
    m.add(box((0.3, 0.75, 1.35), (1.7, 1.75, 1.55), 0.14, 3), col(shade(b, 1.2)))
    for x in (0.65, 1.0, 1.35):
        for y in (1.05, 1.45):
            m.add(sphere((x, y, 1.34), 0.035), col(shade(b, 0.8)))
    m.add(cyl((1.0, 1.9, 1.72), 0.32, 0.01, "y", segments=24), col(WHITE))
    for k in range(12):
        ang = k / 12 * 2 * math.pi
        m.add(sphere((1.0 + 0.32 * math.cos(ang), 1.9, 1.72 + 0.32 * math.sin(ang)), 0.04, (1, 0.3, 1)), col(WHITE))
    m.add(box((0.4, 0.78, 0.4), (1.0, 0.98, 0.9), 0.1, 3), col("FFF59D"),
          transform=rotate_about((0.7, 0.88, 0.65), "Y", 20))
    for x in (0.18, 1.82):
        for z in (0.18, 1.82):
            m.add(cyl((x, 0.07, z), 0.06, 0.12, "y", radius2=0.08, segments=10), col(WOOD))


@item
def cat_tree(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.04, 0.02, 0.04), (1.96, 0.24, 1.96), 0.05), col(a))
    m.add(box((1.02, 0.24, 0.08), (1.92, 0.96, 0.96), 0.06), col(b))
    m.add(cyl((1.47, 0.58, 0.07), 0.24, 0.04, "z", segments=20), col(DARK))
    m.add(cyl((0.5, 1.22, 1.5), 0.17, 1.96, "y", segments=16), col("D2B48C"))
    for y in range(6):
        m.add(torus((0.5, 0.4 + y * 0.3, 1.5), 0.17, 0.02, "y", 16, 6), col("BCA078"))
    m.add(box((0.04, 2.2, 1.04), (1.96, 2.42, 1.96), 0.05), col(a))
    m.add(torus((1.0, 2.5, 1.5), 0.35, 0.1, "y"), col(b))
    m.add(cyl((1.0, 2.45, 1.5), 0.32, 0.06, "y"), col(shade(b, 1.2)))
    m.add(ellipsoid((1.65, 2.5, 1.2), (0.12, 0.07, 0.07)), col("9E9E9E"))
    m.add(rod((1.53, 2.47, 1.2), (1.4, 2.43, 1.15), 0.01), col("9E9E9E"))


@item
def lamp(m, P):
    a, b = P["a"], P["b"]
    m.add(cyl((0.5, 0.05, 0.5), 0.32, 0.08, "y", segments=28, bevel=0.02), col(a))
    m.add(lathe([(0.1, 0.09), (0.05, 0.16), (0.04, 0.2)], (0.5, 0, 0.5), "y", 16), col(a))
    m.add(rod((0.5, 0.18, 0.5), (0.5, 2.35, 0.5), 0.032), metal("9E9E9E"))
    m.add(shell([(0.42, 2.2), (0.36, 2.55), (0.28, 2.88)], (0.5, 0, 0.5), "y", 0.015, 28), col(b))
    for y, r in ((2.21, 0.42), (2.87, 0.285)):
        m.add(torus((0.5, y, 0.5), r, 0.015, "y", 28, 5), col(shade(b, 0.7)))
    m.add(sphere((0.5, 2.42, 0.5), 0.09), glow("FFF3C4"))
    m.add(rod((0.62, 2.35, 0.5), (0.62, 2.1, 0.5), 0.005, 4), metal(GOLD))
    m.add(sphere((0.62, 2.08, 0.5), 0.02), metal(GOLD))


@item
def ficus(m, P):
    a, b = P["a"], P["b"]
    m.add(lathe([(0.24, 0.02), (0.32, 0.55), (0.37, 0.58), (0.37, 0.7), (0.34, 0.7)], (0.5, 0, 0.5), "y", 24), col(a))
    m.add(cyl((0.5, 0.66, 0.5), 0.31, 0.04, "y", segments=20), col("4E342E"))
    m.add(sweep([(0.5, 0.66, 0.5), (0.46, 0.95, 0.48), (0.52, 1.2, 0.52), (0.48, 1.4, 0.5)], 0.04, radius_end=0.02, segments=8), col(WOOD))
    import random
    rng = random.Random(11)
    for _ in range(48):
        y = rng.uniform(1.08, 1.88)
        spread = 0.38 * (1 - abs(y - 1.45) / 0.55) * rng.uniform(0.35, 1.0)
        ang = rng.uniform(0, 2 * math.pi)
        x, z = 0.5 + math.cos(ang) * spread, 0.5 + math.sin(ang) * spread
        leaf = ellipsoid((x, y, z), (0.11, 0.025, 0.06), 10, 6)
        m.add(transformed(leaf, rotate_about((x, y, z), "Y", math.degrees(-ang)) @ rotate_about((x, y, z), "Z", rng.uniform(-30, 30))),
              col(shade(b, rng.uniform(0.75, 1.1))))
    for y in (1.2, 1.45, 1.7):
        m.add(sweep([(0.5, y - 0.15, 0.5), (0.5 + 0.18, y, 0.5 + 0.1), (0.5 + 0.28, y + 0.05, 0.5 + 0.12)], 0.012, segments=5), col(WOOD))


@item
def moving_box(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.05, 0.02, 0.05), (1.95, 0.92, 1.95), 0.02), col(a))
    m.add(box((0.05, 0.92, 0.99), (1.95, 0.925, 1.01)), col(shade(a, 0.7)))
    m.add(box((0.9, 0.92, 0.04), (1.1, 0.935, 1.96)), col("D9C9A8"))
    m.add(box((0.9, 0.5, 0.035), (1.1, 0.935, 0.05)), col("D9C9A8"))
    m.add(box((0.3, 0.3, 0.035), (0.8, 0.62, 0.05)), col(WHITE))
    for k, w in enumerate((0.36, 0.3, 0.38, 0.28)):
        m.add(box((0.36 + k * 0.1, 0.4, 0.03), (0.42 + k * 0.1, 0.55, 0.036)), col(BLACK))
    m.add(hull([(1.4, 0.3, 0.035), (1.7, 0.3, 0.035), (1.55, 0.58, 0.035), (1.4, 0.3, 0.04), (1.7, 0.3, 0.04)]), col("D32F2F"))
    m.add(box((1.53, 0.35, 0.03), (1.57, 0.5, 0.042)), col(WHITE))
    for x in (0.05, 1.95):
        m.add(box((x - 0.006, 0.45, 0.7), (x + 0.006, 0.62, 1.3), 0.005), col(shade(a, 0.6)))


@item
def small_box(m, P):
    a = P["a"]
    m.add(box((0.08, 0.02, 0.08), (0.92, 0.74, 0.92), 0.02), col(a))
    m.add(box((0.12, 0.72, 0.12), (0.88, 0.74, 0.88)), col("3E2C1C"))
    for (x, z, ang, c) in ((0.35, 0.4, 0.3, BLACK), (0.6, 0.55, 1.2, WHITE), (0.45, 0.65, 2.0, "E53935"),
                           (0.65, 0.35, 2.6, "1E88E5")):
        t = torus((x, 0.8, z), 0.16, 0.025, "x")
        rot = Matrix.Translation(Vector((x, 0.8, z))) @ Matrix.Rotation(ang, 4, "Y") @ Matrix.Translation(Vector((-x, -0.8, -z)))
        m.add(t, col(c), transform=rot)


@item
def portrait(m, P):
    a, b = P["a"], P["b"]
    for mn, mx in (((0.05, 0.05, 0.3), (1.95, 0.25, 0.5)), ((0.05, 1.75, 0.3), (1.95, 1.95, 0.5)),
                   ((0.05, 0.05, 0.3), (0.25, 1.95, 0.5)), ((1.75, 0.05, 0.3), (1.95, 1.95, 0.5))):
        m.add(box(mn, mx, 0.04), metal(a))
    m.add(box((0.25, 0.25, 0.4), (1.75, 1.75, 0.46)), col(b))
    m.add(ellipsoid((1.0, 1.1, 0.4), (0.32, 0.42, 0.03)), col(SKIN))
    m.add(ellipsoid((1.0, 1.45, 0.39), (0.34, 0.12, 0.03)), col("BDBDBD"))
    m.add(ellipsoid((1.0, 0.95, 0.37), (0.18, 0.05, 0.03)), col("6D4C41"))
    for x in (0.88, 1.12):
        m.add(sphere((x, 1.15, 0.37), 0.04, (1, 1, 0.5)), col(BLACK))
    m.add(ellipsoid((1.0, 0.45, 0.4), (0.5, 0.25, 0.03)), col("37474F"))
    m.add(rod((1.0, 1.6, 0.52), (1.0, 0.03, 0.95), 0.04), col(WOOD))


@item
def lava_lamp(m, P):
    a, b = P["a"], P["b"]
    m.add(lathe([(0.3, 0.02), (0.29, 0.08), (0.18, 0.55), (0.2, 0.6)], (0.5, 0, 0.5), "y", 28), metal("8E8E93"))
    for k in range(6):
        ang = k / 6 * 2 * math.pi
        m.add(ellipsoid((0.5 + 0.24 * math.cos(ang), 0.25, 0.5 + 0.24 * math.sin(ang)), (0.03, 0.06, 0.03)), glow(b))
    m.add(lathe([(0.2, 0.6), (0.27, 1.05), (0.13, 1.55)], (0.5, 0, 0.5), "y", 28), glass(shade(b, 0.55)))
    for x, y, z, r in ((0.5, 0.78, 0.5, 0.17), (0.44, 1.08, 0.48, 0.12), (0.57, 1.3, 0.52, 0.08), (0.52, 1.46, 0.5, 0.05)):
        m.add(sphere((x, y, z), r, (1, 1.25, 1)), glow(b))
    m.add(lathe([(0.14, 1.55), (0.12, 1.75), (0.08, 1.88)], (0.5, 0, 0.5), "y", 28), metal("8E8E93"))
    m.add(sweep([(0.68, 0.1, 0.5), (0.85, 0.05, 0.6), (0.95, 0.02, 0.85)], 0.015, segments=5), col(BLACK))


@item
def disco_ball(m, P):
    a = P["a"]
    tiles = sphere((0.5, 0.47, 0.5), 0.4, segments=32, rings=18)
    m.add_faces_by(tiles, lambda c, n: metal(a) if int((c.x + c.y * 3.1 + c.z * 1.7) * 17) % 5 else metal("F5F5F5"))
    m.add(cyl((0.5, 0.9, 0.5), 0.05, 0.08, "y", segments=12), metal(STEEL))
    m.add(torus((0.5, 0.96, 0.5), 0.035, 0.012, "z", 12, 6), metal(STEEL))
    m.add(cyl((0.5, 0.035, 0.5), 0.18, 0.07, "y", segments=20, radius2=0.14), col(DARK))


@item
def trombone(m, P):
    a, b = P["a"], P["b"]
    brass = metal(a)
    y = 0.42
    m.add(sweep([(0.4, y, 0.45), (2.6, y, 0.45)], 0.05, segments=12), brass)
    m.add(sweep([(0.4, y, 0.62), (2.6, y, 0.62)], 0.05, segments=12), brass)
    m.add(sweep(arc_points((2.6, y, 0.535), 0.085, -90, 90, "xz", 10), 0.05, segments=12), brass)
    m.add(sweep(arc_points((0.4, y, 0.62), 0.17, 270, 90, "xz", 10)[::-1][:6], 0.05, segments=12), brass)
    m.add(sweep([(0.4, y, 0.45), (0.3, y, 0.55), (0.25, y, 0.9), (0.25, y, 2.3), (0.35, y, 2.55), (0.55, y, 2.6)], 0.055, segments=12), brass)
    m.add(sweep([(0.55, y, 2.6), (1.7, y, 2.6)], 0.06, segments=12), brass)
    m.add(shell([(0.07, 0.0), (0.1, 0.5), (0.18, 0.85), (0.32, 1.05), (0.46, 1.2)], (1.7, y, 2.6), "x", 0.02, 30), metal(b))
    m.add(torus((2.9, y, 2.6), 0.46, 0.025, "x", 30, 6), metal(b))
    for z in (0.95, 1.9):
        m.add(rod((0.15, y, z), (0.35, y, z), 0.03), brass)
    m.add(rod((1.3, y, 0.45), (1.3, y, 0.62), 0.03), brass)
    m.add(rod((1.1, y, 0.62), (1.1, y, 2.6), 0.025), brass)
    m.add(lathe([(0.02, 0.0), (0.03, 0.1), (0.07, 0.18), (0.06, 0.2)], (2.65, y, 0.62), "x", 14), metal(STEEL))
    m.add(cyl((2.4, y, 0.45), 0.065, 0.3, "x", segments=12), metal(shade(a, 1.15)))


@item
def tuba(m, P):
    a, b = P["a"], P["b"]
    brass = metal(a)
    bell = [(0.2, 1.0), (0.24, 1.25), (0.36, 1.5), (0.55, 1.72), (0.8, 1.88), (0.92, 1.96)]
    m.add(shell(bell, (1.0, 0, 1.0), "y", thickness=0.035, segments=36), brass)
    m.add(cyl((1.0, 1.08, 1.0), 0.17, 0.02, "y", segments=20), col("2A1F0B"))
    m.add(torus((1.0, 1.96, 1.0), 0.905, 0.03, "y", 36, 8), brass)
    m.add(sweep(arc_points((0.5, 0.5, 1.5), 0.3, -90, 260, "zy", 26), 0.09, segments=14), brass)
    m.add(sweep(arc_points((0.5, 0.5, 1.5), 0.17, 0, 340, "zy", 20), 0.06, segments=10), metal(b))
    for k in range(3):
        m.add(cyl((0.25 + k * 0.17, 0.66, 1.18), 0.06, 0.38, "y", segments=12), metal(b))
        m.add(cyl((0.25 + k * 0.17, 0.88, 1.18), 0.07, 0.05, "y", segments=12), metal(STEEL))
        m.add(cyl((0.25 + k * 0.17, 0.93, 1.18), 0.04, 0.05, "y", segments=10), col(WHITE))
    m.add(sweep([(0.5, 0.8, 1.5), (0.7, 0.92, 1.35), (0.86, 1.02, 1.12)], 0.14, radius_end=0.2, segments=16), brass)
    m.add(sweep([(0.5, 0.42, 1.78), (0.3, 0.75, 1.82), (0.12, 0.9, 1.62)], 0.035, segments=8), metal(b))
    m.add(cyl((0.12, 0.95, 1.6), 0.045, 0.08, "y", radius2=0.03), metal(STEEL))


@item
def unicycle(m, P):
    a, b, c = P["a"], P["b"], P["c"]
    m.add(torus((0.5, 0.45, 0.5), 0.36, 0.07, "z", 28, 10), col(a))
    m.add(torus((0.5, 0.45, 0.5), 0.3, 0.02, "z", 28, 6), metal(b))
    for i in range(8):
        ang = i / 8 * math.pi
        dx, dy = 0.3 * math.cos(ang), 0.3 * math.sin(ang)
        m.add(rod((0.5 - dx, 0.45 - dy, 0.5), (0.5 + dx, 0.45 + dy, 0.5), 0.008, 6), metal(b))
    m.add(cyl((0.5, 0.45, 0.5), 0.05, 0.3, "z"), metal(b))
    for z in (0.38, 0.62):
        m.add(rod((0.5, 0.45, z), (0.5, 1.5, 0.5 + (z - 0.5) * 0.3), 0.035), metal(b))
    m.add(rod((0.5, 1.5, 0.5), (0.5, 2.6, 0.5), 0.04), metal(b))
    m.add(ellipsoid((0.5, 2.72, 0.5), (0.17, 0.1, 0.32)), col(c))
    for z, x in ((0.3, 0.3), (0.7, 0.7)):
        m.add(box((x - 0.08, 0.42, z - 0.04), (x + 0.08, 0.48, z + 0.04)), col(DARK))


@item
def cream_pies(m, P):
    a, b = P["a"], P["b"]
    for x in (0.5, 1.5):
        m.add(cyl((x, 0.1, 0.5), 0.38, 0.16, "y", radius2=0.43, segments=24), metal(STEEL))
        m.add(torus((x, 0.2, 0.5), 0.38, 0.06, "y", 24), col(b))
        m.add(ellipsoid((x, 0.24, 0.5), (0.36, 0.26, 0.36)), col(a))
        m.add(sphere((x, 0.52, 0.5), 0.06), col("C62828"))


@item
def rubber_chicken(m, P):
    a, b = P["a"], P["b"]
    m.add(ellipsoid((0.5, 0.3, 1.2), (0.22, 0.2, 0.58)), col(a))
    m.add(rod((0.5, 0.36, 0.7), (0.5, 0.5, 0.3), 0.07), col(a), smooth=True)
    m.add(sphere((0.5, 0.55, 0.27), 0.14), col(a))
    for k in range(3):
        m.add(sphere((0.5, 0.7, 0.22 + k * 0.07), 0.05), col(b))
    m.add(rod((0.5, 0.53, 0.14), (0.5, 0.5, 0.03), 0.05, radius2=0.01), col("FF8F00"))
    m.add(sphere((0.5, 0.42, 0.18), 0.05, (1, 1.6, 1)), col(b))
    for x in (0.4, 0.6):
        m.add(rod((x, 0.22, 1.6), (x, 0.15, 1.95), 0.025), col("FF8F00"))
        m.add(sphere((0.38 if x < 0.5 else 0.62, 0.62, 0.22), 0.025), col(BLACK))
    for x in (0.27, 0.73):
        m.add(ellipsoid((x, 0.32, 1.15), (0.04, 0.12, 0.3)), col(shade(a, 0.9)))


@item
def juggling_pins(m, P):
    a, b = P["a"], P["b"]
    profile = [(0.07, 0.0), (0.09, 0.4), (0.06, 1.0), (0.14, 1.75), (0.15, 2.2), (0.1, 2.7), (0.03, 2.82)]
    for (y, z), stripe in zip(((0.17, 0.27), (0.17, 0.73), (0.43, 0.5)), (b, "1E88E5", "43A047")):
        m.add(lathe(profile, (0.09, y, z), "x", 18), col(a))
        for x, r in ((1.32, 0.085), (1.55, 0.11), (2.4, 0.14)):
            m.add(torus((x, y, z), r, 0.022, "x", 18, 6), col(stripe))
        m.add(cyl((0.08, y, z), 0.075, 0.03, "x", segments=12), col(stripe))


@item
def big_shoes(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.05, 0.02, 0.1), (2.75, 0.16, 0.9), 0.06), col(WHITE))
    for k in range(10):
        m.add(box((0.15 + k * 0.26, 0.0, 0.12), (0.25 + k * 0.26, 0.03, 0.88)), col("BDBDBD"))
    m.add(ellipsoid((1.35, 0.42, 0.5), (1.28, 0.34, 0.4)), col(a))
    m.add(sphere((2.45, 0.46, 0.5), 0.48, (1.1, 0.95, 0.95), 28, 16), glass(b))
    m.add(cyl((0.5, 1.12, 0.5), 0.38, 1.6, "y", segments=28, bevel=0.06), col(a))
    for k in range(4):
        m.add(torus((0.5, 1.15 + k * 0.18, 0.5), 0.385, 0.03, "y", 28, 6), col(WHITE if k % 2 else "FFEB3B"))
    m.add(torus((0.5, 1.9, 0.5), 0.36, 0.06, "y"), col(WHITE))
    for k in range(4):
        x = 0.95 + k * 0.22
        m.add(sweep([(x, 0.74, 0.3), (x + 0.2, 0.76, 0.7)], 0.02, segments=6), col(WHITE))
        m.add(sweep([(x, 0.74, 0.7), (x + 0.2, 0.76, 0.3)], 0.02, segments=6), col(WHITE))
    m.add(sweep([(0.95, 0.76, 0.5), (0.8, 0.9, 0.35), (0.75, 0.8, 0.2)], 0.02, segments=6), col(WHITE))
    m.add(sweep([(0.95, 0.76, 0.5), (0.8, 0.9, 0.65), (0.75, 0.8, 0.82)], 0.02, segments=6), col(WHITE))


@item
def seltzer(m, P):
    a = P["a"]
    m.add(lathe([(0.25, 0.02), (0.28, 0.06), (0.28, 0.48), (0.22, 0.58), (0.13, 0.64)], (0.5, 0, 0.5), "y", 28), glass(a))
    for k in range(5):
        m.add(torus((0.5, 0.12 + k * 0.08, 0.5), 0.282, 0.008, "y", 28, 4), metal(STEEL))
    m.add(lathe([(0.14, 0.62), (0.15, 0.68), (0.12, 0.8), (0.08, 0.86)], (0.5, 0, 0.5), "y", 20), metal(STEEL))
    m.add(sweep([(0.5, 0.78, 0.42), (0.5, 0.79, 0.25), (0.5, 0.74, 0.08)], 0.035, radius_end=0.022, segments=10), metal(STEEL))
    m.add(sweep([(0.5, 0.86, 0.56), (0.5, 0.92, 0.4), (0.5, 0.9, 0.2)], 0.02, segments=8), metal(STEEL))
    m.add(rod((0.5, 0.6, 0.5), (0.5, 0.05, 0.5), 0.012), glass("E1F5FE"))


@item
def folded_clown(m, P):
    a, b, c = P["a"], P["b"], P["c"]
    for side, z in ((-1, 0.32), (1, 0.68)):
        leg = [(1.5, 0.45, z), (1.0 - 0.1 * side, 0.55, z), (0.5, 0.35, z)] if side < 0 else \
              [(1.5, 0.45, z), (2.0, 0.55, z), (2.5, 0.35, z)]
        m.add_faces_by(sweep(leg, 0.2, segments=14),
                       lambda cc, n: col(c) if int(cc.x * 6) % 2 == 0 else col("FFEB3B"))
    for x in (0.25, 2.75):
        m.add(ellipsoid((x, 0.2, 0.5), (0.24, 0.18, 0.4)), col("E53935"))
    m.add(lathe([(0.36, 0.6), (0.34, 1.2), (0.28, 1.75)], (1.5, 0, 0.5), "y", 22), col(a))
    for y in (0.95, 1.25, 1.55):
        m.add(sphere((1.5, y, 0.17), 0.07), col("E53935" if y != 1.25 else "2196F3"))
    for side in (-1, 1):
        m.add(sweep([(1.5 + side * 0.28, 1.6, 0.5), (1.5 + side * 0.42, 1.3, 0.4), (1.5 + side * 0.4, 1.05, 0.25)], 0.08, segments=10), col(a))
        m.add(sphere((1.5 + side * 0.4, 1.0, 0.24), 0.09), col(WHITE))
    m.add(torus((1.5, 1.82, 0.5), 0.3, 0.08, "y", 24, 8), col("FFEB3B"))
    m.add(sphere((1.5, 2.3, 0.5), 0.34), col(WHITE))
    m.add(sphere((1.5, 2.28, 0.15), 0.09), col(b))
    for x in (1.13, 1.87):
        m.add(ico((x, 2.42, 0.5), 0.17, 1), col(b))
    for x in (1.38, 1.62):
        m.add(sphere((x, 2.42, 0.2), 0.035), col(BLACK))
    m.add(torus((1.5, 2.14, 0.2), 0.1, 0.02, "z", 12, 5, arc=0.5, scale=(1, -1, 1)), col("C62828"))
    m.add(cyl((1.5, 2.78, 0.5), 0.12, 0.34, "y", radius2=0.02), col("7E57C2"))
    m.add(sphere((1.5, 2.96, 0.5), 0.04), col("FFEB3B"))


@item
def moose(m, P):
    a, b = P["a"], P["b"]
    m.add(hull([(1.1, 0.12, 0.84), (1.9, 0.12, 0.84), (1.1, 1.62, 0.84), (1.9, 1.62, 0.84),
                (1.16, 0.18, 0.96), (1.84, 0.18, 0.96), (1.16, 1.56, 0.96), (1.84, 1.56, 0.96)], 0.03), col("5D3A1A"))
    m.add(sweep([(1.5, 0.95, 0.8), (1.5, 0.95, 0.55)], 0.36, radius_end=0.33, segments=20), col(a))
    m.add(sweep([(1.5, 1.08, 0.58), (1.5, 0.75, 0.35), (1.5, 0.42, 0.2)], 0.3, radius_end=0.22, segments=20), col(a))
    m.add(ellipsoid((1.5, 0.36, 0.17), (0.25, 0.2, 0.15)), col(shade(a, 0.75)))
    for x in (1.41, 1.59):
        m.add(sphere((x, 0.32, 0.04), 0.045), col(BLACK))
    for x in (1.28, 1.72):
        m.add(sphere((x, 1.08, 0.36), 0.065), col(BLACK))
        m.add(sphere((x + (0.015 if x > 1.5 else -0.015), 1.1, 0.31), 0.02), col(WHITE))
    for side in (-1, 1):
        m.add(ellipsoid((1.5 + side * 0.42, 1.28, 0.62), (0.16, 0.08, 0.1)), col(a),
              transform=rotate_about((1.5 + side * 0.42, 1.28, 0.62), "Z", side * 25))
    m.add(ellipsoid((1.5, 0.15, 0.4), (0.09, 0.15, 0.08)), col(shade(a, 0.6)))
    for side in (-1, 1):
        cx = 1.5 + side * 1.0
        m.add(sweep([(1.5 + side * 0.3, 1.32, 0.6), (1.5 + side * 0.55, 1.45, 0.55), (cx - side * 0.2, 1.52, 0.5)],
                    0.07, radius_end=0.06, segments=10), col(b))
        m.add(ellipsoid((cx, 1.55, 0.5), (0.44, 0.24, 0.07)), col(b))
        for k in range(4):
            tx = cx + side * (-0.28 + k * 0.18)
            m.add(sweep([(tx, 1.68, 0.5), (tx + side * 0.04, 1.84, 0.5), (tx + side * 0.06, 1.95, 0.5)],
                        0.045, radius_end=0.015, segments=8), col(b))


@item
def cutout(m, P):
    a, b = P["a"], P["b"]
    from mathutils import Matrix
    shirt, pants, hair = "E74C3C", "34495E", "5D4037"
    flat = Matrix.Translation((0, 0, 0.5)) @ Matrix.Diagonal((1, 1, 0.22, 1)) @ Matrix.Translation((0, 0, -0.5))
    parts = [
        (sweep([(0.72, 0.08, 0.5), (0.72, 0.95, 0.5)], 0.17, segments=12), pants),
        (sweep([(1.28, 0.08, 0.5), (1.28, 0.95, 0.5)], 0.17, segments=12), pants),
        (ellipsoid((0.72, 0.1, 0.45), (0.18, 0.08, 0.2)), BLACK),
        (ellipsoid((1.28, 0.1, 0.45), (0.18, 0.08, 0.2)), BLACK),
        (box((0.48, 0.92, 0.38), (1.52, 1.95, 0.62), 0.12, 3), shirt),
        (sweep([(0.55, 1.85, 0.5), (0.3, 1.5, 0.5), (0.22, 1.15, 0.5)], 0.1, segments=10), shirt),
        (sphere((0.22, 1.08, 0.5), 0.1), b),
        (sweep([(1.45, 1.85, 0.5), (1.72, 2.05, 0.5), (1.8, 2.4, 0.5)], 0.1, segments=10), shirt),
        (sphere((1.8, 2.5, 0.5), 0.11), b),
        (cyl((1.0, 2.03, 0.5), 0.1, 0.12, "y"), b),
        (sphere((1.0, 2.45, 0.5), 0.38, (1, 1.05, 1), 28, 14), b),
        (sphere((1.0, 2.62, 0.52), 0.38, (1.04, 0.62, 1.05), 28, 14), hair),
    ]
    for prim, colour in parts:
        m.add(prim, col(colour), transform=flat)
    m.add(box((0.85, 1.6, 0.44), (1.15, 1.95, 0.445)), col(WHITE))
    for x in (0.86, 1.14):
        m.add(sphere((x, 2.48, 0.415), 0.045, (1, 1.2, 0.3)), col(BLACK))
    m.add(torus((1.0, 2.33, 0.42), 0.12, 0.022, "z", 14, 5, arc=0.5, scale=(1, -1, 1)), col("C62828"))
    # Cardboard back and stand: the side you see when it's facing away.
    m.add(box((0.2, 0.06, 0.585), (1.8, 2.0, 0.6), 0.02), col(a))
    m.add(box((0.15, 0.02, 0.45), (1.85, 0.08, 0.6)), col(shade(a, 0.85)))
    m.add(hull([(0.85, 1.4, 0.6), (1.15, 1.4, 0.6), (0.9, 0.03, 0.95), (1.1, 0.03, 0.95),
                (0.85, 1.4, 0.62), (1.15, 1.4, 0.62), (0.9, 0.03, 0.97), (1.1, 0.03, 0.97)]), col(shade(a, 0.8)))


@item
def surfboard(m, P):
    a, b = P["a"], P["b"]
    from mathutils import Matrix
    outline = [(0.01, 0.0), (0.18, 0.12), (0.32, 0.5), (0.44, 1.6), (0.45, 3.0), (0.4, 4.4), (0.25, 5.4), (0.01, 5.92)]
    board = lathe(outline, (0, 0, 0), "x", 28)
    m.add(transformed(board, Matrix.Translation((0.04, 0.5, 0.5)) @ Matrix.Diagonal((1, 1, 0.17, 1))), col(a))
    stripe = lathe([(0.01, 0.0), (0.06, 0.4), (0.07, 2.8), (0.06, 5.2), (0.01, 5.6)], (0, 0, 0), "x", 12)
    m.add(transformed(stripe, Matrix.Translation((0.2, 0.5, 0.5)) @ Matrix.Diagonal((1, 1, 1.25, 1))), col(b))
    for y, h in ((0.5, 0.32), (0.3, 0.2), (0.7, 0.2)):
        m.add(hull([(0.35, y - 0.02, 0.56), (0.75, y - 0.02, 0.56), (0.42, y - 0.02, 0.56 + h),
                    (0.35, y + 0.02, 0.56), (0.75, y + 0.02, 0.56), (0.42, y + 0.02, 0.56 + h)]), col(b))
    m.add(box((0.4, 0.25, 0.42), (1.3, 0.75, 0.43), 0.03), col(DARK))
    m.add(cyl((0.25, 0.5, 0.43), 0.04, 0.02, "z"), col(DARK))


@item
def kayak(m, P):
    a, b = P["a"], P["b"]
    from mathutils import Matrix
    profile = [(0.01, 0.0), (0.2, 0.25), (0.5, 0.8), (0.74, 1.6), (0.84, 2.5), (0.74, 3.4), (0.5, 4.2), (0.2, 4.75), (0.01, 5.0)]
    hull_prim = lathe(profile, (0, 0, 0), "x", 28)
    xf = Matrix.Translation((0.0, 0.43, 1.0)) @ Matrix.Diagonal((1, 0.5, 1.05, 1))
    m.add_faces_by(transformed(hull_prim, xf), lambda c, n: col(a) if c.y > 0.43 else col(shade(a, 0.7)))
    m.add(sweep([(0.15, 0.44, 1.0), (4.85, 0.44, 1.0)], 0.012, segments=6), col(WHITE))
    m.add(sweep(rounded_rect_points((2.55, 0.83, 1.0), 0.55, 0.36, 0.3, 6), 0.045, closed=True, segments=8), col(b))
    m.add(ellipsoid((2.55, 0.8, 1.0), (0.5, 0.05, 0.32)), col("263238"))
    m.add(box((2.75, 0.8, 0.75), (2.95, 1.0, 1.25), 0.05), col(b))
    for x0, x1 in ((0.8, 1.6), (3.5, 4.3)):
        m.add(sweep([(x0, 0.82, 0.72), (x1, 0.82, 1.28)], 0.012, segments=6), col(BLACK))
        m.add(sweep([(x0, 0.82, 1.28), (x1, 0.82, 0.72)], 0.012, segments=6), col(BLACK))
    for x in (1.1, 3.95):
        m.add(cyl((x, 0.79, 1.0), 0.17, 0.04, "y", segments=18), col(b))
    for x in (0.1, 4.9):
        m.add(torus((x, 0.45, 1.0), 0.06, 0.015, "z", 10, 5), col(BLACK))


@item
def umbrella(m, P):
    a, b = P["a"], P["b"]
    m.add(rod((0.25, 0.5, 0.5), (3.92, 0.5, 0.5), 0.035), col(WHITE))
    m.add(cyl((3.94, 0.5, 0.5), 0.02, 0.06, "x"), metal(STEEL))
    m.add(sweep([(0.3, 0.5, 0.5)] + arc_points((0.18, 0.5, 0.5), 0.12, 0, -200, "xy", 10)[1:], 0.04, segments=10), col("6D4C41"))
    canopy = lathe([(0.04, 0.0), (0.2, 0.25), (0.33, 0.8), (0.36, 1.4), (0.3, 2.0), (0.18, 2.4), (0.05, 2.7)],
                   (1.15, 0.5, 0.5), "x", 24)

    def pleats(c, n):
        ang = math.atan2(c.z - 0.5, c.y - 0.5)
        return col(a) if int((ang + math.pi) / (2 * math.pi) * 8) % 2 == 0 else col(b)
    m.add_faces_by(canopy, pleats)
    m.add(torus((2.0, 0.5, 0.5), 0.355, 0.03, "x", 24, 6), col(b))
    m.add(box((1.96, 0.85, 0.47), (2.04, 0.9, 0.53)), col(WHITE))


@item
def boogie_board(m, P):
    a, b = P["a"], P["b"]
    outline = [(0.08, 0.1), (1.6, 0.06), (1.92, 0.25), (1.95, 0.5), (1.92, 0.75), (1.6, 0.94), (0.08, 0.9)]
    m.add(hull([(x, y, z) for x, y in outline for z in (0.43, 0.57)], 0.05, 3), col(a))
    m.add(hull([(x, y, z) for x, y in ((0.3, 0.38), (1.55, 0.36), (1.75, 0.5), (1.55, 0.64), (0.3, 0.62))
                for z in (0.42, 0.425)]), col(b))
    m.add(hull([(0.5, 0.2, 0.415), (1.2, 0.2, 0.415), (0.85, 0.32, 0.415), (0.5, 0.2, 0.42), (1.2, 0.2, 0.42)]), col(WHITE))
    m.add(sweep([(1.88, 0.5 + 0.06 * math.sin(t * 0.9), 0.6 + t * 0.012) for t in range(24)], 0.012, segments=5), col(DARK))
    m.add(cyl((1.86, 0.5, 0.6), 0.03, 0.04, "z"), col(DARK))


@item
def shark(m, P):
    a, b = P["a"], P["b"]
    from mathutils import Matrix
    body = lathe([(0.01, 0.0), (0.12, 0.15), (0.3, 0.7), (0.42, 1.6), (0.42, 2.6), (0.36, 3.3), (0.22, 3.75), (0.02, 3.95)],
                 (0, 0, 0), "x", 28)
    m.add_faces_by(transformed(body, Matrix.Translation((0.05, 0.46, 0.5)) @ Matrix.Diagonal((1, 1, 1, 1))),
                   lambda c, n: col(b) if c.y < 0.36 else col(a))
    m.add(hull([(1.05, 0.8, 0.46), (1.85, 0.8, 0.46), (1.05, 0.8, 0.54), (1.85, 0.8, 0.54),
                (1.22, 1.88, 0.5), (1.4, 1.88, 0.5)], 0.03), col(a))
    m.add(hull([(0.05, 0.05, 0.47), (0.05, 0.05, 0.53), (0.0, 0.95, 0.47), (0.0, 0.95, 0.53),
                (0.45, 0.46, 0.45), (0.45, 0.46, 0.55)], 0.02), col(a))
    for z in (0.08, 0.92):
        m.add(hull([(2.2, 0.32, 0.5), (2.65, 0.32, 0.5), (2.35, 0.1, z), (2.5, 0.1, z)]), col(a))
        m.add(sphere((3.35, 0.6, 0.5 + (z - 0.5) * 0.72), 0.055), col(BLACK))
        for k in range(3):
            m.add(box((2.9 + k * 0.07, 0.42, 0.5 + (z - 0.5) * 0.92 - 0.01), (2.92 + k * 0.07, 0.62, 0.5 + (z - 0.5) * 0.92 + 0.01)),
                  col(shade(a, 0.7)))
    for k in range(7):
        m.add(rod((3.5 + k * 0.06, 0.36, 0.5 - 0.18 + k * 0.06), (3.5 + k * 0.06, 0.3, 0.5 - 0.18 + k * 0.06), 0.022, radius2=0.002), col(WHITE))
    m.add(cyl((0.6, 0.86, 0.5), 0.05, 0.06, "y"), col(WHITE))


@item
def tiki_torch(m, P):
    a, b = P["a"], P["b"]
    m.add(rod((0.04, 0.5, 0.5), (2.3, 0.5, 0.5), 0.07, segments=12), col(shade(a, 1.3)))
    for x in (0.5, 1.1, 1.7):
        m.add(torus((x, 0.5, 0.5), 0.07, 0.02, "x", 12, 6), col(a))
    m.add(lathe([(0.12, 0.0), (0.2, 0.08), (0.24, 0.35), (0.22, 0.45)], (2.28, 0.5, 0.5), "x", 18), col("6D4C41"))
    for k in range(4):
        m.add(torus((2.36 + k * 0.09, 0.5, 0.5), 0.215 + k * 0.008, 0.015, "x", 18, 4), col("8D6E63"))
    m.add(cyl((2.76, 0.5, 0.5), 0.05, 0.06, "x"), col(BLACK))
    m.add(lathe([(0.15, 0.0), (0.14, 0.08), (0.08, 0.16), (0.01, 0.24)], (2.78, 0.5, 0.5), "x", 14), glow("FF6D00"))
    m.add(lathe([(0.08, 0.0), (0.07, 0.06), (0.03, 0.13), (0.01, 0.17)], (2.79, 0.5, 0.5), "x", 12), glow("FFD54F"))


@item
def kiddie_pool(m, P):
    a, b = P["a"], P["b"]
    path = rounded_rect_points((1.5, 0.43, 1.5), 1.08, 1.08, 0.62, 8)

    def stripe(c, n):
        ang = math.atan2(c.z - 1.5, c.x - 1.5)
        return col(a) if int((ang + math.pi) / (2 * math.pi) * 12) % 2 == 0 else col(b)
    m.add_faces_by(sweep(path, 0.41, closed=True, segments=18), stripe)
    m.add(sweep(rounded_rect_points((1.5, 0.84, 1.5), 1.08, 1.08, 0.62, 8), 0.02, closed=True, segments=6), col(WHITE))
    m.add(cyl((1.5, 0.43, 0.06), 0.07, 0.08, "z", segments=12), col(WHITE))
    m.add(cyl((1.5, 0.43, 0.02), 0.05, 0.04, "z", segments=12), col("90A4AE"))
    for x, cl in ((0.9, "FFEB3B"), (2.1, "FF7043")):
        m.add(ellipsoid((x, 0.62, 0.11), (0.14, 0.09, 0.02)), col(cl))
        m.add(hull([(x + 0.12, 0.62, 0.1), (x + 0.22, 0.7, 0.1), (x + 0.22, 0.54, 0.1), (x + 0.12, 0.62, 0.08)]), col(cl))


@item
def bowling_ball(m, P):
    a = P["a"]
    m.add(sphere((0.5, 0.45, 0.5), 0.43, segments=32, rings=18), glass(a))
    for x, z in ((0.43, 0.4), (0.57, 0.4), (0.5, 0.56)):
        m.add(cyl((x, 0.87, z), 0.045, 0.015, "y", segments=12), col(BLACK))
    m.add(cyl((0.5, 0.45, 0.07), 0.06, 0.005, "z", segments=12), col("90A4AE"))


@item
def bike(m, P):
    a, b = P["a"], P["b"]
    for x in (0.5, 2.5):
        m.add(torus((x, 0.47, 0.5), 0.38, 0.07, "z", 28, 8), col(a))
        m.add(torus((x, 0.47, 0.5), 0.32, 0.015, "z", 28, 6), metal(STEEL))
        m.add(cyl((x, 0.47, 0.5), 0.06, 0.18, "z"), metal(STEEL))
        for k in range(8):
            ang = k / 8 * math.pi
            m.add(rod((x - 0.3 * math.cos(ang), 0.47 - 0.3 * math.sin(ang), 0.5),
                      (x + 0.3 * math.cos(ang), 0.47 + 0.3 * math.sin(ang), 0.5), 0.006, 4), metal(STEEL))
    m.add(rod((0.55, 1.25, 0.5), (2.35, 1.3, 0.5), 0.05), col(b))
    m.add(rod((0.5, 0.47, 0.5), (0.62, 1.4, 0.5), 0.045), col(b))
    m.add(rod((2.5, 0.47, 0.5), (2.38, 1.6, 0.5), 0.045), col(b))
    m.add(rod((0.62, 1.3, 0.5), (0.65, 1.6, 0.5), 0.035), metal(STEEL))
    m.add(ellipsoid((0.66, 1.67, 0.5), (0.2, 0.07, 0.11)), col(DARK))
    m.add(rod((2.38, 1.6, 0.5), (2.32, 1.82, 0.5), 0.035), metal(STEEL))
    m.add(rod((2.32, 1.82, 0.18), (2.32, 1.82, 0.82), 0.035), metal(STEEL))
    for z in (0.15, 0.85):
        m.add(cyl((2.32, 1.82, z), 0.05, 0.14, "z"), col(BLACK))
    for k, cl in enumerate(("E91E63", "FFEB3B", "03A9F4")):
        m.add(rod((2.32, 1.8, 0.1), (2.2 + k * 0.05, 1.45, 0.06), 0.015), col(cl))
    m.add(cyl((2.4, 1.78, 0.75), 0.05, 0.04, "y", segments=12), metal(STEEL))
    m.add(box((2.45, 1.55, 0.3), (2.85, 1.85, 0.7), 0.03), col("8D6E63"))
    for z in (0.08, 0.92):
        m.add(torus((0.35, 0.16, z), 0.14, 0.03, "z", 16, 6), col(WHITE))
        m.add(rod((0.5, 0.47, 0.5), (0.35, 0.16, z), 0.015), metal(STEEL))
    m.add(hull([(0.55, 0.42, 0.42), (0.9, 0.62, 0.42), (0.55, 0.52, 0.42), (0.9, 0.72, 0.42),
                (0.55, 0.42, 0.44), (0.9, 0.62, 0.44)]), col(b))


@item
def fishing_rod(m, P):
    a, b = P["a"], P["b"]
    m.add(box((3.05, 0.02, 0.12), (3.95, 0.6, 0.88), 0.05), col(b))
    m.add(box((3.04, 0.42, 0.11), (3.96, 0.47, 0.89)), col(shade(b, 0.7)))
    m.add(sweep(arc_points((3.5, 0.61, 0.5), 0.16, 0, 180, "zy", 10), 0.025), col(DARK))
    for x in (3.25, 3.75):
        m.add(box((x - 0.05, 0.36, 0.09), (x + 0.05, 0.5, 0.12)), metal(STEEL))
    m.add(sphere((3.3, 0.66, 0.3), 0.06), col("E53935"))
    m.add(sphere((3.3, 0.72, 0.3), 0.04), col(WHITE))
    m.add(ellipsoid((3.65, 0.62, 0.7), (0.12, 0.025, 0.05)), col("FFCA28"))
    # Rod leans from the ground onto the tackle box.
    base, tip = (0.08, 0.08, 0.5), (3.15, 0.66, 0.42)
    m.add(rod(base, (0.75, 0.2, 0.49), 0.055), col("C8A26E"))
    m.add(rod((0.75, 0.2, 0.49), tip, 0.032, radius2=0.012), col(a))
    m.add(cyl((0.95, 0.12, 0.5), 0.11, 0.09, "z", segments=18), metal(STEEL))
    m.add(cyl((0.95, 0.12, 0.56), 0.04, 0.05, "z"), col(BLACK))
    for t in (0.35, 0.55, 0.75, 0.95):
        p = [base[i] + (tip[i] - base[i]) * t for i in range(3)]
        m.add(torus((p[0], p[1] + 0.03, p[2]), 0.025, 0.006, "x", 10, 4), metal(STEEL))
    m.add(sweep([(3.15, 0.66, 0.42), (3.25, 0.58, 0.36), (3.3, 0.66, 0.3)], 0.004, segments=4), col(WHITE))


@item
def mirror(m, P):
    a, b = P["a"], P["b"]
    m.add(cyl((1.0, 1.0, 0.42), 0.86, 0.12, "z", segments=40), metal(b))
    m.add(torus((1.0, 1.0, 0.35), 0.86, 0.06, "z", 40, 10), metal(b))
    m.add(cyl((1.0, 1.0, 0.34), 0.76, 0.03, "z", segments=40), glass(a))
    m.add(ellipsoid((0.75, 1.3, 0.32), (0.18, 0.06, 0.005)), glass("FFFFFF"),
          transform=rotate_about((0.75, 1.3, 0.32), "Z", 35))
    for k in range(20):
        ang = k / 20 * 2 * math.pi
        m.add(sphere((1.0 + 0.92 * math.cos(ang), 1.0 + 0.92 * math.sin(ang), 0.34), 0.035), metal(b))
    m.add(hull([(0.82, 1.82, 0.32), (1.18, 1.82, 0.32), (1.0, 1.97, 0.34), (0.82, 1.82, 0.4), (1.18, 1.82, 0.4)], 0.02), metal(b))
    for x in (0.55, 1.45):
        m.add(rod((x, 0.5, 0.5), (x + (x - 1) * 0.3, 0.03, 0.92), 0.04), metal(b))
    m.add(rod((0.6, 0.12, 0.88), (1.4, 0.12, 0.88), 0.025), metal(b))


@item
def kitchen_sink(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.05, 0.02, 0.06), (2.95, 0.78, 1.95), 0.03), col(WHITE))
    for x0, x1 in ((0.15, 1.45), (1.55, 2.85)):
        m.add(box((x0, 0.12, 0.04), (x1, 0.68, 0.07), 0.03), col("ECEFF1"))
        m.add(box(((x0 + x1) / 2 - 0.15, 0.55, 0.02), ((x0 + x1) / 2 + 0.15, 0.6, 0.04), 0.01), metal(STEEL))
    m.add(box((0.03, 0.78, 0.03), (2.97, 0.98, 1.97), 0.03), metal(a))
    for x0, x1 in ((0.25, 1.4), (1.6, 2.75)):
        m.add(box((x0, 0.93, 0.25), (x1, 0.99, 1.0), 0.04), col("4A5157"))
        m.add(cyl(((x0 + x1) / 2, 0.995, 0.62), 0.06, 0.01, "y"), col(BLACK))
    m.add(box((0.03, 0.98, 1.72), (2.97, 1.88, 1.96), 0.03), col(WHITE))
    for i in range(6):
        for j in range(2):
            m.add(box((0.08 + i * 0.48, 1.02 + j * 0.42, 1.7), (0.5 + i * 0.48, 1.4 + j * 0.42, 1.72)), col("B3E5FC"))
    m.add(rod((1.5, 0.98, 1.55), (1.5, 1.72, 1.55), 0.05), metal(b))
    m.add(rod((1.5, 1.72, 1.55), (1.5, 1.72, 1.12), 0.045), metal(b))
    m.add(rod((1.5, 1.72, 1.12), (1.5, 1.55, 1.08), 0.04), metal(b))
    for x in (1.2, 1.8):
        m.add(cyl((x, 1.05, 1.55), 0.06, 0.12, "y"), metal(b))


@item
def mattress(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.05, 0.02, 0.05), (2.95, 0.58, 1.95), 0.22, 4), col(a))
    for k in range(6):
        m.add(ellipsoid((0.4 + k * 0.44, 0.55, 1.0), (0.2, 0.16, 0.86), 18, 10), col(shade(b, 1.15)))
    m.add(cyl((2.75, 0.4, 0.04), 0.08, 0.06, "z", segments=14), col(WHITE))
    m.add(cyl((2.75, 0.4, 0.0), 0.05, 0.05, "z", segments=12), col("90A4AE"))
    m.add(box((0.25, 0.2, 0.03), (0.75, 0.38, 0.05), 0.02), col(WHITE))


@item
def guitar(m, P):
    a, b = P["a"], P["b"]
    for x, r in ((0.52, 0.44), (1.22, 0.34)):
        m.add(cyl((x, 0.2, 0.5), r, 0.34, "y", segments=36, bevel=0.03), col(a))
        m.add(torus((x, 0.37, 0.5), r, 0.012, "y", 36, 4), col(WHITE))
    m.add(cyl((0.88, 0.2, 0.5), 0.3, 0.34, "y", segments=28), col(a))
    m.add(cyl((1.05, 0.372, 0.5), 0.13, 0.01, "y", segments=24), col(BLACK))
    m.add(torus((1.05, 0.375, 0.5), 0.15, 0.012, "y", 24, 4), col(WHITE))
    m.add(hull([(1.08, 0.373, 0.3), (1.3, 0.373, 0.3), (1.3, 0.373, 0.45), (1.08, 0.376, 0.3), (1.3, 0.376, 0.45)]), col("3E2723"))
    m.add(box((0.33, 0.37, 0.36), (0.43, 0.4, 0.64), 0.01), col(b))
    m.add(box((1.5, 0.27, 0.44), (2.62, 0.35, 0.56), 0.02), col(b))
    for k in range(9):
        x = 1.6 + k * 0.11
        m.add(box((x, 0.35, 0.44), (x + 0.008, 0.355, 0.56)), metal(STEEL))
    m.add(hull([(2.6, 0.25, 0.39), (2.97, 0.27, 0.36), (2.6, 0.25, 0.61), (2.97, 0.27, 0.64),
                (2.6, 0.34, 0.39), (2.97, 0.34, 0.36), (2.6, 0.34, 0.61), (2.97, 0.34, 0.64)], 0.015), col(b))
    for k in range(3):
        for z in (0.33, 0.67):
            m.add(cyl((2.7 + k * 0.1, 0.3, z), 0.025, 0.08, "z", segments=8), metal(STEEL))
    for k in range(6):
        z = 0.455 + k * 0.018
        m.add(rod((0.38, 0.4, z), (2.62, 0.36, z), 0.003, 4), metal(STEEL))


@item
def plant_stand(m, P):
    a = P["a"]
    for x in (0.5, 1.5, 2.5):
        m.add(ellipsoid((x, 1.32, 0.55), (0.24, 0.2, 0.36)), col(a))
        m.add(rod((x, 1.4, 0.3), (x, 1.75, 0.22), 0.06), col(a), smooth=True)
        m.add(sphere((x, 1.8, 0.25), 0.1), col(a))
        m.add(rod((x, 1.78, 0.17), (x, 1.68, 0.04), 0.04, radius2=0.015), col(BLACK))
        m.add(ellipsoid((x, 1.4, 0.85), (0.1, 0.06, 0.12)), col(shade(a, 0.85)))
    for x0, x1 in ((0.45, 0.4), (0.55, 0.6), (2.45, 2.4), (2.55, 2.6)):
        m.add(rod((x0, 1.15, 0.55), (x1, 0.02, 0.55), 0.015), metal(STEEL))
    for x1 in (0.9, 2.1):
        m.add(rod((1.5, 1.15, 0.55), (x1, 0.02, 0.55), 0.015), metal(STEEL))


@item
def picnic_basket(m, P):
    a, b = P["a"], P["b"]
    wicker = col(a)
    m.add(box((0.1, 0.02, 0.14), (1.9, 0.56, 0.86), 0.06, 3), wicker)
    for k in range(7):
        y = 0.08 + k * 0.07
        m.add(sweep(rounded_rect_points((1.0, y, 0.5), 0.915, 0.375, 0.07, 3), 0.014, closed=True, segments=5),
              col(shade(a, 0.85 if k % 2 else 1.1)))
    for k in range(13):
        x = 0.16 + k * 0.14
        m.add(box((x - 0.012, 0.04, 0.125), (x + 0.012, 0.54, 0.14)), col(shade(a, 0.7)))
    lid = box((0.08, 0.54, 0.12), (1.92, 0.62, 0.88), 0.04, 2)
    m.add(lid, col(shade(a, 0.95)))

    for i in range(8):
        for j in range(5):
            x0, z0 = 0.28 + i * 0.1, 0.18 + j * 0.09
            y0 = 0.62 + 0.012 * math.sin(i * 1.3 + j)
            m.add(box((x0, y0, z0), (x0 + 0.1, y0 + 0.03, z0 + 0.09)), col(b) if (i + j) % 2 == 0 else col("FFFFFF"))
    m.add(hull([(0.28, 0.6, 0.62), (1.08, 0.6, 0.62), (0.3, 0.42, 0.88), (1.06, 0.42, 0.88),
                (0.28, 0.63, 0.63), (1.08, 0.63, 0.63)]), col(b))
    m.add(sweep(arc_points((1.0, 0.62, 0.5), 0.34, 0, 180, "xy", 14), 0.035, segments=8), col(shade(a, 0.75)))
    m.add(lathe([(0.01, 0), (0.05, 0.08), (0.06, 0.35), (0.04, 0.42), (0.01, 0.45)], (1.45, 0.62, 0.45), "x", 12),
          col("D4A055"), transform=rotate_about((1.45, 0.62, 0.45), "Z", 25))
    m.add(lathe([(0.05, 0.0), (0.05, 0.18), (0.02, 0.24), (0.02, 0.32)], (1.55, 0.55, 0.7), "y", 12), glass("2E7D32"))
    m.add(box((0.95, 0.4, 0.1), (1.05, 0.5, 0.14), 0.01), metal("B8BCC2"))


@item
def flowers(m, P):
    a, b = P["a"], P["b"]
    m.add(lathe([(0.2, 0.02), (0.24, 0.3), (0.2, 0.62), (0.14, 0.72), (0.17, 0.78)], (0.5, 0, 0.5), "y", 24), glass("B3E5FC"))
    m.add(cyl((0.5, 0.35, 0.5), 0.2, 0.5, "y", segments=20), glass("81D4FA"))
    import random
    rng = random.Random(9)
    heads = []
    for k in range(9):
        ang = k / 9 * 2 * math.pi + rng.uniform(-0.2, 0.2)
        r = 0.08 if k < 3 else 0.22
        top = (0.5 + math.cos(ang) * r * 1.4, 1.45 + rng.uniform(-0.12, 0.25), 0.5 + math.sin(ang) * r * 1.4)
        m.add(sweep([(0.5 + math.cos(ang) * 0.04, 0.4, 0.5 + math.sin(ang) * 0.04), (0.5 + math.cos(ang) * r * 0.6, 1.0, 0.5 + math.sin(ang) * r * 0.6), top],
                    0.018, segments=5), col(a))
        heads.append(top)
    palette = [b, "FFFFFF", "FFCA28", b, "E57373", b, "FFFFFF", "BA68C8", b]
    for (x, y, z), c in zip(heads, palette):
        for p in range(6):
            pa = p / 6 * 2 * math.pi
            m.add(ellipsoid((x + math.cos(pa) * 0.07, y, z + math.sin(pa) * 0.07), (0.07, 0.035, 0.07), 10, 6), col(c))
        m.add(sphere((x, y + 0.02, z), 0.04), col("FFB300"))
    for k in range(6):
        ang = k / 6 * 2 * math.pi
        leaf = ellipsoid((0.5 + math.cos(ang) * 0.18, 1.0, 0.5 + math.sin(ang) * 0.18), (0.13, 0.025, 0.05), 10, 6)
        m.add(transformed(leaf, rotate_about((0.5 + math.cos(ang) * 0.18, 1.0, 0.5 + math.sin(ang) * 0.18), "Y", -math.degrees(ang))), col(shade(a, 0.85)))
    m.add(torus((0.5, 0.75, 0.5), 0.16, 0.025, "y", 20, 6), col(b))
    m.add(hull([(0.48, 0.75, 0.32), (0.52, 0.75, 0.32), (0.42, 0.55, 0.3), (0.58, 0.55, 0.3), (0.5, 0.7, 0.3)]), col(b))


@item
def trophy(m, P):
    a, b = P["a"], P["b"]
    gold = metal(b)
    m.add(box((0.14, 0.02, 0.14), (0.86, 0.22, 0.86), 0.03), col(a))
    m.add(box((0.2, 0.22, 0.2), (0.8, 0.4, 0.8), 0.03), col(shade(a, 1.2)))
    m.add(box((0.3, 0.25, 0.185), (0.7, 0.36, 0.2), 0.01), gold)
    m.add(lathe([(0.12, 0.4), (0.09, 0.48), (0.05, 0.6), (0.05, 0.8), (0.1, 0.88)], (0.5, 0, 0.5), "y", 20), gold)
    m.add(shell([(0.1, 0.88), (0.22, 1.05), (0.3, 1.3), (0.32, 1.45)], (0.5, 0, 0.5), "y", 0.02, 28), gold)
    for side in (-1, 1):
        m.add(sweep(arc_points((0.5 + side * 0.33, 1.22, 0.5), 0.12, -90 if side > 0 else 90, 90 if side > 0 else 270, "xy", 12), 0.025, segments=8), gold)
    m.add(ellipsoid((0.5, 1.58, 0.5), (0.13, 0.11, 0.11)), col("FFEB3B"))
    m.add(sphere((0.5, 1.75, 0.46), 0.08), col("FFEB3B"))
    m.add(ellipsoid((0.5, 1.73, 0.37), (0.04, 0.025, 0.05)), col("FF8F00"))
    for x in (0.47, 0.53):
        m.add(sphere((x, 1.78, 0.39), 0.012), col("1E1E22"))
    m.add(sphere((0.5, 1.47, 0.5), 0.3, (1, 0.12, 1)), metal(shade(b, 1.2)))


@item
def teddy(m, P):
    a, b = P["a"], P["b"]
    fur, dark = col(a), col(b)
    m.add(ellipsoid((0.5, 0.3, 0.52), (0.27, 0.27, 0.24)), fur)
    m.add(ellipsoid((0.5, 0.28, 0.4), (0.15, 0.17, 0.12)), col(shade(a, 1.3)))
    for x in (0.27, 0.73):
        m.add(ellipsoid((x, 0.14, 0.32), (0.11, 0.1, 0.16)), fur)
        m.add(ellipsoid((x, 0.13, 0.17), (0.08, 0.08, 0.03)), col(shade(a, 1.3)))
        m.add(ellipsoid((x - (0.06 if x < 0.5 else -0.06), 0.42, 0.5), (0.09, 0.15, 0.09)), fur,
              transform=rotate_about((x, 0.42, 0.5), "Z", 25 if x < 0.5 else -25))
    m.add(sphere((0.5, 0.68, 0.5), 0.22), fur)
    for x in (0.33, 0.67):
        m.add(sphere((x, 0.86, 0.52), 0.08), fur)
        m.add(sphere((x, 0.86, 0.47), 0.045), col(shade(a, 1.3)))
    m.add(ellipsoid((0.5, 0.63, 0.31), (0.09, 0.07, 0.06)), col(shade(a, 1.3)))
    m.add(sphere((0.5, 0.66, 0.26), 0.03), dark)
    m.add(sphere((0.42, 0.73, 0.31), 0.025), dark)
    m.add(cyl((0.58, 0.73, 0.3), 0.035, 0.015, "z", segments=10), col("FFFFFF"))
    m.add(cyl((0.58, 0.73, 0.295), 0.022, 0.01, "z", segments=10), dark)
    m.add(torus((0.5, 0.52, 0.5), 0.2, 0.025, "y", 18, 6), col("C62828"))
    m.add(hull([(0.5, 0.5, 0.28), (0.4, 0.56, 0.27), (0.4, 0.45, 0.27), (0.5, 0.5, 0.3)]), col("C62828"))
    m.add(hull([(0.5, 0.5, 0.28), (0.6, 0.56, 0.27), (0.6, 0.45, 0.27), (0.5, 0.5, 0.3)]), col("C62828"))


@item
def toy_blocks(m, P):
    a, b = P["a"], P["b"]
    for (x, y, z, c, rot) in ((0.3, 0.15, 0.32, a, 6), (0.68, 0.15, 0.35, b, -10), (0.48, 0.15, 0.7, "43A047", 15),
                              (0.48, 0.45, 0.45, "1E88E5", -4)):
        m.add(box((x - 0.15, y - 0.15, z - 0.15), (x + 0.15, y + 0.15, z + 0.15), 0.025), col(c),
              transform=rotate_about((x, y, z), "Y", rot))
        m.add(box((x - 0.07, y - 0.07, z - 0.155), (x + 0.07, y + 0.07, z - 0.15)), col("FFFFFF"),
              transform=rotate_about((x, y, z), "Y", rot))


@item
def lemonade(m, P):
    a, b = P["a"], P["b"]
    m.add(lathe([(0.24, 0.02), (0.27, 0.1), (0.27, 0.58), (0.2, 0.68), (0.12, 0.72)], (0.5, 0, 0.5), "y", 28), glass(a))
    m.add(cyl((0.5, 0.76, 0.5), 0.12, 0.08, "y", segments=18), col(b))
    m.add(sweep(arc_points((0.8, 0.42, 0.5), 0.16, -80, 80, "xy", 10), 0.03, segments=8), glass(a))
    for k in range(3):
        m.add(cyl((0.38 + k * 0.12, 0.3 + (k % 2) * 0.1, 0.5), 0.07, 0.015, "z", segments=12), col("FFEE58"))
    m.add(cyl((0.5, 0.45, 0.24), 0.1, 0.01, "z", segments=16), col("FFFFFF"))


@item
def toy_truck(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.12, 0.12, 0.25), (0.88, 0.22, 0.75), 0.02), col(b))
    m.add(box((0.6, 0.2, 0.28), (0.88, 0.5, 0.72), 0.05), col(a))
    m.add(box((0.7, 0.36, 0.27), (0.86, 0.46, 0.73)), glass("90CAF9"))
    m.add(hull([(0.12, 0.22, 0.28), (0.58, 0.22, 0.28), (0.12, 0.22, 0.72), (0.58, 0.22, 0.72),
                (0.08, 0.48, 0.26), (0.58, 0.44, 0.26), (0.08, 0.48, 0.74), (0.58, 0.44, 0.74)], 0.02), col(a))
    for x in (0.25, 0.72):
        for z in (0.22, 0.78):
            m.add(cyl((x, 0.12, z), 0.1, 0.06, "z", segments=16), col(b))
            m.add(cyl((x, 0.12, z + (0.03 if z > 0.5 else -0.03)), 0.04, 0.02, "z", segments=10), col("ECEFF1"))


@item
def grandpa_note(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.2, 0.02, 0.25), (0.8, 0.05, 0.75), 0.01), col(a), transform=rotate_about((0.5, 0.03, 0.5), "Y", 12))
    m.add(box((0.2, 0.05, 0.25), (0.8, 0.08, 0.5), 0.01), col(shade(a, 0.95)),
          transform=rotate_about((0.5, 0.05, 0.5), "Y", 12) @ rotate_about((0.5, 0.06, 0.5), "X", -14))
    for k in range(4):
        m.add(box((0.28, 0.055, 0.56 + k * 0.04), (0.28 + 0.42 - k * 0.06, 0.058, 0.575 + k * 0.04)), col(b),
              transform=rotate_about((0.5, 0.03, 0.5), "Y", 12))
    m.add(torus((0.5, 0.06, 0.5), 0.18, 0.01, "x", 16, 4, arc=0.5), col("C62828"),
          transform=rotate_about((0.5, 0.03, 0.5), "Y", 12))


@item
def mini_fridge(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.08, 0.04, 0.12), (1.92, 1.88, 0.92), 0.08, 3), col(a))
    m.add(box((0.12, 0.08, 0.08), (1.88, 1.84, 0.12), 0.04), col(shade(a, 0.96)))
    m.add(box((0.12, 1.3, 0.06), (1.88, 1.34, 0.1)), col(b))
    m.add(box((1.66, 0.4, 0.02), (1.74, 1.2, 0.08), 0.02), metal("B0BEC5"))
    m.add(box((1.66, 1.42, 0.02), (1.74, 1.72, 0.08), 0.02), metal("B0BEC5"))
    for (x, y, c) in ((0.5, 1.0, "FF7043"), (0.8, 0.75, "FFEB3B"), (1.1, 1.05, "42A5F5"), (0.45, 0.55, "66BB6A")):
        m.add(box((x - 0.12, y - 0.1, 0.05), (x + 0.12, y + 0.1, 0.07)), col(c))
    m.add(box((0.3, 1.5, 0.05), (0.75, 1.65, 0.07)), col("FFFFFF"))
    for x in (0.2, 1.8):
        m.add(cyl((x, 0.03, 0.5), 0.05, 0.06, "y", segments=10), col("263238"))


@item
def desk_lamp(m, P):
    a, b = P["a"], P["b"]
    m.add(cyl((0.5, 0.04, 0.55), 0.26, 0.08, "y", segments=24, bevel=0.02), col(a))
    m.add(sweep([(0.5, 0.08, 0.55), (0.42, 0.75, 0.6), (0.52, 1.35, 0.42)], 0.03, segments=8), metal("B0BEC5"))
    m.add(sphere((0.42, 0.75, 0.6), 0.05), col(a))
    m.add(shell([(0.06, 0.0), (0.18, 0.22), (0.24, 0.32)], (0.55, 1.42, 0.35), "y", 0.015, 20), col(a),
          transform=rotate_about((0.55, 1.5, 0.35), "X", 140))
    m.add(sphere((0.56, 1.35, 0.3), 0.08), glow(b))


@item
def hamper(m, P):
    a = P["a"]
    m.add(lathe([(0.32, 0.02), (0.35, 0.3), (0.38, 1.3), (0.39, 1.4)], (0.5, 0, 0.5), "y", 24), col(a))
    for k in range(9):
        m.add(torus((0.5, 0.15 + k * 0.14, 0.5), 0.34 + k * 0.004, 0.012, "y", 24, 4), col(shade(a, 0.8)))
    m.add(torus((0.5, 1.4, 0.5), 0.39, 0.035, "y", 24, 6), col(shade(a, 0.7)))
    m.add(ellipsoid((0.45, 1.45, 0.55), (0.3, 0.12, 0.28)), col("FFFFFF"))
    m.add(ellipsoid((0.6, 1.5, 0.45), (0.18, 0.08, 0.15)), col("EF5350"))
    m.add(sweep([(0.3, 1.5, 0.35), (0.18, 1.62, 0.25), (0.08, 1.55, 0.2)], 0.04, segments=6), col("42A5F5"))


@item
def comforter(m, P):
    a, b = P["a"], P["b"]
    m.add(cyl((1.5, 0.42, 0.5), 0.4, 2.8, "x", segments=28, bevel=0.15), col(a))
    for x in (0.4, 2.6):
        m.add(torus((x, 0.42, 0.5), 0.4, 0.03, "x", 28, 6), col("FFFFFF"))
    for k in range(8):
        a_ = k * 0.8
        x = 0.5 + k * 0.28
        y, z = 0.42 + 0.41 * math.sin(a_ + 1.2), 0.5 + 0.41 * math.cos(a_ + 1.2)
        m.add(ellipsoid((x, y, z), (0.08, 0.06, 0.06)), col(b))
        m.add(ellipsoid((x + 0.07, y + 0.02, z), (0.04, 0.02, 0.03)), col("FF9800"))
    m.add(sweep(arc_points((1.5, 0.42, 0.5), 0.41, 0, 360, "zy", 24), 0.015, segments=5), col("FFFFFF"))


@item
def poster_tube(m, P):
    a, b = P["a"], P["b"]
    m.add(cyl((2.0, 0.45, 0.5), 0.3, 3.7, "x", segments=24), col(a))
    for x in (0.12, 3.88):
        m.add(cyl((x, 0.45, 0.5), 0.33, 0.12, "x", segments=24), col(b))
    m.add(sweep([(0.6, 0.78, 0.5), (2.0, 0.95, 0.5), (3.4, 0.78, 0.5)], 0.03, segments=6), col(b))
    for x0 in (1.2, 2.2):
        m.add(box((x0, 0.3, 0.17), (x0 + 0.6, 0.6, 0.19)), col("FFFFFF"))


@item
def shower_caddy(m, P):
    a = P["a"]
    m.add(box((0.15, 0.04, 0.25), (0.85, 0.4, 0.75), 0.04), col(a))
    m.add(sweep([(0.2, 0.38, 0.5), (0.25, 0.75, 0.5), (0.75, 0.75, 0.5), (0.8, 0.38, 0.5)], 0.03, segments=6), col(shade(a, 0.8)))
    for k, (x, c) in enumerate(((0.27, "EC407A"), (0.42, "FFCA28"), (0.58, "7E57C2"), (0.73, "26C6DA"))):
        h = 0.55 + (k % 2) * 0.12
        m.add(cyl((x, 0.38 + (h - 0.38) / 2, 0.4), 0.065, h - 0.38, "y", segments=12), col(c))
        m.add(cyl((x, h + 0.02, 0.4), 0.04, 0.04, "y", segments=10), col("FFFFFF"))
    m.add(ellipsoid((0.5, 0.45, 0.62), (0.15, 0.06, 0.08)), col("FFF59D"))


@item
def speaker(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.12, 0.04, 0.18), (0.88, 1.84, 0.82), 0.08, 3), col(a))
    for y, r in ((0.55, 0.26), (1.3, 0.18)):
        m.add(cyl((0.5, y, 0.16), r, 0.04, "z", segments=28), col("111111"))
        m.add(torus((0.5, y, 0.15), r, 0.025, "z", 28, 6), glow(b))
        m.add(cyl((0.5, y, 0.13), r * 0.3, 0.03, "z", segments=16), metal("90A4AE"))
    m.add(sweep(arc_points((0.5, 1.84, 0.5), 0.2, 0, 180, "xy", 10), 0.035, segments=8), col("111111"))
    m.add(box((0.3, 1.62, 0.15), (0.7, 1.7, 0.18)), glow(b))


@item
def glow_sticks(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.15, 0.02, 0.2), (0.85, 0.32, 0.8), 0.03), col("263238"))
    colors = [a, b, "FF9100", "00E5FF", "FFEA00"]
    for k in range(9):
        x = 0.24 + (k % 5) * 0.13
        z = 0.35 + (k // 5) * 0.25
        m.add(rod((x, 0.25, z), (x + 0.02 * (k % 3 - 1), 0.72, z + 0.03), 0.035), glow(colors[k % len(colors)]))
    m.add(torus((0.5, 0.42, 0.5), 0.25, 0.03, "y", 20, 6), glow(a))


@item
def futon(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.05, 0.02, 0.05), (2.95, 0.22, 1.95), 0.04), col("8D6E63"))
    m.add(box((0.08, 0.22, 0.08), (2.92, 0.72, 1.92), 0.2, 4), col(a))
    for x in (1.0, 2.0):
        m.add(box((x - 0.02, 0.6, 0.1), (x + 0.02, 0.73, 1.9)), col(shade(a, 0.8)))
    m.add(box((0.08, 0.72, 1.15), (2.92, 1.88, 1.92), 0.22, 4), col(b), transform=rotate_about((1.5, 0.72, 1.92), "X", -8))
    for x in (0.15, 2.85):
        m.add(box((x - 0.06, 0.02, 0.1), (x + 0.06, 1.2, 0.2), 0.03), col("8D6E63"))
    m.add(box((0.5, 0.74, 0.5), (1.1, 1.05, 1.0), 0.12, 3), col("FFB74D"), transform=rotate_about((0.8, 0.9, 0.75), "Y", 20))


@item
def beanbag(m, P):
    a = P["a"]
    m.add(ellipsoid((1.0, 0.42, 1.0), (0.95, 0.42, 0.92), 28, 14), col(a))
    m.add(ellipsoid((1.1, 0.72, 1.25), (0.6, 0.2, 0.5)), col(shade(a, 0.9)))
    m.add(ellipsoid((0.95, 0.66, 0.75), (0.45, 0.14, 0.32)), col(shade(a, 1.1)))
    m.add(sweep([(0.2, 0.55, 0.35), (0.5, 0.75, 0.25), (0.9, 0.82, 0.3)], 0.015, segments=5), col(shade(a, 0.75)))


@item
def record_crate(m, P):
    a, b = P["a"], P["b"]
    wood = col(a)
    for z0, z1 in ((0.1, 0.16), (0.84, 0.9)):
        m.add(box((0.08, 0.02, z0), (1.92, 0.72, z1), 0.02), wood)
    for x0, x1 in ((0.08, 0.14), (1.86, 1.92)):
        m.add(box((x0, 0.02, 0.1), (x1, 0.72, 0.9), 0.02), wood)
        m.add(box((x0 - 0.01, 0.48, 0.38), (x1 + 0.01, 0.58, 0.62), 0.02), col("4E342E"))
    m.add(box((0.08, 0.02, 0.1), (1.92, 0.08, 0.9)), wood)
    sleeves = ["E53935", "FDD835", "1E88E5", "43A047", "8E24AA", "FB8C00", "00ACC1", "F06292", "6D4C41", "FFFFFF"]
    for k in range(14):
        x = 0.2 + k * 0.118
        tilt = (k % 3 - 1) * 5
        m.add(box((x - 0.045, 0.08, 0.18), (x + 0.045, 0.84, 0.82), 0.008), col(sleeves[k % len(sleeves)]),
              transform=rotate_about((x, 0.08, 0.5), "Z", tilt))
        m.add(cyl((x, 0.62, 0.5), 0.15, 0.092, "x", segments=14), col(b), transform=rotate_about((x, 0.08, 0.5), "Z", tilt))


@item
def turntable(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.06, 0.04, 0.12), (1.94, 0.36, 1.88), 0.06, 3), col(a))
    for x, z in ((0.2, 0.25), (1.8, 0.25), (0.2, 1.75), (1.8, 1.75)):
        m.add(cyl((x, 0.03, z), 0.07, 0.06, "y", segments=12), col("212121"))
    m.add(cyl((0.85, 0.4, 1.0), 0.7, 0.06, "y", segments=40), metal("B0BEC5"))
    m.add(cyl((0.85, 0.44, 1.0), 0.66, 0.03, "y", segments=40), col(b))
    for r in (0.3, 0.45, 0.6):
        m.add(torus((0.85, 0.455, 1.0), r, 0.006, "y", 40, 4), col("424242"))
    m.add(cyl((0.85, 0.46, 1.0), 0.22, 0.01, "y", segments=24), col("E53935"))
    m.add(cyl((0.85, 0.48, 1.0), 0.03, 0.06, "y", segments=10), metal("ECEFF1"))
    m.add(cyl((1.68, 0.48, 1.55), 0.1, 0.18, "y", segments=16), metal("B0BEC5"))
    m.add(sweep([(1.68, 0.56, 1.55), (1.6, 0.56, 0.9), (1.35, 0.52, 0.7)], 0.02, segments=6), metal("ECEFF1"))
    m.add(box((1.28, 0.48, 0.62), (1.42, 0.54, 0.75), 0.01), col("212121"))
    for k in range(2):
        m.add(cyl((1.55 + k * 0.18, 0.37, 0.3), 0.05, 0.04, "y", segments=12), metal("ECEFF1"))


@item
def monstera(m, P):
    a, b = P["a"], P["b"]
    import random
    rng = random.Random(4)
    m.add(lathe([(0.26, 0.02), (0.36, 0.7), (0.4, 0.74), (0.4, 0.84), (0.37, 0.84)], (0.5, 0, 1.5), "y", 28), col(a))
    m.add(cyl((0.5, 0.8, 1.5), 0.35, 0.04, "y", segments=20), col("4E342E"))
    for k in range(8):
        ang = k / 8 * 2 * math.pi + rng.uniform(-0.2, 0.2)
        reach = rng.uniform(0.5, 0.75)
        tip = (min(1.65, max(0.35, 1.0 + math.cos(ang) * reach)), 1.45 + rng.uniform(0.0, 0.35),
               min(1.65, max(0.35, 1.0 + math.sin(ang) * reach)))
        m.add(sweep([(0.5, 0.82, 1.5), (0.5 + (tip[0] - 0.5) * 0.4, 1.3, 1.5 + (tip[2] - 1.5) * 0.4), tip], 0.02, segments=5), col(shade(b, 0.8)))
        leaf = ellipsoid(tip, (0.36, 0.03, 0.28), 16, 6)
        m.add(transformed(leaf, rotate_about(tip, "Y", -math.degrees(ang)) @ rotate_about(tip, "Z", rng.uniform(-20, 5))),
              col(shade(b, rng.uniform(0.85, 1.15))))
        for s in (-1, 1):
            for j in (0.14, 0.24):
                notch = (tip[0] + math.cos(ang + s * 1.4) * j, tip[1] + 0.025, tip[2] + math.sin(ang + s * 1.4) * j)
                m.add(sphere(notch, 0.04, (1, 0.4, 1)), col(shade(b, 0.55)))


@item
def cat_carrier(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.06, 0.04, 0.08), (1.94, 1.72, 0.92), 0.2, 4), col(a))
    m.add(box((0.04, 0.82, 0.06), (1.96, 0.9, 0.94), 0.02), col(shade(a, 0.8)))
    m.add(box((0.4, 0.3, 0.06), (1.6, 1.45, 0.09), 0.05), col("263238"))
    # Biscuit peers out from behind the bars.
    m.add(ellipsoid((1.0, 0.78, 0.07), (0.38, 0.3, 0.03)), col(b))
    for x in (0.74, 1.26):
        m.add(hull([(x - 0.11, 0.98, 0.06), (x + 0.11, 0.98, 0.06), (x + (0.04 if x > 1 else -0.04), 1.2, 0.06),
                    (x, 0.98, 0.08)]), col(b))
        m.add(sphere((0.86 if x < 1 else 1.14, 0.84, 0.035), 0.045, (1, 1.2, 0.4)), col("2E7D32"))
    m.add(sphere((1.0, 0.72, 0.03), 0.03), col("F48FB1"))
    for side in (-1, 1):
        for k in range(2):
            m.add(rod((1.0 + side * 0.1, 0.68 - k * 0.03, 0.03), (1.0 + side * 0.32, 0.7 - k * 0.07, 0.03), 0.004, 4), col("FFFFFF"))
    for k in range(7):
        x = 0.5 + k * 0.167
        m.add(rod((x, 0.33, 0.03), (x, 1.42, 0.03), 0.014), metal("CFD8DC"))
    m.add(sweep(arc_points((1.0, 1.72, 0.5), 0.25, 0, 180, "xy", 10), 0.05, segments=8), col(shade(a, 0.7)))
    for k in range(3):
        m.add(box((1.9, 1.1 + k * 0.12, 0.3), (1.95, 1.15 + k * 0.12, 0.7)), col(shade(a, 0.75)))


@item
def toaster(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.15, 0.05, 0.22), (0.85, 0.62, 0.78), 0.12, 4), metal(a))
    for z in (0.38, 0.62):
        m.add(box((0.25, 0.6, z - 0.04), (0.75, 0.64, z + 0.04)), col(b))
        m.add(box((0.3, 0.62, z - 0.03), (0.7, 0.72, z + 0.03), 0.02), col("D7A86E"))
    m.add(box((0.84, 0.35, 0.45), (0.9, 0.5, 0.55), 0.01), col(b))
    for x in (0.25, 0.75):
        m.add(box((x - 0.06, 0.02, 0.28), (x + 0.06, 0.06, 0.72)), col(b))


@item
def ring_box(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.3, 0.02, 0.32), (0.7, 0.24, 0.68), 0.06, 3), col(a))
    m.add(box((0.3, 0.24, 0.6), (0.7, 0.56, 0.7), 0.06, 3), col(a), transform=rotate_about((0.5, 0.24, 0.68), "X", -15))
    m.add(box((0.33, 0.2, 0.35), (0.67, 0.26, 0.62), 0.03), col("FFF8E1"))
    m.add(torus((0.5, 0.33, 0.48), 0.07, 0.016, "x", 20, 6), metal(b))
    m.add(ico((0.5, 0.42, 0.48), 0.04, 1), glass("E3F2FD"))


@item
def sams_armchair(m, P):
    ITEMS["armchair"](m, P)


@item
def tiered_cake(m, P):
    a, b = P["a"], P["b"]
    m.add(cyl((1.0, 0.04, 1.0), 0.96, 0.08, "y", segments=40), metal("ECEFF1"))
    for y, r, h in ((0.45, 0.88, 0.8), (1.25, 0.66, 0.75)):
        m.add(cyl((1.0, y, 1.0), r, h, "y", segments=40, bevel=0.04), col(a))
        m.add(torus((1.0, y - h / 2 + 0.04, 1.0), r, 0.05, "y", 40, 8), col(b))
        for k in range(14):
            ang = k / 14 * 2 * math.pi
            m.add(sphere((1.0 + r * math.cos(ang), y + h / 2 - 0.02, 1.0 + r * math.sin(ang)), 0.055), col("FFFFFF"))
        for k in range(7):
            ang = k / 7 * 2 * math.pi + 0.3
            m.add(sphere((1.0 + (r + 0.01) * math.cos(ang), y, 1.0 + (r + 0.01) * math.sin(ang)), 0.07, (1, 1, 1)), col(b))
    # Top tier sits in the far-left cell, a little proud of the centre.
    m.add(cyl((0.65, 1.95, 1.35), 0.32, 0.55, "y", segments=32, bevel=0.03), col(a))
    m.add(torus((0.65, 1.7, 1.35), 0.32, 0.04, "y", 32, 8), col(b))
    for x, c in ((0.57, "263238"), (0.73, "FFFFFF")):
        m.add(cyl((x, 2.3, 1.35), 0.05, 0.16, "y", segments=10), col(c))
        m.add(sphere((x, 2.45, 1.35), 0.05), col("F1C27D"))
    m.add(sweep(arc_points((0.65, 2.32, 1.35), 0.14, 20, 160, "xy", 8), 0.012, segments=4), col("FFD700"))


@item
def flower_arch(m, P):
    a, b = P["a"], P["b"]
    import random
    rng = random.Random(7)
    for x in (0.5, 3.5):
        m.add(box((x - 0.3, 0.0, 0.25), (x + 0.3, 0.12, 0.75), 0.04), col(shade(a, 0.8)))
        m.add(rod((x, 0.1, 0.5), (x, 2.2, 0.5), 0.06), col(a))
    path = arc_points((2.0, 2.2, 0.5), 1.5, 180, 0, "xy", 18)
    m.add(sweep(path, 0.06, segments=10), col(a))
    flowers = ["F48FB1", "FFFFFF", "F8BBD0", "FFF59D", "EC407A"]
    pts = [(0.5, 0.5 + i * 0.22, 0.5) for i in range(8)] + [(3.5, 0.5 + i * 0.22, 0.5) for i in range(8)] + path
    for p in pts:
        x, y, z = p
        y = min(y, 2.8)
        for _ in range(2):
            o = (x + rng.uniform(-0.15, 0.15), y + rng.uniform(-0.1, 0.1), z + rng.uniform(-0.25, 0.25))
            m.add(ico(o, rng.uniform(0.09, 0.15), 1), col(rng.choice(flowers)))
        m.add(ellipsoid((x + rng.uniform(-0.2, 0.2), y, z + rng.uniform(-0.3, 0.3)), (0.12, 0.03, 0.07)), col("43A047"))


@item
def veil_box(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.1, 0.02, 0.15), (1.9, 0.42, 0.85), 0.05), col(a))
    m.add(box((0.08, 0.38, 0.13), (1.92, 0.5, 0.87), 0.05), col(a))
    m.add(box((0.95, 0.02, 0.12), (1.05, 0.52, 0.88)), col(b))
    m.add(box((0.08, 0.4, 0.45), (1.92, 0.51, 0.55)), col(b))
    for side in (-1, 1):
        m.add(ellipsoid((1.0 + side * 0.13, 0.56, 0.5), (0.14, 0.05, 0.08)), col(b),
              transform=rotate_about((1.0 + side * 0.13, 0.56, 0.5), "Z", side * 20))
    m.add(sphere((1.0, 0.56, 0.5), 0.05), col(b))
    m.add(ellipsoid((0.5, 0.52, 0.5), (0.35, 0.05, 0.3)), glass("FFFFFF"))


@item
def champagne(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.06, 0.02, 0.1), (1.94, 0.42, 0.9), 0.03), col(a))
    for x0, x1 in ((0.06, 0.12), (1.88, 1.94)):
        m.add(box((x0, 0.02, 0.08), (x1, 0.46, 0.92), 0.02), col(shade(a, 0.8)))
    for i in range(4):
        for j in range(2):
            x, z = 0.3 + i * 0.47, 0.3 + j * 0.4
            m.add(lathe([(0.13, 0.0), (0.13, 0.42), (0.06, 0.55), (0.05, 0.72)], (x, 0.02, z), "y", 14), glass(b))
            m.add(cyl((x, 0.78, z), 0.055, 0.12, "y", segments=10), metal("D4AF37"))
    m.add(box((0.6, 0.15, 0.07), (1.4, 0.32, 0.09)), col("FFF8E1"))


@item
def camera_bag(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.15, 0.04, 0.25), (0.85, 0.58, 0.75), 0.1, 3), col(a))
    m.add(box((0.13, 0.4, 0.23), (0.87, 0.62, 0.77), 0.08, 3), col(shade(a, 0.8)))
    m.add(box((0.3, 0.15, 0.22), (0.7, 0.35, 0.24), 0.03), col(b))
    m.add(sweep([(0.15, 0.5, 0.5), (0.05, 0.8, 0.5), (0.5, 0.95, 0.5), (0.95, 0.8, 0.5), (0.85, 0.5, 0.5)], 0.025, segments=6), col("212121"))


@item
def grill(m, P):
    a, b = P["a"], P["b"]
    m.add(rod((0.5, 0.05, 1.5), (0.5, 1.05, 1.5), 0.06), metal("90A4AE"))
    for ang in (0, 120, 240):
        r = math.radians(ang)
        m.add(rod((0.5, 0.35, 1.5), (0.5 + 0.4 * math.cos(r), 0.02, 1.5 + 0.4 * math.sin(r)), 0.03), metal("90A4AE"))
    m.add(shell([(0.25, 1.0), (0.6, 1.15), (0.85, 1.42), (0.9, 1.5)], (1.0, 0, 1.0), "y", 0.04, 32), col(a))
    m.add(cyl((1.0, 1.48, 1.0), 0.86, 0.02, "y", segments=32), metal("757575"))
    for k in range(9):
        z = 0.3 + k * 0.175
        half = math.sqrt(max(0.0, 0.84 ** 2 - (z - 1.0) ** 2))
        m.add(rod((1.0 - half, 1.52, z), (1.0 + half, 1.52, z), 0.012), metal("BDBDBD"))
    for x, z, c in ((0.75, 0.85, "8D3B2F"), (1.2, 1.1, "8D3B2F"), (0.95, 1.3, "FFB74D")):
        m.add(ellipsoid((x, 1.58, z), (0.14, 0.05, 0.08)), col(c))
    m.add(box((1.75, 1.3, 0.85), (1.95, 1.42, 1.15), 0.02), glow("2196F3"))


@item
def tackle_box(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.08, 0.04, 0.2), (1.92, 0.5, 0.8), 0.05), col(a))
    m.add(box((0.06, 0.46, 0.18), (1.94, 0.62, 0.82), 0.05), col(shade(a, 0.85)))
    m.add(sweep(arc_points((1.0, 0.62, 0.5), 0.25, 0, 180, "xy", 10), 0.035, segments=8), col("212121"))
    for x in (0.6, 1.4):
        m.add(box((x - 0.08, 0.38, 0.16), (x + 0.08, 0.5, 0.2), 0.02), metal("ECEFF1"))
    m.add(ellipsoid((0.4, 0.66, 0.4), (0.1, 0.03, 0.04)), col(b))
    m.add(sweep([(0.4, 0.66, 0.4), (0.25, 0.7, 0.3), (0.2, 0.68, 0.2)], 0.008, segments=4), metal("B0BEC5"))


@item
def kitchen_table(m, P):
    a, b = P["a"], P["b"]
    for x in (0.25, 2.75):
        for z in (0.25, 1.75):
            m.add(lathe([(0.07, 0.0), (0.09, 0.2), (0.06, 0.5), (0.08, 0.8), (0.07, 1.08)], (x, 0, z), "y", 14), col(b))
    m.add(box((0.15, 0.95, 0.15), (2.85, 1.05, 1.85), 0.02), col(shade(b, 0.85)))
    m.add(box((0.04, 1.05, 0.04), (2.96, 1.2, 1.96), 0.05, 3), col(a))
    m.add(cyl((1.5, 1.22, 1.0), 0.25, 0.04, "y", segments=24), col("FFFFFF"))
    m.add(lathe([(0.1, 0.0), (0.12, 0.15), (0.08, 0.25)], (1.5, 1.24, 1.0), "y", 14), glass("B3E5FC"))
    for k in range(3):
        m.add(ico((1.45 + k * 0.05, 1.55, 1.0 + (k - 1) * 0.05), 0.07, 1), col(("F48FB1", "FFEB3B", "FFFFFF")[k]))
    for k in range(3):
        m.add(box((0.4, 1.21 + k * 0.012, 0.35 + k * 0.03), (0.75, 1.215 + k * 0.012, 0.6 + k * 0.03)), col(("FFFFFF", "E3F2FD", "FFF8E1")[k]))


@item
def crib_flatpack(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.04, 0.02, 0.06), (2.96, 0.6, 1.94), 0.03), col(a))
    m.add(box((0.04, 0.6, 0.06), (2.96, 0.62, 1.94)), col(shade(a, 0.9)))
    m.add(box((0.1, 0.62, 0.95), (2.9, 0.63, 1.05)), col("D9C9A8"))
    m.add(box((0.6, 0.15, 0.04), (1.6, 0.48, 0.06)), col(b))
    for k in range(5):
        x = 0.75 + k * 0.17
        m.add(box((x, 0.22, 0.03), (x + 0.04, 0.42, 0.04)), col("FFFFFF"))
    m.add(box((0.72, 0.22, 0.03), (1.55, 0.25, 0.04)), col("FFFFFF"))
    m.add(box((2.0, 0.2, 0.04), (2.7, 0.45, 0.06)), col("FFFFFF"))
    m.add(box((2.05, 0.28, 0.03), (2.65, 0.32, 0.04)), col("212121"))


@item
def rocking_chair(m, P):
    a, b = P["a"], P["b"]
    for x in (0.3, 1.7):
        m.add(sweep(arc_points((x, 2.2, 1.0), 2.15, 248, 292, "zy", 14), 0.05, segments=8), col(b))
        for z in (0.35, 1.65):
            m.add(rod((x, 0.12, z), (x, 0.82, z), 0.05), col(a))
    m.add(box((0.2, 0.78, 0.2), (1.8, 0.9, 1.8), 0.05), col(a))
    m.add(box((0.3, 0.9, 0.3), (1.7, 1.0, 1.7), 0.08), col("C0587E"))
    for x in (0.3, 1.7):
        m.add(rod((x, 0.85, 1.75), (x, 2.85, 1.85), 0.05), col(a))
        m.add(rod((x, 1.25, 0.3), (x, 1.3, 1.75), 0.04), col(a))
        m.add(rod((x, 0.85, 0.35), (x, 1.3, 0.3), 0.04), col(a))
    for k in range(5):
        x = 0.55 + k * 0.225
        m.add(rod((x, 1.0, 1.78), (x, 2.7, 1.85), 0.025), col(a))
    m.add(box((0.25, 2.65, 1.72), (1.75, 2.85, 1.95), 0.05), col(b))
    m.add(box((0.4, 1.0, 1.55), (1.6, 1.9, 1.7), 0.1), col("F8BBD0"))


@item
def baby_mobile(m, P):
    a, b = P["a"], P["b"]
    m.add(cyl((0.5, 0.02, 0.5), 0.15, 0.04, "y", segments=16), col("FFFFFF"))
    m.add(rod((0.5, 0.04, 0.5), (0.5, 0.9, 0.5), 0.015), col("FFFFFF"))
    for ang in (0, 120, 240):
        r = math.radians(ang)
        end = (0.5 + 0.3 * math.cos(r), 0.85, 0.5 + 0.3 * math.sin(r))
        m.add(rod((0.5, 0.88, 0.5), end, 0.01), col("FFFFFF"))
        m.add(rod(end, (end[0], 0.62, end[2]), 0.004, 4), col("FFFFFF"))
        m.add(ellipsoid((end[0], 0.55, end[2]), (0.08, 0.06, 0.06)), col(a))
        m.add(sphere((end[0], 0.62, end[2] - 0.02), 0.045), col(a))
        m.add(ellipsoid((end[0], 0.61, end[2] - 0.07), (0.025, 0.012, 0.03)), col("FF9800"))
    m.add(ico((0.5, 0.92, 0.5), 0.05, 1), col("FFEB3B"))


@item
def diapers(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.06, 0.02, 0.06), (1.94, 0.82, 1.94), 0.1, 3), col(a))
    m.add(box((0.5, 0.25, 0.04), (1.5, 0.6, 0.06), 0.03), col(b))
    m.add(ellipsoid((1.0, 0.43, 0.035), (0.25, 0.12, 0.01)), col("FFFFFF"))
    m.add(sphere((0.9, 0.46, 0.03), 0.03), col("212121"))
    m.add(sphere((1.1, 0.46, 0.03), 0.03), col("212121"))
    m.add(box((0.4, 0.82, 0.9), (1.6, 0.86, 1.1), 0.02), col(b))


@item
def paint_cans(m, P):
    a, b = P["a"], P["b"]
    for x in (0.5, 1.5):
        m.add(cyl((x, 0.32, 0.5), 0.36, 0.6, "y", segments=28), metal(a))
        m.add(torus((x, 0.62, 0.5), 0.34, 0.025, "y", 28, 6), metal("90A4AE"))
        m.add(cyl((x, 0.32, 0.135), 0.2, 0.01, "z", segments=20), col("FFFFFF"))
        m.add(cyl((x, 0.32, 0.13), 0.12, 0.01, "z", segments=16), col(b))
        m.add(sweep(arc_points((x, 0.62, 0.5), 0.33, 10, 170, "xy", 10), 0.012, segments=4), metal("90A4AE"))
    m.add(sweep([(0.15, 0.66, 0.5), (0.13, 0.4, 0.45)], 0.04, segments=8), col(b))


@item
def car_seat(m, P):
    a, b = P["a"], P["b"]
    m.add(sweep(arc_points((1.0, 0.0, 1.0), 0.95, 200, 340, "zy", 12), 0.06, segments=8), col("37474F"),
          transform=None)
    m.add(box((0.15, 0.08, 0.1), (1.85, 0.45, 1.9), 0.18, 4), col(a))
    m.add(box((0.15, 0.45, 1.35), (1.85, 1.75, 1.9), 0.2, 4), col(a))
    for x0, x1 in ((0.1, 0.32), (1.68, 1.9)):
        m.add(box((x0, 0.4, 0.15), (x1, 1.3, 1.85), 0.12, 3), col(a))
    m.add(box((0.4, 0.45, 0.25), (1.6, 0.55, 1.4), 0.08), col(b))
    m.add(box((0.4, 0.55, 1.3), (1.6, 1.6, 1.38), 0.1), col(b))
    m.add(ellipsoid((1.0, 1.55, 1.3), (0.35, 0.18, 0.05)), col(shade(b, 1.15)))
    for x in (0.7, 1.3):
        m.add(box((x - 0.04, 0.55, 0.5), (x + 0.04, 1.4, 1.3)), col("37474F"))
    m.add(box((0.85, 0.85, 1.28), (1.15, 0.98, 1.31), 0.02), col("E53935"))
    m.add(sweep(arc_points((1.0, 1.0, 1.0), 0.8, 30, 150, "xy", 14), 0.06, segments=8), col("37474F"))


@item
def hospital_bag(m, P):
    a, b = P["a"], P["b"]
    m.add(cyl((1.0, 0.38, 0.5), 0.36, 1.85, "x", segments=24, bevel=0.12), col(a))
    m.add(box((0.4, 0.68, 0.46), (1.6, 0.76, 0.54)), col(b))
    for x in (0.7, 1.3):
        m.add(sweep(arc_points((x, 0.72, 0.5), 0.18, 0, 180, "zy", 10), 0.03, segments=6), col(shade(a, 0.7)))
    m.add(box((0.85, 0.25, 0.12), (1.15, 0.45, 0.14), 0.02), col("FFFFFF"))
    m.add(sphere((1.0, 0.35, 0.11), 0.05, (1, 1, 0.3)), col("F48FB1"))


@item
def balloons(m, P):
    a, b = P["a"], P["b"]
    colors = [a, b, "FFF59D"]
    m.add(cyl((0.5, 0.05, 0.5), 0.12, 0.1, "y", segments=14), col("FFD700"))
    for k, (dx, dy, dz) in enumerate(((-0.15, 1.45, -0.05), (0.18, 1.55, 0.05), (0.0, 1.75, 0.12))):
        c = (0.5 + dx, dy, 0.5 + dz)
        m.add(ellipsoid(c, (0.22, 0.26, 0.22)), col(colors[k]))
        m.add(cyl((c[0], c[1] - 0.27, c[2]), 0.03, 0.04, "y", segments=8), col(colors[k]))
        m.add(sweep([(c[0], c[1] - 0.28, c[2]), (0.5 + dx * 0.4, 0.7, 0.5), (0.5, 0.1, 0.5)], 0.006, segments=4), col("EEEEEE"))
        m.add(ellipsoid((c[0] - 0.07, c[1] + 0.08, c[2] - 0.15), (0.05, 0.08, 0.02)), col("FFFFFF"))


@item
def balloons_grad(m, P):
    ITEMS["balloons"](m, P)


@item
def stroller(m, P):
    a, b = P["a"], P["b"]
    for x in (0.3, 1.7):
        for z in (0.3, 1.7):
            m.add(cyl((x, 0.18, z), 0.17, 0.08, "x", segments=20), col("212121"))
            m.add(cyl((x, 0.18, z), 0.07, 0.1, "x", segments=12), metal("B0BEC5"))
    m.add(box((0.3, 0.3, 0.3), (1.7, 0.36, 1.7), 0.02), metal("90A4AE"))
    m.add(box((0.25, 0.45, 0.25), (1.75, 0.95, 1.75), 0.2, 4), col(a))
    m.add(box((0.35, 0.85, 0.35), (1.65, 0.9, 1.65), 0.05), col(b))
    m.add(shell([(0.1, 0.0), (0.6, 0.35), (0.75, 0.6), (0.78, 0.75)], (0.75, 0.95, 1.0), "y", 0.02, 24), col(b),
          transform=rotate_about((0.75, 0.95, 1.0), "Z", 75))
    for z in (0.35, 1.65):
        m.add(rod((0.25, 0.9, z), (0.2, 1.85, z), 0.035), metal("90A4AE"))
    m.add(rod((0.2, 1.85, 0.3), (0.2, 1.85, 1.7), 0.05), col("212121"))


@item
def playpen(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.06, 0.02, 0.06), (1.94, 0.6, 1.94), 0.1, 3), col(a))
    for x0, x1 in ((0.1, 0.9), (1.1, 1.9)):
        for z0, z1 in ((0.1, 0.9), (1.1, 1.9)):
            m.add(box((x0, 0.58, z0), (x1, 0.62, z1), 0.03), col(shade(a, 0.9)))
    m.add(box((0.06, 0.25, 0.04), (1.94, 0.35, 0.06)), col(b))
    m.add(sweep(arc_points((1.0, 0.62, 1.0), 0.2, 0, 180, "xy", 8), 0.03, segments=6), col(b))


@item
def baby_bath(m, P):
    a, b = P["a"], P["b"]
    m.add(shell([(0.4, 0.0), (0.5, 0.3), (0.55, 0.5)], (0.0, 0, 0.0), "y", 0.04, 28), col(a),
          transform=Matrix.Translation((1.0, 0.04, 0.5)) @ Matrix.Diagonal((1.7, 1, 0.85, 1)))
    m.add(sphere((1.75, 0.65, 0.5), 0.2), col(a))
    m.add(ellipsoid((1.98, 0.6, 0.5), (0.1, 0.05, 0.08)), col(b))
    for z in (0.42, 0.58):
        m.add(sphere((1.85, 0.72, z), 0.03), col("212121"))
    m.add(ellipsoid((1.0, 0.45, 0.5), (0.7, 0.02, 0.32)), glass("B3E5FC"))


@item
def white_noise(m, P):
    a, b = P["a"], P["b"]
    m.add(lathe([(0.3, 0.02), (0.34, 0.12), (0.32, 0.38), (0.22, 0.5), (0.02, 0.54)], (0.5, 0, 0.5), "y", 28), col(a))
    m.add(cyl((0.5, 0.25, 0.17), 0.12, 0.02, "z", segments=20), col(b))
    for k in range(6):
        ang = k / 6 * 2 * math.pi
        m.add(sphere((0.5 + 0.22 * math.cos(ang), 0.45, 0.5 + 0.22 * math.sin(ang)), 0.02), col("B0BEC5"))
    m.add(cyl((0.5, 0.54, 0.5), 0.06, 0.02, "y", segments=12), glow("90CAF9"))


@item
def photo_album(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.12, 0.02, 0.15), (1.88, 0.3, 0.85), 0.03), col("FFF8E1"))
    m.add(box((0.08, 0.02, 0.12), (1.92, 0.06, 0.88), 0.02), col(a))
    m.add(box((0.08, 0.28, 0.12), (1.92, 0.34, 0.88), 0.02), col(a))
    m.add(box((0.06, 0.02, 0.12), (0.16, 0.34, 0.88), 0.03), col(shade(a, 0.8)))
    m.add(box((0.7, 0.34, 0.3), (1.4, 0.35, 0.7), 0.01), metal(b))
    m.add(box((0.75, 0.345, 0.34), (1.35, 0.352, 0.66)), col("8D6E63"))
    for k in range(4):
        m.add(box((1.7, 0.08 + k * 0.05, 0.14), (1.95, 0.11 + k * 0.05, 0.2)), col(("F48FB1", "90CAF9", "FFF59D", "A5D6A7")[k]))


@item
def sled(m, P):
    a, b = P["a"], P["b"]
    for z in (0.22, 0.78):
        path = [(2.9, 0.06, z), (0.6, 0.06, z)] + arc_points((0.6, 0.6, z), 0.54, 270, 140, "xy", 10)[1:]
        m.add(sweep(path, 0.035, segments=8), metal("B0BEC5"))
        for x in (0.8, 1.6, 2.4):
            m.add(rod((x, 0.06, z), (x, 0.3, z), 0.03), col(b))
    for k in range(6):
        z = 0.18 + k * 0.128
        m.add(box((0.55, 0.3, z), (2.95, 0.38, z + 0.1), 0.02), col(b))
    m.add(box((0.55, 0.38, 0.15), (2.95, 0.42, 0.85), 0.02), col(a))
    m.add(sweep([(0.55, 0.9, 0.3), (0.3, 0.95, 0.5), (0.55, 0.9, 0.7)], 0.025, segments=6), col("FFEB3B"))
    m.add(box((1.2, 0.42, 0.4), (2.2, 0.43, 0.6)), col("FFFFFF"))


@item
def skis(m, P):
    a, b = P["a"], P["b"]
    for z in (0.3, 0.6):
        path = [(4.9, 0.12, z), (0.4, 0.06, z)] + arc_points((0.4, 0.26, z), 0.2, 270, 180, "xy", 6)[1:]
        m.add(sweep(path, 0.06, segments=6), col(a), transform=None)
        m.add(box((2.2, 0.08, z - 0.06), (2.8, 0.22, z + 0.06), 0.03), col("212121"))
    for z in (0.8, 0.9):
        m.add(rod((0.6, 0.35, z), (4.4, 0.35, z), 0.025), metal("B0BEC5"))
        m.add(cyl((4.3, 0.35, z), 0.08, 0.02, "x", segments=12), col(b))
        m.add(cyl((0.75, 0.35, z), 0.04, 0.3, "x", segments=10), col("212121"))
    m.add(box((1.6, 0.0, 0.2), (1.7, 0.4, 0.95)), col(b))
    m.add(box((3.3, 0.0, 0.2), (3.4, 0.4, 0.95)), col(b))


@item
def snowman_kit(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.15, 0.02, 0.2), (0.85, 0.3, 0.8), 0.04), col("8D6E63"))
    m.add(rod((0.3, 0.38, 0.4), (0.7, 0.42, 0.55), 0.06, radius2=0.01), col(a))
    for k in range(3):
        m.add(sphere((0.35 + k * 0.15, 0.36, 0.7), 0.05), col(b))
    m.add(sweep([(0.2, 0.32, 0.3), (0.5, 0.42, 0.25), (0.8, 0.34, 0.35), (0.75, 0.3, 0.6)], 0.05, segments=8), col("C62828"))
    m.add(cyl((0.5, 0.5, 0.5), 0.12, 0.25, "y", segments=16), col("212121"))
    m.add(cyl((0.5, 0.4, 0.5), 0.2, 0.03, "y", segments=16), col("212121"))


@item
def thermos(m, P):
    a, b = P["a"], P["b"]
    m.add(cyl((0.5, 0.36, 0.5), 0.2, 0.62, "y", segments=24, bevel=0.03), col(a))
    m.add(cyl((0.5, 0.72, 0.5), 0.17, 0.12, "y", segments=20), col(b))
    m.add(cyl((0.5, 0.8, 0.5), 0.21, 0.08, "y", segments=20), col(a))
    m.add(torus((0.5, 0.2, 0.5), 0.2, 0.02, "y", 24, 6), col(b))
    m.add(sweep(arc_points((0.72, 0.42, 0.5), 0.14, -70, 70, "xy", 8), 0.025, segments=6), col(b))


@item
def shovel(m, P):
    a, b = P["a"], P["b"]
    m.add(rod((0.1, 0.4, 0.5), (3.0, 0.4, 0.5), 0.045), col(a))
    m.add(sweep(arc_points((0.1, 0.4, 0.5), 0.15, 90, 270, "zy", 8), 0.03, segments=6), col("212121"))
    m.add(hull([(3.0, 0.2, 0.15), (3.0, 0.2, 0.85), (3.95, 0.1, 0.1), (3.95, 0.1, 0.9),
                (3.0, 0.26, 0.15), (3.0, 0.26, 0.85), (3.95, 0.16, 0.1), (3.95, 0.16, 0.9)], 0.02), col(b))
    m.add(box((2.9, 0.15, 0.1), (3.05, 0.5, 0.9), 0.02), col(b))


@item
def costume_trunk(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.06, 0.02, 0.1), (1.94, 0.72, 1.9), 0.06, 3), col(a))
    m.add(cyl((1.0, 0.72, 1.0), 0.9, 1.88, "x", segments=24), col(a),
          transform=Matrix.Translation((0, 0.72, 0)) @ Matrix.Diagonal((1, 0.25, 1, 1)) @ Matrix.Translation((0, -0.72, 0)))
    for x in (0.4, 1.6):
        m.add(box((x - 0.06, 0.02, 0.08), (x + 0.06, 0.95, 1.92), 0.02), metal(b))
    m.add(box((0.85, 0.5, 0.06), (1.15, 0.72, 0.1), 0.02), metal(b))
    for k, c in enumerate(("FF4081", "40C4FF", "FFD740")):
        m.add(sweep([(0.6 + k * 0.3, 0.75, 0.1), (0.65 + k * 0.3, 0.6, 0.0), (0.6 + k * 0.3, 0.4, -0.02)], 0.035, segments=5), col(c))


@item
def kid_suitcase(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.1, 0.04, 0.2), (1.9, 0.75, 0.8), 0.12, 3), col(a))
    for k in range(5):
        m.add(sphere((0.3 + k * 0.35, 0.4 + 0.15 * math.sin(k * 1.7), 0.19), 0.06, (1, 1, 0.3)), col(b))
    m.add(hull([(1.4, 0.55, 0.18), (1.55, 0.7, 0.18), (1.7, 0.55, 0.18), (1.55, 0.4, 0.18), (1.55, 0.55, 0.16)]), col("FFEB3B"))
    m.add(sweep(arc_points((1.0, 0.75, 0.5), 0.2, 0, 180, "xy", 10), 0.035, segments=8), col(shade(a, 0.7)))
    for x in (0.25, 1.75):
        m.add(cyl((x, 0.04, 0.3), 0.05, 0.06, "x", segments=10), col("424242"))


@item
def toy_box(m, P):
    a, b = P["a"], P["b"]
    m.add(box((0.06, 0.02, 0.12), (1.94, 0.62, 0.88), 0.05), col(a))
    m.add(box((0.04, 0.6, 0.1), (1.96, 0.68, 0.9), 0.04), col(b))
    for k, c in enumerate(("E53935", "43A047", "1E88E5", "FDD835")):
        m.add(box((0.25 + k * 0.42, 0.2, 0.09), (0.5 + k * 0.42, 0.45, 0.12), 0.02), col(c))
    m.add(sphere((0.5, 0.78, 0.5), 0.12), col("E53935"))
    m.add(ellipsoid((1.4, 0.75, 0.45), (0.2, 0.08, 0.1)), col("FFEB3B"))
    m.add(sweep([(1.0, 0.68, 0.3), (1.05, 0.95, 0.4), (1.0, 1.0, 0.5)], 0.02, segments=5), col("8D6E63"))


@item
def folding_chairs(m, P):
    a, b = P["a"], P["b"]
    for k in range(4):
        z = 0.25 + k * 0.15
        m.add(box((0.1, 0.05, z - 0.04), (1.9, 1.85, z + 0.04), 0.03), col(a if k % 2 == 0 else shade(a, 0.92)))
        m.add(box((0.3, 1.3, z - 0.05), (1.7, 1.75, z + 0.05), 0.03), col(b))
    m.add(box((0.85, 1.5, 0.15), (1.15, 1.6, 0.85), 0.02), col("212121"))
