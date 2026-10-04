"""Modeling helpers for Pack The Trunk.

Everything here is authored in *Unity* coordinates (x right, y up, z forward/"into the car")
and converted to Blender space only when an object is created. The FBX exporter settings in
`export_fbx` map Blender (x, y, z) -> Unity (-x, z, -y); `UNITY_TO_BLENDER` is its inverse.

Material names carry the colour so Unity can rebuild them at runtime:
    col_RRGGBB    plain painted / plastic
    metal_RRGGBB  metallic
    glass_RRGGBB  glossy glass
    glow_RRGGBB   emissive
"""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector

UNITY_TO_BLENDER = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))

AXIS_ROT = {
    "x": Matrix.Rotation(math.pi / 2, 4, "Y"),   # local +z -> +x
    "y": Matrix.Rotation(-math.pi / 2, 4, "X"),  # local +z -> +y
    "z": Matrix.Identity(4),
}


def V(*a):
    if len(a) == 1:
        return Vector(a[0])
    return Vector(a)


# ----------------------------------------------------------------------------- materials

def col(hexstr):
    return "col_" + hexstr.lstrip("#").upper()


def metal(hexstr):
    return "metal_" + hexstr.lstrip("#").upper()


def glass(hexstr):
    return "glass_" + hexstr.lstrip("#").upper()


def glow(hexstr):
    return "glow_" + hexstr.lstrip("#").upper()


def shade(hexstr, factor):
    """Lighten (factor > 1) or darken (factor < 1) a hex colour."""
    h = hexstr.lstrip("#")
    rgb = [int(h[i:i + 2], 16) for i in (0, 2, 4)]
    if factor >= 1:
        rgb = [int(c + (255 - c) * (factor - 1)) for c in rgb]
    else:
        rgb = [int(c * factor) for c in rgb]
    return "".join(f"{max(0, min(255, c)):02X}" for c in rgb)


def _srgb_to_linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def get_material(name):
    mat = bpy.data.materials.get(name)
    if mat:
        return mat
    mat = bpy.data.materials.new(name)
    kind, hexstr = name.split("_", 1)
    rgb = [_srgb_to_linear(int(hexstr[i:i + 2], 16) / 255) for i in (0, 2, 4)]
    roughness = {"metal": 0.28, "glass": 0.06, "glow": 0.5}.get(kind, 0.55)
    mat.diffuse_color = (*rgb, 1.0)
    mat.metallic = 0.9 if kind == "metal" else 0.0
    mat.roughness = roughness
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (*rgb, 1.0)
        bsdf.inputs["Metallic"].default_value = mat.metallic
        bsdf.inputs["Roughness"].default_value = roughness
        if kind == "glow":
            bsdf.inputs["Emission Color"].default_value = (*rgb, 1.0)
            bsdf.inputs["Emission Strength"].default_value = 3.0
        if kind == "glass":
            bsdf.inputs["Coat Weight"].default_value = 0.6
    return mat


# ----------------------------------------------------------------------------- primitives
# Every primitive returns a fresh bmesh in Unity space; Model.add() merges it.

def _mark_caps_flat(bm, axis_vec):
    for f in bm.faces:
        f.smooth = abs(f.normal.normalized().dot(axis_vec)) < 0.9
    for e in bm.edges:
        if len(e.link_faces) == 2 and e.link_faces[0].smooth != e.link_faces[1].smooth:
            e.smooth = False


def box(mn, mx, bevel=0.0, segments=2):
    mn, mx = V(mn), V(mx)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    size = mx - mn
    bmesh.ops.scale(bm, vec=size, verts=bm.verts)
    bmesh.ops.translate(bm, vec=(mn + mx) / 2, verts=bm.verts)
    if bevel > 0:
        b = min(bevel, min(size) * 0.49)
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=b, offset_type="OFFSET", segments=segments,
                        profile=0.5, affect="EDGES", clamp_overlap=True)
    bm.normal_update()
    return bm, ("auto" if bevel > 0 and segments >= 2 else False)


