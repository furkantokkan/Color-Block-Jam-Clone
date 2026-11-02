---
name: board-piece-authoring
description: Create or edit ColorBlockJam Piece Database JSON footprints with the grid Piece Editor and update the existing runtime library and derived prefabs.
---

# Board piece authoring

Work in this repository. Read `PROJECT.md` and `docs/art/board-kit/README.md`
for current ownership and geometry calibration. Use the installed Unity CLI
preflight and the project's approved Unity 2022 live-Editor transport. Confirm
the exact project before any asset write; preserve unsaved scenes.

## Add a shape

1. Open **Color Block Jam > Level Editor** and use its **Piece Editor** tab (the
   menu has no separate Piece Editor item). The 11-piece base-shape JSON is the
   initial **Piece Database**. Choose **+** beside it, name the new database in
   the popup and choose **Create** to copy the active records. The read-only
   JSON Source field shows the selection.
   Creating or selecting a database binds its JSON to the existing runtime
   library and prepares enum/prefab outputs. The X beside the selector deletes a
   selected non-default JSON after confirmation and reselects BlockShapeLibrary.
   **New piece** starts an unsaved draft.
2. The grid is a fixed 7×7 area around cell (0, 0). Paint connected cells with
   left-drag; erase with right-drag. The independent cross marks the grid's visual center.
   X grows right and Y up. Grid fill and preview share the selected palette color.
   **Ctrl+Z** undoes and **Ctrl+Shift+Z** redoes grid edits. Each paint/erase drag
   is one step; Clear and Rotate are also undoable.
   Text fields retain their own text undo. Saving or changing the selected piece
   resets draft history. Saved JSON uses library Undo.
3. Enter a unique piece **Name**. **Save piece** below the grid derives a PascalCase
   type label (for example `Long Corner` becomes `LongCorner`; the editor keeps it
   internal and lists numbered names such as `6. L Shape`), appends a numeric
   ID within the selected database and saves the trimmed footprint. Saves update
   the enum and derived prefab. **Delete selected piece** removes its JSON record
   after confirmation.
   **New piece** and **Delete selected piece** are above the grid;
   **Save piece**, **Clear grid** and **Rotate 90°** are below it.
   Updating geometry/display name preserves the record's ID and type label.
   Switching away from edited drafts asks before discarding. Empty, disconnected or
   duplicate cells/types are rejected.
   When the Level Editor opens a level-only piece, the Piece Editor shows its
   name and has no Save piece button: each edit reaches the level at once
   (`LevelPieceChanged`) and writes no library data. The Level Editor's
   **Save Level** adds the level's new pieces through `TryAddPieces`, and
   **Save as template** adds one at once. Never run Save Level with new pieces
   or Save as template for the designer; both write the enum, prefabs and
   piece JSON.
4. JSON files live in `Assets/ColorBlockJam/Data/Json/Pieces`.
   The active file is the existing `BlockShapeLibrary.asset` TextAsset reference;
   no second ScriptableObject is created. Place external JSON files in this folder
   and select them after Unity imports them. Save rejects a file changed since its
   loaded snapshot; reopen Piece Editor before saving external changes.
   Preserve existing numeric IDs, enum names, prefab GUIDs and bindings when
   switching databases. Do not modify unrelated runtime or scene assets.
   Rotated footprints are authored in **Level Editor** by selecting a block,
   choosing its base Piece Type and using **Rotate Block 90°** or
   **Flip Block Horizontally**. The level JSON keeps the base type and stores
   the transformed cells.
5. Run focused `BoardKitTests` and `ShapeLibraryJsonTests`; preview the shape in
   all four rotations and its mirrored orientations. Record the database path,
   ID, cells and verification.

Runtime roots must come from `BlockPieceFactory`, implementing Onity
`IFactory<BlockPieceSpawnData, BlockPiece>` with `PooledFactory` and
`PrefabComponentPool`. The attempt owner calls `Prepare(type, count, cellCapacity)`
before Playing, then `Create`/`Return`, and `Dispose` at teardown. Capacity is a
hard bound; neither root checkout nor footprint changes grow a prepared pool.
Pass resolved level cells in the spawn data when present; they are copied without
normalizing away their offsets. `BlockPiece.SetShape`/`SetCells` change attempt
state only. `BlockPiece.TryRemoveCell` was removed (DEC-178); a Rocket split
part is redrawn through `SetCells`/`TrySetCells` (DEC-146), which reject empty
or disconnected survivors. Gameplay still owns targeting, occupancy and
booster consumption. Never mutate SOs at runtime.
Do not call `BlockPiece.Release` on ordinary pool return: hooks reset cells,
type, tint, clipping and transform while retaining warm sub-pools.

## Color or geometry changes

- Author colors in `Assets/ColorBlockJam/Data/Art/GamePalette.asset`. Block/gate
  keys use `PaletteColor`; eight ordered booster swatches are separate art roles.
  **Art > Export Shared Palette for Blender** writes a derived JSON snapshot.
  Baked sprites need a rebake after changing their swatches.
- Prefabs live under `Assets/ColorBlockJam/Prefabs/Board`. Use only the supplied
  FBX meshes, without drawing/remodeling blocks or changing their geometry.
  One gameplay cell spans 2×2 source subtiles, giving four LEGO studs. Tile
  prefabs duplicate and rotate whole supplied children. Never use `corner_3.002`.
- **Art > Build Board Kit Assets** recreates generated prefabs/catalog references;
  it preserves existing palette/shape data. Use it only when those exact generated
  asset replacements are within the user's authorized scope.

Keep scene placement, game rules, package changes, and device-performance claims
outside ordinary shape authoring. This skill does not authorize checkins.
