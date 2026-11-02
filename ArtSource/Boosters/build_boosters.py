"""Rebuild the reference-matched booster meshes and optional offline renders in Blender.

Run with Blender --background --factory-startup --python this_file -- [--render].
Only this task's generated outputs are overwritten. No Unity scenes are edited.
"""

import bpy
import bmesh
import json
import math
import sys
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Boosters"
EXPORT = ROOT / "Assets/ColorBlockJam/Art/Models/Boosters"
REPORT = ROOT / "docs/art/boosters"
for folder in (SOURCE, EXPORT, REPORT):
    folder.mkdir(parents=True, exist_ok=True)

palette_data = json.loads((ROOT/'ArtSource/BoardKit/palette-export.json').read_text(encoding='utf-8-sig'))
COLORS = [tuple(entry['m_color'][channel] for channel in ('r','g','b'))
          for entry in palette_data['m_boosterSwatches']]
assert len(COLORS) == 8, 'Export the eight authored GamePalette booster swatches before rebuilding.'

# A fresh background Blender process is the isolated authoring workspace.
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
bpy.context.preferences.filepaths.save_version = 0
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1.0
scene.render.engine = 'CYCLES'
scene.cycles.samples = 48
scene.cycles.use_denoising = True
scene.render.resolution_x = 768
scene.render.resolution_y = 768
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.view_settings.look = 'Medium High Contrast' if 'Medium High Contrast' in [i.name for i in scene.view_settings.bl_rna.properties['look'].enum_items] else 'None'
scene.view_settings.exposure = -0.3
scene.world.color = (0.18, 0.18, 0.18)

