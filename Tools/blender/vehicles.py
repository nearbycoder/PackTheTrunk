"""Per-level vehicle bodies, built around the exact trunk cavity of each level.

Layout must match Assets/Scripts/Visuals/Vehicle.cs: the trunk interior spans (0,0,0)-(W,H,D)
in Unity space, walls are WALL thick, the ground is at GROUND_Y. The cavity floor, grid and
colliders stay procedural in Unity; this file builds the bodywork, the modeled obstructions
inside the trunk (wheel wells, shelves, the clown...) and the moving parts.

Exported hierarchy (names are looked up by Vehicle.cs):
    <Vehicle (Title)>  empty at the trunk origin
      Body             static bodywork
      Obstructions     the level's blocked cells, modeled (wheel wells, parcel shelf, toolbox, clown)
      Lid              pivot at the hinge (W/2, H+0.08, D+WALL)
      LidFlap          rear hatch panel, pivot at its top edge; Vehicle.cs parents it to Lid
      Tailgate         pickup only; pivot at (W/2, FLOOR_BOTTOM, -WALL)
      Wheel_0..3       pivots at wheel centres, axle along x
"""
import math

from mathutils import Matrix

from ptt_lib import (Model, arc_points, boolean_difference, box, col, cyl, ellipsoid, empty, glass, glow, hull, lathe, loft,
                     metal, rod, rotate_about, rounded_section, shade, sphere, sweep, torus)

GROUND_Y = -1.15
WALL = 0.35
FLOOR_BOTTOM = -0.3
WHEEL_R = 0.72

TIRE = "1C1C1E"
TRIM = "2E2F33"
CHROME = "D5D9DE"
GLASS = "5E7E99"
CARPET = "3A3A42"
PLASTIC = "4A4B52"

STYLES = {
    "sedan": dict(cabin=3.6, roof=1.15, hood=3.2, lean_back=0.75, lean_front=1.15, nose=0.75),
    "hatch": dict(cabin=3.0, roof=0.9, hood=2.2, lean_back=0.45, lean_front=1.0, nose=0.7),
    "suv": dict(cabin=4.0, roof=1.05, hood=2.6, lean_back=0.2, lean_front=0.75, nose=1.05),
    "wagon": dict(cabin=4.4, roof=0.8, hood=3.0, lean_back=0.15, lean_front=0.9, nose=0.75),
    "mini": dict(cabin=2.4, roof=1.1, hood=1.6, lean_back=0.2, lean_front=0.55, nose=0.75),
    "pickup": dict(cabin=3.0, roof=1.55, hood=3.0, lean_back=0.1, lean_front=0.55, nose=1.15),
    "clown": dict(cabin=2.2, roof=2.3, hood=1.4, lean_back=0.1, lean_front=0.25, nose=0.9),
    "van": dict(cabin=5.0, roof=0.5, hood=1.8, lean_back=0.1, lean_front=0.9, nose=0.9),
    "boxtruck": dict(cabin=3.2, roof=3.2, hood=2.0, lean_back=0.1, lean_front=0.5, nose=1.15),
    "convertible": dict(cabin=3.2, roof=0.0, hood=3.2, lean_back=0.0, lean_front=0.0, nose=0.7),
}


