# TASK-20260925-151032072-f1e30581-move-commands — Move commands (every move is one command)

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-151032072-f1e30581-move-commands
- Supersedes: none
- Title: Move commands (every move is one command)
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:7
- Task kind: refactor
- Area: Gameplay core (board changes, drag input, session command phase)
- Workflow: direct

## Objective and baseline

- Observable objective: Every player move is one plain-data command, and one executor applies it. The pointer, a replay and any other system submit the same command. Each attempt logs its applied commands, so a later task can save them as JSON. The same level and the same command list give the same board and the same board events.
- Current behavior/evidence:
  - User order 2026-09-25: "oyunda command patern olmalı yani her hamle komut olmalı böylece json'a kaydedebiliriz ileride hamleleri ve ayrıca başka bir sistem ile de oynatabiliriz buna göre ayarla sistemi" ("the game should use the Command pattern: every move must be a command, so we can save the moves to JSON later and also play them with another system; adjust the system accordingly"). Follow-up: "işin bitince bunu commit atabilirsin command sistemi diye" ("when you finish, you can commit this as 'command system'").
  - Today `DragResolver.Update` writes the board itself. It calls `BoardState.Move` on every validated cell step during the drag, and it frees the cells on an exit. Only the events describe a move. There is no command object, no log, and no way for another system to submit a move (checked-in core: cs:7).
  - The PRD already treats boosters as session commands applied in the command phase (RULE-18, RULE-31). The catalog's unit of play is "one solver transition equals one committed move" (BLK-12), and a move is a drag release that changed the board (catalog section 1, DEC-113).
  - Design: three designs were written and each was attacked by two critic lenses. The files are `design/commands-A-move-level.md`, `commands-B-step-level.md` and `commands-C-hybrid.md` in the session scratchpad; the workflow is `wf_b5ce8419-880`. The decision owner chose A (one move = one command, commit on release or push-out), trimmed to the current scope (see Expected behavior).
- Expected behavior:
  1. **Commands.** `Domain/Commands` holds the following types:
     - `GameCommand`: a readonly struct with `Kind`, `Block`, `From`, `To` and `Option`. `Option` carries the exit side for MoveBlock.
     - `CommandKind`: `MoveBlock` only now; boosters add their own kinds later.
     - `CommandOutcome`: Applied or a refusal reason.
     - `ICommandHandler`.
     - `CommandExecutor`: one handler per kind, fixed when the attempt is built. It is the only place that runs handlers.
     - `CommandLog`: the attempt's applied commands in order. Its array is allocated when the attempt is built; a full log marks itself truncated and never grows.
     - `ICommandSource`.
     - `ReplayCommandSource`.
  2. **MoveBlock handler.** It validates the whole move before changing anything:
     - the block is on the board at `From`;
     - the drag gates allow it (XP-1);
     - the move stays on the allowed axes (XP-2), and an exit side lies on an allowed axis;
     - `To` is reachable in 4-connected steps, where each step is today's `MoveRules.CanOccupy` check with the block shown at the previous origin (RULE-10, XP-3). `MoveReach` finds this with a breadth-first search over buffers allocated when the attempt is built;
     - an exit fits `ExitRules` at `To` (XP-4).

     Only then does the handler move the block once. It raises exactly one event: `MoveCommitted`, or `BlockExited` with Leave or Stay (DEC-113). A refusal changes nothing.
  3. **The drag as a preview.** `DragResolver` keeps today's stepping, clamping, rounding, push threshold and follow lead (DEC-111, DEC-112). It runs over a `PreviewBoard`, an `IBoardReader` that shows one block at another origin exactly as `BoardState` would after `Move`, and never writes the board. A release that changed the board, or a push-out through a fitting door, becomes one MoveBlock command. An interruption (Suspend, Teardown) parks that command; the next command phase after Resume executes it first, and Teardown discards it (DEC-113 unchanged).
  4. **Single writer.** `BoardState.Move`, `Release` and `Reset` become internal to `ColorBlockJam.Domain`. The Gameplay tests reach them through `InternalsVisibleTo`. Only command handlers change occupancy during play.
  5. **Command phase.** It runs in this order:
     1. the parked move;
     2. the pointer: `DragInputRouter` executes the command it produced before it releases the held mark;
     3. an optional `ICommandSource`, which is polled only while no drag is active or parked and the board is not cleared, at most one command per tick. It receives the outcome of each command.

     `LevelSession.TrySetCommandSource` attaches a source to the current attempt, and Teardown drops it.
  6. **Presentation.** `BoardPresenter` reads the preview board for positions: the held block follows its preview origin, and a paused mid-drag block stays at its parked cell. A block moved by a non-pointer command settles onto its new cell.
  7. **Docs and decision.** `docs/Gameplay-Drag-And-Gate.md` gets a Commands section, and PROJECT.md gets one DEC row. Block identity in commands is the authored block index. The later JSON task maps it to the authored string ID and adds the level identity and the rules version.
