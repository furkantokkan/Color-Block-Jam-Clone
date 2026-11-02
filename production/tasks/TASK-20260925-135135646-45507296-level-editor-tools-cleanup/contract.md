# TASK-20260925-135135646-45507296-level-editor-tools-cleanup — Level Editor: lean tools column, timer in the top bar, contextual transform row

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-135135646-45507296-level-editor-tools-cleanup
- Supersedes: none
- Title: Level Editor: lean tools column, timer in the top bar, contextual transform row
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:7
- Task kind: UI
- Area: Level authoring (Level Editor window tools pane and top bar)
- Workflow: direct

## Objective and baseline

- Observable objective: The Level Editor tools column shows only what a designer uses. The Doors, Board and Mechanics groups are gone. Timer Seconds sits in the top bar beside Level Name. The transform buttons appear only when they have a target, under a divider that separates them from the tool buttons.
- Current behavior/evidence (`Editor/Levels/LevelDatabaseWindow.cs` at cs:7 plus the session's uncommitted edits, read 2026-09-25 16:50 local):
  - `CreateToolsPane` adds, in order: Tools, Colors, Blocks, Doors (`CreateDoorsGroup`, a help label only), Board (`CreateBoardGroup`: read-only `level-id`, delayed `board-width`, `board-height`, `timer-seconds` with the `timer-text` m:ss label), Mechanics (`CreatePlaceholderGroup("mechanics-group")`, disabled, "Arrives with the level canvas tools."), Edit and Raw data.
  - `CreateToolsGroup` puts the `canvas-tools` row (Select, Pen, Rotate, Eraser) directly above the `block-actions` row (rotate ccw, rotate cw, mirror H, mirror V), with no separator. The transform buttons are always visible and only enable or disable.
  - The top bar `level-row` holds the Target Level stepper (with + and ×) and the delayed `level-name` field.
  - Width and Height are already edited with the canvas edge bars (task `TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls`).
  - The user's request (relayed 2026-09-25 12:23Z by session "Ruled tile level sistem", verbatim): "doors için dropdowna gerek yok, tek çeşit door var ve board ayarları da gereksiz kaldır, mechanics sil ve şunların arasında divider olsun çünkü bunlar sadece bir block seçince çıkacaklar." Screenshots showed the Doors foldout, the Board group (Level ID, Width, Height, Timer Seconds 300 with 5:00), the Mechanics placeholder and the Tools rows.
  - The user's decision in this session on 2026-09-25 (answer "Hepsi" to the scope question): Timer Seconds moves to the top bar beside Level Name, as proposed.
- Expected behavior:
  - The tools pane holds, in order: Tools, Colors, Blocks, Edit, Raw data. `doors-group`, `board-group` and `mechanics-group` do not exist.
  - The Pen tooltip carries the removed Doors help: click or drag on the wall ring paints a gate of 1 to 3 cells; right click or the Eraser removes it; with Select, click a gate, then a swatch recolours it.
  - `level-id`, `board-width` and `board-height` fields no longer exist. The board size changes only through the canvas edge bars and their resize confirm row.
  - The top bar `level-row` holds the Target Level stepper, `level-name` and a compact `timer-seconds` delayed integer field (tooltip "Level time limit, 10-300 seconds.") followed by its `timer-text` m:ss label. Commit clamps to 10-300, writes the draft through `SetDraftInt` as one undo step, and the label updates from the draft.
  - Inside the Tools group, a 1 px divider (`tools-divider`) separates `canvas-tools` from `block-actions`. The divider and the `block-actions` row are shown only while a transform target exists: a selected block, or a piece armed on the Pen (the existing one-rule target of AC-5 of the toolbox task). Otherwise both are hidden (`DisplayStyle.None`), not just disabled.
  - Showing or hiding the row does not repaint the canvas and does not change the canvas size: the canvas lives in the right pane.
- Behavior that must remain unchanged:
  - Every behavior of the toolbox task (`TASK-20260925-082521477-0b7cb096-level-editor-toolbox`) and the gates/edges task (`TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls`) apart from the removed fields: tool keys 1-4, R/F/Shift keys, one undo group per gesture, edge-bar resize and its confirm row, issues list, Save/Reload, dirty marker, preview publish.
  - Level Name and Timer stay unbound delayed fields refilled from the draft on every draft change (DEC-119: a window rebuilt after a domain reload never shows stale zeros).
  - Keyboard shortcuts are still ignored while the timer or name field has focus.
  - The level JSON is written only by Save Level.

## Ownership

- Owner/writer role: Level Editor session "Parça template boş kareler" (implementer), independent verifier agent
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/**
  - Assets/ColorBlockJam/Tests/EditMode/LevelEditorShellTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelCanvasBlockToolsTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelCanvasElementTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelCanvasGatesEdgesTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelEditorToolsCleanupTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelEditorToolsCleanupTests.cs.meta
  - docs/Level-Editor-Design.md
  - production/tasks/TASK-20260925-135135646-45507296-level-editor-tools-cleanup/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Editor/BoardKit/**
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Assets/ColorBlockJam/Editor/ColorBlockJam.Editor.asmdef
  - Assets/AddressableAssetsData/**
  - Assets/ColorBlockJam/Tests/EditMode/LevelEffectTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelDatabaseJsonTests.cs
  - Writing level JSON except through Save Level during a manual check
  - A refresh, recompile or test run in the shared live Editor while the "Ruled tile level sistem" session holds it (wait for its "EDITOR FREE" message)
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked: session "Ruled tile level sistem" writes Runtime/Levels and its own Level JSON tests and confirmed it does not touch Editor/Levels/**; no other session holds the Level Editor window paths. The toolbox and gates/edges tasks are owned by this same session and are being closed first.
- Execution dependency: `TASK-20260925-082521477-0b7cb096-level-editor-toolbox` and `TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls` closed, so their reviewed revisions stay stable while this task edits the shared window file.
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given the Level Editor window with a level loaded, then the tools pane children are the Tools, Colors, Blocks, Edit and Raw data groups in that order, and `doors-group`, `board-group`, `mechanics-group`, `level-id`, `board-width` and `board-height` are absent.
2. Given the top bar, then `level-row` holds the Target Level stepper, `level-name`, `timer-seconds` and `timer-text` in that order. When 90 is committed in the timer field, then the draft timer is 90, `timer-text` reads 1:30, Undo restores the previous value and label in one step, and a value of 5 or 400 commits as 10 or 300.
3. Given no selected block and no armed piece, then `block-actions` and `tools-divider` are hidden. When a block is selected on the canvas or in the Selected Block dropdown, or a piece is armed on the Pen, then both show; when the selection clears and nothing is armed, both hide again. The transform buttons still act on the armed piece first, then the selected block.
4. Given the Pen tool button, then its tooltip names gate painting on the wall ring (1 to 3 cells), removal with right click or the Eraser, and recolouring a selected gate with a swatch.
5. Given the window rebuilt after a domain reload with a level loaded, then `level-name` and `timer-seconds` show the draft's values, not 0 or empty.
6. Given the Level EditMode groups, then every toolbox and gates/edges test still passes with its assertions adjusted only for the removed fields (tests that probed `level-id` as "a level is loaded" use the draft instead).

## References and constraints

- Story/GDD/ADR/design/log/image: `docs/Level-Editor-Design.md` sections 1 (layout), 2 (Board and Mechanics rows) and 6 (field homes); PROJECT.md DEC-119 (two-pane window, unbound delayed fields); the relayed user screenshots are described in the baseline.
- Unity version/platform: 2022.3.62f2; Editor only
- Package/architecture constraints:
  - UI Toolkit only; no new package, asmdef or serialized asset.
  - Visibility changes use `style.display`; the canvas does not repaint for them.
- Explicit out of scope:
  - The Effect Editor and any home for block or gate effects (Arrow, Ice); `TASK-20260924-230915891-b289bba3-level-canvas-v2-tools` still names the Mechanics group and needs its own revision later.
  - Corner cut/fill and void cells on the canvas (a separate task).
  - The "+" palette tile for level-local pieces (`TASK-20260925-082521498-8e54a900-level-editor-custom-pieces`).
  - The Obstacles group of `TASK-20260925-102907480-9e5cbf34-level-editor-obstacles-barrier`.
  - Showing or editing the level ID anywhere else.

## Verification and risk

- Focused test path/command: EditMode `LevelEditorToolsCleanupTests` (new) plus the updated `LevelEditorShellTests`, `LevelCanvasBlockToolsTests`, `LevelCanvasElementTests`, `LevelCanvasGatesEdgesTests`; then all `ColorBlockJam.Tests.EditMode.Level` groups through MCP for Unity `run_tests`
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: zero `error CS` after a script refresh in the live Editor
- EditMode: all Level groups pass, apart from failures proven unrelated (for example the focus-dependent preview-publish tests when the Editor is unfocused)
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence: a window capture of the new tools column and top bar through `GrabPixels`, stored under this task's `evidence/`; the user's look-and-feel acceptance
- Risk: low
- Approval already granted (exact target only): none
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: $implement-task TASK-20260925-135135646-45507296-level-editor-tools-cleanup, then an independent verifier

## Stop conditions

- A required change falls outside the allowed paths (for example Runtime `LevelData`): stop and record it for the owning task.
- The peer session has not sent "EDITOR FREE" when compilation or tests are needed: finish source edits, then wait.
- Removing a field breaks a behavior listed under "Behavior that must remain unchanged" in a way this task cannot restore inside its paths.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:f335461b5551b798df9c78825ffa90fef8eee02b4ff31b0fbe34db5b71ca517e
- Evidence fingerprint: sha256:64d06175d0202eb2691704b60c0c75ebf06088b94bc9bd393ca5bf50c776a1a1
- Reviewed revision: vcs:cs:7+owned-sha256:56da05b63607c17f52bd764faf2a2a04b7fb5921c524b17f7ec7d90be4e8e691
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:cs:7+owned-sha256:56da05b63607c17f52bd764faf2a2a04b7fb5921c524b17f7ec7d90be4e8e691
- Closed at: 2026-09-28T08:46:46Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Tools pane holds only Tools, Colors, Blocks, Edit, Raw data | PASS | automated: LevelEditorShellTests.ToolsColumnHoldsToolsColorsBlocksEditAndRawData; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 383/383 pass; independent workflow wf_65070663-40b (visual verifier, two code verifiers, adversarial refute): VERIFY: PASS; its low findings fixed and retested (void gate ghost red, no hover on a void, split-floor cut refused, faded disabled edge icons, compact timer, stale comments, doc and test names) | vcs:cs:7+owned-sha256:56da05b63607c17f52bd764faf2a2a04b7fb5921c524b17f7ec7d90be4e8e691 |
| AC-2 Timer in the top bar with m:ss, clamp and one undo step | PASS | automated: LevelEditorShellTests top-bar order, TimerCommitClampsAndShowsMinutes, UndoAndRedoOfABoardEditUpdateFieldsDirtyMarkerAndPreview; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 383/383 pass; independent workflow wf_65070663-40b (visual verifier, two code verifiers, adversarial refute): VERIFY: PASS; its low findings fixed and retested (void gate ghost red, no hover on a void, split-floor cut refused, faded disabled edge icons, compact timer, stale comments, doc and test names) | vcs:cs:7+owned-sha256:56da05b63607c17f52bd764faf2a2a04b7fb5921c524b17f7ec7d90be4e8e691 |
| AC-3 Divider and transform row shown only with a target | PASS | automated: LevelEditorToolsCleanupTests.TransformRowAndDividerShowOnlyWithASelectedBlockOrAnArmedPiece, LevelCanvasElementTests selection sync, LevelCanvasBlockToolsTests row order; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 383/383 pass; independent workflow wf_65070663-40b (visual verifier, two code verifiers, adversarial refute): VERIFY: PASS; its low findings fixed and retested (void gate ghost red, no hover on a void, split-floor cut refused, faded disabled edge icons, compact timer, stale comments, doc and test names) | vcs:cs:7+owned-sha256:56da05b63607c17f52bd764faf2a2a04b7fb5921c524b17f7ec7d90be4e8e691 |
| AC-4 Pen tooltip carries the gate help | PASS | automated: LevelEditorToolsCleanupTests.PenTooltipCarriesTheGateHelp; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 383/383 pass; independent workflow wf_65070663-40b (visual verifier, two code verifiers, adversarial refute): VERIFY: PASS; its low findings fixed and retested (void gate ghost red, no hover on a void, split-floor cut refused, faded disabled edge icons, compact timer, stale comments, doc and test names) | vcs:cs:7+owned-sha256:56da05b63607c17f52bd764faf2a2a04b7fb5921c524b17f7ec7d90be4e8e691 |
| AC-5 Name and timer refill after a domain reload | PASS | automated: LevelEditorShellTests.NameAndTimerShowTheDraftAfterTheGuiIsRebuilt; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 383/383 pass; independent workflow wf_65070663-40b (visual verifier, two code verifiers, adversarial refute): VERIFY: PASS; its low findings fixed and retested (void gate ghost red, no hover on a void, split-floor cut refused, faded disabled edge icons, compact timer, stale comments, doc and test names) | vcs:cs:7+owned-sha256:56da05b63607c17f52bd764faf2a2a04b7fb5921c524b17f7ec7d90be4e8e691 |
| AC-6 Toolbox and gates/edges behaviour preserved | PASS | automated: every Level group; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 383/383 pass; independent workflow wf_65070663-40b (visual verifier, two code verifiers, adversarial refute): VERIFY: PASS; its low findings fixed and retested (void gate ghost red, no hover on a void, split-floor cut refused, faded disabled edge icons, compact timer, stale comments, doc and test names) | vcs:cs:7+owned-sha256:56da05b63607c17f52bd764faf2a2a04b7fb5921c524b17f7ec7d90be4e8e691 |
| V-1 Window capture and user look-and-feel acceptance | UNPROVEN | manual: evidence/level-editor-tools-cleanup-2026-09-25.png (no Doors/Board/Mechanics, divider and transform row, Timer 300 5:00 beside Level Name); user acceptance of the look and feel pending | vcs:cs:7+owned-sha256:56da05b63607c17f52bd764faf2a2a04b7fb5921c524b17f7ec7d90be4e8e691 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's relayed tools-column request of 2026-09-25 and the user's "Hepsi" answer (timer beside Level Name)
- Started — implementation — 2026-09-25T14:00Z: user "görevleri bitir" / "Hepsi"; predecessors closed; direct implement-task run
- Verification — 2026-09-25T14:38Z: implemented; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 383/383 pass; independent verification independent workflow wf_65070663-40b (visual verifier, two code verifiers, adversarial refute): VERIFY: PASS; its low findings fixed and retested (void gate ghost red, no hover on a void, split-floor cut refused, faded disabled edge icons, compact timer, stale comments, doc and test names); V-1 awaits the user
- Superseded criterion — 2026-09-25T15:17Z: AC-3 (transform row shown only with a target) is superseded by TASK-20260925-142546927-22e06638-level-editor-transform-tools on the user's later request (transforms are pick-up tools, row always shown); the other criteria stand; the reviewed revision is stale from that task's edits and is re-stamped at closure
- 2026-09-28T08:46:46Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
