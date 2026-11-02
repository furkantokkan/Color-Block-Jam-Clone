# Evidence — TASK-20260925-142546927-22e06638-level-editor-transform-tools

Live Editor: `ColorBlockJamCase2026@99077e4df1ecfaa4`, Unity 2022.3.62f2. MCP for Unity reached over HTTP at `127.0.0.1:8082`. Workspace cs:7 plus uncommitted work.

## Compilation

- `refresh_unity` (force, compile requested) at 2026-09-25 ~18:02 local, then `read_console` for errors and warnings. The result has no `error CS`.
- The only entries were three `Import Error Code:(4)` lines and one MCP WebSocket notice, which predate this change, plus one "Level Editor: unsaved level changes were discarded by an editor reload." warning.
- `Editor.log` places that warning before this session's refresh. Its reload was an automatic refresh.
- Earlier `error CS0117` lines in `Editor.log` (`LevelEditorTool.Rotate`, `LevelEditorIcons.k_rotation`) came from refreshes that other sessions or auto-refresh ran while the edit batch was half written. The final reload has none.
- `execute_code` read the loaded enum: `Select,Pen,Eraser,RotateCounterClockwise,RotateClockwise,MirrorHorizontal,MirrorVertical`.

## EditMode

1. Focused run, job `cbf5375ae2f74033894294792bfdf7ba`. It covered `LevelEditorToolStateTests`, `LevelEditorIconsTests`, `LevelCanvasBlockToolsTests`, `LevelCanvasElementTests`, `LevelEditorToolsCleanupTests`, `LevelEditorShellTests` and `LevelCanvasGatesEdgesTests`: 53/53 completed, 0 failures.
2. Full Level run, job `b0fb349b7d444005b500ee6b2f611aec`, group `^ColorBlockJam\.Tests\.[A-Za-z.]*\.Level[A-Za-z0-9]*Tests\.`.
   - `TestResults.xml` (`%USERPROFILE%/AppData/LocalLow/Furkan Tokkan/ColorBlockJamCase2026/TestResults.xml`, written 18:04:51 local) was parsed in this session: total 442, passed 442, failed 0, inconclusive 0, skipped 0 (15:04:15Z-15:04:42Z).
   - The changed and new tests all read `Passed`. This covers `TransformToolsActOnTheClickedBlockWithoutASelection`, `ATransformToolRefusesAnOverlapIgnoresEmptyCellsAndRightClickStillRemoves`, `TransformKeysTurnTheArmedGhostElseTheSelectedBlockWithoutChangingTheTool`, `ToolButtonsAndNumberKeysSwitchTheToolAndThePenArmsTheLastPieceAgain`, `ToolBadgeFollowsThePointerInAFrameAndTakesThePenColour`, `ToolsColumnHoldsSwatchesShapesAndEditControls`, `ToolRowsWrapAtANarrowToolsWidthInsteadOfClipping`, `ClickedDoorIsSelectedAndKeepsTheTransformToolsShown`, `EveryToolShowsItsOwnButtonIconInTheCanvasBadge`, `TransformRowAndDividerStayShownWithoutASelectedBlockOrAnArmedPiece`, `PenTooltipCarriesTheGateHelp`, `SelectTransformPicksUpOnlyTheFourRotateAndMirrorTools` and `ThePenRearmsTheLastPieceAndATransformToolArmsNothing`.
   - Another session's 44-test PlayMode/Environment run later overwrote that file, so it no longer holds this run.

## Window capture (V-1)

- `evidence/transform-tools-rotate-cw-active.png`, 1650 x 1080 at 1.5 ppp.
- It was taken with `GrabPixels` from a separate LevelDatabaseWindow instance, which was closed afterwards with no unsaved changes.
- The shot has `rotate-block-cw` picked up and the pointer hovering block b1.
- It shows Select, Pen and Eraser, then the divider and the four transform tools. ↻ is highlighted. The canvas badge frame holds the rotate-right icon beside the pointer.

## Independent verification

- A read-only general-purpose verifier returned **PASS** with no defects.
- **Containment:** no remaining `LevelEditorTool.Rotate`, `SelectRotate`, `RotateRequested`, `k_rotation`, `tool-rotate`, `RefreshTransformButtons`, `m_pieceActions` or `m_toolsDivider`. `rotation.png` is kept.
- **Acceptance:** AC-1 to AC-6 are mapped to test methods.
- **Preserved behaviour** is confirmed:
  - right click, Esc, `PickColor` Select gate, R default-selection rule;
  - corner voids;
  - the preview publish path.
- **Contiguous enum** is confirmed.
- **Verifier gaps:**
  - AC-7 execution was unproven for the verifier, only because `TestResults.xml` had been overwritten. The parse above was taken before that.
  - F-key parity with the R default-selection rule is inferred from the shared `FlipSelection`/`RotateSelection` structure; no test covers it.
