# TASK-20260925-102907480-9e5cbf34-level-editor-obstacles-barrier — Level Editor: Barrier tile in the Obstacles section

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-102907480-9e5cbf34-level-editor-obstacles-barrier
- Supersedes: none
- Title: Level Editor: Barrier tile in the Obstacles section
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:7 plus uncommitted Level Editor work (drafted at cs:5, revised on 2026-09-25)
- Task kind: feature
- Area: Level authoring (Level Editor Obstacles section, canvas and edit commands, board objects)
- Workflow: direct

## Objective and baseline

- Observable objective: A designer picks the Barrier tile, which sits after Wall in the Obstacles section below Blocks, and paints or erases barrier cells on the Level Editor canvas. Each connected stroke becomes one barrier board object. The section's "Starts open" toggle sets its state. The canvas draws and selects barriers, and every edit is one undo step in the draft.
- Current behavior/evidence:
  - The original request, relayed by session "Ruled tile level sistem" on 2026-09-25: "colors altına açılabilir bir tane daha obstacles diye ek olarak ve bir tane obstacle da yapabiliriz" (add a collapsible Obstacles section, and build one obstacle). The user chose the Barrier, catalog OBJ-05 in `docs/Color-Block-Jam-PRD-Mechanics-Catalog.md`.
  - The user later corrected the placement (2026-09-25, verbatim): "obstcaleslar blocks altında ayrı bir bölümde obstcale diye olacak bu entegre edildi mi level editöre" (obstacles go in a separate section called Obstacles under Blocks). The section therefore sits below Blocks, not directly under Colors.
  - The Obstacles section and its Wall tile belong to `TASK-20260925-145628329-306ba760-level-editor-obstacles-wall` (Task A). It arms the Pen with an obstacle brush without adding a `LevelEditorTool` value. Its `LevelCanvasModel` indexes cell-object cells, and `IsFree` refuses them. This task adds the second tile.
  - Schema handoff: the schema owner, session "Ruled tile level sistem", sent SCHEMA FINAL for `TASK-20260925-102657710-f7bfcf6a-level-json-v2-board-objects` on 2026-09-25. Level JSON v2 is verified, and `cellObjects` and `barrier` are unchanged (also recorded in `TASK-20260925-103527585-3d1b0a34-barrier-rule`).
    - The API: `LevelData.CellObjectCount` and `GetCellObjectAt`; `LevelCellObjectData` with `Id`, `CellCount`, `GetCellAt`, `ContainsCell`, `Effect` and `TryGetEffect(out BarrierObject)`; `BarrierObject(StartsOpen)`, where `startsOpen` defaults to false.
    - Validation: cells on the board and on floor, no shared cells, four-connected, and a closed barrier on no block.
    - f7bfcf6a stays open only for its Android device build (AC-7), which does not affect this editor work.
  - Save Level always writes formatVersion 2 (`Runtime/Levels/LevelDatabaseJson.cs`). The former AC-5, a disabled "Needs level JSON v2" state, is therefore dropped.
  - The canvas neither draws nor selects cell objects. `Canvas/LevelCanvasSelection.cs` has only the kinds None, Block and Door.
  - Runtime support:
    - `LevelEffectRegistry.k_barrierRuntimeImplemented` is false, so `LevelData.ValidateRuntimeSupport` refuses any level with a barrier: "Level {L} cell object {id} effect barrier is not implemented in gameplay yet." Project scope would refuse the active database at startup.
    - The Level Editor always edits the active game database, because picking a database in its dropdown makes it active (`LevelDatabaseAddressables.TrySetActive`).
    - The gameplay rule is `TASK-20260925-103527585-3d1b0a34-barrier-rule` (ready, not started). The 3D board view exists (`TASK-20260925-102657924-08cc082b-barrier-board-view`, DEC-121).
  - The Effect Editor (`TASK-20260925-100308276-f4a07e8a`) is not built. This task no longer waits for it: the section's Starts open toggle edits the selected barrier.
