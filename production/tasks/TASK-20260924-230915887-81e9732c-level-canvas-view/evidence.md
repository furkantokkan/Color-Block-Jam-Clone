# Evidence: TASK-20260924-230915887-81e9732c-level-canvas-view

Implementer run, 2026-09-25 09:26-09:56 (local). Base: `LevelDatabaseWindow.cs` last written 08:38 by the shell task (md5 `ce370e00…`), stable for 48 minutes before editing, so the other-writer stop condition did not apply.

## Transport

- The live Editor (PID 32260, `ColorBlockJamCase2026 - Game - Android`) is running, but its MCP bridge (HTTP 127.0.0.1:8082, instance `ColorBlockJamCase2026@99077e4df1ecfaa4`) answered every call with "ping not answered" from 09:30 to 09:54 (`mcpforunity://editor/state`, `execute_code`). Editor.log shows repeated `[WebSocket] Keep-alive failed`, the same failure the shell task recorded from 07:28.
- Fallback per the task rules: isolated copy `%TEMP%\CBJcanvas-3f9b2d` (robocopy `/E /R:0 /W:0 /MT:16` of the repository, Library included; Temp, Logs, obj, .plastic, .vs and Bee excluded; `Library/EditorInstance.json` removed). Unity 2022.3.62f2 `-batchmode -runTests` with its own `-logFile`. The live Editor, its scenes and its undo stack were not touched.
- Before the final run, every changed source file in the copy was byte-identical to the repository (`cmp`).

## Files

- New (all under the allowed paths):
  - `Assets/ColorBlockJam/Editor/Levels/Canvas/LevelCanvasModel.cs` (also `LevelCanvasBlock`, `LevelCanvasDoor`): immutable occupancy, door segments per side, footprints, label cells, next free `b{n}`/`d{n}`, selection resolve (unique id, else index, else clear).
  - `Canvas/LevelCanvasGeometry.cs`: cell size, centred origin, cell/edge rects, point to cell and point to ring segment.
  - `Canvas/LevelCanvasHit.cs` (`LevelCanvasHitKind`, `LevelCanvasHit`), `Canvas/LevelCanvasHitTest.cs`, `Canvas/LevelCanvasHover.cs`.
  - `Canvas/LevelCanvasSelection.cs` (`LevelCanvasSelectionKind`, `LevelCanvasSelection`).
  - `Canvas/LevelCanvasElement.cs`: one `generateVisualContent` pass with `Painter2D`, one Label per block.
  - `Tests/EditMode/LevelCanvasModelTests.cs`, `LevelCanvasHitTestTests.cs`, `LevelCanvasHoverTests.cs`, `LevelCanvasElementTests.cs`.
  - `.meta` files for all of the above, copied back from the copy's import.
- Changed: `Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs` only at the composition points: the canvas replaces the placeholder label in `canvas-slot`; `SyncCanvas` runs in `OnDraftChanged`; `SelectOnCanvas` handles clicks; `RefreshBlockTools` keeps a door selection and pushes the selection to the canvas; the Selected Block callback clears a door selection. md5 after: `b916611c…`.
- Not changed: `LevelDraft.cs`, `Editor/Controls/**`, `Editor/BoardKit/**`, `Editor/LevelPreview/**`, Runtime, Data, Scenes, Packages, ProjectSettings. No new asmdef or package. `ColorBlockJam.Editor` still does not reference `ColorBlockJam.Editor.LevelPreview`.

## Compile (isolated copy)

- Run 1 found one error: `LineCap.Square` does not exist in 2022.3 (only `Butt` and `Round`). The outline now uses `LineCap.Round`.
- Run 2: all assemblies compile with 0 errors. The only compiler warning is the existing `VacuumMotionAssets.cs(54,13) CS0618` (not this task).

## Tests (isolated copy, EditMode)

`-assemblyNames ColorBlockJam.Editor.EditModeTests;ColorBlockJam.Editor.LevelPreview.Tests`, results in `canvas-editmode-results.xml`: **149 of 149 passed**, 0 failed, 0 inconclusive, 0 skipped (42.7 s).

