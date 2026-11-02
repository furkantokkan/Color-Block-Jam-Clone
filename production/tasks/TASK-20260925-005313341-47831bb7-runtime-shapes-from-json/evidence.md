# Evidence: TASK-20260925-005313341-47831bb7-runtime-shapes-from-json

Implementer run, 2026-09-25 23:30 to 2026-09-26 00:30 (local). Plastic workspace cs:9 plus pending changes from
other sessions; nothing checked in or shelved. Live Editor: MCP for Unity HTTP 127.0.0.1:8082, instance
`ColorBlockJamCase2026@99077e4df1ecfaa4`. The user's open scene `LevelEditor.unity` stayed loaded and clean
(`dirty=False` before and after every step); no scene or serialized asset was saved.

## What was built (DEC-124)

- `Runtime/Board/BlockShapeCatalog.cs` (+ `BlockShapeCatalog.Footprint.cs`): plain C# catalog.
  Library first: step 1 by `TypeName` (never for a `Custom_` id), step 2 by a library prefab's canonical
  footprint (rotation-only match first, then library order), step 3 a learned shape the id names, step 4
  learning, which exists only as `TryLearn` before `Seal()`. Learned shapes are keyed by the 25-bit canonical
  footprint code (8 transforms, bit y*5+x, same code as `LevelCustomPiece.GetCustomId`); plain ids are aliases;
  `BlockShapeType` is never used for them. The spawn source of a learned shape is the sentinel
  `BlockShapeCatalog.LearnedSourceType = (BlockShapeType)(-1)`; library IDs are validated nonnegative, so it never
  collides, and `BlockShapeType.cs` is untouched.
- `Runtime/Board/BlockShapeCatalogLoader.cs`: the project-scope step: load cache, learn every level, report
  library errors / rejections / warnings once, add cached records (current levels win), write the cache at most
  once, seal (in `finally`).
- `Runtime/Board/RuntimeShapeCache*.cs`: `RuntimeShapeCache` (the app-scoped owner, own
  `CancellationTokenSource`, one load, at most one write, skips an unchanged or empty write, corrupt/unreadable
  cache = one warning and rebuild), `RuntimeShapeCacheJson` (versioned, strict, `TypeNameHandling.None`),
  `RuntimeShapeCacheRecord`, `IRuntimeShapeCacheStore`, `RuntimeShapeCacheFileStore` (temporary file, then
  `File.Replace`/`File.Move`; a cancelled write never replaces), `RuntimeShapeCacheMemoryStore` (Editor play).
- `Runtime/Board/link.xml`: preserves `RuntimeShapeCacheJson/Document`, `/ShapeDto` and `RuntimeShapeCache`.
- `Runtime/Composition/ProjectScopeInstaller.cs`: optional `m_shapeLibrary`; when assigned it binds the catalog,
  the store (memory in the Editor, `persistentDataPath/shapes/runtime-shapes.json` in players) and
  `RuntimeShapeCache` as a container singleton, and runs the loader right after `LevelDatabase.LoadAsync`, inside
  the same awaited build callback (before Init routes to Hub). Unassigned: one warning, no catalog (see Open).
- `Presentation/Board/BlockPieceFactory.cs`: `Prepare(LearnedSourceType, ...)` (3- and 4-argument) builds one
  generic pool. Its template is the first bare library shape prefab (a `BlockPiece` + root `ModularBlockView`, no
  child; all shape prefabs are identical bare roots since DEC-120), so no per-shape prefab, no runtime prefab
  creation and no `ModularBlock.prefab` change were needed. `HasLearnedSource(library)` lets callers reject
  before touching the scene; `Create` refuses a learned spawn without resolved cells. `BoardPresenter` stays the
  only runtime construction site.
- `Presentation/Board/BlockPiece.cs`: `PrepareLearned(catalog, cellCapacity, pieceCapacity)`, `IsLearned`;
  `SourceType` reports `LearnedSourceType` for a learned root; `Release` clears it.
