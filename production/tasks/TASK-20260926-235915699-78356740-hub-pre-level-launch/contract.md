# TASK-20260926-235915699-78356740-hub-pre-level-launch — Pre-level popup, booster purchase and level launch from Hub

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260926-235915699-78356740-hub-pre-level-launch
- Supersedes: none
- Title: Pre-level popup, booster purchase and level launch from Hub
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:10 (head) plus uncommitted workspace changes from other live sessions
- Task kind: feature
- Area: Hub pre-level overlay, booster purchase popup, Hub-to-Game launch (`ColorBlockJam.Hub`, `Runtime/Levels`, `Gameplay/Composition`)
- Workflow: direct

## Objective and baseline

- Observable objective: The Home level button opens the pre-level popup of references 09 and 10 (Level N, Extra Time and Rocket selectors with counts or checks, close, Play); a zero-stock selector opens the reference 01 purchase popup whose Buy spends coins for stock through the save service; Play loads the Game scene with the chosen level, which the Game scene plays instead of its serialized interim index.
- Current behavior/evidence (2026-09-27, planned state after tasks 1 to 3): the level button asks `HubNavigator.OpenPreLevel` and nothing is registered; `IBoosterInventory` and `IBoosterShop` exist; `GameAttemptLoader` plays `GameSceneInstaller.m_levelIndex` (DEC-116); nothing reads `OnitySceneTransitionStore`.
- Expected behavior: `docs/Hub-And-Player-Data-Design.md` section 9: `PreLevelPresenter`/`IPreLevelView`/`PreLevelView` and `Hub/UI/PreLevel.uxml`/`.uss` in the Hub assembly; the booster purchase popup (`IBoosterPurchaseView`, `BoosterPurchaseView`, `BoosterPurchasePresenter`, `UI/Purchase/BoosterPurchasePopup.uxml`/`.uss`) in the shared `ColorBlockJam.UI` assembly because the in-game zero-stock purchase reuses it (PROJECT.md DEC-136), with a Hub adapter `Hub/Purchase/BoosterPurchaseOverlay.cs` implementing `IHubOverlay`; both instanced in `Hub.uxml`'s overlay layer and registered with `HubNavigator` explicitly (one `IHubOverlay` binding per overlay type, no ambiguous interface bindings); selectors built from `BoosterCatalog` entries with `PreLevel` context (Extra Time with `Clock.png` and the text `Extra Time`, Rocket with `Rocket_Purple_Icon.png`); selectors are rounded squares as in references 09/10 (user feedback 2026-09-27 "butonlar kare olmalı"): unselected = the supplied blue rounded square `Btn_settings_normal` with the booster icon and a red count badge at its lower right (the badge may be a UI Toolkit circle shape because no red badge sprite is supplied), selected = a green rounded square (the supplied `Btn_level_green`, or `Btn_settings_normal` tinted green, whichever renders closer to reference 10) with a green check badge, zero stock = the blue square with the `ic_plus` badge and a tap opens purchase; Buy disabled while `IsSaving`; results shown per `BoosterPurchaseResult`; `LevelLaunchRequest : IOnitySceneEnterData` in `Runtime/Levels/`; `LevelLauncher` transitions once to Gameplay with the request; `GameAttemptLoader` consumes the request first, then the Level Editor's Editor-only test-play request (DEC-127), then the serialized index, through a pure `LevelLaunchResolution` function; `BoosterCatalog.asset` lists Extra Time first so both the pre-level order (Extra Time, Rocket; reference 09) and the in-game order (Time Freeze, Rocket, Hammer, Vacuum) follow the catalog; starting boosters are carried but not applied or spent.
- Behavior that must remain unchanged: direct Play of Game with its serialized level index; the Game attempt build, session, environment and every gameplay rule; Home and Settings behaviour; player-data format; the `NoPauseOverlay` foreground-resume interim of DEC-116; Shop stays locked.

## Ownership

