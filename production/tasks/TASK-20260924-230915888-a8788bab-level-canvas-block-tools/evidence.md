# Evidence: TASK-20260924-230915888-a8788bab-level-canvas-block-tools

Implementer run, 2026-09-25 10:30-11:15 local. Plastic workspace, no checkin, no shelve.

## Preconditions

- Contract read in full. Design: `docs/Level-Editor-Design.md` sections 2, 4 and 5.
- Other writer check: `LevelDatabaseWindow.cs` was last written at 09:33 (task 2), about an hour before this run. The `Canvas/*` files were last written at 10:07 by task 2. The task 2 verifier finished at 10:29 with PASS (`production/tasks/TASK-20260924-230915887-81e9732c-level-canvas-view/evidence.md`). Its lifecycle block still said `ready`, but the orchestrator dispatched this task after it.
- Pre-edit snapshots (scratchpad): `LevelDatabaseWindow.blocktools-before.cs`, `LevelDraft.blocktools-before.cs`, `LevelCanvasElement.blocktools-before.cs`, `LevelCanvasGeometry.cs`, `LevelCanvasModel.cs`, `LevelEditorShellTests.cs`, `LevelCanvasElementTests.cs`. Right before the first edit, `cmp` showed that `LevelDatabaseWindow.cs` still matched the snapshot I had read.
- `LevelDatabase.json` sha256 `02c013a54e180f9570daf142505e018e28564ca4f7ab91bad847b35d8f3ecd6e` before and after, in the repository and in the copy.

## Transport

- MCP HTTP 127.0.0.1:8082 listed instance `ColorBlockJamCase2026@99077e4df1ecfaa4`. Every Editor call (`mcpforunity://editor/state`, `execute_code`) failed with "Unity session not ready ... (ping not answered)". I retried with fresh sessions at 10:31, 10:44, 11:00 and 11:12. `Editor.log` shows the MCP WebSocket keep-alive closing. The bridge was down for more than 5 minutes, so I used the isolated-copy fallback.
- The live Editor itself was alive and auto-refreshing. It imported the new files with the `.meta` GUIDs written in this task. It compiled `ColorBlockJam.Editor.dll` (log line 69221, after the window edits) and `ColorBlockJam.Editor.EditModeTests.dll` (log lines 67323 and 70687) with **0 `error CS`**. No exception from `LevelDatabaseWindow` or `LevelCanvas*` appeared after the domain reloads. After the last canvas change (the ghost border), the live Editor imported `LevelCanvasElement.cs` again at about 11:13. It recompiled `ColorBlockJam.Editor`, `Editor.LevelPreview`, both test assemblies and `Assembly-CSharp(-Editor)` with 0 `error CS` and reloaded the domain.
- Isolated copy: `%TEMP%\CBJblocktools-a8788b`, made with `robocopy /E /R:0 /W:0 /MT:16` of the repository, Library included. Temp, Logs, obj, .plastic, .vs, Bee and `EditorInstance.json` were excluded. Each run used Unity 2022.3.62f2 with `-batchmode -runTests -testPlatform EditMode -assemblyNames "ColorBlockJam.Editor.EditModeTests;ColorBlockJam.Editor.LevelPreview.Tests" -logFile <copy>\bt-runN.log`, without `-nographics`. Before each run, every changed file was checked with `cmp` against the repository.

## Tests (isolated copy)

| Run | Result | Notes |
|---|---|---|
| bt-run1 | 177/180 | 3 new window tests failed: (a) batch mode leaves a Unity-built window's layout NaN until `ValidateLayout`; (b) a key press after a swatch click reached no element (see fix 1). |
| bt-run2 | 179/180 | The Eraser button sat below the scrolled tools view; the test now presses it with `NavigationSubmitEvent`. |
| bt-run3 | **180/180** | 0 `error CS`. |
| bt-run4 (final, after the ghost border) | **181/181** | 180 repository tests plus the copy-only capture probe. 0 failed, 0 skipped, 0 inconclusive, 48.5 s. The only compiler warning is the existing `VacuumMotionAssets.cs(54,13) CS0618`. Results are in `blocktools-editmode-results.xml`. |