def cyl(center, radius, length, axis="y", radius2=None, segments=20, bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments,
                          radius1=radius, radius2=radius if radius2 is None else radius2, depth=length)
    if bevel > 0:
        bm.normal_update()
        rim = [e for e in bm.edges if len(e.link_faces) == 2 and
               (abs(e.link_faces[0].normal.z) > 0.9) != (abs(e.link_faces[1].normal.z) > 0.9)]
        bmesh.ops.bevel(bm, geom=rim, offset=bevel, offset_type="OFFSET", segments=2, profile=0.5,
                        affect="EDGES", clamp_overlap=True)
    bm.transform(Matrix.Translation(V(center)) @ AXIS_ROT[axis])
    bm.normal_update()
    _mark_caps_flat(bm, {"x": V(1, 0, 0), "y": V(0, 1, 0), "z": V(0, 0, 1)}[axis])
    return bm, None


def rod(p0, p1, radius, segments=12, radius2=None):
    """Cylinder from p0 to p1."""
    p0, p1 = V(p0), V(p1)
    d = p1 - p0
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segments, radius1=radius,
                          radius2=radius if radius2 is None else radius2, depth=d.length)
    rot = V(0, 0, 1).rotation_difference(d.normalized()).to_matrix().to_4x4()
    bm.transform(Matrix.Translation((p0 + p1) / 2) @ rot)
    bm.normal_update()
    _mark_caps_flat(bm, d.normalized())
    return bm, None


def sphere(center, radius, scale=(1, 1, 1), segments=20, rings=12):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segments, v_segments=rings, radius=radius)
    bm.transform(Matrix.Translation(V(center)) @ Matrix.Diagonal(V(*scale, 1.0)))
    bm.normal_update()
    return bm, True


def ellipsoid(center, radii, segments=20, rings=12):
    return sphere(center, 1.0, radii, segments, rings)


def ico(center, radius, subdiv=2):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=radius)
    bmesh.ops.translate(bm, vec=V(center), verts=bm.verts)
    bm.normal_update()
    return bm, False