def build_vehicle(level):
    w, h, d = level["w"], level["h"], level["d"]
    if level["style"] == "toywagon":
        return build_toy_wagon(level)
    style = level["style"] if level["style"] in STYLES else "sedan"
    s = STYLES[style]
    paint_hex = level["bodyColor"].lstrip("#").upper()
    paint = col(paint_hex)
    dark_paint = col(shade(paint_hex, 0.7))
    t = WALL
    pickup = style == "pickup"

    body_bottom = GROUND_Y + 0.43
    cabin_start = d + t
    hood_start = cabin_start + s["cabin"]
    front = hood_start + s["hood"]
    roof = h + s["roof"]
    belt = (h + 0.25) if pickup else (h + 0.04)
    bed_top = None
    if style == "boxtruck":
        # Tall cargo box behind a normal-height cab.
        bed_top = h + 0.04
        belt = 1.5
        roof = s["roof"]
    elif pickup:
        bed_top = h
    rear = -0.62
    nose = min(s["nose"], belt - 0.15)
    rear_axle = max(1.2, d * 0.5)
    front_axle = hood_start + s["hood"] * 0.45

    root = empty(f"{level['vehicle']} ({level['title']})")
    body = Model("Body")

    # ---------------------------------------------------------------- body shell
    # One smooth lofted shell from tail to nose; the trunk opening and wheel arches are cut
    # out of it with booleans so every panel flows into the next.
    if pickup:
        rear_end = -t - 0.02
    else:
        rear_end = rear + 0.04
    extra_cuts = []
    if style == "convertible":
        extra_cuts.append(box((0.25, belt - 0.55, cabin_start + 0.3), (w - 0.25, 10.0, hood_start - 0.55)))
    body.add(shell_body(w, h, t, rear_end, front - 0.02, body_bottom, belt, cabin_start, hood_start, front, nose,
                        pickup, rear_axle, front_axle, bed_top=bed_top, extra_cutters=extra_cuts), paint)
    if pickup:
        for x0, x1 in ((-t, 0.0), (w, w + t)):
            body.add(box((x0 - 0.01, h - 0.03, -t - 0.01), (x1 + 0.01, h + 0.05, d + t), 0.03, 2), col(TRIM))
    # Dark wheel-well liners behind the cut arches.
    for z in (rear_axle, front_axle):
        for x in (-0.07, w + 0.07):
            body.add(cyl((x, GROUND_Y + WHEEL_R, z), WHEEL_R + 0.11, 0.02, "x", segments=32), col("0E0E10"))
    # Floor/back of the trunk for the area the body no longer covers.
    if not pickup:
        body.add(box((0.0, FLOOR_BOTTOM, d - 0.01), (w, bed_top or belt, d + 0.02)), paint)

    # ---------------------------------------------------------------- cabin
    if style == "convertible":
        convertible_cockpit(body, w, t, belt, cabin_start, hood_start, paint, paint_hex)
    else:
        greenhouse(body, w, t, belt, roof, cabin_start + 0.05, hood_start - 0.1, s["lean_back"], s["lean_front"], paint, style, paint_hex)
    if style == "boxtruck":
        for x in (-t - 0.012, w + t + 0.002):
            body.add(box((x, 1.2, 0.3), (x + 0.012, 1.55, d - 0.2)), col("FF7043"))
            body.add(box((x, 1.65, 0.3), (x + 0.012, 1.75, d - 0.2)), col("FFB74D"))
        body.add(box((-t, h + 0.04, d + 0.02), (w + t, h + 0.12, d + t)), col(TRIM))

    # ---------------------------------------------------------------- bumpers, grille, lights
    body.add(box((-t - 0.08, body_bottom - 0.05, rear - 0.28), (w + t + 0.08, body_bottom + 0.42, rear + 0.25), 0.16, 3), col(TRIM))
    body.add(box((-t - 0.08, body_bottom - 0.05, front - 0.25), (w + t + 0.08, body_bottom + 0.42, front + 0.25), 0.16, 3), col(TRIM))
    if style in ("sedan", "wagon"):
        for z0, z1 in ((rear - 0.3, rear - 0.24), (front + 0.24, front + 0.3)):
            body.add(box((-t, body_bottom + 0.22, z0), (w + t, body_bottom + 0.3, z1), 0.02), metal(CHROME))
    gw = min(w * 0.32, 1.4)
    grille_top = nose - 0.18
    grille_bottom = body_bottom + 0.48
    if grille_top - grille_bottom > 0.15:
        chrome_grille = style in ("pickup", "wagon", "sedan")
        body.add(box((w * 0.5 - gw - 0.06, grille_bottom - 0.06, front - 0.04), (w * 0.5 + gw + 0.06, grille_top + 0.06, front + 0.02), 0.05, 2),
                 metal(CHROME) if chrome_grille else col(TRIM))
        body.add(box((w * 0.5 - gw, grille_bottom, front - 0.02), (w * 0.5 + gw, grille_top, front + 0.03)), col("141416"))
        rows = max(2, int((grille_top - grille_bottom) / 0.09))
        for k in range(rows):
            y = grille_bottom + (k + 0.5) * (grille_top - grille_bottom) / rows
            body.add(box((w * 0.5 - gw + 0.04, y - 0.015, front + 0.02), (w * 0.5 + gw - 0.04, y + 0.015, front + 0.05)),
                     metal(CHROME) if chrome_grille else col(PLASTIC))
        body.add(cyl((w * 0.5, (grille_top + grille_bottom) / 2, front + 0.06), 0.09, 0.03, "z", segments=20), metal(CHROME))
    round_lights = style in ("mini", "clown", "wagon")
    light_y = nose - 0.32
    for x in (-t + 0.48, w + t - 0.48):
        if round_lights:
            body.add(cyl((x, light_y, front + 0.01), 0.27, 0.1, "z", segments=28), metal(CHROME))
            body.add(cyl((x, light_y, front + 0.05), 0.21, 0.06, "z", segments=28), glow("FFF6D5"))
        else:
            body.add(box((x - 0.42, light_y - 0.13, front - 0.04), (x + 0.42, light_y + 0.13, front + 0.03), 0.05, 2), metal(CHROME))
            body.add(box((x - 0.38, light_y - 0.1, front - 0.01), (x + 0.38, light_y + 0.1, front + 0.05), 0.04, 2), glow("FFF6D5"))
        body.add(box((x - 0.25, body_bottom + 0.15, front + 0.2), (x + 0.25, body_bottom + 0.3, front + 0.27), 0.03), glow("FFB74D"))
    if pickup:
        for x0, x1 in ((-t + 0.05, -0.05), (w + 0.05, w + t - 0.05)):
            body.add(box((x0, h * 0.35, -t - 0.05), (x1, h - 0.2, -t + 0.02), 0.04, 2), glow("E53935"))
            body.add(box((x0, h * 0.22, -t - 0.05), (x1, h * 0.33, -t + 0.02), 0.03), glow("FFB74D"))
    else:
        for x0, x1 in ((-t + 0.2, -t + 0.95), (w + t - 0.95, w + t - 0.2)):
            body.add(box((x0, -0.55, rear - 0.01), (x1, -0.06, rear + 0.06), 0.06, 2), glow("E53935"))
            body.add(box((x0 + 0.05, -0.66, rear - 0.04), (x1 - 0.05, -0.55, rear + 0.03), 0.03), glow("FFB74D"))
            body.add(box((x0 + 0.1, -0.35, rear - 0.06), (x1 - 0.1, -0.3, rear - 0.04)), glow("FFCDD2"))
    body.add(rod((w * 0.78, body_bottom + 0.02, rear - 0.05), (w * 0.78, body_bottom + 0.02, rear - 0.32), 0.07), metal(CHROME))

    # ---------------------------------------------------------------- fender lips
    for z in (rear_axle, front_axle):
        for side, x in ((-1, -t), (1, w + t)):
            flare_col = col(TRIM) if style in ("suv", "pickup") else paint
            body.add(torus((x, GROUND_Y + WHEEL_R, z), WHEEL_R + 0.13, 0.08, "x", 40, 10, arc=0.5), flare_col,
                     transform=Matrix.Translation((side * 0.03, 0, 0)))

    # ---------------------------------------------------------------- side details
    door_cuts = [cabin_start + 0.12, (cabin_start + hood_start) / 2, hood_start - 0.15]
    for x in (-t - 0.012, w + t + 0.002):
        for z in door_cuts:
            body.add(box((x - 0.004, body_bottom + 0.3, z - 0.024), (x + 0.016, belt - 0.05, z + 0.024)), col(shade(paint_hex, 0.62)))
        for z in door_cuts[:-1]:
            body.add(box((x - 0.025, belt - 0.32, z + 0.3), (x + 0.035, belt - 0.24, z + 0.62), 0.02), metal(CHROME))
        if style in ("sedan", "wagon", "hatch"):
            body.add(box((x - 0.01, belt - 0.12, cabin_start), (x + 0.02, belt - 0.08, hood_start)), metal(CHROME))
        body.add(box((x - 0.02, body_bottom + 0.18, rear_axle + WHEEL_R + 0.15), (x + 0.03, body_bottom + 0.3, front_axle - WHEEL_R - 0.15), 0.02), col(TRIM))
    for side in (-1, 1):
        x = -t - 0.2 if side < 0 else w + t + 0.2
        mirror_col = col("F5F5F5") if style == "mini" else paint
        body.add(hull([(x - 0.16, belt + 0.12, hood_start - 0.95), (x + 0.16, belt + 0.12, hood_start - 0.95),
                       (x - 0.16, belt + 0.38, hood_start - 0.9), (x + 0.16, belt + 0.38, hood_start - 0.9),
                       (x - 0.12, belt + 0.15, hood_start - 0.75), (x + 0.12, belt + 0.35, hood_start - 0.72)], 0.04, 2), mirror_col)
        body.add(box((x - 0.13, belt + 0.15, hood_start - 0.97), (x + 0.13, belt + 0.36, hood_start - 0.95)), glass("CFD8DC"))

    # ---------------------------------------------------------------- style flourishes
    if style in ("suv", "wagon", "van"):
        top_z0, top_z1 = cabin_start + s["lean_back"] + 0.3, hood_start - s["lean_front"] - 0.3
        for x in (0.1, w - 0.1):
            body.add(box((x - 0.06, roof + 0.04, top_z0), (x + 0.06, roof + 0.18, top_z1), 0.04, 2), metal(CHROME))
        for z in (top_z0 + 0.4, top_z1 - 0.4):
            body.add(box((0.0, roof + 0.16, z - 0.05), (w, roof + 0.22, z + 0.05), 0.02), col(TRIM))
    if style == "suv":
        zc = (cabin_start + hood_start) / 2
        body.add(cyl((w * 0.5, roof + 0.36, zc), 0.6, 0.26, "y", segments=28, bevel=0.06), col(TIRE))
        body.add(cyl((w * 0.5, roof + 0.36, zc), 0.36, 0.28, "y", segments=24), metal(CHROME))
        body.add(box((-t - 0.04, body_bottom + 0.05, front - 0.3), (w + t + 0.04, body_bottom + 0.15, front + 0.35), 0.04), metal(CHROME))
    if style == "wagon":
        for x in (-t - 0.015, w + t + 0.002):
            body.add(box((x, body_bottom + 0.32, 0.25), (x + 0.013, belt - 0.2, hood_start - 0.4)), col("8D5A2B"))
            for y in (body_bottom + 0.28, belt - 0.24):
                body.add(box((x - 0.006, y, 0.2), (x + 0.02, y + 0.06, hood_start - 0.35), 0.01), col("E8D5B0"))
            for z in (1.2, (cabin_start + hood_start) / 2):
                body.add(box((x - 0.006, body_bottom + 0.3, z - 0.03), (x + 0.02, belt - 0.2, z + 0.03)), col("E8D5B0"))
    if style == "mini":
        body.add(box((-t + 0.18, roof - 0.06, cabin_start + 0.3), (w + t - 0.18, roof + 0.09, hood_start - 0.62), 0.06, 3), col("F5F5F5"))
        for x in (w * 0.5 - 0.3, w * 0.5 + 0.12):
            body.add(hull([(x, belt + 0.02, hood_start + 0.25), (x + 0.18, belt + 0.02, hood_start + 0.25),
                           (x, nose + 0.12, front - 0.45), (x + 0.18, nose + 0.12, front - 0.45),
                           (x, belt + 0.035, hood_start + 0.25), (x + 0.18, nose + 0.135, front - 0.45)]), col("F5F5F5"))
    if style == "clown":
        dots = ["FFEB3B", "2196F3", "E91E63", "4CAF50", "9C27B0"]
        k = 0
        for x in (-t - 0.02, w + t + 0.02):
            for z in [0.4 + i * 0.95 for i in range(int((front - 0.6) / 0.95))]:
                for y in (-0.35, h * 0.55):
                    body.add(sphere((x, y, z), 0.24, (0.15, 1, 1), 16, 10), col(dots[k % len(dots)]))
                    k += 1
        body.add(sphere((w * 0.5, nose - 0.3, front + 0.18), 0.28), col("E53935"))
        horn_z = hood_start - 0.9
        body.add(rod((w * 0.5, roof + 0.05, horn_z), (w * 0.5, roof + 0.5, horn_z), 0.06), metal("D4AF37"))
        body.add(lathe([(0.06, 0.0), (0.1, 0.3), (0.32, 0.6)], (w * 0.5, roof + 0.55, horn_z - 0.2), "z", 24), metal("D4AF37"))
        body.add(sphere((w * 0.5, roof + 0.55, horn_z - 0.3), 0.2), col("E53935"))
        body.add(rod((-t + 0.1, belt, cabin_start + 0.5), (-t + 0.1, roof + 1.3, cabin_start + 0.5), 0.02), metal(CHROME))
        for i in range(6):
            a = i / 6 * 2 * math.pi
            body.add(sphere((-t + 0.1 + 0.16 * math.cos(a), roof + 1.35 + 0.16 * math.sin(a), cabin_start + 0.5), 0.1, (1, 1, 0.4)), col("FF4081"))
        body.add(sphere((-t + 0.1, roof + 1.35, cabin_start + 0.48), 0.09), col("FFEB3B"))
    if pickup:
        for x in (0.25, w * 0.5, w - 0.25):
            body.add(box((x - 0.12, roof + 0.02, cabin_start + 0.45), (x + 0.12, roof + 0.12, cabin_start + 0.6), 0.03), glow("FFB300"))
        body.add(rod((w + t + 0.05, belt - 0.4, hood_start - 0.3), (w + t + 0.05, roof + 0.3, hood_start - 0.3), 0.06), metal(CHROME))
    body.build(parent=root)

    build_obstructions(level, style, root)
    build_wheels(root, w, t, rear_axle, front_axle, style)

    # ---------------------------------------------------------------- lid / tailgate
    if pickup:
        hinge = (w * 0.5, FLOOR_BOTTOM, -t)
        gx, gy, gz = hinge
        gate = Model("Tailgate")
        gh = h - FLOOR_BOTTOM
        gate.add(box((gx - w * 0.5, gy, gz), (gx + w * 0.5, gy + gh, gz + t), 0.08, 3), paint)
        gate.add(box((gx - w * 0.38, gy + gh * 0.5, gz - 0.02), (gx + w * 0.38, gy + gh * 0.72, gz + 0.02), 0.04, 2), dark_paint)
        gate.add(box((gx - 0.32, gy + gh - 0.5, gz - 0.05), (gx + 0.32, gy + gh - 0.32, gz + 0.02), 0.04, 2), metal(CHROME))
        gate.add(box((gx - 0.5, gy + 0.2, gz - 0.03), (gx + 0.5, gy + 0.5, gz + 0.02), 0.03), col("FAFAF0"))
        gate.add(box((gx - 0.42, gy + 0.3, gz - 0.04), (gx + 0.42, gy + 0.4, gz - 0.02)), col("263238"))
        gate.build(origin=hinge, parent=root)
    else:
        hinge = (w * 0.5, h + 0.08, d + t)
        hx, hy, hz = hinge
        lid = Model("Lid")
        lid.add(box((hx - w * 0.5 - t, hy, hz - d - 2 * t), (hx + w * 0.5 + t, hy + 0.16, hz), 0.08, 3), paint)
        if style != "sedan":
            lid.add(box((hx - w * 0.5 + 0.12, hy + 0.15, hz - d * 0.62), (hx + w * 0.5 - 0.12, hy + 0.19, hz - 0.22), 0.04, 2), glass(GLASS))
            lid.add(rod((hx - w * 0.3, hy + 0.21, hz - d * 0.55), (hx + w * 0.1, hy + 0.21, hz - d * 0.3), 0.015), col(TRIM))
        if style in ("hatch", "mini"):
            lid.add(hull([(hx - w * 0.5 - t + 0.1, hy + 0.16, hz - d - 2 * t + 0.05), (hx + w * 0.5 + t - 0.1, hy + 0.16, hz - d - 2 * t + 0.05),
                          (hx - w * 0.5 - t + 0.1, hy + 0.34, hz - d - 2 * t + 0.02), (hx + w * 0.5 + t - 0.1, hy + 0.34, hz - d - 2 * t + 0.02),
                          (hx - w * 0.5 - t + 0.1, hy + 0.16, hz - d - 2 * t + 0.4), (hx + w * 0.5 + t - 0.1, hy + 0.16, hz - d - 2 * t + 0.4)], 0.04, 2), dark_paint)
        else:
            lid.add(box((hx - w * 0.5, hy + 0.15, hz - d - 2 * t + 0.08), (hx + w * 0.5, hy + 0.19, hz - d - 2 * t + 0.14), 0.01), metal(CHROME))
        if style == "sedan":
            lid.add(box((hx - 0.06, hy + 0.15, hz - d * 0.75), (hx + 0.06, hy + 0.18, hz - d * 0.25)), col(shade(paint_hex, 0.85)))
        lid.build(origin=hinge, parent=root)

        flap_origin = (hx, hy, hz - d - t)
        fx, fy, fz = flap_origin
        flap = Model("LidFlap")
        flap_bottom = 0.1
        flap.add(box((fx - w * 0.5 - t, flap_bottom, fz - t), (fx + w * 0.5 + t, fy + 0.16, fz), 0.08, 3), paint)
        panel_h = fy + 0.16 - flap_bottom
        outer = fz - t
        if style != "sedan" and panel_h > 1.2:
            gy0 = flap_bottom + panel_h * 0.52
            flap.add(box((fx - w * 0.5 + 0.08, gy0, outer - 0.03), (fx + w * 0.5 - 0.08, fy + 0.04, outer + 0.02), 0.06, 2), glass(GLASS))
            if style == "van":
                family_decal(flap, fx, gy0 + 0.1, outer - 0.035, w)
        plate_y = flap_bottom + min(0.35, panel_h * 0.3)
        flap.add(box((fx - 0.52, plate_y - 0.16, outer - 0.03), (fx + 0.52, plate_y + 0.16, outer + 0.01), 0.03, 2), col("FAFAF0"))
        flap.add(box((fx - 0.42, plate_y - 0.05, outer - 0.04), (fx + 0.42, plate_y + 0.05, outer - 0.02)), col("263238"))
        flap.add(box((fx - 0.3, plate_y + 0.24, outer - 0.06), (fx + 0.3, plate_y + 0.32, outer + 0.01), 0.03), metal(CHROME))
        flap.add(cyl((fx, flap_bottom + panel_h * 0.42, outer - 0.02), 0.13, 0.04, "z", segments=24), metal(CHROME))
        if style == "sedan":
            for k, cl in enumerate(("FFFFFF", "F48FB1")):
                flap.add(sweep(arc_points((fx + 0.75, plate_y + 0.05, outer - 0.03), 0.14 - k * 0.04, 0, 360, "xy", 16), 0.02, segments=6), col(cl))
        flap.build(origin=flap_origin, parent=root)

    return root


