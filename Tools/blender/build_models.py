"""Builds every Pack The Trunk model and exports FBX files into Assets/Resources/Models.

    blender -b --python Tools/blender/build_models.py -- [items] [vehicles] [props] [--preview DIR] [--only id,id]

Run with no set names to build everything. --preview renders a PNG per model for review.
"""
import json
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import ptt_lib  # noqa: E402
from items import ITEMS  # noqa: E402

ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
DATA = os.path.join(ROOT, "Assets", "Resources", "PackTheTrunkData.json")
OUT = os.path.join(ROOT, "Assets", "Resources", "Models")


def parse_cells(layers):
    cells = []
    for y, layer in enumerate(layers):
        rows = layer.split("|")
        for r, row in enumerate(rows):
            z = len(rows) - 1 - r
            for x, ch in enumerate(row):
                if ch not in ". ":
                    cells.append((x, y, z, ch))
    return cells


def args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = {"sets": [], "preview": None, "only": None}
    i = 0
    while i < len(argv):
        if argv[i] == "--preview":
            opts["preview"] = argv[i + 1]
            i += 2
        elif argv[i] == "--only":
            opts["only"] = set(argv[i + 1].split(","))
            i += 2
        else:
            opts["sets"].append(argv[i])
            i += 1
    if not opts["sets"]:
        opts["sets"] = ["items", "vehicles", "props"]
    return opts


def _setup_preview_scene():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.eevee.taa_render_samples = 32
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Punchy"
    world = bpy.data.worlds.new("preview")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs["Color"].default_value = (0.42, 0.47, 0.55, 1.0)
    bg.inputs["Strength"].default_value = 0.9
    scene.world = world
    if "preview_sun" not in bpy.data.objects:
        sun = bpy.data.objects.new("preview_sun", bpy.data.lights.new("preview_sun", "SUN"))
        sun.data.energy = 3.2
        sun.data.angle = math.radians(8)
        sun.rotation_euler = (math.radians(40), math.radians(-20), math.radians(150))
        scene.collection.objects.link(sun)
        fill = bpy.data.objects.new("preview_fill", bpy.data.lights.new("preview_fill", "SUN"))
        fill.data.energy = 0.8
        fill.rotation_euler = (math.radians(70), 0, math.radians(-30))
        scene.collection.objects.link(fill)


def render_preview(path, objects, size=360, views=((35, -35), (22, 150))):
    """Eevee render of the objects from the game camera's direction plus a reverse angle."""
    _setup_preview_scene()
    scene = bpy.context.scene
    mins = Vector((1e9, 1e9, 1e9))
    maxs = Vector((-1e9, -1e9, -1e9))
    for o in objects:
        for obj in [o] + list(o.children_recursive):
            if obj.type != "MESH":
                continue
            for corner in obj.bound_box:
                w = obj.matrix_world @ Vector(corner)
                mins = Vector(map(min, mins, w))
                maxs = Vector(map(max, maxs, w))
    center = (mins + maxs) / 2
    radius = (maxs - mins).length / 2

    # A ground plane so contact shadows read.
    bpy.ops.mesh.primitive_plane_add(size=radius * 8, location=(center.x, center.y, mins.z - 0.001))
    ground = bpy.context.object
    gmat = bpy.data.materials.get("preview_ground") or bpy.data.materials.new("preview_ground")
    gmat.diffuse_color = (0.3, 0.32, 0.36, 1)
    ground.data.materials.append(gmat)

    cam_data = bpy.data.cameras.new("preview")
    cam_data.lens = 50
    cam = bpy.data.objects.new("preview", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    scene.render.resolution_x = size
    scene.render.resolution_y = size
    stem, ext = os.path.splitext(path)
    for k, (elevation, azimuth) in enumerate(views):
        el, az = math.radians(elevation), math.radians(azimuth)
        # Game camera looks from behind (Unity -z == Blender +y) and above.
        direction = Vector((math.sin(az) * math.cos(el), math.cos(az) * math.cos(el), math.sin(el)))
        cam.location = center + direction * (radius / math.tan(cam_data.angle / 2) * 1.05)
        cam.rotation_euler = (center - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = path if k == 0 else f"{stem}_b{ext}"
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)
    bpy.data.objects.remove(ground)


def build_items(data, opts):
    out = os.path.join(OUT, "Items")
    os.makedirs(out, exist_ok=True)
    built = 0
    for it in data["items"]:
        iid = it["id"]
        if opts["only"] and iid not in opts["only"]:
            continue
        fn = ITEMS.get(iid)
        cells = parse_cells(it["layers"])
        size = Vector((max(c[0] for c in cells) + 1, max(c[1] for c in cells) + 1, max(c[2] for c in cells) + 1))
        palette = {p[0]: p[2:].lstrip("#").upper() for p in it["palette"]}

        ptt_lib.reset_scene()
        m = ptt_lib.Model(it["name"])
        if fn is None:
            print(f"[models] {iid}: no builder, using voxels")
            for x, y, z, ch in cells:
                m.add(ptt_lib.box((x + 0.04, y + 0.02, z + 0.04), (x + 0.96, y + 0.96, z + 0.96), 0.06),
                      ptt_lib.col(palette.get(ch, "FF00FF")))
        else:
            fn(m, palette)
        obj = m.build(origin=tuple(size / 2))
        ptt_lib.export_fbx(os.path.join(out, iid + ".fbx"), [obj])
        if opts["preview"]:
            render_preview(os.path.join(opts["preview"], "items", iid + ".png"), [obj])
        built += 1
    print(f"[models] items built: {built}")


def build_vehicles(data, opts):
    from vehicles import build_vehicle
    out = os.path.join(OUT, "Vehicles")
    os.makedirs(out, exist_ok=True)
    for level in data["levels"]:
        if opts["only"] and level["id"] not in opts["only"]:
            continue
        ptt_lib.reset_scene()
        root = build_vehicle(level)
        ptt_lib.export_fbx(os.path.join(out, level["id"] + ".fbx"), [root])
        if opts["preview"]:
            render_preview(os.path.join(opts["preview"], "vehicles", level["id"] + ".png"), [root], 480)
        print(f"[models] vehicle {level['id']} ({level['style']})")


def build_props(opts):
    from props import PROPS
    out = os.path.join(OUT, "Props")
    os.makedirs(out, exist_ok=True)
    for name, fn in PROPS.items():
        if opts["only"] and name not in opts["only"]:
            continue
        ptt_lib.reset_scene()
        root = fn()
        ptt_lib.export_fbx(os.path.join(out, name + ".fbx"), [root])
        if opts["preview"]:
            render_preview(os.path.join(opts["preview"], "props", name + ".png"), [root], 400)
        print(f"[models] prop {name}")


def main():
    opts = args()
    with open(DATA) as f:
        data = json.load(f)
    if opts["preview"]:
        for sub in ("items", "vehicles", "props"):
            os.makedirs(os.path.join(opts["preview"], sub), exist_ok=True)
    if "items" in opts["sets"]:
        build_items(data, opts)
    if "vehicles" in opts["sets"]:
        build_vehicles(data, opts)
    if "props" in opts["sets"]:
        build_props(opts)


main()
