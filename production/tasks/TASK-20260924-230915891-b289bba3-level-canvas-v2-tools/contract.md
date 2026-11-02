# TASK-20260924-230915891-b289bba3-level-canvas-v2-tools — Level Editor canvas: void cells and Arrow/Ice mechanics (level JSON v2)

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-230915891-b289bba3-level-canvas-v2-tools
- Supersedes: none
- Title: Level Editor canvas: void cells and Arrow/Ice mechanics (level JSON v2)
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: feature
- Area: Level authoring (Level Editor window)
- Workflow: direct

## Objective and baseline

- Observable objective: With level JSON v2 available, the designer paints void cells (holes) and sets Arrow (axis) and Ice (clears to thaw) on the selected block from the Mechanics group, and the canvas shows them.
- Current behavior/evidence: Blocked until `TASK-20260924-215645373-33a6a7a4-level-json-v2-effects` adds `voidCells` and typed effects to `LevelData`; the Board Void tool and the Mechanics group show "Needs level JSON v2" from task 1.
- Expected behavior (design: `docs/Level-Editor-Design.md`, revision 2 with the user's decisions of 2026-09-25):
  1. Void paint (left drag = void, right = floor; refuses cells under a block or in front of a door) and Mechanics cards (Arrow axis, Ice 1-20) per design section 2.
  2. v2 issue rules (blocks on floor, doors open onto floor, floor four-connected), glyph and badge drawing, and the `LevelRecordCopy` effects parameter so every v1 command preserves effects.
- Behavior that must remain unchanged:
  - Every task 1-4 behavior
  - Levels without v2 data behave exactly as before

## Ownership

- Owner/writer role: environment/play-area session (this conversation; user decision 2026-09-25), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas.meta
  - Assets/ColorBlockJam/Editor/Levels/Canvas/**
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs.meta
  - production/tasks/TASK-20260924-230915891-b289bba3-level-canvas-v2-tools/**
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
- Execution dependency: task 4 (level-canvas-doors-board-issues) closed and `TASK-20260924-215645373-33a6a7a4-level-json-v2-effects` closed with effects and void cells Unity-serializable
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given v2 data, then void cells and effects round-trip through Save and Refresh and survive every v1 command.
2. Given invalid void placements, then they are refused with messages naming the cell.
3. Given the preview hook, then published snapshots carry void cells and effects (drawing them in the 3D preview belongs to the preview owner).

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

- Focused test path/command: `LevelEditCommandTests` (void, effects), `LevelDraftIssuesTests` (v2 rules)
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
- Handoff destination: /implement-task TASK-20260924-230915891-b289bba3-level-canvas-v2-tools, then an independent verifier

## Stop conditions

- The other window writer is still active (the file changed within the last 10 minutes, or the user says so): wait, do not edit.
- A required change falls outside the allowed paths (for example Runtime `LevelData` serializability for v2): stop and record it for the owning task.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:15d2e6a348878fa3e9799d84d453698cf5a2f71aa420d11b66d03d974e6c7d34
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 0
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity (unreviewed; workspace has pending changes)
- Closed at: 2026-09-28T07:38:28Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | UNPROVEN | none | none |
| AC-2 | UNPROVEN | none | none |
| AC-3 | UNPROVEN | none | none |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from docs/Level-Editor-Design.md task 5; gated on the v2 schema task
- 2026-09-28T07:38:28Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("kapatabilirsin") after a read-only supersession audit: the scope is delivered by a changed design. Voids are authored by corner cut/fill and staircase steps (TASK-20260925-135325403-e7f5beb8-level-canvas-corner-voids, TASK-20260926-181022947-224a7aef-level-canvas-staircase-corners, TASK-20260925-144031187-141dd370-resize-keeps-edge-voids) instead of a void paint brush; Arrow/Ice are edited in Effect Editor cards (TASK-20260925-100308276-f4a07e8a-level-editor-workspace-effect-editor, TASK-20260926-170647550-7683988f-effect-editor-component-cards) instead of a canvas Mechanics group; LevelRecordCopy keeps effects through v1 commands; LevelEditCommands.GetVoidIssue/GetCornerStepIssue name refused cells; the draft snapshot published to LevelPreviewHost carries voids and effects. No work ran under this contract; AC-1..AC-3 stay UNPROVEN here (preserved, not verified) and the covering tasks carry their own verification.
<!-- TASK-LIFECYCLE:END -->
