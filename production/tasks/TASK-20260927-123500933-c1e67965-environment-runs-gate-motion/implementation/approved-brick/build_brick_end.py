"""Approved Brick_End: builds the rounded brick cap from the supplied Wall profile, saves it as a
reusable .blend and .fbx beside an unchanged Wall reference, and renders 1-, 2- and 3-cell bricks plus
a board whose straight runs are each one brick, split at corners and gates.

The supplied WallAndDoor.fbx is only read. Brick_End occupies the Wall's exact bounds in Wall's own
mesh space (closed roof toward -Y, length along Z), so its prefab can reuse the Wall calibration of
BoardKitAssets.cs. One end is Wall's flat mouth with Wall's exact cross-section. The other end is a
rectangular brick end (user-1.png): Wall's flat roof continues to the end, which drops with the same
side bevel as the long faces, and only the two plan corners are softened, each by sweeping the bevel
around a small radius (by default the bevel width, so the corner has no crease).

Run (Blender 4.1+):
  blender --background --factory-startup --python build_brick_end.py -- [--out DIR] [--corner-radius R]
"""
import argparse
import json
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.join(HERE, "..", "brick-proof"))
import brick_proof as proof  # noqa: E402  (shared FBX mapping, calibration and render helpers)

PROFILE_SAMPLES = 32
CORNER_SEGMENTS = 8
# Roof samples within this height of the centre belong to the flat top (D-001: flat to about +-0.24).
FLAT_TOLERANCE = 0.002


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default=os.path.join(HERE, "out"))
    # Plan radius of the softened end corners at the base, in Wall mesh units (width 1). Default: the bevel width.
    parser.add_argument("--corner-radius", type=float, default=None)
    return parser.parse_args(argv)


def maps(fit):
    """Blender world <-> Unity mesh space for the axis mapping brick_proof.fit_axes chose."""
    _, perm, signs, scale, _ = fit

    def to_unity(v):
        return Vector([signs[i] * v[perm[i]] * scale for i in range(3)])

    def from_unity(u):
        w = [0.0, 0.0, 0.0]
        for i in range(3):
            w[perm[i]] = u[i] / (signs[i] * scale)
        return Vector(w)

    return to_unity, from_unity


def local_bounds(obj):
    low, high = proof.bounds([v.co for v in obj.data.vertices])
    return [round(c, 5) for c in (*low, *high)]


# ---------------------------------------------------------------- the cap, in Unity mesh space

def sample_profile(unity, polygons):
    """Wall's roof height across its width at mid-length, averaged over both sides, centre to base."""
    tree = BVHTree.FromPolygons(unity, polygons)
    low, high = proof.bounds(unity)
    x_centre, z_mid = (low.x + high.x) * 0.5, (low.z + high.z) * 0.5
    half = (high.x - low.x) * 0.5
    profile = []
    for k in range(PROFILE_SAMPLES + 1):
        r = half * math.sin(0.5 * math.pi * k / PROFILE_SAMPLES)  # denser toward the rounded side
        heights = []
        for x in (x_centre + r, x_centre - r):
            hit = tree.ray_cast(Vector((x, low.y - 1.0, z_mid)), Vector((0.0, 1.0, 0.0)))
            if hit[0] is not None:
                heights.append(hit[0].y)
        if heights:
            profile.append((r, sum(heights) / len(heights)))
    if profile[-1][0] < half - 1e-5 or profile[-1][1] < high.y - 1e-4:
        profile.append((half, high.y))  # the open underside sits at the base, as on Wall
    return profile, x_centre, low, high


def split_profile(profile):
    """The flat roof's half width and the bevel samples outside it, from the flat edge to the base."""
    roof = profile[0][1]
    flat = [r for r, y in profile if abs(y - roof) <= FLAT_TOLERANCE]
    flat_half = max(flat)
    bevel = [(r, y) for r, y in profile if r > flat_half]
    return flat_half, roof, bevel


