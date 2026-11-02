# Evidence: TASK-20260924-230915885-717889da-level-editor-shell

Implementer run, 2026-09-25 07:28-08:00 (local). Base: `LevelDatabaseWindow.cs` last written 06:19 by the colour-ids task, stable for more than an hour before editing (md5 `4f816e2b…` before this task).

## Transport

- The live Editor's MCP bridge (HTTP 127.0.0.1:8082, instance `ColorBlockJamCase2026@99077e4df1ecfaa4`) was listed but did not answer from 07:28 to 07:59: every `mcpforunity://editor/state` read returned "ping not answered". Editor.log showed repeated `[WebSocket] Keep-alive failed … Connection closed`.
- Per the task's fallback rule (MCP unreachable for more than 5 minutes), all compilation and tests ran in an isolated copy: `%TEMP%\CBJshell-7a1c2e` (robocopy `/E /R:0 /W:0`, `Library\EditorInstance.json` removed), Unity 2022.3.62f2 `-batchmode -runTests -logFile <copy>\…`. The live Editor, its scenes and its undo stack were not touched.
- No live-Editor evidence exists yet: the window capture, manual gestures and the Save/Discard/Cancel prompt check stay open (see "Open").

## Files

- Changed: `Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs`, `Assets/ColorBlockJam/Tests/EditMode/DatabaseControlsTests.cs`, `docs/Level-Editor-Design.md`, `docs/Color-Block-Jam-PRD.md` (11.1), `PROJECT.md` (DEC-119 row).
- New: `Assets/ColorBlockJam/Editor/Levels/Canvas.meta`, `Canvas/LevelBoardResize.cs(.meta)`, `Assets/ColorBlockJam/Tests/EditMode/LevelEditorShellTests.cs(.meta)`. The `.meta` GUIDs came from the copy's import and were completed by hand to the standard importer layout.
- Unchanged and still used: `Editor/Controls/ChoiceStepper.cs`, `DatabaseActionsRow.cs`, `FieldActionsRow.cs`, `LevelDraft.cs`, `DraftLevelPublished`/`LastPublishedLevel`/`SchedulePublish`/`PublishDraft`.

## Compile

- Copy compile of all assemblies: 0 errors. The only compiler warning in the log is the existing `VacuumMotionAssets.cs(54,13) CS0618` (not this task).
- One intermediate test compile error was fixed: `EditorWindow.hasUnsavedChanges` has a protected setter, so the test teardown uses the public `DiscardChanges()`.

## Tests (isolated copy, EditMode)

| Run | Filter | Result |
|---|---|---|
| `shell-results.xml` | LevelEditorShellTests, DatabaseControlsTests, LevelCreateColourTests | 23/23 passed |
| `regress-results.xml` | assemblies `ColorBlockJam.Editor.EditModeTests;ColorBlockJam.Editor.LevelPreview.Tests` | 122/123: the undo test waited 30 frames for a redo publish |
| `regress3-results.xml` (diagnostic copy) | same | 123/123; the redo publish came after 371 frames. `delayCall` runs with the inspector update (about 10 Hz), so the test now waits up to 5 s of editor time instead of a frame count |
| `final2-results.xml` (final sources, byte-identical to the repo) | same two assemblies | **124/124 passed**, 0 failed, 0 skipped, 0 inconclusive (36.7 s) |

Per class in the final run: LevelEditorShellTests 12, DatabaseControlsTests 9 (includes `LevelEditorPutsNewAndDeleteBesideTargetLevel` and `LevelEditorRowsFitTheWindowWidth` at 960 px), LevelDatabaseAddressablesTests 6, LevelDatabaseInspectorTests 4, LevelPreviewTests 9, LevelCreateColourTests 3; all passed.

## Layout (isolated copy, `layout-probe.txt`)

