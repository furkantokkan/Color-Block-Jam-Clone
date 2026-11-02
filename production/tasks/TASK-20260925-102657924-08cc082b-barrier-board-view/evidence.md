# Evidence: TASK-20260925-102657924-08cc082b-barrier-board-view

Implementer run, 2026-09-25, live Editor `ColorBlockJamCase2026@99077e4df1ecfaa4`
through MCP for Unity HTTP (127.0.0.1:8082). No isolated copy was needed.

## Changes

| File | Change |
|---|---|
| `Assets/ColorBlockJam/Environment/EnvironmentBarrierComposer.cs` (new) | Plain C# composer: per barrier, mask = board floor minus its cells, rule set matched on its quadrants only; closed height, wall scale, barrier tint; refuses off-floor cells and empty/repeated IDs |
| `Assets/ColorBlockJam/Environment/EnvironmentBarrier.cs` (new) | Readonly struct: ID, piece range, `StartsOpen` |
| `Assets/ColorBlockJam/Environment/EnvironmentComposer.cs` | `GetQuadrant`/`GetQuadrantCenter` private to internal (shared quadrant grid); no behaviour change |
| `Assets/ColorBlockJam/Environment/EnvironmentDatabase.cs` | `m_barrierColor` (default `PaletteColor.Wall`), `GetBarrierColor()` |
| `Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset` | `m_barrierColor: 11` written through `SerializedObject` + `SaveAssetIfDirty`; GUID unchanged |
| `Assets/ColorBlockJam/Environment/EnvironmentView.cs` | `Build(layout, level)` (old `Build(layout)` = no board objects), barrier pieces after play-area pieces in the active array, ID-to-index dictionary built on Home, `SetBarrierOpen(id, open, animate)`, `SetBarrierTweensPaused`, `StopBarrierTweens`, `BarrierCount`; Clear stops barrier tweens before returning pieces; Restart (same layout and level) snaps barriers to start |
| `Assets/ColorBlockJam/Environment/EnvironmentPreview.cs` | Dev scaffold passes the level to `Build(layout, level)` |
| `Assets/ColorBlockJam/Environment/ColorBlockJam.Environment.asmdef` | + `PrimeTween.Runtime` |
| `Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewHost.cs` | Passes the published level (and the restored level) to `Build(layout, level)`; look constants untouched |
| `Assets/ColorBlockJam/Tests/Environment/EditMode/EnvironmentBarrierComposerTests.cs` (new) | AC-1 composer tests |
| `Assets/ColorBlockJam/Tests/Environment/PlayMode/EnvironmentBarrierViewTests.cs` (new) | AC-1 view, AC-2 lifecycle and 0 B tests |
| `Assets/ColorBlockJam/Tests/Environment/PlayMode/ColorBlockJam.Environment.PlayModeTests.asmdef` | + `PrimeTween.Runtime` |
| `Assets/ColorBlockJam/Tests/LevelPreview/LevelPreviewTests.cs` | + `BarriersShowInTheirStartStateAndFollowDraftEdits` (AC-4) |
| `docs/Environment-Layout.md` | Board objects: Barrier section (composition, heights, API, owner/stop point, Game scene, preview); assembly, data and lifecycle rows |
| `PROJECT.md` | One sentence under the environment rule, decision row DEC-121 |
| `docs/environment/barrier-preview-start.png`, `barrier-preview-edited.png` (new) | AC-4 captures |

## Compile and Console

- `refresh_unity` (force, compile request) twice; Console after each: 0 errors.
  The only warnings are pre-existing (`VacuumMotionAssets.cs` CS0618, MCP
  WebSocket notices, "LevelEditor is an edit-only scene").

## Tests (MCP `run_tests`)

