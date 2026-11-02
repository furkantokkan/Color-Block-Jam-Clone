# TASK-20260927-010019989-11b501c1-gate-exit-fragments — Gate-exit cube fragments (CBJ Template block-shatter port)

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260927-010019989-11b501c1-gate-exit-fragments
- Supersedes: none (it takes over only the fragments half of the draft TASK-20260925-025854248-9383f38c-held-outline-exit-fragments; that draft keeps the held outline)
- Title: Gate-exit cube fragments (CBJ Template block-shatter port)
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:11 plus existing uncommitted work of other sessions (planning snapshot 2026-09-27T01:00Z)
- Task kind: feature
- Area: Gameplay presentation of a valid door exit (block root exit motion and its fragment burst)
- Workflow: direct

## Objective and baseline

- Observable objective: When a block validly exits through its same-colour gate, small same-colour cube fragments burst outward from the gate for the length of the exit slide, as in the CBJ Template and visual reference 15.
- Current behavior/evidence:
  - User request 2026-09-27 (Turkish, verbatim): "cbj template de block parçalama efekti var bunun benzerini yapıp bize geçir block geçitten geçerken çalışıyor", with a screenshot of a blue 2 x 2 block below a blue top gate.
  - The Game scene exit (`BoardPresenter.StartExit`) clips the block root at the door line and tweens it out; nothing is emitted. No ParticleSystem exists in the project.
  - Reference implementation read on 2026-09-27: CBJ Template `Project Files/Game/Scripts/Other/BlockDestructionParticle.cs`, `LevelController.TryToCollectBlock`, `GateDirection.cs` and `Prefabs/Particles/Block Destroy Particle.prefab`. It plays one pooled particle case per lane of the block's extent along the gate (`GetAlignedSize`) at the cell just outside the gate, rotated so the Box shape emits outward, swaps the block material onto the renderer (`renderer.material`, an instance), and stops emission when the exit tween completes. Prefab: Box shape 1 x 1 x 1, rate 40/s, looping, start lifetime 0.52 s, start speed 8.99, start size random 0.1–0.4, start rotation random 0–360 degrees, rotation over lifetime 90 degrees/s, size over lifetime 1 → 0.63 (73 %) → 0, limit velocity 0 with dampen 0.08, Mesh render mode with the built-in Cube, no shadows. Its static pooling dictionary and `renderer.material` instance are forbidden here (PROJECT.md Rules 7, Performance Rule 1).
  - PROJECT.md already requires the outcome ("A valid door exit plays pooled same-color cube fragments under the session lifecycle"; DEC-90; reference 15 case rule in docs/Color-Block-Jam-Visual-References.md).
  - Ownership at planning: `Gameplay/Board/BoardPresenter.cs`, `Gameplay/Composition/GameSceneInstaller.cs` and `Scenes/Game.unity` have pending changes of TASK-20260926-231349009-945d78fe-ice-visibility, whose lifecycle claim (Codex, acquired 2026-09-26T23:35:27Z) is live; BoardPresenter.cs was last written 2026-09-27 00:35Z.
