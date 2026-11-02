# Solid block / board boundary verification

- Unity 2022.3.62f2; exact project `ColorBlockJamCase2026@99077e4df1ecfaa4`,
  HTTP MCP 8082 under the repository's pre-Unity-6 exception. CLI status
  confirms no Pipeline instance; the Unity process targets this workspace.
- EditMode job `c6665636d5e743e6b267cd9f15ff1f63`: **233 passed, 0 failed,
  0 skipped**. Full results: `editmode-results.json`.
- Coverage: Wall/Gate source mesh identity; solid single, run, square, wide,
  L and holed footprints; cap material/mesh/pool registration; preserved
  boundary/gate outputs; 0 B warmed composition; automatic walls with no
  manual boundary/Wall controls and a separate Block palette item;
  stroke Undo/Redo, erasing, overlap rejection, JSON v2 runtime round-trip,
  matching canvas geometry and blocked attempt-model floor.
- Live Editor check (`editor-ui.json`): no boundary group, no Wall brush,
  Block brush present, compilation finished and the scene remains clean.
- PlayMode job `b63c7ed3a00f4dda9b15c6075731e3cc`: **15 passed, 0 failed,
  0 skipped**. Full results: `playmode-results.json`. Covers static Block
  staying raised while a Barrier opens, wide-cap animation, suspend/resume,
  teardown and pool reuse, plus existing environment lifecycle checks.
- Art acceptance: the user said "güzel" after seeing the solid obstacle,
  then requested separate Wall and Block authoring. The later request moves
  Wall out of Obstacles, then removes the boundary control entirely; this
  changes authoring/data semantics, not the
  accepted kit silhouette. Reproduction: `capture.cs.txt`; final render:
  `docs/art/solid-obstacles/solid-blocks.png`, visually checked after the
  split. Interior caps meet the bevel; the supplied mesh grooves remain.

## Implementation note

- `LevelDatabaseWindow` shows only Block in Obstacles, with no manual Wall
  brush or Board boundary group. Exterior walls are automatic; Pen still
  paints gates on the board edge. Existing Wall, Corner,
  Door and Arrow prefabs retain the supplied `WallAndDoor.fbx` meshes.
- `LevelObstacleCommands`, the canvas model and typed `SolidBlockObject`
  persist permanent obstacles independently from `voidCells`. The attempt
  converter blocks their cells; the loader passes authored data to the view.
- `EnvironmentSolidComposer` shares boundary matching and interior caps
  between static Block and the existing Barrier view. `BlockFill.prefab`
  reuses the existing quad and shared material through Onity pools.
- Original FBX, scenes, packages and settings were not changed by this task.
  Device performance and final Game-scene lighting were not measured here.

## Verification dependencies

In addition to the contract's owned paths, the checks consume the existing
Environment prefab, rule set, floor/base, Wall/Corner/Door/Arrow prefabs,
GamePalette/MAT_Board, WallAndDoor.fbx and their references. Existing
`EnvironmentBarrierComposerTests`, `EnvironmentBarrierViewTests`,
`EnvironmentViewLifecycleTests`, `BoardPieceView`, `BoardLayout`, level JSON
and the canonical shape library provide preservation fixtures.
`review-inputs.json` lists the exact reviewed files, including serialized
dependencies returned by Unity in `editor-verification.json`. Contract
fingerprinting excludes its managed lifecycle block; review hashing excludes
operational claim/lock files. Paths and raw bytes are hashed in ordinal order.
