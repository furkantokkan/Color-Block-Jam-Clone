# TASK-20260925-012336316-a88e25db-block-drag-gate-exit — Block drag and colour-door exit

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-012336316-a88e25db-block-drag-gate-exit
- Supersedes: none
- Title: Block drag and colour-door exit
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: feature
- Area: Gameplay core (board domain, level session, drag input, door exit)
- Workflow: direct

## Objective and baseline

- Observable objective: In the Game scene a player presses on or near a block, drags it cell by cell through free cells, and a block pushed against a door of its own colour whose span covers the block's width leaves the board through that door. The last exit wins the level.
- Current behavior/evidence:
  - User request 2026-09-25: "prd de buluan piece sürükleme ve gate mekaniklerini yap referans projeyi de inceleyebilirsin kuralara uymalı ve solid olmalı sistem" ("Implement the PRD's piece dragging and gate mechanics. You may study the reference project. It must follow the rules and the system must be SOLID.").
  - No session, state machine, tick owner, input, occupancy, move or exit code exists. A grep for `IOnityTickable|Session|InputAction|Occupancy|Exit` in runtime code finds nothing.
  - The Game scene builds only the environment through the `EnvironmentPreview` scaffold ("the level-session task replaces it"). No block is spawned at runtime.
  - No collider, `.inputactions` asset or EventSystem exists.
  - User decisions 2026-09-25 (approval questions):
    - Architecture approved: new `ColorBlockJam.Domain` and `ColorBlockJam.Gameplay` assemblies plus two test assemblies.
    - The Game scene step waits until board-camera-framing releases `Game.unity`.
    - Gate scope is the base colour door plus hooks XP-1..XP-5; catalog GTE effects are out of scope.
    - The held white outline and door cube fragments are wanted as full shader work modelled on the CBJ Template reference. They go to a separate follow-up contract after the reference study and exact target approval.
- Expected behavior:
  1. **Domain.** `ColorBlockJam.Domain` (`noEngineReferences`) holds the rules:
     - `LevelModel`, `BoardState` occupancy, `MoveRules`, `DragResolver`, `ExitRules`, `FollowOffset`, `BlockPicker`.
     - The session transition table and state machine, `ICountdown`.
     - The hooks XP-1..XP-5 with `AttemptHooks`, and the four board events with `BoardEventLog`.
     - All of them follow the design listed under References.
  2. **Movement.** A block moves only while Playing, cell by cell on both axes.
     - The dominant axis moves first and a tie goes to X. A blocked axis slides on the free one.
     - The block stops at the first blocked cell (block, edge, void, unusable door) and never passes through anything.
     - Its logical origin is always an integer cell. Release keeps the last reached cell and the view settles there.
  3. **Exit.** A flush block exits when all of these hold:
     - the pointer pushes outward by at least 0.3 cell;
     - the door colour matches;
     - the block's whole extent along that side lies inside one door span;
     - the outward swept path is free.

     The exit commits in logic at once: cells are freed, the status becomes Exited, and `BlockExited` is logged and published. A wrong-colour or too-narrow door acts as a wall. A block exits exactly once, and an exited block cannot be picked.
  4. **Picking.** Pointer-down inside the board interaction area (interior, walls, gates, border ring) selects the direct hit, or else the block whose projected occupied footprint is nearest in screen space.
     - Ties go to the lower authored index, and exited blocks are excluded.
     - Picking runs once per press. The grab offset is kept and the block is never retargeted.
     - Presses over UI Toolkit UI or outside the area select nothing.
  5. **Session and tick.**
     - `LevelSession` is the only owner of state (Home/Playing/Paused/Won/Failed), the transition table, the per-attempt CTS and `CompositeDisposable`, and Suspend/Teardown/Finish.
     - `GameplayTickOwner` is the project's only `IOnityTickable`. It runs commands, then the Won check, then the timer slot (`InactiveCountdown`), then view sync, and only while Playing.
     - Won latches when no required block remains after the command phase. The final exit motion runs for at most 0.8 foreground seconds before Teardown.
     - Backgrounding suspends the attempt. On foreground the interim `NoPauseOverlay` auto-resumes until a pause UI exists.
  6. **Input.** A code-built `InputActionMap "Gameplay"` uses `<Pointer>/position` and `<Pointer>/press`.
     - It is polled inside the tick and enabled only while Playing.
     - It follows the primary pointer only and handles pointer cancel.
  7. **Presentation.** `BoardPresenter` is the single construction site of `BlockPieceFactory`.
     - It spawns block roots under the EnvironmentView BoardRoot and follows the held block with a lead of at most 0.5 cell toward free cells (0.3 toward a usable door).
     - It moves an exiting block through its door with a clip plane at the door line (`ModularBlockView.SetClipPlane`) and returns pieces to the pool.
     - Tweens target piece Transforms, pause on Suspend, and stop on Teardown.
  8. **Composition.** `GameSceneInstaller` binds everything in the Game `SceneContext`:
     - `LevelSession`, `GameplayTickOwner`, `BoardPresenter` and `GameplayPointerInput` are `AsSingle`, so they are disposed with the scene.
     - It registers `GameAttemptLoader`, which awaits the project scope, builds the attempt on Home, frames `BoardCamera` and plays (interim until Hub selection).
     - `ApplicationPauseRelay` forwards OS backgrounding to the session.
  9. **Scene step** (queued, separate approval already given for its timing). In the live Editor:
     - add the gameplay scope with `GameSceneInstaller` and `ApplicationPauseRelay` to `Game.unity`;
     - clear `EnvironmentSceneInstaller.m_preview`;
     - remove the `EnvironmentPreview` component.
  10. **Documentation.** `docs/Gameplay-Drag-And-Gate.md` records the architecture, loops/owners/stop points and deferred items. PROJECT.md gets decision rows DEC-103..DEC-110 and one assembly-boundary bullet.
