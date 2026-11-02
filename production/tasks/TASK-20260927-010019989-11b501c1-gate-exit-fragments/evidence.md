# Evidence: TASK-20260927-010019989-11b501c1-gate-exit-fragments

Implementer run 2026-09-27 (Claude session e9e1fd, single writer under the task claim). Workspace cs:11 plus other sessions' pending work; no checkin or shelve.

## Preflight and ownership

- Unity CLI 1.0.0-beta.11; project `ProjectVersion.txt` 2022.3.62f2, so the pre-Unity-6 gate applies (MCP for Unity over HTTP 8082).
- Live instance `ColorBlockJamCase2026@99077e4df1ecfaa4`: its hash is SHA-1 of `C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026/Assets` (first 16 hex digits). After pinning, `mcpforunity://project/info` read back the same root and 2022.3.62f2.
- The live Editor has been in a PlayMode transition since 00:46:54Z, a foreign stalled runner that the Ice task reported. It was not touched, so verification ran in an isolated batch copy.
- BoardPresenter.cs was edited only after TASK-20260926-231349009-945d78fe-ice-visibility released its claim (01:12:19Z). This task's claim was live from 01:02:06Z, and no other claim covered the file.

## Changes

- New `Presentation/Board/ExitFragmentSettings.cs`: tuning constants taken from the reference prefab.
- New `Presentation/Board/ExitFragmentEmitter.cs`: a code-built Mesh-mode ParticleSystem using the built-in Cube and the shared `MAT_Gameplay`, with an MPB colour.
- New `Presentation/Board/ExitFragments.cs`: an Onity pool, capped at min(block count, 4), that skips when full.
- New `Gameplay/Board/ExitFragmentPlacement.cs`: the door line, lane centre, rotation and lane count.
- `Gameplay/Board/BoardPresenter.cs`, this task's hunks only:
  - summary line;
  - `m_fragments` field;
  - two diagnostic counts;
  - `TryBuild` prepare;
  - `Sync`, `SetTweensPaused`, `StopTweens`, `ReturnPieces` and `Dispose` forwarding;
  - `PlayExitFragments` from `StartExit`;
  - `GetClipPlane` now reads `ExitFragmentPlacement.GetDoorLine`.
- The pre-edit file was rebuilt by reversing these hunks and is byte-size identical to the pre-edit file (33,268 bytes).
- Tests:
  - `Tests/Gameplay/EditMode/Gameplay/ExitFragmentPlacementTests.cs` (6);
  - `Tests/Gameplay/PlayMode/ExitFragmentsTests.cs` (8);
  - `Tests/Gameplay/PlayMode/BoardPresenterExitFragmentTests.cs` (7).
- Docs:
  - `docs/Gameplay-Drag-And-Gate.md`: an "Exit fragments" section, a loop-table row, the Extending note and the Verification list;
  - `README.md`: one operation row;
  - `PROJECT.md`: DEC-131.

## Compile

