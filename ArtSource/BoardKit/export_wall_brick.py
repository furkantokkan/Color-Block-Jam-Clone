"""Exports Brick_End, Corner_End, Corner_Join and Door_End from WallBrick.blend to
Assets/ColorBlockJam/Art/Models/BoardKit/WallBrick.fbx (DEC-139).

WallBrick.blend holds Brick_End, the wall-run cap built from the supplied Wall profile inside Wall's exact
mesh bounds, Corner_End, the corner unit with Corner_1's footprint, a large quarter-round outer arc and both arm
ends closed by the same bevel, Corner_Join, the same corner with one arm left open to join a wall, Door_End,
the gate end cap with Door_1x1's cross-section and one rounded end, and Wall_Reference, Corner_Reference and
Door_Reference, unchanged copies of the supplied pieces kept only to check calibration. Only the four pieces are
exported, each with its source piece's node transform (the approved generators build_brick_end.py,
build_corner_end.py, build_corner_join.py and build_door_end.py in
production/tasks/TASK-20260927-123500933-c1e67965-environment-runs-gate-motion/implementation/approved-brick)
and the supplied kit's unit convention: WallAndDoor.fbx stores its units as FBX UnitScaleFactor 100, so
this export writes units to the FBX scale too (FBX_SCALE_UNITS). Unity then reads each piece in its source
piece's mesh space, and Wall.prefab's, Corner_1.prefab's and Door_1x1.prefab's calibrations fit them. The script re-imports
the file and checks the unit scale and that every piece kept its bounds.

Run from the repository root (Blender 4.1+):
  blender --background --factory-startup ArtSource/BoardKit/WallBrick.blend --python ArtSource/BoardKit/export_wall_brick.py
Then run Color Block Jam > Art > Build Wall Brick in Unity.
"""
import os
import struct

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
FBX_PATH = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "ColorBlockJam", "Art", "Models", "BoardKit", "WallBrick.fbx"))
NAMES = ("Brick_End", "Corner_End", "Corner_Join", "Door_End")
TOLERANCE = 1e-4
# WallAndDoor.fbx's UnitScaleFactor; the FBX binary stores it as a little-endian double after its name.
KIT_UNIT_SCALE = 100.0


def local_bounds(obj):
    xs, ys, zs = zip(*(tuple(v.co) for v in obj.data.vertices))
    return (min(xs), min(ys), min(zs), max(xs), max(ys), max(zs))


def unit_scale_factor(path):
    data = open(path, "rb").read()
    key = b"UnitScaleFactorS\x06\x00\x00\x00doubleS\x06\x00\x00\x00NumberS\x00\x00\x00\x00D"
    start = data.find(key)
    if start < 0:
        raise SystemExit(f"[wall-brick] no UnitScaleFactor in {path}")
    return struct.unpack("<d", data[start + len(key):start + len(key) + 8])[0]


def main():
    pieces = [bpy.data.objects[name] for name in NAMES]
    expected = {piece.name: local_bounds(piece) for piece in pieces}
    bpy.ops.object.select_all(action="DESELECT")
    for piece in pieces:
        piece.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    bpy.ops.export_scene.fbx(filepath=FBX_PATH, use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=False,
                             mesh_smooth_type="FACE", add_leaf_bones=False, bake_anim=False)
    if abs(unit_scale_factor(FBX_PATH) - KIT_UNIT_SCALE) > TOLERANCE:
        raise SystemExit(f"[wall-brick] UnitScaleFactor {unit_scale_factor(FBX_PATH)} differs from the kit's {KIT_UNIT_SCALE}")

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=FBX_PATH)
    meshes = {obj.name.split(".")[0]: obj for obj in bpy.context.scene.objects if obj.type == "MESH"}
    if sorted(meshes) != sorted(NAMES):
        raise SystemExit(f"[wall-brick] expected only {NAMES} in {FBX_PATH}, found {sorted(meshes)}")
    for name in NAMES:
        actual = local_bounds(meshes[name])
        if max(abs(a - b) for a, b in zip(expected[name], actual)) > TOLERANCE:
            raise SystemExit(f"[wall-brick] round trip changed {name}'s bounds: {expected[name]} -> {actual}")
        print(f"[wall-brick] exported {name} to {FBX_PATH}; bounds {tuple(round(v, 5) for v in actual)}")


main()
