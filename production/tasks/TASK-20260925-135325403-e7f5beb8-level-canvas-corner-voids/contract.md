# TASK-20260925-135325403-e7f5beb8-level-canvas-corner-voids — Level Editor canvas: corner cut and fill with level JSON v2 void cells

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-135325403-e7f5beb8-level-canvas-corner-voids
- Supersedes: none
- Title: Level Editor canvas: corner cut and fill with level JSON v2 void cells
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:7
- Task kind: feature
- Area: Level authoring (Level Editor canvas, void cells)
- Workflow: direct

## Objective and baseline

- Observable objective: The eight corner buttons of the canvas edge frame work. The inner cut button of a corner turns that corner's cell into a void cell, and the outer fill button turns it back into floor. The canvas shows the void and wraps the rule-set walls around it.
- Current behavior/evidence (read 2026-09-25 16:53 local):
  - `Editor/Levels/Canvas/LevelCanvasElement.cs` `CreateCornerButton` calls `SetEnabled(false)` with the tooltip "(needs level JSON v2 void cells)", so no corner button can be clicked. User report relayed by session "Ruled tile level sistem" on 2026-09-25: "ne durumdayız hala köşelere tıklanmıyor level editörde".
  - Level JSON v2 has landed in the project: `LevelData.VoidCellCount`, `GetVoidCellAt`, `IsFloor(Vector2Int)` and the 9-argument constructor; `LevelData.Validate` reports a block cell, a door or a cell object on a void cell. `LevelRecordCopy` and `LevelBoardResize` keep and move void cells (defect D-005 of the gates/edges task).
  - `LevelCanvasModel` has no void set, the canvas draws every cell as floor, and `LevelCanvasWalls.TryCreateLayout` passes `Array.Empty<Vector2Int>()` as void cells to `BoardLayout.TryCreate`, so the rule-set walls never wrap a void.
  - `LevelEditCommands.TryPlaceBlock` and `TryMoveBlock` check only bounds and overlap, and `TryPlaceOrExtendDoor` does not check the cell behind the segment.
  - `docs/Level-Editor-Design.md` (gates, edges, walls section) already specifies the corner block: the fill button where the outer bar row and column cross, the cut button where the inner ones cross.
  - The user chose on 2026-09-25 (answer "Hepsi") to make corner cut/fill a separate small task.
- Expected behavior:
  - Each corner of the board owns one corner cell: Bottom-left (0, 0), Bottom-right (W-1, 0), Top-left (0, H-1), Top-right (W-1, H-1).
  - Cut: a left click on a corner's inner button adds that corner cell to the level's void cells as one undo step ("Cut Corner"). It is enabled only when the cell is floor, no block covers it, no cell object covers it, and no gate opens onto it (the Bottom/Top and Left/Right ring segments at that cell). A refused click is impossible because the button is disabled, and the tooltip names the reason.
  - Fill: a left click on a corner's outer button removes that corner cell from the void cells as one undo step ("Fill Corner"). It is enabled only when the corner cell is void.
  - The canvas model carries the void set. The canvas draws a void cell in the design's void colour (dark grey), with no floor or grid line inside it. The walls come from `EnvironmentComposer` with the level's void cells, so the wall ring follows the cut corner exactly as the play area builds it.
  - Commands refuse a block cell on a void cell ("Cell (x, y) is a void."), for place, move, rotate, mirror and piece-type change, and refuse a gate segment whose cell behind it is void. The issues list keeps agreeing with `LevelData.Validate`.
  - The preview publish, Save and Reload carry the void cells unchanged (they already serialise).
- Behavior that must remain unchanged:
  - Every behavior of `TASK-20260925-082521487-465fec81-level-canvas-gates-edges-walls` and `TASK-20260925-135135646-45507296-level-editor-tools-cleanup`: edge-bar resize with its confirm row, Pen gates, rule-set walls on a board without voids (levels 1-5 composer agreement), issues list, frame geometry and icons.
  - Canvas-view AC-3: hover inside one cell requests no canvas repaint; composition runs on model change only.
  - Void cells, effects and cell objects survive every command (`LevelEditV2FieldsTests`).
  - The level JSON is written only by Save Level.

## Ownership

