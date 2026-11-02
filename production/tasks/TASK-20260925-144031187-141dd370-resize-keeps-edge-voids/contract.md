# TASK-20260925-144031187-141dd370-resize-keeps-edge-voids — Level Editor: a board resize keeps cut corners and edge notches on the outline

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-144031187-141dd370-resize-keeps-edge-voids
- Supersedes: none
- Title: Level Editor: a board resize keeps cut corners and edge notches on the outline
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:7
- Task kind: bug
- Area: Level authoring (Level Editor per-side resize with void cells)
- Workflow: direct

## Objective and baseline

- Observable objective: After a corner is cut, adding or removing a line on that side keeps the cut at the board's corner, instead of leaving a void notch in the middle of the edge.
- Current behavior/evidence:
  - The user's screenshot of 2026-09-25 (about 17:40 local) shows a 5-wide Level 1 with a void cell in the middle of the bottom edge (column 3 of 5), wrapped by walls. The user's words: "böyle buglar oluyor bazen bunları da düzelt ek task açıp" (bugs like this happen sometimes; fix them too, in an extra task).
  - Reproduction: cut the Bottom-right corner of a 4-wide board, making void (3,0), then press the Right add bar. `Canvas/LevelBoardResize.ResizeVoidCells` keeps every void in place (Right growth does not shift), so (3,0) stays void on the now 5-wide board and (4,0) is floor. No corner button can fill it back, because (3,0) is no longer a corner.
  - The shrink path drops a void that sits in a removed line, so removing the Right line after a Bottom-right cut loses the cut.
  - Defect D-005 of the gates/edges task added this void handling and its tests in `LevelEditV2FieldsTests`. The rule was "voids keep their place like content".
- Expected behavior:
  - A void cell on the outermost line of the resized side (Right: x = W-1, Left: x = 0, Top: y = H-1, Bottom: y = 0) follows that edge onto the new outermost line, on both growth and shrink, and the cell it leaves becomes floor. On Left and Bottom growth it stays on line 0 while the content shifts.
  - A moved void is dropped when its new cell is covered by a block or a board object, has a gate opening onto it, is already void, or would split the floor. These are the same rules the corner cut uses (`LevelEditCommands.GetCornerCutIssue`, generalised).
  - Every other void keeps its current behaviour. Voids in removed inner lines go with them, and the rest shift with the content.
  - The resize report and its confirm question are unchanged. A moved or dropped void is board shape, not content.
- Behavior that must remain unchanged:
  - Block, door and cell-object resize rules, the confirm row, hatching and undo (gates/edges task, D-005).
  - Corner cut/fill (`TASK-20260925-135325403-e7f5beb8-level-canvas-corner-voids`).
  - Level JSON written only by Save Level.

## Ownership

- Owner/writer role: Level Editor session "Parça template boş kareler" (implementer), independent verifier agent
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelBoardResize.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelEditCommands.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelEditV2FieldsTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelCanvasCornerVoidsTests.cs
  - docs/Level-Editor-Design.md (the per-side resize lines only)
  - production/tasks/TASK-20260925-144031187-141dd370-resize-keeps-edge-voids/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs (held by the preview-test and transform-tools sessions)
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelCanvasElement.cs (held by the transform-tools session)
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelEditorToolState.cs (held by the transform-tools session)
  - Writing level JSON except through Save Level during a manual check
  - A refresh, recompile or test run in the shared live Editor while another session holds it
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked: "Make Level Editor preview tests work unfocused" holds `LevelDatabaseWindow.cs` and `LevelEditorShellTests.cs`; "Revize seçim ve frame davranışı" (TASK-20260925-142546927-22e06638) takes the window, `LevelCanvasElement.cs`, `LevelEditorToolState.cs`, `LevelEditorIcons.cs`, the tool tests and the Tools lines of the design doc. Neither lists `LevelBoardResize.cs`, `LevelEditCommands.cs` or the two test files above.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given a 4 x 5 level with the Bottom-right corner cut (void (3,0)), when the Right add bar is pressed, then the board is 5 x 5, (4,0) is the only void, and (3,0) is floor; pressing the Right remove bar then gives back void (3,0) on the 4 x 5 board.
2. Given a void on the Left or Bottom edge line, when that side grows, then the void stays on line 0 while blocks, doors and inner voids shift.
3. Given a Top-edge void, when the Top shrinks and the new top cell is covered by a block, then the void is dropped and the block stays; with the cell free, the void moves down onto it.
4. Given the D-005 fixtures in `LevelEditV2FieldsTests`, then every expectation still holds except that the Top-edge void now follows the edge, which is the new rule.
5. Given the Level EditMode groups, then they all pass.

## References and constraints

- Story/GDD/ADR/design/log/image:
  - The user's screenshot and message of 2026-09-25.
  - `docs/Level-Editor-Design.md` (gates, edges, walls: per-side resize and corner block; the design note that an edge click works one cell at a time is a later task).
  - Defect D-005 of `TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls`.
- Unity version/platform: 2022.3.62f2; Editor only
- Package/architecture constraints: plain C# resize with EditMode tests; no new package, asmdef or serialized asset.
- Explicit out of scope:
  - Per-cell carving with the edge bars.
  - Moving voids that are not on the resized side's outermost line.
  - The canvas drawing.

## Verification and risk