def shell_body(w, h, t, z_rear, z_front, y_bottom, belt, cabin_start, hood_start, front, nose,
               pickup, rear_axle, front_axle, bed_top=None, extra_cutters=()):
    """Lofted car body with rounded ends, cut open for the trunk/bed and wheel arches."""
    cx = w * 0.5
    half = w * 0.5 + t

    def top(z):
        if bed_top is not None and z < cabin_start:
            return bed_top
        if z <= hood_start:
            return belt
        s_ = (z - hood_start) / max(0.01, front - hood_start)
        return belt + (nose + 0.05 - belt) * (s_ ** 1.4)

    zs = []
    end_round = [0.0, 0.04, 0.12, 0.25, 0.42]
    for e in end_round:
        zs.append(z_rear + e)
    z = z_rear + 0.6
    while z < z_front - 0.6:
        zs.append(z)
        z += 0.2
    for e in reversed(end_round):
        zs.append(z_front - e)
    zs = sorted(set(round(v, 4) for v in zs))

    sections = []
    for z in zs:
        e = min(z - z_rear, z_front - z)
        k = min(1.0, e / 0.42)
        round_in = 1.0 - (1.0 - math.sqrt(max(0.0, 1.0 - (1.0 - k) ** 2)))  # quarter-circle profile
        width = half - 0.16 * (1.0 - round_in)
        yt = top(z) - 0.12 * (1.0 - round_in)
        yb = y_bottom + 0.12 * (1.0 - round_in)
        sections.append(rounded_section(cx, yb, yt, width - 0.02, width - 0.05, 0.3, 0.14, z, steps=7))
    shell = loft(sections)

    cutters = []
    if pickup:
        cutters.append(box((0.0, -0.02, -4.0), (w, 10.0, cabin_start - t)))
    else:
        cutters.append(box((0.0, -0.02, 0.0), (w, 10.0, cabin_start - t)))
        cutters.append(box((0.0, 0.1, -4.0), (w, 10.0, 0.0)))
    for z in (rear_axle, front_axle):
        for x0, x1 in ((-t - 1.0, -0.08), (w + 0.08, w + t + 1.0)):
            cutters.append(cyl(((x0 + x1) / 2, GROUND_Y + WHEEL_R, z), WHEEL_R + 0.12, x1 - x0, "x", segments=40))
    cutters.extend(extra_cutters)
    return boolean_difference(shell, cutters)


