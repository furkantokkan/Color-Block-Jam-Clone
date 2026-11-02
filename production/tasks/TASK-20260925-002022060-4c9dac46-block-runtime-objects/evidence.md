# Evidence: TASK-20260925-002022060-4c9dac46-block-runtime-objects

Implementation run on 2026-09-25 (Plastic workspace cs:7, Unity 2022.3.62f2), live Editor
over MCP for Unity HTTP (`ColorBlockJamCase2026@99077e4df1ecfaa4`). No checkin or shelve.

## Changed files

| File | Change |
| --- | --- |
| `Assets/ColorBlockJam/Presentation/Board/ModularBlockView.cs` | `Prepare(cellCapacity, pieceCapacity)` warms exactly the given count per kind (no pool for a zero kind); `Prepare(int)` keeps the any-footprint bound for editor previews. New `TrySetCells` (non-throwing capacity rejection), `GetPieceCapacity`, and static `CreatePieceCapacity`, `AddReachableCapacity` (bitmask enumeration of connected sub-footprints, composed with `BlockPieceComposer`, limit 12 cells, warning fallback), `AddBoundCapacity`. |
| `Assets/ColorBlockJam/Presentation/Board/BlockPiece.cs` | `m_authoredVisual`/`AuthoredVisual` removed. Preparation uses the exact reachable capacity (bound only when `cellCapacity` exceeds the footprint). New `Prepare(cellCapacity, pieceCapacity)` for the factory. `SetShape`/`SetCells` validate through the view before changing state; `TryRemoveCell` refuses (and restores the footprint, allocation-free) when a remainder would exceed the prepared pieces. |
| `Assets/ColorBlockJam/Presentation/Board/BlockPieceFactory.cs` | No authored-visual requirement. `Prepare(type, count, cellCapacity)` computes the capacity once per source type; new overload `Prepare(type, count, cellCapacity, pieceCapacity)` for callers that know resolved footprints (merged with the source shape's own reachable pieces). |
| `Assets/ColorBlockJam/Editor/BoardKit/ShapePrefabAuthoring.cs` | Save no longer generates `GeneratedShape`; it removes a legacy owned one (bare child holding only catalog piece instances named `Tile_*`, no added objects/components) and refuses any other `GeneratedShape` before writing. `IsCurrent` is false while the legacy child exists, so `SyncPrefabs` re-saves those prefabs through the existing transaction. |
| `Assets/ColorBlockJam/Editor/BoardKit/BlockPieceEditor.cs` (+ `.meta`, new) | `BlockPieceEditor` custom editor: `HasPreviewGUI`/`OnPreviewGUI`/`RenderStaticPreview` compose the source cells with the original piece prefabs in a `PreviewRenderUtility` scene and destroy everything. `BlockPieceThumbnails` draws that preview as the Project-window grid thumbnail (Unity renders prefab thumbnails only from the prefab's own renderers); owner: the class, stop point: `beforeAssemblyReload`/`quitting`. |
| `Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewHost.cs` | Pool sizes per source type now track roots, cells and per-kind pieces (reachable capacity of every resolved block footprint); rebuilds only when one grows. Look constants untouched. |
| `Assets/ColorBlockJam/Tests/EditMode/ModularBlockViewCapacityTests.cs` (+ `.meta`, new) | Brute-force Rocket reference (BFS over single-cell removals keeping connectivity) for every library shape and every level 1-5 block footprint; exact warm-up per prefab; every remainder reuses the pieces; over-capacity footprint rejected unchanged; bound fallback + warning above 12 cells; factory explicit capacity; zero-allocation Rocket removal. |
| `Assets/ColorBlockJam/Tests/EditMode/BlockPieceEditorPreviewTests.cs` (+ `.meta`, new) | Every library prefab has no `GeneratedShape`, keeps `SourceType`, renders a non-empty preview, and leaves no scene object or piece instance behind. |
| `Assets/ColorBlockJam/Tests/EditMode/ShapePrefabTests.cs` | Authored-visual expectations replaced; `MissingViewRejects...`; legacy `GeneratedShape` removal keeps GUID, source type and custom sibling; custom content inside `GeneratedShape` rejects the save before any write. |
| `Assets/ColorBlockJam/Tests/LevelPreview/LevelPreviewTests.cs` | New `PreviewBlocksHoldOnlyThePiecesTheirFootprintAndRocketRemaindersShow`. |
| `Assets/ColorBlockJam/Prefabs/Board/Shapes/Piece_*.prefab` (12) | Re-saved by `ShapePrefabAuthoring.SaveLibraryPrefabs()` (menu `Color Block Jam/Art/Save Shape Prefabs`). |
| `PROJECT.md` | Board-visuals bullet, Performance Rule 3 and decision row DEC-120. |

Not changed: `BlockPieceComposer.cs` (no helper needed), `ShapeLibraryAuthoring.cs` (public
`Delete(library, index)`, `GetEnumChange` and the transaction constructor untouched),
`BoardPresenter.cs` (outside the allowed paths).

## Prefab migration (AC-2)

Command: `execute_code` -> `ShapePrefabAuthoring.SaveLibraryPrefabs()`; result: all 12 library
shapes `IsCurrent=True`, `GeneratedShape` absent. The library holds 12 records today (including
the user's `NewPieceTest`, ID 30), so 12 prefabs, not 34.

| Prefab | GUID (unchanged) | `m_sourceType` (unchanged) | Lines before -> after |
| --- | --- | --- | --- |
| Piece_000 | d359caa91cf289a46b6e025a7d05638c | 0 | 400 -> 67 |
| Piece_001 | ca820ed424a3d2a4a9f960875dcd742e | 1 | 775 -> 67 |
| Piece_002 | 4c04eaea1293a09438b599876577783b | 2 | 550 -> 67 |
| Piece_004 | 0d2de6c1c04006443b68c132762d1ec4 | 4 | 700 -> 67 |
| Piece_006 | 5606572c392db244bb837c5a14e4d287 | 6 | 700 -> 67 |
| Piece_010 | 2a418a60ba325aa44bb30bc781aa1bdf | 10 | 850 -> 67 |
| Piece_018 | a9571aea741de1944a3c957d2498948c | 18 | 850 -> 67 |
| Piece_022 | 2e416e71808de1749ae44461c0ea311f | 22 | 1000 -> 67 |
| Piece_023 | d2d245e7a6004824abd9b94eee3a6c7b | 23 | 850 -> 67 |
| Piece_027 | dcae445f8bdab7f459949901f2c09d5f | 27 | 1000 -> 67 |
| Piece_029 | bbc08178295ddb14eb24055112180a6e | 29 | 1000 -> 67 |
| Piece_030 | 6f80f372391039c47910781febf0efd5 | 30 | 850 -> 67 |

- Root GameObject fileIDs are preserved (e.g. Piece_000 `5953496491120627585`), so the
  `BlockShapeLibrary.asset` bindings still resolve.
- `BlockShapeLibrary.json` md5 `8723aacf08f81657f452ee2a1abfc343`, `BlockShapeLibrary.asset` md5
  `2000933312c9e15fdc530b27c56ac011` and `BlockShapeType.cs` md5 `ab5bfe60ab6080807e9099f13a3175a2`
  are identical before and after (the save path wrote no JSON/enum change).

## Object counts (AC-3)

LevelEditor preview: a fresh `LevelPreviewHost` per level in an isolated preview scene
(`EditorSceneManager.NewPreviewScene`), counting transforms including inactive ones.

| Level | Host total before -> after | Blocks subtree before -> after | Per block before -> after (pieceViews, shown) |
| --- | --- | --- | --- |
| level-01 | 384 -> 218 | 226 -> 60 | Square 113 -> 30 (41 -> 11 views, 9 shown) x2 |
| level-02 | 364 -> 228 | 178 -> 42 | TripleHorizontal 89 -> 21 (33 -> 8, 8 shown) x2 |
| level-03 | 502 -> 279 | 288 -> 65 | LTypeB1 112 -> 25 (42 -> 10, 10 shown) x2; DoubleHorizontal 64 -> 15 (24 -> 6, 6 shown) |
| level-04 | 504 -> 289 | 290 -> 75 | Square 113 -> 30 x2; DoubleHorizontal 64 -> 15 |
| level-05 | 703 -> 333 | 493 -> 123 | TripleHorizontal 89 -> 21 x3; Square 113 -> 30 x2 |

Game scene, direct Play of `Game.unity` (level 1), first implementation: scene objects 386 -> 220,
`Blocks` subtree 226 -> 60, each Square 113 -> 30 objects. **Superseded by the review fix below**:
that exact sizing came from the library shape, not the level footprint, and could throw in
`BoardPresenter.Place`. The Game path now keeps the any-footprint bound and loses only the hidden
copy (see "Review fixes").

In the LevelEditor preview the Square keeps two extra pieces because its L-shaped Rocket remainder
shows five outer corners and one inner corner. The other preview blocks hold only the pieces they show.

Warmed pieces per kind (Center/Edge/OuterCorner/InnerCorner), exact vs the previous bound:

| Shape | Exact | Previous bound |
| --- | --- | --- |
| Single | 0/0/4/0 | 1/4/4/2 |
| Square | 1/4/5/1 | 4/10/10/8 |
| DoubleHorizontal | 0/2/4/0 | 2/6/6/4 |
| TripleHorizontal | 0/4/4/0 | 3/8/8/6 |
| CornerLB | 0/2/5/1 | 3/8/8/6 |
| LTypeB1 | 0/4/5/1 | 4/10/10/8 |
| TTypeB | 0/4/6/2 | 4/10/10/8 |
| Cross | 0/4/8/4 | 5/12/12/10 |
| ZTypeHorizL | 0/2/6/2 | 4/10/10/8 |
| DoubleTripleHorizontal | 2/6/6/2 | 6/14/14/12 |
| CTypeB | 0/4/6/2 | 5/12/12/10 |
| NewPieceTest | 0/6/4/0 | 4/10/10/8 |

Visual parity (captures in `captures/`, 540 x 960):

- `preview-level-0N-before.png` vs `-after.png`, N = 1..5: 0 differing pixels each.
- `game-level-01-before.png` vs `-after.png`: 4430 pixels differ by at most 1/255 (render
  noise), no visible change.

## Editor preview (AC-2)

- `captures/inspector-preview-Piece_010.png`: `BlockPieceEditor.RenderStaticPreview` output
  (the texture the Inspector preview draws).
- `captures/project-window-shape-thumbnails-after.png`: a temporary Project browser window
  (closed afterwards) in grid view on `Prefabs/Board/Shapes`, showing all 12 thumbnails.
- Probe before adding `BlockPieceThumbnails`: `GameObjectInspector.RenderStaticPreview` returned
  null and `AssetPreview.GetMiniThumbnail` was the generic `d_Prefab Icon`, which is why the
  Project thumbnail needs the drawer.

## Tests (AC-1, AC-2, AC-4)

All runs through MCP `run_tests` in the live Editor.

| Job | Scope | Result |
| --- | --- | --- |
| `57bbb85fae1d4ba6b5343a196239018b` | EditMode focused, final code: `ShapePrefabTests`, `ModularBlockViewCapacityTests`, `BlockPieceEditorPreviewTests`, `LevelPreviewTests`, `BoardKitTests.DeletingAPieceSavedBeforeTheEnumCompiledNeedsNoEnumChange` | 36/36 passed |
| `3410374aee204054a634151413646924` | same focused set before the thumbnail drawer and allocation test | 35/35 passed |
| `0c52e31120ff497a8ff73cc87b3edf27` | EditMode assemblies Editor, LevelPreview, Environment, Gameplay | 478/481 passed |
| `dcc20ee59d3a4601a93ae10ddd68374c` | same assemblies, earlier | 473/480 passed |
| `e6f2951337844e7d82b08fb6c459ba67` | PlayMode assemblies Presentation, Gameplay, Environment (incl. `BoardKitLifecycleTests`, `BoardPresenterLifecycleTests`, `SceneFlowSmokeTests`) | 38/38 passed |

Failures outside this task:

- `BoardKitTests.ActivePieceDatabaseHasOneRecordPerRotationAndMirrorFamily`,
  `CreatingPieceDatabaseActivatesCloneAndRejectsStaleDisk`,
  `EveryLibraryShapeInEveryRotationHasExactOccupancyWithoutOverlappingTiles`: `Expected: 11
  But was: 12`, from the library count assertion (`BoardKitTests.cs:39`, `:449`) and the user's
  12th test piece `NewPieceTest`. Data, not this change.
- Job `dcc20...` also had 4 `LevelCanvasBlockToolsTests` failures from the Level Editor
  session's in-progress work (files edited 12:12-12:30); they pass in job `0c52e...`.

## Console

No compiler errors or warnings from the changed files. Remaining entries come from other
sources: MCP WebSocket reconnects, Unity Search `ObjectDisposedException`
(`PropertyDatabaseFileStoreView.Dispose`), expected `LogAssert` errors of
`ShapePrefabTests.UnrelatedUndoAfterCacheReloadCannotRestoreAnOldLibrarySnapshot`, the expected
13-cell warning of `ModularBlockViewCapacityTests`, and one
`Could not restore piece JSON: Unable to remove the file to be replaced` at 12:31, during
another session's test run (Windows `File.Replace` lock); every run in this task passed that test.

## Editor state

Captures used `NewPreviewScene`, a temporary Project browser window (closed), Play Mode of
`Game.unity` (opened single-mode because LevelEditor was clean, then LevelEditor reopened),
and an empty unsaved scene for the PlayMode run. No scene was saved.

Process deviation (review finding): opening `Game.unity` single-mode broke the harness rule to
open other scenes additively. It unloaded the LevelEditor scene, and with it
`LevelPreviewOwner`'s preview, while the Level Editor session was editing
(`LevelDatabaseWindow.cs`/`LevelCanvasElement.cs` at 12:38) and another session ran tests at 12:31.
The Editor state was restored and nothing was saved. Later live checks use `NewPreviewScene` or an
additive open only.

## Review fixes (2026-09-25, second pass)

Verdicts on the reviewer findings:

| Finding | Verdict | Fix |
| --- | --- | --- |
| Game path sized from the library shape; mismatched level footprint throws in `Place` (3 reports) | Confirmed | `BlockPieceFactory.Prepare(type, count, cellCapacity)` keeps the any-footprint bound again (`AddBoundCapacity`). `BoardPresenter.cs` is outside the allowed paths, so the Game scene loses only the hidden copy. The exact Game-scene reduction needs `TryGetPoolSizes` to accumulate `ModularBlockView.AddReachableCapacity(visual.Cells, ...)` and call the 4-argument `Prepare`, as `LevelPreviewHost` does. That is a follow-up requiring a scope widening. |
| Vacuous zero-allocation test; no refusal-path test | Confirmed | The allocation test uses Unity's `Is.Not.AllocatingGCMemory()` (Recorder-based). New `RocketRemainderNeedingMorePiecesThanPreparedIsRefusedWithoutChangingTheBlockOrAllocating` covers the `TryRemoveCell` refusal/restore branch: a Square footprint on a root prepared with exactly the Square's own pieces. |
| Fallback above 12 cells logs on every call, with interpolation (3 reports) | Confirmed | `AddReachableCapacity` returns `true` when it used the bound and no longer logs. `LevelPreviewHost` warns once per source type for its lifetime (`m_reportedBoundFallbacks`), using plain concatenation. The factory and `BlockPiece` stay silent. The Game path no longer enumerates. New test `LevelPreviewTests.FootprintAboveTheEnumerationLimitIsReportedOncePerHost`. |
| PROJECT.md line endings flipped (75 unchanged lines LF -> CRLF) | Confirmed | Restored the `cs:7` line endings on every line whose text equals `cs:7`, with a scratchpad script. Afterwards there were 0 line-ending-only differences against `cs:7`. The implementer report's claim ("converted back to LF to match cs:5") was wrong: the file was all CRLF. |
| `docs/art/board-kit/README.md:31` still describes `GeneratedShape` | Confirmed, not fixed | `docs/**` is outside the allowed paths. Follow-up: shape prefabs have no authored child, and `BlockPieceEditor` draws the preview. |
| DEC-120 row and Rule 3 overstate the result | Confirmed | Rule 3 now separates the 4-argument exact path from the 3-argument bound path. DEC-120 says the previews are pixel-identical, the Game capture differs by at most 1/255 (render noise), and the Game scene loses only the hidden copy. |
| `Game.unity` opened single-mode | Confirmed (process) | Recorded above. |

Game path after the fix: a factory in a `NewPreviewScene`, prepared as `BoardPresenter` does
(`Prepare(type, 1, shape size)`), measured per root:

| Shape | Objects (was 113/89/112/64 with the hidden copy) | Piece views | Shown |
| --- | --- | --- | --- |
| Square | 87 | 32 | 9 |
| TripleHorizontal | 68 | 25 | 8 |
| LTypeB1 | 87 | 32 | 10 |
| DoubleHorizontal | 49 | 18 | 6 |

Level 1 in the Game scene is therefore about 2 x (113 - 87) = 52 objects lighter (Blocks subtree
226 -> 174). The Game visuals are unchanged: the bound is what the pre-task code warmed.

Tests after the fix (live Editor, MCP `run_tests`):

| Job | Scope | Result |
| --- | --- | --- |
| `47aeab31bc934cf7847bc47df6fc0a8c` | EditMode focused: `ShapePrefabTests`, `ModularBlockViewCapacityTests`, `BlockPieceEditorPreviewTests`, `LevelPreviewTests`, `BoardKitTests.DeletingAPieceSavedBeforeTheEnumCompiledNeedsNoEnumChange` | 39/39 passed |
| `f20db8c5c3c04d0f89992e3035d71732` | EditMode assemblies Editor, LevelPreview, Environment, Gameplay | 485/488. The 3 failures are the known `Expected: 11 But was: 12` library-count tests (user's 12th piece). |
| `73b10c714ca1480fa010a696570cbbd6` | PlayMode assemblies Presentation, Gameplay, Environment | 38/38 passed. The first attempt, `0619086b...`, timed out during initialization and ran no tests. |

New focused tests:
- `FactoryWithoutNamedFootprintsPlacesAnyResolvedFootprintOfTheShapeSize`: a Square pool places a four-cell line and a T4.
- The refusal-path test.
- The host warn-once test.

Console after the compile: no compiler errors. The only new entries are the expected single
"Level preview: a 13-cell Square block exceeds ..." warning per host test run.

## Independent verification (2026-09-25, ~13:10-13:30, workspace cs:7)

Verifier did not edit production code or tests. Live Editor over MCP HTTP
(`ColorBlockJamCase2026@99077e4df1ecfaa4`). Start state: one clean untitled scene
(Main Camera, Directional Light), not playing, `scriptCompilationFailed` false. End state: the same.

### Verdict per acceptance scenario

| AC | Result | Evidence |
| --- | --- | --- |
| AC-1 Exact reachable warm-up | PASS (tested paths) | Focused EditMode job `90e836232636400d9de1e5586a8e8e33`: 39/39. `ModularBlockViewCapacityTests` checks `AddReachableCapacity` against an independent BFS over Rocket removals, for all 12 library shapes and every level 1-5 block footprint, and checks that no Rocket remainder grows a pool (`childCount` stays constant). Caveat: the Game-scene path (`BoardPresenter` -> 3-argument `BlockPieceFactory.Prepare`) still warms the any-footprint bound, not the reachable count. |
| AC-2 No `GeneratedShape`; preview kept | PASS | See "Prefabs" and "Preview" below. The library holds 12 records, not the 34 the contract assumed. |
| AC-3 Visual parity and fewer objects | FAIL (Game scene part) | Level Editor preview: counts drop and the images match (below). Game scene level 1 in Play Mode: the images match, but the counts only drop by the hidden copy: Square 87 objects, 32 pooled piece views for 9 shown (11 reachable). The contract objective ("Game scene ... only the piece objects it can ever display") and expected result 3 ("to its reachable pieces only") are not met there. The fix needs `Gameplay/Board/BoardPresenter.cs`, which is outside the allowed paths. |
| AC-4 Existing tests green | PASS (with data caveat) | EditMode assemblies Editor, LevelPreview, Environment and Gameplay, job `c0c714d437144348b40b6ff5cb6ebef0`: 485/488. PlayMode assemblies Presentation, Gameplay and Environment, job `22d6d96e958944998fea3eddd5eb277a`: 38/38. Earlier attempts `cb47a13d...` and `5e4361b0...` failed to initialize (a domain reload) and ran no tests. The 3 failures are `BoardKitTests` asserting `library.Shapes.Count == 11` (lines 39, 57, 449) against the user's 12-record library (`NewPieceTest`, ID 30). The code does not affect that count. |

### Prefabs (AC-2), compared with `cm cat <file>#cs:7`

- None of the 12 `Prefabs/Board/Shapes/Piece_*.prefab` files contains `GeneratedShape`. Each holds one
  GameObject (root) with `Transform`, `ModularBlockView` and `BlockPiece`, and has no children.
- The `.meta` GUIDs of the 11 prefabs in cs:7 are unchanged. Piece_030 is new since cs:7 (the user's
  piece), and its GUID/root fileID match the `BlockShapeLibrary.asset` binding.
- Root GameObject fileIDs, all component documents except the removed child link, `m_sourceType` and
  `m_generatedSignature` are identical to cs:7. The only change on `BlockPiece` is the removed
  `m_authoredVisual` field. All 12 library bindings resolve to the root fileID.
- `BlockShapeLibrary.json` md5 `8723aacf...`, `BlockShapeLibrary.asset` md5 `20009333...` and
  `BlockShapeType.cs` md5 `ab5bfe60...` match the implementer's values. Their diffs from cs:7 are
  only the user's `NewPieceTest` record.
- No `PrefabTests_*` leftovers on disk after the test runs.

### Preview (AC-2)

- `captures/verify-static-previews.png`: `BlockPieceEditor.Render` (the Inspector and static-preview
  source) for all 12 shapes. Each image has 2799-6499 non-background pixels. Loaded GameObjects stayed
  103 before and after, `Texture2D` stayed 1234, and scene objects stayed 2, so nothing leaked.
- `BlockPieceEditorPreviewTests` passed in the focused job.
- The Project-window thumbnail capture (`project-window-shape-thumbnails-after.png`) is the
  implementer's. It was inspected, not re-taken: it shows all 12 shapes.

### Level Editor preview (AC-3), fresh `LevelPreviewHost` per level in `NewPreviewScene`

Counts equal the implementer's table: totals 218/228/279/289/333, block subtrees 60/42/65/75/123,
Square 30 objects (11 views, 9 shown), TripleHorizontal 21, LTypeB1 25, DoubleHorizontal 15.

Captures `captures/verify-le-preview-level-0N.png` were rendered with `LevelEditor.unity` opened
additively and active. That sets the ambient: flat (0.63, 0.64, 0.67) versus the untitled scene's
skybox ambient. A first attempt with the untitled scene active differed by up to 40/255 from both
before and after, for that reason alone, so it was discarded. Compared with `preview-level-0N-before.png`:
levels 1, 3, 4 and 5 are 0 pixels different, and level 2 differs by at most 1/255 (the same against
`-after`). LevelEditor was closed without saving (it was not dirty).

### Game scene level 1 (AC-3), after the review fix

`Game.unity` was opened additively and set active, then Play Mode was entered. The untitled scene's
default camera and light were deactivated in Play Mode only (reverted on exit).

- Game scene objects 334 (before 386), `Blocks` subtree 174 (before 226).
- 2 Square roots: 87 objects, 32 piece views, 9 shown, no `GeneratedShape`.
- `captures/verify-game-level-01.png` versus `game-level-01-before.png`: at most 1/255 (4430 px of
  render noise). Versus `game-level-01-after.png`: identical.
- After exiting Play Mode, Game was closed without saving (not dirty), and the untitled scene was
  active again.

### Files changed by this task, checked against the allowed paths

All are inside the allowed paths:
- `Presentation/Board/ModularBlockView.cs`, `BlockPiece.cs` and `BlockPieceFactory.cs`.
- `Editor/BoardKit/ShapePrefabAuthoring.cs`, and `BlockPieceEditor.cs` (+ `.meta`).
- `Editor/LevelPreview/LevelPreviewHost.cs`.
- `Tests/EditMode/ModularBlockViewCapacityTests.cs` (+ `.meta`), `BlockPieceEditorPreviewTests.cs`
  (+ `.meta`) and `ShapePrefabTests.cs`, and `Tests/LevelPreview/LevelPreviewTests.cs`.
- The 12 `Prefabs/Board/Shapes/Piece_*.prefab` files, `PROJECT.md`, and this task folder.

The pending diffs from cs:7 in `ShapeLibraryAuthoring.cs` (`GetEnumChange`), `ShapeLibraryTransaction.cs`,
`BoardKitTests.cs`, `BlockShapeType.cs`, `BlockShapeLibrary.asset`/`.json` and `README.md` are other
sessions' work. None refers to the authored visual or to piece capacity. `BlockPieceComposer.cs` equals cs:7.

Notes for the owner:
- Plastic reports `Tests/EditMode/BlockPieceEditorPreviewTests.cs` as private (not added) and pairs its
  `.meta` as a local move of the deleted `Editor/Controls/DatabaseActionsRow.cs.meta`. That pairing is
  similarity detection only: the GUID `2b2f5c1d...` is unique. At checkin, add it as a new file, not as a move.
- Follow-ups outside this contract:
  - `Gameplay/Board/BoardPresenter.cs` should pass resolved footprints to the 4-argument `Prepare`
    (AC-3, Game scene).
  - `docs/art/board-kit/README.md:31` still describes `GeneratedShape`.
  - `BoardKitTests` has library-count assertions of 11.

Console: no compiler errors. The entries are expected test logs ("Could not restore piece JSON",
the 13-cell preview warning, "LevelEditor is an edit-only scene"), MCP WebSocket reconnects and the two
test-job initialization timeouts.

Verifier scripts are in the session scratchpad `iv/`.

## Implementer response to the verification (2026-09-25, ~13:25)

No production code, test, prefab or PROJECT.md change in this pass.

- Freshness: every file this task owns was last written at or before 13:02, before the verifier's
  runs at ~13:10-13:30. The verifier's jobs `90e83623...` (focused, 39/39), `c0c714d4...` (EditMode,
  485/488) and `22d6d96e...` (PlayMode, 38/38) therefore cover the current code, so there was no rerun.
- AC-1, AC-2 and AC-4 stay PASS. AC-4's 3 failures are the `BoardKitTests` assertions that the
  library holds 11 shapes (lines 39, 57 and 449). The user's 12th piece breaks them. `BoardKitTests.cs`
  is outside the allowed paths, and those failures do not depend on this task's code.
- AC-3 (Game scene part) is **blocked on scope**, not on code. The Level Editor preview part passes.
  The only caller that sizes Game-scene pools is `Gameplay/Board/BoardPresenter.cs`
  (`TryBuild` -> `TryGetPoolSizes` -> 3-argument `BlockPieceFactory.Prepare`), and that file is not
  in the contract's allowed paths.
  - An in-scope alternative was considered and rejected. The factory could defer each root's warm-up
    to its first `Create`, which receives the resolved cells. But roots of one source type are reused
    across blocks and Restarts with other footprints. It would also break the "Prepare before Playing,
    exhaustion rejects, no growth" contract of PROJECT.md Performance Rule 3.
  - Proposed fix after the user widens the contract to that one file (about 10 lines, the same
    pattern as `LevelPreviewHost`):
    - `TryGetPoolSizes` also accumulates one `int[ModularBlockView.KindCount]` per source type through
      `ModularBlockView.AddReachableCapacity(visual.Cells, capacity)`.
    - `TryBuild` calls `factory.Prepare(type, count, cellCapacity, capacity)`.
    - The 4-argument overload already merges in the source shape's own reachable pieces.
    - Verify with `BoardPresenterLifecycleTests` (PlayMode) and a Game level 1 recount: expect each
      Square at about 30 objects, like the Level Editor preview.
  - The other resolution is that the user accepts the Game-scene gap for this task: the hidden copy is
    removed there (Square 113 -> 87 objects), and the exact warm-up follows as a separate task.

## Second independent verification (2026-09-25, ~13:24-14:05, workspace cs:7)

This verifier edited no production code, tests, prefabs or PROJECT.md. It used the live Editor over MCP
HTTP (`ColorBlockJamCase2026@99077e4df1ecfaa4`). Start state: one clean untitled scene, not playing, and a
clean compile. End state: the same. No scene was saved. Scripts are in the session scratchpad `iv2/`.

Freshness: no task-owned file changed after 13:02 (`ModularBlockView.cs` 13:02:15, `PROJECT.md` 13:02:24).
`BoardPresenter.cs` last changed at 05:38.

Concurrent activity during this run: the Level Editor session edited `Editor/Levels/**`,
`LevelDatabaseWindow.cs` and Level Editor tests from 13:37 to 13:49. Another session edited
`Domain/Exit/ExitRules.cs` and `ZeroAllocationTests.cs` at 13:37. Between about 13:37 and 13:47, the project
had compile errors in `LevelCanvasElement.cs`, which belongs to the Level Editor session. At least one
other test run happened at 13:52-13:53.

### Verdict per acceptance scenario

| AC | Result | Evidence |
| --- | --- | --- |
| AC-1 Exact reachable warm-up | PASS | Focused EditMode jobs `a28f4efa59864b40a3bfd5c5fc1b65c0` and `c82d01ec962d452a8291596f2e0bbbb9` (after the compile recovered): 39/39 each. `ModularBlockViewCapacityTests` compares `AddReachableCapacity` with an independent BFS over Rocket removals. It covers every library shape and every level 1-5 footprint, checks that remainders never grow a pool, covers the refusal branch, and includes two `Is.Not.AllocatingGCMemory` checks. In the live Level Editor preview, a Square holds 11 piece views (Center/Edge/OuterCorner/InnerCorner 1/4/5/1, the reachable maximum), and 9 are shown. |
| AC-2 No `GeneratedShape`; preview kept | PASS | See "Prefabs" and "Preview" below. The library holds 12 records, not the 34 in the contract. |
| AC-3 Visual parity and fewer objects | FAIL (Game scene part; blocked on scope) | The Level Editor preview part passes (below). The Game-scene path still warms the any-footprint bound. An Edit-mode probe of `BlockPieceFactory` prepared as `BoardPresenter.TryGetPoolSizes` does (3-argument `Prepare(type, 1, shape size)`) measured Square at 87 objects, 32 piece views and 9 shown. TripleHorizontal measured 68/25/8, LTypeB1 87/32/10 and DoubleHorizontal 49/18/6, with no `GeneratedShape`. The contract's objective and expected result 3 ("to its reachable pieces only") therefore do not hold in the Game scene. The fix needs `Gameplay/Board/BoardPresenter.cs`, which is outside the allowed paths. A fresh Play Mode recount was attempted twice but could not complete: with `Game.unity` opened additively, Play Mode started, but `Time.frameCount` stayed at 1-2 for more than 50 s, so the board never built. The one capture from that attempt showed no board and was deleted. This result relies on the probe plus the first verifier's Play Mode capture and count at 13:19, taken with identical task code. |
| AC-4 Existing tests green | PASS (with the known data caveat) | PlayMode assemblies Presentation, Gameplay and Environment, job `597997e0d36e49a6be22143a3b1114d6`: 38/38. EditMode assemblies Editor, LevelPreview, Environment and Gameplay were run 3 times; the only failures every run shares are the 3 `BoardKitTests` `Expected: 11 But was: 12` assertions (the user's 12th piece; `BoardKitTests.cs` is outside the allowed paths). The other failures are covered in the next list. |

Other EditMode failures, none caused by this task:
- `ZeroAllocationTests.ASessionTickDuringAHeldDragAllocatesNothing` failed in the full runs
  `e0127f12...` and `522be50b...`. It passed alone (job `e83742e7...`, 6/6) and passed in the third full run.
  The test exercises the Domain/Gameplay session, and the session editing `ExitRules.cs` and this test
  was active at the time. It does not reference `Presentation/Board`.
- `ShapePrefabTests` threw `IOException: Unable to remove the file to be replaced` once in job
  `463bbc09...` (`JsonImport...`) and once in job `d92f62a1...` (`SavePreserves...`). The exception comes
  from `File.Replace` in `ShapeLibraryFile.cs`, which this task did not change, and looks like a Windows file
  lock. The same tests passed in isolation (`1deed1c5...`) and in both focused reruns.
- `LevelCanvasBlockToolsTests.PlacingAShapeAddsOneBlockAndPublishesItOnce` and
  `LevelEditorShellTests.UndoAndRedoOfABoardEditUpdateFieldsDirtyMarkerAndPreview` failed in the third full
  run (`d92f62a1...`, 551 tests) and alone (`job_le`). Both assert `LevelDatabaseWindow.DraftLevelPublished`,
  which the Level Editor session edited from 13:37 to 13:49. They passed in the earlier full runs. No
  `Editor/LevelPreview/**` file changed after 13:00.

### Prefabs (AC-2), compared with `cm cat <file>#cs:7`

- `grep` finds no `GeneratedShape` or `m_authoredVisual` in the 12 `Piece_*.prefab` files. Each file has one
  GameObject, and `m_Children` is empty.
- For the 11 prefabs in cs:7: the `.meta` GUID, `m_sourceType` and `m_generatedSignature` are unchanged. Every
  YAML document fileID in the current file already existed in cs:7, and the root GameObject fileID is the
  same as in cs:7.
- Piece_030 is new since cs:7 (the user's piece). All 12 `BlockShapeLibrary.asset` bindings resolve to
  `{root fileID, prefab GUID}`, and the asset differs from cs:7 only by the added type-30 entry.
- `BlockShapeLibrary.asset` md5 `2000933312c9e15fdc530b27c56ac011`, `BlockShapeLibrary.json` md5
  `8723aacf08f81657f452ee2a1abfc343` and `BlockShapeType.cs` md5 `ab5bfe60ab6080807e9099f13a3175a2` match the
  implementer's values. Test runs re-save the asset with identical content.
- No `PrefabTests_*` folder is left on disk.

### Preview (AC-2)

- `captures/verify2-static-previews.png` shows `BlockPieceEditor.Render` output for all 12 shapes, with 2799-6499
  non-background pixels each. All shapes are recognisable. GameObjects stayed at 101 before and after,
  `Texture2D` at 1251, and scene objects at 2, so nothing leaked.
- `BlockPieceEditorPreviewTests` passed in both focused jobs.
- `captures/project-window-shape-thumbnails-after.png` (the implementer's capture) was inspected: it shows all
  12 shape thumbnails.

### Level Editor preview (AC-3), fresh `LevelPreviewHost` per level in `NewPreviewScene`, `LevelEditor.unity` additive and active

- Totals were 218/228/279/289/333 and block subtrees 60/42/65/75/123, the same as the implementer and the
  first verifier.
- Per block (objects / piece views / shown):

  | Block | Objects | Piece views | Shown |
  | --- | --- | --- | --- |
  | Square | 30 | 11 | 9 |
  | TripleHorizontal | 21 | 8 | 8 |
  | LTypeB1 | 25 | 10 | 10 |
  | DoubleHorizontal | 15 | 6 | 6 |

- Comparing `captures/verify2-le-preview-level-0N.png` with `preview-level-0N-before.png`: levels 1, 3, 4 and 5
  have 0 differing pixels. Level 2 has 4091 pixels that differ by at most 1/255, as the first verifier also saw.
- LevelEditor was closed without saving (it was not dirty).

### Files changed by this task (checked against `cm status` and the allowed paths)

All of these are inside the allowed paths:
- `Presentation/Board/ModularBlockView.cs`, `BlockPiece.cs` and `BlockPieceFactory.cs`.
- `Editor/BoardKit/ShapePrefabAuthoring.cs`, and `BlockPieceEditor.cs` (+ `.meta`).
- `Editor/LevelPreview/LevelPreviewHost.cs`.
- `Tests/EditMode/ModularBlockViewCapacityTests.cs` (+ `.meta`), `BlockPieceEditorPreviewTests.cs` (+ `.meta`) and
  `ShapePrefabTests.cs`, and `Tests/LevelPreview/LevelPreviewTests.cs`.
- The 12 `Prefabs/Board/Shapes/Piece_*.prefab` files, `PROJECT.md` and this task folder.

No task change falls outside the allowed paths. `BlockPieceComposer.cs` and `BoardPresenter.cs` were not
touched.

`PROJECT.md` against cs:7:
- Line endings: 0 differences where only the ending changed.
- This task's text: the derived-prefab sentences in the board-visuals bullet, the Performance Rule 3 paragraph
  about the 3- and 4-argument `Prepare`, and the DEC-120 row. DEC-120 honestly states the Game-scene gap.
- The other diffs belong to other sessions.

Plastic still pairs `Tests/EditMode/BlockPieceEditorPreviewTests.cs.meta` as a "moved locally" from
`Editor/Controls/DatabaseActionsRow.cs.meta`, and the `.cs` file is still private. At checkin, add both as new files.

### Open items for the owner

- AC-3 (Game scene) needs a user decision:
  - Either widen the contract to `Gameplay/Board/BoardPresenter.cs` (pass resolved footprints to the 4-argument
    `Prepare`),
  - or accept the gap and track it as a follow-up task.
- A Play Mode recount of Game level 1 should be repeated once the Editor's player loop advances again.
- Follow-ups outside this contract:
  - `docs/art/board-kit/README.md:31` still describes `GeneratedShape`.
  - `BoardKitTests` asserts a library count of 11.
- User acceptance of the hierarchy is still pending.

## Game-scene recount after BoardPresenter's 4-argument Prepare (2026-09-25, main session)

The gameplay session switched `BoardPresenter` to `factory.Prepare(type, count, cellCapacity, pieces)`
(`Gameplay/Board/BoardPresenter.cs:139`), verified there with Gameplay PlayMode 38/38 and EditMode 215.
Live Editor over MCP HTTP (instance ColorBlockJamCase2026@99077e4df1ecfaa4), untitled clean scene
active, nothing playing and no test job running at the start.

- `Game.unity` opened additively and set active, Play Mode entered, level 1 built (frame 785).
- Game scene objects: **220** (386 before this task, 334 after the first round).
- 2 Square roots (`Piece_001(Clone)` under `Blocks`): **30 objects each**, 11 piece views, 9 shown,
  no `GeneratedShape` (113 before, 87 after the first round).
- `Blocks` subtree: **61** (226 before, 174 after the first round).
- Capture `captures/recount-game-level-01-540.png` (540 x 960, the earlier captures' size, untitled
  scene camera and light disabled in Play Mode only): 0 pixels differ from `game-level-01-after.png`
  and `verify-game-level-01.png`, and at most 1/255 from `game-level-01-before.png` (the known 4430 px of render noise).
- `captures/recount-game-level-01.png` (1080 x 1920, no MSAA on the capture target) shows the same
  blocks. It also shows hairline seams at two wall/corner joins and under the top gate that the
  540 x 960 captures cannot resolve. Those are environment pieces, not block pieces, and are recorded
  as an environment follow-up.
- Play Mode exited, `Game.unity` closed without saving (not dirty); the untitled scene is active and clean.

Fresh automated evidence at the current files: the barrier-view re-verification ran the full EditMode
suite (800/801; the one failure, `ShapePrefabTests.UnrelatedUndoAfterCacheReloadCannotRestoreAnOldLibrarySnapshot`,
was a file-lock IOException that passed 1/1 on rerun) and PlayMode 44/44. These include
`ModularBlockViewCapacityTests`, `BlockPieceEditorPreviewTests`, `ShapePrefabTests`, the LevelPreview
tests and `BoardKitTests` (the earlier 11-vs-12 failures are gone). No file owned by this task changed
after that run except PROJECT.md (DEC-120 row and Performance Rule 3 updated for the Game scene).

AC-3 now passes in both the LevelEditor preview and the Game scene. The remaining required item is the
user's acceptance of the hierarchy (contract: manual evidence).
