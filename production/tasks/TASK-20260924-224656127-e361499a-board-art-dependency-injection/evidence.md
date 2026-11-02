# Evidence: TASK-20260924-224656127-e361499a-board-art-dependency-injection

Implementer run on 2026-09-25 (Plastic workspace cs:7 plus uncommitted work; no checkin).
Live Editor: MCP for Unity HTTP 127.0.0.1:8082, instance ColorBlockJamCase2026@99077e4df1ecfaa4.
A Codex session (TASK-20260925-160721096-2c8da893-solid-obstacle-art, started 19:08) edited
Environment/EnvironmentDatabase.cs, EnvironmentComposer.cs, EnvironmentBarrierComposer.cs,
EnvironmentView.cs, EnvironmentDatabase.asset, docs/Environment-Layout.md and PROJECT.md at the
same time. It kept every line of this task (tint arguments, floor/wall colours) and built on them.

## Implementation (contract Revision 2 applied)

- `Presentation/Board/BoardArt.cs` (new): palette, catalog, library, `GetColor`, `ArrowColor`,
  `Validate` (every PaletteColor, every piece kind, `Library.Shapes`). No Onity reference.
- `GameSceneInstaller` binds `BoardArt` (new serialized `m_palette`, `m_catalog`), validates it
  at scope build and still binds `BlockShapeLibrary` (GameAttemptLoader resolves it).
  No `BoardArtInstaller.cs`, no `m_installers` entry, no Presentation asmdef change (not needed:
  Presentation has no Onity.DI use).
- `BoardPresenter`: `BoardArt` constructor/field, `new BlockPieceFactory(m_art, ...)` (only
  construction site), 4-argument `Prepare` kept. One further line was forced by the removed
  `BlockPiece.Library`: the prefab identity check now reads `m_shapes.GetPrefab(type) != shape.Prefab`.
- `BoardPieceView`: only `m_filter`/`m_renderer` serialized; no `[ExecuteAlways]`, `OnEnable`,
  `OnValidate`, `ResetState`; `SetColor(Color)` is the one MPB write; `OnPoolRelease` clears
  clip fields in memory only; renderer cache lazy in `ApplyState`.
- `ModularBlockView`: `Prepare(catalog, n)`, `Prepare(catalog, n, pieceCapacity)`,
  `SetCells/TrySetCells/SetColor(Color)`; `m_catalog/m_palette/m_shapes`, `SetShape` removed.
  Exact reachable warm-up (`AddReachableCapacity`, `CreatePieceCapacity`) unchanged.
- `BlockPiece`: `m_library`, `Library`, `m_defaultColor` removed; `Prepare(catalog, n, shape)`,
  `Prepare(catalog, n, cells, Color)`, `Prepare(catalog, n, shape, pieceCapacity)`,
  `SetShape(Shape)`, `SetColor(Color)`; a refused Rocket removal restores the last shape object.
- `BlockPieceFactory(BoardArt, Transform)`: identity `SourceType == type && library.GetPrefab(type) == shape.Prefab`,
  warm `Prepare(art.Catalog, n, shape, capacity)`, `Create` resolves `art.GetColor(data.Color)`.
- Environment: `EnvironmentDatabase` `m_floorColor = Ground`, `m_wallColor = Wall`,
  `GetFloorColor/GetWallColor`; `EnvironmentPiecePlacement` has one tinted constructor, no
  `IsTinted`; composer tints floor, walls/corners, doors, arrows, floor base; barrier composer
  construction adjusted; `EnvironmentView` always calls `SetColor`.
- Editor: `ShapePrefabAuthoring` writes no SO reference, exact-folder scan, binding check;
  `BoardKitAssets`/`BoardArrowAssets` stop writing removed fields; `BoardKitPreview` builds a
  `BoardArt` and tints environment previews by name; `PieceEditorWindow` preview construction
  only (all Revision 2 members kept); LevelPreview owner/host/mapper use `BoardArt`;
  `Editor/BoardKit/BoardKitMigration.cs` menu "Color Block Jam/Art/Reserialize Board Kit Prefabs".
- Compile-forced edits outside the allowed list (same API, no behaviour change):
  `Editor/BoardKit/BlockPieceEditor.cs` (Inspector/thumbnail preview loads library, catalog,
  palette; colour Purple as before), `Tests/EditMode/ModularBlockViewCapacityTests.cs` (new
  Prepare/SetShape/factory signatures), `Tests/EditMode/SharedArtTests.cs` (release makes no GPU
  write, so the clip check follows the next `SetColor`), `Tests/Gameplay/PlayMode/BoardPresenterLifecycleTests.cs`
  line 95 (released tiles hidden instead of GPU clip reset). `LevelPreviewTests` "another library"
  test now rebinds Square to the Single prefab in the copy (prefabs no longer name a library).

## Migration

- `AssetDatabase.ForceReserializeAssets` wrote nothing (Grid.prefab timestamp and bytes unchanged,
  still held `m_palette`), so the menu marks each prefab's objects dirty and saves them instead.
- Ran `BoardKitMigration.Reserialize()` via MCP: 28 prefabs = 12 shape prefabs (every file in
  `Prefabs/Board/Shapes`, top folder only), 14 pieces, `ModularBlock`, `FloorBase`.