| Class | Passed |
|---|---|
| LevelCanvasModelTests (new) | 7 |
| LevelCanvasHitTestTests (new) | 7 |
| LevelCanvasHoverTests (new) | 2 |
| LevelCanvasElementTests (new) | 5 |
| DatabaseControlsTests | 9 |
| LevelDatabaseAddressablesTests | 6 |
| LevelDatabaseInspectorTests | 4 |
| LevelPreviewTests | 9 |
| LevelEditorShellTests (task 1) | 16 |
| LevelCreateColourTests, LevelDatabase*Tests, PlayModeGuard and the rest of the assembly | all passed |

## Gestures and captures (isolated copy, `ZzCanvasCaptureProbe.cs.txt`)

The copy-only probe (not in the repository) opened the window at 960 x 640 and 1280 x 800, drove the gestures with real pointer events and grabbed the window pixels. Output: `canvas-probe.txt`; the captures are flipped upright and stored here.

- 960 x 640: canvas 680 x 509, cell 74 px, board 296 x 370 centred at (340, 254). 1280 x 800: canvas 1000 x 669, cell 99 px, board centred at (500, 334.5).
- Level 1: 4 x 5 board, doors d1 Top 1+3 LightBlue and d2 Bottom 0+3 Pink, labels `b1` and `b2` (`canvas-*-initial.png`).
- Click on b2: canvas selection Block/1/b2, dropdown `2. b2`; hover on an empty cell shows a highlight (`canvas-*-block-selected.png`).
- Click on door d1: selection Door/0/d1, dropdown `Door d1`, Piece Type/Rotate/Flip disabled (`canvas-*-door-selected.png`).
- Dropdown back to `1. b1` selects b1 on the canvas; a click on a ring corner changes nothing.
- `hasUnsavedChanges` stayed false through every gesture. `LevelDatabase.json` sha256 `02c013a54e180f9570daf142505e018e28564ca4f7ab91bad847b35d8f3ecd6e` before and after the probe, and in the repository after this task.

## Acceptance mapping

- AC-1 (level 1 shows 4 x 5 cells, ring, both doors in colour, b1/b2 labels): `CanvasDrawsTheSelectedLevelWithOneLabelPerBlock`, `ModelMapsBlockCellsAndDoorSegments`, the probe and the captures. PASS in the copy.
- AC-2 (resize keeps the board centred, cell >= 18 px): `ResizedWindowKeepsTheBoardCentredWithCellsOfAtLeastEighteenPixels`, `GeometryCentresTheBoardAndFitsItsRing`, `GeometryKeepsCellsAtLeastEighteenPixels`, probe numbers. PASS in the copy.
- AC-3 (hover inside one cell requests no repaint): `MovingInsideOneCellRequestsNoRepaint`, `EnteringAnotherCellSegmentOrLeavingRequestsOneRepaint`, `PointerMovesInsideOneCellKeepTheSameHover`. The element calls `MarkDirtyRepaint` for hover only when `LevelCanvasHover.Update` returns true. PASS.
- AC-4 (click selects on canvas and dropdown, and back): `ClickedBlockAndDropdownSelectionStayInSync`, `ClickedDoorIsSelectedAndDisablesTheBlockTools`, `SelectionResolvesByUniqueIdThenIndexThenClears`, probe. Corners hit as Outside: `RingCornersAndFarPointsAreOutside`. PASS in the copy.
- Unchanged behaviour: all 16 shell tests and 9 DatabaseControlsTests pass; viewing never marks the draft dirty.

## Deviations and notes

- Fit reserve: design section 3 reserves `W + 1` (the 0.5-cell ring per side). Door bars reach 0.75 cell out like `DoorSlot`, so the fit reserves `W + 1.5` and `H + 1.5`; otherwise a door bar overflows the canvas. Margin 12 px.
- No tool switch exists yet (tool state arrives with task 3), so "repaint on tool change" has nothing to react to; the canvas repaints on model, hover, selection, geometry and palette changes only. No `schedule` loop.
- A click on an empty cell, ring segment or corner keeps the current selection (no deselect gesture in this task).
- A door selection shows `Door <id>` in the Selected Block dropdown and disables the block tools; picking a block in the dropdown replaces it. A block selection resolves by unique id after draft edits, else by index, else falls back to the shell's index clamp.
- The block label sits on the footprint cell nearest the footprint centre (first such cell on ties).