def build_cap(profile, x_centre, low, high, corner_radius):
    """A rectangular brick end: the mouth at the +Z bound keeps Wall's cross-section, the roof stays flat
    to the end, and the sides and the end drop with Wall's bevel. Each bevel level is a contour inset
    from the base outline by that level's bevel distance, so the end matches the long faces; a corner
    softened by `corner_radius` at the base shrinks by the same inset, so its bevel is a swept arc.
    """
    bm = bmesh.new()
    half = profile[-1][0]
    flat_half, roof, bevel = split_profile(profile)
    levels = [(flat_half, roof)] + bevel  # the flat roof's edge, then down the bevel to the base

    def contour(r, y):
        inset = half - r
        radius = max(corner_radius - inset, 0.0)
        z_end = low.z + inset
        points = [(x_centre + r, high.z)]
        # Right end corner, from the long side (angle 0) to the end face (angle -90 degrees), then left.
        for centre_x, start in ((x_centre + r - radius, 0.0), (x_centre - r + radius, -0.5 * math.pi)):
            for k in range(CORNER_SEGMENTS + 1):
                angle = start - 0.5 * math.pi * k / CORNER_SEGMENTS
                points.append((centre_x + radius * math.cos(angle), z_end + radius + radius * math.sin(angle)))
        points.append((x_centre - r, high.z))
        return [bm.verts.new((x, y, z)) for x, z in points]

    rows = [contour(r, y) for r, y in levels]
    bm.faces.new(rows[0])  # the flat roof, closed along the mouth edge
    for upper, lower in zip(rows, rows[1:]):
        for j in range(len(upper) - 1):
            bm.faces.new((upper[j], upper[j + 1], lower[j + 1], lower[j]))
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-6)
    # A corner radius of zero at an inner level leaves zero-length arc edges; remove what they collapse.
    bmesh.ops.dissolve_degenerate(bm, dist=1e-6, edges=bm.edges[:])
    return bm, {"flat_half_width": round(flat_half, 5), "bevel_width": round(half - flat_half, 5),
                "corner_radius": round(corner_radius, 5), "roof_y": round(roof, 5)}


# ---------------------------------------------------------------- stage 1: model, .blend, .fbx

def unparent(obj):
    world = obj.matrix_world.copy()
    obj.parent = None
    obj.matrix_world = world