palette = bpy.data.images.new('T_BoosterPalette_BC', width=128, height=16, alpha=False)
pixels = []
for y in range(16):
    for x in range(128):
        pixels.extend((*COLORS[x // 16], 1.0))
palette.pixels = pixels
# The Unity authoring command owns the runtime atlas (eight swatches plus white).
# Keep this UV-layout texture beside the Blender source so a mesh rebuild cannot
# overwrite that atlas with the single-row authoring version.
palette.filepath_raw = str(SOURCE / 'T_BoosterPalette_BC.png')
palette.file_format = 'PNG'
palette.save()
palette.pack()
material = bpy.data.materials.new('MAT_BoosterPalette')
material.use_nodes = True
shader = material.node_tree.nodes.get('Principled BSDF')
shader.inputs['Roughness'].default_value = 0.40
shader.inputs['Specular IOR Level'].default_value = 0.24
tex = material.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = palette
tex.interpolation = 'Closest'
material.node_tree.links.new(tex.outputs['Color'], shader.inputs['Base Color'])

collection = None
parts = []


def put(obj, color):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    collection.objects.link(obj)
    obj.data.materials.clear()
    obj.data.materials.append(material)
    group = obj.vertex_groups.new(name=obj.name)
    group.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')
    uv = obj.data.uv_layers.get('UVMap') or obj.data.uv_layers.new(name='UVMap')
    for loop in uv.data:
        loop.uv = ((color + 0.5) / 8.0, 0.5)
    parts.append(obj)
    return obj


def mesh(name, vertices, faces, color, smooth=True):
    data = bpy.data.meshes.new('SM_' + name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new('SM_' + name, data)
    collection.objects.link(obj)
    for poly in data.polygons:
        poly.use_smooth = smooth and len(poly.vertices) == 4
    return put(obj, color)


def lathe(name, profile, color, n, origin=(0, 0, 0), axis=(0, 0, 1), cap_color=None, oval=1.0):
    rotation = Vector((0, 0, 1)).rotation_difference(Vector(axis).normalized())
    vertices = []
    for z, radius in profile:
        for i in range(n):
            a = 2 * math.pi * i / n
            point = Vector((radius * math.cos(a), radius * math.sin(a) * oval, z))
            vertices.append(rotation @ point + Vector(origin))
    faces = [tuple(reversed(range(n)))]
    for j in range(len(profile) - 1):
        for i in range(n):
            a, b = j * n + i, j * n + (i + 1) % n
            faces.append((a, b, b + n, a + n))
    faces.append(tuple((len(profile) - 1) * n + i for i in range(n)))
    obj = mesh(name, vertices, faces, color)
    if cap_color is not None:
        for idx in (0, len(faces) - 1):
            for loop in obj.data.polygons[idx].loop_indices:
                obj.data.uv_layers.active.data[loop].uv = ((cap_color + 0.5) / 8, 0.5)
    return obj


def barrel(name, length, radius, color, n, origin=(0, 0, 0), axis=(0, 0, 1), bevel=0.04, cap_color=None):
    b = min(bevel, length * 0.22, radius * 0.22)
    profile = [(0, radius-b), (b*.3, radius-b*.3), (b, radius),
               (length-b, radius), (length-b*.3, radius-b*.3), (length, radius-b)]
    return lathe(name, profile, color, n, origin, axis, cap_color)


def box(name, location, size, color, bevel=0.06, segments=3):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = 'SM_' + name
    obj.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mod = obj.modifiers.new('Rounded molded edges', 'BEVEL')
    mod.width, mod.segments = bevel, segments
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for poly in obj.data.polygons:
        poly.use_smooth = True
    mod = obj.modifiers.new('Stable face normals', 'WEIGHTED_NORMAL')
    mod.keep_sharp = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return put(obj, color)


def tube(name, points, radius, color, n):
    vertices = []
    for j, p in enumerate(points):
        tangent = Vector(points[min(j+1, len(points)-1)]) - Vector(points[max(j-1, 0)])
        tangent.normalize()
        side = tangent.cross(Vector((0, 1, 0))).normalized()
        up = tangent.cross(side).normalized()
        for i in range(n):
            a = 2*math.pi*i/n
            vertices.append(Vector(p) + radius * (math.cos(a)*side + math.sin(a)*up))
    faces = [tuple(reversed(range(n)))]
    for j in range(len(points)-1):
        for i in range(n):
            a, b = j*n+i, j*n+(i+1)%n
            faces.append((a, b, b+n, a+n))
    faces.append(tuple((len(points)-1)*n+i for i in range(n)))
    return mesh(name, vertices, faces, color)


def hammer(lod):
    n = 32 if lod == 0 else 16
    # A horizontal orange barrel framed by two thick, rounded green striking pads.
    barrel('HammerCore', 1.36, .38, 4, n, (-.68, 0, 1.64), (1, 0, 0), .055)
    for x in (-1.0, .66):
        barrel('HammerPad', .34, .59, 5, n, (x, 0, 1.64), (1, 0, 0), .075, 6)
    handle_axis = Vector((0,.55,1.36)).normalized()
    barrel('HammerHandle', 1.48, .195, 4, n, (0,-.55,.03), handle_axis, .055)
    barrel('HammerGripRim', .055, .198, 3, n, Vector((0,-.55,.03))+handle_axis*.04, handle_axis, .015)


def rocket(lod):
    n = 32 if lod == 0 else 16
    barrel('RocketBody', 1.30, .40, 4, n, (0, 0, .34), bevel=.07)
    barrel('RocketTail', .58, .445, 2, n, (0, 0, .19), bevel=.065)
    barrel('RocketBelt', .30, .452, 2, n, (0, 0, .99), bevel=.045)
    lathe('RocketNose', [(1.57,.48),(1.59,.54),(1.65,.55),(1.70,.52),
                         (2.20,.13),(2.27,.06),(2.29,.017)], 3, n)
    # Four thick swept fins, rounded at export, no boolean or hidden cylinders.
    contour = [(.39,.69),(.60,.60),(.78,.28),(.79,.12),(.72,.065),(.39,.23)]
    for i in range(4):
        verts = [(x, y, z) for y in (-.067,.067) for x,z in contour]
        faces = [tuple(reversed(range(6))), tuple(range(6,12))]
        faces += [(j,(j+1)%6,(j+1)%6+6,j+6) for j in range(6)]
        obj = mesh('RocketFin', verts, faces, 3, False)
        obj.rotation_euler.z = i*math.pi/2 + math.pi/4
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new('Soft fin edges', 'BEVEL')
        mod.width, mod.segments = .035, (3 if lod == 0 else 1)
        bpy.ops.object.modifier_apply(modifier=mod.name)
        for poly in obj.data.polygons:
            poly.use_smooth = True
        mod = obj.modifiers.new('Fin weighted normals', 'WEIGHTED_NORMAL')
        bpy.ops.object.modifier_apply(modifier=mod.name)
    barrel('RocketExhaust', .13, .27, 4, n, (0, 0, .10), bevel=.025)


def vacuum(lod):
    n = 24 if lod == 0 else 12
    lathe('VacuumShell', [(.41,.53),(.52,.59),(.89,.60),(1.20,.55),(1.49,.44),
                         (1.68,.28),(1.73,.18)], 0, n, oval=.83)
    lathe('VacuumBase', [(.14,.40),(.18,.53),(.26,.60),(.45,.64),(.58,.64),(.62,.59)], 3, n, oval=.88)
    lathe('VacuumBottom', [(.09,.38),(.12,.48),(.20,.53),(.25,.53)], 4, n, oval=.88)
    barrel('VacuumCollar', .21, .245, 0, n, (0, 0, 1.67), bevel=.055)
    # A slightly raised front switch, side service panel, and two blue wheels.
    barrel('VacuumSwitchSeat', .06, .148, 1, n, (0, -.45, 1.32), (0,-1,.18), .018)
    barrel('VacuumSwitch', .048, .127, 7, n, (0, -.502, 1.329), (0,-1,.18), .018)
    # Closed curved side cover follows the shell rather than intersecting a flat oval.
    shell = [(.59,.593,.75),(.65,.596,.88),(.89,.60,.94),
             (1.20,.55,.90),(1.39,.478,.70),(1.48,.444,.39),(1.51,.422,.10)]
    columns = 12 if lod == 0 else 6
    verts = []
    for depth in (.015,-.015):
        for z,radius,half_angle in shell:
            for j in range(columns+1):
                angle = -half_angle + 2*half_angle*j/columns
                verts.append(((radius+depth)*math.cos(angle), (radius+depth)*math.sin(angle)*.83, z))
    rows, width = len(shell), columns+1
    layer = rows*width
    faces = []
    for side in (0,1):
        for row in range(rows-1):
            for col in range(columns):
                a = side*layer+row*width+col
                face = (a,a+1,a+width+1,a+width)
                faces.append(face if side == 0 else tuple(reversed(face)))
    outline = list(range(width)) + [row*width+columns for row in range(1,rows)]
    outline += list(range(layer-2,layer-width-1,-1)) + [row*width for row in range(rows-2,0,-1)]
    for i,a in enumerate(outline):
        b = outline[(i+1)%len(outline)]
        faces.append((a,b,b+layer,a+layer))
    mesh('VacuumSidePanel', verts, faces, 1)
    for x, direction in ((-.55,(-1,0,0)),(.55,(1,0,0))):
        barrel('VacuumWheel', .20, .255, 1, n, (x,0,.28), direction, .045)
        barrel('VacuumWheelHub', .015, .13, 1, 16 if lod == 0 else 8, (x+direction[0]*.198,0,.28), direction, .008)
    # Broad smooth purple inverted U, matching the supplied icon's characteristic loop.
    steps = 28 if lod == 0 else 16
    points = []
    for i in range(steps+1):
        t = math.pi - i/steps*math.pi
        points.append((.56 + .56*math.cos(t), .11, 1.77 + .94*math.sin(t)))
    points += [(1.12,.08,1.60),(1.10,.015,1.43)]
    tube('VacuumHose', points, .105, 2, 12 if lod == 0 else 8)
    start, end = Vector((1.105,.03,1.48)), Vector((1.02,-.44,.30))
    direction = (end-start).normalized()
    length = (end-start).length
    barrel('VacuumWandCore', length, .09, 4, 20 if lod == 0 else 12, start, direction, .018)
    for i in range(5):
        barrel('VacuumGrip', length/5-.026, .125, 3, 20 if lod == 0 else 12,
               start + direction*(i*length/5), direction, .022)
    barrel('VacuumNozzleNeck', .20, .116, 0, n, end-direction*.03, direction, .025)
    box('VacuumFloorHead', (1.02,-.60,.125), (.90,.44,.20), 0, .065, 3 if lod == 0 else 1)
    barrel('VacuumHeadHinge', .36, .12, 1, n, (.84,-.51,.23), (1,0,0), .03)


def combine(name, lod):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = 'SM_' + name + '_LOD' + str(lod)
    scene.cursor.location = (0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if name == 'Hammer':
        # Put the swing pivot at the lower grip center.
        for vertex in obj.data.vertices:
            vertex.co -= Vector((0,-.55,.03))
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bad_edges = sum(not e.is_manifold for e in bm.edges)
    loose = sum(not v.link_faces for v in bm.verts)
    degenerate = sum(f.calc_area() < 1e-10 for f in bm.faces)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    assert not (bad_edges or loose or degenerate), (name, bad_edges, loose, degenerate)
    assert len(obj.data.polygons) <= {'Vacuum':6000,'RocketPurple':3000,'Hammer':2200}[name], (name, len(obj.data.polygons))
    assert len(obj.data.materials) == 1
    return obj, {'triangles':len(obj.data.polygons), 'vertices':len(obj.data.vertices),
                 'materials':len(obj.data.materials), 'non_manifold_edges':bad_edges,
                 'loose_vertices':loose, 'degenerate_triangles':degenerate,
                 'dimensions_m':list(obj.dimensions), 'scale':list(obj.scale)}


assets, stats = {}, {}
for name, build in [('Hammer',hammer),('RocketPurple',rocket),('Vacuum',vacuum)]:
    assets[name], stats[name] = [], {}
    for lod in range(2):
        collection = bpy.data.collections.new('COL_' + name + '_LOD' + str(lod))
        scene.collection.children.link(collection)
        parts = []
        build(lod)
        obj, info = combine(name,lod)
        assets[name].append(obj)
        stats[name]['LOD'+str(lod)] = info
    bpy.ops.object.select_all(action='DESELECT')
    for obj in assets[name]:
        obj.select_set(True)
    path = EXPORT / (name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=str(path), use_selection=True, object_types={'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
                             axis_forward='-Z', axis_up='Y', bake_space_transform=True,
                             use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False,
                             add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
    stats[name]['fbx_bytes'] = path.stat().st_size

# Presentation studio exists only in the source .blend, never in exported FBX files.
studio = bpy.data.collections.new('COL_RenderStudio')
scene.collection.children.link(studio)
def light(name, pos, energy, size, color):
    data = bpy.data.lights.new('LGT_' + name, 'AREA')
    data.energy, data.shape, data.size, data.color = energy, 'DISK', size, color
    obj = bpy.data.objects.new('LGT_' + name, data)
    studio.objects.link(obj)
    obj.location = pos
    obj.rotation_euler = (Vector((0,0,1))-obj.location).to_track_quat('-Z','Y').to_euler()
light('Key', (-3,-4,7), 650, 4.0, (1.0,.96,.87))
light('Fill', (4,-2,4), 320, 3.0, (.80,.91,1.0))
light('Rim', (1,4,5), 800, 3.0, (.90,.94,1.0))
cam_data = bpy.data.cameras.new('CAM_Boosters')
camera = bpy.data.objects.new('CAM_Boosters',cam_data)
studio.objects.link(camera)
scene.camera = camera
cam_data.type = 'ORTHO'

def frame(name, lod=0, back=False):
    for pair in assets.values():
        for obj in pair:
            obj.hide_render = True
    obj = assets[name][lod]
    obj.hide_render = False
    target = Vector((.30,0,1.36) if name == 'Vacuum' else (0,0,1.13))
    offset = Vector((3,-6,3) if name == 'Vacuum' else (4,-7,3.5))
    if back:
        offset = Vector((-4,6,3))
    camera.location = target + offset
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z','Y').to_euler()
    cam_data.ortho_scale = 3.32 if name == 'Vacuum' else 2.80
    if name == 'RocketPurple':
        camera.rotation_euler.rotate_axis('Z', math.radians(32))
    return obj

for name in assets:
    for obj in assets[name]:
        obj.hide_set(True)
assets['Vacuum'][0].hide_set(False)
frame('Vacuum')
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.shading.type = 'MATERIAL'
            area.spaces.active.region_3d.view_location = (.3,0,1.35)
            area.spaces.active.region_3d.view_distance = 4.8
            area.spaces.active.region_3d.view_rotation = camera.rotation_euler.to_quaternion()
            area.spaces.active.region_3d.view_perspective = 'ORTHO'
stats['palette'] = {'width':128,'height':16,'bytes':(SOURCE/'T_BoosterPalette_BC.png').stat().st_size,
                    'intentional_uv_overlap':'Each face samples the center of a padded solid-color swatch.'}
(REPORT/'mesh-report.json').write_text(json.dumps(stats,indent=2)+'\n',encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Boosters.blend'), compress=True)
if '--render' in sys.argv:
    for name in assets:
        for lod in (0,1):
            frame(name,lod)
            scene.render.filepath = str(REPORT/(name+('_LOD1' if lod else '')+'.png'))
            bpy.ops.render.render(write_still=True)
        frame(name,0,True)
        scene.render.filepath = str(REPORT/(name+'_Back.png'))
        bpy.ops.render.render(write_still=True)
    # A single true 3D render presents all three deliverables together.
    for pair in assets.values():
        for obj in pair:
            obj.hide_render = True
    target = Vector((0,0,1.25))
    camera.location = target + Vector((4,-10,4.5))
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z','Y').to_euler()
    right = camera.rotation_euler.to_matrix() @ Vector((1,0,0))
    for name,offset in [('Hammer',-2.7),('RocketPurple',0),('Vacuum',2.7)]:
        obj = assets[name][0]
        obj.hide_render = False
        obj.location = right*offset
    scene.render.resolution_x, scene.render.resolution_y = 1600, 720
    cam_data.ortho_scale = 9.1
    scene.render.filepath = str(REPORT/'BoosterLineup.png')
    bpy.ops.render.render(write_still=True)
print('BOOSTER_REPORT ' + json.dumps(stats))
