# TASK-20260928-124512603-1f645a0d-life-economy-fail-refill — Life economy: loss on fail, level-entry check, timed refill and coin refill

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260928-124512603-1f645a0d-life-economy-fail-refill
- Supersedes: none
- Title: Life economy: loss on fail, level-entry check, timed refill and coin refill
- Repository (absolute path): C:\Users\e-fur\Documents\Repos\ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: Plastic workspace ColorBlockJamCase2026 (efurk64@unity), branch /main, shared working copy
- Base revision: cs:17 plus pending local changes of other sessions
- Task kind: feature
- Area: Player save service (lives), Hub level entry and refill popup, Game fail/Retry/HOME life cost
- Workflow: direct

## Objective and baseline

- Observable objective: the `life` currency behaves as a life economy: a failed, retried or abandoned attempt costs one life, a level cannot start without a life, one life refills every 30 minutes up to 5, and all lives refill at once for 1 coin.
- Current behavior/evidence: user report 2026-09-28 "Fail durumunda can currency azalmıyor bunu düzelt", follow-ups "can kontrolü ve yenilenmesi ekle level girişine de" and "5 canın var mesela 5 canın bitince lobbye aktarsın 1 coin karsılıgında canını resetlesin mesela 1 can 30 dakikada dolsun". Lives are a placeholder today (PRD NG-3, DEC-155, DEC-160: "no life is deducted"); `Life.asset` starts at 5 with maximum 5; only the dummy plus button changes it (DEC-161).
- Expected behavior (user answers 2026-09-28): a life is lost when the attempt fails (timer), when the in-level Retry is confirmed, and when the pause menu's HOME leaves a level in progress; at most one life per attempt. Lives refill 1 per 30 minutes of wall-clock time up to 5, also while the app is closed. At 0 lives the fail popup's Retry (and an in-level Retry that spends the last life) returns to the lobby, and the lobby opens a refill popup that shows the time to the next life and refills all lives for 1 coin. The Home Level button opens that popup instead of the pre-level popup while no life is available.
- Behavior that must remain unchanged: win reward and progression save (DEC-159), booster purchase/consume and opening flows (DEC-92, DEC-149), the pause menu, Retry confirmation and fail popup layouts (DEC-154, DEC-155, DEC-160), the dummy plus-button reward (DEC-161, still +1 life +100 coins), the Home tab bar (DEC-169), player save format version 1 and older saves loading unchanged, nothing in the Playing tick path allocating or doing I/O.

## Ownership