- Verified against pre-write copies (`scratchpad/di/base`, script `verify_prefabs.py`): 28 files
  changed, every removed line is a removed field, no line added, all fileID anchors and
  `m_generatedSignature` values identical, every `.meta` identical, `BlockShapeLibrary.asset`
  byte-identical. `BoardPieceCatalog.asset`, `GamePalette.asset`, piece JSON untouched.
- AC-1 greps: palette/catalog/library GUIDs in `Prefabs`: none; removed field names in `Prefabs`:
  none; palette GUID in scene/asset/prefab files: `Scenes/Game.unity`, `Data/Environment/EnvironmentDatabase.asset`.
- `Game.unity`: opened additively in Edit Mode, `GameSceneInstaller` (on `Gameplay`) `m_palette`
  and `m_catalog` set via SerializedObject, scene saved and closed. Diff is exactly those 2 lines.
- `EnvironmentDatabase.asset`: not written by this task. The Codex solid-obstacle session saved it
  at 19:13 with the compiled class, so it holds `m_floorColor: 12` and `m_wallColor: 11`
  (the defaults this task added). One writer only.

## Tests (MCP run_tests)

- PlayMode, `ColorBlockJam.Presentation.PlayModeTests` + `Environment.PlayModeTests` +
  `Gameplay.PlayModeTests`: job a52e7c26466c4e59a54d5ab3bcb9509f **49/49 passed** (earlier
  c8f314d048c248569811fd7634efb29e 48/48). Includes AC-3 `PooledTileTakesItsOwnersColourAndReleaseMakesNoPropertyBlockWrite`,
  AC-4 `FactoryTintsWithTheArtPaletteRecomposesWithoutAllocatingAndPoolsEverything` (0 B recompose),
  AC-5 `BoardArtContainerTests`, `GameSceneDragTests` (level 1 to Won), barrier view tests (0 B flip).
- EditMode, `Editor.EditModeTests` + `LevelPreview.Tests` + `Environment.EditModeTests` +
  `Gameplay.EditModeTests`: job d8c84351e42c415a948159c4e02e1f85, 844 run, 4 failures, none from
  this task:
  - `BoardKitTests.AssigningAndDeletingClonedPieceDatabase...` and `CreatingPieceDatabase...`:
    unhandled `InvalidOperationException: Level database has not been loaded` thrown by
    `LevelDatabaseWindow.UpdateOtherLevels` from `LevelDatabaseWindow.CreateGUI` (the open Level
    Editor window reacting to the piece-database switch). Passed in full run c5aadb41073f4a07ba1f95ce5fde0875.
  - `LevelEditorShellTests.ToolsColumnHolds...` (Obstacles section, task 306ba760) and
    `LevelSolidBlockTests.PaintAndErase...` (new solid-obstacle test): other sessions' work in progress.
  - Earlier run c5aadb41073f4a07ba1f95ce5fde0875 (834): `LevelDatabaseJsonTests.LinkXml...`
    (SolidBlockObject, other session; passed on rerun 2aaaef1309084c86a8f0f69641bc73f8) and
    `ShapePrefabTests.ReadOnlyLibrary...` ("Wait for compilation" during another session's compile;
    passed on rerun 2aaaef1309084c86a8f0f69641bc73f8).
- Focused EditMode (BoardArtTests, ShapePrefabTests, ModularBlockViewCapacityTests,
  BlockPieceEditorPreviewTests, ShapeLibraryJsonTests, SharedArtTests, PieceEditorLinkTests,
  LevelPreviewTests, EnvironmentComposerTests, EnvironmentBarrierComposerTests): job
  e301f45c6a584180a2f58d773ee312d5, 95 run, 1 failure: `ShapePrefabTests` `IOException: Unable to
  remove the file to be replaced` from `ShapeLibraryFile.Write` (`File.Replace` of the fixture's
  fresh Library.json, a Windows sharing violation; code path unchanged by this task). Reruns
  50827cd5560642bab59e47b5ed80c6f6 and 80594d34c29d411795b6c4c76b66ec38 hit the same IOException
  in `SavePreserves...`; ShapePrefabTests passed completely in full run d8c84351e42c415a948159c4e02e1f85.
  Final focused rerun 5f25f84cf3594633b39edd2f45f6168c (ShapePrefabTests, BoardArtTests,
  ModularBlockViewCapacityTests, BlockPieceEditorPreviewTests): **30/30 passed**.
- Compiler: 0 warning/error CS messages. Console errors after the runs: only the external
  `Level database has not been loaded` and the expected `Could not restore piece JSON` log.
- AC-9 grep `System.Linq|string.Format` over Presentation and Runtime: no matches (unchanged).

## Captures (AC-7, AC-8)

- `docs/art/board-kit/board-art-di/game-level-01-{1080x1920,1080x2400}-{before,after}.png`:
  Game scene in Play Mode, level 1, main camera rendered to a 24-bit RT without MSAA (camera
  aspect set per size, framing unchanged). Before captured at 18:52 before the first write.
  Pixel compare: 1080x1920 differs in 4 pixels by at most 1/255; 1080x2400 in 6 pixels by at
  most 6/255. Blocks per Square: 30 objects (9 shown tiles), as before.
