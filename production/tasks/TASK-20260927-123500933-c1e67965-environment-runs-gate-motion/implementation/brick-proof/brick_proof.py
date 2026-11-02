"""Brick proof: compose reusable rounded wall bricks from the supplied WallAndDoor.fbx meshes with the
real prefab calibration, render them beside the current continuous frame and measure their roof.

Nothing is modelled or edited: the FBX is read only, and every piece is an unchanged supplied mesh
placed with the Visual-child calibration of Editor/BoardKit/BoardKitAssets.cs and a per-placement
root position, yaw and scale, exactly what EnvironmentPiecePlacement can already express.

Run (Blender 4.1+):
  blender --background --factory-startup --python brick_proof.py -- [--out DIR] [--brick-cells 1]
"""
import argparse
import itertools
import json
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", "..", "..", ".."))
FBX_PATH = os.path.join(REPO, "Assets", "ColorBlockJam", "Art", "Models", "WallAndDoor.fbx")

# D-001's Blender measurement of Wall in Unity mesh space plus the roof finding; they select the axis mapping.
WALL_X = (-0.49905, 0.498)
WALL_Z = (-0.4983, 0.5017)
WALL_ROOF_Y = -0.61087

# BoardKitAssets.cs Visual calibration and EnvironmentDatabase root scales.
PIECE_HEIGHT = 0.72
CORNER_OFFSET_X = -0.0083
CORNER_STRETCH_X = 1.0166
WALL_STRETCH_X = 1.01
DOOR_STRETCH_Z = 1.02
QUADRANT_SCALE = (0.5, 1.0, 0.5)
DOOR_SCALE = (1.0, 1.0, 0.5)
ARROW_SCALE = (0.5, 1.0, 0.5)
ARROW_WHITE_BLEND = 0.6
SMOOTH_ANGLE = math.radians(40.0)

# GamePalette.asset (sRGB) and the Game camera clear colour (DEC-118).
COLORS = {"wall": "394961", "floor": "687CA7", "backer": "252D3D", "blue": "00C4F3", "red": "F12B45"}
BACKGROUND = "D6E4FC"

# A 4 x 4 board like the reference crop: a blue top gate over cell 1 and a red left gate at row 2.
WIDTH, HEIGHT = 4, 4
GATES = [("top", 1, "blue"), ("left", 2, "red")]
SIDES = {
    "top": {"line": HEIGHT - 0.25, "along_x": True, "d": (1, 0), "n": (0, -1), "count": WIDTH},
    "bottom": {"line": -0.75, "along_x": True, "d": (1, 0), "n": (0, 1), "count": WIDTH},
    "left": {"line": -0.75, "along_x": False, "d": (0, 1), "n": (1, 0), "count": HEIGHT},
    "right": {"line": WIDTH - 0.25, "along_x": False, "d": (0, 1), "n": (-1, 0), "count": HEIGHT},
}
# EnvironmentRuleSet angles for the current composer (Environment-Layout.md, composer golden tests).
WALL_YAW = {"left": 0, "top": 90, "right": 180, "bottom": 270}
CORNERS = [((-0.75, HEIGHT - 0.25), 270), ((WIDTH - 0.25, HEIGHT - 0.25), 0),
           ((-0.75, -0.75), 180), ((WIDTH - 0.25, -0.75), 90)]
DOOR_YAW = {"top": 0, "bottom": 0, "left": 90, "right": 90}
ARROW_YAW = {"top": 180, "left": 90, "right": 270, "bottom": 0}

VARIANTS = ["baseline", "single", "hairpin", "hairpin_gate"]


def parse_args():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--out", default=os.path.join(HERE, "out"))
    parser.add_argument("--brick-cells", type=int, default=1)
    parser.add_argument("--no-backer", action="store_true")
    parser.add_argument("--variants", default=",".join(VARIANTS))
    return parser.parse_args(argv)


