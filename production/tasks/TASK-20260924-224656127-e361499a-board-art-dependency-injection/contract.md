# TASK-20260924-224656127-e361499a-board-art-dependency-injection — Board views take resolved colours; BoardArt supplies shared art through Onity

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-224656127-e361499a-board-art-dependency-injection
- Supersedes: none
- Title: Board views take resolved colours; BoardArt supplies shared art through Onity
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: refactor
- Area: Modular board art (Presentation/Board, Editor/BoardKit, Editor/LevelPreview, Environment tinting)
- Workflow: direct

## Objective and baseline

- Observable objective: No prefab under `Assets/ColorBlockJam/Prefabs` references `GamePalette`, `BoardPieceCatalog` or `BlockShapeLibrary`. One `BoardArt` object, bound once in the Game scene by `BoardArtInstaller`, supplies them to the block factory; views receive resolved `Color` values from their owner and never consult a palette. Rendering output is unchanged.
- Current behavior/evidence:
  - User request 2026-09-25 (Turkish): every block carries too many unnecessary ScriptableObject references; pass them from one main system through the Onity DI library.
  - Each of the 34 `Prefabs/Board/Shapes/Piece_0xx.prefab` serializes `ModularBlockView.m_catalog`, `m_palette`, `m_shapes` and `BlockPiece.m_library` (136 references); each of the 14 `Prefabs/Board/Pieces/*.prefab` serializes `BoardPieceView.m_palette`; `ModularBlock.prefab` holds 3; `Environment.prefab` FloorBase holds 1. Editor writers: `ShapePrefabAuthoring.cs:74-81`, `BoardKitAssets.cs:181-183,268-269,296-297`, `BoardArrowAssets.cs:74-77`.
  - `BoardPieceView` is `[ExecuteAlways]` and colours itself from its palette in `OnEnable`, `OnValidate` and `OnPoolRelease`; each tile Get costs 3 MaterialPropertyBlock writes and 2 palette scans. Two colour owners contradict PROJECT.md Performance Rule 1 and DEC-96.
  - `ModularBlockView.m_shapes` has no runtime reader. `BlockShapeLibrary.Get` parses JSON lazily; nothing loads it explicitly at scope build.
  - No installer binds these assets; Onity pools never inject; `ColorBlockJam.Presentation.asmdef` has no `Onity.DI` reference; `Game.unity` `OnityContext.m_installers` lists only `EnvironmentSceneInstaller`.
  - `ShapePrefabAuthoring.Save` recreates `GeneratedShape` and `SyncPrefabs` covers only the 11 active shapes, so regeneration cannot be the migration tool; `AssetDatabase.ForceReserializeAssets` (precedent `VacuumMotionAssets.cs:94`) is.
