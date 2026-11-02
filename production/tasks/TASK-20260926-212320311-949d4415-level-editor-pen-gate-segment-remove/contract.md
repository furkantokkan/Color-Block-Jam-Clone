# TASK-20260926-212320311-949d4415-level-editor-pen-gate-segment-remove — Pen right click removes one gate segment; the Eraser removes the whole gate

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260926-212320311-949d4415-level-editor-pen-gate-segment-remove
- Supersedes: none
- Title: Pen right click removes one gate segment; the Eraser removes the whole gate
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:10 plus the uncommitted Level Editor work of 2026-09-26/27 (planning provenance only)
- Task kind: UI
- Area: Level Editor canvas gate editing (`ColorBlockJam.Editor`)
- Workflow: direct

## Objective and baseline

- Observable objective: With the Pen, a right click on a gate removes only the clicked segment (cell) of that gate. The Eraser still removes the whole gate.
- Current behavior/evidence:
  - User message of 2026-09-27: "kalem ile gate sağ tıkladığımda bütün gate yerine bir parçası silinebilir silgi tüm parçaları silecek sadece".
  - `LevelCanvasElement.RequestRemove(hit)` runs for a right click with any tool and for an Eraser left click. For a gate it raises `DoorRemoveRequested(doorIndex)`, and the window's `RemoveDoor` applies `LevelEditCommands.TryRemoveDoor`, which removes the whole gate.
  - A gate hit (`LevelCanvasHit.ForDoor`) carries its side and ring-segment index.
  - A Pen stroke adds segments and merges them into an adjacent same-side gate. Gates are 1 to 3 cells.
  - The Pen tooltip reads "Right click or the Eraser removes a gate".
- Expected behavior:
  - Pen active, right click on a gate segment: one undo step ("Remove Gate Part") that removes only that segment.
    - An end segment shrinks the gate by one; it keeps its id, colour and effects.
    - A 1-cell gate is removed entirely, as today.
    - A middle segment splits the gate into two gates of the same colour on the same side. The lower-index part keeps the original id and its effects. The other part gets the next free gate id (`GetNextDoorId`) and no effects, because gate effects are hand-written, outside the case scope, and cannot be added in the editor.
    - Every other gate keeps its order and id.
  - The Eraser (left click, and right click while the Eraser is active) removes the whole gate as today. A right click with Select, or with a transform tool, also removes the whole gate as today; only the Pen changes.
  - The command is a pure `LevelEditCommands` method (for example `TryRemoveDoorSegment(level, doorIndex, side, segment, out result, out issue)`), with the same refusals style as `TryRemoveDoor`: a stale index or a segment outside the gate is refused and changes nothing.
  - The canvas raises the segment request only for the Pen's right click, and the window applies it through `ApplyCommand` (one undo group, dirty marker, preview publish).
  - Tooltips and docs: the Pen tooltip says a right click takes one gate cell off and the Eraser removes the whole gate. docs/Level-Editor-Design.md (Gates section and the Pen tool text) describes the rule.
- Behavior that must remain unchanged:
  - Painting gates (stroke, merge, corner refusal); gate colour picks; Eraser behaviour; right click on blocks, barriers and void cells with every tool.
  - Select's gate selection and recolour; the Effect Editor's gate card; level JSON format.

## Ownership

- Owner/writer role: implement-task (single writer)
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelEditCommands.cs
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelCanvasElement.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs (the event subscription, the handler and the Pen tooltip only)
  - Assets/ColorBlockJam/Tests/EditMode/GateSegmentRemoveTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/GateSegmentRemoveTests.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/LevelCanvasGatesEdgesTests.cs (only assertions on the changed Pen right-click rule)
  - Assets/ColorBlockJam/Tests/EditMode/LevelEditorToolsCleanupTests.cs (the Pen tooltip gate-help assertion only; revision 1)
  - docs/Level-Editor-Design.md
  - production/tasks/TASK-20260926-212320311-949d4415-level-editor-pen-gate-segment-remove/contract.md (managed lifecycle block only)
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Editor/Levels/LevelDraft.cs
  - Any other existing test file, and any change to another asserted outcome
  - Plastic checkin, shelve, branch, or undo of other sessions' pending changes
  - Changing the user's open Level Editor draft or level JSON
