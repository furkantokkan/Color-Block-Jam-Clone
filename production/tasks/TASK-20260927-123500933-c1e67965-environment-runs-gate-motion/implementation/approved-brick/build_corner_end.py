"""Corner_End (D-001): the play area's corner unit, a Corner_1 footprint whose two arm ends are closed and
rounded like Brick_End's end instead of Corner_1's flat cut. Adds it to ArtSource/BoardKit/WallBrick.blend
beside Brick_End, with Corner_1's node transform and an unchanged Corner_Reference copy.

The supplied WallAndDoor.fbx is only read. Corner_End keeps Corner_1's exact mesh bounds and orientation, so
Corner_1.prefab's calibration fits it. Its outer side is one large quarter-round arc, as in the reference crop
(D-004): the arc's radius is the piece's side minus the soft end radius, so the arc runs tangentially into the
rounded arm ends. Every side drops with the supplied Wall bevel, as Brick_End's closed end does: each bevel level
is the base outline inset by that level's bevel distance, and a corner's plan radius shrinks by the same inset,
so its bevel is a swept arc.

Run from the repository root (Blender 4.1+), then export with export_wall_brick.py:
  blender --background --factory-startup ArtSource/BoardKit/WallBrick.blend --python <this file>
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "brick-proof"))
sys.path.insert(0, HERE)
import brick_proof as proof  # noqa: E402  (FBX path and axis fit)
import build_brick_end as brick  # noqa: E402  (Wall profile sampling and the Blender <-> Unity maps)

NAME = "Corner_End"
REFERENCE = "Corner_Reference"
CORNER_SEGMENTS = 16
# Keeps the outer arc and its neighbouring end corners from meeting in a zero-length edge.
ARC_CLEARANCE = 1e-4


def outer_corner(kit_corner, low, high):
    """Corner_1's two beveled (non-mouth) sides give its rounded outer corner, as (sign x, sign z)."""
    roofs = {}
    for axis, key in ((0, "x"), (2, "z")):
        for sign, edge in ((1, high[axis]), (-1, low[axis])):
            near = [p.y for p in kit_corner if abs(p[axis] - edge) < 0.01]
            roofs[(key, sign)] = min(near)
    roof = min(roofs.values())
    # A mouth reaches the flat roof at its bound; a beveled side stays lower.
    sign_x = 1 if roofs[("x", 1)] > roof + 0.05 else -1
    sign_z = 1 if roofs[("z", 1)] > roof + 0.05 else -1
    return sign_x, sign_z


def rounded_rectangle(bm, x0, x1, z0, z1, radii, y):
    """Outline points counter-clockwise from the (+x, -z) corner; radii maps (sign x, sign z) to a radius."""
    points = []
    for (sx, sz), start in (((1, -1), -0.5 * math.pi), ((1, 1), 0.0), ((-1, 1), 0.5 * math.pi), ((-1, -1), math.pi)):
        radius = radii[(sx, sz)]
        cx = (x1 if sx > 0 else x0) - sx * radius
        cz = (z1 if sz > 0 else z0) - sz * radius
        for k in range(CORNER_SEGMENTS + 1):
            angle = start + 0.5 * math.pi * k / CORNER_SEGMENTS
            points.append((cx + radius * math.cos(angle), cz + radius * math.sin(angle)))
    return [bm.verts.new((x, y, z)) for x, z in points]