- Expected behavior:
  1. **Tile.**
     - The Obstacles section lists Barrier (`obstacle-barrier`) after Wall. Its tooltip names the rule: closed cells are walls, open cells are floor, and the state flips after every cleared block.
     - The section holds a "Starts open" toggle (`barrier-starts-open`). It applies to new barriers and, while a barrier is selected, to that barrier.
  2. **Brush.**
     - Picking the Barrier arms the Pen with the Barrier brush, the way Task A arms the Wall.
     - A left drag over free floor cells paints one barrier object per connected stroke, stepping cell by cell. A stroke that touches an existing barrier joins it.
     - The Eraser, and a right click or drag, remove barrier cells. A barrier left disconnected splits into separate objects, and an empty barrier is removed.
     - While the barrier starts closed, cells under a block are refused with the validation's designer message.
     - Each stroke is one undo group.
  3. **Selection.**
     - `LevelCanvasSelectionKind` gains `Obstacle`, with a `LevelCanvasSelection.ForObstacle(index, id)` factory.
     - The canvas hit test reports a cell held by a cell object. The Select tool on that cell selects the barrier, and the canvas draws the white selection outline around its cells.
     - The selection is resolved by id after every draft change, like blocks and doors.
     - The Starts open toggle shows the selected barrier's `startsOpen` and edits it through the draft as one undo step.
  4. **Canvas.**
     - `LevelCanvasModel` keeps each barrier's cells and its starts-open state.
     - Barrier cells draw as an overlay above the floor and below blocks: a hatched wall tint, lighter when the barrier starts open.
     - The hover ghost uses the Pen badge. The canvas repaints only on change.
  5. **Blocks and barriers.** A block may sit on an open barrier cell but not on a closed one. `IsFree` (from Task A) and the block place and move commands treat a closed barrier cell as occupied, and refuse it with the validation's message. An open barrier cell stays free.
  6. **Commands.**
     - `LevelEditCommands` gains barrier commands: paint cells, erase cells and set starts-open.
     - Each returns a new `LevelData` or a designer message.
     - Each copies records through `LevelRecordCopy`, so blocks, doors, void cells and effects survive.
     - A new barrier gets a unique id.
  7. **Save gate (decision 3(a)).**
     - Until `TASK-20260925-103527585-3d1b0a34-barrier-rule` lands, Save Level refuses a level for which `LevelData.ValidateRuntimeSupport` fails on a barrier cell object. It writes nothing and shows that message in the status line. The barrier stays in the unsaved draft.
     - Levels without a barrier save as before.
     - When 3d1b0a34 sets `k_barrierRuntimeImplemented` to true, the same check passes with no editor change.
  8. **Docs.** `docs/Level-Editor-Design.md` documents the Barrier tile, the toggle, the overlay, obstacle selection and the save gate in the Obstacles section.
- Behavior that must remain unchanged:
  - Task A's Obstacles section, Wall tile and Wall brush.
  - Every Level Editor behavior of tasks 1-3, the toolbox task, the gates/edges/walls task, tools cleanup, corner voids, resize edge voids and the transform tools.
  - Level JSON is written only by Save Level.
  - v1 files load unchanged until they are saved as v2.

## Ownership