def convertible_cockpit(body, w, t, belt, cabin_start, hood_start, paint, paint_hex):
    """Open-top cabin: two bucket seats, a steering wheel and a raked windshield."""
    seat_z0 = cabin_start + 0.55
    for cx in (w * 0.28, w * 0.72):
        body.add(box((cx - 0.4, belt - 0.55, seat_z0), (cx + 0.4, belt - 0.2, seat_z0 + 0.8), 0.12, 3), col("5D4037"))
        body.add(box((cx - 0.38, belt - 0.25, seat_z0 - 0.05), (cx + 0.38, belt + 0.55, seat_z0 + 0.18), 0.14, 3), col("6D4C41"),
                 transform=rotate_about((cx, belt - 0.25, seat_z0), "X", -12))
        body.add(box((cx - 0.2, belt + 0.5, seat_z0 - 0.02), (cx + 0.2, belt + 0.75, seat_z0 + 0.14), 0.08, 3), col("6D4C41"))
    body.add(box((0.25, belt - 0.55, hood_start - 0.95), (w - 0.25, belt + 0.05, hood_start - 0.55), 0.08, 3), col(TRIM))
    sw = (w * 0.72, belt + 0.2, hood_start - 0.95)
    body.add(torus(sw, 0.24, 0.035, "z", 28, 8), col("212121"), transform=rotate_about(sw, "X", -25))
    body.add(rod(sw, (w * 0.72, belt - 0.1, hood_start - 0.7), 0.04), col("212121"))
    wz = hood_start - 0.6
    body.add(box((0.0, belt, wz - 0.03), (w, belt + 0.75, wz + 0.03), 0.03), glass(GLASS),
             transform=rotate_about((w * 0.5, belt, wz), "X", -28))
    for x in (0.0, w):
        body.add(rod((x, belt, wz), (x, belt + 0.66, wz - 0.35), 0.04), metal(CHROME))
    body.add(rod((0.0, belt + 0.66, wz - 0.35), (w, belt + 0.66, wz - 0.35), 0.035), metal(CHROME))
    body.add(box((0.25, belt - 0.6, cabin_start + 0.3), (w - 0.25, belt - 0.55, hood_start - 0.55)), col("4E342E"))