def build(profile, low, high, outer, outer_radius, soft_radius):
    """Level 0 is the flat roof's edge; each later level follows Wall's bevel down to the base outline."""
    half = profile[-1][0]
    flat_half, roof, bevel = brick.split_profile(profile)
    levels = [(flat_half, roof)] + bevel[:-1] + [(half, high.y)]  # the base sits at Corner_1's own base
    bm = bmesh.new()
    rows = []
    for r, y in levels:
        inset = half - r
        radii = {}
        for key in ((1, -1), (1, 1), (-1, 1), (-1, -1)):
            full = outer_radius if key == outer else soft_radius
            radii[key] = max(full - inset, 0.0)
        rows.append(rounded_rectangle(bm, low.x + inset, high.x - inset, low.z + inset, high.z - inset, radii, y))
    bm.faces.new(rows[0])  # the flat roof
    for upper, lower in zip(rows, rows[1:]):
        count = len(upper)
        for j in range(count):
            k = (j + 1) % count
            bm.faces.new((upper[j], upper[k], lower[k], lower[j]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=bm.edges[:])
    return bm, {"flat_half_width": round(flat_half, 5), "bevel_width": round(half - flat_half, 5),
                "outer_corner": outer, "outer_radius": round(outer_radius, 5), "soft_radius": round(soft_radius, 5),
                "roof_y": round(roof, 5), "base_y": round(high.y, 5)}


def main():
    keep = {obj.name for obj in bpy.context.scene.objects}
    for name in (NAME, REFERENCE):
        if name in bpy.data.objects:
            bpy.data.objects.remove(bpy.data.objects[name], do_unlink=True)
            keep.discard(name)
    bpy.ops.import_scene.fbx(filepath=proof.FBX_PATH)
    imported = [obj for obj in bpy.context.scene.objects if obj.name not in keep]
    # The FBX node names identify the pieces; their mesh data names are generic (Scene.NNN).
    wall = next(o for o in imported if o.type == "MESH" and o.name.split(".")[0] == "Wall")
    corner = next(o for o in imported if o.type == "MESH" and o.name.split(".")[0] == "Corner_1")

    wall_world = [wall.matrix_world @ v.co for v in wall.data.vertices]
    to_unity, from_unity = brick.maps(proof.fit_axes(wall_world))
    profile, _, _, _ = brick.sample_profile([to_unity(p) for p in wall_world],
                                            [tuple(p.vertices) for p in wall.data.polygons])
    corner_unity = [to_unity(corner.matrix_world @ v.co) for v in corner.data.vertices]
    low, high = proof.bounds(corner_unity)
    outer = outer_corner(corner_unity, low, high)
    soft_radius = profile[-1][0] - brick.split_profile(profile)[0]  # Brick_End's crease-free end corner
    # The largest arc that still leaves each neighbouring end corner its soft radius on every bevel level.
    outer_radius = min(high.x - low.x, high.z - low.z) - soft_radius - ARC_CLEARANCE

    bm, shape = build(profile, low, high, outer, outer_radius, soft_radius)
    for vert in bm.verts:
        vert.co = from_unity(vert.co)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])  # outward in Blender world, as on the supplied meshes
    to_local = corner.matrix_world.inverted()
    for vert in bm.verts:
        vert.co = to_local @ vert.co
    mesh = bpy.data.meshes.new(NAME)
    bm.to_mesh(mesh)
    bm.free()
    mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))
    if hasattr(mesh, "set_sharp_from_angle"):
        mesh.set_sharp_from_angle(angle=proof.SMOOTH_ANGLE)
    mesh.uv_layers.new(name="UVMap")  # BoardLit samples a fixed atlas texel

    end = corner.copy()
    end.data, end.name = mesh, NAME
    reference = corner.copy()
    reference.data = corner.data.copy()
    reference.name = reference.data.name = REFERENCE
    for obj in (end, reference):
        bpy.context.scene.collection.objects.link(obj)
        brick.unparent(obj)
    for obj in imported:
        bpy.data.objects.remove(obj, do_unlink=True)
    for data in list(bpy.data.meshes):
        if data.users == 0:
            bpy.data.meshes.remove(data)

    end_bounds = brick.local_bounds(end)
    reference_bounds = brick.local_bounds(reference)
    if max(abs(a - b) for a, b in zip(end_bounds, reference_bounds)) > 1e-4:
        raise SystemExit(f"[corner-end] bounds {end_bounds} differ from Corner_1 {reference_bounds}")
    bpy.ops.wm.save_mainfile()
    print(f"[corner-end] {shape}; bounds {end_bounds}; saved {bpy.data.filepath}")


main()
