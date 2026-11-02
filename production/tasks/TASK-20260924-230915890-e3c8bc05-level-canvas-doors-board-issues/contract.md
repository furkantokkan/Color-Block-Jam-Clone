# TASK-20260924-230915890-e3c8bc05-level-canvas-doors-board-issues — Level Editor canvas: doors, board resize and an issues list

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-230915890-e3c8bc05-level-canvas-doors-board-issues
- Supersedes: none
- Title: Level Editor canvas: doors, board resize and an issues list
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: feature
- Area: Level authoring (Level Editor window)
- Workflow: direct

## Objective and baseline

- Observable objective: Doors are painted on the board edge with a colour (click or drag extends a same-colour door), resizing shows what would be removed and asks first, and an issues list names each validation problem and highlights it on the canvas.
- Current behavior/evidence: After task 3, doors are edited only through the Raw data foldout, a shrinking resize is refused, and validation shows only the first error in the status.
- Expected behavior (design: `docs/Level-Editor-Design.md`, revision 2 with the user's decisions of 2026-09-25):
  1. Door tool per design section 2 (place, extend, recolour, right-click remove, ghost on ring segments).
  2. `TryResizeBoard` with a hatch preview and a dialog that states the counts ("Remove 2 blocks and 1 door?"); Confirm is one undo step.
  3. `LevelDraftIssues`, with an agreement test against `LevelData.Validate`, and a `LevelEditorIssuesList` whose click highlights the issue on the canvas.
- Behavior that must remain unchanged:
  - Every task 1-3 behavior

## Ownership

- Owner/writer role: environment/play-area session (this conversation; user decision 2026-09-25), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas.meta
  - Assets/ColorBlockJam/Editor/Levels/Canvas/**
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs.meta
  - production/tasks/TASK-20260924-230915890-e3c8bc05-level-canvas-doors-board-issues/**
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
- Execution dependency: task 3 (level-canvas-block-tools) closed
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given the Door tool, then overlapping or off-edge doors are refused, and a same-colour neighbour is extended instead of duplicated.
2. Given a shrinking resize that removes content, then the dialog reports the exact counts; Cancel restores the value, and Confirm applies it as one undo step.
3. Given every `LevelDatabaseTests` fixture plus generated cases, then `LevelDraftIssues` has an error exactly when `LevelData.Validate` fails.
4. Given an issue row is clicked, then the canvas highlights its cells or segment.

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

- Focused test path/command: `LevelEditCommandTests` (doors, resize), `LevelDraftIssuesTests`
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
- Handoff destination: /implement-task TASK-20260924-230915890-e3c8bc05-level-canvas-doors-board-issues, then an independent verifier

## Stop conditions

- The other window writer is still active (the file changed within the last 10 minutes, or the user says so): wait, do not edit.
- A required change falls outside the allowed paths (for example Runtime `LevelData` serializability for v2): stop and record it for the owning task.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: SUPERSEDED
- Contract fingerprint: sha256:6cdec710881751537b9b9c8fda6a89f5fed27f59220e02d6abcafc798465baf1
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 0
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: cs:5 (unreviewed; never implemented)
- Closed at: 2026-09-25T08:29:07Z
- Closure mode: superseded
- Closed by: task_reconciliation
- Superseded by: TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | UNPROVEN | none | none |
| AC-2 | UNPROVEN | none | none |
| AC-3 | UNPROVEN | none | none |
| AC-4 | UNPROVEN | none | none |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from docs/Level-Editor-Design.md task 4
- Closed — superseded — 2026-09-25T08:29:07Z: replaced by TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls after the user's 2026-09-25 redesign (Pen gates, per-side edge resize, rule-set walls) and ownership transfer to the Level Editor session; never started
<!-- TASK-LIFECYCLE:END -->
