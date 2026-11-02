# Shared opaque art

Updated 2026-09-25 from the user's original-game screenshots. The supplied meshes
and booster motion remain unchanged. Colors are more saturated, especially red,
purple and blue; the shared Blinn–Phong highlight is broader and softer.

## Rendering and authoring

- Blocks, ground, walls, gates, arrows and the Hammer and Rocket booster model LODs
  (the 3D Vacuum model was deleted on 2026-09-28; the Vacuum is a baked sprite) reference
  `Art/Materials/Shared/MAT_Gameplay.mat` and `Art/Shaders/Board/BoardLit.shader`.
- `Data/Art/GamePalette.asset` has one `Colours` mapping and Arrow Cell; the
  **128 x 32 RGB24** `Art/Textures/Atlases/T_GameplayPalette.png` holds the pixels.
  Rows 2-3 contain authored colors and their light variants. As of 2026-09-30,
  rows 0-1 are generated from Colours for the eight fixed Blender UV roles;
  there is no separately authored Booster Swatches list.
- Booster UVs map to the lower half through an instanced scale/offset. Board
  meshes select an atlas cell directly through `_PaletteUV` (DEC-202).
  No FBX/UV edits, generated runtime textures or material clones.
- One cached `BoardPieceView` property block owns tint, UV selection and clipping.
  Color/depth/normal/shadow passes retain the same clipping. Pool return resets
  clipping and tint without losing the neutral UV selection.
- **Color Block Jam > Art > Update Shared Art Material** refreshes only the
  lower two atlas rows from Colours, preserves the authored upper rows, exports
  the Blender palette and binds the booster prefabs. Blender rebuilds keep their local
  authoring texture outside Assets; the Unity import helper reapplies this setup.
- The opaque booster models receive the same lighting/shadows as the board.
  Their shadow casting, probes, triangle counts, exclusive LODs and motion are
  unchanged. The unused `MAT_BoosterPalette.mat` was removed after a dependency scan.
- Vacuum's approved transparent 32-frame bake keeps its separate sprite material
  and sheet. Combining baked lighting and alpha blending into the opaque atlas
  would lose its presentation and depth behavior. Ice/Time Freeze remain separate.

## Evidence

Palette cleanup verified on 2026-09-30: Roslyn compiled all 24 affected/dependent
assemblies; `SharedArtTests` and `BoardKitArtAuditTests` passed 6/6 EditMode tests
(job `93e3816b6f1e408f9e50f9684082738f`). The authored upper atlas pixels were
preserved. Booster tones now reuse Colours; the phone appearance was not checked.

The [before](BEFORE.png) and [after](AFTER.png) renders use identical temporary
staging, camera and lights. They are material studies, not authored playable
levels. The stage is disposed after capture; no scene is saved.
Reproduce the after image with `ArtSource/BoardKit/render_shared_art.cs.txt` in
the pinned Editor.

| Measure | Before | After |
| --- | ---: | ---: |
| Referenced opaque materials / shaders | 2 / 2 | 1 / 1 |
| Color atlas | 128 x 16 | 128 x 32 |
| RGB24 pixel payload, no mipmaps | 6 KiB | 12 KiB |
| Transparent Vacuum materials | 1 | 1 |

The atlas is point-filtered, clamped, sRGB and CPU-readable (DEC-202). Android/iOS use
uncompressed RGB24 to preserve tiny swatches without compression bleed. The
283-byte PNG is source storage, not GPU memory; the Editor reports 29,352 bytes
including texture object/storage overhead. One shared material does not combine
different meshes or passes into one draw. The board now adds one small atlas
sample per shaded pixel; lower device frame time has **not** been established.
NFR-P5's Frame Debugger and phone CPU/GPU comparison remains required.

- EditMode: **23/23 passed**, including shared bindings, atlas contents/settings,
  tint/clip/pool reset and existing piece authoring tests.
- PlayMode: **14/14 passed**, covering booster contact, pause/replay/cleanup,
  Vacuum frame/rig constraints and Onity board-pool lifecycle.
- Shader compiled without messages; open scene remained clean.
- After the final prefab shadow-receiving alignment, the three shared-art
  EditMode tests passed again: [final run](../../../test-results/shared-art-final-editmode.json).
- Separate shared-Editor runs of `ShapePrefabTests` reported file-replacement
  I/O failures during this session. They are not included in the passing counts
  above; no full-project test pass is claimed. The captured
  [unrelated job status](../../../test-results/shared-editor-unrelated-test-status.json)
  retains the failing test name for its authoring owner.
- Results: [EditMode](../../../test-results/shared-art-editmode.json),
  [PlayMode](../../../test-results/shared-art-playmode.json),
  [asset audit](audit.json).
