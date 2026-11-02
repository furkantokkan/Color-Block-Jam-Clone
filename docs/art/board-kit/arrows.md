# Arrow prefab preparation

- Scope: three supplied arrow meshes and visual prefabs only. The user clarified
  that block effects will be assigned through Effect Data; no effect schema,
  automatic selection, movement rule, presenter or new runtime pool was added.
- Assets: `Assets/ColorBlockJam/Prefabs/Board/Pieces/Arrow_1.prefab`,
  `Arrow_2.prefab`, `Arrow_3.prefab`; existing GUIDs and `Arrows.fbx` mesh references
  preserved. Shared `MAT_Gameplay` and cached `BoardPieceView` property blocks remain.
- Appearance: white `GamePalette.ArrowColor`, centered X/Z, local Z orientation,
  unchanged uniform mesh scale 0.48, underside 0.025 units above the LEGO studs,
  cast shadows disabled. Each source mesh has 394 triangles; no material or mesh
  duplication, animation or per-frame work is introduced.
- Authoring: `BoardArrowAssets.Build` is a narrow Editor command; it checks writable
  paths and open Prefab Mode, edits isolated prefab contents and checks save results.
  `BoardKitAssets.Build` reapplies the same settings on a full kit regeneration.
- Visual evidence: [ArrowPrefabs.png](ArrowPrefabs.png), rendered in an isolated
  Unity preview scene and checked against the user's white double-arrow screenshot.
- Ownership: root was the sole serialized writer; no user scene, package, level
  data, original FBX, shape definition or shape prefab was changed. The temporary
  presenter/layout/test files created before the scope clarification were removed.
- Verification: original GUID/source-reference and geometry readback in
  `arrows-before.json` / `arrows-after.json`; focused `BoardKitTests` result in
  `test-results/arrows-editmode.json`: **18/18 passed**, job
  `5d1601a7405e4f1291d567d5b2147a06`. Independent source/asset review passed.
  The initial unfocused Editor test request timed out before any test started;
  the successful retry is the retained result. Existing level Addressables startup logs
  are unrelated to this visual setup; no whole-game launch claim is made.
