# TASK-20260924-230915887-81e9732c-level-canvas-view — Level Editor canvas draws the level and selects blocks and doors

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-230915887-81e9732c-level-canvas-view
- Supersedes: none
- Title: Level Editor canvas draws the level and selects blocks and doors
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: UI
- Area: Level authoring (Level Editor window)
- Workflow: direct

## Objective and baseline

- Observable objective: The canvas under the level selection draws the selected draft level (floor, wall ring, doors, blocks with id labels, hover and selection), and a click with Select picks a block or door in sync with the Selected Block dropdown.
- Current behavior/evidence: Task 1 leaves an empty canvas slot. The reference template draws its level with IMGUI (`LevelEditorWindow.DrawLevel`); this project needs a UI Toolkit element.
- Expected behavior (design: `docs/Level-Editor-Design.md`, revision 2 with the user's decisions of 2026-09-25):
  1. `LevelCanvasModel`, `LevelCanvasGeometry`, `LevelCanvasHitTest` and `LevelCanvasHover` as plain C#, per design sections 3 and 5.
  2. `LevelCanvasElement` draws in one `generateVisualContent` pass with `Painter2D`, in the layer order of design section 3, with one Label per block; it repaints only on model, hover, selection, tool or geometry change.
  3. Select tool: left click selects a block or door; the selection syncs both ways with the Selected Block dropdown; corners hit-test as Outside.
- Behavior that must remain unchanged:
  - Every task 1 behavior
  - Draft data is unchanged by viewing

## Ownership

- Owner/writer role: environment/play-area session (this conversation; user decision 2026-09-25), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas.meta
  - Assets/ColorBlockJam/Editor/Levels/Canvas/**
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs.meta
  - production/tasks/TASK-20260924-230915887-81e9732c-level-canvas-view/**
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
  - Writing level JSON except through the existing Save Level path during manual checks
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked: At 01:55 on 2026-09-25 another live session (no task contract) edited `LevelDatabaseWindow.cs`, `Editor/Controls/ChoiceStepper.cs` and `Tests/EditMode/DatabaseControlsTests.cs`. The user decided: "bekle ve o bitince başla" (wait until it finishes, then start). The redesign tasks run one at a time, and each re-reads the window right before editing.
- Execution dependency: task 1 (level-editor-shell) closed
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given level 1, then the canvas shows 4x5 cells, the ring, both doors in their colours and two blocks labelled b1 and b2.
2. Given a resize of the window, then the board stays centred with a cell size of at least 18 px.
3. Given hover moves within one cell, then no repaint is requested (hover test).
4. Given a click on a block, then it is selected on the canvas and in the dropdown, and vice versa.

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

- Focused test path/command: `LevelCanvasModelTests`, `LevelCanvasHitTestTests`, `LevelCanvasHoverTests`, `LevelCanvasElementTests`
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console in the live Editor (MCP HTTP 127.0.0.1:8082)
- EditMode: the focused tests above, plus `DatabaseControlsTests`, `LevelDatabaseAddressablesTests`, `LevelDatabaseInspectorTests` and the LevelPreview tests stay green
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence:
  - A window capture at 960x640 and larger.
  - The gestures exercised in the live Editor, with the level JSON hash unchanged unless Save is part of the scenario.
  - User acceptance of the look and feel.
- Risk: low
- Approval already granted (exact target only): user decisions 2026-09-25 (layout, ownership after the other writer, arrows, undo, the "her şeyi sen yap" order) for the allowed paths above
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260924-230915887-81e9732c-level-canvas-view, then an independent verifier

## Stop conditions

- The other window writer is still active (the file changed within the last 10 minutes, or the user says so): wait, do not edit.
- A required change falls outside the allowed paths (for example Runtime `LevelData` serializability for v2): stop and record it for the owning task.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: TASK_CLOSED
- Contract fingerprint: sha256:d87e7f0e0f9be1a0f046f5f13e0bff174f59a4107cc334c563dd68d57453a8eb
- Evidence fingerprint: sha256:44ab1dc6a78a50699f1085a4d70f73beffdc0fdff067e153f69404876c0f256c
- Reviewed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:183b5149ddd357885874faf720e9948a68a1be41d2f6ab434897a977eebf15c1
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: none
- Closed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:183b5149ddd357885874faf720e9948a68a1be41d2f6ab434897a977eebf15c1
- Closed at: 2026-09-25T09:08Z
- Closure mode: automatic
- Closed by: implement-task (direct, empty ledger; independent verifier PASS)
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | The EditMode tests CanvasDrawsTheSelectedLevelWithOneLabelPerBlock and ModelMapsBlockCellsAndDoorSegments passed. A copy-only probe also ran against the real Le (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:183b5149ddd357885874faf720e9948a68a1be41d2f6ab434897a977eebf15c1 |
| AC-2 | PASS | ResizedWindowKeepsTheBoardCentredWithCellsOfAtLeastEighteenPixels, GeometryCentresTheBoardAndFitsItsRing and GeometryKeepsCellsAtLeastEighteenPixels passed. A c (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:183b5149ddd357885874faf720e9948a68a1be41d2f6ab434897a977eebf15c1 |
| AC-3 | PASS | LevelCanvasHoverTests (2) and PointerMovesInsideOneCellKeepTheSameHover passed. The probe sent real PointerMove events and counted generateVisualContent calls w (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:183b5149ddd357885874faf720e9948a68a1be41d2f6ab434897a977eebf15c1 |
| AC-4 | PASS | ClickedBlockAndDropdownSelectionStayInSync, ClickedDoorIsSelectedAndDisablesTheBlockTools, SelectionResolvesByUniqueIdThenIndexThenClears and RingCornersAndFarP (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:183b5149ddd357885874faf720e9948a68a1be41d2f6ab434897a977eebf15c1 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from docs/Level-Editor-Design.md task 2
- 2026-09-25T09:08Z — in_progress — task-chain workflow: implement, 3 reviewers (6 findings), fix, independent verify (tests in an isolated copy while MCP was down)
- 2026-09-25T09:08Z — TASK_CLOSED — all criteria PASS at the reviewed revision
<!-- TASK-LIFECYCLE:END -->