- Owner/writer role: Level Editor session "Parça template boş kareler" (implementer), independent verifier agent
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/**
  - Assets/ColorBlockJam/Tests/EditMode/LevelCanvasCornerVoidsTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelCanvasCornerVoidsTests.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/LevelCanvasGatesEdgesTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelCanvasWallsTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelEditCommandTests.cs
  - docs/Level-Editor-Design.md
  - production/tasks/TASK-20260925-135325403-e7f5beb8-level-canvas-corner-voids/**
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
- Other active writers/worktrees checked: session "Ruled tile level sistem" owns Runtime/Levels (schema v2, in verification) and confirmed it does not touch Editor/Levels/**. The tools-cleanup task of this same session edits `LevelDatabaseWindow.cs` first.
- Execution dependency: `TASK-20260925-135135646-45507296-level-editor-tools-cleanup` handed off (implementation verified), and the "Ruled tile level sistem" session's "SCHEMA FINAL" message confirming that the void-cell API used here (`VoidCellCount`, `GetVoidCellAt`, `IsFloor`, the 9-argument `LevelData` constructor, `BoardLayout.TryCreate` with void cells) did not change.
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given a 5 x 5 level with no void cells, when the Bottom-left cut button is clicked, then (0, 0) is the level's only void cell, the canvas draws it as void, the composed walls match `EnvironmentComposer.Compose` for the same board, doors and void cell, Undo restores floor in one step, and the Bottom-left fill button is enabled while the cut button is disabled.
2. Given each of the four corners, then cut and fill act on (0, 0), (W-1, 0), (0, H-1) and (W-1, H-1) respectively.
3. Given a corner cell covered by a block, a cell object or a gate opening onto it, then that corner's cut button is disabled and its tooltip names the reason; no void cell is added.
4. Given a void corner cell, when a block is placed, moved, rotated, mirrored or changed to a piece type onto it, or a gate is painted on a ring segment in front of it, then the command is refused with a message naming the cell and the level is unchanged.
5. Given a level with a cut corner, when it is saved and reloaded, then the void cell is still there, the issues list shows no error for it, and the published preview snapshot carries it.
6. Given the Level EditMode groups, then the gates/edges, tools-cleanup, walls (levels 1-5) and v2-field tests still pass.

## References and constraints

- Story/GDD/ADR/design/log/image: `docs/Level-Editor-Design.md` (gates, edges, walls and issues: the corner block; canvas layers: void colour); `docs/Level-Data-Format.md` (void cells and their validation messages); PROJECT.md DEC-100 (rule-set walls) and DEC-119 (single undoable draft).
- Unity version/platform: 2022.3.62f2; Editor only
- Package/architecture constraints:
  - UI Toolkit and Painter2D only; plain C# commands with EditMode tests; thin VisualElements.
  - No new package, asmdef or serialized asset; no Runtime or Environment change (use the existing `BoardLayout.TryCreate` void overload and `LevelData` accessors).
  - Repaint and wall composition only on model change.
- Explicit out of scope:
  - Per-cell carving with the edge bars (design note: "an inner click voids that edge cell"), interior void painting and staircase corners beyond the single corner cell: those belong to `TASK-20260924-230915891-b289bba3-level-canvas-v2-tools` or a later task.
  - Gates along a void edge inside the board (only the outer ring hosts gates).
  - Effects, the Effect Editor and board-object painting.
  - Rendering void cells in the 3D Level Preview (the preview owner's scope).

## Verification and risk

- Focused test path/command: EditMode `LevelCanvasCornerVoidsTests` (new), `LevelEditCommandTests` (void refusals), `LevelCanvasWallsTests` (void composer agreement), `LevelCanvasGatesEdgesTests` (corner buttons); then all `ColorBlockJam.Tests.EditMode.Level` groups through MCP for Unity `run_tests`
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: zero `error CS` after a script refresh in the live Editor
- EditMode: all Level groups pass, apart from failures proven unrelated
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence: a window capture of a level with one cut corner through `GrabPixels`, stored under this task's `evidence/`; the user's look-and-feel acceptance
- Risk: low
- Approval already granted (exact target only): none
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: $implement-task TASK-20260925-135325403-e7f5beb8-level-canvas-corner-voids, then an independent verifier

## Stop conditions

- The void-cell API or its validation messages change in the "SCHEMA FINAL" message: re-read them before implementing.
- A required change falls outside the allowed paths (for example `BoardLayout` or `EnvironmentComposer`): stop and record it for the owning task.
- The peer session has not sent "EDITOR FREE" when compilation or tests are needed: finish source edits, then wait.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:6bb254b78c3051c4812baa9ee3cbcb1365f061066648fd2706caf3b6477a9f0e
- Evidence fingerprint: sha256:d59076c18da18453255450185944a2602914fa113a7bdec7f3fa600cab5ffd3d
- Reviewed revision: vcs:cs:7+owned-sha256:bb75b238c4ec9b37a4d65737b36afbe5fd7f2708d6f472f091d9a1b4716bf691
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:cs:7+owned-sha256:bb75b238c4ec9b37a4d65737b36afbe5fd7f2708d6f472f091d9a1b4716bf691
- Closed at: 2026-09-28T08:46:46Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Cut and fill the Bottom-left corner with walls from the composer and one undo step | PASS | automated: LevelCanvasCornerVoidsTests.CornerButtonsCutAndFillTheDraftAsOneUndoStepEach, LevelCanvasWallsTests.ACutCornerWrapsTheWallsExactlyAsTheComposerDoes; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 383/383 pass; independent workflow wf_65070663-40b (visual verifier, two code verifiers, adversarial refute): VERIFY: PASS; its low findings fixed and retested (void gate ghost red, no hover on a void, split-floor cut refused, faded disabled edge icons, compact timer, stale comments, doc and test names) | vcs:cs:7+owned-sha256:bb75b238c4ec9b37a4d65737b36afbe5fd7f2708d6f472f091d9a1b4716bf691 |
| AC-2 All four corners act on their own corner cell | PASS | automated: LevelCanvasCornerVoidsTests.CutTurnsEachCornerCellIntoAVoidAndFillTurnsItBack, ModelMarksVoidCellsAndEachCornersCutState; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 383/383 pass; independent workflow wf_65070663-40b (visual verifier, two code verifiers, adversarial refute): VERIFY: PASS; its low findings fixed and retested (void gate ghost red, no hover on a void, split-floor cut refused, faded disabled edge icons, compact timer, stale comments, doc and test names) | vcs:cs:7+owned-sha256:bb75b238c4ec9b37a4d65737b36afbe5fd7f2708d6f472f091d9a1b4716bf691 |
| AC-3 Cut disabled with a reason under a block, object or gate | PASS | automated: LevelCanvasCornerVoidsTests.CutNamesWhyACornerCannotBeCut, button tooltip in the window test; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 383/383 pass; independent workflow wf_65070663-40b (visual verifier, two code verifiers, adversarial refute): VERIFY: PASS; its low findings fixed and retested (void gate ghost red, no hover on a void, split-floor cut refused, faded disabled edge icons, compact timer, stale comments, doc and test names) | vcs:cs:7+owned-sha256:bb75b238c4ec9b37a4d65737b36afbe5fd7f2708d6f472f091d9a1b4716bf691 |
| AC-4 Commands refuse blocks and gates on a void cell | PASS | automated: LevelCanvasCornerVoidsTests.BlocksAndGatesAreRefusedOnAVoidCell; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 383/383 pass; independent workflow wf_65070663-40b (visual verifier, two code verifiers, adversarial refute): VERIFY: PASS; its low findings fixed and retested (void gate ghost red, no hover on a void, split-floor cut refused, faded disabled edge icons, compact timer, stale comments, doc and test names) | vcs:cs:7+owned-sha256:bb75b238c4ec9b37a4d65737b36afbe5fd7f2708d6f472f091d9a1b4716bf691 |
| AC-5 Save, reload, issues and preview keep the void cell | PASS | automated: void cells are Unity-serialized in the draft and written by the v2 JSON writer (LevelEditV2FieldsTests, schema v2 verified by its owner, SCHEMA FINAL 2026-09-25); cut-corner Validate has no void error (CutTurnsEachCornerCell...); Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 383/383 pass; independent workflow wf_65070663-40b (visual verifier, two code verifiers, adversarial refute): VERIFY: PASS; its low findings fixed and retested (void gate ghost red, no hover on a void, split-floor cut refused, faded disabled edge icons, compact timer, stale comments, doc and test names) | vcs:cs:7+owned-sha256:bb75b238c4ec9b37a4d65737b36afbe5fd7f2708d6f472f091d9a1b4716bf691 |
| AC-6 Gates/edges, tools-cleanup, walls and v2-field behaviour preserved | PASS | automated: every Level group; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 383/383 pass; independent workflow wf_65070663-40b (visual verifier, two code verifiers, adversarial refute): VERIFY: PASS; its low findings fixed and retested (void gate ghost red, no hover on a void, split-floor cut refused, faded disabled edge icons, compact timer, stale comments, doc and test names) | vcs:cs:7+owned-sha256:bb75b238c4ec9b37a4d65737b36afbe5fd7f2708d6f472f091d9a1b4716bf691 |
| V-1 Window capture and user look-and-feel acceptance | UNPROVEN | manual: evidence/level-editor-corner-cut-2026-09-25.png (walls wrap the cut top-left corner); later change leaves void cells unpainted and skips grid lines there; user acceptance pending | vcs:cs:7+owned-sha256:bb75b238c4ec9b37a4d65737b36afbe5fd7f2708d6f472f091d9a1b4716bf691 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's relayed report "hala köşelere tıklanmıyor" (2026-09-25) and the user's "Hepsi" answer choosing a separate small task
- Started — implementation — 2026-09-25T14:38Z: user reports "void ekleme çalışmıyor" and "hala istediklerim yapılmamış"; started while the tools-cleanup verification ran, on the peer's word that the void APIs were unchanged (confirmed by SCHEMA FINAL)
- Verification — 2026-09-25T14:38Z: implemented; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 383/383 pass; independent verification independent workflow wf_65070663-40b (visual verifier, two code verifiers, adversarial refute): VERIFY: PASS; its low findings fixed and retested (void gate ghost red, no hover on a void, split-floor cut refused, faded disabled edge icons, compact timer, stale comments, doc and test names); V-1 awaits the user
- Updated — verification — 2026-09-26: TASK-20260926-181022947-224a7aef-level-canvas-staircase-corners (session "Rotate tool rotation behavior", user order 2026-09-26) extends corner cut/fill into a staircase: each further cut press cuts the next diagonal step while nothing stands on it, and Fill takes back the last step. The first cut and the last fill behave as this task defines them. This supersedes this task's "a second cut is refused as already a void" behavior, and three assertions in LevelCanvasCornerVoidsTests were changed there on purpose. That task's run: whole Level EditMode group 687/687 (independent verifier), compile clean. The reviewed revision is stale because owned files changed; re-stamp at closure; V-1 unchanged.
- 2026-09-28T08:46:46Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
