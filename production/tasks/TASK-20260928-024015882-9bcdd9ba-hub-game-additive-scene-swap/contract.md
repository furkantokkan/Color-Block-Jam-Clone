# TASK-20260928-024015882-9bcdd9ba-hub-game-additive-scene-swap — Additive Onity scene swap for Hub<->Game (remove the Single-mode GC.Collect)

## Identity

- Contract status: draft
- Waiting reason: approval
- Action required: User approves the transition plan: additive Hub<->Game swap without the Single-mode GC, in-scene Next Level.
- Task ID: TASK-20260928-024015882-9bcdd9ba-hub-game-additive-scene-swap
- Supersedes: none
- Title: Additive Onity scene swap for Hub<->Game (remove the Single-mode GC.Collect)
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026@efurk64@unity (Plastic workspace, shared live Editor; a Hub Plastic checkin and a PlayerSettings change are in flight there right now — see Execution dependency)
- Base revision: cs:14 (planning provenance only, as recorded in today's sibling contracts TASK-20260928-005915835-5221b8c5-game-hud-top-bar-pause-retry and TASK-20260928-014637019-659eace5-modular-home-route-and-tabs; not independently queried by this task-creation pass, which stayed read-only on the Plastic workspace while the Hub checkin was in flight — the implementer re-reads the live changeset before any shared-workspace write)
- Task kind: performance
- Area: Hub<->Game scene transition (Onity `SceneFlow`/`Async`), new `Runtime/SceneFlow` seam
- Workflow: trio

## Objective and baseline

- Observable objective: Hub<->Game level transitions use an additive Onity scene swap — load the target scene additively, activate it, unload the source scene — instead of `OnitySceneFlow.TransitionAsync`'s `LoadSceneMode.Single` load, so the measured full `GC.Collect` and the following `UnloadUnusedAssets` hitch drop out of the warm Hub->Game and Game->Hub load frames. Next Level keeps running inside the Game scene (Teardown, build the next `LevelData`, Play) with no scene reload; this contract only records that decision for a future task.
- Current behavior/evidence: Warm Hub->Game (Play, level-05, the largest level) takes 718-808 ms (median 772) to the first rendered Playing frame, then a 63-87 ms `UnloadUnusedAssets` hitch frame; warm Game->Hub (Home route) takes 686-925 ms (median 750) to a bound Home. Both go through `OnityLevelSceneTransition.TransitionAsync` (`Assets/ColorBlockJam/Hub/Launch/OnityLevelSceneTransition.cs`) calling `OnitySceneFlow.TransitionAsync(m_profile, OnitySceneFlowStateId.Gameplay, request, cancellationToken)`, which Onity's `OnitySceneFlowStateMachine` runs as a `LoadSceneMode.Single` load. The Unity Profiler shows a full `GC.Collect` of 515-575 ms inside that one `LoadLevelAsync Integrate` (about 70% of the warm load), plus 40-170 ms of other native integration (`UnloadScene` of the source, `OnityContext.Awake`, `Physics.RecreatePhysicsScene`, UI Toolkit layout), and the next frame's `UnloadUnusedAssets` (`GC.MarkDependencies`) costs another 60-90 ms. The board build itself (`GameAttemptLoader.TryBuild`) costs only 22-30 ms; Restart costs 0.4-3.8 ms and an in-scene next level costs 9-17 ms, both already cheap because neither reloads a scene. Full method, per-run numbers, and the cold first-load-after-recompile Mono-JIT finding (Editor-only, not a target): `research/load-baseline.md` and `research/load-notes.md` (copied unchanged from this session's own measurement pass; Editor-only capture, 1.35 GB managed Mono heap, no repository file was changed to measure it).
- Expected behavior: A new Onity-based additive transition replaces the Single-mode call on both directions. It hands the target scene its `IOnitySceneEnterData` through `OnitySceneTransitionStore.SetActiveEnterData`, loads the target with `OnitySceneLoader.LoadAdditiveAsync(target, ..., token)`, activates it with `SceneManager.SetActiveScene(target)` (or `OnitySceneLoader.ActivateAsync` where that fits the seam better), and unloads the source with `OnitySceneLoader.UnloadAsync(source)`; the whole sequence is cancellable through the caller's token and never calls `GC.Collect()` or `Resources.UnloadUnusedAssets()` itself (Unity's own incremental/background collection still runs on its normal schedule). This is the same shape `StartupLoader.InitializeAsync` (`Assets/ColorBlockJam/Runtime/Core/Startup/StartupLoader.cs`) already uses for Init->Hub; this task references that file for the pattern and does not edit it (forbidden path, and it belongs to the "Loading ve init" session). Hub->Game: a new `ILevelSceneTransition` implementation replaces `OnityLevelSceneTransition`'s Single-mode call behind the existing seam (`Assets/ColorBlockJam/Runtime/Hub/Launch/ILevelSceneTransition.cs`), bound in `HubSceneInstaller.cs` in place of its current `container.BindInterfacesAndSelfTo<OnityLevelSceneTransition>().AsSingle();` line; `LevelLauncher.cs` and its no-spend-before-transition call site (confirmed: no stock/coin mutation runs around `m_transition.TransitionAsync(...)`) are otherwise unchanged, and `GameAttemptLoader` keeps consuming the request exactly as it does today through `OnitySceneTransitionStore.TryConsumeActiveEnterData<LevelLaunchRequest>(out m_launch)`. Game->Hub: the same additive sequence runs back to Hub, exposed from `Runtime/Core/SceneFlow/**` so the Game scene can call it without referencing Hub. The HUD task already routes HOME through its own `IHubSceneTransition` seam (confirmed by its owner [4effc5] on 2026-09-28), so adoption there is a binding change or a one-call change once its integration window opens (acceptance criterion 6 below; recorded as an execution dependency, not an ownership claim of this task). Next Level stays exactly as today — inside the Game scene, never a scene reload — and this contract only adds the PROJECT.md decision note for the future result-popup task, which does not exist yet; no Next Level code is written here.
- Behavior that must remain unchanged: DEC-98 scene ownership (each runtime scene keeps its own `SceneContext`; direct Play of Hub and of Game still works); Init->Hub (`StartupLoader`) unchanged and unedited; DEC-127 Test Level (Level Editor Play Mode guard, start-scene swap, `LevelTestPlayRequest`); DEC-138 launch semantics (`LevelLaunchRequest` carries starting boosters without spending; nothing is spent before or during the transition; the target scene still consumes the request exactly once); DEC-92/DEC-149 save and spend rules (the transition itself never debits stock or commits a reward); 0 B per Playing tick (the swap runs between sessions, outside the Playing tick, and adds no per-frame allocation to it); no `Update`, coroutine, or `async void` in the new code, every async step takes the owning `CancellationToken` (PROJECT.md Runtime Lifecycle rules 1 and 4); every new loop/task/subscription has a named owner and a documented stop point; Unity `2022.3.62f2` stays pinned; the in-progress HUD task's live ownership of `Assets/ColorBlockJam/Gameplay/**` (this task does not write there).

## Ownership

- Owner/writer role: one implementer (Opus, high effort — cross-system async scene loading) writes the additive-swap seam and the Game->Hub adapter; an independent verifier proves the automated PlayMode suite and the manual Editor timing/capture evidence; a bugfixer engages only on a verified defect (trio workflow).
- Allowed paths (rebased onto docs/Folder-Layout.md; restructure landed in the workspace, checkin pending — see Execution dependency and Stop conditions):
  - Assets/ColorBlockJam/Runtime/Hub/Launch/**
  - Assets/ColorBlockJam/Runtime/Hub/Composition/HubSceneInstaller.cs
  - Assets/ColorBlockJam/Runtime/Core/SceneFlow/** (new folder for the additive-swap seam and the Game->Hub adapter)
  - Assets/ColorBlockJam/Tests/Hub/**
  - Assets/ColorBlockJam/Tests/Presentation/PlayMode/SceneSwap*Tests.cs (new files, plus their matching `.meta`)
  - PROJECT.md (one new DEC row plus one sentence added to the "Scenes and Onity Scopes" section only)
  - docs/Hub-And-Player-Data-Design.md (the launch section only, i.e. the DEC-138/section 9 area covering `LevelLauncher`/`ILevelSceneTransition`)
  - production/tasks/TASK-20260928-024015882-9bcdd9ba-hub-game-additive-scene-swap/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Library/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Resources/Onity/ProjectContext.prefab
  - Assets/ColorBlockJam/Runtime/Core/Startup/**
  - Assets/ColorBlockJam/Runtime/Gameplay/** (Booster- and HUD-owned; the HOME adoption happens there by dependency, not by this task's write)
  - Assets/ColorBlockJam/UI/Game/** (Booster- and HUD-owned; the HOME adoption happens there by dependency, not by this task's write)
  - any commit or checkin
- Serialized assets: none (stop and ask if a scene, prefab, the scene-flow profile asset, or `ProjectContext.prefab` needs an edit)
- Other active writers/worktrees checked: as of this reading, `production/tasks/*/contract.md` shows no task owning Hub<->Game scene-transition performance, `Assets/ColorBlockJam/Hub/Launch/**`, or a `Runtime/SceneFlow` folder (duplicate search: grepped `production/tasks` for "scene load", "LoadSceneMode", "GC.Collect", "additive", "OnitySceneLoader", "scene transition" — the only hits were this session's own new research files and the in-progress HUD task's research notes, which measured the same load cost but neither owns nor claims this fix). Live locks recorded in today's contracts: the Hub Plastic checkin and a PlayerSettings change (in flight in the shared Editor right now, per this task's assignment); folder-restructure session "Klasör düzenleme ve script organizasyonu" [7d9d8c] (will move `Hub/` -> `Runtime/Hub/`, `Hub/UI` -> `UI/Hub`, `Gameplay/` -> `Runtime/Gameplay/`, `UI/*.cs` -> `Runtime/UI/`, after the Hub and Booster checkins; not yet landed — sibling drafts TASK-20260928-014637019 and TASK-20260928-015130392 are still written against the current layout too); TASK-20260928-005915835-5221b8c5-game-hud-top-bar-pause-retry (`in_progress`, owns `Assets/ColorBlockJam/Gameplay/**` including the HOME button call site; its accepted AC-8 requires HOME to load Hub "through the Onity scene flow"); TASK-20260928-014637019-659eace5-modular-home-route-and-tabs and TASK-20260928-015130392-6c288254-booster-availability-extension (both `draft`, waiting on the same folder restructure, own Hub/Home UI and Booster/Player paths respectively — no path overlap with this contract).
- Execution dependency:
  - The Hub Plastic checkin and the PlayerSettings change in flight in the shared Editor right now: no shared-workspace write (any allowed path above) until that session's checkin lands and its lock releases.
  - The folder-restructure session "Klasör düzenleme ve script organizasyonu" [7d9d8c] has landed its moves in the workspace (`Hub/` -> `Runtime/Hub/`, `Hub/UI` -> `UI/Hub/`, `Gameplay/` -> `Runtime/Gameplay/`, `UI/*.cs` -> `Runtime/UI/`, and the rest of docs/Folder-Layout.md's mapping); its Plastic checkin is still pending. Every allowed path above is rebased onto the moved tree in this pass (recorded in the transition history below); no shared-workspace write (any allowed path above) until that checkin lands, so this task's edits are not carried by the restructure session's own changeset.
  - TASK-20260928-005915835-5221b8c5-game-hud-top-bar-pause-retry (`in_progress`): its stable handoff (integration window, scoped checkin, lock release) gates only acceptance criterion 6 below (adopting the Game->Hub adapter at its HOME call site). The rest of this task (the additive-swap seam, the Game->Hub adapter itself, and the new tests) does not need to wait on it.
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given a warm Editor session with level-05 loaded through the baseline probe method (`research/load-baseline.md`), when Hub Play launches Hub->Game three times through the new additive seam, then the launch-call-to-first-Playing-frame median is at most 250 ms and no run's load frame contains a `GC.Collect` marker or is followed by an `UnloadUnusedAssets` frame (manual Unity Profiler measurement).
2. Given the new Game->Hub swap seam, when the Home route (or its direct test entry point) runs three times, then Hub is bound at a median of at most 250 ms and no run shows a `GC.Collect` or `UnloadUnusedAssets` marker (manual Unity Profiler measurement).
3. Given the automated PlayMode suite, when Hub->Game runs through `ILevelLauncher`, then exactly the Game scene is loaded and active, Hub is unloaded, the `LevelLaunchRequest` is consumed exactly once, and booster stock is unchanged at the start of the transition; when Game->Hub runs through the new adapter, then Hub is loaded and active the same way and Game is unloaded; when a transition is cancelled mid-flight, then no scene is left half-loaded; and the existing `HubLaunchTests`, `SceneFlowSmokeTests`, and the Gameplay PlayMode suites still pass unchanged (automated).
4. Given five consecutive Hub<->Game round trips in one Editor session, when the loaded-scene count, `Object Count`, and managed heap size are sampled after each round, then none of the three grows round over round beyond ordinary noise (automated or manual capture).
5. Given a Hub->Game or a Game->Hub swap, when the transition runs, then no rendered frame after the single activation frame shows both the Hub UI/EventSystem and the Game UI active at the same time (manual capture).
6. Given the HUD task's (TASK-20260928-005915835-5221b8c5-game-hud-top-bar-pause-retry) stable handoff, when its HOME route leaves the Game scene, then the HUD's existing `IHubSceneTransition` seam runs this task's additive Game->Hub swap instead of the Single-mode scene flow, and its Level Editor Test Level branch still ends Play Mode. The seam lives in `Gameplay/Hud/Navigation`; after the move it is at `Runtime/Gameplay/Hud/Navigation/HubSceneTransition.cs`. Its installer binds it, and today its default calls `OnitySceneFlow.TransitionAsync(profile, Hub, null, token)` after `GameExitNavigator` has called `LevelSession.GoHome()`. The adoption is a binding change or a one-call change inside the HUD task's own integration window, a dependency on this task's delivery, not a write this task performs.
7. Given the completed swap, when PROJECT.md and docs/Hub-And-Player-Data-Design.md are read back, then PROJECT.md has exactly one new DEC row documenting the additive swap and the in-scene Next Level decision, plus one sentence added to the "Scenes and Onity Scopes" section, the design doc's launch section reflects the new transition, and the new row names the lifecycle owner and stop point for the added async work.
8. Given this contract's Editor-only scope, when device Android timing or a shader first-render check is asked for, then the evidence record states plainly that neither was run, unless the user separately approves a device build; this criterion does not block closing the rest of the contract.

## References and constraints

- Story/GDD/ADR/design/log/image: user request of 2026-09-28 (Turkish: "game sahnesi yüklenmesi yavaş gibi duruyor ... onitynin gücünü burada kullanabilirsin"); measured baseline `research/load-baseline.md` and `research/load-notes.md` (copied unchanged from this session's own scratchpad measurement pass); PROJECT.md DEC-98 (scene ownership and `SceneContext` lifetimes), DEC-127 (Test Level), DEC-138 (Hub launch request and carried starting boosters), DEC-140 (the additive Init->Hub pattern in `StartupLoader`, referenced not edited), DEC-92/DEC-149 (save/spend rules); docs/Hub-And-Player-Data-Design.md section 9 (Hub launch); AGENTS.md's pinned Onity package `com.onity.framework` at `8929214072` (v0.4.0).
- Unity version/platform: 2022.3.62f2; Android and iOS, portrait, 1080x1920 (this contract's acceptance evidence is Editor-only; device timing is acceptance criterion 8's explicit "not run" record).
- Package/architecture constraints: Onity `Unity.SceneFlow`/`Unity.Async` APIs only (`OnitySceneLoader.LoadAdditiveAsync`/`ActivateAsync`/`UnloadAsync`, `OnitySceneTransitionStore.SetActiveEnterData`/`TryConsumeActiveEnterData`); the vendor package (`Library/PackageCache/com.onity.framework@8929214072`) stays unmodified; no new package, assembly, or DI stack (no Zenject/VContainer/UniRx/UniTask); no `GC.Collect()` or `Resources.UnloadUnusedAssets()` call added by this task's own code; no `Update`, coroutine, or `async void`; every async step carries the owning `CancellationToken`.
- Explicit out of scope: Next Level implementation (stays in-scene; this task only writes the PROJECT.md decision note for a future result-popup task); the HOME button's own wiring inside the Game scene (HUD task's write, this task's dependency only); device/Android timing and shader warm-up measurement (recorded as not run, per acceptance criterion 8); the folder restructure itself (a different, already-running session); Shop, Collection, lives; any change to `LevelData`, the save format, or booster spend rules; Level Editor Test Level's own start-scene swap (DEC-127, unrelated code path, left unedited).

## Verification and risk

- Focused test path/command: new EditMode/PlayMode fixtures under `Assets/ColorBlockJam/Tests/Hub/EditMode` and `Assets/ColorBlockJam/Tests/Hub/PlayMode`, plus `Assets/ColorBlockJam/Tests/Presentation/PlayMode/SceneSwap*Tests.cs`; run alongside the existing `HubLaunchTests` and `SceneFlowSmokeTests` once, in the final stage, per the project's Roslyn-compile-check-first / tests-last workflow.
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev` (the implementer should also load `unity-optimization` for the profiler-evidence acceptance criteria, 1 and 2)
- Unity compilation/Console evidence: the Roslyn compile check (`unity_compile_check.py`) per changed/deleted file during development; one clean live-Editor compile and Console read once the shared-workspace dispatch window opens (after the recorded execution dependencies release).
- EditMode: the new transition seam's cancel-leaves-nothing-half-loaded behavior and its consume-once handling of the enter-data payload; the Game->Hub adapter's seam contract in isolation.
- PlayMode: Hub->Game and the new Game->Hub round trip through the real scene flow; exactly-one-active-scene and no-double-load assertions; cancellation mid-flight; the existing `HubLaunchTests`, `SceneFlowSmokeTests`, and Gameplay PlayMode suites.
- Manual/built-player/profiler/device evidence: Unity Profiler captures for acceptance criteria 1 and 2 (median over 3 runs each direction, no `GC.Collect`/`UnloadUnusedAssets` marker in the load frame); a five-round-trip loaded-scene-count/`Object Count`/managed-heap capture for criterion 4; an activation-frame-only UI-overlap capture for criterion 5; device Android timing and a shader first-render check are explicitly recorded as not run (criterion 8) unless the user separately approves a device build.
- Risk: medium
- Approval already granted (exact target only): none — the transition plan itself (additive Hub<->Game swap replacing the Single-mode load; in-scene Next Level unchanged) is the open approval question recorded in Action required.
- Commit/checkin constraint: forbidden (this task authorizes no checkin; the shared workspace already has one checkin in flight from a different session)
- Current invocation permission: none
- Handoff destination: on user approval, `implement-task TASK-20260928-024015882-9bcdd9ba-hub-game-additive-scene-swap` at Opus, high effort, via `agent-orchestration`, queued behind the Hub checkin/PlayerSettings release and the folder-restructure session for any shared-workspace write, and behind TASK-20260928-005915835-5221b8c5-game-hud-top-bar-pause-retry's stable handoff specifically for acceptance criterion 6.

## Stop conditions

- The user has not yet approved the transition plan recorded in Action required: stay `draft`; do not dispatch implementation.
- The Hub Plastic checkin or the PlayerSettings change is still in flight, or the folder-restructure session has not reported done: keep every shared-workspace write (any allowed path above) queued; do not touch `Hub/Launch/**`, `HubSceneInstaller.cs`, or `PROJECT.md` until they release.
- The folder-restructure session lands before dispatch: re-resolve every allowed path onto the moved tree (`Hub/` -> `Runtime/Hub/`, `Hub/UI` -> `UI/Hub`, `Gameplay/` -> `Runtime/Gameplay/`, `UI/*.cs` -> `Runtime/UI/`), recompute the contract fingerprint, and record the rebase in the transition history before writing anything.
- A required change would touch a scene, prefab, the scene-flow profile asset, or `ProjectContext.prefab`: stop and ask (Serialized assets: none).
- Acceptance criterion 1 or 2's median exceeds 250 ms, or any run still shows a `GC.Collect`/`UnloadUnusedAssets` marker: report the measurement plainly and do not claim the criterion met.
- The HUD task's HOME call site is not yet open for adoption: leave acceptance criterion 6 pending as a recorded dependency; do not edit `Assets/ColorBlockJam/Gameplay/**` to force it.
- Exact Unity Editor identity is unproven for a live operation at dispatch time: preserve unsaved Editor work and do not select another project.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:952a72991eb7cad2c12abbfc932a710f6899e6d5721fddae1b8d2914a3013fb2
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 0
- Last feedback: none
- Blocker: user approval of the transition plan (see Action required); execution is additionally queued behind the in-flight Hub checkin/PlayerSettings release and the folder-restructure session for any shared-workspace write, and behind TASK-20260928-005915835-5221b8c5-game-hud-top-bar-pause-retry's stable handoff for acceptance criterion 6 only
- Next action: None
- Closed revision: cs:23 (Plastic workspace head, unreviewed)
- Closed at: 2026-09-29T01:16:19Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | UNPROVEN | Manual Unity Profiler measurement (3 runs, median) | not run |
| AC-2 | UNPROVEN | Manual Unity Profiler measurement (3 runs, median) | not run |
| AC-3 | UNPROVEN | Automated PlayMode (new fixtures + HubLaunchTests + SceneFlowSmokeTests + Gameplay suites) | not run |
| AC-4 | UNPROVEN | Automated or manual capture (5 round trips) | not run |
| AC-5 | UNPROVEN | Manual capture (activation-frame check) | not run |
| AC-6 | UNPROVEN | Dependency: HUD task's integration window | not run |
| AC-7 | UNPROVEN | Manual read-back (PROJECT.md + design doc) | not run |
| AC-8 | UNPROVEN | Manual read-back (evidence record states not-run) | not run |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- 2026-09-28T02:40:15Z — Created — planning — contract authored from the user's 2026-09-28 Turkish request about slow Game-scene loading and level transitions; the measured Editor baseline was copied unchanged into `research/load-baseline.md` and `research/load-notes.md` (sha256 verified identical to the source scratchpad files); the decision owner's additive-swap plan (Hub<->Game additive seam, in-scene Next Level unchanged) was recorded as the contract's expected behavior, pending the user's approval. Execution was additionally queued behind the in-flight Hub Plastic checkin/PlayerSettings change, the folder-restructure session "Klasör düzenleme ve script organizasyonu" [7d9d8c], and TASK-20260928-005915835-5221b8c5-game-hud-top-bar-pause-retry's stable handoff (for acceptance criterion 6 only). Unity preflight: READY WITH WARNINGS — file-only inspection used throughout (Unity 2022.3.62f2 pre-Unity-6 MCP gate; no live-Editor identity claimed or needed, since this task only writes a planning artifact and the shared Editor was mid-checkin); duplicate search across `production/tasks/*/contract.md` found no existing owner of this fix.
- 2026-09-28 — Draft corrected — planning — the HUD owner [4effc5] reported that HOME already leaves the Game scene through its own `IHubSceneTransition` seam (default `HubSceneTransition` -> `OnitySceneFlow.TransitionAsync(profile, Hub, null, token)`; Test Level ends Play Mode), so the Game->Hub sentence in Expected behavior and acceptance criterion 6 now describe the adoption as a binding change or a one-call change at that seam, not a replacement of `LevelSession.GoHome()`; contract fingerprint recomputed; still draft, waiting for the user's plan approval
- 2026-09-28 — Contract change (path rebase) — planning — paths rebased onto the folder restructure (docs/Folder-Layout.md); no scope, criteria, risk or approval change; contract fingerprint recomputed
- 2026-09-29T01:16:19Z — Closed (user confirmed) — closed — explicit user order 2026-09-29 ("açık taskları da kapat üstünde çalışılmayanları artık"); prior state draft / planning. Closure mode user_confirmed: every acceptance criterion result, blocker note and defect row is preserved as recorded, not verified by this closure.
<!-- TASK-LIFECYCLE:END -->