## Open

- Live-Editor evidence is still missing because the MCP bridge is dead: `refresh_unity` + clean Console in the live Editor, live `run_tests`, a live window capture and live gestures. Rerun them when the bridge answers.
- User acceptance of the look and feel (captures are in this folder).

## Review fixes (2026-09-25 10:05-10:20)

Reviewer findings verified against the code and fixed inside the allowed paths. `LevelDatabaseWindow.cs` is unchanged (md5 `b916611c…`).

- **SetModel label crash (major/critical, three reports of one defect): fixed.** `LevelCanvasElement.SetModel` now calls `RefreshLabels()` before `UpdateGeometry()`, so `PositionLabels` never indexes past the new model's blocks. Regression tests:
  - `LevelCanvasElementTests.ModelWithFewerBlocksAndAnotherBoardSizeKeepsOneLabelPerBlock`: 5x6/3 blocks, then 5x5/2, 3x3/1 and `Empty`; no exception, label count and texts match each model.
  - `LevelCanvasElementTests.SwitchingToALevelWithFewerBlocksRefreshesTheCanvasAndTheBlockChoices`: the window switches Target Level from the first larger level to the smaller level before it (level 3 to level 2 with the shipped JSON); canvas labels and Selected Block choices match the new draft, draft stays clean.
  - Red check: with the old call order restored in the copy only, both new tests failed with `IndexOutOfRangeException` (`-testFilter LevelCanvasElementTests`: 5 passed, 2 failed); the copy was then restored byte-identical to the repository.
- **Overlap paint vs hit order (minor): fixed.** `LevelCanvasModel` occupancy is now last-wins for blocks and door segments, matching the paint order (later entities are drawn on top). `ModelKeepsOffBoardCellsOutOfOccupancyAndFlagsUnknownColours` now asserts last-wins for an overlapping block and an overlapping door.
- **Hit-test summary order (minor): fixed.** The `LevelCanvasHitTest` summary now reads cell or block first, then door bar, then free ring segment.
- **k_doorDepth comment (minor): comment fixed, geometry kept.** The DoorSlot equivalence was wrong (`DoorSlot.k_ringOffset` 0.75 is from the floor cell centre, 0.25 cell beyond the edge). The comment now cites design section 3's door bars (0.75 cell outward) and states the W + 1.5 fit reserve. Design doc follow-up for its owner (docs/ is outside this contract): section 3 says doors reach 0.75 cell outward "like DoorSlot" while its sizing formula reserves W + 1; the as-built canvas reserves W + 1.5 and the DoorSlot comparison should be dropped or corrected.

Transport: the 8082 bridge still answered "ping not answered" from 10:08 to 10:20 (fresh MCP session too), so the run used the isolated copy `%TEMP%\CBJcanvas-3f9b2d` after a `robocopy /MIR /R:0 /W:0` of Assets, Packages and ProjectSettings; the changed files were `cmp`-identical to the repository.

- Compile: 0 `error CS`; no new warnings.
- EditMode (`ColorBlockJam.Editor.EditModeTests;ColorBlockJam.Editor.LevelPreview.Tests`): **151 of 151 passed** (149 before plus the 2 new tests), 0 failed/inconclusive/skipped, 43.0 s. Results: `canvas-review-fix-results.xml`.
- `LevelDatabase.json` sha256 still `02c013a5…ecd6e`.
- Still open: live-Editor refresh, clean Console and live `run_tests` once the bridge answers; user acceptance of the look.

## Independent verification (2026-09-25 10:15-10:30)

Verifier: separate session. No production code or tests were changed. Only files under this task folder were added.

### Transport