Per class in the final run:

| Class | Passed |
|---|---|
| LevelEditCommandTests (new, focused) | 13 |
| LevelRecordCopyTests (new, focused) | 6 |
| LevelEditorToolStateTests (new) | 4 |
| LevelCanvasBlockToolsTests (new, window + events) | 6 |
| DatabaseControlsTests | 9 |
| LevelDatabaseAddressablesTests | 6 |
| LevelDatabaseInspectorTests | 4 |
| LevelPreviewTests | 9 |
| LevelEditorShellTests | 16 |
| LevelCanvasElementTests / HitTest / Hover / Model | 7 / 7 / 2 / 7 |
| LevelDatabaseTests | 7 |

## Fixes found during verification

1. Swatches and shape tiles cannot take focus. After a click on one, no element had keyboard focus, so `[`, `]` and `R` reached nothing. The probe showed `propagationStopped=False`. A swatch or tile pick now focuses the canvas, as a board click already did. Probe source: `ZzBlockToolsProbe.cs.txt`.
2. An invalid ghost drawn over a block of a similar colour, such as red over pink, was hard to see. Ghost cells now get a solid 2 px inset border in green or red on top of the 50 % fill.

## Gestures and captures (isolated copy, copy-only `ZzBlockToolsCaptureProbe.cs.txt`)

The probe drives the real window at 960x640, 1280x800 and 1600x1000 with real pointer and key events. Its output is `blocktools-probe.txt`. The captures are `blocktools-{size}-{initial,ghost-valid,ghost-invalid,placed,drag-ghost,after-undo}.png`, flipped upright. As in task 2, the GrabPixels path makes colours look brighter than on screen.

- The shape list shows 11 tiles from `BlockShapeLibrary`, all with prefabs and enabled. Colors shows 11 swatches.
- Place: arming Single sets tool=Place. Clicking occupied (0,1) is refused with "Overlaps Pink block b2 at (0,1).", blocks stay at 2 and the draft stays clean. Clicking free (0,0) adds b3, blocks go to 3 and the draft is dirty.
- Esc returns to Select. Dragging b1 up one cell shows the ghost at offset (0,1), and the drop moves the origin.
- Right click removes a block (3 -> 2), and undo restores it (3). Undoing everything gives 2 blocks, a clean draft and the "Editing Level 1" status.
- The JSON sha256 is identical before and after, at every size.

## Acceptance mapping

