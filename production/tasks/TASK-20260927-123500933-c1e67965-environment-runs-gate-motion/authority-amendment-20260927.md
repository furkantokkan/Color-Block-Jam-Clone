# Approved wall-brick authority amendment

- User answered the concrete new Brick_End/model, EnvironmentDatabase, GamePalette and shared MAT_Gameplay target question: "oyuna uyarla onaylıyorum". Root explicitly directed this recorded task-change. Ready-contract authority was changed through that explicit approval, not through create-task or task-cycle's ordinary immutable-authority powers.
- This task gains only new `Assets/ColorBlockJam/Art/Models/WallBrick.fbx` (Brick_End mesh and new importer meta), new `Assets/ColorBlockJam/Prefabs/Board/Pieces/Brick_End.prefab` and meta, and the existing `Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset` binding. Preserve its existing meta/GUID. Blender source/export artifacts are `ArtSource/BoardKit/WallBrick.blend` and `export_wall_brick.py`.
- Minimal source expansion: `EnvironmentDatabase.cs`, the previously allowed composer/view/run helpers, and one dedicated `Editor/BoardKit/EnvironmentWallBrickAssets.cs`. This helper imports/saves exact approved targets only. Existing BoardKitAssets.Build regenerates the full kit and SharedArtAssets.Build also rewrites atlas/booster prefabs/JSON; neither may run.
- PRD/PROJECT exception is wall-only: reuse supplied Wall core and existing corners/door prefabs while authored Brick_End caps complete eligible straight runs. Preserve original FBX bytes, irregular topology, existing gameplay block mesh constraint, floor/void/door occupancy, supplied corners and seam-free joins. No palette/material path belongs to this task.
- User palette/material approval stays on original board-look D-001; no replacement color task. Shader, camera, lights, settings, packages, scenes, all other models/prefabs and atlas stay protected.
- One sequential production implementer owns these serialized targets. Stable Ice BoardPresenter release precedes integration; live import/binding/verification waits for Ice → Hub Editor releases and fresh identity. Both other task claims remain untouched. No code/assets/Editor/VCS mutation occurred in this amendment invocation.
- Current authority fingerprint: `sha256:9101dad0eee2f89558002d0e184a18ff92549549dfbaf1ac562757de4bcc617c`. Invocation permission was reconciled to retained user execution authorization without expanding targets. Evidence/review identities are cleared; attempt count remains 1 and ACs remain unproven. V5 reviewed patch is retained and must be rechecked after predecessor edits.
- Fresh Booster claim `TASK-20260927-004448215-54655902-booster-gameplay-flow`, owner token `31b8ef9237fd7d6b738283f2a883c282`, acquired 13:44:47Z, is an additional source/Editor dependency pending exact handoff. Ice releasing its checks alone is insufficient. The other owners' claims were read, never changed.

## Latest existing-art preference and explicit task-change

- User subsequently directed existing art plus the current rule-tile system first, with Blender editing only if still necessary. Root accepts this as the controlling solution direction; the earlier capped-core proposal and new-mesh exception are conditional fallback permission only, not required output. No new assets are needed by current staging-v4.
- A maximal straight run between gates/corners becomes one closed rounded brick; corners and gates remain distinct units. Old environment D-001 preservation now protects joins inside units and logical topology while permitting the requested visible inter-unit separations. Matched actual-scale captures remain necessary; neither groove acceptance nor an unmeasured flat roof is inferred.
- Root explicitly adds exact `Assets/ColorBlockJam/Environment/EnvironmentSolidComposer.cs` source ownership for internal AppendRectangle reuse only. Existing Barrier API and behavior are preserved. Other paths/serialized permissions are not expanded.
- Amendment invocation changed only contract/authority documentation under the task lock. No source/test/asset/Editor write, verification pass, attempt increment or closure occurred. Independent review waits for the staged implementation handoff.

## V6 user decisions (2026-09-27, Claude session [7336b1])