def build_toy_wagon(level):
    """Grandpa Joe's little red wagon: a steel tub on four spoked wheels with a pull handle."""
    w, h, d = level["w"], level["h"], level["d"]
    paint_hex = level["bodyColor"].lstrip("#").upper()
    paint = col(paint_hex)
    t = 0.18
    root = empty(f"{level['vehicle']} ({level['title']})")
    body = Model("Body")
    wall_top = h + 0.06
    body.add(box((-t, -0.32, -t), (w + t, 0.0, d + t), 0.06, 3), paint)
    for x0, x1 in ((-t, 0.0), (w, w + t)):
        body.add(box((x0, -0.32, -t), (x1, wall_top, d + t), 0.05, 3), paint)
    body.add(box((0.0, -0.32, d), (w, wall_top, d + t), 0.05, 3), paint)
    # Rolled lip and a cream stripe, like the real thing.
    for x in (-t * 0.5, w + t * 0.5):
        body.add(rod((x, wall_top, -t), (x, wall_top, d + t), 0.06, 12), paint)
        body.add(box((x - t * 0.5 - 0.005, wall_top * 0.45, 0.1), (x + t * 0.5 + 0.005, wall_top * 0.45 + 0.12, d - 0.1)), col("FFF3E0"))
    body.add(rod((-t, wall_top, d + t * 0.5), (w + t, wall_top, d + t * 0.5), 0.06, 12), paint)
    # Under-frame and axles.
    body.add(box((w * 0.5 - 0.12, -0.55, 0.2), (w * 0.5 + 0.12, -0.32, d - 0.2), 0.03), col("37474F"))
    wheel_r = 0.55
    axle_y = GROUND_Y + wheel_r
    axles = (0.45, d - 0.45)
    for z in axles:
        body.add(rod((-t - 0.12, axle_y, z), (w + t + 0.12, axle_y, z), 0.05), metal(CHROME))
        body.add(box((w * 0.5 - 0.15, axle_y, z - 0.12), (w * 0.5 + 0.15, -0.32, z + 0.12), 0.03), col("37474F"))
    # Pull handle from the front axle.
    hz = d - 0.45
    body.add(rod((w * 0.5, axle_y + 0.05, hz + 0.1), (w * 0.5, axle_y + 0.05, d + 0.6), 0.05), col("37474F"))
    body.add(rod((w * 0.5, axle_y + 0.05, d + 0.6), (w * 0.5, 0.55, d + 1.9), 0.045), col("212121"))
    body.add(rod((w * 0.5 - 0.3, 0.6, d + 1.95), (w * 0.5 + 0.3, 0.6, d + 1.95), 0.07), col("212121"))
    body.build(parent=root)

    for i, (x, z) in enumerate(((-t - 0.1, axles[0]), (w + t + 0.1, axles[0]), (-t - 0.1, axles[1]), (w + t + 0.1, axles[1]))):
        centre = Matrix.Translation((x, axle_y, z))
        wheel = Model(f"Wheel_{i}")
        wheel.add(torus((0, 0, 0), wheel_r - 0.08, 0.08, "x", 32, 10), col("212121"), transform=centre)
        side = -1 if x < 0 else 1
        wheel.add(cyl((0, 0, 0), wheel_r - 0.12, 0.08, "x", segments=28), col("ECEFF1"), transform=centre)
        for k in range(8):
            a = k / 8 * 2 * math.pi
            wheel.add(box((-0.05, -0.02, 0.06), (0.05, 0.02, wheel_r - 0.14)), col(paint_hex), transform=centre @ Matrix.Rotation(a, 4, "X"))
        wheel.add(cyl((side * 0.06, 0, 0), 0.1, 0.08, "x", segments=16), paint, transform=centre)
        wheel.build(origin=(x, axle_y, z), parent=root)

    hinge = (w * 0.5, FLOOR_BOTTOM, -t)
    gx, gy, gz = hinge
    gate = Model("Tailgate")
    gate.add(box((gx - w * 0.5, gy, gz), (gx + w * 0.5, wall_top, gz + t), 0.04, 3), paint)
    gate.add(box((gx - w * 0.4, gy + (wall_top - gy) * 0.45, gz - 0.01), (gx + w * 0.4, gy + (wall_top - gy) * 0.45 + 0.12, gz + 0.01)), col("FFF3E0"))
    gate.build(origin=hinge, parent=root)
    return root


