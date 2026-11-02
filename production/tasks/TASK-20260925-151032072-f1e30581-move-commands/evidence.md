# Evidence: TASK-20260925-151032072-f1e30581-move-commands

## Preflight and workflow

- **Preflight.** Exact project `C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026` on Unity 2022.3.62f2.
  - The live Editor (PID 68220, untitled scene) was left untouched.
  - All Unity verification ran in the isolated batchmode copy `%TEMP%/cbj-draggate-4e3dc6`, mirrored from the project before each run.
  - Batch Unity crashed on the shared `CurlRequestCache.db` lock that other Editors held, so the batch runs used a separate `LOCALAPPDATA` holding copies of `Unity/licenses` and `Unity/config`.
- **Domain workflow:** `unity-game-dev`.
- **Design.** Workflow `wf_b5ce8419-880` produced three designs in the session scratchpad:
  - `commands-A-move-level.md`
  - `commands-B-step-level.md`
  - `commands-C-hybrid.md`

  Two critic lenses attacked each design; all three came back sound-with-fixes. The decision owner (the main session, on Opus while Fable credit is out) chose A: one move is one command, committed on release or push-out. Scope was trimmed: no push queue, no stamps or origins, no EndsLiveDrag, no path log.
- **Implementation.** It was staged outside the project and compiled in the out-of-Editor harness (C# 9, netstandard2.1, Onity analyzers, warnings as errors).
- **Coordination.** The "Ruled tile level sistem" session released its board-art DI change (`TASK-20260924-224656127-e361499a`) with a diff.
  - Its hunks in `BoardPresenter.cs` and `BoardPresenterLifecycleTests.cs` were applied onto the staged files with `patch` (all hunks applied).
  - Afterwards, project and stage differ only by this task's lines.
  - Files other sessions changed and this task does not own were taken from the project unchanged: `GameSceneInstaller.cs`, `GameAttemptLoader.cs`, `AttemptLevelConverter.cs`, `AttemptLevelConverterTests.cs` and `PresenterScene.cs`.

## Review

- Workflow `wf_a97e2862-bb8` ran three lenses (rules and lifecycle; architecture and determinism; performance and tests), with one adversarial verifier per finding.
- 12 findings were confirmed, 7 after deduplication, and 2 were refuted. All 7 were fixed:
  1. **Major.** The release filter skipped a command source's move of the same block in the same tick. `m_released` was removed, and `Sync` now settles every MoveCommitted of a block that is not held.
  2. **Minor.** A non-pointer exit slid diagonally. `PlaceOnExitCell` puts the root on its exit cell first. After the verifier's follow-up it also does this for a root still settling.
  3. **Minor.** PlayMode placed the committed board, and presenter behaviour 6 was untested. `Place(Preview)` is now used, with five new PlayMode tests and `FakeBoardPresenter.PlacedBoard`.
  4. **Minor.** The push-out session tick had no allocation test. `ASessionTickThatPushesABlockOutAllocatesNothing` was added.
  5. **Minor.** `MoveBlockHandler` was public. It is now internal, and an architecture test checks that no public Domain type implements `ICommandHandler`.
  6. **Minor.** A full tick event log threw after the board had already changed. The handler now throws before any write, with a test and a docs note.
  7. **Major.** The replay cell-gate case was vacuous. The `LeashCellGate` fixture reads the board it is asked about, and a new reach test and the replay test use it.
- An independent verifier then checked all 7 fixes: FIXED, with no regression. It noted the settling-root gap in fix 2, which was then closed with a test.

## Unity verification (isolated copy, Unity 2022.3.62f2)

| Run | Code | Result |
| --- | --- | --- |
| Gameplay EditMode, first run | before review fixes | 268/268 |
| Gameplay PlayMode, first run | before review fixes | 11/11, including `GameSceneDragTests` |
| Whole project EditMode / PlayMode | before review fixes | 841/841 / 44/44 |
| Gameplay EditMode / PlayMode | after review fixes, pre-DI base | 272/272 / 16/16 |
| Whole project EditMode / PlayMode | merged with the board-art DI release and the current project | EditMode 925/932: Gameplay 273/273, Environment 76/76, LevelPreview 12/12, Editor 563/570. PlayMode 55/55. |
| Compile warnings | first run's log | 0 from Domain, Gameplay or the Gameplay tests |
| Live Editor after copy-back | `refresh_unity` over MCP HTTP, then reflection | `scriptCompilationFailed` false, no `error CS` in the console, `CommandExecutor` and `LevelSession.TrySetCommandSource` loaded |

The 7 Editor failures are unrelated to this task:
- They are `LevelFreehandPieceTests` (3) and `LevelPlusTileTests` (4) in `ColorBlockJam.Editor.EditModeTests`.
- That assembly and `ColorBlockJam.Editor` reference neither Domain nor Gameplay, so this change cannot reach them.
- The test files changed at 21:20 as in-progress work by the "Parça template boş kareler" Level Editor session, which the board-art session identified.

Reviewed revision: `vcs:cs:8+owned-sha256:7459e3f45edf82daf799ded416ff03c3ab560963fa974c4d13a109467c6288db`.
- It covers 257 paths: `Domain/**`, `Gameplay/Session/**`, `Gameplay/Board/BoardPresenter.cs` with its `.meta`, `Tests/Gameplay/**`, `Domain.meta` and `docs/Gameplay-Drag-And-Gate.md`.
- It was computed with `scratchpad/reviewed_revision_commands.py`.
- `PROJECT.md` is shared and excluded.

## Acceptance mapping

| AC | Channel | Evidence |
| --- | --- | --- |
| AC-1 single writer | automated EditMode | `GameplayArchitectureTests.OnlyDomainCommandHandlersCanChangeTheBoard` (internal `BoardState` mutators, no public handler); `DragResolverTests.TheDragPreviewsWithoutChangingTheBoard`, `AReleaseProducesOneMoveCommandFromStartToTheReachedCell` |
| AC-2 refusals change nothing | automated EditMode | `MoveBlockHandlerTests` (every outcome with a board, event and log check), `CommandExecutorTests` |
| AC-3 preserved rules and DEC-113 | automated EditMode and PlayMode | adapted `DragResolverTests`, `ExitCommitTests`, `ExitRulesTests`, `FollowOffsetTests` and `LevelSessionTests` with unchanged expected values; `InterruptDefersTheMoveUntilTheNextCommandPhase`; `MoveReachTests.EveryMoveTheLiveDragMakesIsAccepted` (seeded walk of 200 drags) |
| AC-4 replay determinism | automated EditMode | `ReplayTests` (plain, Stay exit gate, board-reading cell gate), `PreviewBoardTests` (equivalence at every origin) |
| AC-5 external driver | automated EditMode | `CommandSourceTests` (replay to Won, record then replay after Restart, waits for a held drag, interrupted move first, refusal stops, dropped at Restart, paused) |
| AC-6 zero allocation | automated EditMode | `ZeroAllocationTests` (drag update, move and exit commands, release tick, push-out tick, source tick) |
| AC-7 Game scene and presenter | automated PlayMode | `GameSceneDragTests`, `BoardPresenterLifecycleTests` (including the new preview, settle and exit cases) |

## Notes

- Changeset 8 was checked in at 21:17 by another session. It already contains this task's contract and the command section of `docs/Gameplay-Drag-And-Gate.md`. It also contains this task's lifecycle lock files (`.lock/owner.json`, `.lock.claim`) by accident; this task's checkin removes them.
- `PROJECT.md` (DEC-122 row) stays pending together with the other sessions' rows.
