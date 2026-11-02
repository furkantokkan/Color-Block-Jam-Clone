"""Bake an upright gameplay Vacuum with a swallowing hose; preserve the icon model.

Only the hose deforms during suction. Turn-in/out is baked; Unity owns the scale tween.
"""
import bpy
import bmesh
import json
import math
import sys
from array import array
from pathlib import Path
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/Boosters'
OUTPUT = ROOT / 'Assets/ColorBlockJam/Art/Textures/Atlases'
EVIDENCE = ROOT / 'docs/art/boosters/motion'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE / 'Boosters.blend'))
bpy.context.preferences.filepaths.save_version = 0
scene = bpy.context.scene
scene.render.threads_mode = 'FIXED'
scene.render.threads = 6
FRAMES, CELL, COLUMNS, ROWS = 32, 192, 4, 8
WIDTH, HEIGHT = CELL*COLUMNS, CELL*ROWS
scene.render.resolution_x = scene.render.resolution_y = CELL
scene.render.resolution_percentage = 100
scene.render.film_transparent = True
scene.render.image_settings.color_mode = 'RGBA'
scene.cycles.samples = 48
scene.render.fps = 20
scene.frame_start, scene.frame_end = 1, FRAMES
for other in scene.objects:
    if other.type == 'MESH':
        other.hide_render = True

