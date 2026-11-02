# TASK-20260925-103527585-3d1b0a34-barrier-rule — Barrier gameplay rule (OBJ-05)

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: decision owner — choose how the runtime support flag becomes true (Expected behavior 5): widen this contract to Validate Level barrier solver support, or queue that support as its own Validation task
- Task ID: TASK-20260925-103527585-3d1b0a34-barrier-rule
- Supersedes: none
- Title: Barrier gameplay rule (OBJ-05)
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:17 (revision 2026-09-28; the first draft was written against cs:5)
- Task kind: feature
- Area: Gameplay rules (board object occupancy hook)
- Workflow: direct

## Objective and baseline

- Observable objective: In a level with a Barrier, a closed barrier's cells stop every block like a wall, and an open barrier's cells are floor. The barrier flips once for every cleared block (a door exit or a booster removal). A barrier that closes under a block leaves that block in place: the block may leave, and no other block may enter until it opens.
- Current behavior/evidence:
  - User decision 2026-09-25, relayed by the "Ruled tile level sistem" session and quoted in its contracts `TASK-20260925-102657710-f7bfcf6a-level-json-v2-board-objects` and `TASK-20260925-102907480-9e5cbf34-level-editor-obstacles-barrier`: build one obstacle, the Barrier (OBJ-05); the gameplay session owns the rule.
  - Catalog OBJ-05 (`docs/Color-Block-Jam-PRD-Mechanics-Catalog.md` section 5):
    - Rule: "closed cells are walls, open cells floor; the state starts at `startsOpen` and flips on each `BlockCleared`".
    - DEC-MECH-18: a barrier closing under a block leaves it in place.
    - DEC-MECH-3: listeners react in fixed order (blocks, doors, objects); a Vacuum of N blocks flips a barrier N times.
    - Acceptance: AC-OBJ-05.1 and 05.2.
    - Solver support: parity.
  - The drag core (`TASK-20260925-012336316-a88e25db-block-drag-gate-exit`) and the move commands (`TASK-20260925-151032072-f1e30581-move-commands`, DEC-122) provide:
    - XP-3 `ICellGate.AllowsCell(IBoardReader, int block, Cell)`, asked by `MoveRules.CanOccupy` and by the exit swept path in `ExitRules` (own cells are skipped there, every other empty floor cell is asked);
    - XP-5 `IBoardEventListener` with `BlockExitedEvent` (carrying `ExitOutcome`), `BlockRemovedEvent`, `MoveCommittedEvent` and `FootprintChangedEvent`, published by the command handlers in the command phase;
    - hooks asked about `PreviewBoard` views during a drag and about `BoardState` in commands, so a gate reads occupancy only from the reader it is passed.
  - The shared block-effect architecture (`docs/Block-Effects-Architecture.md`, DEC-158) now owns effect registration: `IBlockEffectModule` (`Read`, `AddHooks`, `CreateView`) in `GameSceneInstaller`'s ordered module list, `BlockEffectContextSource`, `EffectAttemptHookSource` (a fresh `BlockEffectAttempt` per attempt) and `IBlockEffectView`, which `BoardPresenter` drives at its existing lifecycle points (`TryPrepare`, `BindAttempt` in `Place`, `Sync`, `SetPaused` in Suspend, `StopTweens` and `ReturnAll` in Teardown, `Dispose`). Domain hooks live in `Runtime/Domain/Hooks` (`IceRules`, `ArrowAxisConstraint`, `ArrowExitGate`).
  - The Rocket splits a block into parts with new runtime IDs (DEC-146) and raises `FootprintChanged`, never a clear; Hammer raises one `BlockRemoved`; Vacuum raises one `BlockRemoved` per removed block (DEC-147).
  - The view API from `TASK-20260925-102657924-08cc082b-barrier-board-view` (DEC-121) is in place in `Assets/ColorBlockJam/Runtime/Environment/EnvironmentView.cs`: `Build(layout, level)`, `BarrierCount`, `SetBarrierOpen(string objectId, bool open, bool animate)` (throws for an ID it does not show), `SetBarrierTweensPaused`, `StopBarrierTweens`, `Clear`.
  - Verified in the files on 2026-09-28 (revision items of the previous history entry): `GameAttemptLoader.TryBuild` already calls `environment.Build(layout, data)` before `BlockEffectContextSource.Prepare` (item 1 needs no change); `BoardPresenter` already forwards Suspend and Teardown to every effect view, so a barrier view reaches the barrier tweens without a presenter change (item 2 needs no change); the runtime-shapes integration closed on 2026-09-27 (item 3 needs no gate).
  - `LevelEffectRegistry.k_barrierRuntimeImplemented` is still `false` (`Assets/ColorBlockJam/Runtime/Core/Levels/LevelEffectRegistry.cs`, line 52 on 2026-09-28). `LevelSupportGateTests.EveryRuntimeImplementedEffectHasSolverSupport` (DEC-144) fails for any runtime-implemented effect the Validate Level solver does not model, and `LevelSolverSupport` lists only `arrow` and `ice`.
