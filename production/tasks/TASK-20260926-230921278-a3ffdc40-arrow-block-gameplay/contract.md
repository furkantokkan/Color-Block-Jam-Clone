# TASK-20260926-230921278-a3ffdc40-arrow-block-gameplay — Arrow blocks move only along their axis and exit only on its sides (BLK-01), and the game shows their marker

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260926-230921278-a3ffdc40-arrow-block-gameplay
- Supersedes: none
- Title: Arrow blocks move only along their axis and exit only on its sides (BLK-01), and the game shows their marker
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:10 plus the uncommitted work of 2026-09-26/27, including TASK-20260926-212320271-98b25efa-level-editor-arrow-markers (planning provenance only)
- Task kind: feature
- Area: level attempt (`ColorBlockJam.Domain` hooks, `ColorBlockJam.Gameplay` attempt conversion, session hook source and presenter) and the runtime effect gate (`ColorBlockJam.Runtime`)
- Workflow: direct

## Objective and baseline

- Observable objective: In the Game scene, an Arrow block moves only along its axis (Horizontal: its column never changes; Vertical: its row never changes) and exits only through a matching door on one of the two sides its axis points to. Levels with Arrow blocks load in the game, and the block shows its centred Arrow marker.
- Current behavior/evidence:
  - User answer of 2026-09-27: "Add arrow gameplay (Recommended)". The Arrow marker now shows on the canvas and in the preview (TASK-20260926-212320271-98b25efa). The game cannot show it, because arrow movement is not implemented.
  - `LevelEffectRegistry.k_arrowRuntimeImplemented = false` (Runtime/Levels/LevelEffectRegistry.cs L14). `LevelDatabase.LoadAsync` therefore refuses any level database with an Arrow effect at project scope ("… effect arrow is not implemented in gameplay yet.").
  - The Domain already supports axis limits end to end:
    - `IAxisConstraint.GetAxes(IBoardReader, block)` returns an `AxisMask`, read once at drag start and intersected (`AttemptHooks.GetAxes`);
    - `DragResolver` projects the pointer onto the allowed axes and gives no exit push on a locked axis;
    - `MoveBlockHandler.IsOnAxes` refuses off-axis moves and exits on a perpendicular side (`AxisRefused`);
    - `MoveReach` and `FollowOffset` respect the axes.
    No production `IAxisConstraint` exists (only the test fixture `AxisGate`). `ExitRules.Evaluate` has no axis awareness, so direct callers get `Fits` for a perpendicular side.
  - `AttemptLevelConverter.TryConvert` carries no effects into `AttemptLevel`, `BlockVisualSpec` or `BlockDefinition`. `GameSceneInstaller` binds `EmptyAttemptHookSource` ("the effect registry task binds its own source here (DEC-72)"). `LevelSession.TryCommitAttempt` calls `IBoardPresenter.TryBuild(level)`, so the presenter gets no arrow data. The arrow-markers task added `BoardPresenter.TryBuild(AttemptLevel, LevelData authored, out error)` as a stopgap.
  - Spec: docs/Color-Block-Jam-PRD.md §7.12 (RULE-34, AC-BLK-01.1 to .6, V-ARW-1 to -3, V-VAL-2) and docs/Color-Block-Jam-PRD-Mechanics-Catalog.md BLK-01 (L115-129, DEC-MECH-4: an axis, both ways; a diagonal pointer is projected; the axis is fixed for the attempt; Arrow + Ice means Ice first).