- `docs/art/board-kit/board-art-di/preview/*.png`: BoardKitPreview RenderAll + RenderEnvironment
  output (palette colours: walls Wall, Grid Ground, doors Orange, arrows white, blocks by index);
  the original `docs/art/board-kit/{shapes,environment}` files were restored byte-identical.
  Console clean during the render.
- AC-8 needs the user's visual acceptance; AC-7 Piece Editor and LevelPreview host rely on
  `BlockPieceEditorPreviewTests` and `LevelPreviewTests` (pass) plus the render above.

## Editor state

- Before: untitled scene only; later the LevelEditor scene was opened by someone else. After the
  after-capture the LevelEditor scene was reopened as the single active scene; a stray
  InitTestScene left by an interrupted PlayMode run was gone when checked. No scene is dirty.

## Review fixes (2026-09-25, after the first review)

- **First Rocket removal allocated (major, confirmed).** Without `OnEnable`, tiles warmed only for
  Rocket remainders had no property block or renderer cache until their first `SetColor` in
  Playing. Fix: `BoardPieceView.CreateCache()` (idempotent, no GPU write; `ApplyState` still
  calls it for owners that never prepare, such as the environment and editor previews) and
  `ModularBlockView.Prepare` calls it for every rented tile in its rent-all loop.
- **AC-4 assertion was vacuous (major, confirmed).** `GC.GetAllocatedBytesForCurrentThread`
  reports 0 in this Mono runtime. `FactoryTintsWithTheArtPaletteRecomposesWithoutAllocatingAndPoolsEverything`
  now prepares two Squares, warms JIT on the first, and asserts the first `TryRemoveCell` on the
  second with `Is.Not.AllocatingGCMemory()` (Recorder based). Red check: with the
  `CreateCache()` call commented out the test failed ("Expected: not allocates GC memory",
  job 365d8ebad730417eaaf1e86581676676); restored, it passes. The earlier "0 B" AC-4 claim above
  is superseded by this run. The older `GC.GetAllocatedBytesForCurrentThread` uses in
  BoardKitTests, ShapePrefabTests and the Environment tests predate this task and are not changed.
- **AC-5 could not detect a missing scope-build load (minor, confirmed).** `BoardArtContainerTests`
  gains `InstallBindingsLoadsAndValidatesTheShapeDefinitionsBeforeTheContainerBuilds`: the Game
  scene installer (found as `MonoInstaller` by its `m_shapes` field) is pointed at a fresh library
  copy with an empty cache and run against an unbuilt `OnityContainer`; the copy's `LoadedJson`
  must equal the JSON source, and a copy without a JSON source must make `InstallBindings` throw.
  The scene test now also asserts that no Onity-tracked task started during the scene is still
  running and no subscription made while it was loaded is still live after unload
  (`OnityTaskTracker`; `OnityObservableTracker` by reflection because the test asmdef, which this
  task may not edit, does not reference Onity.Reactive).
- **Docs said SetColor is the only MPB write (minor, confirmed).** PROJECT.md Rule 1, the DEC-123
  row, `docs/art/board-kit/README.md` and the `BoardPieceView` summary now say `SetColor` is the
  only colour write and `SetClipPlane` the only other property-block write; a release makes none.
- **Scope (minor, partly confirmed).** Compile-forced edits outside the list stay as recorded
  above (BlockPieceEditor.cs, ModularBlockViewCapacityTests.cs, SharedArtTests.cs); they need the
  owners' acknowledgement. `BoardPresenter.cs` identity line: the tautological
  `m_shapes.GetPrefab(type) != shape.Prefab` clause is removed, leaving
  `prefab == null || prefab.SourceType != visual.SourceType` (the library's binding is the identity
  by construction). `BoardPresenterLifecycleTests`: the vacuous `activeInHierarchy` loop is removed;
  clip reset on reuse is covered by AC-3. `LevelPreviewTests` test renamed to
  `ShapeBoundToAnotherTypesPrefabIsRejectedBeforeTheSceneChanges`. `gameplay-release.diff`
  regenerated against the start snapshot.
- **Design doc superseded (minor).** `docs/Board-Art-DI-Design.md` is outside the allowed paths, so
  it is not edited; the DEC-123 row in PROJECT.md now links it as the historical design superseded
  by the contract revisions.
- Tests after the fixes (MCP run_tests): focused PlayMode job ae536e7ddbd64d87b3244aef2f2c13ab 6/6;
  PlayMode gate (Presentation, Environment, Gameplay PlayModeTests) job
  89d881f2e5894e6db23b48b85d14b44f **50/50 passed**; EditMode gate (Editor.EditModeTests,
  Editor.LevelPreview.Tests, Environment.EditModeTests, Gameplay.EditModeTests) job
  e32c499181104fcfa857d6c555e40331 **867/867 passed**. Console: no compile errors; only
  test-fixture warnings. AC-9 grep over Presentation and Runtime: 0 matches.
- While this run finished, another session briefly commented out the new `CreateCache()` call
  (`// RED-CHECK`) and then restored it; the file was left with the call in place (20:36).
