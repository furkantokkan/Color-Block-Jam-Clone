"""Corner_Join (V9): the corner that merges into one neighbouring wall run. Corner_1's footprint with Corner_End's large
quarter-round outer arc, one arm end left open with the supplied Wall cross-section so it joins a Wall seamlessly, and
the other arm end closed and rounded like Brick_End's end. Adds it to ArtSource/BoardKit/WallBrick.blend beside the
other pieces, with Corner_1's node transform.

In the fit space the open arm is +X (Corner_1's two arms are +X and +Z, its outer corner -X/-Z); Unity's FBX import
mirrors X, so in Unity the open arm faces local -X and the rounded arm +Z. The composer mirrors the piece by a negative
X scale when the other arm must join.

Run from the repository root (Blender 4.1+), then export with export_wall_brick.py:
  blender --background --factory-startup ArtSource/BoardKit/WallBrick.blend --python <this file>
"""
import math
import os
import sys

import bmesh
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "brick-proof"))
sys.path.insert(0, HERE)
import brick_proof as proof  # noqa: E402  (FBX path and axis fit)
import build_brick_end as brick  # noqa: E402  (Wall profile sampling and the Blender <-> Unity maps)

NAME = "Corner_Join"
SEGMENTS = 16
ARC_CLEARANCE = 1e-4


def arc(cx, cz, radius, start, end, y):
    """Points from angle start to end (radians) around (cx, cz); a zero radius gives repeated points."""
    return [(cx + radius * math.cos(start + (end - start) * k / SEGMENTS),
             y, cz + radius * math.sin(start + (end - start) * k / SEGMENTS)) for k in range(SEGMENTS + 1)]


def outline(low, high, inset, outer_radius, soft_radius, y):
    """Open outline at one bevel level, from the mouth's -Z end around the closed sides to its +Z end. The mouth (+X)
    is not inset, so the joined arm keeps Wall's full cross-section; the other sides are inset by the bevel."""
    x0, x1 = low.x + inset, high.x
    z0, z1 = low.z + inset, high.z - inset
    r_out = max(outer_radius - inset, 0.0)
    r_soft = max(soft_radius - inset, 0.0)
    points = [(x1, y, z0)]
    points += arc(x0 + r_out, z0 + r_out, r_out, -0.5 * math.pi, -math.pi, y)   # outer arc, -Z side to -X side
    points += arc(x0 + r_soft, z1 - r_soft, r_soft, math.pi, 0.5 * math.pi, y)  # rounded arm end corner
    points.append((x1, y, z1))
    return points


def main():
    keep = {obj.name for obj in bpy.context.scene.objects}
    if NAME in bpy.data.objects:
        bpy.data.objects.remove(bpy.data.objects[NAME], do_unlink=True)
        keep.discard(NAME)
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
    half = profile[-1][0]
    flat_half, roof, bevel = brick.split_profile(profile)
    soft_radius = half - flat_half
    outer_radius = min(high.x - low.x, high.z - low.z) - soft_radius - ARC_CLEARANCE
    levels = [(flat_half, roof)] + bevel[:-1] + [(half, high.y)]  # the base sits at Corner_1's own base

    bm = bmesh.new()
    rows = [[bm.verts.new(p) for p in outline(low, high, half - r, outer_radius, soft_radius, y)] for r, y in levels]
    bm.faces.new(rows[0])  # the flat roof, closed along the open mouth
    for upper, lower in zip(rows, rows[1:]):
        for j in range(len(upper) - 1):
            bm.faces.new((upper[j], upper[j + 1], lower[j + 1], lower[j]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=bm.edges[:])
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

    join = corner.copy()
    join.data, join.name = mesh, NAME
    bpy.context.scene.collection.objects.link(join)
    brick.unparent(join)
    reference = bpy.data.objects.get("Corner_Reference")
    for obj in imported:
        bpy.data.objects.remove(obj, do_unlink=True)
    for data in list(bpy.data.meshes):
        if data.users == 0:
            bpy.data.meshes.remove(data)

    bounds = brick.local_bounds(join)
    if reference is not None:
        expected = brick.local_bounds(reference)
        if max(abs(a - b) for a, b in zip(bounds, expected)) > 1e-4:
            raise SystemExit(f"[corner-join] bounds {bounds} differ from Corner_1 {expected}")
    bpy.ops.wm.save_mainfile()
    print(f"[corner-join] outer_radius {outer_radius:.5f} soft_radius {soft_radius:.5f}; bounds {bounds}; saved {bpy.data.filepath}")


main()