- I retried the live bridge at 10:15, 10:16-10:19 (six polls), 10:22 and 10:29, with fresh MCP sessions. `mcpforunity://instances` lists `ColorBlockJamCase2026@99077e4df1ecfaa4`, but `mcpforunity://editor/state`, `read_console` and `execute_code` all return "Unity session not ready ... (ping not answered)". Editor.log (PID 32260) keeps logging `[WebSocket] Keep-alive failed` / `Connection closed`. I did not restart the bridge or touch the live Editor, its scenes or its undo history.
- The bridge was down for more than 5 minutes, so I followed the fallback rule. I made a fresh isolated copy, `%TEMP%\CBJcanvasVerify-9e41c7`: a robocopy `/E /R:0 /W:0 /MT:16` of the repository, Library included, with Temp, Logs, obj, .plastic, .vs and Bee excluded and `Library/EditorInstance.json` deleted. I ran Unity 2022.3.62f2 `-batchmode -runTests` with its own `-logFile`, without `-nographics`. It is independent of the implementer's copy `CBJcanvas-3f9b2d`.
- Before the runs I compared the copy with the repository using `cmp`. All 8 `Canvas/*.cs` files, the 4 `LevelCanvas*Tests.cs`, `LevelDatabaseWindow.cs` (md5 `b916611c…`) and `LevelDatabase.json` were byte-identical.

### Files changed by this task

Every file this task changed is inside the allowed paths. I checked by listing every repository file modified since 09:25, excluding Library, Temp, Logs, obj, .plastic and .vs:
- `Assets/ColorBlockJam/Editor/Levels/Canvas/LevelCanvas{Element,Geometry,Hit,HitTest,Hover,Model,Selection}.cs` + `.meta`
- `Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs`. The diff against the pre-task snapshot `ce370e00…` touches only the canvas composition points: `using Runtime.Art`, the canvas fields, the canvas replacing the placeholder label in `canvas-slot`, `SyncCanvas`, `SelectOnCanvas`, the door-aware `RefreshBlockTools`, and the selection reset in the dropdown callback.
- `Assets/ColorBlockJam/Tests/EditMode/LevelCanvas{Element,HitTest,Hover,Model}Tests.cs` + `.meta`
- `production/tasks/TASK-20260924-230915887-81e9732c-level-canvas-view/**`
- In the same time window, files also changed under `production/tasks/TASK-20260924-230915885-717889da-level-editor-shell/` (`verifier2-*`, evidence). They belong to the shell task's own verifier, not to this task. `Canvas.meta` and `Canvas/LevelBoardResize.cs` date from 07:34-07:43 and belong to the shell task.
- No file was changed under Packages, ProjectSettings, Runtime, Presentation, Environment, Data, Scenes, BoardKit or LevelPreview, and no asmdef was added. `LevelDatabase.json` sha256 is still `02c013a54e180f9570daf142505e018e28564ca4f7ab91bad847b35d8f3ecd6e`.

### Static checks

- The canvas sources contain no `System.Linq`, `schedule`, `string.Format`, `async`, `Update()`, `EditorApplication.update` or coroutines.
- Every class opens with a one-sentence summary. Fields use the `m_`/`s_`/`k_` prefixes. The window names an owner and a stop point for the `SelectionRequested` subscription.
- The layer order matches design section 3: floor, ring, doors with notch, blocks with a 2 px outline, then hover, a 3 px white selection and a 1 px grid.

### Test run (isolated copy)

`-assemblyNames ColorBlockJam.Editor.EditModeTests;ColorBlockJam.Editor.LevelPreview.Tests` passed **152 of 152**: 0 failed, 0 inconclusive, 0 skipped, 47.0 s. The 152 are the 151 repository tests plus my copy-only probe. Results are in `verifier-editmode-results.xml`.

| Class | Passed |
|---|---|
| LevelCanvasModelTests | 7 |
| LevelCanvasHitTestTests | 7 |
| LevelCanvasHoverTests | 2 |
| LevelCanvasElementTests | 7 |
| DatabaseControlsTests | 9 |
| LevelDatabaseAddressablesTests | 6 |
| LevelDatabaseInspectorTests | 4 |
| LevelPreviewTests | 9 |
| LevelEditorShellTests | 16 |
| rest of both assemblies | all passed |

