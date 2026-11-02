# TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls — Level Editor canvas: Pen gates, per-side edge resize, rule-set walls and an issues list

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls
- Supersedes: TASK-20260924-230915890-e3c8bc05-level-canvas-doors-board-issues
- Title: Level Editor canvas: Pen gates, per-side edge resize, rule-set walls and an issues list
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: feature
- Area: Level authoring (Level Editor canvas, doors, board size, validation)
- Workflow: direct

## Objective and baseline

- Observable objective: A designer only picks gates: the Pen places or extends a gate of the selected colour on the wall ring. The board is resized with per-side edge buttons that keep content in place, the canvas draws walls and corners exactly as the project's rule set builds them, and an issues list names each validation problem and highlights it on the canvas.
- Current behavior/evidence:
  - This task replaces `TASK-20260924-230915890-e3c8bc05-level-canvas-doors-board-issues`, which never started (ready/planning, attempt 0, no owner lock). The user changed its door and resize design and its owner on 2026-09-25.
  - Doors are edited only through the Raw data foldout. `LevelEditCommands` holds block commands only, and the Doors group is a placeholder (`LevelDatabaseWindow.cs` about line 323).
  - Width and Height are delayed fields. `ResizeBoard` uses `Canvas/LevelBoardResize.cs`: sizes are clamped to 3-8 x 3-10, and a shrink that would remove content is refused. The origin stays bottom-left, so only the right column and top row change.
  - `LevelCanvasElement.DrawRing` draws a fixed half-cell band and quarter-disc corners. It does not use `EnvironmentRuleSet`.
  - Walls are not level data; they are derived. The runtime and the LevelEditor scene preview compose them with the public `EnvironmentComposer(EnvironmentDatabase).Compose(IBoardLayout, List<EnvironmentPiecePlacement>)` (`Environment/EnvironmentComposer.cs:31-62`) from `BoardLayout.TryCreate(LevelData)`.
  - `ColorBlockJam.Editor.asmdef` references Runtime, Presentation, Onity.Unity and Addressables.Editor, not Environment. Nothing under Environment references Editor, so adding the reference creates no cycle.
  - Canvas geometry leaves only the 0.75-cell door band plus 12 px outside the board (`LevelCanvasGeometry.cs:42-58`). There is no room for edge buttons yet.
  - The CBJ Template has neither per-side resize nor automatic walls in its editor; per-side resize with content shift is our own rule (reference only).
- Expected behavior:
  1. **Pen gates.**
     - With the Pen, hovering a ring segment shows a one-segment ghost in the selected colour, or red when refused. A click places a door of the selected colour. If a same-colour door on that side touches the segment, the click extends that door instead (`length` + 1, and `start` - 1 when extending backwards).
     - A drag along one side extends the same door segment by segment, as one undo group.
     - A segment held by another door, or a corner, is refused with a status message.
     - Eraser and right click on a door remove it. Select plus a swatch recolours the selected door. Delete removes the selected door.
     - Commands in `LevelEditCommands`: `TryPlaceOrExtendDoor`, `TryRemoveDoor`, `TryRecolorDoor`, plus the next free `d{n}` id. Each returns a new `LevelData` or a designer message.
  2. **Per-side edge resize.**
     - Each board side has a `+` and a `−` button, centred on the side outside the door band: `+` adds a row or column on that side, and `−` removes that side's edge row or column. `LevelCanvasGeometry` reserves the band, and the hit-test expectations are updated.
     - One command, `TryResizeBoard(LevelData, side, delta, ...)`, handles every change:
       - Left and Bottom changes shift every block origin and every door start on the perpendicular sides.
       - Right and Top changes only change width or height.
       - Removing a line drops the doors on that edge and shortens (or removes) perpendicular doors that span the removed cell.
     - A removal that would delete content hatches the affected cells and segments and asks "Remove 2 blocks and 1 door?" with exact counts and no ids. Confirm is one undo step; Cancel changes nothing.
     - Buttons disable at the 3-8 x 3-10 limits. The Width and Height fields stay; they call the same command on the Right and Top sides, and Cancel restores the field value.
  3. **Rule-set walls.**
     - The canvas draws walls and corners from `EnvironmentComposer.Compose` output, drawing each wall and corner placement as a flat glyph at its quadrant position and yaw. This replaces the fixed ring band. The composed walls are computed once per model change, never per repaint, and cached with the model.
     - Doors keep their own coloured bars. Walls are composed from the board size and the doors that pass validation, so an invalid door is drawn and listed as an issue but does not remove the walls.
     - `ColorBlockJam.Editor.asmdef` gains the `ColorBlockJam.Environment` reference (user-approved). The database is loaded by its asset path and only read.
     - A missing or invalid `EnvironmentDatabase` shows one issue and draws no walls. There is no second wall algorithm.
  4. **Issues list.** `LevelDraftIssues` gives structured issues (kind, cells, side/segment, entity id, designer message), with an agreement test: it has an error exactly when `LevelData.Validate` fails, on every `LevelDatabaseTests` fixture plus generated cases. `LevelEditorIssuesList` (a `ListView`, max 120 px, collapsible) highlights an issue's cells or segment on the canvas when its row is clicked.
  5. **Docs.** `docs/Level-Editor-Design.md` sections 1-3, 5 and 7 describe Pen gates, edge resize, rule-set walls and the issues list, and record that this task supersedes task 4.
