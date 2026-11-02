# Evidence — TASK-20260925-012336316-a88e25db-block-drag-gate-exit

Reviewed revision: `vcs:cs:5+owned-sha256:b90aa2b16f2adb70c3cfa59c89f462ca6fb4a7a530914dcecc211ac8273c0a2c`

The review set has 259 files:
- `Assets/ColorBlockJam/Domain/**`, `Gameplay/**`, `Tests/Gameplay/**`, including every `.meta`;
- the three folder `.meta` files;
- `Assets/ColorBlockJam/Scenes/Game.unity`.

The identity is recomputed with `scratchpad/reviewed_revision.py` (session scratchpad). It uses sorted paths plus raw bytes, per `reviewed-evidence-identity.md`.

## Preflight and workflow

- `unity-preflight`: READY WITH WARNINGS.
  - Exact project and Unity 2022.3.62f2 were proven through MCP for Unity over HTTP (`mcpforunity://project/info`).
  - The Unity CLI cannot reach Editors older than Unity 6 (Pipeline), so it was not usable.
  - Other sessions' uncommitted work was preserved; none of it is in the owned paths.
- Domain workflow: `unity-game-dev`.
- The live Editor's MCP bridge stopped answering pings around 05:29 (WebSocket keep-alive failures in `Editor.log`). All Unity verification therefore ran in an isolated batchmode copy (`%TEMP%/cbj-draggate-4e3dc6`), never in the live Editor.

## Implementation and review