- Compile: 0 `error CS`. The only warning is the existing `VacuumMotionAssets.cs(54,13) CS0618`.

### Probes (copy-only; sources saved as `ZzCanvasVerifyProbe.cs.txt` and `ZzCanvasResizeProbe.cs.txt`)

The probes open the real window, send real `PointerDown`/`PointerUp`/`PointerMove` events through the panel, and grab the window pixels. Outputs are in `verifier-probe-output.txt` and `verifier-resize-output.txt`. The captures are `verifier-{960x640,1280x800,1600x1000}-{initial,hover,block-selected,door-selected}.png`, all upright.

- **AC-1 PASS.** In every window the canvas sits in `canvas-slot` and shows model 4x5. d1 is Top 1+3 in LightBlue and d2 is Bottom 0+3 in Pink, both in known colours. There are exactly 2 labels, `b1` and `b2`, each centred on its block's label cell. In the captures, pixels in the d1 bar and the b1 cells read LightBlue, and pixels in the d2 bar and the b2 cells read Pink. The ring is drawn with rounded corners, and the door notches point outward.
- **AC-2 PASS.** Separate windows at 960x640, 1280x800 and 1600x1000 give cells of 74, 99 and 130 px. I also resized one window in place: 960x640, then 1280x800, 1600x1000, 1100x660, back to 960x640, then 2000x700. The cells were 74, 99, 130, 77, 74 and 83 px. At every step the board centre is within 1 px of the canvas centre (|dx| = 0, |dy| <= 0.67), the cell is at least 18 px, the labels follow their cells, and the door bars stay inside the canvas.
  - The window's `minSize` is 960x640, so a window resize never reaches the 18 px floor. The floor itself is covered by `GeometryKeepsCellsAtLeastEighteenPixels`. The 700x420 probe step was clamped to 960x640, so I discarded its captures.
- **AC-3 PASS.** I counted `generateVisualContent` calls, with the panel repainted immediately after each move. Entering empty cell (0,0) gave 1 regeneration. Three more moves inside that cell gave 0 more, and the panel `repaintVersion` stayed unchanged (881 -> 881). Entering cell (1,0) gave exactly 1 more. The results were the same at all window sizes. The unit tests `MovingInsideOneCellRequestsNoRepaint` and `PointerMovesInsideOneCellKeepTheSameHover` also pass.
- **AC-4 PASS.** Both directions stay in sync:
  - Initially both the canvas and the dropdown show b1 (`1. b1`).
  - Clicking a b2 cell that is not its label cell selects `Block/1/b2` and shows `2. b2` in the dropdown.
  - Clicking an empty cell keeps b2.
  - Picking `1. b1` in the dropdown selects `Block/0/b1` on the canvas.
  - Clicking door d2 selects `Door/1/d2`, the dropdown shows `Door d2`, and Rotate is disabled.
  - All 4 ring corners hit-test as `Outside`, and clicking them keeps the door selection.
  - Picking `2. b2` in the dropdown replaces the door selection with b2.
  - `hasUnsavedChanges` stayed false throughout, and the level JSON sha256 is the same before and after.

### Observations (non-blocking)

- The captures are uniformly brighter than on screen. The editor chrome itself reads about 0.49 grey, and every sampled palette colour comes out as its gamma-encoded value (Wall 0.22 reads as 0.51, Pink G 0.27 reads as 0.56). This is the GrabPixels/ARGB32 capture path encoding twice, not a canvas colour bug. Judge the look on the live screen.
- When the pointer hovers a block, moving between two cells of the same block still repaints once, because the hit keys on the cell. The drawn hover stays the same. This is harmless and does not affect AC-3, which is about moves within one cell.
- `RefreshBlockTools` builds `Door {id}` with string interpolation. That matches the window's existing `$"{index + 1}. {id}"` and runs in editor-only, non-hot code.

### Still open (unchanged by this verification)

- The live Editor still needs `refresh_unity`, a clean Console, and `run_tests` over MCP once the 8082 bridge answers ping.
- The user still has to accept the look and feel.
