# TASK-20260924-230915885-717889da-level-editor-shell — Level Editor two-pane shell: tools column, top selection bar, canvas slot, Save bottom-left

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-230915885-717889da-level-editor-shell
- Supersedes: TASK-20260924-224138113-766ebc15-level-editor-compact-actions
- Title: Level Editor two-pane shell: tools column, top selection bar, canvas slot, Save bottom-left
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: UI
- Area: Level authoring (Level Editor window)
- Workflow: direct

## Objective and baseline

- Observable objective: The Level Editor window is split into a left tools column and a right pane. The right pane holds the database and level selection bar above an (empty) canvas area, and Save Level sits alone at the bottom left with the status line.
- Current behavior/evidence: User plan 2026-09-25: "üst taraf level seçimi sol taraf toollar ve level seçiminin hemen altı ise çizim ekranı" (level selection on top, tools on the left, the drawing screen right below the level selection), plus the earlier requests to put Refresh on the JSON Source row, "+" and delete on the Target Level row, and only Save at the bottom left. The window is one vertical ScrollView today; "+"/"×" already sit in `ChoiceStepper` beside Target Level (another session, 01:55), and Reload JSON / Restore Previous Save are still full-width text buttons.
- Expected behavior (design: `docs/Level-Editor-Design.md`, revision 2 with the user's decisions of 2026-09-25):
  1. Layout per design section 1: a `TwoPaneSplitView` (fixed tools pane 280 px, min 240; right pane min 480) with the view-data key, `minSize` 960x640, three top-bar rows, icon buttons `refresh-json` and `restore-backup` on the JSON Source row (same handlers), Level Name after the stepper, and a bottom bar with Save Level and the status.
  2. Tools pane shell: Board group (read-only Level ID, delayed unbound Width/Height that refuse a shrink which would remove content, with a status message; Timer clamped 10-300 with an m:ss label), and the Blocks group holding the existing Selected Block/Piece Type/Rotate/Flip controls. Raw data (a collapsed Doors/Blocks PropertyField) and empty groups for Colors, Doors, Mechanics (disabled with the tooltip "Needs level JSON v2") and Edit are placeholders for tasks 2-5.
  3. Draft model per design section 4: `TrackSerializedObjectValue` change tracking, `Undo.undoRedoPerformed` (with the active-GUID check), `Undo.ClearUndo` before the draft is destroyed, history kept after Save of the same level, `hasUnsavedChanges`/`saveChangesMessage`/`SaveChanges()` in place of the warn-only `OnDisable`.
  4. Absorbs and supersedes the compact-actions task: Refresh and Restore become icon buttons, Save sits alone at the bottom left, and the "+"/"×" buttons are kept.
  5. PRD 11.1 layout text updated and one PROJECT.md decision row added.
- Behavior that must remain unchanged:
  - Save/Refresh/Restore/Create/Delete Level and Create/Delete Database semantics, dialogs, status texts, backups and the stale-file check
  - The `DraftLevelPublished` hook fires on every draft change, including undo
  - The ChoiceStepper test names (`LevelEditorPutsNewAndDeleteBesideTargetLevel`)

## Ownership

- Owner/writer role: environment/play-area session (this conversation; user decision 2026-09-25), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelDraft.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas.meta
  - Assets/ColorBlockJam/Editor/Levels/Canvas/**
  - Assets/ColorBlockJam/Tests/EditMode/DatabaseControlsTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs.meta
  - docs/Level-Editor-Design.md
  - docs/Color-Block-Jam-PRD.md
  - PROJECT.md
  - production/tasks/TASK-20260924-230915885-717889da-level-editor-shell/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Editor/BoardKit/**
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Assets/ColorBlockJam/Editor/Controls/** (used unchanged)
  - Writing level JSON except through the existing Save Level path during manual checks
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked: At 01:55 on 2026-09-25 another live session (no task contract) edited `LevelDatabaseWindow.cs`, `Editor/Controls/ChoiceStepper.cs` and `Tests/EditMode/DatabaseControlsTests.cs`. The user decided: "bekle ve o bitince başla" (wait until it finishes, then start). The redesign tasks run one at a time, and each re-reads the window right before editing.
- Execution dependency: the other live writer of `LevelDatabaseWindow.cs` has finished (file stable, or the user confirms); TASK-20260924-222616741-46522211-level-editor-scene-preview verifier handoff
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given the window opens at 960x640, then every row of design section 6 is present in its new home, nothing overflows, and the split position survives a reopen.
2. Given Refresh, Restore, "+", "×" and Save, then each behaves exactly as the old button did (prompts, dialogs, status, enable rules).
3. Given an edit to a bound field and then Ctrl+Z, then the draft, the dirty marker and `DraftLevelPublished` all update; Save keeps the history for the same level; a level switch clears it.
4. Given a dirty draft, when the window closes, then Unity's Save/Discard/Cancel prompt appears.
5. Given the existing EditMode tests, then all pass, with `DatabaseControlsTests` updated only for the 960 px width and the new root.

## References and constraints

- Story/GDD/ADR/design/log/image: `docs/Level-Editor-Design.md`; user messages and screenshots 2026-09-25 (two-part editor photo; Refresh/+/delete/Save placement); PRD 11.x Level Editor requirements; `Editor/BoardKit/PieceEditorWindow.cs` stroke/undo/shortcut patterns (copied, file untouched).
- Unity version/platform: 2022.3.62f2; Editor
- Package/architecture constraints:
  - UI Toolkit only (`TwoPaneSplitView`, `generateVisualContent` + `Painter2D`, pointer capture).
  - Plain C# models and commands with EditMode tests; thin VisualElements.
  - No new package and no new asmdef.
  - `ColorBlockJam.Editor` must not reference `ColorBlockJam.Editor.LevelPreview`.
  - No schedule loops: repaint only on change.
- Explicit out of scope: Play-from-editor, drag-reorder and duplicate, solver, editor stamp, and the Effects panel beyond Arrow/Ice (design section 6).

## Verification and risk

- Focused test path/command: `DatabaseControlsTests`, new `LevelEditorShellTests` (EditMode)
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console in the live Editor (MCP HTTP 127.0.0.1:8082)
- EditMode: the focused tests above, plus `DatabaseControlsTests`, `LevelDatabaseAddressablesTests`, `LevelDatabaseInspectorTests` and the LevelPreview tests stay green
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence:
  - A window capture at 960x640 and larger.
  - The gestures exercised in the live Editor, with the level JSON hash unchanged unless Save is part of the scenario.
  - User acceptance of the look and feel.
- Risk: medium (window restructuring)
- Approval already granted (exact target only): user decisions 2026-09-25 (layout, ownership after the other writer, arrows, undo, the "her şeyi sen yap" order) for the allowed paths above
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260924-230915885-717889da-level-editor-shell, then an independent verifier

## Stop conditions

- The other window writer is still active (the file changed within the last 10 minutes, or the user says so): wait, do not edit.
- A required change falls outside the allowed paths (for example Runtime `LevelData` serializability for v2): stop and record it for the owning task.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: TASK_CLOSED
- Contract fingerprint: sha256:d1170ea731c6090a44c2a5f1b8b7e3f4214785282e114c336f1e5fd825d9b3d1
- Evidence fingerprint: sha256:d07ab3131877c1c2e2636733284efd3284ab909ae97c572f06b25287a56937f7
- Reviewed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:173df4b39d3cf984b8e3f90004a84fe8f612edce9f7978cdc57e9f84a910a6c3
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: none
- Closed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:173df4b39d3cf984b8e3f90004a84fe8f612edce9f7978cdc57e9f84a910a6c3
- Closed at: 2026-09-25T09:08Z
- Closure mode: automatic
- Closed by: implement-task (direct, empty ledger; independent verifier PASS)
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | Isolated copy %TEMP%/CBJshellverify-5d19a4 (byte-identical to the repo; diff -rq empty), Unity 2022.3.62f2 batchmode EditMode: the layout tests ShellSplitsTools (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:173df4b39d3cf984b8e3f90004a84fe8f612edce9f7978cdc57e9f84a910a6c3 |
| AC-2 | PASS | Isolated copy only. My probe B_DirtyDraftPromptsBeforeRefreshRestorePlusDeleteAndSwitch made an unsaved edit, then clicked Refresh, Restore, + and × (Navigation (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:173df4b39d3cf984b8e3f90004a84fe8f612edce9f7978cdc57e9f84a910a6c3 |
| AC-3 | PASS | EditMode UnityTests UndoAndRedoOfABoardEditUpdateFieldsDirtyMarkerAndPreview (real Undo.PerformUndo/PerformRedo) and OneFieldCommitPublishesTheDraftOnce, plus t (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:173df4b39d3cf984b8e3f90004a84fe8f612edce9f7978cdc57e9f84a910a6c3 |
| AC-4 | PASS | LevelEditorShellTests.ClosingADirtyWindowGoesThroughUnitysSavePrompt drives Unity's internal ContainerWindow.InternalRequestClose in batch mode. The first verif (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:173df4b39d3cf984b8e3f90004a84fe8f612edce9f7978cdc57e9f84a910a6c3 |
| AC-5 | PASS | The whole EditMode platform in my isolated copy, and a diff of DatabaseControlsTests.cs against the pre-task copy (scratchpad DatabaseControlsTests.shell-before (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:173df4b39d3cf984b8e3f90004a84fe8f612edce9f7978cdc57e9f84a910a6c3 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's two-part editor plan and docs/Level-Editor-Design.md; supersedes the compact-actions task
- 2026-09-25T09:08Z — in_progress — task-chain workflow: implement, 3 reviewers (13 findings), fix, independent verify (tests in an isolated copy while MCP was down)
- 2026-09-25T09:08Z — TASK_CLOSED — all criteria PASS at the reviewed revision
<!-- TASK-LIFECYCLE:END -->