- Implementer addendum (20:45-20:51). The `// RED-CHECK` edit was this implementer's deliberate red
  check: with the call commented out, the AC-4 test failed alone (job 7629f9abbba14a6f863e884b690fe1c5,
  "Expected: not allocates GC memory"); a read-only preview-scene probe showed 69 uncached tiles
  after `Prepare(Square, 3, 4)` and 6 GC.Alloc samples on a fresh block's first removal, against 0
  uncached tiles with the call restored. One full PlayMode gate run right after another session's
  test run (job cb03b9fd8b544977bd2c46febb7b6821) failed only this assertion; reruns
  85de21cb223048aba4379088ab56ca91 and 161d8cb400564ad6ba7bf5447e568e7c passed 50/50. The warm pass
  now runs the same `TestDelegate` through `AllocatingGCMemoryConstraint.ApplyTo` on the other root,
  so one-time JIT of the delegate, the removal and the constraint wrapper is excluded; the measured
  call is still the first removal on the second, freshly prepared root. After that change: PlayMode
  gate 4b119da1a77244ddb78e0f19a01f80a0 and b06e56fedbc846a5bf63c6bc85457a58 **50/50 twice** (the
  first directly after a domain reload), EditMode gate 3c475f60271744deb8f0f866a0b7175c
  **867/867**; `EditorUtility.scriptCompilationFailed` false, no `error CS` in the Console.
  In Edit Mode (execute_code) the first `SetLocalPositionAndRotation` of a pooled tile still records
  one GC.Alloc sample (plain GameObjects record none); Play Mode, where Rocket runs, records none.

## Open items

- Prefab Mode and scene placement of a bare piece prefab now show white (untinted) pieces.
- Other contracts' history lines (board-kit, level-editor-scene-preview, play-area-environment)
  were not written. `gameplay-release.diff` holds this task's Gameplay lines for the RELEASE message.
- Profiler GC Alloc over one Rocket removal on device was not measured (0 B asserted in AC-4 test).

## Independent verification (2026-09-25, verifier session; no production or test edits)

Live Editor: MCP HTTP 127.0.0.1:8082, instance ColorBlockJamCase2026@99077e4df1ecfaa4. Start and end
state: only `LevelEditor.unity` open, active, not dirty, no Prefab Mode; no scene saved. Baselines:
Plastic `cs:7` (`cm cat`, read-only) plus the implementer's pre-write copies (`scratchpad/di/base`).
Captures: `verifier/` in this task folder.