def greenhouse(model, w, t, base_y, roof, z0, z1, lean_back, lean_front, paint, style, paint_hex):
    """Lofted windowed cabin: tinted glass with rounded corners, a crowned roof and pillars."""
    inset = 0.2
    bx0, bx1 = -t + 0.06, w + t - 0.06
    tx0, tx1 = -t + inset, w + t - inset
    tz0, tz1 = z0 + lean_back, z1 - lean_front
    if tz1 - tz0 < 0.6:
        mid = (z0 + z1) / 2
        tz0, tz1 = mid - 0.3, mid + 0.3
    cx = w * 0.5
    half_b, half_t = (bx1 - bx0) / 2, (tx1 - tx0) / 2

    def height_at(z):
        if z < tz0:
            return base_y + (roof - base_y) * max(0.0, (z - z0) / max(0.01, tz0 - z0))
        if z > tz1:
            return base_y + (roof - base_y) * max(0.0, (z1 - z) / max(0.01, z1 - tz1))
        return roof

    def half_top_at(z):
        f = (height_at(z) - base_y) / max(0.01, roof - base_y)
        return half_b + (half_t - half_b) * f

    zs = [z0 + 0.001] + [z0 + (z1 - z0) * i / 24 for i in range(1, 24)] + [z1 - 0.001]
    sections = []
    for z in zs:
        y1 = max(base_y + 0.04, height_at(z))
        sections.append(rounded_section(cx, base_y - 0.02, y1, half_b, half_top_at(z), 0.18, 0.02, z, steps=5))
    model.add(loft(sections), glass(GLASS))

    roof_col = col("F5F5F5") if style == "mini" else paint
    rz = [tz0 - 0.1 + (tz1 - tz0 + 0.2) * i / 12 for i in range(13)]
    roof_secs = []
    for i, z in enumerate(rz):
        e = min(i, 12 - i) / 2.0
        k = min(1.0, e)
        crown = 0.05 * math.sin(math.pi * i / 12)
        roof_secs.append(rounded_section(cx, roof - 0.14, roof + 0.05 + crown, half_t + 0.05 * k, half_t - 0.02, 0.12, 0.03, z, steps=5))
    model.add(loft(roof_secs), roof_col)

    bottom = [(bx0, base_y, z0), (bx1, base_y, z0), (bx0, base_y, z1), (bx1, base_y, z1)]
    top = [(tx0, roof, tz0), (tx1, roof, tz0), (tx0, roof, tz1), (tx1, roof, tz1)]
    for b, tp in zip(bottom, top):
        model.add(rod(b, tp, 0.08, 12), paint)
    posts = [0.5] + ([0.75] if style in ("van", "wagon") else [])
    for f in posts:
        bz, tz = z0 + (z1 - z0) * f, tz0 + (tz1 - tz0) * f
        for bx, tx in ((bx0, tx0), (bx1, tx1)):
            model.add(rod((bx, base_y, bz), (tx, roof, tz), 0.075, 12), paint)
    trim = metal(CHROME) if style in ("sedan", "wagon") else col(shade(paint_hex, 0.6))
    for bx in (bx0 - 0.01, bx1 + 0.01):
        model.add(rod((bx, base_y + 0.02, z0), (bx, base_y + 0.02, z1), 0.03, 8), trim)


def family_decal(model, cx, y, z, w):
    """Stick-figure family sticker on the minivan's rear glass."""
    for k, size in enumerate((1.0, 0.95, 0.65, 0.55, 0.4)):
        x = cx - w * 0.3 + k * 0.32
        hgt = 0.5 * size
        model.add(sphere((x, y + hgt + 0.06, z), 0.06 * (0.8 + size * 0.3), (1, 1, 0.2)), col("FFFFFF"))
        model.add(rod((x, y + 0.18 * size, z), (x, y + hgt, z), 0.018, 4), col("FFFFFF"))
        for dx in (-0.06, 0.06):
            model.add(rod((x, y + 0.18 * size, z), (x + dx, y, z), 0.016, 4), col("FFFFFF"))
            model.add(rod((x, y + hgt * 0.8, z), (x + dx * 1.5, y + hgt * 0.55, z), 0.014, 4), col("FFFFFF"))
    model.add(sphere((cx + w * 0.3 + 0.1, y + 0.12, z), 0.07, (1.4, 1, 0.2)), col("FFFFFF"))


