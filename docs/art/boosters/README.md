# Booster asset report

Created 2026-09-24 with Astra in Blender 5.1.2 from the supplied `ic_vacuum.png`,
`ic_rocket_purple.png`, and `ic_hammer.png` icons. The supplied files are unchanged.

## Delivery and use

- `Assets/ColorBlockJam/Prefabs/Boosters/Models/{Hammer,RocketPurple}.prefab`: use these
  prefabs for a rotating/moving 3D prop. Each contains two exclusive LOD levels.
  Dragging the raw FBX into a scene bypasses the material and LOD configuration.
- `Assets/ColorBlockJam/Prefabs/Boosters/Models/Vacuum.prefab` uses the baked sprite
  animation described below; its retired 3D model is not a runtime LOD target.
- `ArtSource/Boosters/Boosters.blend`: editable meshes, shared packed palette,
  render camera and studio lights. Initially only Vacuum LOD0 is visible;
  reveal the desired named collection/object to edit another asset.
- `ic_hammer.png`, `RocketPurple.png`, `ic_vacuum.png`: 768 x 768 transparent Blender
  renders. `_LOD1` and `_Back` files are inspection views; `BoosterLineup.png`
  is the combined presentation. These sources remain outside runtime Assets.
- `Unity_*_LOD0.png` and `Unity_*_LOD1.png`: actual imported-prefab previews,
  rendered separately with forced LODs in a temporary Unity preview scene.

The [motion delivery](motion/README.md) uses these 3D models for Hammer's swing
and Rocket's flight. Vacuum keeps its flat presentation through 32 frames baked
from Blender shape-key animation, avoiding runtime skinning. Keep the supplied
sprites for fixed UI. If adopting
the new renders into UI, size them to their actual display footprint (typically
256/512), import as fixed Single sprites under the project's UI policy, and pack
them into the owning screen atlas. No live RenderTexture or per-frame camera is
needed for these offline images. Reusable motion prefabs are delivered separately
from future game-session/domain integration.

## Measured asset budget

| Asset | LOD0 tris / Unity vertices | LOD1 tris / Unity vertices | FBX, both LODs |
| --- | ---: | ---: | ---: |
| Hammer | 1,900 / 1,280 | 940 / 640 | 91,708 B |
| Purple Rocket | 2,972 / 1,824 | 1,228 / 792 | 123,596 B |
| Vacuum | 5,872 / 3,786 | 2,936 / 1,942 | 252,972 B |

All three together: **10,744 LOD0 triangles**, or **5,104 LOD1 triangles**.
Imported vertices include normal/UV splits and differ from authoring vertices.
Per-model budgets were 2,200 / 3,000 / 6,000 LOD0 triangles respectively.

- One mesh, one submesh and one material slot per visible model. Since the
  2026-09-25 shared-art update, all LODs reference `MAT_Gameplay` and Board Lit,
  also used by blocks, walls, gates, ground and arrows. Instancing is enabled;
  actual draw calls depend on meshes and passes and remain unmeasured on device.
- Palette: **128 x 32 RGB24**, 12 KiB raw pixel payload. The lower row retains
  the eight Blender colors; the upper white row supports dynamic board tint.
  Original booster UVs are remapped by the shader without changing the FBX.
  See the [current comparison and audit](../shared-art/README.md). Point filtering, Clamp,
  sRGB, no mipmaps, Read/Write off. Uncompressed tiny swatches preserve exact
  colors and avoid block-compression bleed.
- UV overlap is deliberate: faces sample the center of padded color swatches.
  No normal maps, tangents, lightmap UVs, or high-resolution textures.
- Import: normals preserved, vertex/index optimization on, Read/Write off,
  no animation, cameras, lights, colliders, or imported materials. Mesh compression
  stays off to preserve rounded edges; counts are already bounded.
- Prefabs: LOD0 above 2.5% screen height; LOD1 down to 0.1%, then culled. No
  crossfade, so the two levels are not blended/drawn together. Shadow casting
  and reflection/light probes are disabled for these short-lived effects.
  Shared opaque lighting now includes shadow receiving, matching board art.

## LOD verification, 2026-09-30