- Expected behavior:
  1. **Domain rule.** `Assets/ColorBlockJam/Runtime/Domain/Hooks/BarrierRule.cs` (plain C#, no engine reference, beside `IceRules` and the Arrow hooks as the architecture places Domain hooks; the first draft's `Domain/Effects` folder is dropped) implements `ICellGate` and `IBoardEventListener`.
     - It copies its construction arrays (board size, each barrier's cells, each barrier's `startsOpen`) and allocates nothing after construction.
     - It holds per-barrier open state, a per-barrier version (the flips since the attempt began) and a per-cell barrier index.
     - `AllowsCell` refuses a closed barrier cell unless the reader it is passed shows the asking block on that cell (`board.GetOccupant(cell) == block`, DEC-MECH-18). It keeps no occupancy of its own, so the drag's `PreviewBoard`, a command's `BoardState` and a future solver get the same answer.
     - `BlockCleared` means a `BlockExitedEvent` with `ExitOutcome.Leave`, or a `BlockRemovedEvent`. Each one flips every barrier once and increments its version, so a Vacuum removing N blocks flips it N times. `Stay`, `MoveCommitted` and `FootprintChanged` change nothing.
     - A Rocket part (DEC-146) needs no state: the reader reports the part's new ID on its cells, so a part standing on a closed cell may leave and nothing else may enter.
  2. **Metadata.** `Assets/ColorBlockJam/Runtime/Gameplay/Effects/BarrierEffectLevel.cs` (`IBlockEffectLevel`, TypeId `barrier`) copies the level's barrier cell objects into immutable per-barrier IDs, cells and `startsOpen`, in authored order, and never mutates `LevelData`.
     - It reads them through the v2 schema API: `LevelData.CellObjectCount`/`GetCellObjectAt(i)` (`Id`, `CellCount`, `GetCellAt`) and `TryGetEffect<BarrierObject>` (`StartsOpen`), only on Home, because typed parameters convert on first access. Other cell objects are ignored.
     - Its `BlockCount` is the authored block count, as `BlockEffectContextSource.Prepare` requires. `CreateRule()` returns a fresh `BarrierRule`.
     - `AttemptLevel` and `AttemptLevelConverter` stay effect-free (architecture layer 2).
  3. **Module.** `Assets/ColorBlockJam/Runtime/Gameplay/Effects/BarrierEffectModule.cs` (`IBlockEffectModule`, TypeId `barrier`) replaces the obsolete `BoardEffectHookSource` design.
     - It is constructed with the scene's `EnvironmentView`, as `IceEffectModule` is constructed with its material, and is listed last in `GameSceneInstaller`'s module list (`Ice, Arrow, Barrier`: blocks before board objects, DEC-MECH-3). That list line is the only installer change.
     - `Read` returns `BarrierEffectLevel`; `AddHooks` creates one fresh `BarrierRule` per attempt, so Restart restores `startsOpen`, registers it as `ICellGate` and `IBoardEventListener` when the level has a barrier, and returns `BarrierEffectState` (`Runtime/Gameplay/Effects/BarrierEffectState.cs`) holding the rule.
     - `IBlockEffectModule`, `IBlockEffectView`, `BlockEffectViewContext`, `EffectAttemptHookSource`, `LevelSession` and `EmptyAttemptHookSource` stay unchanged; no new public interface is added.
  4. **View sync.** `Assets/ColorBlockJam/Runtime/Gameplay/Effects/BarrierEffectView.cs` (`IBlockEffectView`, returned by `CreateView`) wraps the `EnvironmentView`:
     - `TryPrepare` (Home) takes the typed metadata. When the level has barriers, the shown play area must show the same number of barriers (`EnvironmentView.BarrierCount`), or the level is rejected with one error, before any Playing call could name an unknown barrier.
     - `BindAttempt` (every `Place`, Restart included) binds the fresh rule and calls `SetBarrierOpen(id, open, animate: false)` for every barrier.
     - `Sync` (the presenter's view-sync phase, after the command phase) calls `SetBarrierOpen(id, open, animate: true)` once for each flip since the last shown version, in order, the last call carrying the rule's current state. The call is presentation-only, and the domain never waits for it. It allocates nothing.
     - `SetPaused` forwards to `SetBarrierTweensPaused`, `StopTweens` to `StopBarrierTweens`; `ReturnAll` drops the attempt binding; `Attach`, `Detach`, `SetClipPlane` and `Reproject` do nothing (barriers are not block-bound); `Dispose` leaves the environment to its own owner.
     - A read-only `AnimatedFlipCount` reports the animated calls since the last bind (AC-6 evidence).
     - `BoardPresenter`, `LevelSession` and `GameAttemptLoader` stay unchanged.
  5. **Runtime support.** Change exactly one line: `private const bool k_barrierRuntimeImplemented = false;` becomes `true` in `Assets/ColorBlockJam/Runtime/Core/Levels/LevelEffectRegistry.cs` (member `k_barrierRuntimeImplemented`; the line number may move, the name does not). Barrier levels then pass the project-scope load gate (`LevelDatabase.LoadAsync`); the schema's tests derive their expectations from this flag.
     - Gate: the flip lands only when the Validate Level solver supports `barrier` (DEC-144, `LevelSupportGateTests.EveryRuntimeImplementedEffectHasSolverSupport`) and only together with or after the installer's module line. Until then the prepared hunk waits in `pending-hunks.md` and barrier levels stay refused at project load; tests build `LevelData` directly and are unaffected.
  6. **Docs.** `docs/Gameplay-Drag-And-Gate.md` gets a Barrier entry under Extending and a Loops row, and PROJECT.md gets one decision row (DEC-163: the orchestrator reserved DEC-161, which another session used first, and DEC-162 went to "Level content updates").
- Behavior that must remain unchanged:
  - Levels without cell objects play exactly as before.
  - Drag, exit, session and tick-order tests stay green.
  - 0 B in the Playing tick.

## Ownership

- Owner/writer role: gameplay session, independent verifier afterwards
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Runtime/Domain/Hooks/BarrierRule.cs (new)
  - Assets/ColorBlockJam/Runtime/Gameplay/Effects/Barrier*.cs (new: `BarrierEffectModule`, `BarrierEffectLevel`, `BarrierEffectState`, `BarrierEffectView`)
  - Assets/ColorBlockJam/Runtime/Gameplay/Composition/GameSceneInstaller.cs (the module-list line only)
  - Assets/ColorBlockJam/Runtime/Core/Levels/LevelEffectRegistry.cs (the `k_barrierRuntimeImplemented` constant only, under the gate of Expected behavior 5)
  - Assets/ColorBlockJam/Tests/Gameplay/EditMode/**/Barrier*.cs (new)
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/Barrier*.cs (new)
  - docs/Gameplay-Drag-And-Gate.md
  - PROJECT.md (one decision row)
  - production/tasks/TASK-20260925-103527585-3d1b0a34-barrier-rule/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/Domain/** other than the new `Hooks/BarrierRule.cs` (Movement, Exit, Session, Commands, Board and the existing hooks stay closed)
  - Assets/ColorBlockJam/Runtime/Gameplay/Session/**, Hud/**, Board/**, Levels/**, UI/**, Boosters/**, Input/**, `Composition/GameAttemptLoader.cs`, and the existing Effects files
  - Assets/ColorBlockJam/Runtime/Environment/** (the view API belongs to the barrier-board-view task)
  - Assets/ColorBlockJam/Runtime/Core/** other than the `k_barrierRuntimeImplemented` constant; Assets/ColorBlockJam/Runtime/Player/**
  - Assets/ColorBlockJam/Editor/** (the Validate Level solver included), Assets/ColorBlockJam/UI/**, Scenes/**, Prefabs/**, Data/**
  - any `.asmdef` or `AssemblyInfo.cs`
  - Plastic checkin, shelve, undo, add, move or publishing
  - Unity test runs and batchmode Unity by this owner (the orchestrator runs one combined batch at the end, user order 2026-09-28)
- Serialized assets: none
- Other active writers/worktrees checked (2026-09-28):
  - The Win-screen session edits `Runtime/Gameplay/Hud/**`, `Runtime/Gameplay/Session/LevelSession.cs`, `Runtime/Player/**` and `UI/Game/**`.
  - The Vacuum VFX session holds the booster-gameplay-flow lock and may touch `Runtime/Gameplay/Board/**`.
  - A shared file changed in the last 15 minutes is not edited: its exact hunk waits in `pending-hunks.md` (WAITING_FOR_OWNER) until its owner releases it.
- Execution dependency: none blocking. Released or landed: the "schema final" message for f7bfcf6a, board-art DI, runtime shapes (DEC-124, closed 2026-09-27), the barrier board view (08cc082b, DEC-121), the move commands (f1e30581, DEC-122) and the shared effect architecture (DEC-158). The runtime flag waits for barrier solver support (Expected behavior 5).
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given a closed barrier across a lane, when a block is dragged into it, then the block stops before the barrier. After one block clears (a door exit or a booster removal), a block can pass through (AC-OBJ-05.1).
2. Given an open barrier with a block standing on it, when another block clears, then the barrier closes and the standing block does not move. It can still move off, but no other block can enter those cells until the barrier opens again (AC-OBJ-05.2, DEC-MECH-18).
3. Given a Vacuum-style removal of N blocks in one command phase, then the barrier flips N times. A `Stay` exit outcome does not flip it.
4. Given a closed barrier in a concave shape's swept exit path, then that exit is refused (XP-3 in `ExitRules`).
5. Given Restart, then every barrier returns to `startsOpen`, and the view snaps without animating.
6. Given the Playing tick with barriers, then hook dispatch and view sync allocate 0 B, and the view receives exactly one `SetBarrierOpen` per flip.

## References and constraints

- Story/GDD/ADR/design/log/image: catalog OBJ-05, DEC-MECH-3 (Vacuum flips N times), DEC-MECH-18; PRD 8.3, RULE-28..RULE-30; `docs/Gameplay-Drag-And-Gate.md`; `docs/Block-Effects-Architecture.md` (DEC-158); `docs/Folder-Layout.md` (DEC-157); PROJECT.md Runtime Lifecycle and Performance Rules, DEC-110, DEC-113, DEC-121, DEC-122, DEC-146, DEC-147
- Unity version/platform: 2022.3.62f2; Android/iOS
- Package/architecture constraints: Domain has no engine references; no static registry or static mutable state; hooks are built per attempt; no `System.Linq`; C# 9; every tween has an owner and a stop point (the barrier tweens: `EnvironmentView`, paused by Suspend and stopped by Teardown through the barrier view)
- Explicit out of scope: the Validate Level solver (parity support belongs to a Validation task or a decision-owner widening), other board objects, booster implementation (removals are exercised through the event and command API in tests)

## Verification and risk

- Focused test path/command: EditMode `BarrierRuleTests` (acceptance 1–4 and the Domain half of 6 with the BoardFixture), `BarrierEffectLevelTests` (conversion and fresh rules), `BarrierSessionTests` (session integration with fakes, acceptance 1, 2 and 5); PlayMode `BarrierEffectViewTests` (the view with the real `EnvironmentView`: snap at bind, one animated call per flip, N flips in one sync, Suspend and Teardown, Restart snap, 0 B)
- Required execution preflight: `unity-cli` project readiness (the retired `unity-preflight` routes there)
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: `unity_compile_check.py` over every changed and added `.cs` (Domain, Gameplay and both test assemblies)
- EditMode: acceptance 1–6 (Domain and session halves)
- PlayMode: the view with the real `EnvironmentView`
- Unity test runs: deferred to the orchestrator's one combined batch run (user order 2026-09-28); this owner runs none
- Manual/built-player/profiler/device evidence: a Game-scene check of a Barrier level (after the runtime flag), or user acceptance
- Risk: medium (a new Domain rule and a Gameplay module and view; one cross-task registry flag)
- Approval already granted (exact target only): execution — the user's order of 2026-09-28 ("delegate the unfinished tasks to subagents"), relayed by the orchestrating session "Görevleri inceleme ve dağıtma [8ea911]", is recorded as the user's go for this contract. No scene, prefab, asmdef, package or serialized-asset target is needed or approved.
- Commit/checkin constraint: forbidden
- Current invocation permission: implementation by "Görevleri inceleme ve dağıtma [8ea911] / barrier-rule-owner" inside the allowed paths
- Handoff destination: the orchestrating session "Görevleri inceleme ve dağıtma [8ea911]" (combined batch test run), then /task-cycle TASK-20260925-103527585-3d1b0a34-barrier-rule

## Stop conditions

- The rule would require a change in `Runtime/Domain/Movement`, `Runtime/Domain/Exit`, `Runtime/Domain/Session`, `Runtime/Domain/Commands` or `Runtime/Gameplay/Session` (core must stay closed).
- The schema does not expose barrier cells and `startsOpen` as typed data.
- The view would need a new public interface or a change to `IBlockEffectModule`, `IBlockEffectView` or `BlockEffectViewContext` (medium-risk public contract: report the smallest change instead).
- The runtime flag would be flipped without barrier solver support.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:9c59fafb1b4ccb5a3d0fd4eb2f8414f93513c05368cd99377a69678be31f4c06
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 1
- Last feedback: none
- Blocker: WAITING_FOR_OWNER — `GameSceneInstaller.cs` module line with `GameSceneArrowTests.cs` (Arrow task a3ffdc40, live lock, session "Edit dropdown ve colors kareleri [c818e4]") and the `PROJECT.md` DEC-163 row (file edited at 10:52 and 11:02); DECISION — `k_barrierRuntimeImplemented` needs barrier solver support (DEC-144, `LevelSupportGateTests.EveryRuntimeImplementedEffectHasSolverSupport`)
- Next action: None
- Closed revision: cs:23 (Plastic workspace head, unreviewed)
- Closed at: 2026-09-29T01:16:19Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Closed barrier stops blocks; opens after a clear | UNPROVEN | pending automated EditMode (orchestrator batch): BarrierRuleTests.AClosedBarrierStopsADragAndAMoveUntilOneBlockClears, BarrierSessionTests.AClosedBarrierStopsTheDragUntilADoorExitOpensIt | none |
| AC-2 Closing under a block leaves it; no entry until open | UNPROVEN | pending automated EditMode (orchestrator batch): BarrierRuleTests.ABarrierClosingUnderABlockKeepsItAndLetsOnlyItLeave, BarrierRuleTests.ARocketPartOnAClosedBarrierMayLeaveAndNoOtherBlockEnters, BarrierSessionTests.ABarrierClosingUnderABlockKeepsItAndOnlyThatBlockMayLeave | none |
| AC-3 N removals flip N times; Stay does not flip | UNPROVEN | pending automated EditMode and PlayMode (orchestrator batch): BarrierRuleTests.AVacuumOfNBlocksFlipsNTimesAndAHammerOnce, BarrierRuleTests.AStayExitAMoveAndASplitFlipNothing, BarrierEffectViewTests.AVacuumOfTwoBlocksMakesTwoCallsAndEndsWhereItStarted | none |
| AC-4 Closed barrier in a swept exit path refuses the exit | UNPROVEN | pending automated EditMode (orchestrator batch): BarrierRuleTests.AClosedBarrierInAConcaveSweptExitPathRefusesTheExit | none |
| AC-5 Restart restores startsOpen; view snaps | UNPROVEN | pending automated EditMode and PlayMode (orchestrator batch): BarrierEffectLevelTests.EveryRuleStartsAtTheAuthoredStateSoRestartRestoresIt, BarrierSessionTests.RestartBuildsAFreshRuleAtItsStartState, BarrierEffectViewTests.RestartSnapsEveryBarrierBackToItsStartStateWithoutAnimating | none |
| AC-6 Zero allocation and one view call per flip | UNPROVEN | pending automated EditMode and PlayMode (orchestrator batch): BarrierRuleTests.TheGateAndTheFlipDispatchAllocateNothing, BarrierSessionTests.APlayingTickThatDragsAcrossABarrierAllocatesNothing, BarrierEffectViewTests.EveryFlipMakesOneAnimatedCallInTheViewSyncPhase, BarrierEffectViewTests.TheFlipDispatchAndTheViewSyncAllocateNothing | none |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — draft from the "Ruled tile level sistem" session's request relaying the user's Barrier decision; waits for the board-objects schema handoff
- ready — planning — schema handoff details received (LevelEffectRegistry.k_barrierRuntimeImplemented, CellObject API, startsOpen validation); queued behind the schema close and the Gameplay integrations; execution needs the user's go
- ready — planning — contract revision: dependency gates on the schema owner's "schema final" message instead of the schema task closing (that task stays open for its device build); fingerprint recomputed
- ready — planning — "schema final" received for f7bfcf6a (AC-1..AC-6 pass, no schema change pending). Verified in the files: `LevelEffectRegistry.k_barrierRuntimeImplemented` at line 51 (read by the Barrier definition at line 261); `LevelData.CellObjectCount`/`GetCellObjectAt`; `LevelCellObjectData.Id`, `CellCount`, `GetCellAt(i)`, `TryGetEffect<BarrierObject>`; `BarrierObject.StartsOpen`. Remaining gates: the DI and runtime-shapes integrations, `SetBarrierOpen` from 08cc082b before the adapter step, and the user's go
- ready — planning — later facts, recorded here; the authority text above is not yet revised:
  - Board-art DI released.
  - The barrier view landed (08cc082b): `EnvironmentView.Build(layout, level)`, `SetBarrierOpen(id, open, animate)`, `SetBarrierTweensPaused`, `StopBarrierTweens`, `Clear`.
  - `startsOpen` stays (user clarification relayed 2026-09-25). The solid-block obstacle is removed.
  - The move-commands task (f1e30581, cs:9) changed the core this rule plugs into. Board changes now run through `CommandExecutor`/`MoveBlockHandler`, and hooks are asked about `PreviewBoard` views. `BarrierRule.AllowsCell` must read occupancy only from the `IBoardReader` it is passed. XP-3/XP-5 and the events are unchanged.
  - The contract needs a revision before execution:
    1. Allow `Gameplay/Composition/GameAttemptLoader.cs`, which must call `Build(layout, data)`.
    2. Allow `Gameplay/Board/BoardPresenter.cs` to forward Suspend and Teardown to the barrier tweens (`SetTweensPaused` to `SetBarrierTweensPaused`, `StopTweens` to `StopBarrierTweens`), so the session stays unchanged.
    3. Replace the dependency on the DI release with the runtime-shapes release.
- 2026-09-28 — Contract change (revision for DEC-157/DEC-158 and go recorded) — in_progress — by "Görevleri inceleme ve dağıtma [8ea911] / barrier-rule-owner" under the lifecycle lock:
  - Paths rebased onto DEC-157 (`docs/Folder-Layout.md`) and narrowed to exact new files; base revision cs:17.
  - The hook-source design is replaced by the DEC-158 module architecture: `BarrierEffectModule` in the existing `IBlockEffectModule` list (last, after Ice and Arrow), `BarrierEffectLevel` metadata read on Home, a fresh `BarrierRule` per attempt through `AddHooks`, and `BarrierEffectView` through `CreateView`. The module takes the scene's `EnvironmentView` in its constructor (the `IceEffectModule` material pattern), so no public interface and no `IBlockEffectModule`/`BlockEffectViewContext` change is needed. `BarrierRule` moves to `Runtime/Domain/Hooks` beside the other effect hooks.
  - The three recorded revision items were checked against the current code: `GameAttemptLoader` already calls `Build(layout, data)` and `BoardPresenter` already forwards Suspend and Teardown to effect views, so neither file is allowed any more; the runtime-shapes release has landed.
  - Rocket parts (DEC-146) and Vacuum multi-removal (DEC-147) are accounted for in Expected behavior 1.
  - New finding: flipping `k_barrierRuntimeImplemented` fails `LevelSupportGateTests.EveryRuntimeImplementedEffectHasSolverSupport` (DEC-144) until the Validate Level solver models barriers, which this contract keeps out of scope. The flip is gated and waits for the decision owner.
  - Execution approval: the user's order of 2026-09-28 ("delegate the unfinished tasks to subagents") is recorded as the user's go for this contract.
  - Acceptance and objective wording unchanged; decision row DEC-161 reserved by the orchestrator for this task; contract fingerprint recomputed.
- 2026-09-28 — implementation — in_progress (partial) — by "Görevleri inceleme ve dağıtma [8ea911] / barrier-rule-owner":
  - Added `Runtime/Domain/Hooks/BarrierRule.cs`, `Runtime/Gameplay/Effects/BarrierEffectLevel.cs`, `BarrierEffectState.cs`, `BarrierEffectModule.cs` and `BarrierEffectView.cs`; tests `Tests/Gameplay/EditMode/Domain/BarrierRuleTests.cs`, `Tests/Gameplay/EditMode/Gameplay/BarrierEffectLevelTests.cs`, `BarrierSessionTests.cs` and `Tests/Gameplay/PlayMode/BarrierEffectViewTests.cs`; a Barrier entry, a Loops row and a test line in `docs/Gameplay-Drag-And-Gate.md`. Unity creates the `.meta` files on import; nothing is added to Plastic.
  - `unity_compile_check.py` over all nine files: COMPILE_OK (Domain, Gameplay, Gameplay.EditModeTests, Gameplay.PlayModeTests).
  - Held in `pending-hunks.md`, applied by no one yet: (1) the `GameSceneInstaller` module line, which would turn `GameSceneArrowTests` red (exactly two modules) while the Arrow task verifies; (2) that test's count, outside this contract; (3) the runtime flag, gated on solver support and on (1); (4) the `PROJECT.md` row. DEC-161 was used by another session at 10:52 and DEC-162 by "Level content updates", so the row takes DEC-163. Expected behavior 6 now names DEC-163; contract fingerprint recomputed.
  - No Unity test was run (user order 2026-09-28: one combined batch by the orchestrator). Acceptance rows stay UNPROVEN with their fixtures named.
- 2026-09-29T01:16:19Z — Closed (user confirmed) — closed — explicit user order 2026-09-29 ("açık taskları da kapat üstünde çalışılmayanları artık"); prior state in_progress / implementation partial; waiting for the GameSceneInstaller.cs owner (the Arrow task a3ffdc40, whose GameSceneArrowTests counts the modules) and for PROJECT.md to settle; the runtime flag waits for the solver-support decision; Unity tests deferred to the orchestrator's combined batch run (user order 2026-09-28). Closure mode user_confirmed: every acceptance criterion result, blocker note and defect row is preserved as recorded, not verified by this closure.
<!-- TASK-LIFECYCLE:END -->