def srgb_to_linear(hex_color):
    channels = [int(hex_color[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in channels]


def color_of(key):
    if key.startswith("arrow_"):
        base = srgb_to_linear(COLORS[key[6:]])
        return [c + (1.0 - c) * ARROW_WHITE_BLEND for c in base] + [1.0]
    return srgb_to_linear(COLORS[key]) + [1.0]


def to_blender(u):
    # Unity is left-handed Y-up; Blender is right-handed Z-up.
    return (u.x, u.z, u.y)


# ---------------------------------------------------------------- supplied meshes in Unity space

def load_fbx():
    """Imports the FBX once and returns {name: (Blender world vertices, polygons)}; the scene is emptied after."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=FBX_PATH)
    meshes = {}
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        world = obj.matrix_world
        data = ([world @ v.co for v in obj.data.vertices], [tuple(p.vertices) for p in obj.data.polygons])
        meshes.setdefault(obj.name, data)
        meshes.setdefault(obj.data.name, data)
    clear_scene()
    return meshes


def clear_scene():
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)


def find(meshes, name):
    if name in meshes:
        return meshes[name]
    for key, value in meshes.items():
        if key.startswith(name) and not key.startswith("corner_3.002"):
            return value
    raise KeyError(f"{name} not found in {FBX_PATH}; meshes: {sorted(meshes)}")


def fit_axes(wall_vertices):
    """Tries every signed axis permutation and picks the one that reproduces D-001's Wall bounds and roof."""
    best = None
    for perm in itertools.permutations(range(3)):
        for signs in itertools.product((1, -1), repeat=3):
            points = [[signs[i] * v[perm[i]] for i in range(3)] for v in wall_vertices]
            z = [p[2] for p in points]
            scale = 1.0 / (max(z) - min(z))  # Wall spans exactly one unit along Z (D-001)
            x = [p[0] * scale for p in points]
            y = [p[1] * scale for p in points]
            error = (abs(min(x) - WALL_X[0]) + abs(max(x) - WALL_X[1]) + abs(min(z) * scale - WALL_Z[0])
                     + abs(max(z) * scale - WALL_Z[1]) + abs(min(y) - WALL_ROOF_Y))
            if best is None or error < best[0]:
                inversions = sum(1 for i in range(3) for j in range(i + 1, 3) if perm[i] > perm[j])
                det = (-1 if inversions % 2 else 1) * signs[0] * signs[1] * signs[2]
                best = (error, perm, signs, scale, det)
    return best


class Kit:
    """The supplied meshes in Unity mesh space with their prefab Visual matrices (BoardKitAssets.cs)."""

    def __init__(self, meshes):
        error, perm, signs, scale, det = fit_axes(find(meshes, "Wall")[0])
        self.fit = {"error": error, "perm": perm, "signs": signs, "scale": scale, "det": det}
        if error > 0.01:
            print(f"WARNING: no axis mapping reproduces D-001's Wall bounds (error {error:.4f}).")
        self.det = det
        self.meshes = {}
        self.visual = {}
        for name in ("Wall", "Corner_1", "Door_1x1", "Door_Arrow"):
            vertices, polygons = find(meshes, name)
            unity = [Vector([signs[i] * v[perm[i]] * scale for i in range(3)]) for v in vertices]
            self.meshes[name] = (unity, polygons)
            self.visual[name] = visual_matrix(name, *bounds(unity))

    def world(self, name, root):
        matrix = root @ self.visual[name]
        return [matrix @ v for v in self.meshes[name][0]]


def bounds(points):
    low = Vector([min(p[i] for p in points) for i in range(3)])
    high = Vector([max(p[i] for p in points) for i in range(3)])
    return low, high


def visual_matrix(name, low, high):
    size, center = high - low, (low + high) * 0.5
    position, rotation = Vector((0.0, 0.0, 0.0)), Matrix.Rotation(math.pi, 4, "X")
    if name == "Door_Arrow":
        position = Vector((-center.x, 0.74 + high.y * 0.2, center.z))
        scale = Vector((1.0, 0.2, 1.0))
    else:
        sx = 1.0 / size.x if name == "Door_1x1" else 1.0
        scale = Vector((sx, PIECE_HEIGHT / size.y, 1.0))
        position.y = high.y * scale.y
        if name in ("Wall", "Door_1x1"):
            position.x = -center.x * sx
        if name == "Corner_1":
            position.x, scale.x = CORNER_OFFSET_X, CORNER_STRETCH_X
        elif name == "Wall":
            scale.x *= WALL_STRETCH_X
        elif name == "Door_1x1":
            scale.z = DOOR_STRETCH_Z
    return Matrix.Translation(position) @ rotation @ Matrix.Diagonal(scale.to_4d())


def root_matrix(position_xz, yaw, scale):
    # Unity TRS with a positive yaw about +Y; Matrix.Rotation gives the same numbers.
    return (Matrix.Translation((position_xz[0], 0.0, position_xz[1]))
            @ Matrix.Rotation(math.radians(yaw), 4, "Y") @ Matrix.Diagonal(Vector(scale).to_4d()))


def rotate_xz(vector, yaw):
    c, s = round(math.cos(math.radians(yaw))), round(math.sin(math.radians(yaw)))
    return (vector[0] * c + vector[1] * s, -vector[0] * s + vector[1] * c)


def mouths(kit, name):
    """Sides (x, z) at yaw 0 where the roof reaches the piece bound: a flat connection mouth, not a rounded edge."""
    points = kit.world(name, Matrix.Identity(4))
    low, high = bounds(points)
    roof = [p for p in points if p.y > high.y - 0.01]
    found = []
    for axis, key in ((0, "x"), (2, "z")):
        for sign in (1, -1):
            edge = high[axis] if sign > 0 else low[axis]
            reach = max(p[axis] for p in roof) if sign > 0 else min(p[axis] for p in roof)
            if abs(edge - reach) < 0.03:
                found.append((sign, 0) if key == "x" else (0, sign))
    return found


def side_roofs(kit, name):
    """Mesh-space roof Y (the closed top faces -Y) of the vertices on each side bound, for the root's evidence."""
    points, _ = kit.meshes[name]
    low, high = bounds(points)
    result = {}
    for axis, key in ((0, "x"), (2, "z")):
        for sign, edge in (("+", high[axis]), ("-", low[axis])):
            near = [p.y for p in points if abs(p[axis] - edge) < 0.01]
            result[sign + key] = round(min(near), 5) if near else None
    return result


def corner_yaw(kit, required):
    wanted = sorted(tuple(v) for v in required)
    base = mouths(kit, "Corner_1")
    for yaw in (0, 90, 180, 270):
        if sorted(rotate_xz(v, yaw) for v in base) == wanted:
            return yaw
    raise ValueError(f"No Corner_1 yaw gives mouths {wanted}; yaw-0 mouths are {base}.")


# ---------------------------------------------------------------- composition (board-local Unity x, z)

def point(side, along, across=0.0):
    cfg = SIDES[side]
    x, z = (along, cfg["line"]) if cfg["along_x"] else (cfg["line"], along)
    return (x + cfg["n"][0] * across, z + cfg["n"][1] * across)


def runs(side):
    """Quadrant centres of each wall run on a side, split at its gates; ring corners are separate segments."""
    gates = {cell for gate_side, cell, _ in GATES if gate_side == side}
    result, current = [], []
    for cell in range(SIDES[side]["count"]):
        if cell in gates:
            if current:
                result.append(current)
            current = []
            continue
        current += [cell - 0.25, cell + 0.25]
    if current:
        result.append(current)
    return result


def add_brick(kit, pieces, side, start, length, style, color):
    """One reusable brick from `start` along the side's run direction, `length` long and one quadrant thick.

    single:  quadrant-scale pieces; each end is one Corner_1 whose rounded edge faces outward at that end.
    hairpin: quarter-scale pieces in two columns; each end is two Corner_1 joined mouth to mouth (a U cap),
             so both vertical end edges are rounded, with two Wall columns between the caps.
    """
    d, n = SIDES[side]["d"], SIDES[side]["n"]
    minus_d, minus_n = (-d[0], -d[1]), (-n[0], -n[1])
    wall_yaw = 0 if d[1] != 0 else 90
    if style == "single":
        size, columns = 0.5, [(0.0, None)]
    else:
        size, columns = 0.25, [(-0.125, n), (0.125, minus_n)]
    steps = int(round(length / size))
    scale = (size, 1.0, size)
    for i in range(steps):
        along = start + (i + 0.5) * size
        for across, toward_other in columns:
            position = point(side, along, across)
            if i in (0, steps - 1):
                inward = d if i == 0 else minus_d
                side_mouth = n if toward_other is None else toward_other
                yaw = corner_yaw(kit, [inward, side_mouth])
                pieces.append(("Corner_1", position, yaw, scale, color))
            else:
                pieces.append(("Wall", position, wall_yaw, scale, color))


def compose(kit, variant, brick_cells, backer):
    pieces, quads = [], []
    for cell_x in range(WIDTH):
        for cell_z in range(HEIGHT):
            quads.append(((cell_x, cell_z), (0.96, 0.96), -0.001, "floor"))
    for position, yaw in CORNERS:
        pieces.append(("Corner_1", position, yaw, QUADRANT_SCALE, "wall"))
    for side in SIDES:
        for run in runs(side):
            if variant == "baseline":
                for along in run:
                    pieces.append(("Wall", point(side, along), WALL_YAW[side], QUADRANT_SCALE, "wall"))
                continue
            style = "single" if variant == "single" else "hairpin"
            chunk = 2 * brick_cells
            for first in range(0, len(run), chunk):
                quadrants = run[first:first + chunk]
                add_brick(kit, pieces, side, quadrants[0] - 0.25, 0.5 * len(quadrants), style, "wall")
            if backer:
                # Stands in for the existing FloorBase quad under the run, so rounded joins show a dark groove.
                middle = (run[0] + run[-1]) * 0.5
                length = 0.5 * len(run)
                size = (length, 0.5) if SIDES[side]["along_x"] else (0.5, length)
                quads.append((point(side, middle), size, -0.002, "backer"))
    for side, cell, color in GATES:
        if variant == "hairpin_gate":
            add_brick(kit, pieces, side, cell - 0.5, 1.0, "hairpin", color)
            if backer:
                size = (1.0, 0.5) if SIDES[side]["along_x"] else (0.5, 1.0)
                quads.append((point(side, cell), size, -0.002, "backer"))
        else:
            pieces.append(("Door_1x1", point(side, cell), DOOR_YAW[side], DOOR_SCALE, color))
        pieces.append(("Door_Arrow", point(side, cell), ARROW_YAW[side], ARROW_SCALE, "arrow_" + color))
    return pieces, quads


# ---------------------------------------------------------------- scene objects

def add_object(label, vertices, polygons, color_key):
    mesh = bpy.data.meshes.new(label)
    mesh.from_pydata(vertices, [], polygons)
    mesh.update()
    mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))
    if hasattr(mesh, "set_sharp_from_angle"):
        mesh.set_sharp_from_angle(angle=SMOOTH_ANGLE)
    obj = bpy.data.objects.new(label, mesh)
    obj.color = color_of(color_key)
    bpy.context.scene.collection.objects.link(obj)