- Expected behavior:
  1. **Emitter (Presentation).** `ExitFragmentEmitter` is one pooled, code-built emitter: a GameObject with a Mesh-mode `ParticleSystem` configured from `ExitFragmentSettings` (the reference values above, adapted: non-looping with the exit duration, Box shape across the block's lanes, 3D start rotation and spin, Local simulation, no gravity, no shadows, no GPU instancing), rendering Unity's built-in Cube with the shared `MAT_Gameplay` taken from the board piece catalog's prefab renderer. Colour goes through one cached `MaterialPropertyBlock` (`_BaseColor`, the white-row `_PaletteUV` that BoardPieceView uses, clip disabled); no material instance. No prefab, scene or installer change is needed, so no serialized asset changes.
  2. **Pool owner (Presentation).** `ExitFragments` owns one Onity `OnityObjectPool<ExitFragmentEmitter>`, warms it on Home to a capped capacity (`min(block count, ExitFragmentSettings.k_maxEmitters)`), plays an emitter per valid exit, returns emitters whose last fragment died (polled, no callbacks), pauses/resumes, stops and returns all, and disposes the pool. A full pool skips the effect and never grows during Playing.
  3. **Placement (Gameplay).** `ExitFragmentPlacement` (plain C#) computes the emitter's local centre on the door line that the exit clip plane uses (floor edge + `ExitMotionSettings.k_clipOffsetCells`), across the block's extent along the door side, its outward rotation and lane count. `BoardPresenter.GetClipPlane` reads the same door line, so fragments start exactly where the block disappears.
  4. **Presenter hook-up (Gameplay).** BoardPresenter owns one `ExitFragments` under its Blocks root: prepared in TryBuild on Home, played in StartExit only for a `Leave` exit with the exit tween's duration and the block's palette colour, returned in Sync when finished, paused in SetTweensPaused, stopped in StopTweens, returned in ReturnPieces and disposed in Dispose. IBoardPresenter, LevelSession, the installer and the scene are unchanged.
  5. **Docs.** docs/Gameplay-Drag-And-Gate.md gets an exit-fragments subsection; README's runtime-operation table gets the fragment simulation row; PROJECT.md gets decision row DEC-131 (claimed by this contract; re-check before writing).
- Behavior that must remain unchanged:
  - Drag, exit, fit, colour and session rules and their tests; FinishSeconds and the 0.8 s Won Finish bound.
  - Exit motion timing, clip plane, settle, held follow, Arrow markers and Ice effect views.
  - Pool counts after Restart (no Instantiate/Destroy during an attempt); the Blocks-subtree object count is stable across Restart of the same level.
  - `MAT_Gameplay`, `BoardLit.shader`, piece prefabs, BoardPieceView, BlockPiece, ModularBlockView, BlockPieceFactory, BoardArt, BoardPieceCatalog.

## Ownership

- Owner/writer role: gameplay presentation implementer (this session), then an independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Presentation/Board/ExitFragments.cs
  - Assets/ColorBlockJam/Presentation/Board/ExitFragments.cs.meta
  - Assets/ColorBlockJam/Presentation/Board/ExitFragmentEmitter.cs
  - Assets/ColorBlockJam/Presentation/Board/ExitFragmentEmitter.cs.meta
  - Assets/ColorBlockJam/Presentation/Board/ExitFragmentSettings.cs
  - Assets/ColorBlockJam/Presentation/Board/ExitFragmentSettings.cs.meta
  - Assets/ColorBlockJam/Gameplay/Board/ExitFragmentPlacement.cs
  - Assets/ColorBlockJam/Gameplay/Board/ExitFragmentPlacement.cs.meta
  - Assets/ColorBlockJam/Gameplay/Board/BoardPresenter.cs (exit-fragment lines and the shared door line only, after the execution dependency below)
  - Assets/ColorBlockJam/Tests/Gameplay/EditMode/Gameplay/ExitFragmentPlacementTests.cs
  - Assets/ColorBlockJam/Tests/Gameplay/EditMode/Gameplay/ExitFragmentPlacementTests.cs.meta
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/ExitFragmentsTests.cs
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/ExitFragmentsTests.cs.meta
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/BoardPresenterExitFragmentTests.cs
  - Assets/ColorBlockJam/Tests/Gameplay/PlayMode/BoardPresenterExitFragmentTests.cs.meta
  - docs/Gameplay-Drag-And-Gate.md (one exit-fragments subsection)
  - README.md (one runtime-operation table row)
  - PROJECT.md (decision-log row DEC-131 only)
  - production/tasks/TASK-20260927-010019989-11b501c1-gate-exit-fragments/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Art/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Resources/**
  - Assets/ColorBlockJam/Gameplay/Composition/**
  - Assets/ColorBlockJam/Gameplay/Session/**
  - Assets/ColorBlockJam/Gameplay/Effects/**
  - Assets/ColorBlockJam/Gameplay/Levels/**
  - Assets/ColorBlockJam/Domain/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Environment/**
  - Every other file under Assets/ColorBlockJam/Presentation/** (read-only)
  - Every other test file (existing tests are not edited)
  - Material instances (`Renderer.material`), shared-material property, keyword or pass changes
  - A second writer on BoardPresenter.cs while another task's lifecycle claim covers it
  - Plastic checkin, shelve, undo of others' changes or publishing
- Serialized assets: none (Unity generates the `.meta` files of the new scripts; this task owns only those listed above)
- Other active writers/worktrees checked: TASK-20260926-231349009-945d78fe-ice-visibility holds a live claim over BoardPresenter.cs, GameSceneInstaller.cs and Game.unity (pending changes present). TASK-20260926-230921278-a3ffdc40-arrow-block-gameplay (no claim file) waits on the same owner for BoardPresenter.cs. TASK-20260927-004448215-54655902-booster-gameplay-flow (ready, not started) owns GameSceneInstaller.cs and Game.unity, which this task does not touch. TASK-20260926-235915696-6b7f82f4-player-data-currencies (claim, Hub session) does not overlap. All new paths above are unowned.
- Execution dependency: the BoardPresenter.cs hook-up (Expected behavior 4) waits until TASK-20260926-231349009-945d78fe-ice-visibility releases its lifecycle claim and no other claim covers BoardPresenter.cs; the new files, tests and docs do not wait.
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given a block that validly exits (right colour, fitting door span, flush push; `ExitOutcome.Leave`), when its exit motion starts, then exactly one pooled emitter plays same-colour cube fragments (the block's palette colour on the shared material) from the door line across the block's lanes, moving outward away from the board, emits for the exit motion's duration, and returns to its pool after its last fragment dies.
2. Given a refused exit (wrong colour, too narrow door, not flush, a `Stay` outcome) or an ordinary move or settle, then no fragment is emitted.
3. Given fragments in flight, when the session suspends they freeze and continue on Resume; when it tears down (Won after Finish, Failed, Home, Restart, a level change or Dispose) every emitter stops, clears and returns exactly once. Restart keeps the prepared emitter count and the Blocks-subtree object count.
4. Given the Playing tick with an exit, then the fragment path allocates 0 B, instantiates nothing and creates no material instance; emitters are created only on Home, the pool is capped, and a full pool skips fragments instead of growing.
5. Given the Game scene at 1080 x 1920, when a block exits through a gate on each side, then the fragments read like the CBJ Template effect and reference 15 closely enough for the user's acceptance.

## References and constraints

- Story/GDD/ADR/design/log/image: PROJECT.md (Runtime Lifecycle, Performance Rules 1, 3, 5, 6, 7; DEC-89, DEC-90, DEC-93, DEC-96, DEC-111, DEC-117, DEC-120, DEC-123); docs/Color-Block-Jam-Visual-References.md reference 15 and `docs/media/color-block-jam/user-references/15-gate-exit-particles.jpg`; docs/Gameplay-Drag-And-Gate.md; the user's 2026-09-27 screenshot (blue 2 x 2 block below a blue top gate); CBJ Template files listed under Current behavior (behavior reference only, not copied).
- Unity version/platform: Unity 2022.3.62f2, URP 14, Android/iOS portrait 1080 x 1920
- Package/architecture constraints: Onity pools and ownership rules; PrimeTween stays the only tween library; no new package; the ParticleSystem module (`com.unity.modules.particlesystem`) is already in the manifest; one shared opaque material with MPB colour; no `Update` on any new MonoBehaviour (the new classes are plain C#); no statics with mutable state; no coroutines; ZLinq/ZString rules unaffected (no LINQ or string formatting in the Playing path).
- Explicit out of scope: the held-block outline (stays in the draft TASK-20260925-025854248-9383f38c-held-outline-exit-fragments); booster destruction effects (Hammer, Rocket, Vacuum) and Ice shards, although they may reuse ExitFragments later; audio; the static result capture; device profiling.

## Verification and risk

- Focused test path/command: EditMode `ColorBlockJam.Gameplay.EditModeTests` filter `ExitFragmentPlacementTests`; PlayMode `ColorBlockJam.Gameplay.PlayModeTests` filters `ExitFragmentsTests` and `BoardPresenterExitFragmentTests`, then the existing `BoardPresenterLifecycleTests` and `BoardPresenterArrowMarkerTests` as preservation; run in the live Editor over MCP for Unity (HTTP 8082) only while it is idle, otherwise in an isolated batchmode copy.
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile of Presentation, Gameplay and both test assemblies with no new Console errors or warnings
- EditMode: acceptance 1 (placement part)
- PlayMode: acceptance 1–4
- Manual/built-player/profiler/device evidence: acceptance 5 by a Game-scene capture or explicit user acceptance; the Android device Profiler check of NFR-P4/P5 remains with the performance gate
- Risk: medium (new public Presentation classes and presenter behaviour; no serialized asset, package, scene or setting change)
- Approval already granted (exact target only): the user's order of 2026-09-27 quoted above covers this scope; it grants no serialized-asset target, and none is needed
- Commit/checkin constraint: forbidden
- Current invocation permission: implement this contract; no commit/checkin or closure permission
- Handoff destination: independent general-purpose verifier (the unity-test-verifier agent type needs a git worktree), then `$task-done TASK-20260927-010019989-11b501c1-gate-exit-fragments` on the user's close order

## Stop conditions

- The effect would need a prefab, scene, installer, shader or material change.
- The fragments would need a material instance, a renderer feature or a Playing-time allocation that cannot be removed.
- The BoardPresenter.cs owner changes the exit path (StartExit, GetClipPlane, Sync, the lifecycle methods) so that the hook-up no longer fits; re-read and re-plan instead of merging blindly.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:09a6bf22240ba0b95786923786c84db36b49538fe3e114154cd0355c07c0bb07
- Evidence fingerprint: sha256:7c4871347d1dca52ac03f240f3d7cc0faeec4567b5f2e92cad45bedb54d51d45
- Reviewed revision: vcs:plastic:cs:11@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a7a0600864f13e659ecae887bbef5d1cbaac16fc94a36d107f29813bf013eb4b
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:11@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a7a0600864f13e659ecae887bbef5d1cbaac16fc94a36d107f29813bf013eb4b
- Closed at: 2026-09-28T08:46:47Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Valid exit plays one same-colour outward burst for the exit duration and returns | PASS | automated: BoardPresenterExitFragmentTests (AValidExitPlaysOneBurstOnTheDoorLineInItsBlocksColour, TheBurstReturnsInSyncOnceItsLastFragmentDies), ExitFragmentsTests (ABurstDrawsTheSharedMaterialInItsBlockColourAndFliesOutOfTheBoard, ABurstReturnsToThePoolOnceItsLastFragmentDies), ExitFragmentPlacementTests 7/7; real Game scene GameSceneDragTests and frame captures in the batch copy | vcs:plastic:cs:11@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a7a0600864f13e659ecae887bbef5d1cbaac16fc94a36d107f29813bf013eb4b |
| AC-2 Refused exit, move or settle emits nothing | PASS | automated: BoardPresenterExitFragmentTests (AMoveOrAStayExitPlaysNoBurst, ARefusedExitCommandPlaysNoBurst) | vcs:plastic:cs:11@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a7a0600864f13e659ecae887bbef5d1cbaac16fc94a36d107f29813bf013eb4b |
| AC-3 Suspend freezes, Resume continues, Teardown stops and returns once; Restart counts stable | PASS | automated: SuspendHoldsTheBurstAndTeardownStopsAndReturnsIt, RestartKeepsTheWarmedEmittersAndCreatesNothing, PauseHoldsTheBurstAndResumeContinuesIt, ResumingAnEndedBurstDoesNotReplayIt, StopClearsEveryBurstAndReturnAllHidesThem, PrepareWarmsAtMostTheCapOnceAndKeepsTheEmittersHidden | vcs:plastic:cs:11@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a7a0600864f13e659ecae887bbef5d1cbaac16fc94a36d107f29813bf013eb4b |
| AC-4 Zero allocation, no instance or instantiation in Playing, capped pool skips when full | PASS | automated (Editor Mono): PlayingPausingStoppingAndReturningAllocateNothing, AnExitSyncAfterRestartAllocatesNothing, AFullPoolSkipsTheBurstInsteadOfGrowing, the shared-material SameAs check and the Restart object count | vcs:plastic:cs:11@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a7a0600864f13e659ecae887bbef5d1cbaac16fc94a36d107f29813bf013eb4b |
| AC-5 Visual match with the CBJ Template effect and reference 15 | UNPROVEN | manual: evidence/exit-fragments-*.png from the real Game scene; awaiting explicit user acceptance | vcs:plastic:cs:11@ColorBlockJamCase2026@efurk64@unity+owned-sha256:a7a0600864f13e659ecae887bbef5d1cbaac16fc94a36d107f29813bf013eb4b |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's 2026-09-27 request and the CBJ Template study; BoardPresenter.cs hook-up queued after the Ice claim
- 2026-09-27T01:04Z — in_progress — direct implement-task started under lifecycle claim (Claude session e9e1fd); unity-cli preflight READY WITH WARNINGS (live Editor proven, busy with another session's Play Mode; BoardPresenter.cs owned by the Ice claim)
- 2026-09-27T01:55Z — verification — BoardPresenter.cs hook-up applied after the Ice claim released (01:12Z); AC-1..AC-4 PASS in the isolated batch copy (EditMode 7/7, PlayMode 16/16, suites 295/295 and 82/83 with one unrelated Ice test), independent verifier PASS, live Editor compile clean; AC-5 captures await user acceptance (ACTION_REQUIRED: USER_ACCEPTANCE); see evidence.md
- 2026-09-28T08:46:47Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
