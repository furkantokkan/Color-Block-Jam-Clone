# Solid obstacle art

- Reference: the user's 2026-09-25 screenshot of the smooth dark obstacle above
  a cyan block, plus `docs/Color-Block-Jam-Visual-References.md` reference 17.
- The reference project's `EnvironmentSpawner.SpawnInnerObstacle` instantiates
  `EnvironmentData.InnerObstaclePrefab` once per obstacle cell. This project
  instead joins cells through its existing quadrant-based wall composer.
- Author with **Level Editor > Obstacles > Block**. Drag to paint; right-drag
  or use Eraser to remove. Each stroke is one Undo/Redo step. The footprint
  is a typed `solid-block` cell object in the existing JSON v2 format.
- Exterior walls are generated automatically from the board shape. There
  is no Board boundary group or Wall brush. Existing `voidCells` still
  describe board cutouts. Gates use Pen on the board edge. Both retain the
  supplied WallAndDoor art and existing prefabs. Obstacles is reserved for
  Block and future board objects such as wooden crates.
- Block cells remain visible floor in the layout and become blocked floor
  in the attempt model. They reject overlaps with pieces, gates and other
  objects. The canvas, scene preview and runtime use the same footprint.
- Supplied `Wall` and `Corner_1` meshes provide the rounded boundary. Interior
  quadrants use `Prefabs/Environment/BlockFill.prefab`: the existing FloorBase
  quad, at half-cell scale, seated at the Wall's inner edge (0.66764). The
  rounded wall crest is 0.72; putting the cap at that crest would leave a slit
  above the inward bevel. The supplied kit's grooves remain visible.
- `EnvironmentDatabase.asset` registers the fill with the same Onity pools.
  All parts use `BoardPieceView`, `MAT_Gameplay`, BoardLit, the shared palette
  atlas and the owner's Wall tint. The fill adds two triangles per quadrant;
  there is no runtime mesh generation, material clone or footprint prefab.
- Barrier uses the same fill, so its top follows the existing rise/sink,
  pause, hide and release behavior. This does not change the Barrier rule.
- Rebuild the owned prefab with **Color Block Jam > Art > Prepare Solid
  Obstacle Fill**. The builder preserves its GUID and refuses a read-only
  target, an open target prefab stage or unexpected custom root content.

Verification is recorded in the solid-obstacle task's `verification.md`.
The isolated board render is `solid-blocks.png`; it is a composition study,
not a claim of final Game-scene lighting or measured device performance.