def build(kit, pieces, quads):
    # Unity mesh space to Blender flips handedness once; the axis fit may flip it again.
    flip = -kit.det < 0
    for index, (name, position, yaw, scale, color) in enumerate(pieces):
        world = kit.world(name, root_matrix(position, yaw, scale))
        polygons = kit.meshes[name][1]
        if flip:
            polygons = [tuple(reversed(p)) for p in polygons]
        add_object(f"{index:03d}_{name}", [to_blender(v) for v in world], polygons, color)
    for index, ((x, z), (sx, sz), y, color) in enumerate(quads):
        hx, hz = sx * 0.5, sz * 0.5
        corners = [Vector((x - hx, y, z - hz)), Vector((x + hx, y, z - hz)),
                   Vector((x + hx, y, z + hz)), Vector((x - hx, y, z + hz))]
        add_object(f"quad_{index:03d}", [to_blender(v) for v in corners], [(0, 1, 2, 3)], color)


# ---------------------------------------------------------------- measurements

def roof_profile(kit, pieces, side, along, samples=21):
    """Roof height across the ring strip at one position along a side (ray cast straight down)."""
    vertices, polygons = [], []
    for name, position, yaw, scale, _ in pieces:
        if name == "Door_Arrow":
            continue
        offset = len(vertices)
        vertices += kit.world(name, root_matrix(position, yaw, scale))
        polygons += [tuple(i + offset for i in p) for p in kit.meshes[name][1]]
    tree = BVHTree.FromPolygons(vertices, polygons)
    heights = []
    for k in range(samples):
        across = -0.25 + 0.5 * k / (samples - 1)
        x, z = point(side, along, across)
        hit = tree.ray_cast(Vector((x, 5.0, z)), Vector((0.0, -1.0, 0.0)))
        heights.append(round(hit[0].y, 4) if hit[0] is not None else None)
    return heights