- Gameplay (granted lines only): `ShapeResolver` now resolves through the catalog; `AttemptLevelConverter` gains
  `TryConvert(level, layout, BlockShapeCatalog, ...)` (rejected level returns its load message; learned blocks get
  `LearnedSourceType` and keep the level's cells) and its library overload delegates through a library-only
  catalog; `BoardPresenter.TryGetPoolNeeds` source-type lines only (learned: check `HasLearnedSource`, no library
  lookup; `shapeCells` 0).
- Editor preview: `LevelPreviewBlockMapper` learns the draft in its own editor-only catalog, resolves each block
  and checks the source; `LevelPreviewHost.PrepareFactory` skips the library lookup for learned blocks.
- Docs: `docs/Level-Data-Format.md` "Runtime-learned shapes" section; `PROJECT.md` one sentence in the level-data
  rule and the DEC-124 row (DEC-121..123 were taken).

## Acceptance

| AC | Result | Evidence |
|---|---|---|
| AC-1 Unknown shape learned and previewed | PARTIAL (catalog + LevelEditor preview PASS; project load in the shipped composition BLOCKED, see Open 1) | `BlockShapeCatalogTests.AnUnknownShapeIsLearnedFromItsCellsAndResolvesToThem` (ZigZag5 learned, level validates, mirrored block resolves, unseen id does not). `LevelPreviewTests.ALearnedShapeShowsItsCellsAndColourFromTheGenericPool`. Live capture through the real LevelEditor preview (`LevelPreviewOwner.OnLevelPublished`): `captures/leveleditor-preview-learned-zigzag5.png`, 1080x1920; `Piece_000(Clone) learned=True source=-1 cells=(0,0)(0,1)(1,1)(1,2)(2,2) tiles=12`, Square `learned=False source=1 tiles=9`; the original level-01 was republished afterwards (`restored=level-01`), LevelEditor.unity `dirty=False`. |
| AC-2 Saved and reloaded | PASS | `RuntimeShapeCacheTests.ALearnedShapeIsSavedAtomicallyAndKnownAfterARestartWithoutItsLevel` (temp directory, `formatVersion` 1, no `.tmp` left, restart with no levels resolves ZigZag5, unchanged cache not rewritten), `ACorruptCacheIsIgnoredWithOneWarningAndRebuilt`, `MalformedCacheTextIsRefusedAsAWhole` (7 cases incl. `$type`, case-changed field, non-canonical code, `Custom_` alias, duplicate id), `CacheTextRoundTrips`. |
| AC-3 Invalid learned shapes rejected | PASS | `InvalidLearnedCellsRejectTheLevelNamingLevelBlockAndShape` (disconnected, duplicated, 26 cells, 6 x 1), `TheSameUnknownIdWithAnotherFootprintRejectsTheLaterLevel`, `ACustomIdIsLearnedByItsFootprintAndMustNameItsOwnCells`, `ARotatedOrMirroredBlockWithTheSameIdIsTheSameLearnedShape`; `RuntimeShapeCacheTests.ARejectedLevelIsLoggedAndCachesNothing`; `AttemptLevelConverterTests.ALevelRejectedAtLoadFailsWithTheLoadMessage`; `LevelPreviewTests.AShapeThatCannotBeLearnedIsRejectedNamingLevelBlockAndShape`. |
| AC-4 Generic pooled spawning, no Playing instantiation | PASS | PlayMode `LearnedShapeAttemptTests.ALearnedShapeIsPooledOnHomeAndLeavesThroughItsDoorWithoutInstantiating`: real library, presenter and `LevelSession`; pools `Block roots Learned` and `Block roots Single` exist after Home; learned root `IsLearned`, cells = level cells; the learned block is dragged out through its door, the root returns to its pool, `PreparedPieceCount` and the BoardRoot object count are unchanged from Home. |
| AC-5 Built-in behaviour unchanged | PASS | Full EditMode and PlayMode suites green (below), including the unchanged Gameplay drag/session tests and `GameSceneDragTests` (real Game scene, level 1). `ProjectScopeShapeCatalogTests.TheProjectScopeLearnsAtLoadWritesAtMostOnceAndSealsTheCatalog` converts every authored level through the sealed project catalog (since the 2026-09-26 review round it no longer asserts authored-content details; the first run showed library sources and no warning for levels 1-5). |
| AC-6 Store patch takes over, no duplicate | PASS | `BlockShapeCatalogTests.AStorePatchThatShipsALearnedFootprintTakesItOverWithoutASecondShape` (Custom_ id and RemotePiece resolve to NewPiece type 40, mirrored footprint, cached entry ignored, learned count 0, next records empty); `RuntimeShapeCacheTests.AfterAPatchTheCachedEntryIsDroppedAtTheNextWrite` (store text is empty after the write). |
| AC-7 Id and enum safety | PASS | `ARemoteTypeNameIsLearnedAndAfterThePatchResolvesByNameKeepingDifferentCellsWithOneWarning`, `TwoUnknownIdsWithOneFootprintAreOneLearnedShape`, `ACustomIdNeverResolvesByNameAndACustomLibraryTypeNameIsReportedOnce`, `ABuiltInIdWithOtherCellsKeepsTheLevelCellsAndWarnsOnce`, `ALibraryShapeWinsByNameAndByFootprintAndIsNeverLearned`. Code identity: `CanonicalCodesEqualTheLevelEditorsCustomIdsForEveryFootprintUpToSixCells` (all 305 learnable fixed polyominoes of 1-6 cells), `...ForLargeRandomFootprints` (400 random footprints up to 25 cells, all 8 transforms), `OnlyTheCanonicalUppercaseEncodingIsACustomId`. |
| AC-8 Learned at load, sealed | PARTIAL (installer with the library assigned PASS; shipped ProjectContext/Game path BLOCKED, see Open 1) | `ASealedCatalogResolvesButNeverLearns`, `AttemptLevelConverterTests.ASealedCatalogNeitherLearnsNorResolvesAnUnseenShape`, `RuntimeShapeCacheTests.TheCacheLoadsOnceAndWritesAtMostOncePerLoad`; PlayMode `ProjectScopeShapeCatalogTests.TheProjectScopeLearnsAtLoadWritesAtMostOnceAndSealsTheCatalog` builds the real `ProjectScopeInstaller` (copied from ProjectContext.prefab onto an unsaved object, library assigned) in a new `OnityContainer`: the async build loads the Addressable JSON, learns and seals; the store is the memory store; `WriteCount <= 1`; `TryLearn` afterwards throws; disposing the container disposes `RuntimeShapeCache`. |

## Verification runs (live Editor, MCP `run_tests`)

- Clean compile after every edit: `EditorUtility.scriptCompilationFailed=False`; last compile after the final edit
  (link.xml + PlayMode test), Console had no compiler error.
- Focused EditMode (BlockShapeCatalogTests, RuntimeShapeCacheTests, LevelPreview tests, AttemptLevelConverterTests):
  job a2733f50bd82473488d79e7c2684dbd2 54/56 (two wrong test expectations: fixed-polyomino count and a footprint
  resolution the test misread), then job 0c363034841049cba3732c4d464a3416 **56/56**.
- Focused PlayMode `LearnedShapeAttemptTests`: 86b285ba… 0/1 (the test waited 60 frames, shorter than the exit
  tween; now waits up to 3 s), d68ae152… 1/1, final 08f9126493c6474595d189723b8865f8 **2/2**.
- Full EditMode (final state): job ce7c8fd882cf429cac624853c085d66c, **1021 passed, 0 failed, 1 skipped**
  (pre-existing `[Ignore]`), 00:21-00:23. Includes ColorBlockJam.Gameplay.EditModeTests.
- Full PlayMode (final state): job 60845ca47d444b8aaeb5030ad62c6bd3, **56/56 passed**, 00:23-00:24. Includes
  ColorBlockJam.Gameplay.PlayModeTests (GameSceneDragTests plus the 2 new tests).
- An earlier full PlayMode run (job c06ae0bc64b34e94b18491f347ef377c, before the project-scope test) had 54/55
  with `EnvironmentBarrierViewTests.BarrierFlipsPauseAndStopAllocateNothing` failing; alone it passed (job
  07c2a8a438a54fa9b21ed76263f1d087, 6/6) and it passed in the final full run. This is the known order-dependent
  PrimeTween pool warm-up failure recorded in the board-art-DI evidence ("Gate finding"), not this task.
- Grep over the new and changed Runtime/Presentation/Gameplay/LevelPreview files: no `System.Linq`,
  `string.Format`, `async void`, `Update()`; no `Instantiate` added.
- Console after the final runs: only test-expected logs (corrupt-cache warning, `Level rejected: ...`, PieceEditor
  link test's "Test listener failure", 13-cell enumeration warning) and the expected
  "Project shape library is not assigned" warning from the Play Mode ProjectContext.

## Open items and risks

1. **Wiring outside this contract (needs an approved change by the owners):**
   - `Assets/ColorBlockJam/Resources/Onity/ProjectContext.prefab`: assign `ProjectScopeInstaller.m_shapeLibrary`
     = `Data/Board/BlockShapeLibrary.asset` (one serialized reference). Until then the project scope logs one
     warning and binds no catalog.
   - `Assets/ColorBlockJam/Gameplay/Composition/GameAttemptLoader.cs` (gameplay session): pass the project
     catalog, `AttemptLevelConverter.TryConvert(data, layout, resolver.Resolve<BlockShapeCatalog>(), ...)`.
     Today it uses the library overload, which resolves library names and library footprints but never a
     learned shape. So a learned shape plays in the real Game scene only after both one-line changes; the
     learned-shape play is proven with the real presenter/session/library in `LearnedShapeAttemptTests`.
2. Expected 5 (Level Editor "Add to Piece Library") skipped as not trivial: follow-up for the Level Editor /
   board-kit owners.
3. Piece Editor refusing `Custom_` names: `BlockShapeLibraryJson.IsTypeName` (`[A-Z][A-Za-z0-9]*`) already
   rejects any underscore, so a `Custom_` TypeName cannot reach a saved library; the catalog still reports one if
   it appears. The board-kit follow-up can be closed or kept as a UI message only.
4. IL2CPP: cache read/write and the Onity-built `RuntimeShapeCache` are unverified on device (link.xml added);
   pending the next Android IL2CPP build. `File.Replace` atomicity on Android is also a device check.
5. Behaviour change by design: through any catalog (including the library overload), an unknown id whose cells
   equal a library prefab footprint now resolves to that library shape instead of failing (step 2). Existing
   levels use library TypeNames only, so they load identically.
6. Cosmetic: `LevelPreviewHost`'s once-per-host "enumeration limit" warning prints the source as `-1` for a
   learned block above 12 cells.
7. The learned-shape pool clones a library prefab, so learned roots are named `Piece_000(Clone)` in the hierarchy.

## Review fix round (2026-09-26)

Seven reviewer findings (two pairs were duplicates). Files touched:
`Runtime/Board/BlockShapeCatalog.cs`, `Runtime/Board/RuntimeShapeCacheJson.cs`, `Runtime/Board/RuntimeShapeCache.cs`,
`Runtime/Board/BlockShapeCatalogLoader.cs`, `Runtime/Composition/ProjectScopeInstaller.cs`,
`Tests/EditMode/RuntimeShapeCacheTests.cs`, `Tests/Gameplay/PlayMode/LearnedShapeAttemptTests.cs`,
new `Tests/Gameplay/PlayMode/ProjectScopeShapeCatalogTests.cs` (+ Unity-generated `.meta`),
`docs/Level-Data-Format.md`, `PROJECT.md`.

- **Findings 1 and 4 (major, confirmed, BLOCKED outside this contract).** `ProjectContext.prefab` has no
  `m_shapeLibrary`, and `GameAttemptLoader.cs:89` uses the library-only `TryConvert` overload, so in the shipped
  composition nothing is learned and a learned shape fails the Game attempt with "Block {id} uses unknown shape".
  Neither file is in the allowed paths, and no in-contract workaround exists without static state or a service
  locator. AC-1 (project-load part), AC-8 (shipped path) and the Revision gate "a Game-scene level with a learned
  shape plays" are now recorded as PARTIAL/BLOCKED, not PASS. Required owner-approved edits: (1) assign
  `ProjectScopeInstaller.m_shapeLibrary` = `Data/Board/BlockShapeLibrary.asset` in
  `Resources/Onity/ProjectContext.prefab` through Unity APIs; (2) `GameAttemptLoader.cs:89` passes
  `resolver.Resolve<BlockShapeCatalog>()`. Then add an unpatched-prefab regression test and run the Game-scene
  learned-shape play check.
- **Findings 2 and 5 (minor, confirmed, fixed).** `TryLearn` now rejects a plain id longer than
  `BlockShapeCatalog.MaxLearnedIdLength` (128, shared with the reader) when its footprint is not a library footprint,
  with a `Level {id} block {id} shape {shapeId}` message; a long id on a library footprint still resolves to the
  library. `RuntimeShapeCacheJson.Write` now verifies its text with `Read` and throws `ArgumentException` for anything
  the reader would refuse (over 256 ids per footprint, over 4096 shapes, over 4 MiB, long or `Custom_` ids); the
  loader treats that like an I/O failure (one warning, previous file kept). A corrupt cache is always rewritten
  because the new text is readable and therefore never equals the corrupt text. Tests:
  `ANewPieceIdLongerThanTheCacheAllowsRejectsItsLevelAndTheCacheStaysReadable` (129-char id rejected, 128-char id
  cached, restart reads the file with no corrupt warning), `TheWriterRefusesTextItsReaderWouldRefuse`.
- **Finding 3 (minor, confirmed, fixed).** `ProjectScopeInstaller.LoadAsync` resolves `RuntimeShapeCache` before the
  first await and links the build token with the new `RuntimeShapeCache.LifetimeToken` (cancelled in `Dispose`) for
  the level load and the learning step, and checks it after the level load; the loader checks it after the cache
  read. "Stops:" comments in the installer and loader now name that token. Test:
  `TheLifetimeTokenIsCancelledWhenTheCacheIsDisposed`. The project-scope container path is dormant until Open 1.
- **Finding 6 (minor, confirmed, fixed).** `docs/Level-Data-Format.md` "Runtime-learned shapes" gains an Open note
  (inactive until the two edits land; what happens meanwhile) and the 128-character id rule; `PROJECT.md`
  level-data sentence gains the same Open caveat.
- **Finding 7 (minor, partly fixed).** The project-scope test moved into its own file,
  `ProjectScopeShapeCatalogTests.cs`, and no longer asserts authored-content details (no `Warnings` empty check, no
  "no learned source" check), so a level-pipeline change cannot turn it red unless a level fails to convert. It stays
  under `Tests/Gameplay` because `ColorBlockJam.Editor.EditModeTests` and the LevelPreview test asmdef do not
  reference `Onity.DI`, and their asmdefs are outside this contract. Moving it needs either that asmdef reference or
  the gameplay owner's approval to keep it. The split also removed two ONITY006 analyzer warnings from
  `LearnedShapeAttemptTests.cs`.

Verification (live Editor, MCP `run_tests`, instance `ColorBlockJamCase2026@99077e4df1ecfaa4`):
- Compile: `scriptCompilationFailed=False`; Console after the recompile had no compiler error or warning
  (only the MCP WebSocket notice).
- Focused EditMode `RuntimeShapeCacheTests` + `BlockShapeCatalogTests`: job 4c79eb00b3e94e15b96d2b8b4ffa6e8e, 35/35.
- Full EditMode: job 4131434f7b064e988ff21eae7ab92702, 1024 passed, 0 failed, 1 skipped (pre-existing `[Ignore]`).
- Full PlayMode: job d8811bd86d3048bca6cf78ec1cd13595, 56/56 passed (includes ColorBlockJam.Gameplay.PlayModeTests,
  GameSceneDragTests, LearnedShapeAttemptTests, ProjectScopeShapeCatalogTests).
- Console after the runs: only test-expected logs. `LevelEditor.unity` stayed loaded with `dirty=False`; no scene or
  serialized asset was saved.

## Independent verification (2026-09-26, verifier)

Read-only for production code and tests. Live Editor over MCP for Unity HTTP 127.0.0.1:8082, instance
`ColorBlockJamCase2026@99077e4df1ecfaa4`. Start and end state: not playing, not compiling, no compile failure,
`LevelEditor.unity` loaded, active and `dirty=False`; the Level Editor preview showed `level-01` before and after.
Nothing was saved, checked in or shelved.

### Test runs (MCP `run_tests`)

- Full EditMode: job f752cea2f1bf48e885147ba5ec800d71, **1024 passed, 0 failed, 1 skipped** (the existing
  `PieceEditorLinkTests.ShowLevelPieceWithoutFocusUpdatesTheOpenWindow` `[Ignore]`).
- Full PlayMode: job cb9fc9f6da714efc96d71496bb336e5e, **56/56 passed**. `EnvironmentBarrierViewTests` did not
  flake in this run.
- Focused EditMode with details: job b187d8e6749b4c5f9c2d7d46e4b01e44, **59/59 passed**: all 19 `BlockShapeCatalogTests`,
  all 16 `RuntimeShapeCacheTests`, all 14 `LevelPreviewTests` (including `ALearnedShapeShowsItsCellsAndColourFromTheGenericPool`,
  `AShapeThatCannotBeLearnedIsRejectedNamingLevelBlockAndShape`, `AnUnknownShapeWithALibraryFootprintMapsToThatLibraryShape`)
  and all 10 `AttemptLevelConverterTests`.
- Focused PlayMode with details: job 36b1cc30d5ed4004bec94ea47e1c8e1a, **2/2**
  (`LearnedShapeAttemptTests`, `ProjectScopeShapeCatalogTests`); job a879b267d04a422092af3a17a276e7ad, **1/1**
  (`GameSceneDragTests.EveryBlockOfLevelOneLeavesThroughItsDoorAndTheLevelIsWon`).

### Independent live probes

- **Catalog and cache, end to end** (`execute_code`, in memory, with the real `BlockShapeLibrary` shapes and a real
  `RuntimeShapeCacheFileStore` in a throwaway `%TEMP%` directory, deleted afterwards). The piece was a P pentomino,
  which is not in the library.
  - Launch 1: level A has `Custom_0000067`, `RemoteP` (mirrored) and `AliasP` (rotated). Result: learned=1,
    writes=1, sealed. All three ids resolve to learned code `0000067`, and the unseen id `Unseen` does not resolve.
    `TryLearn` after sealing throws. The file is `formatVersion` 1 with ids `["RemoteP","AliasP"]`, and no `.tmp` is left.
  - Launch 2 (restart with no levels): learned=1, writes=0, and both ids still resolve.
  - Launch 3 (corrupt file `{ corrupt`): the file is rebuilt with the same content, writes=1.
  - Launch 4 (a store patch adds `NewPiece`, rotated, with a prefab): learned=0. All three ids resolve to library
    `NewPiece` (source 40), and the file becomes `"shapes": []`.
  - Launches 5 and 6: `ShipLater` is first learned. After a patch ships `ShipLater` with an F-pentomino footprint,
    it resolves by name to the library, with one warning ("Level vB block b1 shape ShipLater: its cells differ…").
    The level is not rejected.
  - Launch 7: `RemoteP` with a 4-line footprint is rejected ("Level vC block bx shape RemoteP: the id already names
    another footprint, Custom_0000067."), a disconnected `Gap` is rejected ("Level vD block by shape Gap: its cells do
    not all touch edge to edge."), and only level A's shape is cached.
- **Level Editor preview capture:** `captures/verifier-preview-learned-p5.png` (1080 x 1920). Captured through the
  real `LevelPreviewOwner.OnLevelPublished` path from an in-memory draft `verify-p5`:
  - red `Custom_0000067` and blue mirrored `RemoteP` come from the learned pool (`learned=True source=-1`, 11 tiles each);
  - orange `Plus` with the Cross footprint resolves to library Cross by footprint (`source=22`);
  - pink `Square` stays a library block (`source=1`).

  The first attempt was rejected for an invalid colour (`Green`), and the last valid preview stayed, as designed.
  `level-01` was republished afterwards, and `LevelEditor.unity` stayed `dirty=False`.
- **Authored levels:** every block in levels 1-5 (`Data/Levels/LevelDatabase.json`) uses a library `type`, so the
  new footprint step cannot change them.

### Shipped wiring (confirmed again)

- `Resources/Onity/ProjectContext.prefab` sets only `m_sceneFlowProfile`, `m_boosterCatalog` and `m_levelDatabase`.
  There is no `m_shapeLibrary`, and the Console shows "Project shape library is not assigned, so level JSON cannot
  add pieces (DEC-124)." during Play Mode.
- `Gameplay/Composition/GameAttemptLoader.cs:89` still calls
  `AttemptLevelConverter.TryConvert(..., resolver.Resolve<BlockShapeLibrary>(), ...)`. This is the library-only
  overload, which `AttemptLevelConverterTests.ALearnedShapeConvertsThroughTheSealedCatalogToTheGenericSource` proves
  never resolves a learned shape.
- Result: in the shipped app, no project catalog is bound or sealed, nothing is learned or cached, and a Game-scene
  level with a learned shape fails to build. The Revision gate "a Game-scene level with a learned shape plays" is
  **not met**.

### Changed files and scope

All files carrying this task's symbols are listed below. The `Plastic cm status` changes in these areas belong to
this task only.

| File | Allowed? |
|---|---|
| `Runtime/Board/BlockShapeCatalog.cs`, `BlockShapeCatalog.Footprint.cs`, `BlockShapeCatalogLoader.cs` (+ `.meta`) | yes (`BlockShapeCatalog*.cs`) |
| `Runtime/Board/RuntimeShapeCache.cs`, `…Json.cs`, `…Record.cs`, `…Store.cs`, `…FileStore.cs`, `…MemoryStore.cs` (+ `.meta`) | yes (`RuntimeShapeCache*.cs`) |
| `Runtime/Board/link.xml` | yes |
| `Runtime/Composition/ProjectScopeInstaller.cs` | yes |
| `Presentation/Board/BlockPieceFactory.cs`, `BlockPiece.cs` | yes |
| `Gameplay/Levels/ShapeResolver.cs`, `AttemptLevelConverter.cs` | yes |
| `Gameplay/Board/BoardPresenter.cs` | yes; the diff against the pre-task copy touches only the `TryGetPoolNeeds` source-type/`shapeCells` lines |
| `Editor/LevelPreview/LevelPreviewBlockMapper.cs`, `LevelPreviewHost.cs` | yes |
| `Tests/EditMode/BlockShapeCatalogTests.cs`, `RuntimeShapeCacheTests.cs` (+ `.meta`) | yes |
| `Tests/LevelPreview/LevelPreviewTests.cs` | yes |
| `Tests/Gameplay/EditMode/Gameplay/AttemptLevelConverterTests.cs` | yes |
| `Tests/Gameplay/PlayMode/LearnedShapeAttemptTests.cs` (+ `.meta`) | yes (it resolves shapes) |
| `Tests/Gameplay/PlayMode/ProjectScopeShapeCatalogTests.cs` (+ `.meta`) | **borderline**: inside `Tests/Gameplay/**`, but it tests the project-scope installer, not a fixture that resolves shapes. The gameplay owner should accept it or have it moved. |
| `docs/Level-Data-Format.md`, `PROJECT.md` (DEC-124 row, level-data sentence) | yes |
| `production/tasks/…-runtime-shapes-from-json/**` | yes |

No pending change touches `Runtime/Levels/**`, `Data/**`, `Scenes/**`, `BlockShapeType.cs`, `Editor/BoardKit/**`,
`Packages/**`, `ProjectSettings/**`, `ModularBlock.prefab`, `ProjectContext.prefab`, `GameAttemptLoader.cs` or any
asmdef. `TypeNameHandling` is `None` only, and a grep of the task files finds no `System.Linq`, `string.Format`,
`async void`, `Update()` or new `Instantiate`. The only `Instantiate` is the environment `Instantiate` in
`LevelPreviewHost.cs:282`, which predates this task.

Minor notes (not acceptance failures):
- Load-time messages in `BlockShapeCatalog*.cs` use `$"…"` interpolation rather than ZString. This is outside the
  Playing path.
- The IL2CPP cache read/write and `File.Replace` on Android are still unverified on a device.

### Verdict

| AC | Result | Basis |
|---|---|---|
| AC-1 | PASS on its named channels (EditMode + live capture) | Tests above and `verifier-preview-learned-p5.png`. In the shipped ProjectContext the project-load learning is dormant; see AC-8. |
| AC-2 | PASS | `RuntimeShapeCacheTests` plus the file-store probe (atomic write, restart, corrupt rebuild) |
| AC-3 | PASS | Catalog, cache, converter and preview tests, plus probe launch 7 |
| AC-4 | PASS | `LearnedShapeAttemptTests` (pools `Block roots Learned`/`Single` prepared on Home; no new objects during Playing) |
| AC-5 | PASS | Full EditMode/PlayMode green, `GameSceneDragTests`, levels 1-5 use library TypeNames only |
| AC-6 | PASS | `AStorePatchThatShipsALearnedFootprintTakesItOverWithoutASecondShape`, `AfterAPatchTheCachedEntryIsDroppedAtTheNextWrite`, probe launch 4 |
| AC-7 | PASS | Id-safety tests, plus probe launches 1, 5 and 6 |
| AC-8 | **FAIL** | The component works (sealed-catalog tests, `ProjectScopeShapeCatalogTests` with the library assigned). The shipped project scope binds no catalog, and `GameAttemptLoader` bypasses it. |
| Revision gate (Game-scene learned level plays) | **FAIL / not met** | Blocked on the two owner-approved edits above |

## Implementer round after the independent verification (2026-09-26)

Scope: the verifier's AC-8 FAIL and the borderline test file. Nothing outside the allowed paths was edited, and
nothing was saved, checked in or shelved. `ProjectContext.prefab` and `GameAttemptLoader.cs` were re-read and are
unchanged: the prefab still has no `m_shapeLibrary`, and `GameAttemptLoader.cs:89` still calls the library-only overload.

### AC-8: still BLOCKED, not fixable inside this contract

- Both missing edits are outside the allowed paths. `Resources/Onity/**` is not listed at all.
  `Gameplay/Composition/GameAttemptLoader.cs` belongs to the gameplay session, and this contract's grant covers
  only `ShapeResolver.cs`, `AttemptLevelConverter.cs`, the BoardPresenter source-type lines and `Tests/Gameplay/**`.
- Both in-scope alternatives were rejected:
  - The library-only `TryConvert` overload could reach the project catalog only through static mutable state.
    PROJECT.md Rule 7 forbids that.
  - `ProjectScopeInstaller` could find the library only through a serialized reference (on the prefab), or by
    making the library Addressable or a Resources asset. Those files are in forbidden `Data/**` and
    `AddressableAssetsData/**`.
- Exact owner-approved changes, ready to apply:
  1. `Assets/ColorBlockJam/Resources/Onity/ProjectContext.prefab`, `ProjectScopeInstaller` component (set it
     through Unity APIs, not by hand):
     `m_shapeLibrary: {fileID: 11400000, guid: 15e437ff7d9c59d4daa9be1f9a882770, type: 2}`
     (`Data/Board/BlockShapeLibrary.asset`).
  2. `Assets/ColorBlockJam/Gameplay/Composition/GameAttemptLoader.cs:89`: replace
     `resolver.Resolve<BlockShapeLibrary>()` with `resolver.Resolve<BlockShapeCatalog>()`.
     After change 1, the project container binds the catalog. The catalog overload of
     `AttemptLevelConverter.TryConvert` already exists.
- After those changes, a regression test on the unpatched prefab, `ProjectScopeShapeCatalogTests`, can drop its
  hand-assigned library and assert the shipped prefab binds `BlockShapeCatalog`.

### New demonstration of the Revision gate (everything but the two lines above)

- New PlayMode test `Tests/Gameplay/PlayMode/GameSceneLearnedShapeTests.cs` (+ Unity-generated `.meta`):
  `ALearnedShapeLevelBuildsOnHomeAndIsWonInTheGameScene`.
- It loads the real `Game` scene additively and waits for the interim loader to play level 1. It then pauses and
  goes Home, which tears that attempt down.
- It runs the project-scope step against the scene's own `BlockShapeLibrary`: `BlockShapeCatalogLoader` with a
  memory cache learns one shape (a 4-cell line) and seals the catalog.
- It then takes `GameAttemptLoader.Load`'s Home steps with the catalog overload: `BoardLayout.TryCreate`, the
  catalog `TryConvert`, `EnvironmentView.Build`, `LevelSession.TryPrepare`, `BoardCamera.Frame` and `Play`.
- Assertions:
  - The learned root `IsLearned` with the level's cells, and the library Single keeps its prefab pool.
  - A simulated mouse, through the scene's `GameplayPointerInput`, drags the learned block out through its door.
  - The root returns to its pool. `PreparedPieceCount` and the Onity pool diagnostics `CountAll` sum are unchanged
    from Home, so no pool created an object during Playing.
  - The library block's exit wins the level, and Teardown follows the bounded Finish.
- The scene is unloaded afterwards.
- The first run failed on my own check: a BoardRoot transform count drops when a root returns to its pool, because
  pooled roots are parented outside BoardRoot. I replaced that check with the pool-diagnostics sum.
- This test is the Game-scene proof that the Revision gate asks for. The only unexercised line is the loader's
  overload choice itself.

### Borderline file justified

- `Tests/Gameplay/PlayMode/ProjectScopeShapeCatalogTests.cs` converts every authored level through
  `AttemptLevelConverter.TryConvert` with the sealed project catalog. That makes it a `Tests/Gameplay/**` fixture
  that resolves shapes, inside the grant.
- It cannot move to `Tests/EditMode/BlockShapeCatalog*Tests.cs`: that asmdef lacks `Onity.DI`, and asmdefs are
  outside this contract.
- The new `GameSceneLearnedShapeTests.cs` resolves shapes through the same converter, so the same basis applies.
- Minor note kept as is: the load-time `$"..."` messages match the existing load-time code (`ProjectScopeInstaller`,
  `GameAttemptLoader`). PROJECT.md Rule 8 requires ZString for HUD, popup and editor text, and none of these is on
  the Playing path.

### Verification (live Editor, MCP `run_tests`, instance `ColorBlockJamCase2026@99077e4df1ecfaa4`)

- Compile: `scriptCompilationFailed=False` after both edits.
- Focused PlayMode `GameSceneLearnedShapeTests`: job 16c0ff7aa412447cad66ac90d869b819 failed 0/1 on the
  transform-count check described above. Job 9c90f153227e4d51b9666f48f05b892a passed **1/1**.
- Full EditMode: job 1ada3a023de6408e80e99a7cd40e5a6b, **1024 passed, 0 failed, 1 skipped** (the existing
  `PieceEditorLinkTests` `[Ignore]`).
- Full PlayMode:
  - Job 3e959844f4004935b8e4cf522940a71e: 56/57. The only failure was
    `EnvironmentBarrierViewTests.BarrierFlipsPauseAndStopAllocateNothing`, the known order-dependent PrimeTween
    warm-up failure. Rerun alone (job 90daf7db9a71488f828c656b563a23f5): 6/6.
  - Rerun of the full suite: job 6509b42480ca42099a76d397d1e9622f, **57/57 passed**. It includes
    `GameSceneDragTests`, `LearnedShapeAttemptTests`, `ProjectScopeShapeCatalogTests` and `GameSceneLearnedShapeTests`.
- Console errors after the runs are test-expected only: `Level rejected: ...`, the PieceEditor tests' "Test listener
  failure" and "Could not restore piece JSON" logs. No leftover `PrefabTests_*` folder remains.
- Editor end state: not playing, `LevelEditor.unity` loaded, active and `dirty=False`.

### Verdict after this round

| AC | Result |
|---|---|
| AC-1..AC-7 | PASS (unchanged from the verifier) |
| AC-8 | **BLOCKED**. The catalog and sealing are proven, and the Game-scene play with the catalog is proven by `GameSceneLearnedShapeTests`. The shipped wiring still needs the two owner-approved edits above. |
| Revision gate | **BLOCKED** on the same two edits. Demonstrable today through `GameSceneLearnedShapeTests`. |

## Independent verification 2 (2026-09-26 01:15-01:26, verifier)

Read-only for production code and tests; only this section and `captures/verifier2-preview-learned-zigzag5.png` were
added. Live Editor over MCP for Unity HTTP 127.0.0.1:8082, instance `ColorBlockJamCase2026@99077e4df1ecfaa4`. Start and
end state: not playing, not compiling, `scriptCompilationFailed=False`, `LevelEditor.unity` loaded, active and
`dirty=False`; the Level Editor preview showed `level-01` before and after. Nothing was saved, checked in or shelved.

### Test runs (MCP `run_tests`)

- Full EditMode: job 3780cbc74c2b47a58d66979b5642fb5a, **1024 passed, 0 failed, 1 skipped** (existing
  `PieceEditorLinkTests.ShowLevelPieceWithoutFocusUpdatesTheOpenWindow` `[Ignore]`).
- Focused EditMode with details: job 8f162ff1c4364728b6bb8772732cb827, **59/59**: 19 `BlockShapeCatalogTests`,
  16 `RuntimeShapeCacheTests` (including the three review-round tests), 14 `LevelPreviewTests`, 10
  `AttemptLevelConverterTests`.
- Full PlayMode with details: job aa4f1072813a4a328690fcadf88565bc, **57/57**, including `GameSceneDragTests`,
  `GameSceneLearnedShapeTests.ALearnedShapeLevelBuildsOnHomeAndIsWonInTheGameScene`,
  `LearnedShapeAttemptTests`, `ProjectScopeShapeCatalogTests`, `BoardPresenterLifecycleTests`, `SceneFlowSmokeTests`.
  `EnvironmentBarrierViewTests.BarrierFlipsPauseAndStopAllocateNothing` passed in this run.
- Console after the runs: 0 errors, 0 warnings.

### Live probes

- **AC-1 preview capture** `captures/verifier2-preview-learned-zigzag5.png` (1080 x 1920), through the real
  `LevelPreviewOwner.OnLevelPublished` path from the in-memory draft `verify2-zz5`: purple `ZigZag5`, yellow `ZigZag5`
  rotated and light-green `OtherName` mirrored all come from the learned pool (`learned=True source=-1`, 12 tiles each,
  cells exactly as authored); red `Square` stays library (`source=1`, 9 tiles). A fresh library catalog did not resolve
  `ZigZag5` before learning; after learning one shape (`Custom_00010C3`) both ids resolve to it; `TryLearn` after
  `Seal()` throws. `level-01` was republished afterwards.
- **Shipped wiring** (read through `SerializedObject`, nothing written): `ProjectContext.prefab`'s
  `ProjectScopeInstaller.m_shapeLibrary` is **null**. The same 5 x 5 ZigZag5 level through the library overload that
  `GameAttemptLoader.cs:89` still calls fails with "Block zzA uses unknown shape 'ZigZag5'."; through a sealed catalog
  overload it converts.

### Changed files and scope (Plastic `cm status`, diffs against cs:9)

- This task's pending files are exactly the list in "Independent verification" above plus
  `Tests/Gameplay/PlayMode/GameSceneLearnedShapeTests.cs` (+ `.meta`) and this folder's captures/evidence. All are in
  the allowed paths; the two new Game-scene/project-scope test files are `Tests/Gameplay/**` files that resolve shapes
  through `AttemptLevelConverter.TryConvert`, so they fit the grant.
- `BoardPresenter.cs` against cs:9 differs only in `TryGetPoolNeeds` (learned source check, `shapeCells`).
  `AttemptLevelConverter.cs` also lost the `SolidBlockObject` floor loop since cs:9; that removal belongs to the
  gameplay session (barrier-starts-closed-no-solid-block contract), not this task.
- `Data/Board/BlockShapeLibrary.asset` is checked out and its timestamp moves during test runs, but its content is
  byte-identical to cs:9 (PieceEditor tests re-save it). No leftover `PrefabTests_*` folder remains.
- Untouched: `ProjectContext.prefab`, `GameAttemptLoader.cs`, `Runtime/Levels/**` (its pending changes are the
  level-pipeline session's effect files), `BlockShapeType.cs`, `Editor/BoardKit/**` (board-kit session), `Scenes/**`
  (other sessions), `Packages/**`, `ProjectSettings/**` (other sessions), `ModularBlock.prefab`, asmdefs.
- Grep of the task files: no `System.Linq`, `string.Format`, `async void`, `Update()`; `TypeNameHandling.None` only;
  the only `Instantiate` is the pre-existing environment one in `LevelPreviewHost.cs`.

### Verdict

| AC | Result | Basis |
|---|---|---|
| AC-1 | PASS on its named channels | EditMode catalog/preview tests + live capture above. In the shipped ProjectContext the load-time learning is dormant (AC-8). |
| AC-2 | PASS | `RuntimeShapeCacheTests` with a temp-directory file store (versioned, atomic, restart, corrupt rebuild) |
| AC-3 | PASS | Catalog, cache, converter and preview rejection tests |
| AC-4 | PASS | `LearnedShapeAttemptTests` and `GameSceneLearnedShapeTests` (pools prepared on Home, pool `CountAll` unchanged during Playing) |
| AC-5 | PASS | Full EditMode 1024/0/1 and PlayMode 57/57, `GameSceneDragTests`, authored levels use library TypeNames |
| AC-6 | PASS | `AStorePatchThatShipsALearnedFootprintTakesItOverWithoutASecondShape`, `AfterAPatchTheCachedEntryIsDroppedAtTheNextWrite` |
| AC-7 | PASS | Id-safety tests incl. canonical-code equality with `LevelCustomPiece` |
| AC-8 | **FAIL** | Sealed-catalog behaviour is proven with an assigned library, but the shipped project scope binds no catalog (`m_shapeLibrary` null) and `GameAttemptLoader` uses the library-only overload. |
| Revision gate (Game-scene learned level plays) | **Not met in the shipped app** | Proven only by `GameSceneLearnedShapeTests`, which substitutes the catalog overload. Needs the two owner-approved edits listed above. |