def build_and_export(out, corner_radius):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=proof.FBX_PATH)
    wall = next(o for o in bpy.context.scene.objects if o.type == "MESH" and "Wall" in (o.name, o.data.name))
    world = [wall.matrix_world @ v.co for v in wall.data.vertices]
    polygons = [tuple(p.vertices) for p in wall.data.polygons]
    fit = proof.fit_axes(world)
    to_unity, from_unity = maps(fit)
    profile, x_centre, low, high = sample_profile([to_unity(p) for p in world], polygons)

    if corner_radius is None:
        corner_radius = profile[-1][0] - split_profile(profile)[0]  # the bevel width: a crease-free corner
    bm, shape = build_cap(profile, x_centre, low, high, corner_radius)
    for vert in bm.verts:
        vert.co = from_unity(vert.co)
    # Blender world is the winding authority: outward normals there, as on every supplied mesh.
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.verts.index_update()
    cap_world = [v.co.copy() for v in bm.verts]
    cap_polygons = [tuple(v.index for v in f.verts) for f in bm.faces]
    to_local = wall.matrix_world.inverted()
    for vert in bm.verts:
        vert.co = to_local @ vert.co
    mesh = bpy.data.meshes.new("Brick_End")
    bm.to_mesh(mesh)
    bm.free()
    mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))
    if hasattr(mesh, "set_sharp_from_angle"):
        mesh.set_sharp_from_angle(angle=proof.SMOOTH_ANGLE)
    mesh.uv_layers.new(name="UVMap")  # BoardLit samples a fixed atlas texel, so zero UVs are enough

    # Same node transform as Wall, so the exported mesh data keeps Wall's mesh space.
    brick = wall.copy()
    brick.data, brick.name = mesh, "Brick_End"
    reference = wall.copy()
    reference.data = wall.data.copy()
    reference.name = reference.data.name = "Wall_Reference"
    for obj in (brick, reference):
        bpy.context.scene.collection.objects.link(obj)
        unparent(obj)
    for obj in list(bpy.context.scene.objects):
        if obj not in (brick, reference):
            bpy.data.objects.remove(obj, do_unlink=True)

    expected = {"Brick_End": local_bounds(brick), "Wall_Reference": local_bounds(reference)}
    blend_path = os.path.join(out, "Brick_End.blend")
    fbx_path = os.path.join(out, "WallBrick.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=blend_path, copy=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in (brick, reference):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = brick
    bpy.ops.export_scene.fbx(filepath=fbx_path, use_selection=True, object_types={"MESH"},
                             bake_space_transform=False, mesh_smooth_type="FACE",
                             add_leaf_bones=False, bake_anim=False)

    # Round trip: the re-imported mesh data must keep both bounds, so Unity reads Brick_End in Wall's space.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx_path)
    reimported = {}
    for obj in bpy.context.scene.objects:
        for name in expected:
            if obj.type == "MESH" and obj.name.startswith(name):
                reimported[name] = local_bounds(obj)
    round_trip = all(name in reimported and max(abs(a - b) for a, b in zip(expected[name], reimported[name])) < 1e-4
                     for name in expected)
    return {
        "blend": blend_path, "fbx": fbx_path, "axis_fit": fit, "end_shape": shape,
        "profile_unity_r_y": [(round(r, 5), round(y, 5)) for r, y in profile],
        "local_bounds": expected, "reimported_local_bounds": reimported, "round_trip_ok": round_trip,
    }, cap_world, cap_polygons


# ---------------------------------------------------------------- stage 2: bricks with the prefab calibration

def end_yaw(kit, mouth):
    """Yaw that turns Brick_End's single mouth toward `mouth`; its rounded end then faces the other way."""
    base = proof.mouths(kit, "Brick_End")
    if len(base) != 1:
        raise ValueError(f"Brick_End should have exactly one mouth, found {base}.")
    for yaw in (0, 90, 180, 270):
        if proof.rotate_xz(base[0], yaw) == tuple(mouth):
            return yaw
    raise ValueError(f"No yaw turns Brick_End's mouth {base[0]} toward {mouth}.")


def add_brick(kit, pieces, side, quadrants, color, shift=(0.0, 0.0)):
    """One brick over a contiguous straight run: Brick_End, Walls, Brick_End, all at quadrant scale."""
    d = proof.SIDES[side]["d"]
    for i, along in enumerate(quadrants):
        x, z = proof.point(side, along)
        position = (x + shift[0], z + shift[1])
        if i in (0, len(quadrants) - 1):
            mouth = d if i == 0 else (-d[0], -d[1])
            pieces.append(("Brick_End", position, end_yaw(kit, mouth), proof.QUADRANT_SCALE, color))
        else:
            pieces.append(("Wall", position, proof.WALL_YAW[side], proof.QUADRANT_SCALE, color))


def board(kit, gate_bricks):
    """brick_proof's 4 x 4 reference board: every straight run between corners and gates is one brick."""
    pieces, quads = [], []
    for x in range(proof.WIDTH):
        for z in range(proof.HEIGHT):
            quads.append(((x, z), (0.96, 0.96), -0.001, "floor"))
    for position, yaw in proof.CORNERS:
        pieces.append(("Corner_1", position, yaw, proof.QUADRANT_SCALE, "wall"))
    for side, cfg in proof.SIDES.items():
        for run in proof.runs(side):
            add_brick(kit, pieces, side, run, "wall")
            middle, length = (run[0] + run[-1]) * 0.5, 0.5 * len(run)
            quads.append((proof.point(side, middle), (length, 0.5) if cfg["along_x"] else (0.5, length), -0.002, "backer"))
    for side, cell, color in proof.GATES:
        if gate_bricks:
            add_brick(kit, pieces, side, [cell - 0.25, cell + 0.25], color)
        else:
            pieces.append(("Door_1x1", proof.point(side, cell), proof.DOOR_YAW[side], proof.DOOR_SCALE, color))
        pieces.append(("Door_Arrow", proof.point(side, cell), proof.ARROW_YAW[side], proof.ARROW_SCALE, "arrow_" + color))
    return pieces, quads


def lengths_row(kit):
    """Bricks of 1, 2 and 3 cells, and the current continuous 3-cell wall run for comparison, one row each."""
    pieces, quads = [], []
    for row, cells in enumerate((1, 2, 3, 0)):
        shift = (0.0, 1.5 * row)
        quadrants = [q * 0.5 - 0.25 for q in range(2 * (cells or 3))]
        if cells:
            add_brick(kit, pieces, "bottom", quadrants, "wall", shift)
        else:
            for along in quadrants:
                x, z = proof.point("bottom", along)
                pieces.append(("Wall", (x, z + shift[1]), proof.WALL_YAW["bottom"], proof.QUADRANT_SCALE, "wall"))
        x, z = proof.point("bottom", (quadrants[0] + quadrants[-1]) * 0.5)
        quads.append(((x, z + shift[1]), (0.5 * len(quadrants), 0.5), -0.002, "backer"))
        for cell in range(cells or 3):
            quads.append(((cell, 0.0 + shift[1]), (0.96, 0.96), -0.001, "floor"))
    return pieces, quads


def joins(kit):
    """Roof profiles across a 2-cell brick: the End/Wall join and mid-length against plain Walls."""
    brick, walls = [], []
    quadrants = [-0.25, 0.25, 0.75, 1.25]
    add_brick(kit, brick, "bottom", quadrants, "wall")
    for along in quadrants:
        walls.append(("Wall", proof.point("bottom", along), proof.WALL_YAW["bottom"], proof.QUADRANT_SCALE, "wall"))
    before = proof.roof_profile(kit, brick, "bottom", -0.004)
    after = proof.roof_profile(kit, brick, "bottom", 0.004)
    middle = proof.roof_profile(kit, brick, "bottom", 0.5)
    reference = proof.roof_profile(kit, walls, "bottom", 0.5)

    def largest(a, b):
        steps = [abs(p - q) for p, q in zip(a, b) if p is not None and q is not None]
        return round(max(steps), 5) if steps else None

    return {
        "end_to_wall_max_step": largest(before, after),
        "middle_vs_plain_wall_max_difference": largest(middle, reference),
        "cap_tip_profile": proof.roof_profile(kit, brick, "bottom", -0.45),
        "profiles": {"before_join": before, "after_join": after, "middle": middle},
    }


def main():
    args = parse_args()
    os.makedirs(args.out, exist_ok=True)
    evidence, cap_world, cap_polygons = build_and_export(args.out, args.corner_radius)

    kit = proof.Kit(proof.load_fbx())
    fit = kit.fit
    to_unity, _ = maps((fit["error"], fit["perm"], fit["signs"], fit["scale"], fit["det"]))
    unity = [to_unity(p) for p in cap_world]
    kit.meshes["Brick_End"] = (unity, cap_polygons)
    kit.visual["Brick_End"] = proof.visual_matrix("Wall", *proof.bounds(unity))  # the Wall calibration
    evidence["brick_end_mouth_at_yaw_0"] = proof.mouths(kit, "Brick_End")
    evidence["joins"] = joins(kit)

    proof.setup_render()
    renders = {}
    center = ((proof.WIDTH - 1) * 0.5, 0.0, (proof.HEIGHT - 1) * 0.5)
    scenes = {
        "bricks_1_2_3": (lengths_row(kit), [("", (1.0, 0.0, 1.8), 7.5, 40.0, (1080, 1080))]),
        "board_door_gates": (board(kit, False), [("_game", center, proof.game_distance(1080 / 1920), 60.0, (1080, 1920)),
                                                 ("_close", (0.5, 0.0, proof.HEIGHT - 1.5), 4.5, 40.0, (1080, 1080))]),
        "board_brick_gates": (board(kit, True), [("_close", (0.5, 0.0, proof.HEIGHT - 1.5), 4.5, 40.0, (1080, 1080))]),
    }
    for name, ((pieces, quads), shots) in scenes.items():
        proof.clear_scene()
        proof.build(kit, pieces, quads)
        for suffix, target, distance, fov, resolution in shots:
            path = os.path.join(args.out, f"{name}{suffix}.png")
            proof.render(path, target, distance, fov, resolution)
            renders[name + suffix] = path
    proof.clear_scene()
    proof.build(kit, [("Brick_End", (0.0, 0.0), 0, (1.0, 1.0, 1.0), "wall")], [])
    renders["brick_end_close"] = os.path.join(args.out, "brick_end_close.png")
    proof.render(renders["brick_end_close"], (0.0, 0.3, 0.0), 2.4, 35.0, (1080, 1080), pitch=55.0)
    evidence["renders"] = renders

    with open(os.path.join(args.out, "evidence.json"), "w", encoding="utf-8") as handle:
        json.dump(evidence, handle, indent=2, default=str)
    print(f"[brick-end] round trip {'OK' if evidence['round_trip_ok'] else 'MISMATCH'}; "
          f"End/Wall step {evidence['joins']['end_to_wall_max_step']}; outputs in {args.out}")


if __name__ == "__main__":
    main()