- **AC-1** (place adds one block, one publish; an invalid cell is refused with a message naming the cell): `PlacingAShapeAddsOneBlockAndPublishesItOnce` (Unity-built window, real tile and canvas clicks: +1 block with the next free `b{n}`, tool colour, armed shape and the clicked origin; exactly 1 `DraftLevelPublished` within 1.5 s carrying the new block; a refused click leaves the count unchanged and the status shows the command's message with the cell). Also `PlaceAddsOneBlockWithTheNextFreeIdAndKeepsTheSource` and the probe. PASS in the copy.
- **AC-2** (an invalid drag snaps back; a valid drop moves the origin; Ctrl+Z restores origin, canvas and dirty state): `DraggingABlockMovesItAndUndoRestoresOriginCanvasAndDirtyState` (the invalid drag leaves the origin and cells unchanged and shows "Overlaps Pink block b2 at (1,2)."; the valid drag moves them; `Undo.PerformUndo` restores the origin and the canvas cells and clears `hasUnsavedChanges`). The keyboard path is `KeysInTextFieldsAreNotShortcuts` (an arrow key on the canvas nudges; the same key in the Level Name field does not). PASS in the copy.
- **AC-3** (the command test rejects each refusal case with its message): `PlaceRefusesAShapeThatLeavesTheBoard`, `PlaceRefusesAShapeThatOverlapsABlock`, `PlaceRefusesWithoutALevelOrAShape`, `MoveRefusesOffBoardAndOverlap`, `ReplaceCellsRefusesOffBoardOverlapAndEmptyFootprints`, `CommandsOnAMissingBlockAreRefused` and `RecolourChangesOnlyTheColour` (empty colour). PASS in the copy.
- **AC-4** (right click or the Eraser removes a block as one undo step): `RightClickAndEraserRemoveABlockAsOneUndoStep` (right click gives -1, one undo gives +1 and a clean draft; the Eraser button then a left click gives -1, one undo gives +1; Esc returns to Select). PASS in the copy.
- Unchanged behaviour: all task 1-2 tests stay green (shell 16, canvas 23). One shell assertion was updated because the design adds Flip V: `blocks.Query<Button>()` now counts 3 buttons (Rotate, Flip H, Flip V). Block ids on create use the lowest free `b{n}` (`NextBlockIdSkipsUsedIds`).

## Static checks

- New and changed sources contain no `System.Linq`, `schedule`, `string.Format`, `async`, `Update()` or coroutines.
- Every new class opens with a one-sentence summary. Fields use `m_`/`s_`/`k_`. Each new subscription names its owner and stop point:
  - canvas `PlaceRequested`, `MoveRequested`, `RemoveRequested`; swatch `ColorPicked`; palette `ShapePicked`: owned by the window GUI, dropped with it;
  - root `KeyDownEvent`: unregistered and registered again on each `CreateGUI`;
  - the canvas drag capture: stopped by pointer up, capture loss, Esc or a new model.
- No new asmdef or package. No reference from `ColorBlockJam.Editor` to `ColorBlockJam.Editor.LevelPreview`. No serialized asset changed.

## Deviations and notes

- Refusals return a designer message string (`bool Try...(out LevelData, out string issue)`), not a structured `LevelIssue`. Structured issues arrive with `LevelDraftIssues` in task 4.
- A swatch click (or `[` / `]`) with the Select tool recolours a block only when the designer chose it with a canvas click or the Selected Block dropdown; it always sets the colour for new blocks. The automatic block-0 default and a just-placed block are never recoloured (review fix, see below).
- `F` and `Shift+F` also flip the armed ghost (design: `R` rotates it). Without an armed shape they flip the selection.
- Door recolour, remove and Delete for doors are task 4 (design section 7). A swatch with a door selected only sets the tool colour.
- Test harness note: in batch mode a window that Unity builds itself keeps a NaN layout until `ValidateLayout`. The UnityTests validate it once after the window is built.

## Review fixes (2026-09-25 11:34-12:20 local, live Editor over MCP HTTP 8082)

Other-writer check: another session edited `LevelDatabaseWindow.cs` (database row: `FieldActionsRow` + `DatabaseNamePopup`) at 11:36:26, plus `LevelEditorShellTests.cs`, `DatabaseControlsTests.cs`, `Editor/BoardKit/*` and the piece library. Per the stop condition I waited until the window and those tests had been quiet for 10 minutes (quiet since 11:36:44, confirmed 11:46:51). I diffed the file against my snapshot (`LevelDatabaseWindow.review-before2.cs`): only the database-row lines had changed, none of this task's lines. I edited only after that.

| Finding | Verdict | Change |
|---|---|---|
| 1 Swatch recolours the default block | Fixed | `m_blockChosen` is true only after a canvas block click or a Selected Block dropdown pick. It is false after the block-0 default, a clamp, a place, a level load, and a chosen block that disappears or is replaced by its index neighbour (`SyncCanvas` compares ids). `PickColor` recolours only a chosen block. New test `SwatchesOnAFreshLevelSetOnlyTheToolColour`. `ArrowKeysNudge...` already selects by canvas click. |
| 2 Stale shape catalog | Fixed | `RefreshShapeCatalog(force)` re-reads when `BlockShapeLibrary.LoadedJson` changes by reference or the last read failed (forced on level load). It runs from `RefreshBlockTools`, `OnFocus`, `ApplyPieceType`, `ArmShape` (re-finds the tile's shape by name) and `PlaceBlock` (returns if disarmed). After a read, an armed shape that is gone or whose cells changed (`LevelShapeChoice.HasSameCells`) is disarmed, and the status says to pick it again. New test `ALibraryReloadedElsewhereRefreshesTheShapesAndDisarmsAChangedShape` (transient library: added shape gets a tile and a Piece Type entry, changed cells used, changed shape disarmed, unchanged shape stays armed, broken JSON shows the error and disarms). |
| 3 Stale regression evidence | Addressed | Re-run below at the current revision. |
| 4 Task 2 not closed | Recorded, not fixable here | Task 2's lifecycle is outside this contract's allowed paths. The orchestrator must close task 2 (its 10:29 verifier PASS) or record the dispatch decision before this task's closure gate. |
| 5 Backspace removes | Fixed | Removed the `KeyCode.Backspace` case; only Delete removes, as the design and the tooltip say. |
| 6 `Perform*` verbs | Fixed | `PerformUndo`/`PerformRedo` renamed `UndoEdit`/`RedoEdit` (buttons and Ctrl+Z/Y call sites). |

Files: `Editor/Levels/LevelDatabaseWindow.cs`, `Editor/Levels/Canvas/LevelShapeCatalog.cs` (`HasSameCells`, summary), `Tests/EditMode/LevelCanvasBlockToolsTests.cs` (2 tests + helpers).

Live Editor verification:

- `refresh_unity` (force, compile) then Console: 0 errors, and only the existing `VacuumMotionAssets.cs(54,13) CS0618` warning. `OnFocus`/`UndoEdit` are present in the loaded assembly. `LevelEditor.unity` is open and not dirty. No scene was saved.
- Contract set (14 classes) job `29f1495140ef433faaebc2c559e07b08`: **108/108 passed**, 0 failed, 0 skipped. LevelEditCommandTests 13, LevelRecordCopyTests 6, LevelEditorToolStateTests 4, LevelCanvasBlockToolsTests 8, LevelEditorShellTests 16, DatabaseControlsTests 12, LevelDatabaseAddressablesTests 6, LevelDatabaseInspectorTests 4, LevelCanvasElement/HitTest/Hover/Model 7/7/2/7, LevelDatabaseTests 7, LevelPreviewTests 9.
- Full assemblies (jobs `1c3983ed…`, `0a76fb46…`): 181/186 passed. Failures outside this task:
  - 4 BoardKitTests and ShapeLibraryJsonTests failures ("Expected 11 but was 12", production prefab null). A 12th piece, `NewPieceTest`, was added to `Data/Board/PieceDatabases/BlockShapeLibrary.json` and `BlockShapeType.cs` at 11:34, before any review edit and outside this contract.
  - `LevelEditorShellTests.UndoAndRedoOfABoardEditUpdateFieldsDirtyMarkerAndPreview` timed out waiting 5 s for the delayCall publish in 2 of 3 multi-class runs. Those runs coincided with MCP WebSocket drops, and another session's test job was running (`tests_running` refusal). The test passed alone (`00cf37d7…`), as a class (`607fb32a…` 16/16), with block tools (`550d2bfa…` 24/24), with DatabaseControls (`2f69f49d…` 28/28), with the canvas and database classes (`8f19f267…` 69/69) and in the contract set above. Neither its body nor the publish path changed in this review. I treat it as timing contention in the shared live Editor and flag it as a flakiness risk.
- `LevelDatabase.json` sha256 is still `02c013a54e180f9570daf142505e018e28564ca4f7ab91bad847b35d8f3ecd6e`.

## Still open

- The user still has to accept the look and feel.
- The 12-shape library and BoardKit test failures belong to the other writer's piece-database work.
- Task 2's lifecycle closure (finding 4).

## Independent verifier (2026-09-25 11:59-12:07 local, live Editor over MCP HTTP 8082)

Instance `ColorBlockJamCase2026@99077e4df1ecfaa4`. No production code or test was edited. Before: `LevelEditor.unity` open and not dirty, not in Play Mode, the user's docked Level Editor window (level-01) clean. After: the same, `LevelDatabase.json` sha256 `02c013a54e180f9570daf142505e018e28564ca4f7ab91bad847b35d8f3ecd6e` unchanged, no scene saved.

Compile and Console: `EditorUtility.scriptCompilationFailed=False`. `ColorBlockJam.Editor.dll` was written at 11:49:16, after the last source edit at 11:48:53. The loaded assembly has `UndoEdit` and `m_blockChosen`. The Console has no errors from this task. The only entries are MCP WebSocket warnings and the other writer's BoardKit "Could not restore piece JSON" errors.

Tests: contract set of 14 classes, job `514f43e90ef14807b69f71f156e2f847`. **108/108 passed**, 0 failed, 0 skipped, 0 inconclusive. Per class: LevelEditCommandTests 13, LevelRecordCopyTests 6, LevelEditorToolStateTests 4, LevelCanvasBlockToolsTests 8, LevelEditorShellTests 16, DatabaseControlsTests 12, LevelDatabaseAddressablesTests 6, LevelDatabaseInspectorTests 4, LevelCanvasElement/HitTest/Hover/Model 7/7/2/7, LevelDatabaseTests 7, LevelPreviewTests 9. The list is in `verifier-editmode-contract-set.txt`. No window test hit its `Assert.Ignore` or `Assume` branch.

Live gestures: `VerifierLiveProbe.cs.txt`, run through `execute_code`. It drives a separate floating `LevelDatabaseWindow` with real pointer and key events. The user's window was not touched. Captures are `verifier-live-*.png`.

- AC-1 (960x640):
  - The Single tile arms Place.
  - Clicking occupied (0,1) is refused: blocks stay at 2, the status reads "Overlaps Pink block b2 at (0,1).", and the draft stays clean.
  - Clicking free (0,0) adds `b3` at (0,0), Red/Single, and the draft is dirty.
  - A controlled re-run counted `DraftLevelPublished` **exactly 1** time, carrying 3 blocks.
  - Note: in the first run the count was still 0 about 3 s after the place, because the delayCall publish had not run yet while the Editor was unfocused. The re-run waited about 8 s. This is timing only.
- AC-2:
  - Dragging b1 by (-1,0) onto b2 snaps it back: the origin and canvas cell stay (2,2) and the status reads "Overlaps Pink block b2 at (1,2).".
  - Mid-drag, the ghost shows at offset (0,1) (`verifier-live-960x640-drag-ghost.png`).
  - The drop moves the origin to (2,3), as undo group "Move Block".
  - Ctrl+Z on the canvas restores origin (2,2), the canvas cell and the dirty state. It stays dirty only because the earlier place was still pending, as expected.
- AC-3: the command tests assert the exact refusal messages for off-board and overlap cases: place, move and replace-cells, plus an empty footprint, a missing block and an empty colour. They passed in the live run above.
- AC-4:
  - Right click on b3: blocks 3 -> 2. One Ctrl+Z gives 3 again, with b3 restored.
  - Pressing the Eraser button sets tool=Eraser. A left click on b1 gives 3 -> 2, and one Ctrl+Z gives 3 again, with b1 back first.
  - Undoing the place returns 2 blocks, a clean draft and the "Editing Level 1" status.
- Sizes: the place/refuse/undo sequence and its captures were repeated at 1280x800 and 1600x1000 with the same results. The JSON hash was unchanged throughout.
- Cleanup: the verifier window was discarded and closed. The user's window's draft (level-01, 2 blocks) was re-published so the LevelEditor preview matches its pre-run publish (JSON identical). The only listener left is `LevelPreviewOwner.OnLevelPublished`.

Scope: every file this task changed is inside the allowed paths:
- `Editor/Levels/LevelDatabaseWindow.cs` and `LevelDraft.cs`;
- `Editor/Levels/Canvas/*` (the 7 new sources plus metas, and the canvas element, geometry and model);
- `Tests/EditMode/Level*Tests.cs` plus metas;
- this folder.

Forbidden paths that changed after 10:30 were changed by the other piece-library writer, not by this task: `Runtime/Board/BlockShapeType.cs`, `Data/Board/*`, `Editor/BoardKit/*` and `ProjectSettings/EntitiesClientSettings.asset`. The Plastic workspace carries every task's pending changes since cs:5, so this attribution rests on timestamps and the implementer's record, not on a per-task changeset.

Verdict: AC-1 to AC-4 PASS. Still open outside the automated gates:
- the user's acceptance of the look and feel;
- task 2's lifecycle closure (the dependency);
- the intermittent `UndoAndRedoOfABoardEditUpdateFieldsDirtyMarkerAndPreview` timeout. It did not reproduce in this run.