collection = bpy.data.collections.new('COL_VacuumGameplay')
scene.collection.children.link(collection)
source = bpy.data.objects['SM_Vacuum_LOD0']
material = source.data.materials[0]
palette_data = json.loads((ROOT/'ArtSource/BoardKit/palette-export.json').read_text(encoding='utf-8-sig'))
colors = [tuple(entry['m_color'][key] for key in ('r','g','b')) for entry in palette_data['m_boosterSwatches']]
assert len(colors) == 8
palette = bpy.data.images['T_BoosterPalette_BC']
palette.pixels = [value for y in range(16) for x in range(128) for value in (*colors[x//16],1)]
palette.pack()
body = bpy.data.objects.new('SM_VacuumGameplayBody', source.data.copy())
collection.objects.link(body)
removed = {g.index for g in source.vertex_groups if any(part in g.name for part in
    ('VacuumHose', 'VacuumWand', 'VacuumGrip', 'VacuumNozzle', 'VacuumFloorHead', 'VacuumHeadHinge'))}
indices = {v.index for v in source.data.vertices if any(g.group in removed for g in v.groups)}
bm = bmesh.new()
bm.from_mesh(body.data)
bm.verts.ensure_lookup_table()
bmesh.ops.delete(bm, geom=[bm.verts[i] for i in indices], context='VERTS')
bm.to_mesh(body.data)
bm.free()
for vertex in body.data.vertices:
    vertex.co.x *= .82
    vertex.co.y *= .82
    vertex.co.z *= .68
body.data.update()

def finish(obj, color):
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    collection.objects.link(obj)
    obj.data.materials.clear()
    obj.data.materials.append(material)
    uv = obj.data.uv_layers.get('UVMap') or obj.data.uv_layers.new(name='UVMap')
    for loop in uv.data:
        loop.uv = ((color+.5)/8, .5)
    return obj

def box(name, center, dimensions, color, bevel=.03):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.name = 'SM_'+name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    mod = obj.modifiers.new('Rounded molded edges', 'BEVEL')
    mod.width, mod.segments = bevel, 3
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    mod = obj.modifiers.new('Rigid weighted normals', 'WEIGHTED_NORMAL')
    bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(obj, color)

def cylinder(name, z, depth, radius, color):
    bpy.ops.mesh.primitive_cylinder_add(vertices=32, radius=radius, depth=depth,
                                      location=(0,0,z+depth*.5))
    obj = bpy.context.object
    obj.name = 'SM_'+name
    mod = obj.modifiers.new('Soft connector rim', 'BEVEL')
    mod.width, mod.segments = .015, 3
    bpy.ops.object.modifier_apply(modifier=mod.name)
    for polygon in obj.data.polygons:
        polygon.use_smooth = len(polygon.vertices) == 4
    return finish(obj,color)

# Broad upper mouth faces the board; blue inset and cyan lips reveal its opening.
cylinder('VacuumGripLower', 2.16, .11, .112, 3)
cylinder('VacuumGripUpper', 2.25, .22, .09, 3)
cylinder('VacuumNozzleSocket', 2.43, .10, .125, 1)
box('VacuumIntakeHead', (0,0,2.555), (.76,.32,.19), 0, .045)
box('VacuumIntakeSlot', (0,-.006,2.653), (.61,.205,.015), 1, .006)
box('VacuumIntakeBackLip', (0,.132,2.66), (.71,.044,.055), 7, .014)
box('VacuumIntakeFrontLip', (0,-.132,2.66), (.71,.044,.055), 7, .014)
for x in (-.354,.354):
    box('VacuumIntakeSideLip', (x,0,2.66), (.044,.30,.055), 0, .014)

RINGS, SIDES = 109, 20
BOTTOM, TOP = 1.25, 2.19
def hose_point(ring, side, phase):
    t = ring/(RINGS-1)
    travel = max(0,min(1,(phase-.18)/.64))
    center = 1.05-1.10*travel
    envelope = math.sin(math.pi*max(0,min(1,(phase-.10)/.83)))**2
    bulge = math.exp(-((t-center)/.16)**2)*envelope
    rib = .5+.5*math.cos(2*math.pi*18*t)
    radius = .065+.012*rib+.085*bulge*math.sin(math.pi*t)
    bend = .033*envelope*math.sin(math.pi*t)*math.sin(2*math.pi*t+phase*math.pi)
    angle = 2*math.pi*side/SIDES
    return (bend+radius*math.cos(angle),radius*math.sin(angle),BOTTOM+(TOP-BOTTOM)*t)

vertices = [hose_point(r,s,0) for r in range(RINGS) for s in range(SIDES)]
faces = []
for r in range(RINGS-1):
    for s in range(SIDES):
        a,b = r*SIDES+s,r*SIDES+(s+1)%SIDES
        faces.append((a,b,b+SIDES,a+SIDES))
faces += [tuple(reversed(range(SIDES))),tuple((RINGS-1)*SIDES+s for s in range(SIDES))]
mesh = bpy.data.meshes.new('SM_VacuumSuctionHose')
mesh.from_pydata(vertices,[],faces)
mesh.update()
hose = bpy.data.objects.new('SM_VacuumSuctionHose',mesh)
collection.objects.link(hose)
finish(hose,2)
for polygon in mesh.polygons:
    polygon.use_smooth = len(polygon.vertices)==4
hose.shape_key_add(name='Basis')
for sample in range(1,FRAMES-1):
    key = hose.shape_key_add(name='Swallow_'+str(sample).zfill(2))
    for ring in range(RINGS):
        for side in range(SIDES):
            key.data[ring*SIDES+side].co = hose_point(ring,side,sample/(FRAMES-1))
    for frame,value in ((sample,0),(sample+1,1),(sample+2,0)):
        key.value = value
        key.keyframe_insert(data_path='value',frame=frame)

camera = scene.camera
target = Vector((0,0,1.36))
camera.location = target+Vector((.28,-6,1.65))
camera.rotation_euler = (target-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.ortho_scale = 3.28
turn = bpy.data.objects.new('VacuumTurn',None)
scene.collection.objects.link(turn)
for obj in collection.objects:
    obj.parent = turn
def smooth(t):
    t = max(0,min(1,t))
    return t*t*(3-2*t)
for frame in range(FRAMES):
    yaw = math.pi*(1-smooth(frame/4)) if frame<4 else math.pi*smooth((frame-27)/4)
    turn.rotation_euler.z = yaw
    turn.keyframe_insert(data_path='rotation_euler',frame=frame+1)
scene.frame_set(1)
intake = world_to_camera_view(scene,camera,Vector((0,-.01,2.667)))
body_center = world_to_camera_view(scene,camera,Vector((0,0,.61)))
metadata = {'frames':FRAMES,'cell_pixels':CELL,'atlas_width':WIDTH,'atlas_height':HEIGHT,'duration_seconds':.75,
    'pixels_per_unit':75,'intake_local':[float((intake.x-.5)*2.56),float((intake.y-.5)*2.56),0],
    'body_center_local':[float((body_center.x-.5)*2.56),float((body_center.y-.5)*2.56),0],
    'runtime':'One SpriteRenderer; no Animator, SkinnedMeshRenderer, or render camera.',
    'authoring':'Upright gameplay pose; localized Blender Swallow shape keys on hose only.',
    'reference':'Reference 18: body below, rigid intake above, hose bulge during suction; colors retained.',
    'motion_inference':'One upper-to-lower bulge; exact timing authored, not measured from stills.',
    'entry_exit':'Baked 180-degree turn-in and turn-out; existing Unity scale tween retained.',
    'suction_frames':[4,27],'turn_degrees':[180*(1-smooth(i/4)) if i<4 else 180*smooth((i-27)/4) for i in range(FRAMES)],
    'palette_authority':'Assets/ColorBlockJam/Data/Art/GamePalette.asset via exported JSON',
    'astc_6x6_bytes':(WIDTH//6)*(HEIGHT//6)*16}

# Check closed topology, connector continuity and all-frame framing before rendering.
for obj in collection.objects:
    bm=bmesh.new(); bm.from_mesh(obj.data)
    assert all(e.is_manifold for e in bm.edges), obj.name
    assert all(v.link_faces for v in bm.verts), obj.name
    bm.free()
for sample in range(FRAMES):
    for endpoint in (0,RINGS-1):
        assert (Vector(hose_point(endpoint,0,sample/(FRAMES-1)))-Vector(hose_point(endpoint,0,0))).length < 1e-6
    scene.frame_set(sample+1)
    depsgraph=bpy.context.evaluated_depsgraph_get()
    for obj in collection.objects:
        evaluated=obj.evaluated_get(depsgraph)
        for vertex in evaluated.data.vertices:
            projected=world_to_camera_view(scene,camera,evaluated.matrix_world@vertex.co)
            assert .04<projected.x<.96 and .04<projected.y<.96, (obj.name,sample,list(projected))
scene.frame_set(1)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_location=target
            area.spaces.active.region_3d.view_distance=4
            area.spaces.active.region_3d.view_rotation=camera.rotation_euler.to_quaternion()
(EVIDENCE/'vacuum-bake.json').write_text(json.dumps(metadata,indent=2)+'\n',encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'VacuumMotion.blend'),compress=True)
atlas_data = array('f',[0])*(WIDTH*HEIGHT*4)
preview_only = '--preview-only' in sys.argv
for frame in ([16] if preview_only else range(1,FRAMES+1)):
    scene.frame_set(frame)
    path = EVIDENCE/('VacuumFrame'+str(frame-1).zfill(2)+'.png')
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    if preview_only:
        continue
    image = bpy.data.images.load(str(path),check_existing=False)
    rgba = array('f',[0])*(CELL*CELL*4)
    image.pixels.foreach_get(rgba)
    cell_x,cell_y = (frame-1)%COLUMNS,ROWS-1-(frame-1)//COLUMNS
    for y in range(CELL):
        offset = ((cell_y*CELL+y)*WIDTH+cell_x*CELL)*4
        atlas_data[offset:offset+CELL*4] = rgba[y*CELL*4:(y+1)*CELL*4]
    bpy.data.images.remove(image)
if not preview_only:
    atlas=bpy.data.images.new('VacuumSuctionAtlas',width=WIDTH,height=HEIGHT,alpha=True)
    atlas.pixels.foreach_set(atlas_data)
    atlas.filepath_raw=str(OUTPUT/'VacuumSuctionAtlas.png')
    atlas.file_format='PNG'
    atlas.save()
print('VACUUM_BAKE '+json.dumps(metadata))