def build_wheels(root, w, t, rear_axle, front_axle, style):
    whitewall = style == "wagon"
    for i, (x, z) in enumerate(((-t - 0.12, rear_axle), (w + t + 0.12, rear_axle), (-t - 0.12, front_axle), (w + t + 0.12, front_axle))):
        centre = Matrix.Translation((x, GROUND_Y + WHEEL_R, z))
        wheel = Model(f"Wheel_{i}")
        r = WHEEL_R
        tire = lathe([(0.42, -0.22), (0.6, -0.235), (r - 0.04, -0.21), (r, -0.12), (r, 0.12), (r - 0.04, 0.21), (0.6, 0.235), (0.42, 0.22)],
                     (0, 0, 0), "x", 36)
        wheel.add(tire, col(TIRE), transform=centre)
        for k in range(18):
            a = k / 18 * 2 * math.pi
            wheel.add(box((-0.19, -0.05, r - 0.035), (0.19, 0.05, r + 0.02)), col("101012"), transform=centre @ Matrix.Rotation(a, 4, "X"))
        side = -1 if x < 0 else 1
        if whitewall:
            wheel.add(torus((side * 0.235, 0, 0), 0.54, 0.06, "x", 32, 6, scale=(1, 1, 0.3)), col("F5F5F0"), transform=centre)
        wheel.add(cyl((side * 0.2, 0, 0), 0.44, 0.06, "x", segments=32), metal(CHROME), transform=centre)
        for k in range(5):
            a = k / 5 * 2 * math.pi
            spoke = box((side * 0.2 - 0.03, -0.045, 0.1), (side * 0.2 + 0.035, 0.045, 0.4), 0.015)
            wheel.add(spoke, metal("ECEFF1"), transform=centre @ Matrix.Rotation(a, 4, "X"))
        wheel.add(cyl((side * 0.24, 0, 0), 0.13, 0.05, "x", segments=20), metal("ECEFF1"), transform=centre)
        wheel.add(cyl((side * 0.27, 0, 0), 0.06, 0.02, "x", segments=12), col(TRIM), transform=centre)
        wheel.build(origin=(x, GROUND_Y + WHEEL_R, z), parent=root)


# ----------------------------------------------------------------------------- obstructions

def parse_blocked(level):
    cells = set()
    for y, layer in enumerate(level.get("blocked") or []):
        rows = layer.split("|")
        for r, row in enumerate(rows):
            z = len(rows) - 1 - r
            for x, ch in enumerate(row):
                if ch == "#":
                    cells.add((x, y, z))
    return cells


def _runs(values):
    """Split ints into sorted contiguous runs."""
    out = []
    for v in sorted(values):
        if out and v == out[-1][-1] + 1:
            out[-1].append(v)
        else:
            out.append([v])
    return out


def build_obstructions(level, style, root):
    """Model each level's blocked cells as something that would really be in that trunk."""
    cells = parse_blocked(level)
    if not cells:
        return
    w, h, d = level["w"], level["h"], level["d"]
    m = Model("Obstructions")
    carpet = col(CARPET)
    used = set()

    # The clown car's resident clown sits in a 2-tall column; balloons fill the top corners.
    if style == "clown":
        for (x, y, z) in sorted(cells):
            if y == 0 and (x, 1, z) in cells:
                clown_passenger(m, x, z)
                used |= {(x, 0, z), (x, 1, z)}
        for c in cells - used:
            if c[1] == h - 1:
                balloons(m, *c)
                used.add(c)

    # Wheel wells: carpeted quarter-round humps against the side walls.
    for side_x in (0, w - 1):
        zs = [z for (x, y, z) in cells if x == side_x and y == 0 and (x, y, z) not in used]
        for run in _runs(zs):
            z0, z1 = run[0], run[-1] + 1
            wall_x = 0.0 if side_x == 0 else float(w)
            inward = 1 if side_x == 0 else -1

            def quarter(r, za, zb):
                pts = [(wall_x, 0.0, za), (wall_x, 0.0, zb)]
                for k in range(13):
                    a = math.pi / 2 * k / 12
                    pts += [(wall_x + inward * math.cos(a) * r, math.sin(a) * r, zz) for zz in (za, zb)]
                return hull(pts)
            m.add(quarter(0.97, z0 + 0.06, z1 - 0.06), carpet, smooth=True)
            m.add(quarter(0.99, z0 + 0.02, z0 + 0.06), col(PLASTIC))
            m.add(quarter(0.99, z1 - 0.06, z1 - 0.02), col(PLASTIC))
            used |= {(side_x, 0, z) for z in run}

    # Floor humps in the middle (spare tyre well cover).
    for (x, y, z) in [c for c in cells if c[1] == 0 and c not in used]:
        m.add(box((x + 0.04, 0.0, z + 0.04), (x + 0.96, 0.92, z + 0.96), 0.3, 4), carpet)
        m.add(torus((x + 0.5, 0.93, z + 0.5), 0.28, 0.04, "y", 24, 6), col(shade(CARPET, 0.8)))
        used.add((x, y, z))

    # Pickup toolbox across the cab end.
    if style == "pickup":
        tb = [c for c in cells if c[1] >= 1 and c not in used]
        if tb:
            xs, ys, zs = [c[0] for c in tb], [c[1] for c in tb], [c[2] for c in tb]
            x0, x1, y0, y1, z0, z1 = min(xs), max(xs) + 1, min(ys), max(ys) + 1, min(zs), max(zs) + 1
            m.add(box((x0 - 0.3, y0 + 0.02, z0 + 0.03), (x1 + 0.3, y1 - 0.02, z1 - 0.02), 0.05, 2), metal("B0B6BC"))
            for k in range(int((x1 - x0 + 0.6) / 0.2)):
                for j in range(4):
                    px = x0 - 0.25 + k * 0.2 + (0.1 if j % 2 else 0)
                    m.add(box((px, y0 + 0.18 + j * 0.2, z0), (px + 0.1, y0 + 0.22 + j * 0.2, z0 + 0.03)), metal("CFD5DA"))
            m.add(box((x0 - 0.32, y1 - 0.06, z0), (x1 + 0.32, y1 + 0.04, z1), 0.03), metal("9AA0A6"))
            for x in (x0 + 0.5, x1 - 0.5):
                m.add(box((x - 0.15, y1 - 0.4, z0 - 0.03), (x + 0.15, y1 - 0.25, z0 + 0.01), 0.02), col(TRIM))
            used |= set(tb)

    # Top rows: parcel shelf near the opening, headrests by the seats.
    for row_z, kind in ((0, "shelf"), (d - 1, "headrests")):
        row = [c for c in cells if c[1] == h - 1 and c[2] == row_z and c not in used]
        for run in _runs([c[0] for c in row]):
            x0, x1 = run[0], run[-1] + 1
            if kind == "shelf":
                m.add(box((x0 + 0.02, h - 1 + 0.02, row_z + 0.02), (x1 - 0.02, h - 0.02, row_z + 0.98), 0.08, 3), col(PLASTIC))
                m.add(box((x0 + 0.04, h - 0.06, row_z + 0.04), (x1 - 0.04, h - 0.01, row_z + 0.96), 0.03), carpet)
                for k in range(len(run)):
                    if len(run) == 1 or k in (0, len(run) - 1):
                        cx = x0 + k + 0.5
                        m.add(cyl((cx, h - 0.005, row_z + 0.5), 0.3, 0.02, "y", segments=24), col("1A1A1D"))
                        m.add(cyl((cx, h + 0.005, row_z + 0.5), 0.12, 0.02, "y", segments=16), metal("8E8E93"))
            else:
                m.add(box((x0 + 0.04, h - 1, row_z + 0.4), (x1 - 0.04, h - 0.5, row_z + 0.98), 0.08, 3), col(PLASTIC))
                for k in range(len(run)):
                    cx = x0 + k + 0.5
                    for dx in (-0.15, 0.15):
                        m.add(rod((cx + dx, h - 0.5, row_z + 0.7), (cx + dx, h - 0.35, row_z + 0.7), 0.025), metal(CHROME))
                    m.add(box((cx - 0.38, h - 0.38, row_z + 0.45), (cx + 0.38, h - 0.02, row_z + 0.95), 0.12, 3), col("5A5560"))
            used |= {(x, h - 1, row_z) for x in run}

    for (x, y, z) in cells - used:
        m.add(box((x + 0.03, y + 0.03, z + 0.03), (x + 0.97, y + 0.97, z + 0.97), 0.15, 3), col("5A5560"))
    m.build(parent=root)


