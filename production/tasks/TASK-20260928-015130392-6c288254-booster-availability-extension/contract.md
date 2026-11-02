# TASK-20260928-015130392-6c288254-booster-availability-extension — Data-driven booster bars with lock, unlock level and open extension

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260928-015130392-6c288254-booster-availability-extension
- Supersedes: none
- Title: Data-driven booster bars with lock, unlock level and open extension
- Repository (absolute path): C:\Users\e-fur\Documents\Repos\ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: Plastic workspace ColorBlockJamCase2026 on /main (efurk64@unity); implementation in an isolated copy under %TEMP% with a guarded copy-back, as the booster task did
- Base revision: cs:14 (planning provenance only; execution starts after the booster task's checkin lands)
- Task kind: refactor
- Area: boosters (Runtime/Boosters data, Gameplay booster flow and HUD bar, Hub pre-level selectors)
- Workflow: direct

## Objective and baseline

- Observable objective: A designer adds, removes, reorders, locks, hides or level-gates any booster in the pre-level (Hub) and in-game (HUD) bars by editing data only, and a new booster behaviour needs one new class plus one registration line, with no change to the booster flow's existing classes.
- Current behavior/evidence:
  - `BoosterDefinition` (abstract) with `TargetedBoosterDefinition`, `TimeFreezeBoosterDefinition` and `ExtraTimeBoosterDefinition` subclasses; each subclass's `Validate` hard-codes its allowed `BoosterId`s. `BoosterCatalog` is an ordered ID registry, and placement is the `UseContexts` flag on each definition.
  - Gameplay branches on booster IDs in 13 recorded sites:
    - `BoosterUseCoordinator.Press` and the `TrySubmitTarget` switch;
    - `BoosterTargeting.IsTargeted`;
    - `StartingBoosterPreparer` and `StartingBoosterSelection.From`, which accept only Extra Time and Rocket;
    - `OpeningPlan`, hard-wired to Extra Time and Rocket;
    - the `BoosterIntent.ForTimeFreeze` ID;
    - `BoosterInstructions` (the banner text switch);
    - `BoosterBarPresenter`, which special-cases Time Freeze while frozen;
    - `GameplayHud`, `TimerPresenter` and `OpeningPresenter`, which look up Extra Time, Time Freeze and Rocket by ID;
    - `RocketReticlePresenter`;
    - `BoosterCasts`, with three fixed cast kinds.
  - The in-game bar is four fixed `booster-button-0..3` buttons in `Gameplay/UI/GameplayHud.uxml`.
  - Hub's pre-level popup already builds one selector per catalog pre-level booster (`PreLevelPresenter`, `IPreLevelView.AddSelector`), but has no locked state.
  - Nothing supports a lock switch, a hidden option or an unlock level.
- Expected behavior:
  - **Layouts.** Two `BoosterBarLayout` assets (pre-level, in-game) list ordered slots `{definition, access Unlocked | Locked | Hidden, unlock level (0 = none, else ≥ 1)}`. `BoosterCatalog` references both.
  - **Availability.** A plain C# `BoosterAvailability` computes each slot's state (Available, Locked with its level, or Hidden-and-skipped) from the layout, the level number and a runtime access override seam. It publishes a revision that bars observe.
  - **Hub.** The pre-level popup builds its selectors from the pre-level layout.
  - **HUD.** The in-game bar builds one `BoosterButton` template instance per visible slot on Home.
  - **Locked slots.** A locked slot shows `lock.png` and "Lvl N" (lock only for a manual lock) and gives press feedback only.
  - **Behaviours.** One `IBoosterBehaviour` per effect kind (cell removal, block removal, colour removal, time freeze, extra time) owns activation, target validation, intent creation, starting preparation and application. The coordinator, targeting, starting preparer and selection, opening plan, session intent application, HUD and casts call only the interface or definition data.
- Behavior that must remain unchanged:
  - Every booster rule and lifecycle rule of TASK-20260927-004448215-54655902-booster-gameplay-flow (DEC-146 to DEC-152):
    - Rocket's one-cell split and final-cell refusal;
    - Hammer's whole-block removal including Ice;
    - Vacuum's colour removal with no crosshair;
    - the Time Freeze 10 s hold with leftover delta;
    - Extra Time +10 once, before Rocket (now by layout order);
    - commit-before-effect through `Paused(Saving)`;
    - one combined starting debit on Home;
    - the zero-stock purchase popup;
    - template particles;
    - the bounded Won Finish.
  - Shared stock per `BoosterId` across both bars (DEC-78); the save format (no migration).
  - 0 B allocation in the Playing tick and nothing unattended (PROJECT.md Runtime Lifecycle, Performance Rules).
  - Default layouts reproduce today's bars exactly: pre-level Extra Time, Rocket; in-game Time Freeze, Rocket, Hammer, Vacuum; all Unlocked, no unlock level.

## Ownership

- Owner/writer role: Claude session "Booster gameplay flow [f1ed7e]" as decision owner and implementer, one writer at a time; delegated agents only on disjoint files inside the isolated copy
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Runtime/Boosters/BoosterId.cs
  - Assets/ColorBlockJam/Runtime/Boosters/BoosterCatalog.cs
  - Assets/ColorBlockJam/Runtime/Boosters/TargetedBoosterDefinition.cs
  - Assets/ColorBlockJam/Runtime/Boosters/TimeFreezeBoosterDefinition.cs
  - Assets/ColorBlockJam/Runtime/Boosters/ExtraTimeBoosterDefinition.cs
  - Assets/ColorBlockJam/Runtime/Boosters/BoosterBarLayout.cs
  - Assets/ColorBlockJam/Runtime/Boosters/BoosterBarLayout.cs.meta
  - Assets/ColorBlockJam/Runtime/Boosters/BoosterSlot.cs
  - Assets/ColorBlockJam/Runtime/Boosters/BoosterSlot.cs.meta
  - Assets/ColorBlockJam/Runtime/Boosters/BoosterAvailability.cs
  - Assets/ColorBlockJam/Runtime/Boosters/BoosterAvailability.cs.meta
  - Assets/ColorBlockJam/Runtime/Boosters/IBoosterAvailability.cs
  - Assets/ColorBlockJam/Runtime/Boosters/IBoosterAvailability.cs.meta
  - Assets/ColorBlockJam/Runtime/Boosters/BoosterSlotState.cs
  - Assets/ColorBlockJam/Runtime/Boosters/BoosterSlotState.cs.meta
  - Assets/ColorBlockJam/Gameplay/Boosters/**
  - Assets/ColorBlockJam/Gameplay/Session/LevelSession.cs
  - Assets/ColorBlockJam/Gameplay/UI/BoosterBarPresenter.cs
  - Assets/ColorBlockJam/Gameplay/UI/BoosterInstructions.cs
  - Assets/ColorBlockJam/Gameplay/UI/BoosterInstructions.cs.meta
  - Assets/ColorBlockJam/Gameplay/UI/GameplayHud.cs
  - Assets/ColorBlockJam/Gameplay/UI/GameplayHudView.cs
  - Assets/ColorBlockJam/Gameplay/UI/IGameplayHudView.cs
  - Assets/ColorBlockJam/Gameplay/UI/TimerPresenter.cs
  - Assets/ColorBlockJam/Gameplay/UI/OpeningPresenter.cs
  - Assets/ColorBlockJam/Gameplay/UI/RocketReticlePresenter.cs
  - Assets/ColorBlockJam/Gameplay/UI/GameplayHud.uxml
  - Assets/ColorBlockJam/Gameplay/UI/GameplayHud.uss
  - Assets/ColorBlockJam/Gameplay/UI/BoosterButton.uxml
  - Assets/ColorBlockJam/Gameplay/UI/BoosterButton.uxml.meta
  - Assets/ColorBlockJam/Gameplay/Board/BoosterCasts.cs
  - Assets/ColorBlockJam/Gameplay/Board/BoardPresenter.cs
  - Assets/ColorBlockJam/Gameplay/Composition/GameSceneInstaller.cs
  - Assets/ColorBlockJam/Gameplay/Composition/GameAttemptLoader.cs
  - Assets/ColorBlockJam/Hub/PreLevel/PreLevelPresenter.cs
  - Assets/ColorBlockJam/Hub/PreLevel/PreLevelView.cs
  - Assets/ColorBlockJam/Hub/PreLevel/IPreLevelView.cs
  - Assets/ColorBlockJam/Hub/UI/PreLevel.uxml
  - Assets/ColorBlockJam/Hub/UI/PreLevel.uss
  - Assets/ColorBlockJam/Tests/Gameplay/**
  - Assets/ColorBlockJam/Tests/Hub/EditMode/PreLevelPresenterTests.cs
  - Assets/ColorBlockJam/Tests/Hub/EditMode/PreLevelTestFixtures.cs
  - Assets/ColorBlockJam/Tests/Hub/EditMode/PreLevelAssetTests.cs
  - Assets/ColorBlockJam/Tests/Hub/PlayMode/HubLaunchTests.cs
  - Assets/ColorBlockJam/Tests/PlayMode/BoosterEffectViewTests.cs
  - docs/Booster-Gameplay.md
  - docs/Booster-Bars.md
  - docs/Hub-And-Player-Data-Design.md
  - PROJECT.md
  - production/tasks/TASK-20260928-015130392-6c288254-booster-availability-extension/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Domain/**
  - Assets/ColorBlockJam/Player/**
  - Assets/ColorBlockJam/Runtime/Boosters/BoosterDefinition.cs (owned by TASK-20260927-141439926-2949c75f-currency-amount-price)
  - Assets/ColorBlockJam/Runtime/Currencies/**
  - Assets/ColorBlockJam/UI/**
  - Assets/ColorBlockJam/Gameplay/Hud/**
  - Assets/ColorBlockJam/Gameplay/Board/Outline/**
  - Assets/HighlightPlus/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Resources/**
  - Assets/ColorBlockJam/Presentation/**
  - Save-format migration, new packages or assemblies, a second economy, a new session state, Update/coroutine/static event owners, Time.timeScale pauses
  - Checkin, shelve, push, build or publishing without an explicit user order
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Data/Boosters/Layouts/PreLevelBoosterBar.asset (new, with its .meta and the Layouts folder .meta)
    Sole writer: this task's implementer (copy-only batchmode editor script, deleted after use)
    Matching `.meta` owner: this task's implementer
  - Asset: Assets/ColorBlockJam/Data/Boosters/Layouts/InGameBoosterBar.asset (new, with its .meta)
    Sole writer: this task's implementer
    Matching `.meta` owner: this task's implementer
  - Asset: Assets/ColorBlockJam/Data/Boosters/BoosterCatalog.asset (two layout references added; GUID unchanged)
    Sole writer: this task's implementer, after the currency-amount-price task releases it or in agreed sequence
    Matching `.meta` owner: unchanged
  - Asset: Assets/ColorBlockJam/Data/Boosters/Definitions/Rocket.asset, Hammer.asset, Vacuum.asset (targeting instruction text and behaviour kind on the targeted definition; GUIDs unchanged)
    Sole writer: this task's implementer, sequenced with currency-amount-price, which edits the same assets' price fields
    Matching `.meta` owner: unchanged
- Other active writers/worktrees checked:
  - 2026-09-28T01:51Z live lifecycle locks:
    - hub-pre-level-launch: Hub/PreLevel, checking in now.
    - booster-gameplay-flow: this session; waiting on the Hub changeset to check in.
    - home-tab-bar-locked-ends and home-currency-holders-authored.
    - game-hud-top-bar-pause-retry: session [4effc5]. It will edit GameplayHud.uxml top-content, GameSceneInstaller, GameAttemptLoader and DragInputRouter after the booster checkin.
  - The held-outline session will apply Highlight Plus to BoardPresenter, GameSceneInstaller and the Gameplay asmdef after the booster checkin.
  - The folder-restructure session will move folders after the checkins.
  - The ready task currency-amount-price owns BoosterDefinition.cs, BoosterCatalog.cs and the definition assets' price fields.
- Execution dependency: TASK-20260927-004448215-54655902-booster-gameplay-flow — its scoped checkin lands, it closes and its lifecycle lock is released; then rebase the isolated copy on the new head, re-resolve paths moved by the folder restructure, and take Hub/PreLevel only after the hub-pre-level-launch lock is released
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. **In-game bar from data.** Given the in-game layout lists N visible slots (tested with 0, 3, 4 and 6), when an attempt reaches Home, then the HUD bar shows exactly N booster buttons in layout order. They are built from `BoosterButton.uxml` outside the Playing tick. Adding, removing or reordering slots in the asset changes the bar with no code change.
2. **Pre-level bar from data.** Given the pre-level layout, when Hub's pre-level popup opens, then its selectors follow the layout's visible slots in order, with the same state rules as the HUD.
3. **Manual lock.** Given a slot whose access is Locked, when either bar shows it, then it shows `lock.png` over the dimmed booster icon, without the count or plus badge. A press gives press feedback only: no targeting, no purchase popup, no selection, no spend.
4. **Unlock level.** Given a slot with unlock level N ≥ 1 that is otherwise Unlocked, when the level number is below N, then it is Locked and shows "Lvl N". At level N or above it is Available. In Game the attempt's level number (index + 1) decides; in Hub the playable level shown by the popup decides.
5. **Hidden slot.** Given a slot whose access is Hidden, when a bar is built, then that booster does not appear in it and the bar's slot count shrinks. The same booster can still appear in the other bar.
6. **Runtime change.** Given Hub is open or an attempt is running, when the saved level changes, or a slot's access changes through the availability override API or an Inspector edit of a layout in Play Mode, then the affected bar updates without a restart:
   - A Locked/Available change toggles the existing slot at once, allocating 0 B while Playing.
   - A Hidden/visible change rebuilds the bar outside the Playing tick: on the next Home, or at once when the attempt is not Playing.
7. **Open extension.** Given a new definition asset of an existing behaviour kind, added to the catalog and to a layout, it works in both bars and in play with no C# change. Given a new behaviour kind, the only code change is one `IBoosterBehaviour` class plus one registration line in `GameSceneInstaller`. None of the 13 recorded sites keeps a `BoosterId`-specific branch; a test pins this.
8. **Behaviour preserved.** Given the default layouts, when the existing booster, drag, gate, Ice, purchase, particle and lifecycle suites run, then they pass. Changed assertions are only the ones explicitly superseded by the dynamic bar, and each is recorded in the verification note.
9. **Stock and counts.** Given a definition's initial charges, purchase amount or price change, when the save seeds or a purchase runs, then it uses the new value and both bars' badges update reactively. Stock stays shared per `BoosterId`. A Locked or Hidden booster keeps its stock untouched and is never debited, seeded twice or offered.
10. **Starting boosters.** Given pre-level selections of any Available pre-level boosters, when Play commits on Home, then one combined debit covers them and they apply once in layout order, holding the opening. A Locked or Hidden booster in a launch request is dropped with no spend and logged once.
11. **Validation.** Given an invalid layout, then catalog validation fails on project load with a designer-language message naming the bar and slot. Invalid means any of:
    - an empty slot;
    - a booster twice in one bar;
    - a definition whose `UseContexts` lacks the bar's context;
    - an unlock level below 0;
    - a behaviour kind with no registered behaviour, checked when the Game scene composes.
12. **Lifecycle and allocation.** Given any layout, then bar construction, template cloning and availability recomputation never run in a Playing tick, except the 0 B state toggles of scenario 6. Every subscription has an owner and a stop point, no static mutable state or static event is added, and the Playing tick allocates 0 B with targeting, freeze and the new bar.
13. **Documentation.** `docs/Booster-Bars.md` has a designer how-to (add, lock, hide, level-gate a booster; add a behaviour). A new PROJECT.md DEC row and `docs/Booster-Gameplay.md` record the design. The Hub design note describes the locked selector.

## References and constraints

- Story/GDD/ADR/design/log/image:
  - User order of 2026-09-28 (Turkish original preserved in the task arguments).
  - AskUserQuestion answers of 2026-09-28:
    - "ilk seçenek olsun ama tamamen kapatma seçeneğimizde olsun çıkarma" (lock plus Lvl N, and also a fully-off option that removes the booster);
    - dynamic = slot count per bar, stock/starting counts, runtime change;
    - check in the booster task first, then this task.
  - Docs: docs/Booster-Gameplay.md (B-1 to B-33), PROJECT.md DEC-78, DEC-84, DEC-86, DEC-92, DEC-138, DEC-141 and DEC-146 to DEC-152, and docs/Hub-And-Player-Data-Design.md section 9.
  - Reference 06/DEC-141 for the "Lvl N" locked look.
- Unity version/platform: Unity 2022.3.62f2; Android/iOS portrait 1080 x 1920 and 20:9
- Package/architecture constraints:
  - Onity DI (behaviours bound in the Game scene container; availability built from the catalog), UI Toolkit (template instances, USS classes), PrimeTween, ZString only where the assembly already references it.
  - Plain C# rules with no Unity reference where practical.
  - SOLID: definitions are data, layouts are placement, availability is policy, behaviours are strategies. The coordinator depends on abstractions (the behaviour registry); new kinds extend it without modifying it (Open/Closed).
- Explicit out of scope:
  - New booster mechanics.
  - Remote config transport. Only the in-process override seam is built.
  - Tutorials or unlock celebration popups.
  - Shop or IAP.
  - Price-field changes (currency-amount-price task).
  - Save migration.
  - Level Editor changes.
  - Device builds.

## Verification and risk

- Focused test path/command:
  - Batchmode `-runTests` in the isolated copy for:
    - `ColorBlockJam.Gameplay.EditModeTests`, with new fixtures `BoosterAvailabilityTests`, `BoosterBarLayoutValidationTests`, `BoosterBehaviourRegistryTests` and `NoBoosterIdBranchTests`;
    - `ColorBlockJam.Gameplay.PlayModeTests` (the dynamic bar built on Home, locked-slot press, runtime toggle at 0 B);
    - `ColorBlockJam.Hub.EditModeTests` (`PreLevelPresenterTests`: locked selector, hidden slot, level change);
    - `ColorBlockJam.Presentation.PlayModeTests` (`BoosterEffectViewTests` unchanged).
  - Then the affected existing suites once.
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: Roslyn compile check during development. Then one clean live-Editor compile after the copy-back, with the Console free of new errors.
- EditMode: availability states for every access/level combination; layout validation messages; the behaviour registry covering every catalog kind; coordinator, targeting, preparer and opening driven only through behaviours; the 13 sites free of `BoosterId` branches; stock rules for locked and hidden boosters.
- PlayMode: HUD bar built from layouts with 0/3/4/6 slots; a locked slot refuses use and purchase; a runtime Locked/Available toggle at 0 B in Playing; a Hidden change rebuilding on Home; Hub pre-level selectors from the pre-level layout.
- Manual/built-player/profiler/device evidence: live-Editor captures of both bars with one locked "Lvl N" slot at 1080 x 1920 and 1080 x 2400, when an Editor slot is available. Device checks remain release checks.
- Risk: high
- Approval already granted (exact target only):
  - The user's order and three decisions of 2026-09-28 approve the architecture (layouts, availability, behaviour strategies, dynamic bars) and the paths above.
  - Before the first serialized write, confirm once the four serialized targets listed under Ownership: the two new layout assets, the catalog references, and the three targeted definition assets.
- Commit/checkin constraint: allowed-on-explicit-invocation
- Current invocation permission: none
- Handoff destination: implement-task TASK-20260928-015130392-6c288254-booster-availability-extension, dispatched after the booster task's checkin and closure

## Stop conditions

- The booster task's checkin has not landed, or its lifecycle lock is still held: stay WAITING_FOR_OWNER.
- The hub-pre-level-launch lock is live when Hub/PreLevel edits are due: finish the Gameplay and Runtime parts first, then wait for its release.
- The folder restructure moved an owned path: re-resolve the new path and record it before writing, and never write to the old location.
- The design requires BoosterDefinition.cs, Player/**, Domain/**, a new assembly, a package or a save migration: stop and ask.
- A default-layout run changes any preserved booster behaviour, or the Playing tick allocates: fix before continuing, never weaken a test.
- Exact Unity identity is unproven for a live-Editor operation: keep to the isolated copy and do not select another project.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:e742ec930f26ad18b8063b5427d8eefdfa05637fbcbe54d0314744686fb61f94
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 1
- Last feedback: none
- Blocker: the isolated copy %TEMP%\cbj-boosterbars-5bcc6f, its base snapshot, file list and the session scratchpad were deleted by a %TEMP% cleanup around 2026-09-28T21:44Z (00:44 local); the workspace never received any of the work. User decisions also pending: the BoosterAvailability.Evaluate lock rule (unknown level 0; Locked plus an unlock level) and target approval for StartingRocket.asset's behaviour and instruction fields
- Next action: None
- Closed revision: cs:23 (unreviewed; no task code in the workspace)
- Closed at: 2026-09-28T22:48Z
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
| AC-7 | UNPROVEN | none | none |
| AC-8 | UNPROVEN | none | none |
| AC-9 | UNPROVEN | none | none |
| AC-10 | UNPROVEN | none | none |
| AC-11 | UNPROVEN | none | none |
| AC-12 | UNPROVEN | none | none |
| AC-13 | UNPROVEN | none | none |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- 2026-09-28T01:51Z — Created — planning — contract authored from the user's order and three AskUserQuestion answers, the 13 recorded BoosterId branch sites and the live-lock survey; queued after the booster task's checkin and closure.
- 2026-09-28T12:36Z — Started — implementation — direct implement-task by session "Booster sistemi dinamik UI [5bcc6f]" under the lifecycle lock (Library/ccgs/task-lifecycle-locks); the named owner [f1ed7e] is no longer live. Dependencies met: booster-gameplay-flow checked in (cs:16) and closed, hub-pre-level-launch closed. Unity preflight READY WITH WARNINGS (2022.3.62f2, live Editor via MCP for Unity only; folder move pending; live writers on BoardPresenter.cs/BoosterCasts.cs). User decisions: rows wrap (HUD 4 per row, pre-level 3 per row), all four serialized targets approved, overlapping files last. Paths re-resolved after DEC-157 and two contract gaps recorded in path-resolution.md.
- 2026-09-28T21:44Z — Blocked — implementation — the isolated copy was lost in a %TEMP% cleanup before copy-back. At loss it compiled with 0 errors (batch Unity compile) and its dry-run copy-back listed 108 files with 0 conflicts, including the follow-up ColorBlockJamDataSetup.cs edit by session [bc7b6e]; only BoosterAvailability.Evaluate was unwritten, awaiting the user's lock-rule decision. No test ran. The decision number moved to DEC-173 because DEC-167 and DEC-172 were taken by other sessions. Reconstruction from the session transcripts is under way by the orchestrator session, with the user's approval.
- 2026-09-28T22:48Z — Closed (user_confirmed, cancelled) — closed — the user cancelled the task ("İptal et"; "mimari bir sorun varsa düzelt yeterli sadece iptal et"): the live booster system meets the brief and locks, layouts and more than four HUD slots are not wanted. Nothing was implemented in the workspace; the recovery was stopped. All thirteen criteria stay UNPROVEN, unverified and unmet; the empty defect ledger is preserved. The booster-ID branch cleanup the user allows ("fix architecture problems") is separate work, not this task.
<!-- TASK-LIFECYCLE:END -->