- Expected behavior (revision 2, 2026-09-28: follows docs/Block-Effects-Architecture.md revision 6, the shared structure the user ordered with "mimari için ortak solid bir yapıda anlaşın"; it supersedes the revision 1 converter-based axis plan):
  - Module: `ArrowEffectModule : IBlockEffectModule` (TypeId `arrow`) in Gameplay Effects.
    - `Read` runs on Home and returns an immutable `ArrowEffectLevel`: the axis of each authored block (`ArrowMarkerRun.TryGetAxis`), in converted block order.
    - `AddHooks` registers the Arrow hooks only when some block has an axis, and returns a state that holds no changing values.
    - `CreateView` returns an `ArrowEffectView`.
    - `GameSceneInstaller` lists it after `IceEffectModule` (blockers first; catalog "Arrow + Ice: Ice first"). That one list line is the only installer change.
    - `AttemptLevel`, `AttemptLevelConverter`, `LevelSession`, the hook source and the movement and exit algorithms stay effect-free.
  - Domain rules, engine-free:
    - `ArrowAxisConstraint : IAxisConstraint` returns the axis of the block's authored source (`board.GetFootprint(block).Source`), so a Rocket part keeps its parent's axis with no copied state. A block without Arrow gets `Both`.
    - `ArrowExitGate : IExitGate` vetoes an exit whose door side is perpendicular to that axis (`SideMath.RunsAlongX`), so `ExitRules.Evaluate` returns `Vetoed` for every caller.
    - Both are fixed for the whole attempt and allocate nothing per call.
  - Game view: `ArrowEffectView : IBlockEffectView, IBlockFootprintView` wraps `BlockArrowMarkers`.
    - `TryPrepare` warms one marker pool per Arrow_N on Home, including Rocket parts (`FragmentCapacity.AddArrowMarkerCapacity`), and rejects a missing Arrow prefab, naming the block.
    - `Attach`, `AttachPart` and `SetFootprint` attach or re-attach the centred marker over the block's current cells. `SetClipPlane` clips it on exit. `Detach`, `ReturnAll` and `Dispose` return it.
    - The inline Arrow code in `BoardPresenter` is removed: `m_markers`, `m_arrows`, `m_arrowAxes`, `AttachMarker`, `TryGetArrowAxes` and the stopgap `TryBuild(level, authored, …)` overload. `BoardPresenter` keeps no effect-specific branch. Any test accessor becomes generic, or lives on the view.
    - The marker keeps every arrow-markers rule: pooled, parented, centred, clipped on exit, returned on exit, teardown and restart.
    - Revision 3: accepted deviations. A level rejected for a missing Arrow prefab now leaves no level built, like Ice, because effect views prepare inside the presenter's shared build step; the error still names the block. Idle pooled markers wait under BoardRoot, the only root the view context provides. Both are recorded in docs/Gameplay-Drag-And-Gate.md.
  - Editor views: the inline Arrow display moves behind the existing extension lists, with the same look and no second renderer.
    - Canvas: an `ArrowCanvasEffectLayer : ILevelCanvasEffectLayer` draws the badge and outline that `LevelCanvasModel`/`LevelCanvasElement` draw inline today.
    - Preview: an `ArrowPreviewEffect : ILevelPreviewEffect` attaches the markers that `LevelPreviewHost` attaches inline today.
    - Existing canvas and preview Arrow tests keep passing unchanged, or with a seam-only port.
  - Runtime gate: `k_arrowRuntimeImplemented = true`, so project-scope loads accept Arrow levels. Test messages that assumed Arrow was unimplemented use Ice or are updated. The Ice flag is not changed by this task.
  - Editor issue, AC-BLK-01.5 / PRE-1: `LevelDraftIssues` reports, as a warning with a stable message, an Arrow block that has no door of its colour on either of its axis sides within its row or column span. Saving is not blocked (docs/Level-Data-Format.md: solver static checks are not validation errors).
  - Docs:
    - docs/Gameplay-Drag-And-Gate.md: the Arrow hooks;
    - docs/Block-Effects-Architecture.md: Arrow step done;
    - docs/Level-Data-Format.md: Arrow is runtime-implemented;
    - PROJECT.md: one decision row for BLK-01 gameplay;
    - docs/Color-Block-Jam-PRD-Mechanics-Catalog.md BLK-01: the centring rule note;
    - docs/Level-Editor-Design.md: the PRE-1 issue.