| Job | Scope | Result |
|---|---|---|
| `e5460ece0dbe4f19ad4a259927493eeb` | EditMode: `ColorBlockJam.Environment.EditModeTests`, `ColorBlockJam.Editor.LevelPreview.Tests` | 78/78 passed |
| `cb5f4bee1298409598167759831b8aa6` | EditMode by name: 5 `EnvironmentBarrierComposerTests` + `LevelPreviewTests.BarriersShowInTheirStartStateAndFollowDraftEdits` | 6/6 passed |
| `91e6a8dce49c4d4fb4967db998c72ca5` | PlayMode: `ColorBlockJam.Environment.PlayModeTests` (5 new barrier view tests + 7 lifecycle) | 12/12 passed |
| `b887f461a9454beebb60781e9af6c4fa` | EditMode: full suite | 765/765 passed |
| `9a42cbf68c2f4deabcb34a146624cbd7` | PlayMode: full suite | Aborted by the MCP job manager ("Manually cleared stuck job") after 31/43 with no failure reported; rerun in two parts below |
| `52edca317d8447e6a824e49402d0f8e1` | PlayMode: Gameplay + Environment | 23/23 passed |
| `1251aeb16b3344f8a41b7f2784354ce1` | PlayMode: Presentation | 20/20 passed |

Acceptance mapping:

- AC-1: `SingleStraightAndLShapedBarriersGetTheirPieceKindsPositionsAndYaws`
  (exact kind/position/yaw sets for single, 3x1 and L), 
  `BarrierPiecesAreThePiecesOfAnInteriorWallBlockOnTheSameCells`,
  `BarriersFlushWithTheBoardEdgeOrAVoidCellWallEveryQuadrantOfTheirCells` (renamed in the review fix round),
  `BarrierOffTheFloorOrWithARepeatedIdIsRefused`,
  `ClosedBarriersStandOnTheirCellsAndOpenOnesSinkUnderTheFloor` (closed y 0,
  open y -0.8, barrier tint, floor tile and base kept under barrier cells,
  pool counts, Clear returns all).
- AC-2: `AnimatedFlipReachesItsHeightWithinThreeTenthsOfASecond`,
  `SuspendPausesTheTweenAndTeardownStopsItAndReturnsThePieces`
  (`Tween.GetTweensCount(piece)` 1 while running or paused, 0 after stop and
  Clear; paused heights unchanged over 0.4 s),
  `SnapAndRestartReturnBarriersToTheirStartStateWithoutRebuilding`,
  `BarrierFlipsPauseAndStopAllocateNothing` (Unity `Is.Not.AllocatingGCMemory`
  over animate/pause/resume/snap/stop after one warm-up call).
- AC-3: every existing environment golden test (`EnvironmentComposerTests`,
  including `RecomposingAPreparedBoardAllocatesZeroBytes`), the view
  lifecycle tests (Restart no-op, pools) and the full EditMode/PlayMode suites
  stay green; `LevelsWithoutBarriersComposeNothing`.
- AC-4: `BarriersShowInTheirStartStateAndFollowDraftEdits` (host in the test
  scene, no tween manager object in Edit Mode) plus the live check below.

## AC-4 live check (LevelEditor preview)

Through the real owner path: `LevelEditor.unity` opened additively,
`LevelPreviewOwner.OnLevelPublished` invoked (reflection) with a 6 x 7 level,
the preview camera rendered at 1080 x 1920, then `null` published and the
scene closed without saving (`CloseScene(scene, true)`, `isDirty` false).

| Draft | Piece heights read from the preview | Capture |
|---|---|---|
| run (2-4, 4) closed, L (1,1)(1,2)(2,1) open, single (4,2) closed | run 12 pieces y 0; L 12 pieces y -0.8; single 4 pieces y 0 | `docs/environment/barrier-preview-start.png` |
| run moved to (0-3, 4) and opened, L closed | old run cell (4,4): 0 pieces; run 16 pieces y -0.8; L 12 pieces y 0; single 4 pieces y 0 | `docs/environment/barrier-preview-edited.png` |

## Editor state and housekeeping

- The user's Editor had an untitled, clean scene at the start and has an
  untitled empty scene at the end; no scene was saved.