Window built at 960 x 640 and 1280 x 800 (CreateGUI + ValidateLayout):
- 960 x 640: tools pane x 0-280, selection pane from x 281 (680 wide), top bar 92 px tall, canvas slot 680 x 518 under it, bottom bar full width at y 610 with Save Level at x 9 and the status at x 120. `refresh-json`/`restore-backup` at x 896/926 on the JSON Source row; `create-level`/`delete-level` at x 627/657 on the Target Level row; Level Name after them.
- 1280 x 800: the same structure; the extra width goes to the selection pane and canvas (1000 x 678).
- The flexed pane ends 1 px past the window edge (TwoPaneSplitView's drag line); the top bar's 8 px padding keeps every control inside. `LevelEditorRowsFitTheWindowWidth` walks the tools pane (depth 3) and both bars and finds no overflow.

## Splitter persistence

- `TwoPaneSplitView` in 2022.3 serializes `m_FixedPaneDimension` and overrides `OnViewDataReady`. A batch-mode close/reopen probe still came back at 280 after the width was set to 350, so the design's fallback applies: the width is stored in `EditorPrefs` (`ColorBlockJam.LevelEditor.ToolsWidth`), and `CreateGUI` starts the split at that width. The view-data key is kept.
- Superseded by the review fixes below: the width is no longer stored from `GeometryChangedEvent`, only from the pointer release on the splitter handle.

## Acceptance mapping

- AC-1 (layout at 960 x 640, rows in new homes, split persists): `ShellSplitsToolsFromTheSelectionBarAndCanvas`, `TopBarRowsHoldTheDatabaseSourceAndLevelControls`, `ToolsColumnHoldsBoardBlocksRawDataAndPlaceholderGroups`, `LevelEditorRowsFitTheWindowWidth`, `ReopenedWindowRestoresTheDraggedToolsWidth`, `ForcedToolsWidthDoesNotOverwriteTheSavedWidth` and the layout probe. The live capture is still open.
- AC-2 (Refresh/Restore/+/×/Save behave as before): the handlers, dialogs, status texts and enable rules are unchanged (`UpdateLevelChoices` holds the old enable block). Save now keeps the draft: it refreshes the level choices, resets the saved baseline and does not rebuild the draft. Manual live gestures are still open.
- AC-3 (edit + Ctrl+Z updates the draft, dirty marker and publish; Save keeps history; level switch clears): `UndoAndRedoOfABoardEditUpdateFieldsDirtyMarkerAndPreview` (a real `Undo.PerformUndo/PerformRedo` on the draft; fields, `hasUnsavedChanges` and `DraftLevelPublished` follow). `TimerCommitClampsAndShowsMinutes` and `WidthCommitFollowsTheResizeRule` return the dirty flag to false when the saved value comes back. Save-keeps-history and the database-switch undo are verified by code review only, because both need a JSON/asset write.
- AC-4 (dirty close prompts Save/Discard/Cancel): `hasUnsavedChanges` and `saveChangesMessage` are set, and the `SaveChanges()` override calls `SaveLevel`. Unity's built-in prompt needs a live, interactive check.
- AC-5 (existing tests green, DatabaseControlsTests changed only for 960 px and the new root): 124/124. The diff of `DatabaseControlsTests` touches only `k_windowWidth` (700 to 960) and `AssertRowsFit` (scroll depth parameter plus the `top-bar`/`bottom-bar` roots).
- D-001 related observation (fields at 0 after a domain reload): Level Name, Width, Height and Timer are unbound and filled from the draft on every load and change. `BoardFieldsShowTheDraftAfterTheGuiIsRebuilt` runs OnDisable, OnEnable and CreateGUI and checks all six values.

## Data safety

- `Assets/ColorBlockJam/Data/Levels/LevelDatabase.json` sha256 `02c013a54e180f9570daf142505e018e28564ca4f7ab91bad847b35d8f3ecd6e` at 07:59; nothing in this task wrote it. The tests never save.
- No scene, prefab, ScriptableObject, package or project setting changed. No Plastic operation.

## Review fixes (2026-09-25 08:28-08:50)

The live MCP bridge still answered "ping not answered" at 08:30 and 08:50, so this round also ran in the isolated copy `%TEMP%\CBJshell-7a1c2e`, re-mirrored from the repo (`robocopy /MIR /R:0 /W:0` of Assets, Packages, ProjectSettings) before the first run. Temporary probe tests lived only in the copy and were deleted before the final run.

| Finding | Verdict | Change |
|---|---|---|
| Status stays "Unsaved…" after undo to the saved state | Real | `UpdateDirtyStatus` restores "Editing {name}. Save Level writes the JSON source." when the draft is clean and the status still shows the unsaved text (other messages stay). Asserted in the undo test and `TimerCommitClampsAndShowsMinutes`. |
| One commit publishes twice (two findings) | Real | The `TrackSerializedObjectValue` callback runs `OnDraftChanged` only when the draft differs from the last sync (`IsDraftOutOfSync`, also used by `OnUndoRedo`). New `OneFieldCommitPublishesTheDraftOnce` runs the pending publish before the next binding tick; with the guard removed in the copy it failed "Expected: 1 But was: 2", with the guard it passes. |
| Piece Editor rows checked at 960 px (two findings) | Real | `OpenWithGui`/`AssertRowsFit` take the width: Piece Editor 700 px (as before this task), Level Editor 960 px. |
| Splitter test did not test the save | Real | `ReopenedWindowRestoresTheDraggedToolsWidth` drags the splitter handle with synthesized pointer events (300 to 340 px), checks the preference, closes, reopens and finds the tools pane at 340. Before the handler fix it failed at 300. |
| `OnUndoRedo` drops a dirty draft after an outside database switch | Real | With unsaved edits the draft stays and the status asks for Reload JSON (which offers Save/Discard/Cancel); a clean draft reloads as before. New `UndoAfterAnOutsideDatabaseSwitchKeepsUnsavedEdits` (simulates the switch through `m_loadedGuid`; no asset write). |
| Forced or per-move widths overwrite the saved splitter width | Real | The width is stored only on `PointerUpEvent` of the split's `unity-dragline-anchor` handle, which holds the pointer capture during a drag (a probe showed captured events never trickle through the split). New `ForcedToolsWidthDoesNotOverwriteTheSavedWidth`. Tests save and restore the user's preference (read as 280 before the runs). |
| Test publishes leak into `LastPublishedLevel`/the scene preview | Real | Both publishing UnityTests close their window and republish the previous `LastPublishedLevel` in `finally` (test-only reflection; no production hook). |
| Stepper children not walked (depth 2) | Real | The bars are walked at depth 3, which reaches the Target Level arrows and `+`/`×`. |
| `k_minWindowSize` naming | Real | Replaced by `k_minWindowWidth`/`k_minWindowHeight` consts. |
| PRD AC-7.13, 11.4, V-M30 name the removed "Database & Level" panel | Real | Reworded to the level selection bar plus the bottom-bar Save Level; requirements unchanged. |
| Design §1/§4 deviations unrecorded | Real | Added a "Deviations" bullet to the task-1 As built note (status texts, `minSize` in `OnEnable`, `FindTexture`, drag-end width, publish guard, outside switch). |

Files touched in this round: `Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs` (md5 `ce370e00…`), `Assets/ColorBlockJam/Tests/EditMode/LevelEditorShellTests.cs` (`2176a4b7…`), `Assets/ColorBlockJam/Tests/EditMode/DatabaseControlsTests.cs` (`0dcd751c…`), `docs/Color-Block-Jam-PRD.md`, `docs/Level-Editor-Design.md`, this file.

Results (isolated copy, sources byte-identical to the repo):
- `fix2-results.xml` focused LevelEditorShellTests + DatabaseControlsTests: 24/24 before the deterministic publish test was added.
- `fixfinal-results.xml`, assemblies `ColorBlockJam.Editor.EditModeTests;ColorBlockJam.Editor.LevelPreview.Tests`: **127/127 passed**, 0 failed/skipped/inconclusive (41.3 s). LevelEditorShellTests 15, DatabaseControlsTests 9, LevelDatabaseAddressablesTests 6, LevelDatabaseInspectorTests 4, LevelPreviewTests 9, all others green.
- Compile: 0 errors; no new compiler warnings (only the existing `VacuumMotionAssets.cs` CS0618).
- `LevelDatabase.json` sha256 still `02c013a5…6ecd6e`.

## Open

- Live Editor: once the MCP bridge answers, run `refresh_unity` and a clean Console check, then run LevelEditorShellTests, DatabaseControlsTests, LevelDatabaseAddressablesTests, LevelDatabaseInspectorTests and the LevelPreview tests. Also take a window capture at 960 x 640 and at a larger size, and exercise Refresh/Restore/+/×/Save with the JSON hash checked.
- Interactive: close a dirty window to see Unity's Save/Discard/Cancel prompt, and check the tab asterisk (user acceptance of the look and feel).

## Independent verification (2026-09-25 08:49-09:01)

Verifier only: no production code, test, scene, asset or setting was edited in the repository. The live Editor, its scenes and its undo stack were not touched.

### Transport

- The MCP HTTP bridge (127.0.0.1:8082) still lists `ColorBlockJamCase2026@99077e4df1ecfaa4`, but `mcpforunity://editor/state` answered "ping not answered" from a fresh MCP session at every poll from 08:50 to 08:57 (more than 5 minutes). Editor.log keeps repeating `[WebSocket] Keep-alive failed` / `Connection closed`. The live Editor process responds, but its `Library/ScriptAssemblies/ColorBlockJam.Editor.dll` dates from 04:34, so it has not compiled this task's code yet.
- Fallback per the task rules: own isolated copy `%TEMP%\CBJshellverify-5d19a4` (robocopy `/E /R:0 /W:0`, Library included, `Library/EditorInstance.json` removed), Unity 2022.3.62f2 `-batchmode -runTests` with its own `-logFile`. Before the runs, the copy's `LevelDatabaseWindow.cs` (`ce370e00…`), `LevelEditorShellTests.cs` (`2176a4b7…`), `DatabaseControlsTests.cs` (`0dcd751c…`), `LevelBoardResize.cs` and `LevelDatabase.json` matched the repository byte for byte.

### Files changed by this task (repository, modified since 07:25)

All within the contract's allowed paths: `Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs`, `Editor/Levels/Canvas.meta`, `Editor/Levels/Canvas/LevelBoardResize.cs(.meta)`, `Tests/EditMode/DatabaseControlsTests.cs`, `Tests/EditMode/LevelEditorShellTests.cs(.meta)`, `docs/Level-Editor-Design.md`, `docs/Color-Block-Jam-PRD.md`, `PROJECT.md` (DEC-119 row), and this task folder. No forbidden path changed. The three new `.meta` GUIDs are unique in `Assets`/`Packages`. The other files modified in this window belong to the colour-ids task folder (another task). `Editor/Controls/**` is unchanged.

### Tests (isolated copy)

- Whole EditMode platform (`verifier-editmode-all-results.xml`): **402/402 passed**, 0 failed/skipped/inconclusive (43.4 s). LevelEditorShellTests 15, DatabaseControlsTests 9, LevelDatabaseAddressablesTests 6, LevelDatabaseInspectorTests 4, LevelPreviewTests 9, LevelCreateColourTests 3. Compile: 0 errors. The only compiler warning is the existing `VacuumMotionAssets.cs(54,13) CS0618`.
- `DatabaseControlsTests` diff against the pre-task copy (`LevelDatabaseWindow.shell-before.cs`/`DatabaseControlsTests.shell-before.cs` in the session scratchpad): only the 960 px Level Editor width, the width/depth parameters and the `top-bar`/`bottom-bar` roots. `LevelEditorPutsNewAndDeleteBesideTargetLevel` is kept and passes.
- A verifier-only probe (`VerifierShellProbeTests.cs.txt`, which existed only in the copy; results in `verifier-probe-results.xml`, 2/2 passed) ran after the suite, starting from the repository `LevelDatabase.json`:
  - Layout (`verifier-layout-probe.txt`) at 960 x 640, 1280 x 800 and 1600 x 1000: all 28 named elements are present and inside the window. The tools pane is at x 0-280. The top bar's three rows (database row with `refresh-json`/`restore-backup` icon buttons, database actions, Target Level stepper with `+`/`×`, then Level Name) sit above `canvas-slot`, and the bottom bar is full width with Save Level at x 9 and the status after it.
  - Gestures (`verifier-gestures-probe.txt`; buttons were clicked through `NavigationSubmitEvent`):
    - Refresh with a clean draft: the status is "Reloaded LevelDatabase.json from disk." and the JSON hash is unchanged.
    - `+`: 5 to 6 levels, "Created a valid starter level in LevelDatabase.json.", and `level-006` is selected.
    - Edit then Save: "Saved Level 6 to LevelDatabase.json." and the draft is clean.
    - **Undo after Save** brings back the pre-save timer (90 to 60) and the draft is dirty again, so the history survives Save. Redo makes it clean again.
    - `SaveChanges()` (the Save branch of Unity's close prompt) writes the level and clears `hasUnsavedChanges`. `saveChangesMessage` is set.
    - Restore: "Restored the previous saved level JSON. The replaced version is now the backup."
    - **Level switch** (6 to 1), then Undo: the new draft is unchanged and clean, so the previous level's history was cleared.
    - `×`: shows the "Delete Level / Delete Level 6 from this database?" dialog. Batch mode cancels every dialog, so nothing is deleted and the hash is unchanged.
    - All of these writes happened only in the copy.
- Handler bodies are byte-identical to the pre-task window: `CreateDatabase`, `DeleteDatabase`, `CreateLevel`, `DeleteLevel`, `RestorePreviousSave`, `ReloadDatabase`, `AskToLeaveDraft` and `SaveLevels`. The enable rules in `UpdateLevelChoices` are identical too. The status/dialog strings differ from before only in three ways: the warn-only close dialog was removed, as the contract replaces it; there is one new outside-switch status; and there is the resize refusal message.

### Data safety

- Repository `Assets/ColorBlockJam/Data/Levels/LevelDatabase.json` sha256 is `02c013a54e180f9570daf142505e018e28564ca4f7ab91bad847b35d8f3ecd6e` before and after verification.

### Still unproven (live Editor or user)

- Live `refresh_unity`, a clean live Console, and live `run_tests`: the bridge is down, and the live Editor has not compiled this code.
- A window **capture** at 960 x 640 and larger. Only the rect probe exists; no pixels were captured.
- Unity's interactive Save/Discard/Cancel prompt on closing a dirty window, and the tab's unsaved marker. Only the `hasUnsavedChanges`/`saveChangesMessage`/`SaveChanges()` wiring is proven.
- The `×` confirm-and-delete path in a real dialog, and the dirty-draft Save/Discard/Cancel prompt of Refresh/Restore/+/×. That code is unchanged, but batch mode cancels dialogs.
- User acceptance of the look and feel.
- Design note: §6 lists "Blocks (+ Flip V)", but the contract asks only for the existing Rotate/Flip. Flip V is not present, which is not a contract defect.

## Implementer follow-up to the verifier (2026-09-25 09:02-09:14)

The live bridge still answered "ping not answered" at 09:02 and 09:13 (instance listed, reconnected 09:02), so this round again used the isolated copy `%TEMP%\CBJshell-7a1c2e`. Before the run its `Assets`, `Packages` and `ProjectSettings` matched the repository (`diff -rq` empty, `LevelDatabase.json` sha256 `02c013a5…6ecd6e`). Copy-only probes were deleted before the final run; their source is archived in `ImplementerCloseAndCaptureProbes.cs.txt`.

### AC-4: Unity's own close path, now automated

- Probe (`close-prompt-probe.txt`), utility and docked windows: after a timer edit, Unity's `ContainerWindow` reports `hasUnsavedChanges = True`, lists the window in `m_UnsavedEditorWindows`, and shows the unsaved marker in `displayedTitle` (`…LevelDatabaseWindow*` for the utility window, `*` for the docked tab). Unity's close request `ContainerWindow.InternalRequestClose(window)` raised `DisplayDialogComplex` "Unsaved Changes Detected" with this window's `saveChangesMessage` ("The selected level has unsaved changes. Save them to the JSON source?"). Batch mode answers Cancel, so the request returned False and the window stayed open and dirty. After `DiscardChanges()` the marker cleared and the request returned True with no prompt. The Save answer was already covered by the verifier's `SaveChanges()` probe.
- New permanent test `LevelEditorShellTests.ClosingADirtyWindowGoesThroughUnitysSavePrompt` asserts the host's unsaved state and title asterisk, and, in batch mode only, the prompt text (`LogAssert.Expect`), Cancel keeping the edit, and the clean close with no prompt. In the interactive Editor it skips the dirty close request, because that call would open the modal prompt and block the run. It never saves. Test file md5 `f1c2b463…`; production code unchanged (`LevelDatabaseWindow.cs` still `ce370e00…`).
- Still for the user: seeing the prompt in the interactive Editor. The unchanged Unity path is the one the test drives.

### AC-1: pixel captures

- `capture-960x640-window.png` and `capture-1280x800-window.png` came from Unity's own `GrabPixels` of the window's host view in the copy (display scale 1.5, so the files are 1440 x 960 and 1920 x 1200; flipped upright). They show the tools column (Colors, Blocks with Selected Block/Piece Type/Rotate 90°/Flip Horizontal, Doors, Board with Level ID/Width/Height/Timer 300 and 5:00, disabled Mechanics, Edit, collapsed Raw data), the three top-bar rows (Level Database plus JSON Source with the refresh and restore icons; Database Name with Create/Delete Database; Target Level with ▲ ▼ + × and then Level Name), the empty canvas slot, and Save Level bottom left with the status. Batch mode draws the light skin. Nothing overflows at either size.
- Still for the user: acceptance of the look and feel in the live Editor.

### Tests (isolated copy, whole EditMode platform)

- `implementer-ac4-editmode-all-results.xml`: **403/403 passed**, 0 failed/skipped/inconclusive (44.7 s). LevelEditorShellTests 16 (the new test passed), DatabaseControlsTests 9, LevelDatabaseAddressablesTests 6, LevelDatabaseInspectorTests 4, LevelPreviewTests 9, LevelCreateColourTests 3. 0 compile errors.
- `LevelDatabase.json` sha256 unchanged (`02c013a5…6ecd6e`) in the repository and in the copy.

### Still open

- Live `refresh_unity`, a clean live Console and live `run_tests` once the bridge answers. The live Editor has not compiled this task's code yet.
- The real-dialog answers (× OK, dirty-draft prompts of Refresh/Restore/+/×). That code is byte-identical to before the task.
- User acceptance of the look and feel.

## Second independent verification (2026-09-25 09:14-09:26)

Verifier only. No production code, test, scene, asset, setting or EditorPrefs value in the repository was edited, and the live Editor was not touched. Only these files in this task folder were added: `verifier2-*` and `Verifier2ReloadAndPromptProbe.cs.txt`.

### Transport

- The MCP HTTP bridge (127.0.0.1:8082) lists `ColorBlockJamCase2026@99077e4df1ecfaa4` (`connected_at` 06:14 UTC). From a fresh MCP session, `mcpforunity://editor/state` answered "ping not answered" at every poll between 09:14 and 09:25, more than 5 minutes. Editor.log still shows the MCP WebSocket send failures. The live `Library/ScriptAssemblies/ColorBlockJam.Editor.dll` still dates from 04:34, so the live Editor has not compiled this task's code.
- Fallback: the verifier copy `%TEMP%\CBJshellverify-5d19a4`, re-mirrored with `robocopy /MIR /R:0 /W:0` (Assets, Packages, ProjectSettings; `Library/EditorInstance.json` removed). A `diff -rq` against the repo came back empty. Hashes: `LevelDatabaseWindow.cs` md5 `ce370e00…`, `LevelEditorShellTests.cs` `f1c2b463…`, `DatabaseControlsTests.cs` `0dcd751c…`. Unity 2022.3.62f2 ran with `-batchmode -runTests` and its own `-logFile`.

### Files changed by this task

All changed files are inside the allowed paths:
- `Editor/Levels/LevelDatabaseWindow.cs`
- `Editor/Levels/Canvas.meta`
- `Editor/Levels/Canvas/LevelBoardResize.cs` (and `.meta`)
- `Tests/EditMode/DatabaseControlsTests.cs`
- `Tests/EditMode/LevelEditorShellTests.cs` (and `.meta`)
- `docs/Level-Editor-Design.md`
- `docs/Color-Block-Jam-PRD.md`
- `PROJECT.md` (DEC-119, no collision)
- this task folder

Checked by mtime since 07:20. `Editor/Controls/**` is unchanged (last written 02:15), and so is `LevelDraft.cs`. No forbidden path changed. The only other file newer than 07:20 is in the colour-ids task folder (another task).

### Tests (isolated copy)

- Whole EditMode platform (`verifier2-editmode-all-results.xml`): **403 of 403 passed**, 0 failed, skipped or inconclusive (42.2 s). By class: LevelEditorShellTests 16 (including `ClosingADirtyWindowGoesThroughUnitysSavePrompt`), DatabaseControlsTests 9, LevelDatabaseAddressablesTests 6, LevelDatabaseInspectorTests 4, LevelPreviewTests 9, LevelCreateColourTests 3. Compile: 0 errors, and no `error CS` in the log.
- `DatabaseControlsTests` diff against the pre-task copy: only the 960 px Level Editor width (`k_levelEditorWidth`), the width and depth parameters, and the `top-bar`/`bottom-bar` roots. The Piece Editor is still checked at 700 px.
- New verifier-only probe (`Verifier2ReloadAndPromptProbe.cs.txt`; it lived only in the copy and was deleted after the run). Results are in `verifier2-probe-results.xml` (2 of 2 passed) and `verifier2-probe-output.txt`:
  - **Real domain reload (D-001 related observation).** The window was opened and built by Unity, then the test called `EditorUtility.RequestScriptReload()` and yielded `WaitForDomainReload` (the log shows "Reloading assemblies due to reload request"). It then waited for the recreated window's GUI plus 0.5 s. Before and after the reload the fields showed the same values: `level-01` / Level 1 / 4 x 5 / 300 / 5:00, and the window stayed clean. Nothing read 0.
  - **Dirty-draft prompts (AC-2).** With an unsaved timer edit, Refresh, Restore, `+` and `×` each raised `DisplayDialogComplex` "Unsaved Level / Save this level before leaving it?". Batch mode answers Cancel, and after each one the edit, the dirty flag, the level count (5) and the JSON sha were unchanged. A Target Level switch raised the same prompt, and Cancel kept the selected level.
- `LevelDatabase.json` sha256 is `02c013a54e180f9570daf142505e018e28564ca4f7ab91bad847b35d8f3ecd6e` in the repo before and after this verification, and in the copy after the probe.

### Code review notes

- `CreateGUI` → `RefreshLevelSelection` → `LoadSelectedDraft` → `OnDraftChanged` → `SyncDraftFields` fills the unbound Name, Width, Height, Timer and ID fields directly, so they do not depend on a binding tick after a reload.
- `DestroyDraft` calls `Undo.ClearUndo(m_draft)` before `DestroyImmediate`. `SaveLevel` keeps the draft and resets only `m_savedDraftJson`.
- `OnDisable` unsubscribes `undoRedoPerformed` and any pending `delayCall`. The tracker is unbound on the next load.
- The new window strings still use interpolation, as the window did before this task. PROJECT.md Rule 8 prefers ZString for editor text. This is a style note only; no acceptance criterion depends on it.

### Criteria verdict (this verifier)

- AC-1: PASS in the isolated copy. Layout tests, the 960 x 640 and 1280 x 800 `GrabPixels` captures (reviewed: nothing overflows, and Save Level is bottom left) and the drag/reopen splitter test all pass. Still open: a capture in the live Editor, which is blocked by the bridge.
- AC-2: PASS in the isolated copy. The clean-draft gestures come from the first verifier probe; the dirty-draft prompts for all five controls come from this probe. Still open: live gestures and the real OK answers.
- AC-3: PASS. The undo/redo UnityTest, the one-publish test, and undo-after-Save and level-switch-clears-history from the first verifier probe.
- AC-4: PASS through Unity's own `ContainerWindow.InternalRequestClose` path in batch mode (prompt text, Cancel keeps the window, and the `SaveChanges()` Save branch). Still open: seeing the modal in the interactive Editor.
- AC-5: PASS. 403 of 403, with the DatabaseControlsTests diff limited as the contract allows.
- Still required by the contract and not possible here: the live `refresh_unity`, a clean live Console, live `run_tests`, a live window capture, live gestures, and the user's acceptance of the look and feel.