- Behavior that must remain unchanged:
  - Environment composition, `EnvironmentView`, `BoardLayout`, the level JSON and `LevelData`.
  - `BlockPiece`/`BlockPieceFactory` behaviour, piece prefabs and the board material/shader.
  - The Init/Hub/Loading scene flow.
  - Existing EditMode and PlayMode suites stay green. No existing asmdef, package or project setting changes.

## Ownership

- Owner/writer role: gameplay session (this conversation) with sequential implementer agents; independent verifier/reviewer afterwards
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Domain/**
  - Assets/ColorBlockJam/Gameplay/**
  - Assets/ColorBlockJam/Tests/Gameplay/**
  - Assets/ColorBlockJam/Domain.meta
  - Assets/ColorBlockJam/Gameplay.meta
  - Assets/ColorBlockJam/Tests/Gameplay.meta
  - docs/Gameplay-Drag-And-Gate.md
  - PROJECT.md
  - Assets/ColorBlockJam/Scenes/Game.unity
  - production/tasks/TASK-20260925-012336316-a88e25db-block-drag-gate-exit/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Editor/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Art/**
  - Assets/ColorBlockJam/Tests/EditMode/**
  - Assets/ColorBlockJam/Tests/PlayMode/**
  - Assets/ColorBlockJam/Tests/Environment/**
  - Assets/ColorBlockJam/Tests/LevelPreview/**
  - Assets/ColorBlockJam/Scenes/Init.unity
  - Assets/ColorBlockJam/Scenes/Hub.unity
  - Assets/ColorBlockJam/Scenes/Loading.unity
  - Assets/ColorBlockJam/Scenes/LevelEditor.unity
  - Editing Game.unity before board-camera-framing releases it, or saving it with unrelated unsaved changes
  - Adding a `.inputactions` asset, a collider, a package or an `activeInputHandler` change
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Scenes/Game.unity
    Sole writer: gameplay session through the live Editor (MCP HTTP), only after board-camera-framing's stable handoff and only when the scene has no unrelated unsaved changes
    Matching `.meta` owner: unchanged (no `.meta` edit)
  - Asset: new `.asmdef` files and `.meta` files under Assets/ColorBlockJam/Domain/**, Gameplay/** and Tests/Gameplay/** (generated by Unity on import)
    Sole writer: gameplay session
    Matching `.meta` owner: gameplay session
- Other active writers/worktrees checked:
  - board-camera-framing (`TASK-20260924-224137662-d526d5ad-board-camera-framing`) is actively writing `Environment/BoardCamera*.cs` and `EnvironmentPreview.cs`, and will write `Game.unity`. This task reads `BoardCamera` only and queues its scene step.
  - play-area-environment (in_progress) owns Environment and Game.unity for verification.
  - board-kit (ready_to_close) and board-art-dependency-injection (ready) own Presentation/Board and the board shader. The factory constructor stays in one place (`BoardPresenter`).
  - level-json-v2-effects owns `LevelData`. It is read only here.
  - No other contract claims Domain/, Gameplay/ or Tests/Gameplay/.
- Execution dependency: code and tests: none. Scene step (expected behavior 9): TASK-20260924-224137662-d526d5ad-board-camera-framing reaches a stable verifier handoff and releases Game.unity.
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given a board with a block and a wall, a void or another block in its way, when the pointer drags past the obstacle (including a jump of several cells in one frame), then the block stops at the last free cell and never overlaps or passes through. A diagonal drag moves the dominant axis first and slides along a blocking wall. On release the block rests on its reached cell (RULE-9..11, AC-3.2, AC-3.3, V-E8).
2. Given a flush block beside a door of its colour whose span covers the block's extent, when the pointer pushes at least 0.3 cell outward, then the block exits once: cells are freed, `BlockExited` is published, and the block is excluded from picking. A wrong-colour door, a too-narrow or partly covering door, a door without flush contact, or a blocked concave path does not exit and acts as a wall. A push under 0.3 or a release while flush does not exit (RULE-12..14, AC-3.4, V-E9, DEC-104).
3. Given presses on a block, a wall, a gate, the border ring or an empty cell, then the direct hit or the nearest projected occupied footprint is chosen once. Ties go to the lower authored index, exited blocks are excluded, and an empty board selects nothing. A press over UI or outside the area never starts a drag (AC-3.13, AC-3.19..3.21, V-E27).
4. Given the session, then every row of the PRD 7.5 transition table works and every other request is rejected. After Won, commands and ticks change nothing and a second terminal request is ignored. A last exit plus timer zero in the same tick gives Won (AC-5.6, RULE-15..18, V-E1, V-E3, V-E4, V-E5, V-E16).
5. Given Suspend and Teardown, then their fixed steps run in the PROJECT.md order:
   - pointer disabled and drag interrupted;
   - clocks paused or stopped;
   - tweens paused or stopped;
   - CTS cancelled;
   - disposables disposed;
   - pieces returned.

   Resume continues without replaying, and a deferred `MoveCommitted` is published first. Restart rebuilds a board equal to a fresh one (V-E11, V-E26, AC-5.3).
6. Given the final exit, then Won latches in that tick, input stops immediately, the exit motion finishes, and Teardown completes within 0.8 foreground seconds. Backgrounding during Finish forces cleanup at once (AC-4.12, DEC-93).
7. Given the project, then:
   - exactly one `IOnityTickable` exists;
   - no Domain/Gameplay type declares Update/FixedUpdate/LateUpdate or `async void`;
   - Domain references no engine or Onity assembly;
   - Runtime references neither Domain nor Gameplay;
   - the steady drag tick, exit evaluation, picking, follow offset and hook publish allocate 0 B (NFR-L3, NFR-L6, NFR-11, V-E22, V-E23).
8. Given the presenter in PlayMode, then:
   - roots sit at `CellToLocal(origin)` under BoardRoot;
   - an exit plays a clipped tween on the piece Transform and returns the piece;
   - Teardown leaves `Tween.GetTweensCount(t) == 0` and the factory `ActiveCount == 0`;
   - rebuilding the same level instantiates nothing new (AC-3.16, V-M1, V-M20 partial).
9. Given the Game scene after the scene step, in Play Mode on level 1 at 1080×1920:
   - a block can be dragged to its door and exits through it, and the level ends in Won;
   - the console shows no error or warning from the new assemblies;
   - the result is verified by manual evidence (capture) or explicit user acceptance.

## References and constraints

- Story/GDD/ADR/design/log/image:
  - `docs/Color-Block-Jam-PRD.md` 6.4–6.6, 7.3, 7.5, 8.3, 9.2–9.4, 10, 16.
  - PROJECT.md lifecycle and Performance Rules.
  - `docs/Color-Block-Jam-Visual-References.md` references 14, 15 and 17.
  - Approved design `production/tasks/TASK-20260925-012336316-a88e25db-block-drag-gate-exit/design/final-design.md` plus `final-design-2.md` (part 2 overrides part 1 where stated).
  - CBJ Template behaviour reference (`BlockMovementManager`, `EnvironmentSpawner.NearGates`, `LevelController.TryToCollectBlock`), not copied.
- Unity version/platform: 2022.3.62f2; Android/iOS portrait 1080×1920 and 20:9
- Package/architecture constraints:
  - C# 9 only; Onity DI/Reactive; PrimeTween target overloads; Input System only.
  - No LINQ in Domain/Gameplay; one tick owner; nothing runs unattended; zero allocation in the Playing tick.
  - Block roots only through `BlockPieceFactory`; no Instantiate or Destroy during an attempt.
  - Every class carries a one-sentence responsibility summary; m_/s_/k_ naming.
- Explicit out of scope:
  - GTE-01..11 gate effects.
  - Ice/Arrow registrations.
  - The countdown timer and Failed flow.
  - Pause, result and pre-level UI and HUD.
  - Save/reward and Hub level selection.
  - The held white outline and cube fragments: separate follow-up contract with exact shader/material/prefab targets.
  - Editor validation solver.
  - Removing the `EnvironmentPreview` script file (environment owner).

## Verification and risk

- Focused test path/command: `Assets/ColorBlockJam/Tests/Gameplay/EditMode` (namespace `ColorBlockJam.Tests.Gameplay`) and `Assets/ColorBlockJam/Tests/Gameplay/PlayMode`, run through MCP for Unity `run_tests`; the out-of-Editor harness `scratchpad/harness/build.sh all` compiles every slice first
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean recompile in the live Editor after a green harness build; 0 errors and 0 warnings from `ColorBlockJam.Domain`, `ColorBlockJam.Gameplay` and their tests
- EditMode: acceptance 1–5 and 7 (domain, session with fakes, picker, pointer input with `InputTestFixture`, architecture and zero-allocation tests)
- PlayMode: acceptance 6 and 8 (presenter lifecycle, Finish deadline); regression Environment and Presentation PlayMode suites
- Manual/built-player/profiler/device evidence: acceptance 9 (Game scene capture or user acceptance); device 0 B and feel checks stay UNPROVEN (V-M5, OQ-8)
- Risk: medium (new assemblies and public contracts) for code; high for the Game.unity scene step
- Approval already granted (exact target only): user answers 2026-09-25 — the new Domain/Gameplay/test assemblies and their references (including `Unity.InputSystem`); the Game.unity scene step after board-camera-framing releases the scene
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260925-012336316-a88e25db-block-drag-gate-exit, then an independent verifier/reviewer

## Stop conditions

- A slice cannot compile in the harness without editing a forbidden path.
- A requirement would need an edit to Runtime/Presentation/Environment sources, prefabs, the board material/shader, packages or project settings.
- The live Editor is compiling, running tests or in Play Mode for another session when this task needs it (wait, do not interrupt).
- Game.unity has unrelated unsaved changes or another live writer at scene-step time.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:81f2264e3bfbd61bc9ab982d0bad8aae4fed1349a17e82e114574d5441db0255
- Evidence fingerprint: sha256:dfcb391af03201cb31adc69b627ebc546358b50b2ad22bbd0d4822c80e51cc79
- Reviewed revision: vcs:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b7f051e073b1fa2fd64185f466461df91d2f01c4ea977bbd79c9874cd7317e3c
- Attempt count: 2
- Last feedback: Architecture review found terminal observer reentrancy and disposed-session work acceptance (D-001..D-003).
- Blocker: none
- Next action: None
- Closed revision: vcs:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b7f051e073b1fa2fd64185f466461df91d2f01c4ea977bbd79c9874cd7317e3c
- Closed at: 2026-09-28T08:46:45Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Cell-by-cell movement, no overlap or pass-through, rest on reached cell | PASS | independent Gameplay EditMode 282/282; DragResolverTests, MoveRulesTests, LevelSessionTests; job 4c0a6d4d772c47dba919266488eef498 | vcs:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b7f051e073b1fa2fd64185f466461df91d2f01c4ea977bbd79c9874cd7317e3c |
| AC-2 Colour-door exit rule and exactly-once commit | PASS | independent Gameplay EditMode 282/282; exit, command and follow-offset tests; job 4c0a6d4d772c47dba919266488eef498 | vcs:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b7f051e073b1fa2fd64185f466461df91d2f01c4ea977bbd79c9874cd7317e3c |
| AC-3 Nearest-footprint picking and UI/area gating | PASS | independent Gameplay EditMode 282/282; picker, pointer input and session tests; job 4c0a6d4d772c47dba919266488eef498 | vcs:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b7f051e073b1fa2fd64185f466461df91d2f01c4ea977bbd79c9874cd7317e3c |
| AC-4 Transition table, terminal latch, same-tick Won | PASS | independent Gameplay EditMode 282/282; transition and LevelSessionTests including synchronous terminal observers; job 4c0a6d4d772c47dba919266488eef498 | vcs:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b7f051e073b1fa2fd64185f466461df91d2f01c4ea977bbd79c9874cd7317e3c |
| AC-5 Suspend/Teardown order, Resume, Restart rebuild | PASS | independent Gameplay EditMode 282/282; LevelSessionTests including Failed-observer Restart and all five disposed states; job 4c0a6d4d772c47dba919266488eef498 | vcs:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b7f051e073b1fa2fd64185f466461df91d2f01c4ea977bbd79c9874cd7317e3c |
| AC-6 Won Finish within 0.8 s and forced cleanup | PASS | independent PlayMode 19/19 including FinishDeadlineTests; job b585f1d023844e079397a9c3ef300f00; Won-observer regression in EditMode | vcs:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b7f051e073b1fa2fd64185f466461df91d2f01c4ea977bbd79c9874cd7317e3c |
| AC-7 Architecture and zero-allocation tests | PASS | independent Gameplay EditMode 282/282 including architecture and allocation regressions; job 4c0a6d4d772c47dba919266488eef498 | vcs:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b7f051e073b1fa2fd64185f466461df91d2f01c4ea977bbd79c9874cd7317e3c |
| AC-8 Presenter pooling, exit motion and tween cleanup | PASS | independent PlayMode 19/19 including BoardPresenterLifecycleTests; job b585f1d023844e079397a9c3ef300f00 | vcs:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b7f051e073b1fa2fd64185f466461df91d2f01c4ea977bbd79c9874cd7317e3c |
| AC-9 Game scene drag-to-door win on level 1 | UNPROVEN | required live Game-scene capture or explicit user acceptance still absent; historical automated GameSceneDragTests is not the manual channel | vcs:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b7f051e073b1fa2fd64185f466461df91d2f01c4ea977bbd79c9874cd7317e3c |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| [D-001](defects/D-001.md) | ac_failure:ac-6:won-observer-home-breaks-finish | VERIFIED | AC_FAILURE | AC-6 | A Won observer calls GoHome while Finish is being initialized. | Independent final EditMode 282/282 and PlayMode FinishDeadline coverage 19/19. | vcs:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b7f051e073b1fa2fd64185f466461df91d2f01c4ea977bbd79c9874cd7317e3c |
| [D-002](defects/D-002.md) | ac_failure:ac-5:failed-observer-restart-loses-attempt | VERIFIED | AC_FAILURE | AC-5 | A Failed observer calls Restart and the replacement attempt is torn down. | Independent final EditMode 282/282, synchronous Restart preserves its new attempt. | vcs:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b7f051e073b1fa2fd64185f466461df91d2f01c4ea977bbd79c9874cd7317e3c |
| [D-003](defects/D-003.md) | ac_failure:ac-5:disposed-session-accepts-work | VERIFIED | AC_FAILURE | AC-5 | A disposed session still accepts transitions/work, recreates attempts, or throws. | Independent final EditMode 282/282, all five disposed-state cases pass. | vcs:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b7f051e073b1fa2fd64185f466461df91d2f01c4ea977bbd79c9874cd7317e3c |

### Verification identity

- Review set: every file under Domain, Gameplay and Tests/Gameplay, their three root .meta files, docs/Gameplay-Drag-And-Gate.md, PROJECT.md, Game.unity, and the final PlayMode verification dependency Runtime/Composition/ProjectScopeInstaller.cs (307 files).
- Review digest: repository-relative paths sorted ordinally; UTF-8 path, NUL, raw file bytes (or `<missing>`), NUL; SHA-256. VCS: cs:10@ColorBlockJamCase2026@efurk64@unity.
- Evidence digest: LF-joined contract fingerprint, reviewed revision, acceptance rows in order, defect ID/status/mapping/verified-revision rows, then the following channel lines:
  - EditMode | PASS | VERIFY: PASS (282/282) | independent final Gameplay.EditMode assembly | job 4c0a6d4d772c47dba919266488eef498
  - PlayMode | PASS | VERIFY: PASS (19/19) | independent final FinishDeadlineTests, BoardPresenterLifecycleTests, ProjectScopeShapeCatalogTests | job b585f1d023844e079397a9c3ef300f00
  - Manual AC-9 | UNPROVEN | live Game-scene capture or explicit user acceptance pending
- Scope: only this task's source/test/doc changes are attributed here. Independent verification followed the separate ProjectScopeInstaller repair; no authored assets were written. External Editor tests are outside this result and are not claimed green.

### Transition history

- 2026-09-26 — reproduction — NEW_DEFECT D-001, D-002, D-003; cycle attempt 2; repository-local atomic claim with manual team serialization; Unity preflight READY WITH WARNINGS; domain workflow unity-game-dev; scope limited to LevelSession and its tests, no serialized assets.
- Created — planning — contract authored from the approved drag/gate design and the user's 2026-09-25 approval answers
- in_progress — implementation — direct /implement-task started (preflight READY WITH WARNINGS: CLI cannot reach 2022.3, MCP for Unity HTTP used; task-local lifecycle claim)
- in_progress — verification — S1-S3 implemented, 4-lens review with 9 confirmed fixes, IAttemptHookSource port; isolated batch EditMode 387/387 and PlayMode 38/38 (incl. Game-scene drag-to-Won); Game.unity wired (3e35a928…); decision IDs renumbered to DEC-110..DEC-117 (DEC-103 taken); AC-9 awaits user acceptance
- in_progress — verification — exit swept path now asks XP-3 cell gates (Barrier readiness) and zero-allocation warm-up; Gameplay EditMode 215/215, PlayMode 11/11; reviewed revision 38551da1…; AC-9 still awaits user acceptance
- in_progress — verification — BoardPresenter prepares exact reachable piece capacity (block-runtime-objects request); Gameplay EditMode 215/215, PlayMode 38/38; reviewed revision f1a1eceb…; AC-9 still awaits user acceptance
- in_progress — verification — the owned core changed after review:
  - The move-commands task (f1e30581, cs:9) turned the drag into a preview plus one MoveBlock command.
  - The board-art DI (e361499a) changed the BoardPresenter constructor.
  - Solid-block handling was removed from `AttemptLevelConverter` on the user's decision relayed by the schema owner. That change is local and not checked in.
  - The AC-1..AC-8 suites ran green on the merged code: Gameplay EditMode 273/273 (272 after the solid-block test was removed) and PlayMode 55/55, including `GameSceneDragTests`.
  - The reviewed revision f1a1eceb… is stale and must be recomputed at closure. AC-9 still awaits user acceptance.
- 2026-09-26 — reproduction/fix — baseline job 948f80577042481bb5257d123bf32b57: 21 existing session cases passed, seven new cases failed. Terminal notifications preceded cleanup/Finish initialization and disposed entry points lacked guards; surgical repair and regression tests yielded FIXED_UNVERIFIED (28/28, job 54fc240311ab4a0d9204d7ea408a3e2b).
- 2026-09-26 — independent verification — final VERIFY: PASS (282/282), job 4c0a6d4d772c47dba919266488eef498; VERIFY: PASS (19/19), job b585f1d023844e079397a9c3ef300f00. D-001..D-003 VERIFIED at the reviewed revision above. AC-1..AC-8 PASS; AC-9 UNPROVEN, so the task stays in_progress and is not close-ready.
- 2026-09-28T08:46:45Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