- Behavior that must remain unchanged:
  - The drag feel: follow lead 0.5 toward a free cell and 0.3 toward a usable door, push threshold 0.3, bounded steps, no tunnelling, release keeps the reached cell.
  - Picking and UI gating.
  - The Suspend, Teardown and Won Finish order, and the same-tick Won check.
  - One event per move, and a deferred `MoveCommitted` after Resume.
  - 0 B in the Playing tick.
  - Level 1 still wins through the real Game scene.
  - Levels and hooks (XP-1..XP-5) behave as before.

## Ownership

- Owner/writer role: gameplay session, independent verifier afterwards
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Domain/Commands/**
  - Assets/ColorBlockJam/Domain/Board/PreviewBoard.cs
  - Assets/ColorBlockJam/Domain/Board/BoardState.cs
  - Assets/ColorBlockJam/Domain/AssemblyInfo.cs
  - Assets/ColorBlockJam/Domain/Movement/**
  - Assets/ColorBlockJam/Domain/Session/BoardSimulation.cs
  - Assets/ColorBlockJam/Gameplay/Session/**
  - Assets/ColorBlockJam/Gameplay/Board/BoardPresenter.cs (Sync, SetHeld and settling moves the pointer did not drag only)
  - Assets/ColorBlockJam/Tests/Gameplay/**
  - docs/Gameplay-Drag-And-Gate.md
  - PROJECT.md (one decision row)
  - production/tasks/TASK-20260925-151032072-f1e30581-move-commands/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Domain/Exit/**
  - Assets/ColorBlockJam/Domain/Hooks/**
  - Assets/ColorBlockJam/Gameplay/Composition/** (the board-art DI integration owns the installer)
  - Assets/ColorBlockJam/Gameplay/Levels/** (the runtime-shapes integration owns them)
  - JSON serialization of commands and booster handlers (later tasks)
  - Plastic checkin before every required gate passes, or of files outside this task's explicit list
- Serialized assets: none. New scripts get their `.meta` files from Unity import in the isolated copy, and those files are copied back with the scripts.
- Other active writers/worktrees checked:
  - The "Ruled tile level sistem" session holds the user-approved board-art DI and runtime-shapes integrations. Those touch `BoardPresenter.cs` (constructor, factory and field lines), `GameSceneInstaller.cs`, `ShapeResolver.cs`, `AttemptLevelConverter.cs` and the Tests/Gameplay fixtures. This task stages its change outside the project and writes only at one copy-back. It merges onto any version they have written, keeps their lines, and announces the copy-back.
  - `TASK-20260925-012336316-a88e25db-block-drag-gate-exit` (same session) owns the same core. Its verification moves to the post-change revision.
  - The queued Barrier rule (`TASK-20260925-103527585-3d1b0a34-barrier-rule`) forbids itself this core and runs after this task.
- Execution dependency: none (the peer's writes are merged at copy-back, not waited for)
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. **Single writer.** Given any board change during play, it happens only inside a command handler that `CommandExecutor` runs. A release that changed the board or a fitting push-out produces exactly one MoveBlock command, and `BoardState` mutators are unreachable from Gameplay code (architecture test).
2. **Validation.** A MoveBlock is refused with its reason and changes neither the board, the events nor the log when any of these holds:
   - the block is unknown or off the board;
   - its origin is stale;
   - a drag gate refuses it;
   - it leaves an allowed axis;
   - `To` is unreachable;
   - the exit does not fit;
   - it changes nothing.
3. **Preserved rules.** The existing movement, exit, follow, picking, session and pointer tests pass, adapted to the new API with the same expected values. One move raises exactly one event. A move interrupted by Suspend runs first after Resume, and Teardown discards it (DEC-113).
4. **Replay determinism.** The applied-command log of a pointer-played attempt, executed on a fresh simulation of the same level, gives the same final board and the same event sequence. The cases include a Leave exit, a Stay exit gate and a cell gate.
5. **External driver.** An attached `ReplayCommandSource` plays a level to Won through the session and receives each outcome. A refused command stops the replay and reports its index. Teardown and Restart drop the source.
6. **Zero allocation.** A Playing tick allocates 0 B with a held drag, a release command, a push-out command or a source command.
7. **Game scene.** Level 1 still wins through the real Game scene (`GameSceneDragTests`), and the presenter lifecycle tests pass.

## References and constraints

- Story/GDD/ADR/design/log/image:
  - PRD RULE-9..14, RULE-18, RULE-28..31 and section 8.3.
  - Catalog section 1 (events) and BLK-12.
  - DEC-110..117 and DEC-113.
  - `docs/Gameplay-Drag-And-Gate.md`.
  - The design files named above.
- Unity version/platform: 2022.3.62f2; Android/iOS
- Package/architecture constraints:
  - Domain has no engine references and no `System.Linq`.
  - C# 9 only.
  - One `IOnityTickable`.
  - No static state.
  - 0 B in the Playing tick; buffers are allocated when the attempt is built.
  - Structs are never boxed through interfaces in the tick.
- Explicit out of scope:
  - JSON files and the level identity header.
  - Booster commands and their journal rules.
  - Paced replay animation along the found path.
  - Multi-block units (Combined, Magnet).
  - A push queue for network input.

## Verification and risk

- Focused test path/command:
  - EditMode: `ColorBlockJam.Gameplay.EditModeTests`, covering command, handler, reach, preview board, replay and the updated drag and session tests.
  - PlayMode: `ColorBlockJam.Gameplay.PlayModeTests`.
  - Both run in the isolated batchmode copy.
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: the out-of-Editor harness (C# 9, Onity analyzers, warnings as errors), then a clean Unity compile in the isolated copy.
- EditMode: acceptance 1–6
- PlayMode: acceptance 7
- Manual/built-player/profiler/device evidence: none required. The drag feel is unchanged by construction and covered by the preserved tests. Device feel stays under OQ-8 of the drag task.
- Risk: medium (public contracts and the core data flow)
- Approval already granted (exact target only): the user's order to restructure moves as commands and to check the result in as "command system" once done (2026-09-25)
- Commit/checkin constraint: allowed once every acceptance scenario passes. The checkin uses an explicit list of this task's files, with no directory recursion. `PROJECT.md` and `Game.unity` stay out because they hold other sessions' pending work. Before checking in, check ListAgents for another busy commit session.
- Current invocation permission: execution and checkin, per the user's order
- Handoff destination: /implement-task TASK-20260925-151032072-f1e30581-move-commands

## Stop conditions

- A preserved drag, exit or session test needs a different expected value.
- A hook (XP-1..XP-4) would need to change its interface.
- The peer's integration changes the files above in a way that cannot be merged line by line.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: PASS
- Contract fingerprint: sha256:47e11550bf8d4b598e1a5039aafca31f5dcd31352aacd59b67cc3f0c132d13c2
- Evidence fingerprint: sha256:3a6d5ccb632747c6918021ef67192ab58cfd66b662781a2373d65255f6d975d2
- Reviewed revision: vcs:cs:8+owned-sha256:7459e3f45edf82daf799ded416ff03c3ab560963fa974c4d13a109467c6288db
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: none (check in as "command system" per the user's order)
- Closed revision: vcs:cs:8+owned-sha256:7459e3f45edf82daf799ded416ff03c3ab560963fa974c4d13a109467c6288db
- Closed at: 2026-09-25T18:45:08Z
- Closure mode: automatic
- Closed by: implement-task (gameplay session)
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Single writer through the executor | PASS | automated EditMode: GameplayArchitectureTests.OnlyDomainCommandHandlersCanChangeTheBoard, DragResolverTests (preview, one command) | vcs:cs:8+owned-sha256:7459e3f45edf82daf799ded416ff03c3ab560963fa974c4d13a109467c6288db |
| AC-2 Refusals change nothing | PASS | automated EditMode: MoveBlockHandlerTests, CommandExecutorTests | vcs:cs:8+owned-sha256:7459e3f45edf82daf799ded416ff03c3ab560963fa974c4d13a109467c6288db |
| AC-3 Preserved rules and DEC-113 | PASS | automated EditMode/PlayMode: adapted drag, exit, follow and session suites (same expected values), MoveReachTests seeded walk | vcs:cs:8+owned-sha256:7459e3f45edf82daf799ded416ff03c3ab560963fa974c4d13a109467c6288db |
| AC-4 Replay determinism | PASS | automated EditMode: ReplayTests (plain, Stay gate, board-reading cell gate), PreviewBoardTests | vcs:cs:8+owned-sha256:7459e3f45edf82daf799ded416ff03c3ab560963fa974c4d13a109467c6288db |
| AC-5 External driver | PASS | automated EditMode: CommandSourceTests | vcs:cs:8+owned-sha256:7459e3f45edf82daf799ded416ff03c3ab560963fa974c4d13a109467c6288db |
| AC-6 Zero allocation | PASS | automated EditMode: ZeroAllocationTests (drag, move, exit, release tick, push-out tick, source tick) | vcs:cs:8+owned-sha256:7459e3f45edf82daf799ded416ff03c3ab560963fa974c4d13a109467c6288db |
| AC-7 Game scene win and presenter lifecycle | PASS | automated PlayMode: GameSceneDragTests, BoardPresenterLifecycleTests | vcs:cs:8+owned-sha256:7459e3f45edf82daf799ded416ff03c3ab560963fa974c4d13a109467c6288db |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — from the user's command-pattern order; design A chosen after a three-design, six-critique workflow
- in_progress — implementation — lock claimed; preflight: exact project, Unity 2022.3.62f2 live Editor (PID 68220, untitled scene) left untouched, verification in an isolated batchmode copy; merge-at-copy-back agreed with the "Ruled tile level sistem" session
- closed — closed — design, review (7 confirmed findings fixed, independent re-verification), merged onto the board-art DI release; isolated EditMode 925/932 (Gameplay 273/273, 7 unrelated Editor failures) and PlayMode 55/55; live Editor compile clean; every AC PASS at the reviewed revision
<!-- TASK-LIFECYCLE:END -->