- The old 18% LOD0 threshold selected LOD1 during normal close gameplay. The
  effect prefabs scale the models to 0.55; native Unity LOD measurements across
  the six current levels, 1080 x 1920 and 1080 x 2340 framing, and fully shown
  motion poses measured about 7.28-12.51% screen height. The game camera uses
  perspective, FOV 60 and pitch 75.
- Both model prefabs and `ArtSource/Boosters/import_boosters.cs.txt` now use
  0.025 / 0.001. With Performant's LOD bias 0.4, LOD0 lasts down to 6.25% and
  culling starts below 0.25%; Balanced uses 2.5% / 0.1%, and High Fidelity uses
  1.25% / 0.05%. This keeps full-size casts detailed while retaining LOD1 and
  culling for tiny scale-in/out poses. Each visible LOD remains one renderer.
- Unity 2022.3.62f2 verification passed: 1,080 native LOD checks across six
  levels, two aspect ratios, two models, three quality biases, three target
  positions and five fully shown poses; 36 before/after diagnostic renders;
  and 18 small-size LOD0/LOD1/cull checks. Temporary previews kept the gameplay
  FOV: `PreviewRenderUtility.Render(true, false)` disables its automatic
  portrait-preview FOV widening. Profile biases were simulated on temporary
  LOD thresholds without changing the project's quality settings.
- Generated evidence: `Temp/BoosterLodAudit/framing-check-final.json` and
  `Temp/BoosterLodAudit/native-render-check-final.json`. Prefab diffs contain
  only the two thresholds. Runtime C# and meshes did not change; no Test Runner
  run was needed. This verifies Editor LOD selection, not device GPU timing.

## Scale, geometry and verification

Blender uses meters; FBX exports at scale 1 with Unity Y-up. Prefab root and child
transforms are identity. Hammer pivots at its lower grip for a swing; Rocket and
Vacuum have a bottom-axis/body-base pivot. Approximate Unity bounds before a
presentation scale: Hammer 2.00 x 2.26 x 1.30, Rocket 1.18 x 2.21 x 1.18,
Vacuum 2.23 x 2.79 x 1.38. Apply a uniform scale on the effect owner as needed.

Blender checks pass for triangle budgets, unit scales, one material, zero loose
vertices, zero degenerate triangles and zero non-manifold component edges.
The molded parts are intentionally overlapping closed components, not a boolean
union; these are visual props, not collision meshes. No physics is attached.

Reference review retained the green/orange mallet, purple/gold rocket bands and
fins, and cyan/yellow vacuum with a purple loop and blue wheels. A second pass
fixed the Vacuum side-cover intersection, widened the nozzle, softened gloss,
and corrected the Rocket preview angle. LOD1 preserves the identifying shapes.

Unity 2022.3.62f2 checks pass for both imported meshes, normals/orientation,
material references, exclusive LOD groups and import settings. Forced-LOD previews
were inspected. Static-model verification did not run Play Mode or a build;
the later motion delivery has its own focused PlayMode verification.
No booster-related console warnings/errors were found. Device frame time, final
lighting, pool ownership and game-session integration are follow-up work.

Machine-readable evidence: [Blender geometry](mesh-report.json) and
[Unity import/preview checks](unity-report.json). Editor memory measurements are
evidence for this import, not a claim about the final Android/iOS player.

## Regeneration

From the repository root, run Blender in a fresh background process:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' --background --factory-startup --python-exit-code 1 --python ArtSource/Boosters/build_boosters.py -- --render
```

The generator overwrites only its named booster exports, Blender source, renders,
and mesh report. Omit `--render` to rebuild meshes without preview images. In the
exact project's Unity Editor, execute the method body in
`ArtSource/Boosters/import_boosters.cs.txt`, then
`ArtSource/Boosters/verify_boosters.cs.txt` through the authorized Editor transport.
The import helper owns only these new booster assets; the verifier generates
preview/report evidence and closes its temporary preview scenes.

The Blender authoring texture stays in `ArtSource/Boosters`. Unity's **Color
Block Jam > Art > Update Shared Art Material** owns the runtime atlas derived
from `GamePalette`; the import helper calls it before binding model LODs. The
approved Vacuum motion sheet remains a separate transparent bake.