def torus(center, major, minor, axis="y", segments=24, ring_segments=10, arc=1.0, scale=(1, 1, 1)):
    """Torus lying in the plane perpendicular to `axis`. `arc` < 1 gives an open arc."""
    bm = bmesh.new()
    rings = []
    count = segments if arc >= 1.0 else segments + 1
    for i in range(count):
        a = 2 * math.pi * arc * i / segments
        ring = []
        for j in range(ring_segments):
            b = 2 * math.pi * j / ring_segments
            r = major + minor * math.cos(b)
            ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), minor * math.sin(b))))
        rings.append(ring)
    for i in range(len(rings) - (0 if arc >= 1.0 else 1)):
        r0, r1 = rings[i], rings[(i + 1) % len(rings)]
        for j in range(ring_segments):
            bm.faces.new((r0[j], r1[j], r1[(j + 1) % ring_segments], r0[(j + 1) % ring_segments]))
    if arc < 1.0:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bm.transform(Matrix.Translation(V(center)) @ AXIS_ROT[axis] @ Matrix.Diagonal(V(*scale, 1.0)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    return bm, True


def lathe(profile, center, axis="y", segments=20, smooth=True):
    """Revolve [(radius, height), ...] (bottom to top) around `axis`. Ends are capped."""
    bm = bmesh.new()
    rings = []
    for r, h in profile:
        ring = []
        for i in range(segments):
            a = 2 * math.pi * i / segments
            ring.append(bm.verts.new((r * math.cos(a), r * math.sin(a), h)))
        rings.append(ring)
    for k in range(len(rings) - 1):
        for i in range(segments):
            j = (i + 1) % segments
            bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bm.transform(Matrix.Translation(V(center)) @ AXIS_ROT[axis])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    if smooth:
        _mark_caps_flat(bm, {"x": V(1, 0, 0), "y": V(0, 1, 0), "z": V(0, 0, 1)}[axis])
    return bm, None


def sweep(points, radius, closed=False, segments=12, radius_end=None, caps=True):
    """Tube along a polyline (Unity space). Frames are parallel-transported so it never twists.
    `radius_end` tapers linearly from `radius`."""
    pts = [V(p) for p in points]
    n = len(pts)
    tangents = []
    for i in range(n):
        if closed:
            t = (pts[(i + 1) % n] - pts[i - 1])
        elif i == 0:
            t = pts[1] - pts[0]
        elif i == n - 1:
            t = pts[-1] - pts[-2]
        else:
            t = (pts[i + 1] - pts[i]).normalized() + (pts[i] - pts[i - 1]).normalized()
        tangents.append(t.normalized())
    ref = V(0, 1, 0) if abs(tangents[0].y) < 0.9 else V(1, 0, 0)
    normal = (ref - tangents[0] * ref.dot(tangents[0])).normalized()
    bm = bmesh.new()
    rings = []
    for i in range(n):
        if i > 0:
            rot = tangents[i - 1].rotation_difference(tangents[i])
            normal = (rot @ normal).normalized()
        binormal = tangents[i].cross(normal)
        r = radius if radius_end is None else radius + (radius_end - radius) * i / max(1, n - 1)
        ring = []
        for k in range(segments):
            a = 2 * math.pi * k / segments
            ring.append(bm.verts.new(pts[i] + (normal * math.cos(a) + binormal * math.sin(a)) * r))
        rings.append(ring)
    count = n if closed else n - 1
    for i in range(count):
        r0, r1 = rings[i], rings[(i + 1) % n]
        for k in range(segments):
            bm.faces.new((r0[k], r0[(k + 1) % segments], r1[(k + 1) % segments], r1[k]))
    if not closed and caps:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    for f in bm.faces:
        f.smooth = len(f.verts) == 4
    return bm, None


def arc_points(center, radius, start_deg, end_deg, plane="xy", steps=12):
    """Points on a circular arc; plane names the two axes the arc spans."""
    c = V(center)
    out = []
    for i in range(steps + 1):
        a = math.radians(start_deg + (end_deg - start_deg) * i / steps)
        u, v = math.cos(a) * radius, math.sin(a) * radius
        d = {"xy": V(u, v, 0), "xz": V(u, 0, v), "zy": V(0, v, u)}[plane]
        out.append(c + d)
    return out


def rounded_rect_points(center, half_x, half_z, corner, steps=6):
    """Closed rounded rectangle in the xz plane (for rings and rims)."""
    c = V(center)
    pts = []
    for cx, cz, start in ((half_x - corner, half_z - corner, 0), (-(half_x - corner), half_z - corner, 90),
                          (-(half_x - corner), -(half_z - corner), 180), (half_x - corner, -(half_z - corner), 270)):
        for i in range(steps + 1):
            a = math.radians(start + 90 * i / steps)
            pts.append(c + V(cx + math.cos(a) * corner, 0, cz + math.sin(a) * corner))
    return pts


def shell(profile, center, axis="y", thickness=0.03, segments=24):
    """Hollow surface of revolution: `profile` [(radius, height)...] is the outer wall, the inner
    wall is offset inward by `thickness`, and the rim at the last point is closed. Open at the top."""
    loop = list(profile) + [(max(0.001, r - thickness), h) for r, h in reversed(profile)]
    bm = bmesh.new()
    rings = []
    for r, h in loop:
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / segments), r * math.sin(2 * math.pi * i / segments), h))
                      for i in range(segments)])
    for k in range(len(rings) - 1):
        for i in range(segments):
            j = (i + 1) % segments
            bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))
    bm.transform(Matrix.Translation(V(center)) @ AXIS_ROT[axis])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    for f in bm.faces:
        f.smooth = len(f.verts) == 4
    return bm, None


def transformed(prim, matrix):
    """Apply a matrix to a primitive before adding it (e.g. to tilt a lid)."""
    bm, smooth = prim
    bm.transform(matrix)
    bm.normal_update()
    return bm, smooth