- Behavior that must remain unchanged:
  - Every non-Arrow block's drag, move command, exit, Won/Failed, pause and teardown behaviour.
  - The DEC-112 stepping and DEC-111 exit push.
  - The zero-allocation Playing tick (`ZeroAllocationTests`).
  - The command log (DEC-122).
  - Level JSON format.
  - Editor authoring of the effect (Effect Editor).
  - Ice stays unimplemented.
  - The arrow-markers presentation rules.

## Ownership

- Owner/writer role: implement-task (single writer)
- Allowed paths (revision 2; locations after the folder reorg of 2026-09-28, all under Assets/ColorBlockJam unless stated; each new `.cs` includes its Unity-generated `.cs.meta`):
  - Runtime/Domain/Hooks/ArrowAxisConstraint.cs (new)
  - Runtime/Domain/Hooks/ArrowExitGate.cs (new)
  - Runtime/Gameplay/Effects/ArrowEffectModule.cs, ArrowEffectLevel.cs, ArrowEffectState.cs, ArrowEffectView.cs (new)
  - Runtime/Gameplay/Composition/GameSceneInstaller.cs (the module list line only)
  - Runtime/Gameplay/Board/BoardPresenter.cs (removing the inline Arrow code; a generic effect-view accessor if tests need one)
  - Runtime/Core/Levels/LevelEffectRegistry.cs (the k_arrowRuntimeImplemented line only)
  - Editor/Core/Levels/Canvas/ArrowCanvasEffectLayer.cs (new)
  - Editor/Core/Levels/Canvas/LevelCanvasModel.cs and LevelCanvasElement.cs (moving the inline Arrow display behind the layer list)
  - Editor/Core/Levels/Canvas/LevelDraftIssues.cs (PRE-1)
  - Editor/LevelPreview/ArrowPreviewEffect.cs (new)
  - Editor/LevelPreview/LevelPreviewHost.cs, LevelPreviewBlock.cs, LevelPreviewBlockMapper.cs, and the file that registers the preview effect list (moving the inline Arrow display behind the adapter list)
  - Tests/Gameplay/EditMode/** (new Arrow tests; edits only where a fixture must carry Arrow)
  - Tests/Gameplay/PlayMode/BoardPresenterArrowMarkerTests.cs (ported to the view)
  - Tests/Gameplay/PlayMode/GameSceneArrowTests.cs (new)
  - Tests/Gameplay/PlayMode/Fixtures/** (only if a fixture needs an arrow level)
  - Tests/EditMode/LevelEffectTests.cs and LevelDatabaseJsonTests.cs (only the assertions that assume Arrow is unimplemented)
  - Tests/EditMode/ArrowIssueTests.cs (new)
  - Tests/EditMode/CanvasArrowBadgeTests.cs and Tests/LevelPreview/PreviewArrowMarkerTests.cs (seam-only ports)
  - docs/Gameplay-Drag-And-Gate.md (repository root)
  - docs/Block-Effects-Architecture.md (repository root)
  - docs/Level-Data-Format.md (repository root)
  - docs/Level-Editor-Design.md (repository root)
  - docs/Color-Block-Jam-PRD-Mechanics-Catalog.md (repository root; the BLK-01 view rule lines only)
  - PROJECT.md (repository root; one decision-log row)
  - production/tasks/TASK-20260926-230921278-a3ffdc40-arrow-block-gameplay/contract.md (managed lifecycle block only)
  - Revision 3 additions (2026-09-28, decision owner, from the review evidence):
    - Editor/Validation/LevelSolverSupport.cs, LevelSolverMoves.cs, LevelSolverTables.cs, LevelSolvabilityCheck.cs (Arrow solver support: axis-only slides, axis-side exits, and "arrow" in the support table). This keeps the project invariant `LevelSupportGateTests.EveryRuntimeImplementedEffectHasSolverSupport`, so the earlier "a solver is out of scope" line no longer applies to Arrow's move rule.
    - Tests/Validation/EditMode/** (Arrow solver tests; edits only where a fixture must pass axes)
    - Tests/Editor/EditMode/IceAuthoringTests.cs (only the assertion that Arrow is not runtime-implemented; point it at a still-gated effect and rename the test)
    - Tests/Gameplay/PlayMode/GameSceneIceTests.cs and Tests/LevelPreview/EditMode/PreviewIceTests.cs (seam-only ports that the removed 3-argument TryBuild and the preview effect list require)
    - Runtime/Presentation/Board/BlockArrowMarkers.cs (the owner and stop-point summary comment only)
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Data/** (no shipped level gains an arrow here; the user saves their own draft)
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Prefabs/**
  - Any other Domain, Gameplay, Runtime, Presentation or Editor file not listed, including LevelSession, AttemptLevel, AttemptLevelConverter, EffectAttemptHookSource and the Ice module and views; any asmdef change
  - Any shared-workspace write before the folder-reorg session reports "restructure done"
  - Plastic checkin, shelve, branch, or undo of other sessions' pending changes
  - Changing the user's open Level Editor draft, the level JSON or any scene file
- Serialized assets: none. New `.cs.meta` files are Unity-generated script metadata owned by implement-task.
- Other active writers/worktrees checked (2026-09-28T03:15Z):
  - Released: booster-gameplay-flow (54655902) and held-outline (9383f38c).
  - Game HUD (TASK-20260928-005915835-5221b8c5, session "Oyun sahnesi UI hazırlama") holds a lock. Its only overlap is a separate integration hunk in GameSceneInstaller.cs; this task edits only the module list line. Re-read the file right before the edit.
  - The folder reorg ("Klasör düzenleme ve script organizasyonu") moves the folders with `cm mv` and will message "restructure done". File names, GUIDs, namespaces and assemblies do not change.
- Execution dependency: the reorg's "restructure done" message (a path move, not an ownership conflict)
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. AC-1 (AC-BLK-01.1): Given Horizontal and Vertical Arrow blocks (I3, L3 and 1x1 footprints), when dragged in any direction, including diagonals, then a Horizontal block changes only its column and a Vertical block only its row, through free cells both ways. The axis is fixed for the attempt.
2. AC-2 (AC-BLK-01.2): Given an Arrow block flush against a matching door, when the door is on an axis side and the pointer pushes outward, then it exits. When the matching door is on a perpendicular side, then it never exits (the move is refused and `ExitRules.Evaluate` returns `Vetoed`).
3. AC-3: Given the Game scene with a level database containing Arrow blocks, when the project scope loads and an attempt is built on Home, then the load succeeds, each Arrow block shows its centred marker through `ArrowEffectView` on the existing `IBoardPresenter.TryBuild(level)` path, and dragging and exiting follow AC-1 and AC-2. The marker moves, clips and returns as in the arrow-markers task (AC-BLK-01.4). A Rocket part keeps its parent's axis and marker. `BoardPresenter` has no Arrow-specific code left, and the canvas and preview draw Arrow only through their effect lists, looking as before.
4. AC-4 (AC-BLK-01.5): Given a draft with an Arrow block that has no door of its colour on either axis side within its row or column span, when the Level Editor lists issues, then a warning names the block and the missing axis sides. With such a door present, no warning appears.
5. AC-5 (AC-BLK-01.6): Given the suites, when they run, then:
   - Domain, Gameplay EditMode and PlayMode, Level EditMode, LevelPreview, Environment and Presentation PlayMode pass;
   - the new tests cover AC-1 to AC-4 for I3, L3 and 1x1;
   - `ZeroAllocationTests` stays green with Arrow hooks bound.
6. AC-6: Given the Game scene with the user's Arrow level (after the user saves their draft), when the user plays it, then they accept the arrow movement and marker (explicit user acceptance).

## References and constraints

- Story/GDD/ADR/design/log/image:
  - docs/Color-Block-Jam-PRD.md §7.12;
  - docs/Color-Block-Jam-PRD-Mechanics-Catalog.md BLK-01 and DEC-MECH-4;
  - docs/Gameplay-Drag-And-Gate.md (hooks, DEC-110 to DEC-113, DEC-122);
  - PROJECT.md Runtime Lifecycle and Performance Rules;
  - TASK-20260926-212320271-98b25efa (the marker owner and centring rule);
  - the user's answer of 2026-09-27.
- Unity version/platform: Unity 2022.3.62f2; Android/iOS runtime; tests in the Editor
- Package/architecture constraints:
  - The Domain stays engine-free (`GameplayArchitectureTests.DomainReferencesNoEngineOrOnityAssembly`).
  - No `System.Linq` in Domain, Gameplay or Presentation; zero allocation in the Playing tick.
  - Onity binding in GameSceneInstaller; no new asmdef or package.
  - Class summaries, `m_`/`k_`/`s_` naming, and owner/stop-point comments.
- Explicit out of scope:
  - A solver (the PRE-1 check is a static issue only).
  - Turn Based Arrow (BLK-17).
  - Arrow + Rocket re-pick.
  - Ice gameplay (stays unimplemented).
  - A shipped demo level with an Arrow block.
  - The Level Editor window or canvas files held by the gate task.

## Verification and risk

- Focused test path/command: UnityMCP `run_tests` for the new Arrow Domain/Gameplay EditMode classes, `BoardPresenterArrowMarkerTests`, `GameSceneArrowTests` and `GameSceneIceTests` (PlayMode), `ArrowIssueTests`, `CanvasArrowBadgeTests`, `PreviewArrowMarkerTests`, `LevelEffectTests` and `LevelDatabaseJsonTests`. Then the Gameplay EditMode and PlayMode, Level EditMode, LevelPreview, Environment EditMode and Presentation PlayMode suites.
- Required execution preflight: `unity-cli` project readiness (the retired `unity-preflight` routes there)
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: a clean compile with no new Console errors. Check `mcpforunity://editor/state` before every refresh or test run, because another workflow shares the Editor. PlayMode must not dirty or switch the user's scene.
- EditMode:
  - axis constraint and exit gate tables;
  - converter axes;
  - hook source;
  - session drag and exit with Arrow (I3, L3, 1x1);
  - the runtime gate flip;
  - the PRE-1 issue;
  - zero allocation.
- PlayMode:
  - the Game scene loads an Arrow level database, shows markers through the real path, and drags along the axis only;
  - an exit on an axis side works, and a perpendicular exit is refused.
- Manual/built-player/profiler/device evidence: the user's acceptance in the Game scene (AC-6); device checks are not required by this task.
- Risk: high. It changes gameplay rules and the project-scope runtime gate; no serialized or schema change.
- Approval already granted (exact target only): the user's answer of 2026-09-27 "Add arrow gameplay (Recommended)", covering the gameplay rule, the runtime gate flip and the Game-scene marker
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: `/implement-task TASK-20260926-230921278-a3ffdc40-arrow-block-gameplay`

## Stop conditions

- A needed change falls outside the allowed paths, for example a serialized scene binding, an asmdef or `Data/**`.
- The Domain would need an engine reference.
- A non-Arrow behaviour or zero-allocation test changes.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:cec425f65cbfd806108aaf3d5e72e6a6573ed35b7475d415d525d5a7b3f9e9d1
- Evidence fingerprint: sha256:b55bb560a50032d17f35244c1466c96f6db9f7ce11a7be4db5cb569b8cef54a3
- Reviewed revision: none
- Attempt count: 1
- Last feedback: none
- Blocker: ENVIRONMENT: the shared Unity Editor has been hung in a Hub Play Mode transition since 07:21 (not this session's), so no Unity compile or test can run
- Next action: None
- Closed revision: vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity (unreviewed; workspace has pending changes)
- Closed at: 2026-09-28T08:48:17Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | UNPROVEN | none | none |
| AC-2 | UNPROVEN | none | none |
| AC-3 | UNPROVEN | none | none |
| AC-4 | UNPROVEN | none | none |
| AC-5 | UNPROVEN | none | none |
| AC-6 | UNPROVEN | none | none |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored 2026-09-26T23:15Z by session "Edit dropdown ve colors kareleri" from the user's answer "Add arrow gameplay (Recommended)"; scouting by a read-only Explore agent
- Started — implementation — 2026-09-26T23:16Z: the user's answer is the execution order. Only the gate-segment claim (same session, disjoint code paths) was held.
- Paused — 2026-09-26T23:45Z: at 23:35:27Z a Codex session claimed TASK-20260926-231349009-945d78fe-ice-visibility on overlapping files (BoardPresenter, GameSceneInstaller, EffectAttemptHookSource, LevelEffectRegistry, the effect tests, canvas and preview). The user chose "Stop Arrow; Codex goes first" and then ordered "mimari için ortak solid bir yapıda anlaşın". The workflow (wf_97946475-003) was stopped mid-implementation, before review. Its 31 files (changed and new, with metas and the design-doc PRE-1 lines) were moved or copied to Library/ccgs/parked/a3ffdc40; the modified files were restored from the pre-task baseline, which was checked byte-equal, so the project is back at its pre-task compiling state. docs/Block-Effects-Architecture.md records the agreed shared structure, which supersedes this contract's converter-based axis plan. The lock was released.
- Queued — 2026-09-27T17:05Z: the Ice watcher expired after 16 h with Ice still waiting (no lock; framework in place, renderer checks pending under another Claude session). New live writers overlap this task: booster-gameplay-flow (Domain/Hooks/*, Gameplay/Effects/*, GameSceneInstaller) and environment-runs-gate-motion (BoardPresenter, PreviewArrowMarkerTests). The task stays queued behind both, with a watcher armed on their claims.
- Revised — 2026-09-28T03:20Z: the booster (closed) and held-outline claims were released. Contract revision 2 adopts docs/Block-Effects-Architecture.md revision 6, which the user ordered: an Arrow module, stateless Domain hooks keyed by the footprint source, a view that moves the inline BoardPresenter marker code, canvas and preview adapters, the runtime flag and PRE-1. Paths are rewritten to the reorg's confirmed destinations. The lock was acquired; the task waits for the reorg's "restructure done" before any shared-workspace write.
- 2026-09-28T09:05Z Implemented revisions 2 and 3. Workflow wf_533ffd2b-7ad built the game side, the editor side and the docs (DEC-158); three reviewers found 12 issues. The fixer added the installer line against the Game HUD hold, and it was reverted at once. Revision 3 (decision owner) adds solver support to keep LevelSupportGateTests, moves the IceAuthoringTests assertion to layer, records two seam-only test ports and the BlockArrowMarkers comment, and accepts two deviations. Workflow wf_949fe5c2-1b8 found nothing in review. Every assembly compiles offline with 0 errors (Unity Roslyn); tests outside Unity pass for Arrow, but no Unity run has happened, because the Editor has been hung in a Hub Play Mode transition since 07:21.
- 2026-09-28T09:20Z The Game HUD session sent "done" and Haptics sent "go". Only line 107 of GameSceneInstaller.cs was changed: new ArrowEffectModule() was added after IceEffectModule. The file was handed to Haptics.
- 2026-09-28T08:48:17Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed (this task after its stale claim, held since 2026-09-28T03:12:52Z by the absent session "Edit dropdown ve colors kareleri [c818e4]", was renamed to audit TASK-20260926-230921278-a3ffdc40-arrow-block-gameplay.lock.audit.92019bd5123b5f02fceb791a0ad01ec2.recovered-20260928T084743Z on the same user order); unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