- Owner/writer role: Hub-and-player-data session, one implementer at a time, then an independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Runtime/Hub/PreLevel/**
  - Assets/ColorBlockJam/Runtime/Hub/PreLevel.meta
  - Assets/ColorBlockJam/Runtime/Hub/Purchase/**
  - Assets/ColorBlockJam/Runtime/Hub/Purchase.meta
  - Assets/ColorBlockJam/Runtime/Hub/Launch/**
  - Assets/ColorBlockJam/Runtime/Hub/Launch.meta
  - Assets/ColorBlockJam/UI/Hub/PreLevel.uxml
  - Assets/ColorBlockJam/UI/Hub/PreLevel.uxml.meta
  - Assets/ColorBlockJam/UI/Hub/PreLevel.uss
  - Assets/ColorBlockJam/UI/Hub/PreLevel.uss.meta
  - Assets/ColorBlockJam/UI/Purchase/**
  - Assets/ColorBlockJam/UI/Purchase.meta
  - Assets/ColorBlockJam/Runtime/UI/Purchase/**
  - Assets/ColorBlockJam/Runtime/UI/Purchase.meta
  - Assets/ColorBlockJam/Tests/UI/**
  - Assets/ColorBlockJam/UI/Hub/Hub.uxml
  - Assets/ColorBlockJam/Runtime/Hub/Composition/HubSceneInstaller.cs
  - Assets/ColorBlockJam/Runtime/Hub/Composition/HubScreen.cs
  - Assets/ColorBlockJam/UI/Common/Common.uss
  - Assets/ColorBlockJam/Runtime/Core/Levels/LevelLaunchRequest.cs
  - Assets/ColorBlockJam/Runtime/Core/Levels/LevelLaunchRequest.cs.meta
  - Assets/ColorBlockJam/Runtime/Gameplay/Composition/GameAttemptLoader.cs
  - Assets/ColorBlockJam/Runtime/Gameplay/Composition/LevelLaunchResolution.cs
  - Assets/ColorBlockJam/Runtime/Gameplay/Composition/LevelLaunchResolution.cs.meta
  - Assets/ColorBlockJam/Data/Boosters/BoosterCatalog.asset (m_definitions order only: Extra Time moved first; user approval 2026-09-27)
  - Assets/ColorBlockJam/Runtime/Gameplay/Composition/GameSceneInstaller.cs (the `m_levelIndex` tooltip text only)
  - Assets/ColorBlockJam/Tests/Hub/**
  - Assets/ColorBlockJam/Tests/Gameplay/EditMode/LevelLaunchResolutionTests.cs
  - Assets/ColorBlockJam/Tests/Gameplay/EditMode/LevelLaunchResolutionTests.cs.meta
  - docs/UI-Asset-Import.md
  - docs/Hub-And-Player-Data-Design.md
  - docs/verification/hub-pre-level/**
  - PROJECT.md
  - production/tasks/TASK-20260926-235915699-78356740-hub-pre-level-launch/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/Player/** (report defects to task 1)
  - Assets/ColorBlockJam/Runtime/Core/Levels/LevelDatabase.cs
  - Assets/ColorBlockJam/Runtime/Core/Boosters/**
  - Assets/ColorBlockJam/Runtime/Gameplay/Session/**
  - Assets/ColorBlockJam/Runtime/Gameplay/Board/**
  - Assets/ColorBlockJam/Runtime/Domain/**
  - Assets/ColorBlockJam/Scenes/**
  - Applying starting boosters, debiting stock on Play, or any opening sequence (booster opening task, AC-5.7)
  - Any other `Assets/ColorBlockJam/Art/UI/**` file or `.meta`
  - Real-money purchases or unlocking the Shop
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Data/Boosters/BoosterCatalog.asset
    Sole writer: this task's implementer through SerializedObject (reorder m_definitions only; every reference kept)
    Matching `.meta` owner: unchanged
- Other active writers/worktrees checked: 2026-09-27 02:59 local. `GameAttemptLoader.cs` is also listed by TASK-20260926-231349009-945d78fe-ice-visibility (ready), TASK-20260925-103527585-3d1b0a34-barrier-rule (ready) and TASK-20260925-160721096-2c8da893-solid-obstacle-art (ready_to_close); `GameSceneInstaller.cs` by TASK-20260926-230921278-a3ffdc40-arrow-block-gameplay (in_progress, hook-source binding only). This task changes only the level-index source in `GameAttemptLoader` and one tooltip. Before writing, run ListAgents, compare file mtimes with the read copy, and apply a surgical edit on the current file; if a live writer is editing either file, queue behind it.
- Execution dependency: TASK-20260926-235915698-c5020d50-hub-settings-overlay (stable handoff: its Hub suites green); tasks 1 and 2 verified
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given Home, when the level button is pressed, then the pre-level popup opens with `LEVEL N` (same N as the button), the close button, "Select Boosters:", an Extra Time selector (clock icon, text `Extra Time`) and a Rocket selector, both unselected, and a Play button; nothing is spent (AC-1.1a, reference 09).
2. Given stock 2 and 1, when each selector is tapped, then its rounded square turns green with a check badge (reference 10; unselected it is a blue rounded square with a red count badge, reference 09); a second tap returns it to blue with its count; neither, either or both may be selected; no stock changes (AC-1.9, reference 10).
3. Given a selector with stock 0, when tapped, then it is not selected and the purchase popup opens for that booster with its name, icon, `x1`, description, the Coin icon and price 10 (reference 01, DEC-85).
4. Given 10 or more coins, when Buy is pressed, then coins drop by 10 and the stock rises by 1 in one save, the popup stays open, counts refresh on the popup and the selector, and nothing is auto-selected; with fewer coins, "Not enough coins" shows and nothing changes; Buy cannot be pressed again while `IsSaving` is true (AC-6.7 to AC-6.9).
5. Given the pre-level popup, when a zero-stock selector opens the purchase popup, then the purchase popup shows alone over the dimmed Home, with no part of the pre-level popup visible behind it (user feedback 2026-09-27: the popups must not overlap); the rocket art stays as it is. Given the purchase popup, when closed or Back is pressed, then the pre-level popup shows again; given the pre-level popup, when closed or Back is pressed, then Home shows and the selection is cleared (AC-1.1a).
6. Given the pre-level popup, when Play is pressed, then exactly one transition to Game starts with a `LevelLaunchRequest` holding the playable level index and the selected booster IDs; a second Play press does nothing; the Game scene plays that level (AC-1.1).
7. Given Game played directly without a request, when it builds, then it plays `GameSceneInstaller.m_levelIndex` as before; given a request with index 3, it plays index 3; an index outside the database falls back to the clamped last level with one warning (tested through a pure resolution function).
8. Given selected starting boosters, when Play is pressed, then Hub and the launcher debit no stock and apply no booster up to the moment the transition starts, and the request carries the selected IDs; any debit and opening effect belong to the Game-scene opening of TASK-20260927-004448215-54655902-booster-gameplay-flow (its AC-13/AC-14), so this task asserts nothing about stock after the Game scene is ready; the design doc records this boundary.
9. Given the test runner, when Hub EditMode/PlayMode, the new Gameplay EditMode test, the existing Gameplay suites and SceneFlowSmokeTests run, then all pass; a PlayMode test presses Play in Hub and ends in Game with the requested level loaded.
10. Given captures at 1080 x 1920 and 1080 x 2400 with a simulated notch, when the pre-level popup (both selector states) and the purchase popup show, then they match references 09, 10 and 01 in structure and nothing is clipped; captures are stored under docs/verification/hub-pre-level/.
11. Given PROJECT.md and the design doc, when read, then a decision row records the launch request replacing the Hub half of DEC-116 and the carried-not-applied starting boosters, and the USS slices of `bg_popup`, `bg_popup_header`, `bg_popup_info_holder` and `Btn_popup_green` equal the borders task 3 applied (`docs/UI-Asset-Import.md`).
12. Given the shared purchase popup, when inspected and tested, then `ColorBlockJam.UI` still references no Hub, Gameplay or scene type, the presenter is covered by EditMode tests with fake views/ports, and the Hub adapter keeps it Hub-agnostic (PROJECT.md DEC-136).
13. Given the captures and references 09, 10 and 01, when the user-named chief validator (Opus 5.5 as a read-only reviewer agent; it replaced Codex `gpt-6-astra` on the user's 2026-09-27 order) reviews them, then the verdict is APPROVE, or its remaining findings are fixed.

## References and constraints

- Story/GDD/ADR/design/log/image: docs/Hub-And-Player-Data-Design.md sections 6.4, 9, 10; docs/media/color-block-jam/user-references/01-booster-purchase.png, 09, 10; docs/Color-Block-Jam-PRD.md 7.1 (AC-1.1, AC-1.1a, AC-1.9), 7.5 transition table, 7.6 (AC-6.7 to AC-6.9), 7.10 pre-level rows, DEC-47, DEC-80, DEC-84, DEC-85, DEC-86, DEC-92, AF-15, AF-16; PROJECT.md Game States, DEC-98, DEC-116
- Unity version/platform: 2022.3.62f2; Android and iOS portrait
- Package/architecture constraints: Onity scene flow (`OnitySceneFlow.TransitionAsync`, `OnitySceneTransitionStore`); no static mutable handoff; presenters plain C#; one transition in flight
- Explicit out of scope: prepare/commit/opening of starting boosters in the Game scene, the in-game purchase popup (it may reuse this view later), the level-70 reward panel, ads

## Verification and risk

- Focused test path/command: MCP `run_tests` EditMode assemblies `ColorBlockJam.Hub.EditModeTests` and `ColorBlockJam.Gameplay.EditModeTests` (filter `LevelLaunchResolutionTests`); PlayMode `ColorBlockJam.Hub.PlayModeTests`, `ColorBlockJam.Tests.PlayMode.SceneFlowSmokeTests`, and the existing Gameplay PlayMode suite
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile after `refresh_unity`; Init -> Hub -> Level -> Play -> Game runs without Console errors
- EditMode: presenters, launcher guard, launch resolution (scenarios 2 to 7)
- PlayMode: Hub Play reaches Game with the requested level (scenarios 6, 9)
- Manual/built-player/profiler/device evidence: popup captures (scenario 10); full-flow run Init -> Hub -> popup -> Play -> Game in the Editor
- Risk: medium (cross-scene data flow and a shared Gameplay file; two sprite import borders)
- Approval already granted (exact target only): user chose the whole Hub scope on 2026-09-27; explicit approval for the two sprite metas and the `GameAttemptLoader`/`GameSceneInstaller` edits is requested with this contract
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: independent verifier, then /task-cycle or automatic closure

## Stop conditions

- The user has not approved the listed targets.
- A live writer is editing `GameAttemptLoader.cs` or `GameSceneInstaller.cs`.
- Consuming the request needs a change in `Gameplay/Session/**`, the Game scene or a package.
- Onity's transition store cannot carry the request on a direct (non-Loading) transition.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:11506bfe1f85e7dde28c06acc81453f5a6c805f09d58d8413033afd3aadaa343
- Evidence fingerprint: none
- Reviewed revision: vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867
- Attempt count: 1
- Last feedback: 2026-09-27 user with references 09/10 and a Hub purchase screenshot: selectors must be rounded squares like the reference; the Hub purchase popup must not overlap the pre-level popup; the rocket image stays
- Blocker: dependency: TASK-20260927-004448215-54655902-booster-gameplay-flow owns the two remaining failures (EditMode BoosterSerializedAssetTests.GameScene_WiresTheHudDocument_EventSystem_AndInstallerFields, order-dependent; PlayMode BoardPresenterBoosterTests.AFinalVacuumRunsInsideTheSessionsBoundedWonFinishAndTeardownReturnsItsRootsAndCast, reproduces alone); told to its owner
- Next action: None
- Closed revision: vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867
- Closed at: 2026-09-28T08:46:47Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated+manual: final pass wf_3ea5c9c9-c98 (all 12 PASS) and landing re-run wf_438f6bbc-06a | vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867 |
| AC-2 | PASS | automated+manual: final pass wf_3ea5c9c9-c98 (all 12 PASS) and landing re-run wf_438f6bbc-06a | vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867 |
| AC-3 | PASS | automated+manual: final pass wf_3ea5c9c9-c98 (all 12 PASS) and landing re-run wf_438f6bbc-06a | vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867 |
| AC-4 | PASS | automated+manual: final pass wf_3ea5c9c9-c98 (all 12 PASS) and landing re-run wf_438f6bbc-06a | vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867 |
| AC-5 | PASS | automated+manual: final pass wf_3ea5c9c9-c98 (all 12 PASS) and landing re-run wf_438f6bbc-06a | vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867 |
| AC-6 | PASS | automated+manual: final pass wf_3ea5c9c9-c98 (all 12 PASS) and landing re-run wf_438f6bbc-06a | vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867 |
| AC-7 | PASS | automated+manual: final pass wf_3ea5c9c9-c98 (all 12 PASS) and landing re-run wf_438f6bbc-06a | vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867 |
| AC-8 | PASS | automated: HubLaunchTests: Hub and the launcher spend nothing up to the transition start; request carries [ExtraTime, Rocket]; no stock claim after Game is ready | vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867 |
| AC-9 | UNPROVEN | live Editor after cs:16: Hub PlayMode 17 passed + 3 capture skips (HubLaunchTests: Play in Hub ends in Game with the requested level), SceneFlowSmokeTests 6/6, Hub+UI+Player+Gameplay EditMode 776/777, Gameplay PlayMode 124/125; the five InputSystemUIInputModule failures are gone; two Booster-owned failures remain: EditMode BoosterSerializedAssetTests.GameScene_WiresTheHudDocument_EventSystem_AndInstallerFields (expects the default UI actions GUID that the cs:16 fix cleared; passes alone) and PlayMode BoardPresenterBoosterTests.AFinalVacuumRunsInsideTheSessionsBoundedWonFinishAndTeardownReturnsItsRootsAndCast ('Teardown waits for the final presentation', reproduces alone); jobs d546fe34, 093cbb23, be5e7e47, 1f878561, 9d98af25 | vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867 |
| AC-10 | PASS | automated+manual: final pass wf_3ea5c9c9-c98 (all 12 PASS) and landing re-run wf_438f6bbc-06a | vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867 |
| AC-11 | PASS | automated+manual: final pass wf_3ea5c9c9-c98 (all 12 PASS) and landing re-run wf_438f6bbc-06a | vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867 |
| AC-12 | PASS | automated+manual: final pass wf_3ea5c9c9-c98 (all 12 PASS) and landing re-run wf_438f6bbc-06a | vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867 |
| AC-13 | PASS | manual: Opus 5.5 chief review of the pre-level and purchase captures (wf_3ea5c9c9-c98): CHIEF VERDICT: APPROVE | vcs:plastic:cs:16@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3587d636fdf9f63fc416581cd3f37ac2a734424c7fbff14894ed21eb6311a867 |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- 2026-09-27 — Created — planning — contract authored from the user's Hub request and references 01, 09, 10
- 2026-09-27 — Approval recorded — planning — the user approved every serialized target listed in this contract ("Hepsini onayla"), and the portrait lock plus UI atlases ("İkisini de şimdi ekle")
- 2026-09-27 — Revised before execution — planning — user approved moving Extra Time first in BoosterCatalog.asset ("Katalogda Extra Time'ı başa al"); LevelLaunchResolution.cs added; precedence launch request > Editor test play (DEC-127, agreed with the Test Level session) > serialized index
- 2026-09-27 — Revised before execution — planning — PROJECT.md DEC-136 (user references 19/20): the booster purchase popup moves to the shared ColorBlockJam.UI assembly with a Hub adapter; bg_popup and Btn_popup_green borders are applied by task 3, so this task no longer writes those metas; explicit overlay registration; scenarios 12-13 added (shared component, chief validator)
- 2026-09-27 — Revised before execution — planning — user order "astra onayı yerine artık opus 5.5 kullan": the chief visual validator is now an Opus 5.5 read-only reviewer agent instead of Codex gpt-6-astra; scenario 13 updated, contract fingerprint recomputed
- 2026-09-27 — Started — implementation — direct implement-task; unity-cli READY WITH WARNINGS (live Editor ColorBlockJamCase2026@99077e4df1ecfaa4 shared with other sessions; identity proven this session); unity-game-dev loaded; lifecycle lock acquired; the Gameplay-side step (GameAttemptLoader, GameSceneInstaller, LevelLaunchResolution and its test) waits for the live booster-gameplay-flow owner
- 2026-09-27 — Gameplay step — implementation — the booster-gameplay-flow owner granted a one-time window (option B); LevelLaunchRequest, LevelLaunchResolution (+13 EditMode cases), the GameAttemptLoader request read into m_launch and the GameSceneInstaller tooltip landed; Load(IResolver, LevelDatabase) unchanged; Gameplay EditMode 309/309, PlayMode 46/46, SceneFlowSmokeTests 6/6; files handed back to the owner ("done" sent)
- 2026-09-27 — Contract change — implementation — user feedback before verification ("referanstaki gibi olmamış butonlar kare olmalı"; "sıkıntı ui'ın üst üste binmesi ... resim aynı kalabilir roket resmi"): selectors become rounded squares (Btn_settings_normal blue, green square, red count badge), the Hub purchase popup shows alone with the pre-level popup hidden until it closes; the first implementation run was stopped before its verifier; contract fingerprint recomputed
- 2026-09-27 — Contract change — implementation — user order "testleri en sona saklayıp development süresinden kazan": test runs, captures, measurements and the chief review move to one final verification pass; development continues with out-of-Editor compile checks only; AC-8 narrowed after coordination with booster-gameplay-flow (its AC-13/AC-14 debit selected starting boosters in the Game scene on Home): Hub and the launch spend nothing, and the Game-scene opening owns any debit; contract fingerprint recomputed
- 2026-09-28 — Development — implementation — HubLaunchTests asserts that Hub and the launcher spend nothing up to the transition start, and makes no stock claim after Game is ready (AC-8 as narrowed); design 9.3 records the boundary; ColorBlockJam.Hub.PlayModeTests compiles out of Editor; no test run (user order to defer tests)
- 2026-09-28 — Staged — verification — booster-gameplay-flow reported that, once its Game-scene opening lands, HubLaunchTests' launches spend the shared Editor in-memory stock and break a later pre-level test, and that Hub fixtures leave Hub.unity loaded; staged in scratchpad/stage-hubtests (Tests/Hub only): the index test launches without boosters, the stock-dependent tests arrange their stock through a documented test-only helper (PlayerSaveService exposes no grant API by design and is booster-owned now), every Hub PlayMode fixture returns to a clean scene in UnityTearDown; ColorBlockJam.Hub.PlayModeTests compiles out of Editor; lands after the running final verification pass, then the Hub PlayMode suite is re-run before closure
- 2026-09-28 — Verified — verification — landing re-run after the Hub test-isolation change: every Hub suite green; Gameplay PlayMode 6/130 fail identically without Hub (foreign, booster integration); AC-9 kept UNPROVEN, no defect recorded (not task-caused); waiting on that owner
- 2026-09-28 — Verified — verification — after Hub cs:15 and Booster cs:16 (Game.unity input-module fix), the live-Editor re-run: live Editor after cs:16: Hub PlayMode 17 passed + 3 capture skips (HubLaunchTests: Play in Hub ends in Game with the requested level), SceneFlowSmokeTests 6/6, Hub+UI+Player+Gameplay EditMode 776/777, Gameplay PlayMode 124/125; the five InputSystemUIInputModule failures are gone; two Booster-owned failures remain: EditMode BoosterSerializedAssetTests.GameScene_WiresTheHudDocument_EventSystem_AndInstallerFields (expects the default UI actions GUID that the cs:16 fix cleared; passes alone) and PlayMode BoardPresenterBoosterTests.AFinalVacuumRunsInsideTheSessionsBoundedWonFinishAndTeardownReturnsItsRootsAndCast ('Teardown waits for the final presentation', reproduces alone); jobs d546fe34, 093cbb23, be5e7e47, 1f878561, 9d98af25. The 12 PASS criteria are re-stamped at the new revision (owned bytes unchanged since review; none has a pending change on cs:16). AC-9 stays UNPROVEN; no defect recorded (not task-caused)
- 2026-09-28 — Contract change (path rebase) — verification — Allowed and Forbidden paths rebased onto the folder restructure (docs/Folder-Layout.md: Hub/ -> Runtime/Hub/, Hub/UI -> UI/Hub, UI/*.cs -> Runtime/UI/, Runtime/ -> Runtime/Core/, Gameplay/ -> Runtime/Gameplay/, Player/ -> Runtime/Player/, Domain/ -> Runtime/Domain/); no scope, criteria, risk or approval change; contract fingerprint recomputed
- 2026-09-28T08:46:47Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