- Focused test path/command: `LevelEditV2FieldsTests`, `LevelCanvasCornerVoidsTests`, then all `ColorBlockJam.Tests.EditMode.Level` groups through MCP for Unity `run_tests`
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: zero `error CS` after a script refresh
- EditMode: all Level groups pass
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence: the user's check in the Level Editor (cut a corner, grow and shrink that side)
- Risk: low
- Approval already granted (exact target only): none
- Commit/checkin constraint: forbidden
- Current invocation permission: user "böyle buglar oluyor bazen bunları da düzelt ek task açıp" (2026-09-25)
- Handoff destination: $implement-task TASK-20260925-144031187-141dd370-resize-keeps-edge-voids, then an independent verifier

## Stop conditions

- The fix needs a change in a path held by another session: stop and coordinate.
- A void rule would contradict `LevelData.Validate`: stop and ask the schema owner.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:a2956a20e03c32dc502c0ec33c2d509c40e5cf2f8bd10eae6237bdbf4ff1b1b4
- Evidence fingerprint: sha256:0254df4d503cb0774bff2e208669affda7ca5f067e0578693ad57882134bcc64
- Reviewed revision: vcs:cs:7+owned-sha256:1109a47f217797e3f91ce269e454279f8703008ab82458caa5bcf9d0cea7ea1e
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:cs:7+owned-sha256:1109a47f217797e3f91ce269e454279f8703008ab82458caa5bcf9d0cea7ea1e
- Closed at: 2026-09-28T08:46:46Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 A cut corner follows its side through growth and shrink | PASS | automated: LevelCanvasCornerVoidsTests.RightGrowthMovesTheCutCornerAndShrinkRestoresIt; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 386/386 pass; independent read-only verifier (four sides x grow/shrink, delta 1 and >1, Validate agreement): VERIFY: PASS | vcs:cs:7+owned-sha256:1109a47f217797e3f91ce269e454279f8703008ab82458caa5bcf9d0cea7ea1e |
| AC-2 Left and Bottom growth keep edge voids on line 0 | PASS | automated: LevelCanvasCornerVoidsTests.LeftAndBottomGrowthKeepTheEdgeVoidOnLineZero; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 386/386 pass; independent read-only verifier (four sides x grow/shrink, delta 1 and >1, Validate agreement): VERIFY: PASS | vcs:cs:7+owned-sha256:1109a47f217797e3f91ce269e454279f8703008ab82458caa5bcf9d0cea7ea1e |
| AC-3 A moved void is dropped under a block and moves onto a free cell | PASS | automated: LevelCanvasCornerVoidsTests.TopShrinkDropsTheEdgeVoidWhenBlockedAndMovesItWhenFree; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 386/386 pass; independent read-only verifier (four sides x grow/shrink, delta 1 and >1, Validate agreement): VERIFY: PASS | vcs:cs:7+owned-sha256:1109a47f217797e3f91ce269e454279f8703008ab82458caa5bcf9d0cea7ea1e |
| AC-4 D-005 expectations hold, apart from the new edge rule | PASS | automated: LevelEditV2FieldsTests (ShrinkingMovesTheTopEdgeVoidAndRemovesCellObjectsWithTheirLine updated for the new rule; other D-005 expectations unchanged); Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 386/386 pass; independent read-only verifier (four sides x grow/shrink, delta 1 and >1, Validate agreement): VERIFY: PASS | vcs:cs:7+owned-sha256:1109a47f217797e3f91ce269e454279f8703008ab82458caa5bcf9d0cea7ea1e |
| AC-5 Level EditMode groups pass | PASS | automated: every Level group; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 386/386 pass; independent read-only verifier (four sides x grow/shrink, delta 1 and >1, Validate agreement): VERIFY: PASS | vcs:cs:7+owned-sha256:1109a47f217797e3f91ce269e454279f8703008ab82458caa5bcf9d0cea7ea1e |
| V-1 User check in the Level Editor | UNPROVEN | manual: the user cuts a corner and grows/shrinks that side in the Level Editor; pending | vcs:cs:7+owned-sha256:1109a47f217797e3f91ce269e454279f8703008ab82458caa5bcf9d0cea7ea1e |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's screenshot and "böyle buglar oluyor bazen bunları da düzelt ek task açıp" (2026-09-25)
- Started — implementation — 2026-09-25T14:41Z: direct implement-task run on the user's order
- Verification — 2026-09-25T14:55Z: implemented (edge-line voids follow the resized edge; GetVoidIssue shared with the corner cut); Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 386/386 pass; independent read-only verifier (four sides x grow/shrink, delta 1 and >1, Validate agreement): VERIFY: PASS; V-1 awaits the user
- Updated — verification — 2026-09-26: TASK-20260926-181022947-224a7aef-level-canvas-staircase-corners changed LevelBoardResize.cs with this task owner's go. Staircases 2 or more steps deep at the resized side's two corners follow their corner (AC-8 of that task), laid again step by step through `LevelEditCommands.GetCornerStepIssue` in the same resize result and undo step. Depth 0 and 1 take the unchanged path, so this task's void order, report and confirm text are identical, and its edge-void tests are textually unchanged and green. That task's run: whole Level EditMode group 687/687. The reviewed revision is stale; re-stamp at closure; V-1 unchanged.
- 2026-09-28T08:46:46Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
