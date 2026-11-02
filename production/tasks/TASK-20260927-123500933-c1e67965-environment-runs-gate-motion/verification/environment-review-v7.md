# V7 repair: D-001 corner end and D-002 wall bases (Claude session [7336b1], Opus 5.5, 2026-09-27)

The user asked to fix the two notes left in the V6 closing report ("bunlari da hallet"). They reopened the task as defects D-001 and D-002 (both TASK_REGRESSION). For D-001 the user chose "All outer corners" after seeing the exact new targets (authority-amendment-20260927.md, V7).

## What changed

- **D-001 Corner_End.** `implementation/approved-brick/build_corner_end.py` (Blender 5.2) adds `Corner_End` to `ArtSource/BoardKit/WallBrick.blend`, next to an unchanged `Corner_Reference`.
  - It has Corner_1's exact mesh bounds and its outer rounded corner (base radius 0.30).
  - Every side drops with the supplied Wall bevel, like Brick_End's closed end, and the other three corners use Brick_End's radius (0.26).
  - `export_wall_brick.py` exports Brick_End and Corner_End with the kit's FBX unit scale and checks both round trips.
  - `EnvironmentWallBrickAssets` checks both meshes' bounds before it writes anything. It then saves `Corner_End.prefab` from Corner_1.prefab with only the mesh replaced, and binds `m_cornerEndPiece`.
  - Brick_End.prefab is byte-identical after the rebuild.
  - `ComposePlayArea` places Corner_End, with the corner rule's yaw, on every corner quadrant of the outer boundary (ring and notch corners). `Compose`, the Level Editor canvas, barriers and interior-hole frames keep Corner_1.
- **D-002 wall bases.** `ComposePlayArea` puts one `FloorBase` under every wall quadrant and door slot, in the floor-base colour, just under floor level.
  - Each base reaches every side that meets floor, a wall or a gate, and stops 0.25 quadrant short of a side that faces open void.
  - Gaps between units, beside rounded corners and under gate fronts therefore read dark. No base shows outside the board or inside a hole.
  - The first two rules were refuted by review and replaced: bases only on the floor side left pinch joins open, and notch bases had been on the wrong side.
- **Tests:**
  - EnvironmentWallRunTests: Corner_End replaces only outer corners, and hole corners keep Corner_1 (exactly 6 on the notch/hole fixture). One base per wall quadrant and door slot, with each side checked against its neighbour. Straight bases reach the floor edge. Compose places no Corner_End and no wall base.
  - EnvironmentViewLifecycleTests: the counts include the 32 bases, and Corner_End and its base sit at the ring corner.
  - EnvironmentGateTests: the recorder warm-up no longer asserts on its first run.

## Verification

| Channel | Result |
|---|---|
| Isolated copy, final code (v7d) | EditMode 1563/1565: one skip is an `[Explicit]` test; `ShapePrefabTests.JsonImportAndUndoRestore…` is order-dependent and passes 31/31 alone (`v7d-shape.xml`). PlayMode 135/139: the pre-existing `BoardArtContainerTests` "2 audio listeners" failure plus 3 designed capture skips. Environment EditMode 90/90 and PlayMode 24/24 after the wording and test-name changes (v7e). |
| Integrated proof: fresh mirror of the shared workspace at 21:24, 34 owned files byte-identical | EditMode 1575/1576 (`int7-editmode.xml`, one `[Explicit]` skip). PlayMode 135/139 (`int7-playmode.xml`, the same pre-existing failure and designed skips). |
| Background enclosed by the board (flood fill from the image edge), 1080x1920 / 1080x2400 | L1 idle: V6 456 → 0 / 0. L1 gate lowered: V6 4307 → 0. Notch 566 → 0, pinch 929 → 0, spans 566 → 0, corridor 321 → 0, levels 1-5 and level 5 gate down 0, new camera-side notch 0. The hole fixture shows only its intended 2x2 void. |
| D-001 visual | `captures-v7/L1-05-root-returned`, top-right corner beside the lowered gate, and `fx-level-05-gate0-down`, top-left corner: a rounded closed end, not a cut face. |
| Independent review | `v7-corner-base-review` workflow (code and visual lenses with adversarial verify) found the notch-base side, pinch, hole-frame and gate-line spots, plus doc and test items; all were fixed. A final independent verifier on v7d found no code or visual defect and flagged the PROJECT.md drift below, now resolved. |
| Sheets | `v7-corner-end-and-bases.png` (V6 vs V7: gate corner, joins, gate line, notch, pinch). |

## Integration

- `integrate_v7.py` has a drift guard, and 15 files were written byte-identical to the copy (`staging-v7/`, `staging-v7-sha256.txt`).
- Another session changed `PROJECT.md` after the mirror (DEC-140). Only this task's two DEC-139 hunks were merged into that live version (live sha256 62cc8347…); DEC-140 and DEC-138 were kept.
- `environment-gate-v7.patch` holds the text diffs (V6 → V7).
- The Booster session got a merge heads-up for PROJECT.md and the PRD. BoardPresenter files were not touched.

## Not done / notes

- Android/iOS builds and device profiling were not run.
- The capture harness names `L1-06-rising` after the same frame as `L1-05`; the real rise frames are seq-050…058.
- The filled joins show the floor-base colour, the same as the tile-corner stars, which reads as a seam.
