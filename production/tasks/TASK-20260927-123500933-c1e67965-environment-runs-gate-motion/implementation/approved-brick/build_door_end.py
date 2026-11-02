"""Door_End (D-003): the gate end cap, Door_1x1's cross-section with one end closed and rounded like Brick_End's end
instead of Door_1x1's flat cut. Adds it to ArtSource/BoardKit/WallBrick.blend beside Brick_End and Corner_End, with
Door_1x1's node transform and an unchanged Door_Reference copy.

The supplied WallAndDoor.fbx is only read. Door_End keeps Door_1x1's exact mesh bounds, so Door_1x1.prefab's
calibration fits it. Door_1x1 runs along mesh X with its thickness across Z, the transpose of Wall; the builder swaps
X and Z, reuses build_brick_end's cap on the door's own profile, and swaps back. In the fit space the open mouth
faces +X and the rounded end -X; Unity's FBX import mirrors X, so in Unity the rounded end faces local +X.

Run from the repository root (Blender 4.1+), then export with export_wall_brick.py:
  blender --background --factory-startup ArtSource/BoardKit/WallBrick.blend --python <this file>
"""
import os
import sys

import bmesh
import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "brick-proof"))
sys.path.insert(0, HERE)
import brick_proof as proof  # noqa: E402  (FBX path and axis fit)
import build_brick_end as brick  # noqa: E402  (profile sampling and the rounded cap)

NAME = "Door_End"
REFERENCE = "Door_Reference"


def swap(v):
    return Vector((v.z, v.y, v.x))


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
    door = next(o for o in imported if o.type == "MESH" and o.name.split(".")[0] == "Door_1x1")

    to_unity, from_unity = brick.maps(proof.fit_axes([wall.matrix_world @ v.co for v in wall.data.vertices]))
    door_unity = [to_unity(door.matrix_world @ v.co) for v in door.data.vertices]
    # In the swapped space the door's thickness runs along X and its length along Z, as on Wall.
    swapped = [swap(p) for p in door_unity]
    profile, x_centre, low, high = brick.sample_profile(swapped, [tuple(p.vertices) for p in door.data.polygons])
    soft_radius = profile[-1][0] - brick.split_profile(profile)[0]
    bm, shape = brick.build_cap(profile, x_centre, low, high, soft_radius)
    for vert in bm.verts:
        vert.co = from_unity(swap(vert.co))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])  # the swap mirrors; outward again in Blender world
    to_local = door.matrix_world.inverted()
    for vert in bm.verts:
        vert.co = to_local @ vert.co
    mesh = bpy.data.meshes.new(NAME)
    bm.to_mesh(mesh)
    bm.free()
    mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))
    if hasattr(mesh, "set_sharp_from_angle"):
        mesh.set_sharp_from_angle(angle=proof.SMOOTH_ANGLE)
    mesh.uv_layers.new(name="UVMap")  # BoardLit samples a fixed atlas texel

    end = door.copy()
    end.data, end.name = mesh, NAME
    reference = door.copy()
    reference.data = door.data.copy()
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
        raise SystemExit(f"[door-end] bounds {end_bounds} differ from Door_1x1 {reference_bounds}")
    bpy.ops.wm.save_mainfile()
    print(f"[door-end] {shape}; bounds {end_bounds}; saved {bpy.data.filepath}")


main()