- Design: `design/final-design.md` (A–E, written by the Fable decision owner before it stopped on a usage-credit error) and `design/final-design-2.md` (section 0 corrections and F–I, by the main session). Slices are in `design/slices.md`.
- Implementers S1–S3 (Opus) wrote into a staging tree. Every slice compiled in the out-of-Editor harness (C# 9, netstandard2.1, Onity analyzers, warnings as errors).
- Review: four lenses (rules correctness, lifecycle/performance, SOLID/quality, tests) produced 23 findings.
  - One adversarial verifier ran per finding; 9 were confirmed and fixed. Notable fixes:
    - pointer device disambiguation;
    - a lost first press after Resume;
    - a settle tween to its own position;
    - `TryPrepare` could leave the session half-prepared;
    - the pause mapping was duplicated;
    - the board sentinels moved to `Occupant`;
    - the 0.8 s Finish cap had no test;
    - one tautological test.
  - The main session added the `IAttemptHookSource` port, so effects plug in without changing `LevelSession` (AC-EXT-1). One refuted-but-valid SOLID finding was acted on this way.
  - After the first Unity run, the main session also fixed two EditMode tests:
    - `Tween.Delay` cannot run in EditMode, so that test was removed; the PlayMode `BackgroundingDuringFinishTearsDownAtOnce` covers it.
    - A value-returning lambda passed to `AllocatingGCMemory` was turned into a statement lambda.
- Decision IDs were renumbered to DEC-110..DEC-117 because DEC-103 was taken by board-camera-framing.

## Unity verification (isolated batchmode copy, Unity 2022.3.62f2)

| Run | Command | Result |
| --- | --- | --- |
| EditMode, whole project | `Unity.exe -batchmode -runTests -testPlatform EditMode` | 387/387 passed (Gameplay 214, EditMode 103, Environment 60, LevelPreview 9, stub 1) |
| PlayMode, whole project | `Unity.exe -batchmode -runTests -testPlatform PlayMode` | 38/38 passed, including `GameSceneDragTests.EveryBlockOfLevelOneLeavesThroughItsDoorAndTheLevelIsWon` (0.69 s), `FinishDeadlineTests` (3), `BoardPresenterLifecycleTests` (7) |
| Compile warnings | first EditMode log | 2 warnings, both pre-existing in `Editor/Boosters/VacuumMotionAssets.cs` (another task); 0 from Domain/Gameplay/tests |
| Scene step | `-executeMethod GameSceneWiring.Run` (a tool that lived only in the copy and was deleted after the run) | `installers=2`, Gameplay under SceneScope, `pickPlaneHeight=0.5665`, EnvironmentPreview removed |

## Scene step (Game.unity)

- Base `9086dcd3…`, handed over by the board-look session; board-camera-framing is closed. Result `3e35a928fc5a863a…`.
- Only these changed:
  - the new `Gameplay` GameObject with `GameSceneInstaller` and `ApplicationPauseRelay`;
  - `SceneContext.m_installers` gained a second entry;
  - `EnvironmentSceneInstaller.m_preview` is none;
  - the `EnvironmentPreview` component is removed (its script file stays, for the environment owner).
- The copy-back ran only after the hash was unchanged and the live Editor title showed no `*`. Script `.meta` files were copied from the isolated copy, so the scene's script GUIDs resolve.

## Acceptance mapping

| AC | Channel | Evidence |
| --- | --- | --- |
| AC-1 movement | automated EditMode | `DragResolverTests`, `MoveRulesTests`, `LevelSessionTests` |
| AC-2 exit | automated EditMode | `ExitRulesTests`, `ExitCommitTests`, `FollowOffsetTests` |
| AC-3 picking, UI/area gating | automated EditMode | `BlockPickerTests`, `LevelSessionTests` (UI, outside area, wall ring), `GameplayPointerInputTests` (InputTestFixture) |
| AC-4 transitions, latch, same-tick Won | automated EditMode | `SessionTransitionsTests` (exhaustive), `SessionStateMachineTests`, `LevelSessionTests` |
| AC-5 Suspend/Teardown/Resume/Restart | automated EditMode | `LevelSessionTests` journal-order tests |
| AC-6 Won Finish ≤ 0.8 s | automated PlayMode | `FinishDeadlineTests` (cap, background forced cleanup, normal Finish) |
| AC-7 architecture, zero allocation | automated EditMode | `GameplayArchitectureTests`, `ZeroAllocationTests` (`Is.Not.AllocatingGCMemory`) |
| AC-8 presenter pooling and exit motion | automated PlayMode | `BoardPresenterLifecycleTests` |
| AC-9 Game-scene drag-to-door win | the contract names a manual capture or user acceptance | The automated real-scene `GameSceneDragTests` passed (level 1, simulated mouse, both blocks exit, Won, Teardown). The live-Editor capture or user acceptance is still open. |

## Unproven

- The live-Editor run: the MCP bridge is down; the live Editor imports the new files on its next refresh.
- Feel on a phone (OQ-8 push threshold, follow lead), 0 B per frame on the device (V-M5), and the pick-plane height visually (0.5665, measured from the piece prefabs).

## Update 2026-09-25 (after the Barrier coordination)

- Reviewed revision: `vcs:cs:5+owned-sha256:38551da10222d25eef9d0143f1fa7eb6abbd4e326672201d37e87f11bb89b015`. Before this change the identity still equalled `b90aa2b1…`, so no one else had touched the owned files.
- Core fix, found while mapping the Barrier rule (OBJ-05) onto the hooks:
  - `ExitRules`' swept exit path now asks the XP-3 cell gates for every non-own cell. A closed board object in a concave block's notch therefore refuses that exit.
  - The block's own cells are never gated (DEC-MECH-18).
  - New test: `ExitRulesTests.ACellGateClosingANotchCellInFrontOfAnLArmIsPathBlocked`.
- Test robustness: `ZeroAllocationTests` warms Unity's GC.Alloc recorder once in `[OneTimeSetUp]`. The first allocation assertion in a filtered run otherwise measured the recorder's own setup (`ASessionTickDuringAHeldDragAllocatesNothing` failed only when run alone).
- Re-verified in the isolated copy:
  - EditMode `ColorBlockJam.Gameplay.EditModeTests`: 215/215. This is the exact filtered scenario that had failed.
  - PlayMode `ColorBlockJam.Gameplay.PlayModeTests`: 11/11, including the Game-scene drag-to-Won test.
- The other assemblies do not reference Domain or Gameplay, so their earlier 387/387 EditMode and 38/38 PlayMode results still hold.

## Update 2026-09-25 14:30 (exact reachable piece capacity)

- Reviewed revision: `vcs:cs:5+owned-sha256:f1a1eceb14abf29bcdad77505d7bd201d8bfcb05d5fd62854b77ac6e2cfbb2a2`. Before this change it still equalled `38551da1…`.
- Change: `BoardPresenter` prepares each source type's pool with the 4-argument `BlockPieceFactory.Prepare`.
  - The piece counts per kind come from `ModularBlockView.AddReachableCapacity` over every placed footprint and its Rocket remainders, so a block spawns only the pieces it can show.
  - Requested by the block-runtime-objects task (`TASK-20260925-002022060-4c9dac46`, the user's "spawn only what you show").
  - It also closes the reviewer's risk of a level footprint that differs from its library shape.
- Re-verified in the isolated copy after mirroring the current project `Assets`, `Packages` and `ProjectSettings` (other sessions' Presentation changes included):
  - EditMode: 556 total, 553 passed. All 215 Gameplay tests passed. The 3 failures are `BoardKitTests` expecting 11 shapes while `Data/Board/PieceDatabases/BlockShapeLibrary.json` now holds 12 (the user's "New Piece Test", ID 30 / `Piece_030`, saved from the Piece Editor at 11:34; the three tests hard-code 11). They are outside this task's ownership and change.
  - PlayMode: 38/38, including `GameSceneDragTests` and the `BoardPresenterLifecycleTests`.