- Expected behavior:
  1. `Presentation/Board/BoardArt.cs`: immutable class with `Palette`, `Catalog`, `Library`, `GetColor(PaletteColor)`, `ArrowColor`, `Validate(out string)` (reads `Library.Shapes`, never `Load()`); constructor injection; no Onity reference.
  2. `Presentation/Board/Composition/BoardArtInstaller.cs` (`MonoInstaller`) on the Game scene `SceneScope`, listed in `OnityContext.m_installers`: three serialized asset fields, `container.BindInstance<BoardArt>(art)`, `RegisterBuildCallback` runs `Validate` and throws on failure. `ColorBlockJam.Presentation.asmdef` references `Onity.DI` and `Onity.Unity`.
  3. `BoardPieceView`: only `m_filter` and `m_renderer` serialized; no `[ExecuteAlways]`, `OnEnable`, `OnValidate`, `ResetState`; `SetColor(Color)` is the single MPB write; `OnPoolRelease` clears clip fields without a GPU write; renderer cache stays lazy in `ApplyState`.
  4. `ModularBlockView`: `Prepare(BoardPieceCatalog, int)`, `SetCells(IReadOnlyList<Vector2Int>, Color)`, `SetColor(Color)`; `m_catalog`, `m_palette`, `m_shapes` and `SetShape(BlockShapeType, PaletteColor)` removed.
  5. `BlockPiece`: `m_library`, `Library`, `m_defaultColor` and `Prepare(int)` removed; `Prepare(BoardPieceCatalog, int, BlockShapeLibrary.Shape)`, `Prepare(BoardPieceCatalog, int, IReadOnlyList<Vector2Int>, Color)`, `SetShape(BlockShapeLibrary.Shape)`, `SetCells`, `TryRemoveCell`, `SetColor(Color)`; `ShapeType` semantics unchanged; `m_sourceType`, `m_view`, `m_authoredVisual`, `m_generatedSignature` kept.
  6. `BlockPieceFactory(BoardArt art, Transform parent)`: validates `piece.SourceType == type && art.Library.GetPrefab(type) == shape.Prefab`; warm loop calls `Prepare(art.Catalog, cellCapacity, shape)`; `Create` resolves `art.GetColor(data.Color)`, then `SetShape(shape)` or `SetCells(data.ResolvedCells)`. `BlockPieceSpawnData.Color` stays `PaletteColor`.
  7. `BoardPieceCatalog` unchanged (`m_environment` stays; it is the only reference to Arrow_1..3).
  8. `EnvironmentDatabase`: `m_floorColor = PaletteColor.Ground`, `m_wallColor = PaletteColor.Wall`, `GetFloorColor()`, `GetWallColor()`. `EnvironmentPiecePlacement` loses the untinted constructor and `IsTinted`; `EnvironmentComposer` tints floor, wall/corner, door, arrow and floor-base placements; `EnvironmentView.Build` always calls `SetColor` and drops the stale comment. Injection wiring unchanged.
  9. Editor: `ShapePrefabAuthoring.Save` writes no ScriptableObject references; `ValidateDestination` scans the exact folder only and checks `SourceType` plus the library's prefab binding; `Signature` unchanged; `BoardKitAssets.Build` and `BoardArrowAssets.Calibrate` stop writing removed fields; `BoardKitPreview` tints every environment preview by name; `PieceEditorWindow`, `LevelPreviewOwner`, `LevelPreviewHost` and `LevelPreviewBlockMapper` construct or consume `BoardArt` from `AssetDatabase` loads; new menu "Color Block Jam/Art/Reserialize Board Kit Prefabs" (`Editor/BoardKit/BoardKitMigration.cs`) force-reserializes the exact asset list.
  10. Migration executed: reserialize of 34 shape prefabs, 14 piece prefabs, `ModularBlock.prefab` and `FloorBase.prefab` with unchanged GUIDs, fileIDs and signatures; `EnvironmentDatabase.asset` saves its two new fields; `Game.unity` gains the installer in `m_installers`.
- Behavior that must remain unchanged:
  - Rendered colours of blocks, floor, walls, doors and arrows; one shared block material; MPB path.
  - Pool lifecycle (Rule 3): warm on Home, rent-all/release-all, no Instantiate/Destroy in an attempt, Dispose at teardown; exhaustion rejects.
  - Zero allocation in `SetCells`/`TryRemoveCell`/`SetColor` during Playing (Rule 5).
  - Shape prefab GUIDs, nested fileIDs and `m_generatedSignature`; `BlockShapeLibrary.asset` bytes; `PieceDatabases/BlockShapeLibrary.json`; `BlockShapeType` enum; `BoardPieceCatalog.asset`; `GamePalette.asset`; `Environment.prefab`; `ProjectContext.prefab`; packages; scene flow.
  - Level JSON schema and `BlockPieceSpawnData` domain types.

## Revision 2026-09-25 (takes precedence over any conflicting text above or below)