- Owner/writer role: Level Editor session "Parça template boş kareler" (user decision 2026-09-25), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/**
  - Assets/ColorBlockJam/Editor/Levels/Icons/** (only a Barrier tile icon if one is needed; importer settings set through Unity APIs)
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs
  - Assets/ColorBlockJam/Tests/EditMode/Level*Tests.cs.meta
  - docs/Level-Editor-Design.md (the Obstacles lines only)
  - production/tasks/TASK-20260925-102907480-9e5cbf34-level-editor-obstacles-barrier/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/** (the schema belongs to f7bfcf6a, the runtime support flag to 3d1b0a34)
  - Assets/ColorBlockJam/Gameplay/**
  - Assets/ColorBlockJam/Domain/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Editor/BoardKit/**
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Assets/ColorBlockJam/Editor/Levels/LevelEffectEditorWindow.cs (the Effect Editor task f4a07e8a)
  - Writing level JSON except through the existing Save Level path during manual checks
  - Any write to an allowed path before Task A has released its paths
  - A refresh, recompile or test run in the shared live Editor while another session holds it
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below, only if the Barrier tile uses an icon; otherwise none
  - Asset: Assets/ColorBlockJam/Editor/Levels/Icons/obstacle-barrier.png
    Sole writer: implementer of this task
    Matching `.meta` owner: implementer of this task (TextureImporter settings set through Unity APIs, matching the existing tool icons)
- Other active writers/worktrees checked (2026-09-25T14:56Z):
  - Task A (`TASK-20260925-145628329-306ba760-level-editor-obstacles-wall`) is ready and queued. It takes the same Level Editor paths first.
  - Its predecessors are `TASK-20260925-142546927-22e06638-level-editor-transform-tools` and `TASK-20260925-144031187-141dd370-resize-keeps-edge-voids`. Both are in progress, with live lifecycle locks, on `LevelDatabaseWindow.cs`, `Canvas/**` and Level tests.
  - "Ruled tile level sistem" owns the schema (f7bfcf6a, SCHEMA FINAL sent) and the view (08cc082b). The gameplay session owns the Barrier rule (3d1b0a34).
  - No lifecycle lock exists for this task.
- Execution dependency: `TASK-20260925-145628329-306ba760-level-editor-obstacles-wall` verified and its Level Editor paths released ("LEVEL EDITOR PATHS FREE" from its run). The f7bfcf6a schema handoff is already recorded above and is no longer a dependency. 3d1b0a34 is not a dependency, because the save gate covers the time before it lands.
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given a v2 level, when the Barrier is picked and a connected stroke is painted over free floor, then one barrier object with those cells is added as one undo step. A stroke touching it joins it.
2. Given a barrier, when the Eraser removes a middle cell, then the barrier splits into two connected objects, and erasing all cells removes it. Each is one undo step.
3. Given a closed barrier stroke over a block cell, then that stroke is refused with the validation's message, and with Starts open on, the same cells are accepted. A block placed or moved onto a closed barrier cell is refused; onto an open barrier cell it is accepted.
4. Given a selected barrier, when Starts open is toggled, then its `startsOpen` param changes through the draft and the overlay lightens. Undo restores it.
5. Given the Level Editor window, then the Obstacles section lists Wall, then Barrier, then the Starts open toggle. The Select tool on a barrier cell selects that barrier with an outline, and the toggle shows its state. The selection survives an unrelated draft edit.
6. Given `k_barrierRuntimeImplemented` is false, when Save Level runs on a level with a barrier, then nothing is written and the status line shows the runtime-support message. A level without a barrier saves as before. This is proven by an EditMode test of the save check, with no write to `Data/Levels`.
7. Given the Level EditMode groups, including Task A's tests, then every test passes.

## References and constraints

- Story/GDD/ADR/design/log/image:
  - User request of 2026-09-25 (relayed), and the user's placement request of 2026-09-25 quoted above.
  - Survey and design workflow `wf_e3f41998-902` (2026-09-25), section 3, with the parent's adopted decisions 1(a), 2(a) and 3(a).
  - `docs/Color-Block-Jam-PRD-Mechanics-Catalog.md` OBJ-05 Barrier.
  - `docs/Level-Data-Format.md` (the `barrier` registry row and the runtime support gate).
  - `docs/Environment-Layout.md` (Barrier section).
  - `production/tasks/TASK-20260925-145628329-306ba760-level-editor-obstacles-wall/contract.md`.
  - `production/tasks/TASK-20260925-102657710-f7bfcf6a-level-json-v2-board-objects/contract.md`.
  - `production/tasks/TASK-20260925-103527585-3d1b0a34-barrier-rule/contract.md`.
  - `docs/Level-Editor-Design.md`.
- Unity version/platform: 2022.3.62f2; Editor only
- Package/architecture constraints:
  - UI Toolkit only.
  - Plain C# commands with EditMode tests.
  - No new package or asmdef.
  - No `LevelEditorTool` value is added.
  - Newtonsoft without `TypeNameHandling` (the schema task).
  - The canvas mutates nothing, and repaints only on change.
- Explicit out of scope:
  - The Barrier gameplay rule and its runtime support flag (3d1b0a34).
  - The 3D barrier view (08cc082b), and barriers in the LevelEditor scene preview (the LevelPreview owner).
  - The Effect Editor window (f4a07e8a).
  - The Wall tile (Task A).
  - Other obstacle types.
  - The v2 schema itself.

## Verification and risk

- Focused test path/command: new `LevelBarrierCommandTests` (paint, join, erase, split, refusal, starts-open, block onto a closed or open barrier) and `LevelCanvasBarrierTests` (tile order, brush stroke, overlay, obstacle selection, toggle undo, save check), then all `ColorBlockJam.Tests.EditMode.Level*` groups through MCP for Unity `run_tests`
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console in the live Editor or an isolated batchmode copy
- EditMode: the focused tests plus every Level* suite and the LevelPreview tests stay green
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence:
  - A live check of the Barrier tile, the brush, the toggle and the refused save.
  - User acceptance.
- Risk: medium
- Approval already granted (exact target only): the user's relayed request of 2026-09-25 for the Obstacles group and one obstacle (Barrier), the user's placement request of 2026-09-25, the parent's adopted decisions 2(a) and 3(a), and this session's ownership of Level Editor work
- Commit/checkin constraint: forbidden
- Current invocation permission: user "bütün işleri ajanlara teslim edip paralel çalış" (2026-09-25); execution waits for Task A
- Handoff destination: /game-studio-orchestration TASK-20260925-145628329-306ba760-level-editor-obstacles-wall TASK-20260925-102907480-9e5cbf34-level-editor-obstacles-barrier, then /implement-task this task and run an independent verifier

## Stop conditions

- Task A has not released its paths: write nothing.
- The schema's barrier API or its validation messages differ from the SCHEMA FINAL record above: stop and ask its owner.
- A required change falls outside the allowed paths: stop and record it.
- Another session changed a Level Editor path within the last 10 minutes: wait, do not edit.
- The user wants barriers saved before the rule lands (decision 3(b)) or the tile hidden (decision 3(c)): that is a contract change.

## Contract history (user decisions during implementation)

- 2026-09-25, relayed by the schema owner "Ruled tile level sistem" through the coordinator. First message: "starts open bool nedir gereksiz olmuş sil" and "barier kullanacaksak block kalkabilir". Correction that overrides it: "sadece bariyer olsun o zaman official olan da olan, block kaldır bu bölümden ve starts open da kalkmalı; bu effect olarak eklenebilir olmalı burada durmak yerine 3. windowdan".
  - Only the official Barrier stays in the Obstacles section. The Block (Solid Block, `solid-block`) tile, brush and editor commands are removed from `Editor/Levels/**` and the Level tests. The void-cell Wall tile of Task A no longer existed (Block had replaced it), so no Wall tile is kept; the void commands stay.
  - The section's "Starts open" toggle is removed (expected behavior 1 and 3, acceptance scenarios 4 and 5). `startsOpen` stays an optional barrier param, false by default, edited in the Effect Editor (f4a07e8a). The editor keeps `LevelBarrierCommands.TrySetStartsOpen` for it, with no UI.
  - A newly painted barrier has no params, so it starts closed. Join, split and erase keep each barrier's effect params. Merge rule: the first-listed barrier's id and whole effect win.
  - Unchanged: the open/closed overlay shade, obstacle selection, the rule that a block may sit on an open barrier but not a closed one, and the save gate (decision 3(a)).

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:6aedf20e15a6d1d8d097f00644e0a6a4c5eb72f74cca418a82d77ac1ddd56c8c
- Evidence fingerprint: sha256:323c5d0ffe24ba546a3b3aeedf8bc89fabaddef7aefe9cbaf994fc682d1faed0
- Reviewed revision: vcs:cs:7+owned-sha256:28b0226d61ce8030ea0b82795b640808441ca3dd1a98892a4af302c737698010
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:cs:7+owned-sha256:28b0226d61ce8030ea0b82795b640808441ca3dd1a98892a4af302c737698010
- Closed at: 2026-09-28T08:46:46Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Barrier stroke paints or joins one object | PASS | automated: LevelObstacleBarrierTests (paint, join, merge with the first-listed effect winning); Level EditMode 2026-09-25 (live Editor, MCP, instance-pinned): implementer 476/476; Barrier verifier 503/504 (schema-owner link.xml race); later full run 505/505 (Effect Editor verifier); independent verifier: VERIFY: FAIL (1 finding) — the finding is outside this task (Runtime link.xml, schema owner); no defect in the Barrier code | vcs:cs:7+owned-sha256:28b0226d61ce8030ea0b82795b640808441ca3dd1a98892a4af302c737698010 |
| AC-2 Erase splits or removes barriers | PASS | automated: LevelObstacleBarrierTests (erase, split keeping params, empty removed); Level EditMode 2026-09-25 (live Editor, MCP, instance-pinned): implementer 476/476; Barrier verifier 503/504 (schema-owner link.xml race); later full run 505/505 (Effect Editor verifier); independent verifier: VERIFY: FAIL (1 finding) — the finding is outside this task (Runtime link.xml, schema owner); no defect in the Barrier code | vcs:cs:7+owned-sha256:28b0226d61ce8030ea0b82795b640808441ca3dd1a98892a4af302c737698010 |
| AC-3 Closed barrier refuses block cells, both ways | PASS | automated: LevelObstacleBarrierTests and LevelObstacleWallCommandsTests (closed barrier refuses block cells in paint, place, move, replace and IsFree); Level EditMode 2026-09-25 (live Editor, MCP, instance-pinned): implementer 476/476; Barrier verifier 503/504 (schema-owner link.xml race); later full run 505/505 (Effect Editor verifier); independent verifier: VERIFY: FAIL (1 finding) — the finding is outside this task (Runtime link.xml, schema owner); no defect in the Barrier code | vcs:cs:7+owned-sha256:28b0226d61ce8030ea0b82795b640808441ca3dd1a98892a4af302c737698010 |
| AC-4 Starts open toggles through the draft | UNPROVEN | superseded in part by the user's decision (2026-09-25): the Obstacles toggle is removed and startsOpen is edited from the Effect Editor (TASK-20260925-100308276-f4a07e8a); LevelBarrierCommands.TrySetStartsOpen is tested; the UI and its undo arrive with the Effect Editor | vcs:cs:7+owned-sha256:28b0226d61ce8030ea0b82795b640808441ca3dd1a98892a4af302c737698010 |
| AC-5 Barrier tile after Wall; Select selects a barrier | PASS | automated: LevelObstaclesSectionTests (Obstacles holds Barrier only per the user decision; Select outlines a barrier and keeps it across unrelated edits and undo); Level EditMode 2026-09-25 (live Editor, MCP, instance-pinned): implementer 476/476; Barrier verifier 503/504 (schema-owner link.xml race); later full run 505/505 (Effect Editor verifier); independent verifier: VERIFY: FAIL (1 finding) — the finding is outside this task (Runtime link.xml, schema owner); no defect in the Barrier code | vcs:cs:7+owned-sha256:28b0226d61ce8030ea0b82795b640808441ca3dd1a98892a4af302c737698010 |
| AC-6 Save refused with the runtime-support message until 3d1b0a34 lands | PASS | automated: save gate after Validate and before any write; barrier-free levels unaffected; Level EditMode 2026-09-25 (live Editor, MCP, instance-pinned): implementer 476/476; Barrier verifier 503/504 (schema-owner link.xml race); later full run 505/505 (Effect Editor verifier); independent verifier: VERIFY: FAIL (1 finding) — the finding is outside this task (Runtime link.xml, schema owner); no defect in the Barrier code | vcs:cs:7+owned-sha256:28b0226d61ce8030ea0b82795b640808441ca3dd1a98892a4af302c737698010 |
| AC-7 Level EditMode groups pass | PASS | automated: every Level group; the earlier link.xml failure (schema owner) is resolved; Level EditMode 2026-09-25 (live Editor, MCP, instance-pinned): implementer 476/476; Barrier verifier 503/504 (schema-owner link.xml race); later full run 505/505 (Effect Editor verifier); independent verifier: VERIFY: FAIL (1 finding) — the finding is outside this task (Runtime link.xml, schema owner); no defect in the Barrier code | vcs:cs:7+owned-sha256:28b0226d61ce8030ea0b82795b640808441ca3dd1a98892a4af302c737698010 |
| V-1 Live check and user acceptance | UNPROVEN | manual: evidence/level-editor-obstacles-barrier.png (Obstacles with only Barrier); the user paints, erases and selects a barrier; pending | vcs:cs:7+owned-sha256:28b0226d61ce8030ea0b82795b640808441ca3dd1a98892a4af302c737698010 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's relayed Obstacles/Barrier request of 2026-09-25
- ready — planning — contract revision 2026-09-25: the Barrier tile moves into Task A's Obstacles section below Blocks (TASK-20260925-145628329-306ba760, user placement request); the former AC-5 v1 disabled state is dropped (Save always writes v2); the SCHEMA FINAL handoff for f7bfcf6a is recorded; the canvas model, obstacle selection and closed-barrier block rules are added; the Effect Editor dependency is replaced by the section's Starts open toggle; saving a barrier is refused until 3d1b0a34 lands (decision 3(a)); queued after Task A; fingerprint recomputed
- Started — implementation — 2026-09-25T18:28Z: the Wall task (306ba760) is verified and the Level Editor paths are free; user order "bütün işleri ajanlara teslim edip paralel çalış"; the block rule on closed barriers and solid board objects is folded in (verifier follow-up)
- Contract revision — 2026-09-25T19:33Z: the "Contract history" section records the user's decisions (Block obstacle removed; Obstacles Starts open toggle removed, startsOpen edited from the Effect Editor); fingerprint restamped from sha256:f4f2c0a53c40…
- Verification — 2026-09-25T19:33Z: implemented by an agent (Block obstacle and the Obstacles Starts open toggle removed per the user); independent verification clean for this task; AC-7 waits for the schema owner's solid-block removal to finish; AC-4 moves to the Effect Editor; V-1 awaits the user
- Verification — 2026-09-25T19:43Z: AC-7 re-checked: the Level groups pass 505/505 after the schema owner finished removing solid-block; AC-4 moves to the Effect Editor; V-1 awaits the user
- 2026-09-28T08:46:46Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