def clown_passenger(m, x, z):
    """The clown who already lives in the clown car: sits in cell column (x, 0..1, z), facing the rear."""
    cx, cz = x + 0.5, z + 0.5
    m.add(box((x + 0.05, 0.0, z + 0.1), (x + 0.95, 0.45, z + 0.95), 0.12, 3), col("1E88E5"))
    for k, cl in enumerate(("FFEB3B", "E53935", "4CAF50")):
        m.add(sphere((x + 0.25 + k * 0.25, 0.35, z + 0.08), 0.06, (1, 1, 0.4)), col(cl))
    for dx in (-0.2, 0.2):
        m.add(ellipsoid((cx + dx, 0.12, z + 0.02), (0.13, 0.1, 0.18)), col("E53935"))
    m.add(lathe([(0.32, 0.45), (0.3, 0.9), (0.24, 1.25)], (cx, 0, cz + 0.05), "y", 20), col("FFFFFF"))
    for y in (0.65, 0.95):
        m.add(sphere((cx, y, cz - 0.27), 0.06), col("E53935"))
    m.add(torus((cx, 1.27, cz + 0.05), 0.24, 0.06, "y", 20, 6), col("FFEB3B"))
    m.add(sphere((cx, 1.55, cz + 0.02), 0.27), col("FFFFFF"))
    m.add(sphere((cx, 1.53, cz - 0.26), 0.08), col("E53935"))
    for dx in (-0.25, 0.25):
        m.add(sphere((cx + dx, 1.62, cz + 0.02), 0.13), col("FF7043"))
    for dx in (-0.09, 0.09):
        m.add(sphere((cx + dx, 1.62, cz - 0.23), 0.03), col("1E1E22"))
    m.add(torus((cx, 1.44, cz - 0.22), 0.08, 0.015, "z", 12, 4, arc=0.5, scale=(1, -1, 1)), col("C62828"))
    m.add(lathe([(0.16, 1.75), (0.02, 1.97)], (cx, 0, cz + 0.02), "y", 16), col("7E57C2"))
    for side in (-1, 1):
        m.add(sweep([(cx + side * 0.26, 1.15, cz), (cx + side * 0.4, 0.85, cz - 0.15), (cx + side * 0.25, 0.6, cz - 0.3)], 0.07, segments=8), col("FFFFFF"))
        m.add(sphere((cx + side * 0.25, 0.57, cz - 0.32), 0.08), col("FFFFFF"))


def balloons(m, x, y, z):
    colours = ["E53935", "FFEB3B", "1E88E5", "8E24AA"]
    for k in range(3):
        a = k / 3 * 2 * math.pi + x
        bx, bz = x + 0.5 + 0.18 * math.cos(a), z + 0.5 + 0.18 * math.sin(a)
        m.add(ellipsoid((bx, y + 0.65, bz), (0.2, 0.24, 0.2)), col(colours[(x + z + k) % len(colours)]))
        m.add(sweep([(bx, y + 0.41, bz), (x + 0.5, y + 0.05, z + 0.5)], 0.006, segments=4), col("EEEEEE"))
    m.add(box((x + 0.4, y, z + 0.4), (x + 0.6, y + 0.06, z + 0.6), 0.02), col("424242"))