- **Current code base.** `TASK-20260925-002022060-4c9dac46-block-runtime-objects` already removed `GeneratedShape` and the authored-visual field, and added exact reachable warm-up (4-argument `BlockPieceFactory.Prepare`). The shape library has 12 records (the user's test piece Piece_030 included and deletable). Migrate every shape prefab that exists, not a fixed count of 34.
- **Game-scene composition.** The gameplay session built `ColorBlockJam.Gameplay`: `Gameplay/Board/BoardPresenter.cs` constructs the only `BlockPieceFactory` (`new BlockPieceFactory(m_shapes, m_blocksRoot)`), and `Gameplay/Composition/GameSceneInstaller.cs` binds `BlockShapeLibrary` from the scene.
  - `BoardArt` is bound there: `GameSceneInstaller` binds `BoardArt` (palette, catalog, library) instead of the separate `BoardArtInstaller` on `SceneScope`. `BoardArtInstaller.cs` is not created.
  - `BoardPresenter` takes `BoardArt` instead of `BlockShapeLibrary` and passes it to the factory.
  - `new BlockPieceFactory(...)` in `BoardPresenter` stays the only construction site.
  - The `Game.unity` write is limited to `GameSceneInstaller`'s serialized asset fields.
- **Access granted by the owner.** Session "Buluan parça sürükleme ve gate mekanikleri" chose narrow sequential write access for this task: BoardPresenter's factory construction and its `BlockShapeLibrary`→`BoardArt` field/constructor, GameSceneInstaller's binding, and the `Tests/Gameplay/**` fixtures that construct `BoardPresenter`. Message that session before starting and when releasing.
- **Extra verification.** Before handoff, `ColorBlockJam.Gameplay.EditModeTests` (215) and `ColorBlockJam.Gameplay.PlayModeTests` (11, including `GameSceneDragTests`, which plays level 1 to Won in `Game.unity`) are green.
- **User approval** for the serialized writes was given on 2026-09-25 ("Yes, go ahead") and is recorded in the lifecycle history.

## Revision 2 2026-09-25 (takes precedence over any conflicting text above or below)

- **Barrier view landed** (`TASK-20260925-102657924-08cc082b-barrier-board-view`). `EnvironmentBarrierComposer.cs` builds tinted placements (`EnvironmentDatabase` barrier colour) and its tests assert `IsTinted`. Expected 8 therefore also covers `EnvironmentBarrierComposer.cs` (placement construction only) and the barrier tests' tint assertions. Keep the barrier API (`Build(layout, level)`, `SetBarrierOpen`, `SetBarrierTweensPaused`, `StopBarrierTweens`, `BarrierCount`), its 0 B flip and every barrier test green.
- **Piece Editor level link landed** (`TASK-20260925-145158848-5858b722-piece-editor-level-link`, session "Database oluşturma popup", released to this task). The `PieceEditorWindow.cs` edit stays "preview construction only" and keeps `m_levelPieceMode`, `m_levelPiece`, `m_pendingLibraryType`, `ShowLibraryPiece`/`ShowLevelPiece`, `LoadLibraryPiece`/`LoadLevelPiece`, `LevelPieceApplied`, the `OnLibraryCommitted` subscription in `OnEnable`/`OnDisable` and the Save → `SaveOrApply`/`SaveAsTemplate` wiring. `ShapeLibraryAuthoring.cs` is not in this task. `PieceEditorLinkTests`, `BoardKitTests` and `ShapeLibraryJsonTests` stay green.
- **Game.unity.** The write is `GameSceneInstaller`'s serialized asset fields only (the palette and catalog it needs to bind `BoardArt`); there is no `BoardArtInstaller` component or `m_installers` entry.
- **Gameplay coordination.** The gameplay session is staging a move-commands change outside the project and will three-way merge onto this task's lines. Send "START board-art DI" before the first write, and at release "RELEASE board-art DI" with the unified diff of `BoardPresenter.cs`, `GameSceneInstaller.cs` and every changed `Tests/Gameplay/**` file against the version at start. The Gameplay test gate is every test in `ColorBlockJam.Gameplay.EditModeTests` and `ColorBlockJam.Gameplay.PlayModeTests`, whatever their current count.

## Revision 3 2026-09-25: compile-forced edits accepted (takes precedence)

The API removals this contract requires (`BlockPiece.Library`, the view SO fields, the release GPU write) forced edits the lists above do not name. They are accepted as part of this task, all verified by independent verifier 2:
- `Assets/ColorBlockJam/Editor/BoardKit/BlockPieceEditor.cs` (loads the same assets by path, Purple preview colour as before)
- `Assets/ColorBlockJam/Tests/EditMode/ModularBlockViewCapacityTests.cs` and `Assets/ColorBlockJam/Tests/EditMode/SharedArtTests.cs` (constructors and the removed release-write assertion)
- `BoardPresenter.cs` identity clause `prefab.Library != m_shapes` removed (always false), and the `BoardPresenterLifecycleTests.cs` clip assertion replaced by BoardKitLifecycleTests coverage; both in the gameplay release diff
- `Tests/LevelPreview/LevelPreviewTests.cs` fixture rebinding and one rename
- the PROJECT.md decision row is DEC-123 (DEC-102 was taken)
- Expected 3 "renderer cache stays lazy" is replaced by `BoardPieceView.CreateCache()` called from `ModularBlockView.Prepare`, which the zero-allocation Rocket test requires.

## Ownership

- Owner/writer role: board-art refactor session (this task); sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Gameplay/Board/BoardPresenter.cs (factory construction; BlockShapeLibrary→BoardArt field and constructor only)
  - Assets/ColorBlockJam/Gameplay/Composition/GameSceneInstaller.cs (BoardArt binding only)
  - Assets/ColorBlockJam/Tests/Gameplay/** (fixtures that construct BoardPresenter only)
  - Assets/ColorBlockJam/Presentation/Board/BlockPieceFactory.cs
  - Assets/ColorBlockJam/Presentation/Board/BoardArt.cs
  - Assets/ColorBlockJam/Presentation/Board/Composition/BoardArtInstaller.cs
  - Assets/ColorBlockJam/Presentation/Board/BoardPieceView.cs
  - Assets/ColorBlockJam/Presentation/Board/ModularBlockView.cs
  - Assets/ColorBlockJam/Presentation/Board/BlockPiece.cs
  - Assets/ColorBlockJam/Presentation/Board/BlockPieceFactory.cs
  - Assets/ColorBlockJam/Presentation/ColorBlockJam.Presentation.asmdef
  - Assets/ColorBlockJam/Environment/EnvironmentDatabase.cs
  - Assets/ColorBlockJam/Environment/EnvironmentComposer.cs
  - Assets/ColorBlockJam/Environment/EnvironmentPiecePlacement.cs
  - Assets/ColorBlockJam/Environment/EnvironmentView.cs (tint branch and comment only)
  - Assets/ColorBlockJam/Environment/EnvironmentBarrierComposer.cs (placement construction only)
  - Assets/ColorBlockJam/Tests/Environment/EditMode/EnvironmentBarrierComposerTests.cs (tint assertions only)
  - Assets/ColorBlockJam/Tests/Environment/PlayMode/EnvironmentBarrierViewTests.cs (tint assertions only)
  - Assets/ColorBlockJam/Editor/BoardKit/ShapePrefabAuthoring.cs
  - Assets/ColorBlockJam/Editor/BoardKit/BoardKitAssets.cs
  - Assets/ColorBlockJam/Editor/BoardKit/BoardArrowAssets.cs
  - Assets/ColorBlockJam/Editor/BoardKit/BoardKitPreview.cs
  - Assets/ColorBlockJam/Editor/BoardKit/BoardKitMigration.cs
  - Assets/ColorBlockJam/Editor/BoardKit/PieceEditorWindow.cs (preview construction only)
  - Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewHost.cs (constructor and factory construction only)
  - Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewOwner.cs (asset loads only)
  - Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewBlockMapper.cs (identity check only)
  - Assets/ColorBlockJam/Tests/EditMode/BoardKitTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/ShapePrefabTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/ShapeLibraryJsonTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/BoardArtTests.cs
  - Assets/ColorBlockJam/Tests/PlayMode/BoardKitLifecycleTests.cs
  - Assets/ColorBlockJam/Tests/PlayMode/BoardArtContainerTests.cs
  - Assets/ColorBlockJam/Tests/Environment/EditMode/EnvironmentComposerTests.cs
  - Assets/ColorBlockJam/Tests/Environment/PlayMode/EnvironmentViewLifecycleTests.cs
  - Assets/ColorBlockJam/Tests/LevelPreview/LevelPreviewTests.cs (host construction only)
  - Assets/ColorBlockJam/Prefabs/Board/** (reserialize only)
  - Assets/ColorBlockJam/Prefabs/Environment/FloorBase.prefab (reserialize only)
  - Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset
  - Assets/ColorBlockJam/Scenes/Game.unity (GameSceneInstaller serialized asset fields only; see Revision 2)
  - docs/Environment-Layout.md (tint note)
  - docs/art/board-kit/** (migration note and captures)
  - PROJECT.md (Decision Log row DEC-102; Performance Rule 1 wording for the single colour owner)
  - production/tasks/TASK-20260924-224656127-e361499a-board-art-dependency-injection/**
  - production/tasks/TASK-20260924-191842745-88e64a0d-board-kit/contract.md (history line only)
  - production/tasks/TASK-20260924-222616741-46522211-level-editor-scene-preview/contract.md (history line only)
  - production/tasks/TASK-20260924-185655569-dea430a7-play-area-environment/contract.md (history line only)
- Forbidden paths/actions:
  - Packages/**, ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Resources/Onity/ProjectContext.prefab
  - Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab and any other Prefabs/Environment file except FloorBase.prefab
  - Assets/ColorBlockJam/Environment/EnvironmentSceneInstaller.cs, EnvironmentPreview.cs
  - Assets/ColorBlockJam/Presentation/Board/BoardPieceCatalog.cs
  - Assets/ColorBlockJam/Data/Art/GamePalette.asset, Data/Board/BoardPieceCatalog.asset, Data/Board/BlockShapeLibrary.asset, Data/Board/PieceDatabases/**
  - Assets/ColorBlockJam/Art/** (FBX, textures, MAT_Board.mat)
  - Assets/ColorBlockJam/Tests/EditMode/*.asmdef, Tests/PlayMode/*.asmdef, Tests/Environment/**/*.asmdef
  - Scenes other than Game.unity
  - Running "Build Board Kit Assets" or "Save Shape Prefabs" as part of the migration
  - Changing `ShapePrefabAuthoring.Signature`
  - Adding `[Inject]` to pooled components or calling `container.Inject` per pool Get
  - `System.Linq`, `string.Format`, static accessors, `OnityUiServiceLocator`
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Prefabs/Board/Shapes/Piece_000.prefab … Piece_033.prefab (34)
    Sole writer: this session through "Reserialize Board Kit Prefabs" (`AssetDatabase.ForceReserializeAssets`)
    Matching `.meta` owner: unchanged GUIDs; no `.meta` write
  - Asset: Assets/ColorBlockJam/Prefabs/Board/Pieces/{Center,Edge,OuterCorner,InnerCorner,Wall,Corner_1,corner_3,corner_5,Door_1x1,Door_Arrow,Grid,Arrow_1,Arrow_2,Arrow_3}.prefab (14)
    Sole writer: this session through "Reserialize Board Kit Prefabs"
    Matching `.meta` owner: unchanged GUIDs; no `.meta` write
  - Asset: Assets/ColorBlockJam/Prefabs/Board/ModularBlock.prefab
    Sole writer: this session through "Reserialize Board Kit Prefabs"
    Matching `.meta` owner: unchanged GUID
  - Asset: Assets/ColorBlockJam/Prefabs/Environment/FloorBase.prefab
    Sole writer: this session through "Reserialize Board Kit Prefabs", after floor-base-per-tile lands
    Matching `.meta` owner: unchanged GUID
  - Asset: Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset
    Sole writer: this session through Unity APIs (two new fields at defaults), after floor-base-per-tile and environment-rule-set-grid land
    Matching `.meta` owner: unchanged GUID
  - Asset: Assets/ColorBlockJam/Scenes/Game.unity
    Sole writer: this session through MCP for Unity or the Editor (BoardArtInstaller component and m_installers entry on SceneScope)
    Matching `.meta` owner: unchanged GUID
- Other active writers/worktrees checked:
  - TASK-20260924-191842745-88e64a0d-board-kit (`ready`, phase verification): live writer on PieceEditorWindow.cs (01:55) and BoardKitTests.cs (01:50); sole writer of every Prefabs/Board asset. This task waits, then inherits.
  - TASK-20260924-222616741-46522211-level-editor-scene-preview (`ready`, phase planning; live writer on Editor/LevelPreview/** and Tests/LevelPreview/** at 02:03-02:05): its contract stops on Presentation API changes; this task waits for it to land.
  - TASK-20260924-224137887-1a445bb0-floor-base-per-tile (`ready`): owns EnvironmentView.cs, EnvironmentDatabase.asset, FloorBase.prefab, Environment.prefab; lands first.
  - TASK-20260924-231413359-d89f8597-environment-rule-set-grid (`ready`): owns Environment/**, Data/Environment/**, Tests/Environment/**; lands first.
  - TASK-20260924-185655569-dea430a7-play-area-environment (verification): its verifier re-runs Tests/Environment/** after this task.
- Execution dependency: TASK-20260925-002022060-4c9dac46-block-runtime-objects handed off; the gameplay session's BoardPresenter 4-argument Prepare change has landed; message session "Buluan parça sürükleme ve gate mekanikleri" before starting
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given the repository after migration, when `grep -rlE "guid: (e13fe037917f25e4e9b6f520b747555f|baefb98cfae26c648b5c2cd923368ba5|15e437ff7d9c59d4daa9be1f9a882770)" Assets/ColorBlockJam/Prefabs` and `grep -rlE "m_palette|m_catalog|m_shapes|m_library|m_useArrowColor|m_defaultColor|m_colorType" Assets/ColorBlockJam/Prefabs` run, then both return nothing, and the palette GUID appears in scene/asset/prefab files only in `Scenes/Game.unity` and `Data/Environment/EnvironmentDatabase.asset`.
2. Given the 34 shape prefabs after reserialize, then every GUID, nested fileID, `m_generatedSignature`, `BlockShapeLibrary.asset` byte and `docs/art/board-kit/json-migration-source.json` entry is unchanged, `ShapePrefabAuthoring.IsCurrent` is true for every bound library shape, and `cm status` lists exactly 34 + 14 + 1 + 1 prefabs, 1 data asset and 1 scene besides code and asmdef changes (EditMode).
3. Given a `BoardPieceView` taken from a pool, coloured red, released and taken again, when its owner calls `SetColor(blue)`, then its property block is blue, `OnPoolRelease` performed no property-block write, and clip state is reset (PlayMode).
4. Given `BlockPieceFactory(new BoardArt(palette, catalog, library), root)` prepared for one type, when `Create` runs with `PaletteColor.LightBlue` and `TryRemoveCell` removes one cell, then tile colours equal `palette.GetColor(LightBlue)`, `ShapeType` is the source type after `Create` and null after the removal, the recompose allocates 0 B, and after `Return` + `Dispose` every tile and root is pooled with no `Instantiate` after Prepare (PlayMode).
5. Given the Game scene `SceneContext`, when built, then `BoardArt` resolves as one instance, `BlockShapeLibrary.Shapes` is loaded before any `Get`, and the scene context disposes without live subscriptions or tasks (PlayMode).
6. Given a level layout composed by `EnvironmentComposer`, then every placement carries a tint: floor `GetFloorColor()`, wall/corner `GetWallColor()`, door/arrow unchanged, floor base `GetFloorBaseColor()`; `EnvironmentViewLifecycleTests` colour assertions pass (EditMode/PlayMode).
7. Given the Piece Editor preview, BoardKitPreview (blocks and environment) and LevelPreview host, when opened without a container, then blocks and environment render with palette colours and the Console is clean (manual).
8. Given level 1 in the Game view at 1080x1920 and 20:9, when captured before and after, then the board is visually identical (manual, user acceptance).
9. Given all tests under `Tests/EditMode`, `Tests/PlayMode`, `Tests/Environment`, `Tests/LevelPreview`, then they pass, and `grep "System.Linq\|string.Format"` over Presentation and Runtime is unchanged from baseline.

## References and constraints

- Story/GDD/ADR/design/log/image: user request 2026-09-25 (Turkish, ScriptableObject references); PROJECT.md Performance Rules 1, 3, 4, 5, 7, 8 and Architecture Rules (assembly boundaries, thin views); DEC-96, DEC-99, DEC-100; `docs/Rendering-Reference-Review.md`; user skill `~/.claude/skills/onity-use/SKILL.md`; this design document (sections 1-8); `VacuumMotionAssets.cs:94` (force-reserialize precedent).
- Unity version/platform: 2022.3.62f2; Android/iOS portrait.
- Package/architecture constraints: Onity v0.3.13 (`BindInstance`, `RegisterBuildCallback`, no pool injection, no `WithArguments`, no `FromInstance`); no new package; no new assembly; Presentation gains `Onity.DI` and `Onity.Unity` (medium-risk plan item, approved with this contract); Runtime never references Presentation.
- Explicit out of scope: the runtime level session or binding `BlockPieceFactory` in a scene; dedicated floor/wall materials or SRP Batcher measurement (NFR-P5); tinted Prefab-Mode thumbnails (default `_BaseColor` is white); saving `GeneratedShape` inactive; removing the hidden duplicate hierarchy; `BoardPieceCatalog.m_environment` and an arrow presenter; `Environment.prefab` edits; changes to `GamePalette` entries, `MAT_Board.mat` or `ShapePrefabAuthoring.Signature`.

## Verification and risk

- Focused test path/command: `Assets/ColorBlockJam/Tests/EditMode/{BoardKitTests,ShapePrefabTests,ShapeLibraryJsonTests,BoardArtTests}.cs`, `Tests/PlayMode/{BoardKitLifecycleTests,BoardArtContainerTests}.cs`, `Tests/Environment/**`, `Tests/LevelPreview/LevelPreviewTests.cs`; MCP for Unity `run_tests` when connected, else the `%TEMP%` isolated batchmode copy with `Unity.exe -runTests -testPlatform EditMode|PlayMode -logFile`, results copied back (memory: isolated-copy verification).
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`, `clean-oop-architecture`, `onity-use`
- Unity compilation/Console evidence: clean compile after the asmdef change; clean Console during reserialize and Game scene open.
- EditMode: acceptance 1, 2, 6 (composer), 9
- PlayMode: acceptance 3, 4, 5, 6 (view)
- Manual/built-player/profiler/device evidence: acceptance 7 and 8 captures under `docs/art/board-kit/`; Profiler GC Alloc 0 B over one Rocket removal; no `BoardPieceView` sample outside Playing.
- Risk: high (50 prefab reserializes, one data asset, one scene, one asmdef, four upstream tasks)
- Approval already granted (exact target only): the user's 2026-09-25 request authorizes the design; the serialized targets under "Serialized assets" and the Presentation asmdef change still require the user's explicit go before the first write.
- Commit/checkin constraint: forbidden
- Current invocation permission: none (WAITING_FOR_OWNER)
- Handoff destination: /implement-task TASK-20260924-224656127-e361499a-board-art-dependency-injection, then an independent verifier; notify the play-area-environment verifier to re-run Tests/Environment/**.

## Stop conditions

- Any of board-kit, level-editor-scene-preview, floor-base-per-tile or environment-rule-set-grid is still live or unlanded (stay WAITING_FOR_OWNER; never a second writer).
- Reserialize would change a shape prefab GUID, a nested fileID, a `m_generatedSignature`, or any byte of `BlockShapeLibrary.asset`, the JSON or `BoardPieceCatalog.asset`.
- A listed prefab is open in Prefab Mode at reserialize time, or the scripts have not compiled cleanly before it.
- The user rejects white untinted Prefab-Mode previews (needs a separate editor-only tint decision).
- `BoardArt.Validate` cannot load the library through `Library.Shapes` without a Runtime edit.
- Achieving 0 B in `TryRemoveCell` would require changing `BlockPieceComposer` or `PrefabComponentPool`.
- MCP for Unity reports no connected instance when a live-Editor step (scene edit, tests, Console) is needed and the isolated batchmode copy is also unavailable.
- Any change would require `Assets/ColorBlockJam/Runtime/**`, `ProjectContext.prefab`, `Environment.prefab` or a test asmdef.
```

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:68008bd617561591133557dbef4078d0729404661be51e076e41be67bebaeeff
- Evidence fingerprint: sha256:cc22a7c507065d4627c886006a774c8759481f5f919c1a60d8e5c861964ec39a
- Reviewed revision: vcs:plastic:cs:8@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a66321f24e92cefd81e0318562f2c2e44bdb441f92ff7d4af10c462799fb31b7
- Attempt count: 1
- Last feedback: verifier 2: AC-1..AC-7 and AC-9 PASS; AC-8 needs user acceptance; barrier 0 B test order-dependent in the full gate (barrier task)
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:8@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a66321f24e92cefd81e0318562f2c2e44bdb441f92ff7d4af10c462799fb31b7
- Closed at: 2026-09-25T19:32Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | independent verifier 2 (evidence.md, Independent verification 2): prefab greps empty; palette GUID only in Game.unity and EnvironmentDatabase.asset | vcs:plastic:cs:8@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a66321f24e92cefd81e0318562f2c2e44bdb441f92ff7d4af10c462799fb31b7 |
| AC-2 | PASS | independent verifier 2 (evidence.md, Independent verification 2): 28 prefabs only SO-field lines removed; GUIDs/fileIDs/signatures/.meta unchanged; BlockShapeLibrary.asset byte-identical | vcs:plastic:cs:8@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a66321f24e92cefd81e0318562f2c2e44bdb441f92ff7d4af10c462799fb31b7 |
| AC-3 | PASS | independent verifier 2 (evidence.md, Independent verification 2): PooledTileTakesItsOwnersColourAndReleaseMakesNoPropertyBlockWrite (PlayMode) | vcs:plastic:cs:8@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a66321f24e92cefd81e0318562f2c2e44bdb441f92ff7d4af10c462799fb31b7 |
| AC-4 | PASS | independent verifier 2 (evidence.md, Independent verification 2): factory tint + Recorder-based Is.Not.AllocatingGCMemory on first Rocket removal (red-checked) | vcs:plastic:cs:8@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a66321f24e92cefd81e0318562f2c2e44bdb441f92ff7d4af10c462799fb31b7 |
| AC-5 | PASS | independent verifier 2 (evidence.md, Independent verification 2): BoardArtContainerTests (one instance, released on unload, no live task/subscription, scope-build load) | vcs:plastic:cs:8@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a66321f24e92cefd81e0318562f2c2e44bdb441f92ff7d4af10c462799fb31b7 |
| AC-6 | PASS | independent verifier 2 (evidence.md, Independent verification 2): EnvironmentComposerTests + EnvironmentViewLifecycleTests tints; 6 placement sites tinted | vcs:plastic:cs:8@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a66321f24e92cefd81e0318562f2c2e44bdb441f92ff7d4af10c462799fb31b7 |
| AC-7 | PASS | independent verifier 2 (evidence.md, Independent verification 2): live LevelPreview host, Level Editor preview, Piece Editor, kit preview palette colours; captures verifier2/ | vcs:plastic:cs:8@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a66321f24e92cefd81e0318562f2c2e44bdb441f92ff7d4af10c462799fb31b7 |
| AC-8 | UNPROVEN | needs user visual acceptance: docs/art/board-kit/board-art-di/compare/*.png (1080x1920 4 px <=1/255; 1080x2400 6 px <=6/255, wall joins) | vcs:plastic:cs:8@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a66321f24e92cefd81e0318562f2c2e44bdb441f92ff7d4af10c462799fb31b7 |
| AC-9 | PASS | independent verifier 2 (evidence.md, Independent verification 2): EditMode 874/874, PlayMode Gameplay 11/11 + Presentation/Environment 39/39; 0 System.Linq/string.Format | vcs:plastic:cs:8@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a66321f24e92cefd81e0318562f2c2e44bdb441f92ff7d4af10c462799fb31b7 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the board-art DI design workflow (decision owner plus critic) for the user's request to pass shared ScriptableObjects from one system through Onity DI
- 2026-09-25T09:11Z — approval recorded — user answered "Yes, go ahead" on 2026-09-25: re-serialize about 50 block/piece prefabs (GUIDs kept), BoardArtInstaller on the Game scene SceneScope, Onity.DI/Onity.Unity in the Presentation asmdef
- 2026-09-25T11:11Z — contract revised before execution — rebased on block-runtime-objects and the gameplay assembly; BoardArt bound in GameSceneInstaller; narrow Gameplay access granted by the gameplay session; Gameplay test gate added
- 2026-09-25T15:49Z — contract revised before execution (Revision 2) — barrier view and Piece Editor level link landed: EnvironmentBarrierComposer tint and barrier tests added narrowly, PieceEditorWindow members to keep, Game.unity limited to GameSceneInstaller fields, START/RELEASE + diff protocol with the gameplay session; dependencies met (4c9dac46 handed off, BoardPresenter 4-argument Prepare landed)
- 2026-09-25T18:34Z — verification — chain wf_15df5b4b-2a3: implement, 3 reviewers (8 findings), fix (plus a concurrent resumed fix copy 20:27-20:52, final code verified after it), verify, retry-fix, re-verify: AC-1..AC-7, AC-9 PASS; AC-8 user acceptance pending; Revision 3 accepts compile-forced edits; RELEASE with diff sent to the gameplay session
- 2026-09-25T19:32Z — closed — USER_CONFIRMED closure — user order "bunları kapat" (2026-09-25) accepting the AC-8 before/after look (4 px differ) and the white untinted piece prefabs in Prefab Mode (stop condition cleared); AC-8 row preserved as recorded; PieceEditorWindow.cs was released to "Database oluşturma popup" and later D-001 calibration touched Corner_1/Wall/Door_1x1 and BoardKitAssets.cs after this task's review
<!-- TASK-LIFECYCLE:END -->