def rotate_about(point, axis, degrees):
    p = V(point)
    return Matrix.Translation(p) @ Matrix.Rotation(math.radians(degrees), 4, axis) @ Matrix.Translation(-p)


def rounded_section(cx, y0, y1, half_bottom, half_top, r_top, r_bottom, z, steps=6):
    """Closed rounded-rectangle (optionally trapezoid) outline in the xy plane at depth z."""
    height = y1 - y0
    r_top = max(0.005, min(r_top, height * 0.48, half_top * 0.95))
    r_bottom = max(0.005, min(r_bottom, height * 0.48, half_bottom * 0.95))
    corners = [(cx + half_bottom - r_bottom, y0 + r_bottom, r_bottom, -90, 0),
               (cx + half_top - r_top, y1 - r_top, r_top, 0, 90),
               (cx - half_top + r_top, y1 - r_top, r_top, 90, 180),
               (cx - half_bottom + r_bottom, y0 + r_bottom, r_bottom, 180, 270)]
    pts = []
    for ccx, ccy, r, a0, a1 in corners:
        for k in range(steps + 1):
            a = math.radians(a0 + (a1 - a0) * k / steps)
            pts.append((ccx + r * math.cos(a), ccy + r * math.sin(a), z))
    return pts


def loft(sections, cap=True):
    """Skin a list of equal-length closed outlines (ordered along +z) into a closed solid."""
    bm = bmesh.new()
    rings = [[bm.verts.new(V(p)) for p in sec] for sec in sections]
    n = len(rings[0])
    for i in range(len(rings) - 1):
        a, b = rings[i], rings[i + 1]
        for k in range(n):
            bm.faces.new((a[k], a[(k + 1) % n], b[(k + 1) % n], b[k]))
    if cap:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.normal_update()
    return bm, "auto"


def boolean_difference(prim, cutters):
    """Subtract cutter primitives from a solid using Blender's exact boolean solver."""
    scene = bpy.context.scene
    bm, smooth = prim
    base_mesh = bpy.data.meshes.new("_bool_base")
    bm.to_mesh(base_mesh)
    bm.free()
    base = bpy.data.objects.new("_bool_base", base_mesh)
    scene.collection.objects.link(base)
    temps = []
    for i, cutter in enumerate(cutters):
        cbm, _ = cutter
        cmesh = bpy.data.meshes.new(f"_bool_cut{i}")
        cbm.to_mesh(cmesh)
        cbm.free()
        cobj = bpy.data.objects.new(f"_bool_cut{i}", cmesh)
        scene.collection.objects.link(cobj)
        mod = base.modifiers.new(f"cut{i}", "BOOLEAN")
        mod.operation = "DIFFERENCE"
        mod.solver = "EXACT"
        mod.object = cobj
        temps.append(cobj)
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = base.evaluated_get(depsgraph)
    result = bmesh.new()
    result.from_mesh(evaluated.to_mesh())
    evaluated.to_mesh_clear()
    for obj in temps + [base]:
        mesh = obj.data
        bpy.data.objects.remove(obj)
        bpy.data.meshes.remove(mesh)
    bmesh.ops.remove_doubles(result, verts=result.verts, dist=1e-5)
    result.normal_update()
    return result, smooth


def hull(points, bevel=0.0, segments=2):
    """Convex hull of the given points (good for wedges, cabins, fins)."""
    bm = bmesh.new()
    for p in points:
        bm.verts.new(V(p))
    res = bmesh.ops.convex_hull(bm, input=bm.verts)
    leftovers = list({g for g in res["geom_interior"] + res["geom_unused"] if isinstance(g, bmesh.types.BMVert)})
    if leftovers:
        bmesh.ops.delete(bm, geom=leftovers, context="VERTS")
    bmesh.ops.dissolve_limit(bm, angle_limit=0.01, verts=bm.verts, edges=bm.edges)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, offset_type="OFFSET", segments=segments,
                        profile=0.5, affect="EDGES", clamp_overlap=True)
    bm.normal_update()
    return bm, ("auto" if bevel > 0 and segments >= 2 else False)