| AC | Result | Channel and evidence |
|---|---|---|
| AC-1 | PASS | Both greps over `Assets/ColorBlockJam/Prefabs` return nothing. The palette GUID `e13fe037…` appears in `.unity/.asset/.prefab` files only in `Scenes/Game.unity` and `Data/Environment/EnvironmentDatabase.asset`. |
| AC-2 | PASS (count clause read as task delta) | 27 prefabs compared with `cs:7` and all 28 with the pre-write copies: only `m_palette/m_catalog/m_shapes/m_library/m_useArrowColor/m_defaultColor` lines removed, nothing added, fileID anchors identical, `.meta` files identical to `cs:7`, every `m_generatedSignature` identical to `cs:7` (Piece_030 is new since cs:7; identical to the pre-write copy). Shape fileIDs are a subset of the cs:7 set (the cs:7 `GeneratedShape` removal belongs to block-runtime-objects). `BlockShapeLibrary.asset` byte-identical to the pre-write copy (its delta against cs:7, the Piece_030 binding, predates this task). `json-migration-source.json`, `PieceDatabases/BlockShapeLibrary.json`, `BoardPieceCatalog.asset`, `GamePalette.asset`, `ProjectContext.prefab`, the Presentation asmdef: identical to cs:7. `Environment.prefab` identical to pre-write. Live `ShapePrefabAuthoring.IsCurrent`: 11/11 bound library shapes true. `cm status` cannot list only this task in the shared workspace; this task's serialized delta is 28 prefabs (12 shapes + 14 pieces + ModularBlock + FloorBase, all "Checked-out (changed)"/"Added") and 2 lines of `Game.unity` (`m_palette`, `m_catalog` on GameSceneInstaller, diff against pre-write copy). `EnvironmentDatabase.asset` holds `m_floorColor: 12` (Ground) and `m_wallColor: 11` (Wall), saved by the solid-obstacle session. |
| AC-3 | PASS | `BoardKitLifecycleTests.PooledTileTakesItsOwnersColourAndReleaseMakesNoPropertyBlockWrite` in PlayMode gate job c1f28f7e66a34dd19d1e34b5267f12fa (50/50). Code read: `OnPoolRelease` only clears fields. |
| AC-4 | PASS | `FactoryTintsWithTheArtPaletteRecomposesWithoutAllocatingAndPoolsEverything` (Recorder-based `Is.Not.AllocatingGCMemory()` on a second, JIT-warmed-elsewhere root; transform count unchanged after Prepare; Dispose empties the parent) in job c1f28f7e…. `ModularBlockView.Prepare` calls `CreateCache()` in its rent-all loop; no `RED-CHECK` leftover. |
| AC-5 | PASS | `BoardArtContainerTests` (one instance, same library as the container's, validates; container releases the binding on unload; no running Onity task or live subscription left; unbuilt-container `InstallBindings` loads the JSON and throws on a library with no JSON) in job c1f28f7e…. |
| AC-6 | PASS | `EnvironmentComposerTests` (floor `GetFloorColor`, wall/corner `GetWallColor`, door/arrow `GetDoorColor/GetDoorArrowColor`, floor base `GetFloorBaseColor`) in EditMode jobs; `EnvironmentViewLifecycleTests` colour assertions in job c1f28f7e…. `IsTinted` no longer exists in any `.cs`. |
| AC-7 | PASS | Live, no container: Level Editor preview host (DontSave root in LevelEditor scene): blocks Pink x12 / LightBlue x12, walls Wall x48, floor Ground x20, the two Door_Arrow tiles equal `GetDoorArrowColor(Pink/LightBlue)` (`verifier/ac7-level-preview-host.png`). Piece Editor window preview refreshed (no data write, `m_dirty` stayed false): 4/4 tiles Red = its preview colour (`verifier/ac7-piece-editor-preview.png`). BoardKitPreview's code path replicated into the task folder: Square Blue 16/16 tiles, Wall/Grid/Door_1x1/Arrow_1 tinted Wall/Ground/Orange/ArrowColor (`verifier/ac7-kitpreview-*.png`). `BoardArt.Validate` true. Console errors after the checks: only the expected test log "Could not restore piece JSON". |
| AC-8 | UNPROVEN (user acceptance) | Recomputed on the implementer's captures: 1080x1920 differs in 4 pixels, max channel delta 1/255; 1080x2400 in 6 pixels, max 6/255. Before files 18:52 (before the first code write 18:58), after files 19:49 (before the review fix, which adds no colour write). Side by side: `verifier/ac8-level-01-1080x1920-before-left-after-right.png`. Needs the user's visual acceptance. |
| AC-9 | PASS | PlayMode gate (Presentation, Environment, Gameplay PlayModeTests) job c1f28f7e66a34dd19d1e34b5267f12fa: 50/50. EditMode gate (Editor.EditModeTests incl. PieceEditorLinkTests, BoardKitTests, ShapeLibraryJsonTests; LevelPreview.Tests; Environment.EditModeTests; Gameplay.EditModeTests) job b628393b859e43528f214aa70d820591: 867/867. The first EditMode run eb33b29c08a0422cb25c2e47b6ba81af had 2 `ShapePrefabTests` failures, `IOException: Unable to remove the file to be replaced` from `File.Replace` (a path this task does not change); `ShapePrefabTests` alone passed 17/17 (b4f283813aca452982c496a045d8ddd4) and the full rerun passed; another session was running tests on the same Editor at the time. `grep "System.Linq\|string.Format"` over Presentation and Runtime: 0 matches (unchanged). |

### Files changed by this task (against the pre-write copies and cs:7)

- New: `Presentation/Board/BoardArt.cs`, `Editor/BoardKit/BoardKitMigration.cs`, `Tests/EditMode/BoardArtTests.cs`,
  `Tests/PlayMode/BoardArtContainerTests.cs` (+ `.meta`), `docs/art/board-kit/board-art-di/**`, this folder's files.
- Changed, inside the allowed paths: `Presentation/Board/{BoardPieceView,ModularBlockView,BlockPiece,BlockPieceFactory}.cs`;
  `Environment/{EnvironmentDatabase,EnvironmentComposer,EnvironmentPiecePlacement,EnvironmentBarrierComposer}.cs`,
  `Environment/EnvironmentView.cs` (tint branch and comment; the solid-composer lines there are the Codex session's);
  `Editor/BoardKit/{ShapePrefabAuthoring,BoardKitAssets,BoardArrowAssets,BoardKitPreview}.cs`,
  `Editor/BoardKit/PieceEditorWindow.cs` (two preview lines; all Revision 2 members present);
  `Editor/LevelPreview/{LevelPreviewHost (field, constructor, factory),LevelPreviewOwner (asset loads),LevelPreviewBlockMapper (identity check)}.cs`;
  `Gameplay/Composition/GameSceneInstaller.cs`; `Gameplay/Board/BoardPresenter.cs` (field, constructor, factory construction; 4-argument Prepare kept);
  `Tests/Gameplay/PlayMode/Fixtures/PresenterScene.cs`; `Tests/EditMode/{BoardKitTests,ShapePrefabTests,ShapeLibraryJsonTests}.cs`;
  `Tests/PlayMode/BoardKitLifecycleTests.cs`; `Tests/Environment/EditMode/{EnvironmentComposerTests,EnvironmentBarrierComposerTests (one IsTinted assertion removed)}.cs`;
  `Tests/Environment/PlayMode/EnvironmentViewLifecycleTests.cs`; 28 prefabs; `Scenes/Game.unity` (2 lines);
  `PROJECT.md` (DEC-123 row, Rule 1), `docs/Environment-Layout.md`, `docs/art/board-kit/README.md`.
- **Outside the allowed paths or their narrow qualifiers (flagged):**
  - `Editor/BoardKit/BlockPieceEditor.cs` (not listed; compile-forced: loads library/catalog/palette, Purple as before).
  - `Tests/EditMode/ModularBlockViewCapacityTests.cs` (not listed; new signatures).
  - `Tests/EditMode/SharedArtTests.cs` (not listed; adds a `SetColor` before the clip check).
  - `Gameplay/Board/BoardPresenter.cs` line ~352: identity check `prefab.Library != m_shapes` clause removed (beyond "field/constructor/factory").
  - `Tests/Gameplay/PlayMode/BoardPresenterLifecycleTests.cs` line ~95: `IsClipped` assertion replaced by a comment (beyond "fixtures that construct BoardPresenter"); the constructor line is in scope.
  - `Tests/LevelPreview/LevelPreviewTests.cs`: test renamed and its fixture rebinds Square to the Single prefab (beyond "host construction only").
  - `PROJECT.md` row is DEC-123, not the contract's DEC-102 (DEC-102 was already taken).
- Not written by this task although listed as its targets: `Presentation/ColorBlockJam.Presentation.asmdef` (not needed after Revision 2), `Data/Environment/EnvironmentDatabase.asset` (saved by the solid-obstacle session), no `BoardArtInstaller.cs` (Revision 2).
- Changed in the workspace by other sessions, not attributed here: `Runtime/Levels/*`, `Editor/Levels/**`, `Environment/EnvironmentSolidComposer.cs`, `Environment/Editor/EnvironmentSolidAssets.cs`, `Gameplay/Composition/GameAttemptLoader.cs`, `Gameplay/Levels/AttemptLevelConverter.cs`, the Level* tests.

### Remaining for closure

- AC-8 user acceptance of the before/after captures; the user's answer on white untinted Prefab-Mode pieces (contract stop condition).
- Owners' acknowledgement of the flagged out-of-scope edits; history lines in the three other contracts; RELEASE message with `gameplay-release.diff`.

## Implementer response to the independent verification (2026-09-25, ~21:00-21:15)

No production code, test, scene or prefab was edited in this pass. No Plastic write.

### AC-8 made demonstrable (still needs the user's visual acceptance)

- New `docs/art/board-kit/board-art-di/compare/ac8-level-01-{1080x1920,1080x2400}-before-after-diff.png`
  (generated with PIL from the existing before/after captures). Each image is before | after | diff
  marker: the diff panel darkens the before capture and draws a red 9 px box around every differing pixel.
- Recomputed: 1080x1920 differs in 4 pixels (x 884-887, y 555), max channel delta 1/255;
  1080x2400 (20:9) differs in 6 pixels (x 44-46 y 1735, x 1002-1003 y 1762-1779), max 6/255.
  All sit on wall joins/edges, the known no-MSAA hairline seam positions; no block, floor, door
  or arrow colour differs.

### Out-of-scope edits: kept, each forced by the contract (none reverted)

Reverting any of these would break compilation or assert behaviour the contract removes.
- `Editor/BoardKit/BlockPieceEditor.cs`: read `BlockPiece.Library`, `ModularBlockView.m_catalog/m_palette`
  and `BlockPiece.m_defaultColor`, all removed by Expected 4-5. It now loads the same three assets by
  path. The colour is `PaletteColor.Purple`, the value every pre-write prefab serialized
  (`m_defaultColor: 9` in all 12 base shape prefabs, `Purple = 9`), so the thumbnails are unchanged.
- `Tests/EditMode/ModularBlockViewCapacityTests.cs`: call sites of the removed `Prepare(int)`,
  `SetShape(BlockShapeType)` and `BlockPieceFactory(BlockShapeLibrary, …)` only. Assertions unchanged.
- `Tests/EditMode/SharedArtTests.cs`: asserted that `OnPoolRelease` writes the cleared clip to the GPU,
  the opposite of Expected 3. One `SetColor` (the owner's next write) is added before the unchanged checks.
- `Gameplay/Board/BoardPresenter.cs` ~352: the clause `prefab.Library != m_shapes` reads the removed
  `BlockPiece.Library`. Only that clause is deleted. The prefab comes from `m_shapes.Get(type).Prefab`,
  so it belongs to this library by construction. The line is included in `gameplay-release.diff`.
- `Tests/Gameplay/PlayMode/BoardPresenterLifecycleTests.cs` ~95: asserted "the pool release resets the
  clip plane" on the GPU, which Expected 3 removes. The replacement coverage is AC-3
  (`PooledTileTakesItsOwnersColourAndReleaseMakesNoPropertyBlockWrite`). Included in `gameplay-release.diff`.
- `Tests/LevelPreview/LevelPreviewTests.cs`: the old fixture ("a copy's prefabs still name the original
  library") has nothing left to reject, because prefabs no longer name a library. The mapper's identity check,
  which is in scope, now rejects a library binding a shape to another type's prefab. The fixture sets up
  that case and the test name says so.
- `PROJECT.md` DEC-123: DEC-102 is the LevelEditor scene preview row; DEC-121/122 were also taken.

### Tests after this pass (MCP run_tests)

- PlayMode gate (Presentation, Environment, Gameplay PlayModeTests): job fe4c3686b43a4377a4dae4c48c97fd43
  **50/50 passed**.
  - Before that, jobs 41429b32dec748ce8045394c0f04f387 and edb45fd3595e494ea3ae2c1e33af9f7e failed
    only `EnvironmentBarrierViewTests.BarrierFlipsPauseAndStopAllocateNothing` ("not allocates GC memory").
    Another session was running tests on the same Editor then (the runner answered `busy`/`tests_running`).
  - The flip path (`SetBarrierOpen`/`MoveBarrier`, transforms, renderers, PrimeTween) calls nothing this
    task changed. An Edit Mode probe in a preview scene measured 0 GC.Alloc samples per step on the
    second and third flips.
  - Once the runner was free: Environment PlayModeTests alone a2e9b4ac05a8485d81bcd153d7c7eed9 15/15,
    then the full gate 50/50.
- EditMode gate (Editor.EditModeTests incl. PieceEditorLinkTests, BoardKitTests, ShapeLibraryJsonTests,
  ShapePrefabTests; Editor.LevelPreview.Tests; Environment.EditModeTests; Gameplay.EditModeTests):
  job 0c79889930c94b7f96715df6f003b874 **873/873 passed**. The six new tests came from other sessions'
  Level editor work.
- AC-9 grep `System.Linq|string.Format` over Presentation and Runtime: 0 matches.
- `BlockShapeLibrary.asset` was touched at 21:06 by another session. Its content is still identical to
  this task's pre-write copy.
- Editor end state: only `LevelEditor.unity` open, not dirty, not in Play Mode.

## Independent verification 2 (2026-09-25 ~21:15-21:45, verifier session; no production, test, scene or prefab edit)

Live Editor: MCP HTTP 127.0.0.1:8082, instance ColorBlockJamCase2026@99077e4df1ecfaa4. Start and end state:
only `LevelEditor.unity` open, active, not dirty, not in Play Mode, no Prefab Mode; no scene saved. Temporary
scenes (one additive empty scene, PreviewRenderUtility scenes) were closed/cleaned without saving. Baselines:
the implementer's pre-write copies (`scratchpad/di/base`) and Plastic `cs:7` (`cm cat`, read-only).
Captures: `verifier2/` in this folder.

| AC | Result | Channel and evidence |
|---|---|---|
| AC-1 | PASS | Both greps over `Assets/ColorBlockJam/Prefabs` return nothing. Palette GUID in `.unity/.asset/.prefab`: only `Scenes/Game.unity` and `Data/Environment/EnvironmentDatabase.asset`. |
| AC-2 | PASS (count clause read per Revision: every existing shape prefab) | Own difflib comparison of 28 prefabs vs pre-write copies: only removed lines, all field lines (`m_palette` 28, `m_colorType` 15, `m_catalog` 13, `m_shapes` 13, `m_library` 12, `m_defaultColor` 12, `m_useArrowColor` 4); no added line; fileID anchors and `m_generatedSignature` identical; all `.meta` identical to pre-write (and to cs:7 where they exist). Against cs:7: signatures identical for the 11 shape prefabs in cs:7; fileIDs a subset (GeneratedShape removal is block-runtime-objects'). `BlockShapeLibrary.asset` byte-identical to pre-write (its cs:7 delta is the Piece_030 binding, earlier task); `json-migration-source.json`, `PieceDatabases/BlockShapeLibrary.json`, `BoardPieceCatalog.asset`, `GamePalette.asset`, `ProjectContext.prefab`, Presentation asmdef identical to cs:7; `Environment.prefab` identical to pre-write. Live: `ShapePrefabAuthoring.IsCurrent` 11/11 bound library shapes; `BoardKitMigration.GetPaths()` = 28. |
| AC-3 | PASS | `PooledTileTakesItsOwnersColourAndReleaseMakesNoPropertyBlockWrite` passed in PlayMode jobs aa82d55a95ae4447af24c6fdb185b92b and 6aa7f7e071724bdf9c3b76b6620c73bb; code read: `OnPoolRelease` clears two fields only. |
| AC-4 | PASS | `FactoryTintsWithTheArtPaletteRecomposesWithoutAllocatingAndPoolsEverything` (Recorder-based `Is.Not.AllocatingGCMemory` on the second, freshly prepared root; LightBlue tint; ShapeType null after removal; transform count unchanged; Dispose empties parent) passed in the same jobs and in c5df19d770f54651b9052b8d7d949f6b. |
| AC-5 | PASS | `BoardArtContainerTests` (2 tests: one instance, same library, released with scene, no running task/live subscription; unbuilt `InstallBindings` loads JSON and throws on a JSON-less copy) passed in the same jobs. |
| AC-6 | PASS | `EnvironmentComposerTests` (floor/wall/floor-base/door/arrow tints) in EditMode job 8bbd755d6b9d450eb4861e97d13f7669; `EnvironmentViewLifecycleTests` colour assertions in PlayMode jobs above and 89ba1af57fe242c190c25c3b4109e19f. Every `new EnvironmentPiecePlacement` site (composer x5, solid composer x1) passes a tint; `IsTinted` absent from all `.cs`. |
| AC-7 | PASS | Live, no container. (a) Own `LevelPreviewHost` built by reflection exactly as `LevelPreviewOwner` does (asset-loaded `BoardArt`) in a temporary additive scene, level-01 from `Data/Levels/LevelDatabase.json`: blocks LightBlue/Pink = authored colours, doors LightBlue/Pink, Door_Arrow = `GetDoorArrowColor`, Wall/Corner_1 = Wall, Grid = Ground, FloorBase = Wall (`verifier2/ac7-level-preview-host-level-01.png`, rendered on an isolated layer). (b) The real Level Editor preview (DontSave root in LevelEditor): all 116 visible tiles palette colours, including the draft's two Red Singles. (c) Piece Editor window preview (read only, not refreshed): 4 tiles Red = its `m_previewColor`. (d) BoardKitPreview code path, output redirected to `verifier2/`: Single LightGreen 4/4, CTypeB LightBlue 12/12; Wall/corner_3 Wall, Door_1x1/Door_Arrow Orange, Grid Ground, Arrow_2 white (ArrowColor); each bare piece had an empty MPB before tinting. (e) `BlockPieceEditor.CanRender/Render` (Inspector/thumbnail) for Square: true, rendered. No error logged by these checks. |
| AC-8 | UNPROVEN (user acceptance) | Recomputed: 1080x1920 differs in 4 pixels (x 884-887, y 555), max 1/255; 1080x2400 in 6 pixels, max 6/255; all at wall joins. Before files 18:52:18 precede the first code write (18:58:14 `BlockPiece.cs`). After files 19:49 predate the review fix (`CreateCache`, no colour write). Comparison: `docs/art/board-kit/board-art-di/compare/*.png`. |
| AC-9 | PASS (for its listed folders), with one order-dependent failure in the extra Gameplay gate | EditMode gate (Editor.EditModeTests incl. PieceEditorLinkTests, BoardKitTests, ShapeLibraryJsonTests, ShapePrefabTests, BoardArtTests; LevelPreview.Tests; Environment.EditModeTests; Gameplay.EditModeTests): job 8bbd755d6b9d450eb4861e97d13f7669 **874/874**. PlayMode: Environment alone 89ba1af5… 15/15; Presentation+Environment c5df19d7… 39/39; Gameplay alone 7b3cd5f0d20e4cdd827e08e9d4bc70ea 11/11. Grep `System.Linq|string.Format` over Presentation and Runtime: 0 matches. |

### Gate finding: `EnvironmentBarrierViewTests.BarrierFlipsPauseAndStopAllocateNothing` fails when run with Gameplay PlayMode tests

- Full PlayMode gate (Presentation + Environment + Gameplay): jobs aa82d55a95ae4447af24c6fdb185b92b and
  6aa7f7e071724bdf9c3b76b6620c73bb, 49/50, this test only ("Expected: not allocates GC memory").
  Environment + Gameplay: job c58e6211115e4b729899d4ed63c7769e, same single failure. Environment alone and
  Environment + Presentation pass. Deterministic in these three runs; the implementer saw it in
  41429b32…, edb45fd3…, cb03b9fd… and green in several other gate runs.
- Not attributable to this task on the evidence: the test file is identical to the pre-write copy, and the
  flip path (`SetBarrierOpen`/`SetBarrierTweensPaused`/`StopBarrierTweens`) contains no line this task
  changed. Likely cause, from PrimeTween 1.4.11 `PrimeTweenManager.fetchTween_internal`: a new `ColdData`
  is allocated when `pool.Count == 0`, and tweens stopped by `StopBarrierTweens` return to the pool only on
  the manager's next update, so the measured second `Flip()` in the same frame depends on how many pooled
  tweens earlier tests (Gameplay level play) left. The test's owner (barrier-board-view task) should warm
  across a frame or set a tween capacity; it is recorded here, not fixed (verifier makes no test edits).

### Files changed by this task (confirmed against the pre-write copies)

Same list as "Independent verification" above; nothing new. Outside the allowed paths or their narrow
qualifiers (flagged, each read and confirmed compile- or contract-forced):
`Editor/BoardKit/BlockPieceEditor.cs`; `Tests/EditMode/ModularBlockViewCapacityTests.cs`;
`Tests/EditMode/SharedArtTests.cs`; `Gameplay/Board/BoardPresenter.cs` identity clause (~352);
`Tests/Gameplay/PlayMode/BoardPresenterLifecycleTests.cs` line ~95 (assertion removed);
`Tests/LevelPreview/LevelPreviewTests.cs` (fixture/rename beyond "host construction only");
`PROJECT.md` DEC-123 instead of DEC-102. Also noted: Expected 3 said "renderer cache stays lazy in
ApplyState"; the review fix adds `CreateCache()` called from `ModularBlockView.Prepare` (justified by AC-4).

### New observations (not defects of this task's code)

- **Plastic cs:8 (21:17, "Level Editor with its level, Addressables and environment infrastructure", another
  session) already contains this task's code, tests and the 28 migrated prefabs**, while `Game.unity` stays
  checked-out (pending). In cs:8 alone, `GameSceneInstaller` requires `m_palette`/`m_catalog`, which the
  committed `Game.unity` lacks, so the server state would throw at Game scope build until `Game.unity` is
  checked in. This task made no Plastic write.
- cs:8 also added a `Prefabs/Board/Shapes/PrefabTests_*` test-leftover folder; `cm status` now shows 256
  "Removed" items there (the tests deleted it locally). Owner: whoever checked in cs:8.
- `BoardKitMigration.Reserialize` logs with string interpolation (editor-only, outside the AC-9 grep scope;
  PROJECT.md Rule 8 prefers ZString for editor text). Minor.
- Bare piece prefabs show white untinted in Prefab Mode/scene placement (confirmed: empty MPB). Contract
  stop condition needs the user's answer.