- The aborted full PlayMode job left `Assets/InitTestSceneb152ff05-5e5a-4a8b-ba84-0838646f7161.unity`
  (+ `.meta`), a Unity Test Framework temp scene. MCP `execute_code` blocks
  `AssetDatabase.DeleteAsset`, so it is still on disk and needs deleting by hand.
- Other sessions ran tests in the same Editor during this run; one
  "LevelEditor is an edit-only scene" warning may come from a Play Mode
  attempt while the LevelEditor scene was open additively for the capture.

## Review fix round (2026-09-25)

| Finding | Verdict | Change |
|---|---|---|
| Edge/void-flush barrier misses quadrants (major) | Confirmed, fixed | `EnvironmentBarrierComposer`: the mask now spans the ring (`RectInt(-3, -3, 2(W+2), 2(H+2))`) and marks every quadrant outside this barrier as floor, including void cells and the ring. An edge or hole-adjacent barrier gets the same pieces as an interior one. |
| Barrier 2+ cells wide leaves a floor square inside (major) | Confirmed, not fixable inside the contract | Every rule needs a floor neighbour, so all-barrier quadrants match nothing. Filling them needs a rule-set change or a new piece, which is a contract stop condition, or validation that keeps barriers one cell wide (schema owner). **AC-1 is unmet for barriers two or more cells wide; escalated.** `docs/Environment-Layout.md` now records it as a known gap (the wrong "like a wide wall block" sentence is gone), and the PROJECT.md DEC-121 row names it. |
| Suspend test timing-flaky (minor) | Confirmed, fixed | `SuspendPausesTheTweenAndTeardownStopsItAndReturnsThePieces` pauses in the flip's own call, then checks heights stay put over 0.4 s with the tween alive (count 1), so no frame length can finish the tween first. |
| Stray `InitTestScene...` in `Assets/` (minor, reported twice) | Confirmed, left for the user | `Assets/InitTestSceneb152ff05-5e5a-4a8b-ba84-0838646f7161.unity` + `.meta` are still on disk. They sit outside the allowed paths; deleting files and undoing their Plastic pending add are left to the user (Project window delete, then `cm undo` on both paths). Keep them out of any checkin. |
| Open barriers still drawn (minor) | Confirmed, fixed | `EnvironmentView.MoveBarrier`: a snap sets `Renderer.enabled = !open`; a sink tween hides the renderer through `OnComplete(renderer, s_hideRenderer)` (cached static delegate, 0 B); a rise turns the renderer on before it moves; `Clear` turns barrier renderers back on before the pieces return to the shared wall pools. |
| Doc "(below)" cross-reference (minor) | Confirmed, fixed | "(see Board objects: Barrier)". |
| Doc dependents sentence (minor) | Confirmed, fixed | Lists Gameplay (`GameAttemptLoader`, later the barrier adapter), the level preview, `ColorBlockJam.Editor`, tests and the Game scene; `Runtime` never references Environment. |

Tests changed: `EnvironmentBarrierComposerTests.BarriersFlushWithTheBoardEdgeOrAVoidCellWallEveryQuadrantOfTheirCells`
(replaces the weak edge test: exact pieces for a corner cell, a 3-cell run on
row 0 and a cell beside a void cell, plus exactly one piece per quadrant of
every barrier cell); `EnvironmentBarrierViewTests.ClosedBarrierFlushWithTheBoardEdgeCoversEveryQuadrantOfItsCells`
(new); renderer on/off checks added to the start-state, animated-flip,
suspend and Clear paths. `BarrierFlipsPauseAndStopAllocateNothing` still
covers the path with the sink's `OnComplete`.

Compile: `refresh_unity` (force), `EditorUtility.scriptCompilationFailed`
false, 0 Console errors. The only warnings are pre-existing or come from other
sessions: `VacuumMotionAssets.cs` CS0618, MCP WebSocket notices, `Import Error
Code:(4)` on Level Editor files that another session was editing, the
13-cell Square preview notice, and "Files generated by test without cleanup"
for another session's new `PieceEditorLinkTests.cs`. The domain reload also
logged "Level Editor: unsaved level changes were discarded by an editor
reload" from another session's open window.

