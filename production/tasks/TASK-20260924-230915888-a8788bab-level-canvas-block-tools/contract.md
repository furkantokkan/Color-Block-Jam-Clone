# TASK-20260924-230915888-a8788bab-level-canvas-block-tools — Level Editor canvas: place, move, recolour, rotate and remove blocks

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-230915888-a8788bab-level-canvas-block-tools
- Supersedes: none
- Title: Level Editor canvas: place, move, recolour, rotate and remove blocks
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: feature
- Area: Level authoring (Level Editor window)
- Workflow: direct

## Objective and baseline

- Observable objective: A designer places blocks by picking a colour and a shape in the tools column and clicking the canvas. Blocks are moved by drag or nudge, recoloured, rotated or flipped, and removed with a right click or the eraser, each as one undo step.
- Current behavior/evidence: After task 2 the canvas is view-only; blocks can be edited only through dropdowns and Rotate/Flip buttons.
- Expected behavior (design: `docs/Level-Editor-Design.md`, revision 2 with the user's decisions of 2026-09-25):
  1. `LevelEditCommands` (block commands), `LevelRecordCopy` and `LevelDraft.Replace` per design sections 4 and 5; each gesture is one undo group recorded on the draft.
  2. Colors group (11 swatches, `[`/`]`), Blocks group (shape list from `BlockShapeLibrary` that tolerates library errors, ghost placement, `R` to rotate the ghost), Edit group (Select, Eraser, Undo, Redo, four nudge buttons plus the arrow keys, Delete).
  3. Rotate, Flip H, the new Flip V and Piece Type go through `TryReplaceBlockCells` refusal; right click removes.
- Behavior that must remain unchanged:
  - Every task 1-2 behavior
  - Block IDs are unique on create (next free b{n})

## Ownership

- Owner/writer role: environment/play-area session (this conversation; user decision 2026-09-25), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelDraft.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas.meta
  - Assets/ColorBlockJam/Editor/Levels/Canvas/**
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs.meta
  - production/tasks/TASK-20260924-230915888-a8788bab-level-canvas-block-tools/**
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
- Execution dependency: task 2 (level-canvas-view) closed
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given a shape and colour, when a valid cell is clicked, then one block is added and `DraftLevelPublished` fires once; an invalid cell is refused with a message naming the cell.
2. Given a drag to an invalid place, then the block snaps back; a valid drop moves the origin; Ctrl+Z restores the origin, the canvas and the dirty state.
3. Given each refusal case (off-board, overlap), then the command test rejects it with its message.
4. Given right click or the eraser on a block, then it is removed as one undo step.

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

- Focused test path/command: `LevelEditCommandTests`, `LevelRecordCopyTests`
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console in the live Editor (MCP HTTP 127.0.0.1:8082)
- EditMode: the focused tests above, plus `DatabaseControlsTests`, `LevelDatabaseAddressablesTests`, `LevelDatabaseInspectorTests` and the LevelPreview tests stay green
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence:
  - A window capture at 960x640 and larger.
  - The gestures exercised in the live Editor, with the level JSON hash unchanged unless Save is part of the scenario.
  - User acceptance of the look and feel.
- Risk: medium
- Approval already granted (exact target only): user decisions 2026-09-25 (layout, ownership after the other writer, arrows, undo, the "her şeyi sen yap" order) for the allowed paths above
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260924-230915888-a8788bab-level-canvas-block-tools, then an independent verifier

## Stop conditions

- The other window writer is still active (the file changed within the last 10 minutes, or the user says so): wait, do not edit.
- A required change falls outside the allowed paths (for example Runtime `LevelData` serializability for v2): stop and record it for the owning task.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: TASK_CLOSED
- Contract fingerprint: sha256:488c0619795c3b3f2ee745bf661209ffad5a05a955fed40564125ca10fe5d825
- Evidence fingerprint: sha256:570dc45c2249118b4a60dc35ad9788f14a5c125c47e9050257156b3eddf0ddcc
- Reviewed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:411c7fe06fd5b9fd06dfb091be692575e8dab60167ad2aa01957fdbb98dd74c0
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: none
- Closed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:411c7fe06fd5b9fd06dfb091be692575e8dab60167ad2aa01957fdbb98dd74c0
- Closed at: 2026-09-25T09:08Z
- Closure mode: automatic
- Closed by: implement-task (direct, empty ledger; independent verifier PASS)
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | Live MCP run_tests job 514f43e90ef14807b69f71f156e2f847, test LevelCanvasBlockToolsTests.PlacingAShapeAddsOneBlockAndPublishesItOnce passed. A separate live-Edi (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:411c7fe06fd5b9fd06dfb091be692575e8dab60167ad2aa01957fdbb98dd74c0 |
| AC-2 | PASS | Live test DraggingABlockMovesItAndUndoRestoresOriginCanvasAndDirtyState passed. The live probe dragged a block with real pointer events and pressed Ctrl+Z on th (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:411c7fe06fd5b9fd06dfb091be692575e8dab60167ad2aa01957fdbb98dd74c0 |
| AC-3 | PASS | Live run of the LevelEditCommandTests class: 13/13 passed. (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:411c7fe06fd5b9fd06dfb091be692575e8dab60167ad2aa01957fdbb98dd74c0 |
| AC-4 | PASS | Live test RightClickAndEraserRemoveABlockAsOneUndoStep passed. The live probe also removed blocks with a right click and with the Eraser, then undid each with C (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:411c7fe06fd5b9fd06dfb091be692575e8dab60167ad2aa01957fdbb98dd74c0 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from docs/Level-Editor-Design.md task 3
- 2026-09-25T09:08Z — in_progress — task-chain workflow: implement, 3 reviewers (6 findings), fix, independent verify (tests in an isolated copy while MCP was down)
- 2026-09-25T09:08Z — TASK_CLOSED — all criteria PASS at the reviewed revision
<!-- TASK-LIFECYCLE:END -->