- Blender fallback used under the user's standing permission ("eğer hala gerekli olursa blenderdan edit yapma hakkın var"). The existing-art V5 bricks read as recessed trays in Game-scene captures. V6 caps straight runs with the approved Brick_End and keeps Compose's rule placements for the Level Editor canvas and barriers.
- Interior holes: "Outer ring only". AC-1 bricks the outer boundary (void connected to the board's outer ring, notches included). An interior hole keeps one continuous rule frame, as in project references 02/17.
- Preview test: "Approve test fix". Only `Assets/ColorBlockJam/Tests/LevelPreview/PreviewArrowMarkerTests.cs` is added, for its pool-reuse baseline after one warm-up cycle over both levels.
- Integration: "Everything now". BoardPresenter.cs, BoardPresenterGateTests.cs and this task's PROJECT.md/PRD edits are integrated despite Booster's live claim on those paths. Booster's copy-back merges them.
- Visual acceptance: "Accept both". Explicit user acceptance of AC-1 (walls) and AC-2 (gate motion) from the V6 captures.

## V7 user decision (2026-09-27, Claude session [7336b1], reopened for D-001/D-002)

- The user ordered both closing-report notes fixed ("bunlari da hallet") and then chose "All outer corners" for D-001 after being shown the exact targets.
- Added serialized targets, same sole-writer rules as Brick_End: a `Corner_End` mesh in `ArtSource/BoardKit/WallBrick.blend` and `Assets/ColorBlockJam/Art/Models/WallBrick.fbx` (both already owned), new `Assets/ColorBlockJam/Prefabs/Board/Pieces/Corner_End.prefab` and its new `.meta` (Corner_1.prefab contents with only the mesh replaced), and one `m_cornerEndPiece` binding in the owned `EnvironmentDatabase.asset`. The helper `EnvironmentWallBrickAssets.cs` builds and binds both pieces.
- Use: in `ComposePlayArea` only, every corner quadrant of the outer boundary (ring corners with floor only diagonally, and notch corners with floor on two adjacent sides) takes `Corner_End` (a corner with rounded arm ends), per the chosen "All outer corners"; `Compose`, the Level Editor canvas, barriers and interior-hole frames keep Corner_1. The supplied FBX, Corner_1.prefab and every other prefab stay unchanged.
- D-002 is repaired in the owned `EnvironmentComposer.cs` with the existing floor-base piece under every wall quadrant and door slot; no new target.

## V8 user decision (2026-09-27, Claude session [7336b1], reopened for D-003/D-004)

- The user asked for rounded gate ends and, if the existing art allowed it, corners like the reference crop. They answered "Approve gate ends" and "Reshape Corner_End" after being shown the exact targets. No supplied corner (Corner_1, corner_3, corner_4 or corner_5) has the reference's large arc, and Door_1x1 has flat ends.
- Added serialized target, under the same sole-writer rules: new `Assets/ColorBlockJam/Prefabs/Board/Pieces/Door_End.prefab` and its new `.meta` (Door_1x1.prefab contents with only the mesh replaced). The `Door_End` mesh goes in the already-owned `WallBrick.blend`/`.fbx`, with one `m_doorEndPiece` binding in the owned `EnvironmentDatabase.asset`. The existing `Corner_End` mesh is reshaped in place with a large quarter-round outer arc, so no new corner file.
- Use: in `ComposePlayArea` only, each gate span has `Door_End` at both ends (two half-length ends on a one-slot gate) and `Door_1x1` between them. All of these pieces stay in the gate's span and move together. `Compose` and the Level Editor canvas keep `Door_1x1`. The supplied FBX and Door_1x1.prefab are unchanged.

## V9 user decision (2026-09-28, Claude session [7336b1], reopened for the minimum unit size)

- The user asked for walls around a single void cell to read as one piece, then clarified: "tek block minimum ... yarım block merge olacak". So no wall unit is shorter than one cell, and a half-cell unit merges into its neighbour. They answered "All corners" and "Yes, add Corner_Join".
- Rule, `ComposePlayArea` only: every outer-boundary corner merges into its shorter neighbouring straight run. The corner becomes `Corner_Join` (the arc corner with its joined arm flat and its other arm rounded, mirrored by a negative X scale when the other arm joins). The run's end next to it becomes a plain rule `Wall` instead of a `Brick_End` cap. A corner with no neighbouring straight run stays `Corner_End`.
- Added serialized target, under the same sole-writer rules: new `Assets/ColorBlockJam/Prefabs/Board/Pieces/Corner_Join.prefab` and its `.meta` (Corner_1.prefab with only the mesh replaced). The mesh lives in the owned `WallBrick.blend`/`.fbx`, with one `m_cornerJoinPiece` binding in the owned `EnvironmentDatabase.asset`. Compose and the Level Editor canvas are unchanged. Tests run once at the end, per the user.

## V10 user corrections and folder split (2026-09-28, Claude session [7336b1])

- Corner rule narrowed by two user corrections to the V9 captures. "Köşeler hatalı olmuş buralar eskisi gibi tek block olacaktı": the board's own corners stay separate `Corner_End` units. For the notch's inner corner, "köşelerdeki gibi iki taraflı block olabilir": it stays `Corner_End`, rounded on both arms. Final rule, `ComposePlayArea` only: a ring corner (floor only diagonally) in or beside a one-cell void merges into its shorter straight run as `Corner_Join`. A one-cell void is an outer void cell inside the board with no void cell beside it. A one-quadrant run joined at one end becomes one full `Brick_End` whose mouth meets the corner. Every other corner stays `Corner_End`. This supersedes the V9 "every outer-boundary corner" wording.
- Folder split, user answer "Split supplied/added" to "rollicin verdiği modelleri kullanıyoruz değil mi klasörleri de düzenle ve ek olarak ne ekledik ve neden ekledik". The owned project-authored files move, with GUIDs and content kept, as Plastic moves:
  - `Art/Models/WallBrick.fbx` (+`.meta`) moves to `Art/Models/BoardKit/WallBrick.fbx` (+`.meta`).
  - `Prefabs/Board/Pieces/{Brick_End,Corner_End,Door_End,Corner_Join}.prefab` (+`.meta`) move to `Prefabs/Board/Pieces/WallBrick/`.
  - Added targets: the new folder metas `Art/Models/BoardKit.meta` and `Prefabs/Board/Pieces/WallBrick.meta`, and `docs/art/wall-brick/README.md` plus `docs/art/wall-brick/WallBrick.png` (a level-1 capture). The README states what Rollic supplied, what the project added and why.
  - The helper's path constants and `export_wall_brick.py` follow the new paths.
  - Rollic's supplied files, their prefabs and every other asset stay where they are, unchanged.
- Allowed paths and serialized targets in the contract are rewritten to the new locations. The old locations are no longer owned once the moves land.