| Job | Scope | Result |
|---|---|---|
| `940859472322440fba9d4a6bf367922f` | EditMode: `ColorBlockJam.Environment.EditModeTests`, `ColorBlockJam.Editor.LevelPreview.Tests` | 78/78 passed |
| `97474985ae754b17ab88ef1c279ce57b` | PlayMode: Environment | Did not start: MCP auto-failed it after 15 s while the Editor was busy (a "LevelEditor is an edit-only scene" guard fired from another session's run at the same time); rerun below |
| `198dbad2d11049faaf35c2d965cc9c6e` | PlayMode: `ColorBlockJam.Environment.PlayModeTests` | 13/13 passed |
| `4c61be2e109347a3b62c5b572b34717a` | EditMode: full suite | 784/784 passed |
| `d40036f071bd4de797a1066240f7801f` | PlayMode: Gameplay + Environment + Presentation (all PlayMode assemblies) | 44/44 passed |

AC-4 edge capture: `LevelEditor.unity` opened additively, the owner published
a 6 x 7 level with a closed 4-cell run on row 0 flush with the bottom and right
walls, the open L and the closed single, then `null` was published and the scene
closed without saving (`isDirty` false before close, no scene left open). Read
back: edge run 16 pieces at y 0, all 16 rendered; L 12 pieces at y -0.8,
0 rendered; single 4 pieces at y 0, 4 rendered. Capture:
`docs/environment/barrier-preview-edge-closed.png`, with no floor channel
between the run and the ring wall.

## Open items

- Game scene: `GameAttemptLoader` (gameplay owner, `Gameplay/**`) still calls
  `Build(layout)`; the gameplay Barrier task switches it to
  `Build(layout, level)` and adds the `BarrierViewAdapter`, which also calls
  `SetBarrierTweensPaused`/`StopBarrierTweens` from Suspend/Teardown.
- Wide barriers (two or more cells wide): AC-1 unmet, escalated (see above).
- Stray `Assets/InitTestSceneb152ff05-...unity` (+ `.meta`): the user deletes them by hand.
- Open barriers now switch off their renderers at rest. Device Frame Debugger
  numbers still belong to the NFR-P5 measurement.
- User acceptance of the look (captures above).

## Independent verification (2026-09-25, verifier)

Live Editor `ColorBlockJamCase2026@99077e4df1ecfaa4` through MCP for Unity HTTP
(127.0.0.1:8082). The verifier changed no production code or tests. Its
artifacts are under `verify/`. At the start and at the end, the Editor had an
untitled, clean scene and was not in Play Mode. No scene was saved.

### Tests (MCP `run_tests`)

| Job | Scope | Result |
|---|---|---|
| `b209ca443ae245e3a88c332554c20cd8` | EditMode: `ColorBlockJam.Environment.EditModeTests` + `ColorBlockJam.Editor.LevelPreview.Tests` | 78/78 passed |
| `a9d024a1f83a46519bc34cac4c25890c` | PlayMode: `ColorBlockJam.Environment.PlayModeTests` | 13/13 passed (all 6 `EnvironmentBarrierViewTests` included) |
| `759e88b9711f41aa98177ff52a2c8575` | EditMode: full suite | 788/788 passed |
| `33753a14305a4909905e7ede07495890` | PlayMode: full suite | Did not start: another session's PlayMode run held the Editor ("tests did not start within timeout") |
| `8e32caf2a8ab48d0bcfaf37ecd29ff6f` | PlayMode: full suite (rerun) | 44/44 passed |

Summaries: `verify/verifier-test-jobs.txt`. Console: no compile errors. The only
error is another session's `PrefabTests_*` piece JSON restore message.

### Acceptance scenarios

- **AC-1: PASS.** These EditMode tests passed in both runs:
  - `SingleStraightAndLShapedBarriersGetTheirPieceKindsPositionsAndYaws` checks exact kind@position/yaw sets for a single cell, a 3x1 run and an L.
  - `BarrierPiecesAreThePiecesOfAnInteriorWallBlockOnTheSameCells`
  - `BarriersFlushWithTheBoardEdgeOrAVoidCellWallEveryQuadrantOfTheirCells`
  - `BarrierOffTheFloorOrWithARepeatedIdIsRefused`

  The PlayMode test `ClosedBarriersStandOnTheirCellsAndOpenOnesSinkUnderTheFloor` covers a closed 3x1 and an open L: 12 pieces at y 0 and 12 at y -0.8 with renderers off, the barrier tint, and the floor tile and base kept under the cells.

  The live preview probe below found exactly one piece per quadrant for every barrier cell.

  Known gap, outside this scenario's given: a probe (`verify/verifier-wide-barrier-probe.cs.txt`) confirms that a 2x2 closed barrier passes `LevelData.Validate` but composes only 12 of its 16 quadrants. The four inner quadrants get no piece. The implementer already escalated this. It needs a rule-set or piece change, or a one-cell-wide validation rule from the schema owner.
- **AC-2: PASS.** These PlayMode tests passed:
  - `AnimatedFlipReachesItsHeightWithinThreeTenthsOfASecond`: one tween per piece, the target height within 0.3 s, tween count 0 afterwards.
  - `SuspendPausesTheTweenAndTeardownStopsItAndReturnsThePieces`: heights hold for 0.4 s while paused, `StopBarrierTweens` leaves 0 tweens, and `Clear` leaves 0 tweens, 0 active pool items and every piece inactive.
  - `SnapAndRestartReturnBarriersToTheirStartStateWithoutRebuilding`
  - `BarrierFlipsPauseAndStopAllocateNothing`: Unity `Is.Not.AllocatingGCMemory` over animate, pause, resume, snap and stop.

  Code read: every tween is `Tween.LocalPositionY` on the piece `Transform`. The ID lookup is built in `Build`. The sink callback is a cached static delegate. On the success path, the call does no LINQ, capture, concatenation or boxing; string concatenation happens only on the throw path.
- **AC-3: PASS.** The full EditMode suite (788) and full PlayMode suite (44) are green. That includes every `EnvironmentComposerTests` golden test, `RecomposingAPreparedBoardAllocatesZeroBytes`, `LevelsWithoutBarriersComposeNothing` and the `EnvironmentViewLifecycleTests` Restart and pool tests.
- **AC-4: PASS.** Live check through the real owner handler (`LevelPreviewOwner.OnLevelPublished`, called by reflection so other subscribers are not triggered). `LevelEditor.unity` was opened additively, then closed without saving; `isDirty` stayed false and no preview root was left. The level was 7 x 8 with a Top door and a block. Probe: `verify/verifier-ac4-probe.cs.txt`.

| Draft | Read back | Capture |
|---|---|---|
| run (1-3,4) closed, L (5,4)(5,5)(4,5) open, single (1,1) closed | run 12 pieces y 0, 12 rendered, 12/12 quadrants; L 12 pieces y -0.8, 0 rendered, 12/12; single 4 pieces y 0, 4 rendered, 4/4 | `verify/verifier-ac4-preview-start.png` |
| same ID; run moved to (1-3,6) and opened, L closed | old run cells: 0 pieces; run 12 pieces y -0.8, 0 rendered; L 12 pieces y 0, 12 rendered; single unchanged | `verify/verifier-ac4-preview-edited.png` |

### Files changed by this task, and allowed paths

The Plastic workspace carries many pending changes from other sessions, so
this list is the implementer's claimed set. The verifier checked it against
file timestamps (17:19 to 18:12) and against a grep for the new API symbols.
Every file is inside the contract's allowed paths:

- `Assets/ColorBlockJam/Environment/`:
  - `EnvironmentBarrierComposer.cs` (+ `.meta`)
  - `EnvironmentBarrier.cs` (+ `.meta`)
  - `EnvironmentView.cs`
  - `EnvironmentComposer.cs`
  - `EnvironmentDatabase.cs`
  - `EnvironmentPreview.cs`
  - `ColorBlockJam.Environment.asmdef`
- `Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset`: `m_barrierColor: 11`. The `.meta` is unchanged (mtime 2026-09-24).
- `Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewHost.cs`: the DEC-118 look constants are still present.
- `Assets/ColorBlockJam/Tests/Environment/`:
  - `EditMode/EnvironmentBarrierComposerTests.cs` (+ `.meta`)
  - `PlayMode/EnvironmentBarrierViewTests.cs` (+ `.meta`)
  - `PlayMode/ColorBlockJam.Environment.PlayModeTests.asmdef`
- `Assets/ColorBlockJam/Tests/LevelPreview/LevelPreviewTests.cs`
- `docs/Environment-Layout.md`
- `docs/environment/barrier-preview-start.png`, `barrier-preview-edited.png` and `barrier-preview-edge-closed.png`
- `PROJECT.md`: the environment sentence and the DEC-121 row
- `production/tasks/TASK-20260925-102657924-08cc082b-barrier-board-view/**`

Checks outside those paths:

- No barrier API symbol appears under `Gameplay/**`, `Editor/Levels/**` or `Runtime/Levels/**`.
- `GameAttemptLoader.cs:96` still calls `environment.Build(layout)`, as expected; it belongs to the gameplay owner.
- A side effect outside the allowed paths is still present: `Assets/InitTestSceneb152ff05-5e5a-4a8b-ba84-0838646f7161.unity` (+ `.meta`), left by the implementer's aborted PlayMode job. The user deletes it and runs `cm undo` on both paths.
- `ProjectSettings/ProjectSettings.asset` (17:35) and the Editor/Levels files in the same window match other sessions' work, not this task's.

### Open items after verification

- The wide-barrier gap is escalated. It sits outside the AC-1 scenario but inside the objective.
- The stray `InitTestScene` files need a manual delete.
- The user still has to accept the look.
- Game scene wiring belongs to the gameplay Barrier task.

## Post-verification fix round (2026-09-25, implementer)

The verifier reported all four criteria PASS and no FAIL/UNPROVEN, so no
production code or test changed. This round removed the one out-of-scope side
effect.

- **Stray `InitTestScene` reverted.** The aborted PlayMode job's temp scene
  `Assets/InitTestSceneb152ff05-5e5a-4a8b-ba84-0838646f7161.unity` (+ `.meta`,
  GUID `2e43c8b181ba7ad448777e56d3cfddc9`) was a pending Plastic add (`AD`).
  - Nothing referenced it: no hit for the GUID or name in `Assets/**` scenes,
    prefabs, assets or JSON, or in `ProjectSettings/**`.
  - Both files were copied to the session scratchpad
    (`stray-init-test-scene/`). Then `cm undo` removed the pending add; the
    status became `PR` (private).
  - Both files were then moved out of `Assets/` into that scratchpad folder,
    not hard-deleted.
  - `AssetDatabase.Refresh()` confirmed the result: the file no longer exists,
    `GetMainAssetTypeAtPath` returns null, and `FindAssets("InitTestSceneb152ff05")`
    returns 0.
  - `cm status` lists no `InitTestScene` item.
  - No checkin, shelve or other pending change was touched.
- **Wide barriers (2+ cells wide).** This remains the escalated known gap.
  Filling the all-barrier inner quadrants needs a rule-set or piece change,
  which is a contract stop condition. A one-cell-wide validation rule would go
  in `Runtime/Levels/**`, which is forbidden to this task. It is not a failed
  scenario, so this round does not change it.

| Job | Scope | Result |
|---|---|---|
| `558281187a67420cb2b78a489066344b` | EditMode: `ColorBlockJam.Environment.EditModeTests` + `ColorBlockJam.Editor.LevelPreview.Tests` | 78/78 passed |
| `0efff97340664a1a8e69d553d3cc100f` | PlayMode: `ColorBlockJam.Environment.PlayModeTests` | 13/13 passed; the run left no new `InitTestScene` in `Assets/` |

Compile: `EditorUtility.scriptCompilationFailed` false. The Console shows only
other sessions' errors: the `PrefabTests_*` piece JSON restore messages and
one transient "All compiler errors have to be fixed" from another session's
compile, which cleared. Editor state at the end: untitled clean scene, not
playing, no scene saved.

Open items: the wide-barrier gap (escalated), the user's acceptance of the
look, and Game scene wiring by the gameplay Barrier task.

## Independent verification, second pass (2026-09-25 18:36-18:41, verifier)

Live Editor `ColorBlockJamCase2026@99077e4df1ecfaa4` over MCP for Unity HTTP
(127.0.0.1:8082). The verifier changed no production code or tests. Its files
are `verify/verifier2-*`. The Editor started and ended on an untitled, clean
scene, not in Play Mode, with no compile failure. No scene was saved.

### Tests (MCP `run_tests`)

| Job | Scope | Result |
|---|---|---|
| `c9aaa073307c49a999f4d97158ad6bcf` | EditMode: full suite | 800/801 passed. The one failure is `ShapePrefabTests.UnrelatedUndoAfterCacheReloadCannotRestoreAnOldLibrarySnapshot` (`IOException: Unable to remove the file to be replaced`). It belongs to the piece-editor area, not to this task. |
| `6ff29f6aa4244fe38442cd7caf4a8781` | EditMode: that test alone | 1/1 passed. The failure was a transient file lock. |
| `dabb817ad22e4bcd81ede7fbe07a406d` | PlayMode: full suite | 44/44 passed |
| `44a0782842574122ade3136b7743c7c6` | PlayMode: `ColorBlockJam.Environment.PlayModeTests` | 13/13 passed: 6 `EnvironmentBarrierViewTests` and 7 `EnvironmentViewLifecycleTests` |

The full EditMode suite includes every `EnvironmentBarrierComposerTests` test,
every `EnvironmentComposerTests` golden test, `RecomposingAPreparedBoardAllocatesZeroBytes`
and `LevelPreviewTests.BarriersShowInTheirStartStateAndFollowDraftEdits`.
Nothing in `Environment` or `LevelPreview` failed.

Console errors all come from other areas: the `PrefabTests_*` "Could not
restore piece JSON" messages, one stale "All compiler errors have to be
fixed" (`scriptCompilationFailed` is false now), and "Already added
touchscreen" from the PlayMode input tests. The full EditMode run rewrote
`AddressableAssetSettings.asset`, `Level Database.asset`, `BlockShapeLibrary.asset`
and `EntitiesClientSettings.asset` through other suites (the Addressables
registration, level database and shape prefab tests). No test in this
task's test folders writes an asset.

### Acceptance scenarios

- **AC-1: PASS.** The EditMode composer tests passed in the full suite:
  - `SingleStraightAndLShapedBarriersGetTheirPieceKindsPositionsAndYaws` checks exact kinds, positions and yaws for a single cell, a 3x1 run and an L.
  - `BarrierPiecesAreThePiecesOfAnInteriorWallBlockOnTheSameCells`
  - `BarriersFlushWithTheBoardEdgeOrAVoidCellWallEveryQuadrantOfTheirCells`
  - `BarrierOffTheFloorOrWithARepeatedIdIsRefused`
  - `LevelsWithoutBarriersComposeNothing`

  The PlayMode test `ClosedBarriersStandOnTheirCellsAndOpenOnesSinkUnderTheFloor` also passed: a closed 3x1 run at y 0 and an open L at y -0.8 with renderers off.

  The live probe found exactly one piece per quadrant on every barrier cell.

  The known gap is unchanged: a barrier two or more cells wide leaves its inner quadrants empty. It sits outside this scenario's given and is escalated.
- **AC-2: PASS.** These `EnvironmentBarrierViewTests` passed:
  - `AnimatedFlipReachesItsHeightWithinThreeTenthsOfASecond`
  - `SuspendPausesTheTweenAndTeardownStopsItAndReturnsThePieces`: `Tween.GetTweensCount(piece)` is 0 after Stop and after Clear, with 0 active pool items.
  - `SnapAndRestartReturnBarriersToTheirStartStateWithoutRebuilding`
  - `BarrierFlipsPauseAndStopAllocateNothing`, which uses Unity's `Is.Not.AllocatingGCMemory`.

  Code read of `EnvironmentView.cs` (17:59):
  - Every tween is `Tween.LocalPositionY` on the piece `Transform`.
  - The ID lookup is a `Dictionary` built in `Build`.
  - The sink's `OnComplete` uses a cached static delegate.
  - The success path has no LINQ, closures, concatenation or boxing.
- **AC-3: PASS.** No Environment or LevelPreview test failed:
  - Every `EnvironmentComposerTests` golden test and the 0 B recompose test passed.
  - The `EnvironmentViewLifecycleTests` (Restart no-op and pools) passed.
  - The full PlayMode suite passed 44/44.
  - The only EditMode failure was unrelated, and it passed on rerun.
- **AC-4: PASS.** Live check through the real owner handler, `LevelPreviewOwner.OnLevelPublished`, called by reflection. `LevelEditor.unity` was opened additively and closed without saving. It was never dirty, and no preview root was left. Probe: `verify/verifier2-ac4-probe.cs.txt`, on a 7 x 8 level with a Top door and one block.

| Draft | Read back | Capture |
|---|---|---|
| run (1-3,4) closed, L (5,4)(5,5)(4,5) open, single (1,1) closed | run: 12 pieces at y 0, 12 rendered, 12/12 quadrants. L: 12 pieces at y -0.8, 0 rendered, 12/12. Single: 4 pieces at y 0, 4 rendered, 4/4. | `verify/verifier2-ac4-preview-start.png` |
| run moved to (1-3,6) and opened, L closed | Old run cells: 0 pieces. Run: 12 at y -0.8, 0 rendered. L: 12 at y 0, 12 rendered. Single unchanged. | `verify/verifier2-ac4-preview-edited.png` |

### Files changed by this task (mtime 17:19 to 18:12, checked against every repo file modified since 17:15)

Every file below is inside the allowed paths:

- `Assets/ColorBlockJam/Environment/`:
  - `EnvironmentBarrier.cs` (+ `.meta`)
  - `EnvironmentBarrierComposer.cs` (+ `.meta`)
  - `EnvironmentComposer.cs`
  - `EnvironmentDatabase.cs`
  - `EnvironmentView.cs`
  - `EnvironmentPreview.cs`
  - `ColorBlockJam.Environment.asmdef`
- `Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset`
- `Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewHost.cs`: the look constants `k_lightIntensity`, `k_shadowStrength` and `k_farClipPlane` are still present.
- `Assets/ColorBlockJam/Tests/Environment/`:
  - `EditMode/EnvironmentBarrierComposerTests.cs` (+ `.meta`)
  - `PlayMode/EnvironmentBarrierViewTests.cs` (+ `.meta`)
  - `PlayMode/ColorBlockJam.Environment.PlayModeTests.asmdef`
- `Assets/ColorBlockJam/Tests/LevelPreview/LevelPreviewTests.cs`
- `docs/Environment-Layout.md`
- `docs/environment/barrier-preview-start.png`, `barrier-preview-edited.png` and `barrier-preview-edge-closed.png`
- `PROJECT.md`: the environment sentence and the DEC-121 row. Another session touched the file again at 18:34, alongside `docs/Gameplay-Drag-And-Gate.md`.
- This task folder.

Nothing is outside the allowed paths:

- The stray `InitTestScene` is gone from disk and from the AssetDatabase.
- The other files modified in the same window belong to other sessions or other test suites: the `Editor/Levels/**` and `Editor/BoardKit/**` files, the EditMode tests outside `Environment`/`LevelPreview`, `ProjectSettings/**`, and the Addressables and shape library assets.

### Open items

- The wide-barrier gap is escalated.
- The user still has to accept the look.
- Game scene wiring belongs to the gameplay Barrier task.