- Serialized assets: none (a new `.cs.meta` is Unity-generated script metadata owned by implement-task)
- Other active writers/worktrees checked (2026-09-26T21:25Z): TASK-20260926-204508638-a3ad6ee3-level-editor-architecture holds a lifecycle lock (since 21:03Z, owner "/root task-cycle Level Editor architecture") on `LevelDatabaseWindow.cs` and `Tests/EditMode/Level*Tests.cs`. Both are needed here. TASK-20260926-212320271-98b25efa-level-editor-arrow-markers (this session) writes LevelCanvasElement.cs first.
- Execution dependency:
  - TASK-20260926-204508638-a3ad6ee3-level-editor-architecture: wait until its claim `Library/ccgs/task-lifecycle-locks/TASK-20260926-204508638-a3ad6ee3-level-editor-architecture.lock.claim` is gone.
  - TASK-20260926-212320271-98b25efa-level-editor-arrow-markers (same session, LevelCanvasElement.cs): wait until its implementation handoff.
  - Before the first write, re-read LevelDatabaseWindow.cs (the architecture refactor may move `RemoveDoor` into a controller) and LevelCanvasElement.cs.
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. AC-1: Given a 3-cell gate, when the Pen right-clicks its first or last segment, then the gate shrinks to 2 cells, keeping its id, colour and effects, in one undo step. When it right-clicks the middle segment, then there are two 1-cell gates of the same colour: the lower part keeps the id and effects, and the other gets the next free id and no effects.
2. AC-2: Given a 1-cell gate, when the Pen right-clicks it, then the gate is removed.
3. AC-3: Given any gate, when the Eraser clicks it (left or right), or Select or a transform tool right-clicks it, then the whole gate is removed as before.
4. AC-4: Given a stale door index or a segment outside the gate, when the command runs, then it is refused and the level is unchanged. Undo after a segment removal restores the gate exactly.
5. AC-5: Given the Level EditMode suite, when it runs after the change, then it passes. The Pen tooltip and the design doc state the new rule.
6. AC-6: Given the live Level Editor, when the user right-clicks a gate cell with the Pen and uses the Eraser on a gate, then they accept the behaviour (explicit user acceptance).

## References and constraints

- Story/GDD/ADR/design/log/image: the user message of 2026-09-27 above; docs/Level-Editor-Design.md "Gates, edges, walls and issues"; PROJECT.md "Gates still use the Pen on the board edge".
- Unity version/platform: Unity 2022.3.62f2, Windows Editor only
- Package/architecture constraints: UI Toolkit; pure command in `LevelEditCommands`; the window applies commands through `ApplyCommand` (or its successor after the architecture refactor); m_/k_ naming; class summaries.
- Explicit out of scope: gate painting changes, gate effects editing, multi-segment drag erase with the Pen, and the Eraser's behaviour.

## Verification and risk

- Focused test path/command: UnityMCP `run_tests` EditMode `ColorBlockJam.Tests.EditMode.GateSegmentRemoveTests` and `ColorBlockJam.Tests.EditMode.LevelCanvasGatesEdgesTests`, then the Level suite `ColorBlockJam[.]Tests[.]EditMode[.]Level`
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: a clean compile after `refresh_unity` with no new Console errors; check `mcpforunity://editor/state` first
- EditMode: command table (end, middle, single, stale, out of range, undo); canvas routing (Pen right click → segment, Eraser/Select → whole)
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence: the user's acceptance (AC-6)
- Risk: low
- Approval already granted (exact target only): the user's request of 2026-09-27 above; the middle-segment split rule is this contract's default and is shown to the user
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: `/implement-task TASK-20260926-212320311-949d4415-level-editor-pen-gate-segment-remove`

## Contract history (conditions agreed after authoring)

- Revision 1, 2026-09-26T23:45Z, by the owning session after review: the reviewers noted that the Pen tooltip kept the old lead-in "Right click or the Eraser removes a gate:" only because LevelEditorToolsCleanupTests.PenTooltipCarriesTheGateHelp asserts it. The tooltip now reads "A right click on a gate takes one gate cell off; the Eraser removes the whole gate." That test file joins the allowed paths for that one assertion.