def prism(points2d, z0, z1, plane="xy", bevel=0.0):
    """Extrude a convex 2D outline. plane 'xy' extrudes along z; 'zy' along x; 'xz' along y."""
    pts = []
    for a, b in points2d:
        for depth in (z0, z1):
            if plane == "xy":
                pts.append((a, b, depth))
            elif plane == "zy":
                pts.append((depth, b, a))
            else:
                pts.append((a, depth, b))
    return hull(pts, bevel)


# ----------------------------------------------------------------------------- model

AUTO_SMOOTH_DEGREES = 50.0


def _sharpen(bm, degrees):
    """Mark edges sharper than `degrees` so smooth shading keeps hard corners."""
    limit = math.radians(degrees)
    bm.normal_update()
    for e in bm.edges:
        if len(e.link_faces) != 2:
            e.smooth = False
            continue
        e.smooth = e.calc_face_angle(math.pi) < limit


class Model:
    """Accumulates primitives (each with one material) into a single mesh object."""

    def __init__(self, name):
        self.name = name
        self.bm = bmesh.new()
        self.materials = []

    def add(self, prim, material, smooth=None, transform=None):
        part, default_smooth = prim
        if transform is not None:
            part.transform(transform)
            part.normal_update()
        if material not in self.materials:
            self.materials.append(material)
        index = self.materials.index(material)
        flag = default_smooth if smooth is None else smooth
        for f in part.faces:
            f.material_index = index
            if flag is not None:
                f.smooth = flag is True or flag == "auto"
        if flag == "auto":
            _sharpen(part, AUTO_SMOOTH_DEGREES)
        mesh = bpy.data.meshes.new("_part")
        part.to_mesh(mesh)
        part.free()
        self.bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)
        return self

    def add_faces_by(self, prim, pick):
        """Like add(), but `pick(face_center, face_normal) -> material` chooses per face."""
        part, default_smooth = prim
        mats = {}
        for f in part.faces:
            m = pick(f.calc_center_median(), f.normal)
            if m not in self.materials:
                self.materials.append(m)
            f.material_index = self.materials.index(m)
            if default_smooth is not None:
                f.smooth = default_smooth
        mesh = bpy.data.meshes.new("_part")
        part.to_mesh(mesh)
        part.free()
        self.bm.from_mesh(mesh)
        bpy.data.meshes.remove(mesh)
        return self

    def build(self, origin=(0, 0, 0), parent=None, parent_origin=(0, 0, 0)):
        """Create the Blender object. Vertices are made relative to `origin` (Unity space)."""
        bm = self.bm
        bmesh.ops.translate(bm, vec=-V(origin), verts=bm.verts)
        bm.transform(UNITY_TO_BLENDER)
        bmesh.ops.reverse_faces(bm, faces=bm.faces)  # the axis swap mirrors, so flip winding
        bm.normal_update()
        mesh = bpy.data.meshes.new(self.name)
        bm.to_mesh(mesh)
        bm.free()
        for m in self.materials:
            mesh.materials.append(get_material(m))
        obj = bpy.data.objects.new(self.name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        obj.location = (UNITY_TO_BLENDER @ (V(origin) - V(parent_origin)).to_4d()).to_3d()
        if parent is not None:
            obj.parent = parent
        return obj


def empty(name, origin=(0, 0, 0), parent=None, parent_origin=(0, 0, 0)):
    obj = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = (UNITY_TO_BLENDER @ (V(origin) - V(parent_origin)).to_4d()).to_3d()
    obj.parent = parent
    return obj


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.objects):
        for block in list(coll):
            coll.remove(block)


def export_fbx(path, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
        for c in o.children_recursive:
            c.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z", axis_up="Y", bake_space_transform=True, object_types={"MESH", "EMPTY"},
        mesh_smooth_type="OFF", use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
        path_mode="STRIP")