- Owner/writer role: implementer (main session "Can currency azalmıyor fail durumunda [f840fe]")
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Runtime/Core/Currencies/CurrencyDefinition.cs
  - Assets/ColorBlockJam/Runtime/Player/PlayerSaveService.cs
  - Assets/ColorBlockJam/Runtime/Player/PlayerSnapshot.cs
  - Assets/ColorBlockJam/Runtime/Player/PlayerDataInstaller.cs
  - Assets/ColorBlockJam/Runtime/Player/Storage/PlayerSnapshotJson.cs
  - Assets/ColorBlockJam/Runtime/Player/Lives/**
  - Assets/ColorBlockJam/Runtime/Player/Lives.meta
  - Assets/ColorBlockJam/Runtime/Hub/Home/HomePresenter.cs
  - Assets/ColorBlockJam/Runtime/Hub/Lives/**
  - Assets/ColorBlockJam/Runtime/Hub/Lives.meta
  - Assets/ColorBlockJam/Runtime/Hub/Composition/HubScreen.cs (additive hunks only; co-owned with TASK-20260928-103425388-64d04aa0 on the user's approval 2026-09-28 "Evet, ekle")
  - Assets/ColorBlockJam/Runtime/Hub/Composition/HubSceneInstaller.cs (additive hunks only; same approval)
  - Assets/ColorBlockJam/UI/Hub/Hub.uxml
  - Assets/ColorBlockJam/Runtime/Gameplay/Hud/Menu/GameMenuFlow.cs
  - Assets/ColorBlockJam/Runtime/Gameplay/Hud/Menu/AttemptLifeCost.cs
  - Assets/ColorBlockJam/Runtime/Gameplay/Hud/Menu/AttemptLifeCost.cs.meta
  - Assets/ColorBlockJam/Data/Currencies/Life.asset
  - Assets/ColorBlockJam/Tests/Player/EditMode/Lives*.cs
  - Assets/ColorBlockJam/Tests/Player/EditMode/Lives*.cs.meta
  - Assets/ColorBlockJam/Tests/Player/EditMode/PlayerSnapshotJsonTests.cs (added cases only)
  - Assets/ColorBlockJam/Tests/Hub/EditMode/HomePresenterTests.cs (constructor update and added cases only)
  - Assets/ColorBlockJam/Tests/Hub/EditMode/LifeRefill*.cs
  - Assets/ColorBlockJam/Tests/Hub/EditMode/LifeRefill*.cs.meta
  - Assets/ColorBlockJam/Tests/Gameplay/EditMode/Hud/GameMenuFlowLivesTests.cs
  - Assets/ColorBlockJam/Tests/Gameplay/EditMode/Hud/GameMenuFlowLivesTests.cs.meta
  - PROJECT.md (one decision-log row; re-read the next free DEC number right before writing)
  - docs/Hub-And-Player-Data-Design.md (one Lives subsection)
  - docs/Game-Hud.md (life-cost sentences only)
  - docs/Color-Block-Jam-PRD.md (NG-3 wording only)
  - production/tasks/TASK-20260928-124512603-1f645a0d-life-economy-fail-refill/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Scenes/** (no scene edit; new services resolve through existing containers)
  - Assets/ColorBlockJam/Runtime/Gameplay/Composition/GameSceneInstaller.cs (owned by TASK-20260928-015130392-6c288254)
  - Assets/ColorBlockJam/Runtime/Hub/Navigation/** (owned by TASK-20260928-103425388-64d04aa0; the popup opens through the existing string-menu route)
  - Assets/ColorBlockJam/Runtime/Hub/PreLevel/** (owned by TASK-20260928-015130392-6c288254)
  - Assets/ColorBlockJam/Runtime/Hub/Tabs/**
  - Assets/ColorBlockJam/UI/Purchase/** (the shared purchase popup is reused unchanged)
  - Assets/ColorBlockJam/Art/** (no new art)
  - VCS checkin, branch or shelve
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Data/Currencies/Life.asset
    Sole writer: implementer (this task)
    Matching `.meta` owner: unchanged (no `.meta` edit)
- Other active writers/worktrees checked: 2026-09-28 15:40 ListAgents: busy sessions 635814 (Level Editor validation), cdfd9d, 11d014; no file in this task's allowed paths changed in the last hour. TASK-20260928-103425388-64d04aa0 (in_progress, idle) owns HubScreen.cs/HubSceneInstaller.cs; co-writing approved by the user. TASK-20260928-015130392-6c288254 (in_progress, isolated copy) owns GameSceneInstaller.cs and PreLevel; avoided. TASK-20260928-014637019-659eace5 (ready, not started) plans Runtime/Hub/Home/**; its implementer must rebase on this task's HomePresenter change.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given a Playing attempt with 5 lives, when the timer runs out (Failed), then lives become 4 once, the fail popup shows, and its Retry restarts the level without taking another life.
2. Given a Paused attempt with 3 lives, when the player confirms the in-level Retry, then lives become 2 and the level restarts; when the player instead presses the pause menu's HOME, then lives become 2 and the game returns to Hub. HOME after Won takes no life.
3. Given 1 life, when the attempt fails (or the in-level Retry spends the last life), then lives become 0 and Retry returns to Hub instead of restarting; Hub opens the life refill popup.
4. Given 0 lives on Home, when the player presses Level, then the refill popup opens instead of the pre-level popup; it shows 0 lives, "Next life in mm:ss" counting down, and a Buy button priced 1 coin.
5. Given the refill popup with at least 1 coin, when the player presses Buy, then lives become 5, 1 coin is spent in one save, and the popup closes; with 0 coins it shows "Not enough coins" and changes nothing; a failed save changes nothing.
6. Given lives below 5, when 30 minutes of wall-clock time pass (app open or closed), then one life is added per full 30 minutes up to 5, the partial progress is kept, and at 5 the holder shows "Full"; below 5 the Home life holder shows the time to the next life.
7. Given an older save without refill data, when it loads, then it loads unchanged (format version 1) and a below-maximum life balance starts its 30-minute timer; a save written by this build loads in the file store round trip.

## References and constraints

- Story/GDD/ADR/design/log/image: PROJECT.md DEC-92, DEC-128, DEC-129, DEC-155, DEC-160, DEC-161; docs/Hub-And-Player-Data-Design.md sections 3-5; docs/Game-Hud.md; docs/Color-Block-Jam-PRD.md NG-3
- Unity version/platform: Unity 2022.3.62f2, Android/iOS portrait
- Package/architecture constraints: Onity DI and reactive; one save owner (PlayerSaveService) writes every player value; commit before publish; no static mutable state; no Update/coroutines; UI Toolkit only; the countdown is view-owned presentation work (UI Toolkit scheduled item on the Hub document, stopped on unbind); Newtonsoft with TypeNameHandling.None.
- Explicit out of scope: server-authoritative time or clock-tamper protection (a device clock change can move refills; stated limit), notifications, a lives shop beyond the 1-coin refill, a life cost for Game-scene direct Play or Level Editor Test Level gating, new art (the refill popup reuses the shared purchase popup UXML and supplied Heart/Coin icons).

## Verification and risk

- Focused test path/command: `python C:/Users/e-fur/.claude/skills/unity-cli/scripts/unity_test_batch.py submit --project C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026 --mode EditMode --filter "ColorBlockJam.Player.Tests;ColorBlockJam.Hub.Tests.HomePresenterTests;ColorBlockJam.Hub.Tests.LifeRefill;ColorBlockJam.Gameplay.Tests.Hud" --files <changed files>`
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: Roslyn compile check COMPILE_OK for ColorBlockJam.Runtime (with --dependents), Player, Hub, Gameplay and their test assemblies
- EditMode: lives rules, save-service lives operations (fake clock, memory store), JSON round trip with refill data, HomePresenter level gating, refill overlay, GameMenuFlow life cost scenarios
- PlayMode: not applicable (no scene or MonoBehaviour lifecycle change beyond HubScreen additive wiring; covered by manual check)
- Manual/built-player/profiler/device evidence: Editor Play of Hub -> Game: fail with 1 life returns to Hub with the refill popup; Buy refills. Device check pending (not claimed).
- Risk: high (save format field, ScriptableObject asset, cross-scene flow)
- Approval already granted (exact target only): user 2026-09-28 "Onaylıyorum" for an additive player-save field, Life.asset refill settings and a new Hub popup; "Evet, ekle" for additive hunks in HubScreen.cs and HubSceneInstaller.cs; "taski üstüne al direkt" as execution authorization
- Commit/checkin constraint: forbidden
- Current invocation permission: implement (user 2026-09-28 "taski üstüne al direkt")
- Handoff destination: /implement-task TASK-20260928-124512603-1f645a0d-life-economy-fail-refill

## Stop conditions

- A live writer changes one of this task's allowed files during implementation.
- The Roslyn check or the final test batch reports errors this task cannot fix inside its allowed paths.
- A change would need a scene, prefab, package or project-setting edit.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:ccc707d5c8f8a050efe8aad1ff1e932469f939bca053581417ab9b6e8bc0d423
- Evidence fingerprint: sha256:8bdc79b7a3d711d633c576e2e83e44d49cdcc78189a8063af7a0e5ea5bf48714
- Reviewed revision: vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:7963797b64627dc505c2370bb03caec6c7b20f9ca6c022398080cff7fa9b769c
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: cs:23 (Plastic workspace head, unreviewed)
- Closed at: 2026-09-29T01:16:19Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated: GameMenuFlowLivesTests.AFailedAttemptCostsOneLifeAndItsRetryTakesNoOther (TEST_BATCH e196e824e62a PASS 194/194) | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:7963797b64627dc505c2370bb03caec6c7b20f9ca6c022398080cff7fa9b769c |
| AC-2 | PASS | automated: GameMenuFlowLivesTests.TheInLevelRetryCostsOneLifeAndRestarts, HomeFromALevelInProgressCostsOneLifeButHomeAfterWonIsFree, TheOverlayWaitsWhileTheLifeIsSaving (TEST_BATCH e196e824e62a) | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:7963797b64627dc505c2370bb03caec6c7b20f9ca6c022398080cff7fa9b769c |
| AC-3 | UNPROVEN | automated Game side: GameMenuFlowLivesTests.WithNoLifeLeftRetryReturnsToHubInsteadOfRestarting (TEST_BATCH e196e824e62a); Hub auto-open in HubScreen.BindLives needs the user's Editor check | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:7963797b64627dc505c2370bb03caec6c7b20f9ca6c022398080cff7fa9b769c |
| AC-4 | UNPROVEN | automated: HomePresenterTests.Level_AtZeroLives_OpensTheRefillPopupInsteadOfThePreLevelPopup, LifeRefillOverlayTests.Open_ShowsTheLifeCountTheTimeToTheNextLifeAndThePrice (TEST_BATCH e196e824e62a); Hub.uxml life-refill-instance builds BoosterPurchaseView in the live Editor (MCP execute_code); the real Hub screen needs the user's Editor check | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:7963797b64627dc505c2370bb03caec6c7b20f9ca6c022398080cff7fa9b769c |
| AC-5 | PASS | automated: LivesTests.ARefillForOneCoinFillsEveryLifeInOneSave, ARefillWithoutEnoughCoinsOrWithAFailedSaveChangesNothing; LifeRefillOverlayTests.Buy_ThatRefills_ClosesThePopup, Buy_WithoutEnoughCoins_ShowsAMessageAndStaysOpen (TEST_BATCH e196e824e62a) | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:7963797b64627dc505c2370bb03caec6c7b20f9ca6c022398080cff7fa9b769c |
| AC-6 | PASS | automated: LivesTests.TheRefillRuleKeepsPartialProgressAndStopsAtTheMaximum, OneLifeRefillsEveryThirtyMinutesUpToTheMaximum, AnOlderSaveStartsTheTimerAndTimeWhileClosedRefills; HomePresenterTests.Tick_BelowTheMaximum_ShowsTheTimeToTheNextLifeAndFullAtTheMaximum, Tick_WithALifeDue_CommitsOneRefreshAtATime (TEST_BATCH e196e824e62a) | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:7963797b64627dc505c2370bb03caec6c7b20f9ca6c022398080cff7fa9b769c |
| AC-7 | PASS | automated: LivesTests.AnOlderSaveStartsTheTimerAndTimeWhileClosedRefills, RefillStartsSurviveTheSaveJsonAndStayOptional; existing PlayerSnapshotJsonTests, PlayerSaveServiceTests and FilePlayerSnapshotStoreTests in the same run (TEST_BATCH e196e824e62a) | vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity+owned-sha256:7963797b64627dc505c2370bb03caec6c7b20f9ca6c022398080cff7fa9b769c |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- 2026-09-28 — Created — planning — routed from task-bug NO_TASK_MATCH (lives out of scope per NG-3); user answers: life lost on Fail + Retry + HOME, lobby + refill popup at 0, approvals for save field, Life.asset and popup, co-writing HubScreen/HubSceneInstaller
- 2026-09-28 — Started — implementation — direct implement-task on the user's "taski üstüne al direkt"; Unity preflight READY WITH WARNINGS (Unity 2022.3.62f2, CLI 1.0.0-beta.11, below the Pipeline version gate; source-first work, Roslyn check available; live Editor identity proven later only if the Life.asset write goes through MCP)
- 2026-09-28 — Implementation checkpoint — implementation — Player (CurrencyDefinition refill fields, PlayerSnapshot refill starts, PlayerSnapshotJson optional currencyRefillStarts, PlayerSaveService TakeAsync/BuyRefillAsync/RefreshRefillsAsync/GetRefillState, Lives/*), Hub (HomePresenter level gate and countdown, Lives/LifeRefillOverlay, additive HubScreen/HubSceneInstaller hunks, Hub.uxml life-refill-instance), Game (AttemptLifeCost, GameMenuFlow charges). Life.asset set through MCP for Unity on ColorBlockJamCase2026@99077e4df1ecfaa4 (identity: SHA1(Assets path) hash, project/info readback root and 2022.3.62f2; refill 1800 s, price 1 coin, Validate passed). Roslyn COMPILE_OK for ColorBlockJam.Runtime, Player, Gameplay, Hub and Hub.EditModeTests; Gameplay/Player test assemblies blocked only by the Level Complete session's in-progress RecordWinAsync(levelCount) change (LevelCompleteHost, LevelCompleteFlowTests, LevelWinRecorderTests). DEC number moved from DEC-173 (claimed by that session) to DEC-174; PROJECT.md row, design doc 3.1.1 and 4.2, Game-Hud.md and PRD NG-3 written
- 2026-09-28 — Tests passed — verification — TEST_BATCH: PASS (194/194) request=e196e824e62a mode=EditMode batch=20260928T175702-7f5643 shared_requests=1 unity_runs=1 (leader run through the live Editor, MCP job f8d8abcd2a6a402aabba329efa64a4c5; LivesTests 8, GameMenuFlowLivesTests 5, LifeRefillOverlayTests 6, HomePresenterTests 22, GameMenuFlowTests 52 plus the rest of ColorBlockJam.Tests.PlayerData). Lock released while waiting, reacquired on the result. Hub.uxml life-refill-instance checked in the live Editor. AC-1, AC-2, AC-5, AC-6, AC-7 PASS; AC-3 and AC-4 UNPROVEN pending the user's Editor check (ACTION_REQUIRED: USER_ACCEPTANCE)
- 2026-09-29T01:16:19Z — Closed (user confirmed) — closed — explicit user order 2026-09-29 ("açık taskları da kapat üstünde çalışılmayanları artık"); prior state in_progress / verification. Closure mode user_confirmed: every acceptance criterion result, blocker note and defect row is preserved as recorded, not verified by this closure.
<!-- TASK-LIFECYCLE:END -->