- Behavior that must remain unchanged:
  - Every behavior of tasks 1-3 and of `TASK-20260925-082521477-0b7cb096-level-editor-toolbox`.
  - One undo group per gesture.
  - Right click removes in every tool.
  - Canvas-view AC-3: no repaint for hover within one cell.
  - The 3D LevelEditor preview and the runtime environment (read-only use of Environment).
  - The level JSON is written only by Save Level.

## Ownership

- Owner/writer role: Level Editor session "Parça template boş kareler" (user decision 2026-09-25), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/**
  - Assets/ColorBlockJam/Editor/ColorBlockJam.Editor.asmdef (add the `ColorBlockJam.Environment` reference only)
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/ColorBlockJam.Editor.EditModeTests.asmdef (add the `ColorBlockJam.Environment` reference only, if the wall tests need it)
  - docs/Level-Editor-Design.md
  - production/tasks/TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Environment/** (public API use only)
  - Assets/ColorBlockJam/Data/** (EnvironmentDatabase and rule set are read only)
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Editor/BoardKit/**
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Assets/AddressableAssetsData/**
  - Writing level JSON except through the existing Save Level path during manual checks
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Editor/ColorBlockJam.Editor.asmdef
    Sole writer: this session (one added reference, through a text edit Unity reimports)
    Matching `.meta` owner: unchanged
  - Asset: Assets/ColorBlockJam/Tests/EditMode/ColorBlockJam.Editor.EditModeTests.asmdef (only if needed)
    Sole writer: this session (one added reference, through a text edit Unity reimports)
    Matching `.meta` owner: unchanged
- Other active writers/worktrees checked: The superseded task 4 belonged to the session "Ruled tile level sistem", which was asked on 2026-09-25 at about 11:10 not to start tasks 4 or 5. Environment/** belongs to the play-area environment task, which stays untouched.
- Execution dependency: TASK-20260925-082521477-0b7cb096-level-editor-toolbox closed or handed off with its Level Editor paths released
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given the Pen and a colour, when a free ring segment is clicked, then one door of that colour is added; a same-colour touching door is extended instead of duplicated; an occupied segment or a corner is refused with a message. A drag along a side extends one door as one undo step.
2. Given a door, when Eraser or right click hits it, then it is removed; given a selected door, a swatch recolours it and Delete removes it, each as one undo step.
3. Given `+` on Left or Bottom, then width or height grows by one and every block origin and perpendicular door start shifts so all content keeps its place on screen; Right and Top grow without a shift; four `+` then four `−` on the same side restore the original level exactly.
4. Given `−` on a side whose edge line holds content, then the canvas hatches it and the dialog reports the exact block and door counts. Cancel leaves the draft unchanged, and Confirm applies the change as one undo step. At a limit the button is disabled.
5. Given levels 1-5 of the active database, then the canvas wall and corner glyphs match `EnvironmentComposer.Compose` placements (count, quadrant position, yaw) for the same board and valid doors. A missing environment database gives one issue and no walls.
6. Given every `LevelDatabaseTests` fixture plus generated cases, then `LevelDraftIssues` has an error exactly when `LevelData.Validate` fails; clicking an issue row highlights its cells or segment.

## References and constraints

- Story/GDD/ADR/design/log/image:
  - User message 2026-09-25 with the edge-resize reference screenshot: "grid genişliği de bu şekilde çevredeki butonlara basılarak ayarlanabilir, duvarlar da otomatik atanabilir, biz sadece gateleri seçeriz, ruled tile sistemimiz kullanılabilir".
  - User answer 2026-09-25: the pen is used to add gates and draw pieces; the Editor → Environment reference is approved.
  - `docs/Level-Editor-Design.md`; `docs/Environment-Layout.md`; PROJECT.md DEC-100 revision (own rule set on the Unity Grid); PRD 11.2 (doors) and AC-7.2 (resize dialog).
- Unity version/platform: 2022.3.62f2; Editor only
- Package/architecture constraints:
  - UI Toolkit only.
  - Plain C# commands and issue model with EditMode tests; thin VisualElements.
  - No new package and no new asmdef; the only asmdef change is the approved Environment reference.
  - `ColorBlockJam.Editor` must not reference `ColorBlockJam.Editor.LevelPreview`.
  - Repaint only on change; composition runs on model change only.
- Explicit out of scope:
  - Void cells and interior walls, which need level JSON v2 and task 5's Void paint.
  - Gate effects (outside case scope per `docs/Color-Block-Jam-Mechanics-Guide.md`).
  - Freehand Pen pieces (`TASK-20260925-082521498-8e54a900-level-editor-custom-pieces`).
  - Doors that wrap a corner.
  - Play-from-editor, reorder/duplicate, solver, editor stamp.

## Verification and risk

- Focused test path/command: `LevelEditCommandTests` (doors, per-side resize incl. round trip), `LevelDraftIssuesTests` (agreement), `LevelCanvasHitTestTests` (updated geometry, ring segments), `LevelCanvasWallsTests` (composer agreement on levels 1-5), `LevelCanvasElementTests` (edge buttons, issue highlight)
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console after the asmdef change, in the live Editor (MCP HTTP 127.0.0.1:8082) or an isolated batchmode copy
- EditMode: the focused tests above, plus `DatabaseControlsTests`, `LevelDatabaseAddressablesTests`, `LevelDatabaseInspectorTests`, the Environment EditMode tests and the LevelPreview tests stay green
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence:
  - A window capture showing edge buttons, rule-set walls and a Pen gate ghost.
  - Gestures exercised in the live Editor with the level JSON hash unchanged unless Save is part of the scenario.
  - User acceptance of the look and feel.
- Risk: medium (asmdef reference change; new commands shifting content)
- Approval already granted (exact target only): user answers 2026-09-25 for the `ColorBlockJam.Editor.asmdef` → `ColorBlockJam.Environment` reference and this session's ownership of the Level Editor after task 3
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: $implement-task TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls, then an independent verifier

## Stop conditions

- A Level Editor path changed within the last 10 minutes by another session: wait, do not edit.
- The composer or rule-set API needed for wall glyphs is not public, or composing needs an Environment change: stop and record it for the environment owner.
- A required change falls outside the allowed paths (for example Runtime `LevelData`): stop and record it for the owning task.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: READY_TO_CLOSE
- Contract fingerprint: sha256:b657d6fa4c65e2fbb76dfbecbc457a719ecc01288f9dfad1132907c63bb69f87
- Evidence fingerprint: sha256:b326a073840a06366672827499ee9c40a63de9ee8483e2f3e5339cd34b13a184
- Reviewed revision: vcs:cs:7+owned-sha256:76626fa09c07fd3a364e9192621d9da3de214b5268158f36dccdc3922a3464b3
- Attempt count: 3
- Last feedback: 2026-09-25 user answer 'Hepsi': accept and close
- Blocker: none
- Next action: None
- Closed revision: vcs:cs:7+owned-sha256:76626fa09c07fd3a364e9192621d9da3de214b5268158f36dccdc3922a3464b3
- Closed at: 2026-09-25T13:55Z
- Closure mode: criteria_verified
- Closed by: task-cycle (automatic)
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Pen places or extends gates on the ring | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: automated: LevelCanvasGatesEdgesTests (Pen gates, Left stroke, corner refusal, 3-cell cap) + LevelEditCommandTests door commands; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 374/374 pass | vcs:cs:7+owned-sha256:76626fa09c07fd3a364e9192621d9da3de214b5268158f36dccdc3922a3464b3 |
| AC-2 Door remove and recolour | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: automated: LevelCanvasGatesEdgesTests erase/right click/swatch recolour/Delete key + LevelEditCommandTests TryRemoveDoor/TryRecolorDoor; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 374/374 pass | vcs:cs:7+owned-sha256:76626fa09c07fd3a364e9192621d9da3de214b5268158f36dccdc3922a3464b3 |
| AC-3 Per-side grow keeps content in place | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: automated: LevelEditCommandTests Grow* and the four-plus-four-minus round trip; LevelEditV2FieldsTests grow/shrink with void cells and board objects; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 374/374 pass | vcs:cs:7+owned-sha256:76626fa09c07fd3a364e9192621d9da3de214b5268158f36dccdc3922a3464b3 |
| AC-4 Per-side shrink dialog with exact counts | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: automated: hatch, exact counts, Cancel and one-step Apply pass; user acceptance pending for two deviations: an inline resize-confirm row replaces the modal dialog, and doors on the removed edge follow the edge instead of being dropped | vcs:cs:7+owned-sha256:76626fa09c07fd3a364e9192621d9da3de214b5268158f36dccdc3922a3464b3 |
| AC-5 Canvas walls match the rule-set composer | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: automated: LevelCanvasWallsTests (levels 1-5 composer agreement, missing database gives one issue); Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 374/374 pass | vcs:cs:7+owned-sha256:76626fa09c07fd3a364e9192621d9da3de214b5268158f36dccdc3922a3464b3 |
| AC-6 Issues agree with Validate and highlight | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: automated: LevelDraftIssuesTests agreement incl. schema v2 gate rules (length 1-3, gate colour) + issue-row highlight test; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 374/374 pass | vcs:cs:7+owned-sha256:76626fa09c07fd3a364e9192621d9da3de214b5268158f36dccdc3922a3464b3 |
| V-1 Window capture and user look-and-feel acceptance | PASS | explicit user acceptance 2026-09-25 (answer 'Hepsi' in this session: accept and close the toolbox and gates/edges tasks); last automated run: Level EditMode groups 374/374 (2026-09-25T13:16Z); earlier: manual: evidence/level-editor-gates-2026-09-25.png (reference frame, 22 px corners, gates flush with the ring); D-001..D-004 verified; user acceptance of the look and feel pending | vcs:cs:7+owned-sha256:76626fa09c07fd3a364e9192621d9da3de214b5268158f36dccdc3922a3464b3 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| [D-001](defects/D-001.md) | ac_failure:v-1:corner-buttons-too-small | VERIFIED | AC_FAILURE | V-1 | Corner cut/fill buttons are 16 px, smaller than the reference corner squares. | independent workflow wf_3932e0e8-f0a (visual + code verifiers, adversarial judges): VERIFY: PASS; capture evidence/level-editor-frame-2026-09-25.png; regression rerun 2026-09-25 at the current revision, canvas files unchanged; revision re-stamped 2026-09-25T13:55Z: the only owned-set change since verification is the peer-owned LevelDatabaseJsonTests.cs, unrelated to this defect | vcs:cs:7+owned-sha256:76626fa09c07fd3a364e9192621d9da3de214b5268158f36dccdc3922a3464b3 |
| [D-002](defects/D-002.md) | ac_failure:v-1:corner-buttons-too-close | VERIFIED | AC_FAILURE | V-1 | The two buttons of a corner nearly touch on the diagonal. | independent workflow wf_3932e0e8-f0a (visual + code verifiers, adversarial judges): VERIFY: PASS; capture evidence/level-editor-frame-2026-09-25.png; regression rerun 2026-09-25 at the current revision, canvas files unchanged; revision re-stamped 2026-09-25T13:55Z: the only owned-set change since verification is the peer-owned LevelDatabaseJsonTests.cs, unrelated to this defect | vcs:cs:7+owned-sha256:76626fa09c07fd3a364e9192621d9da3de214b5268158f36dccdc3922a3464b3 |
| [D-003](defects/D-003.md) | ac_failure:v-1:edge-bars-not-spanning-frame | VERIFIED | AC_FAILURE | V-1 | Edge bars span only the board, not the full frame over the wall ring. | independent workflow wf_3932e0e8-f0a (visual + code verifiers, adversarial judges): VERIFY: PASS; capture evidence/level-editor-frame-2026-09-25.png; regression rerun 2026-09-25 at the current revision, canvas files unchanged; revision re-stamped 2026-09-25T13:55Z: the only owned-set change since verification is the peer-owned LevelDatabaseJsonTests.cs, unrelated to this defect | vcs:cs:7+owned-sha256:76626fa09c07fd3a364e9192621d9da3de214b5268158f36dccdc3922a3464b3 |
| [D-004](defects/D-004.md) | ac_failure:v-1:gate-bars-reach-past-wall-ring | VERIFIED | AC_FAILURE | V-1 | Gate bars reach 0.75 cell out, past the 0.5 cell wall ring. | independent workflow wf_d596f9ff-928 (visual + code verifiers, adversarial judges): VERIFY: PASS; capture evidence/level-editor-gates-2026-09-25.png; regression rerun 2026-09-25 at the current revision, canvas files unchanged; revision re-stamped 2026-09-25T13:55Z: the only owned-set change since verification is the peer-owned LevelDatabaseJsonTests.cs, unrelated to this defect | vcs:cs:7+owned-sha256:76626fa09c07fd3a364e9192621d9da3de214b5268158f36dccdc3922a3464b3 |
| [D-005](defects/D-005.md) | preservation_failure:field-complete-copies:v2-fields-dropped-by-edits | VERIFIED | PRESERVATION_FAILURE | Tasks 1-3 field-complete record copies | Any editor command drops void cells, effects and cell objects. | independent read-only code verifier (every record construction site under Editor/Levels, no aliasing, resize math for all four sides x grow/shrink): VERIFY: PASS; LevelEditV2FieldsTests 8/8; revision re-stamped 2026-09-25T13:55Z: the only owned-set change since verification is the peer-owned LevelDatabaseJsonTests.cs, unrelated to this defect | vcs:cs:7+owned-sha256:76626fa09c07fd3a364e9192621d9da3de214b5268158f36dccdc3922a3464b3 |

### Transition history

- Created — planning — contract authored from the user's Level Editor feedback item 3 of 2026-09-25; supersedes the unstarted task 4
- Started — implementation — 2026-09-25T10:31:27Z: user "devam et" after the toolbox handoff (toolbox awaits visual acceptance); direct implement-task run
- Verification — 2026-09-25T11:35Z: implementation, review fixes and relayed user refinements complete; LevelDraftIssues aligned with the schema v2 Validate gate rules (TASK-20260925-102657710-f7bfcf6a); edge icons added under Editor/Levels/Icons (outside the listed paths, same owner session, relayed user request); full EditMode suite green apart from the BoardKit baseline; awaiting user acceptance
- Reopened work — 2026-09-25T12:24Z: V-1 FAIL on user feedback; NEW_DEFECT D-001, D-002, D-003 (AC_FAILURE, V-1) OPEN; attempt 2 starts
- Verification — 2026-09-25T12:34Z: attempt 2 repaired D-001..D-003 (reference frame); independent workflow verified all three; guards added for the absolute sizes; V-1 and AC-4 await the user
- Reopened work — 2026-09-25T12:45Z: V-1 FAIL on user feedback; NEW_DEFECT D-004 (AC_FAILURE, V-1) OPEN; attempt 3 starts
- Verification — 2026-09-25T12:52Z: attempt 3 repaired D-004 (gates as deep as the ring); independent workflow verified it; V-1 and AC-4 await the user
- Reopened work — 2026-09-25T13:05Z: NEW_DEFECT D-005 (PRESERVATION_FAILURE) OPEN from the v2 schema owner's report; no attempt consumed for a peer-reported preservation fix beyond attempt 3
- Verification — 2026-09-25T13:16Z: D-005 repaired (v2 fields survive every editor command and resize) and independently verified; D-001..D-003 ledger rows repaired to eight cells; D-001..D-004 and AC rows re-stamped after a regression rerun; V-1 and AC-4 await the user
- Closed — 2026-09-25T13:55Z: the user accepted every criterion (answer "Hepsi"); all criteria PASS and every defect VERIFIED at the reviewed revision; closed automatically (criteria_verified)
<!-- TASK-LIFECYCLE:END -->