## Stop conditions

- The architecture lock is still held, or its refactor moved gate removal into a file this contract does not list (revise the contract first).
- The user rejects the middle-segment split rule.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:d06d99feeb3d5352378b72bd234044a7f2604b64008f803b884c397425760d47
- Evidence fingerprint: sha256:0e536ad7de648da459cdc0ad9ebe19cbfc7823e9e6caf6704770e4b80db1d3a0
- Reviewed revision: vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:f64030c708d13af6ffc861228489366086af97cde9a4b241866537572168daa4
- Attempt count: 0
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:f64030c708d13af6ffc861228489366086af97cde9a4b241866537572168daa4
- Closed at: 2026-09-27T17:24:20Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated: GateSegmentRemoveTests command table and window routing (job c2d8031a, 7/7; earlier 6274e8ab) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:f64030c708d13af6ffc861228489366086af97cde9a4b241866537572168daa4 |
| AC-2 | PASS | automated: GateSegmentRemoveTests 1-cell gate (job c2d8031a) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:f64030c708d13af6ffc861228489366086af97cde9a4b241866537572168daa4 |
| AC-3 | PASS | automated: GateSegmentRemoveTests Select, Eraser left/right and rotate right click remove the whole gate (job c2d8031a) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:f64030c708d13af6ffc861228489366086af97cde9a4b241866537572168daa4 |
| AC-4 | PASS | automated: GateSegmentRemoveTests refusals and one-Undo exact restore (job c2d8031a) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:f64030c708d13af6ffc861228489366086af97cde9a4b241866537572168daa4 |
| AC-5 | PASS | automated: LevelEditorToolsCleanupTests.PenTooltipCarriesTheGateHelp with the revision 1 wording (job c2d8031a) and the Level suite 729/729 (job 8144f72a); the design doc states the rule | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:f64030c708d13af6ffc861228489366086af97cde9a4b241866537572168daa4 |
| AC-6 | UNPROVEN | none | none |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored 2026-09-26T21:25Z by session "Edit dropdown ve colors kareleri" from the user's message of 2026-09-27; ready and queued (WAITING_FOR_OWNER) behind the architecture lock
- Started — implementation — 2026-09-26T23:25Z: the architecture lock was released at 22:15:07Z (that task is ready_to_close); session "Shuffle ve switch değişiklikleri" sent PATHS FREE for LevelDatabaseWindow.cs; the arrow-markers workflow finished its LevelCanvasElement.cs work. Only this task's claim is held. Gate removal is still RemoveDoor in LevelDatabaseWindow.cs (line 2381, subscribed at 642).
- Verification — 2026-09-26T23:48Z: workflow wf_cd0bf65e-6c9 implemented TryRemoveDoorSegment, the canvas routing (Pen right click raises DoorSegmentRemoveRequested; the Eraser and other tools keep DoorRemoveRequested), the window handler ("Remove Gate Part"), the tooltip, 7 GateSegmentRemoveTests and the docs. Both review lenses were CLEAN; the behaviour lens probed all four sides live in a temporary window. Focused 14/14 (job 6274e8ab) and the Level suite 723/723 (job 06625df9). Revision 1 then cleaned the tooltip lead-in; its rerun is pending, because the project compile is waiting on the arrow-gameplay workflow's in-progress test ports (CS1501 in its files).
- Rerun — 2026-09-27T00:12Z: the revision 1 tooltip and the gate tests passed in job c2d8031a (GateSegmentRemoveTests 7/7, LevelEditorToolsCleanupTests 2/2), run by the Restore task's workflow after LevelCanvasElement.cs also received the Ice task's 23:59Z canvas changes. The Level suite passed 729/729 (job 8144f72a). The reviewed revision moved to the current digest.
- 2026-09-27T17:24:20Z Closed by explicit user confirmation (task-done, user_confirmed): the user accepted the live result on 2026-09-27 ('bunlar okay', item 5, the Pen right-click gate change; AC-6) and ordered 'tasklari kapat izin veriyorum'. Criteria rows are preserved as recorded, not re-verified; owned files may have changed after the reviewed revision through other sessions.
<!-- TASK-LIFECYCLE:END -->