The out-of-Editor harness (dotnet, C# 9, Unity 2022.3.62f2 engine DLLs, `Library/ScriptAssemblies`) built:

- the new sources with the Onity analyzer and warnings treated as errors: 0 warnings, 0 errors;
- the whole `ColorBlockJam.Presentation` and `ColorBlockJam.Gameplay` assemblies from the live Assets sources under their real names: 0 errors (12 existing warnings, all in other files);
- the whole `ColorBlockJam.Gameplay.EditModeTests` and `ColorBlockJam.Gameplay.PlayModeTests` assemblies: 0 errors.

## Tests (isolated copy `%TEMP%\cbj-exitfrag-a05690`, Unity 2022.3.62f2 `-batchmode -runTests`)

- EditMode `ColorBlockJam.Gameplay.EditModeTests`: 295/295 passed, including the 6 `ExitFragmentPlacementTests`.
- PlayMode run 1 (`ColorBlockJam.Gameplay.PlayModeTests` and `ColorBlockJam.Presentation.PlayModeTests`, copy as found): 73/83 passed.
  - 8 scene tests failed, plus the copy-only capture test, all with the same `OnityResolveException` at `GameAttemptLoader.Load:88`. That line (`resolver.Resolve<BlockShapeCatalog>()`) is a pending change of another task, and `Resources/Onity/ProjectContext.prefab` still has `m_shapeLibrary: {fileID: 0}`. TASK-20260925-005313341-47831bb7 records that this assignment is waiting for user approval. The failure happens before any attempt is built, so this task's code never ran in those tests.
  - `ExitFragmentsTests.ABurstDraws…` failed because it compared an MPB colour exactly after the linear-space round trip. It now uses `ColorEqualityComparer(1e-4)`.
- PlayMode run 2 (the same suites; in the copy only, `m_shapeLibrary` was assigned to `BlockShapeLibrary.asset`; the live prefab is unchanged): 82/83 passed.
  - All 15 new PlayMode tests passed.
  - `GameSceneDragTests` passed: level 1 was won by dragging in the real Game scene, with fragments playing.
  - `BoardPresenterLifecycleTests` and `BoardPresenterArrowMarkerTests` passed.
  - The one failure is `GameSceneIceTests` line 107. It expects `Failed` after `RunTick(61f)`, but the Game scene binds `InactiveCountdown`. This is an assumption of the Ice task's in-progress test. Its earlier assertions, which run this task's exit, pause and Restart paths, passed.

## Visual (AC-5)

- The copy-only `ExitFragmentCaptureTests` was never copied back. It drags level 1's blocks out of their doors in the real Game scene at 1080 x 1920, with `Time.captureDeltaTime` set to 1/60, and renders the main camera.
  - `evidence/exit-fragments-top-door-sequence.png`, blue 2 x 2 block through the top door:
    - the cubes appear at 0.10 s;
    - the cloud peaks around 0.35 s;
    - the cubes drift and shrink until 0.60 s.
  - `evidence/exit-fragments-final-exit-bottom-door-sequence.png`: the pink winning block. The Won Finish lasts only for the exit motion (`FinishSeconds`), so Teardown clears the burst at about 0.35 s. See the open decision below.
  - `evidence/exit-fragments-game-scene-1080x1920.png`: one full frame.
- An earlier capture using `WaitForSeconds` showed frozen cubes between two frames. That was a capture artifact: slow capture frames combined with Unity's 0.03 s per-frame particle step cap (`Time.maximumParticleDeltaTime`). It is not the effect's behaviour.

## Independent review and final focused runs

- A read-only general-purpose verifier reviewed the change. The unity-test-verifier agent type needs a git worktree, so it was not used. Verdict: **PASS**, with no blocking defect. Three of its non-blocking findings were added as tests:
  - Resume keeps `system.time` (a restart would reset it);
  - a T footprint whose origin is inside it (`MinX`/`MinY` are not zero);
  - an exit command that the domain refuses plays no burst.
  The other findings are not changed:
  - `IsFinished` and `SetPaused` have no destroyed-object guard (unreachable while the scene is alive);
  - `OnPoolGet` throws for an emitter destroyed from outside, the same policy as `BlockIceOverlay`;
  - the doc edits reach a few consistency lines beyond the new section;
  - the live Editor import at 01:41Z rewrote `ProjectSettings/EntitiesClientSettings.asset` and `ShaderGraphSettings.asset`. They are not this task's files; keep them out of its checkin.
- Final focused runs in the copy, whose test and production files are byte-identical to the live workspace:
  - EditMode `ExitFragmentPlacementTests`: 7/7 passed (`final-editmode.xml`);
  - PlayMode `ExitFragmentsTests;BoardPresenterExitFragmentTests`: 16/16 passed (`final-playmode.xml`).
- Live Editor (idle since its 01:41Z domain reload):
  - the Console has no compile error;
  - `execute_code` read-only check: `ExitFragments`, `ExitFragmentPlacement`, `BoardPresenter.ActiveExitFragmentCount` and `BoardPresenterExitFragmentTests` are loaded, and `Resources.GetBuiltinResource<Mesh>("Cube.fbx")` returns the 24-vertex Cube.
  - No test ran in the live Editor: the LevelEditor scene is open and blocks Play Mode, and it holds the user's draft.

## Reviewed revision

`vcs:plastic:cs:11@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a7a0600864f13e659ecae887bbef5d1cbaac16fc94a36d107f29813bf013eb4b`

- Review set (15 files):
  - the 4 new sources, the 3 new test files and their 7 `.meta` files;
  - `Gameplay/Board/BoardPresenter.cs`.
- Recipe: repository-relative paths with forward slashes, sorted ordinally. Each file contributes `UTF-8(path) + "\n" + raw bytes + "\n"` to one SHA-256.
- Docs and this folder are outputs, not verification inputs.

## Open

- AC-5 needs the user's visual acceptance.
- Decision for the user: on the winning exit, should the Won Finish also wait for the burst (up to the 0.8 s DEC-93 cap)? That changes `FinishSeconds`, which this contract lists as preserved.
- Device checks (0 B GC and frame cost on Android) stay with the performance gate.