def measure(kit):
    """Roof profiles of one two-cell brick per style on a gate-free test side, at mid-length and at its joins."""
    report = {}
    for style in ("baseline", "single", "hairpin"):
        pieces = []
        if style == "baseline":
            for along in (-0.25, 0.25, 0.75, 1.25):
                pieces.append(("Wall", point("bottom", along), WALL_YAW["bottom"], QUADRANT_SCALE, "wall"))
        else:
            add_brick(kit, pieces, "bottom", -0.5, 2.0, style, "wall")
        size = 0.5 if style != "hairpin" else 0.25
        stations = {"middle": 0.5, "cap_join_before": -0.5 + size - 0.004, "cap_join_after": -0.5 + size + 0.004,
                    "cap_centre": -0.5 + size * 0.5}
        profiles = {key: roof_profile(kit, pieces, "bottom", along) for key, along in stations.items()}
        middle = [h for h in profiles["middle"] if h is not None]
        centre = profiles["middle"][len(profiles["middle"]) // 2]
        before, after = profiles["cap_join_before"], profiles["cap_join_after"]
        steps = [abs(a - b) for a, b in zip(before, after) if a is not None and b is not None]
        report[style] = {
            "profiles": profiles,
            "roof_max": max(middle) if middle else None,
            "centre_groove_depth": round(max(middle) - centre, 4) if middle and centre is not None else None,
            "cap_join_max_step": round(max(steps), 4) if steps else None,
        }
    return report


# ---------------------------------------------------------------- rendering

def setup_render():
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    shading = scene.display.shading
    shading.light = "STUDIO"
    shading.color_type = "OBJECT"
    shading.show_cavity = True
    shading.cavity_type = "BOTH"
    shading.show_shadows = True
    scene.display.render_aa = "8"
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("Background")
    world.color = srgb_to_linear(BACKGROUND)
    scene.world = world
    scene.render.image_settings.file_format = "PNG"


def render(path, target, distance, fov, resolution, pitch=75.0):
    """Renders from the Game camera's pitch (DEC-103): Euler(pitch, 0, 0), looking at target from distance."""
    scene = bpy.context.scene
    data = bpy.data.cameras.new("Camera")
    data.sensor_fit = "VERTICAL"
    data.angle = math.radians(fov)
    camera = bpy.data.objects.new("Camera", data)
    scene.collection.objects.link(camera)
    forward = Vector((0.0, -math.sin(math.radians(pitch)), math.cos(math.radians(pitch))))
    camera.location = to_blender(Vector(target) - forward * distance)
    camera.rotation_euler = (math.radians(90.0 - pitch), 0.0, 0.0)
    scene.camera = camera
    scene.render.resolution_x, scene.render.resolution_y = resolution
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(camera, do_unlink=True)


def game_distance(aspect, fov=60.0):
    # BoardCameraFraming: the board plus its ring, side margin 1.5, vertical margin 5 (DEC-103, DEC-118).
    half = math.tan(math.radians(fov) * 0.5)
    return max((WIDTH + 1 + 1.5) / (2 * half * aspect), (HEIGHT + 1 + 5) / (2 * half))


def main():
    args = parse_args()
    os.makedirs(args.out, exist_ok=True)
    kit = Kit(load_fbx())
    setup_render()
    evidence = {
        "fbx": FBX_PATH,
        "axis_fit": kit.fit,
        "mouths_at_yaw_0": {name: mouths(kit, name) for name in ("Wall", "Corner_1", "Door_1x1")},
        "mesh_space_side_roof_y": {name: side_roofs(kit, name) for name in ("Wall", "Corner_1", "Door_1x1")},
        "baseline_corner_yaws_match_mouths": {
            str(yaw): [rotate_xz(v, yaw) for v in mouths(kit, "Corner_1")] for _, yaw in CORNERS},
        "roof": measure(kit),
        "renders": {},
    }
    center = ((WIDTH - 1) * 0.5, 0.0, (HEIGHT - 1) * 0.5)
    close = (0.5, 0.0, HEIGHT - 1.5)
    for variant in args.variants.split(","):
        clear_scene()
        pieces, quads = compose(kit, variant, args.brick_cells, backer=not args.no_backer and variant != "baseline")
        build(kit, pieces, quads)
        game = os.path.join(args.out, f"{variant}_game.png")
        closeup = os.path.join(args.out, f"{variant}_close.png")
        render(game, center, game_distance(1080 / 1920), 60.0, (1080, 1920))
        render(closeup, close, 4.5, 40.0, (1080, 1080))
        counts = {}
        for name, *_ in pieces:
            counts[name] = counts.get(name, 0) + 1
        evidence["renders"][variant] = {"game": game, "close": closeup, "piece_counts": counts}
        print(f"[brick-proof] {variant}: {counts}")
    with open(os.path.join(args.out, "evidence.json"), "w", encoding="utf-8") as handle:
        json.dump(evidence, handle, indent=2)
    print(f"[brick-proof] axis fit error {kit.fit['error']:.5f}; evidence in {args.out}")


if __name__ == "__main__":
    main()
