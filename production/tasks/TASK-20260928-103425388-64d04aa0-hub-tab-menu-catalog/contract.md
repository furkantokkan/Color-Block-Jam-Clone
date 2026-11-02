# TASK-20260928-103425388-64d04aa0-hub-tab-menu-catalog — Hub tab bar: data-driven tab and menu catalog

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260928-103425388-64d04aa0-hub-tab-menu-catalog
- Supersedes: none (takes over the "Tab and menu catalog" part and the haptics coordination of TASK-20260928-014637019-659eace5-modular-home-route-and-tabs by the user's split decision; that contract keeps difficulty, node styles and the pulse)
- Title: Hub tab bar: data-driven tab and menu catalog
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026@efurk64@unity (Plastic workspace, shared live Editor)
- Base revision: cs:17 plus pending workspace changes (the DEC-157 folder moves are still pending; the user chose to proceed)
- Task kind: feature
- Area: Hub Home tab bar, Hub navigation, Hub haptics hook
- Workflow: direct

## Objective and baseline

- Observable objective:
  - Every Home tab is driven by one catalog entry. A designer can make a tab Available, Locked, Level Gated (lock plus "Lvl N" until the player reaches level N, then open) or Hidden, pick its locked look and label, and name a menu it opens, all in the Inspector.
  - A new tab or menu needs a Home.uxml slot, a catalog entry and, for a menu, one `HubNavigator.RegisterMenu` call; no presenter, view or navigator code changes.
- Current behavior/evidence:
  - The tab bar is hard-coded in `UI/Hub/Home.uxml` and `Home.uss`: left `tab--level-locked` lock with no label, Shop and Collection dimmed with `tab-lock` and no label, Home active, right lock with "Lvl 50" (DEC-141 as revised 2026-09-28). Dividers are removed by `#tab-shop` and `#tab-support` id rules.
  - No tab navigates; no code knows about locks or level gates (design section 7).
  - `HubNavigator` opens only the three enum overlays (`HubOverlayId`: Settings, PreLevel, BoosterPurchase).
  - `HubScreen` has no haptics hook; the haptics session (DEC-156) handed its hook design to the modular Home contract so HubScreen.cs and Home.uxml keep one writer.
  - The user's requests (2026-09-28): "ui butonları da ... kitleyip level sınırı koyabileceğimiz kapatabileceğimiz ve yeni buton ve menü ekleyebileceğimiz şekilde modüler olmalı" and, in this session, "HUB da bulunan navigasyon butonlarına kolayca yeni menü eklenebilir şekilde modüler olmalı istediğimiz şekilde menüleri kapatıp açabilmeiyiz, kitlleyebilmeliyiz ve level sınırı getirebilmeliyiz, alt tarafta bulunan nav butonlara bu modüler solid çalışmasını yap".
- Expected behavior:
  - **Catalog.** `HubNavigationCatalog` (ScriptableObject, `Data/Hub/HubNavigationCatalog.asset`) holds an ordered list of `HubTabDefinition` entries in tab-bar order. Each entry has:
    - an ID (lowercase letters, digits, `-`) matching the authored `tab-<id>` slot;
    - a label and an icon (Sprite);
    - an availability: Available, Locked, Level Gated (with an unlock level of 1 or more) or Hidden, stored by value and never renumbered;
    - a locked look: Dimmed Icon With Lock or Lock Only;
    - whether a label shows while locked (Level Gated shows "Lvl N", Locked shows its own label);
    - a menu ID, empty for press feedback only.
  - **Rules.** A plain C# rule turns an entry and the saved level into Hidden, Unlocked or Locked, and a label.
  - **View.** A thin `HubTabBarView` (UI Toolkit) finds the authored `tab-<id>` Buttons in `tab-row`, shows or hides them, sets the icon from the catalog, the label, and one state class (`tab--locked` for Dimmed Icon With Lock, `tab--lock-only` for Lock Only), and removes the divider after the last shown tab and beside the active tab, so hiding a tab leaves no stray divider. It raises one press event per slot and decides nothing.
  - **Presenter.** A plain C# `HubTabPresenter` binds the entries to the slots by ID in the same way DEC-143 binds the currency holders. It follows the player's saved level reactively, so a Level Gated tab opens without a restart. Press feedback stays on every slot. Pressing an unlocked tab that names a menu asks the navigator to open that menu; a locked tab does nothing beyond press feedback. A menu ID with no registered menu, or an entry with no authored slot, logs one designer-language error.
  - **Navigation.** `HubNavigator` gains menus registered by string ID (`RegisterMenu`, `UnregisterMenu`, `HasMenu`, `OpenMenu`) on the same overlay stack, so Back closes an open menu. The presenter depends only on a small `IHubMenuNavigator`. Settings, pre-level and purchase keep their `HubOverlayId` routes.
  - **Inspector.** The catalog Inspector shows the catalog's validation result (a designer-language message naming the entry) and shows Unlock Level only for Level Gated tabs, through an Editor-only `ColorBlockJam.Hub.Editor` assembly.
  - **Shipped data** expresses today's look only through the catalog: leaderboard Locked, Lock Only, no label; shop Locked, Dimmed Icon With Lock, no label; home Available with "Home"; collection Locked, Dimmed Icon With Lock, no label; support Level Gated 50, Lock Only, "Lvl 50".
  - **Haptics hook (DEC-156 coordination).** `HubScreen.Construct` takes `IHaptics` (ColorBlockJam.Player.Haptics, bound on ProjectContext by PlayerDataInstaller). HubScreen creates `ButtonHaptics`, attaches it to the Hub document root on bind and detaches it on unbind. The Home Level button carries `cbj-haptic-confirm`; the Settings button and every tab slot carry `cbj-haptic-tap`. Every Construct caller is updated.
- Behavior that must remain unchanged: the approved Home look (tab bar within capture tolerance), press feedback on every Home button, the Level button and pre-level flow, Settings, the purchase popup, the currency holders (DEC-143), the route and its pulse, the launch request (DEC-138), Back on Home doing nothing, and every gameplay rule.

## Ownership

- Owner/writer role: this session ("Modüler navigasyon butonları"), one implementer, then an independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/UI/Hub/Home.uxml
  - Assets/ColorBlockJam/UI/Hub/Home.uss
  - Assets/ColorBlockJam/Runtime/Hub/Tabs/**
  - Assets/ColorBlockJam/Runtime/Hub/Tabs.meta
  - Assets/ColorBlockJam/Runtime/Hub/Navigation/HubNavigator.cs
  - Assets/ColorBlockJam/Runtime/Hub/Navigation/IHubMenuNavigator.cs
  - Assets/ColorBlockJam/Runtime/Hub/Navigation/IHubMenuNavigator.cs.meta
  - Assets/ColorBlockJam/Runtime/Hub/Composition/HubScreen.cs
  - Assets/ColorBlockJam/Runtime/Hub/Composition/HubSceneInstaller.cs
  - Assets/ColorBlockJam/Runtime/Hub/AssemblyInfo.cs
  - Assets/ColorBlockJam/Editor/Hub/**
  - Assets/ColorBlockJam/Editor/Hub.meta
  - Assets/ColorBlockJam/Data/Hub/HubNavigationCatalog.asset
  - Assets/ColorBlockJam/Data/Hub/HubNavigationCatalog.asset.meta
  - Assets/ColorBlockJam/Data/Hub.meta
  - Assets/ColorBlockJam/Scenes/Hub.unity (HubSceneInstaller `m_navigationCatalog` reference only)
  - Assets/ColorBlockJam/Tests/Hub/EditMode/ColorBlockJam.Hub.EditModeTests.asmdef (add the Hub Editor reference only)
  - Assets/ColorBlockJam/Tests/Hub/EditMode/HubNavigatorTests.cs
  - Assets/ColorBlockJam/Tests/Hub/EditMode/HubTab*.cs
  - Assets/ColorBlockJam/Tests/Hub/EditMode/HubTab*.cs.meta
  - Assets/ColorBlockJam/Tests/Hub/EditMode/HubNavigation*.cs
  - Assets/ColorBlockJam/Tests/Hub/EditMode/HubNavigation*.cs.meta
  - Assets/ColorBlockJam/Tests/Hub/PlayMode/HubSceneTests.cs
  - Assets/ColorBlockJam/Tests/Hub/PlayMode/HubStartupFailureTests.cs (the Construct call only)
  - Assets/ColorBlockJam/Tests/Hub/PlayMode/PreLevelCaptureTests.cs (the Construct call only)
  - docs/Hub-And-Player-Data-Design.md (sections 6.4, 7 and 10, tab and menu parts only; co-owned with TASK-20260928-015130392-6c288254-booster-availability-extension, so write guarded hunks against the live file)
  - PROJECT.md (one decision-log row; re-read the next free DEC number right before writing)
  - production/tasks/TASK-20260928-103425388-64d04aa0-hub-tab-menu-catalog/**
  - production/tasks/TASK-20260928-014637019-659eace5-modular-home-route-and-tabs/contract.md (the split's contract change only)
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Art/** (no new art; supplied sprites only)
  - Assets/ColorBlockJam/Runtime/Hub/Home/** (route, holders and pulse stay with TASK-20260928-014637019-659eace5)
  - Assets/ColorBlockJam/Runtime/Hub/PreLevel/** (owned by TASK-20260928-015130392-6c288254-booster-availability-extension)
  - Assets/ColorBlockJam/UI/Hub/PreLevel.uxml
  - Assets/ColorBlockJam/UI/Hub/PreLevel.uss
  - Assets/ColorBlockJam/Tests/Hub/EditMode/PreLevel*.cs
  - Assets/ColorBlockJam/Tests/Hub/PlayMode/HubLaunchTests.cs
  - Assets/ColorBlockJam/Runtime/Player/** (haptics service stays with its owner; use only)
  - Assets/ColorBlockJam/Runtime/UI/** (ButtonHaptics stays with its owner; use only)
  - Assets/ColorBlockJam/Runtime/Core/Levels/** (difficulty stays with TASK-20260928-014637019-659eace5)
  - Assets/ColorBlockJam/Runtime/Gameplay/**
  - Assets/ColorBlockJam/Editor/Core/**
  - docs/verification/hub-home/*.png (approved captures are not overwritten; comparisons go under this task's folder)
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Data/Hub/HubNavigationCatalog.asset (new)
    Sole writer: this task's implementer through ScriptableObject APIs in the live Editor (SerializedObject without Undo)
    Matching `.meta` owner: this task (new; the `Data/Hub` folder `.meta` too, which TASK-20260928-014637019-659eace5 will reuse for its style catalog)
  - Asset: Assets/ColorBlockJam/Scenes/Hub.unity
    Sole writer: this task's implementer through SerializedObject on HubSceneInstaller (`m_navigationCatalog` only), saved without other scene changes
    Matching `.meta` owner: unchanged
- Other active writers/worktrees checked: 2026-09-28 10:35 UTC. Live locks: booster-availability-extension (busy session "Booster sistemi dinamik UI"; PreLevel files and the Hub design doc are its paths), level-editor-validation-panel-solutions (Editor/Core/Levels only). No lock on TASK-20260928-014637019-659eace5; the sessions that read it ("Level editöründe kolay/zor task", "Görevleri inceleme ve dağıtma", "Ana menü işareti tween animasyonu") were idle. Hub files were last written at 10:45-10:48 local (pulse, dummy reward) by now-idle sessions.
- Execution dependency: none (the user chose to proceed before the DEC-157 moves are checked in; that checkin will carry this task's edits)
- Queue policy: none

## Acceptance scenarios

1. Given the shipped catalog and saved level 0, when Home binds, then the tab bar looks as approved (DEC-141): leaderboard a lock with no label, Shop and Collection dimmed with a lock and no label, Home active with "Home", support a lock with "Lvl 50"; the tab-bar region matches a capture of the current look within tolerance at 1080 x 1920 and 1080 x 2400 with a notch, and a pressed locked tab still only scales and darkens.
2. Given a Level Gated(N) tab, when the saved level rises to N while Home is shown, then the tab shows its catalog icon, its open look and its label without a rebind; below N it stays locked, showing "Lvl N" when its label shows while locked.
3. Given a Hidden tab, when Home binds, then its slot is not shown, the other tabs share the row, and no divider shows after the last shown tab or beside the active tab.
4. Given a Locked tab or a Level Gated tab below its level, when it is pressed, then no menu opens and the press gives feedback only.
5. Given a tab whose menu ID names a menu registered with `HubNavigator.RegisterMenu`, when the unlocked tab is pressed, then that menu opens on the overlay stack and Back closes it. Given a menu ID with no registered menu, a press logs one designer-language error per bind and opens nothing. Given a catalog entry with no `tab-<id>` slot in Home.uxml, binding logs one designer-language error; a slot that no entry claims stays hidden.
6. Given a new tab or menu, when it is added, then only a Home.uxml slot, a catalog entry and (for a menu) one RegisterMenu call change; `HubTabPresenter`, `HubTabBarView` and `HubNavigator` do not.
7. Given the catalog in the Inspector, when an entry is invalid (bad or duplicate ID, missing icon, unlock level below 1, bad menu ID), then a validation message names the entry; Unlock Level shows only for Level Gated tabs.
8. Given the Hub document, then the Level button carries `cbj-haptic-confirm` and the Settings button and every tab slot carry `cbj-haptic-tap`; HubScreen attaches `ButtonHaptics` to the document root on bind and detaches it on unbind.
9. Given the Hub EditMode suite and the Hub PlayMode scene, settings and startup-failure tests, when run, then all pass (Settings, pre-level and purchase overlays keep their behavior).
10. Given design sections 6.4, 7 and 10 and PROJECT.md, when read, then they describe the catalog, the tab rules, menu registration, the renamed state classes and the haptics hook, with a new decision row (next free DEC number).

## References and constraints

- Story/GDD/ADR/design/log/image:
  - production/tasks/TASK-20260928-014637019-659eace5-modular-home-route-and-tabs/contract.md (approved plan, "Tab and menu catalog" part);
  - reference docs/media/color-block-jam/user-references/06-home-tabs.png;
  - PROJECT.md DEC-86, DEC-133, DEC-141, DEC-143, DEC-156;
  - docs/Hub-And-Player-Data-Design.md sections 6.4 and 7;
  - docs/verification/hub-home/README.md.
- Unity version/platform: Unity 2022.3.62f2; Android and iOS portrait
- Package/architecture constraints:
  - UI Toolkit with authored slots bound by ID (DEC-143 pattern).
  - Onity DI; the catalog is bound by HubSceneInstaller.
  - Presenters stay plain C#; views decide nothing.
  - No new package, no new art, no Update, coroutine or static state.
  - Onity has no collection injection, so menus register explicitly.
- Explicit out of scope: a real Leaderboard, Shop, Collection or Support menu; unlocking Shop or Collection in shipped data; difficulty, node styles and the pulse (TASK-20260928-014637019-659eace5); remote config of tabs; page-style tab switching.

## Verification and risk

- Focused test path/command: shared batch EditMode `ColorBlockJam.Tests.Hub.` (HubTab*, HubNavigation*, HubNavigatorTests plus the existing Hub EditMode tests); PlayMode `ColorBlockJam.Tests.HubScene.HubSceneTests`, `HubStartupFailureTests`, `HubSettingsTests`
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: Roslyn compile check after each edit; no UXML/USS import warnings for Home.uxml/Home.uss
- EditMode: tab rules, presenter (states, reactive unlock, press routing, error logs), catalog validation, navigator menus, view classes and dividers, the shipped asset against Home.uxml, the Inspector message
- PlayMode: the Hub scene shows the shipped tab states and the haptic classes; existing Hub scene, settings and startup-failure tests
- Manual/built-player/profiler/device evidence: tab-bar before/after captures at 1080 x 1920 and 1080 x 2400-notch in an isolated copy, pixel comparison of the tab-bar region
- Risk: high (new serialized catalog, Hub.unity reference, new Editor-only assembly)
- Approval already granted (exact target only): the user approved the tab and menu catalog plan with its catalog asset and the Hub.unity catalog reference on 2026-09-28 ("Onaylıyorum", TASK-20260928-014637019-659eace5), and in this session on 2026-09-28 chose "Sadece sekmeler (Önerilen)" (split the tabs out, haptics hook included) and "Devam et (Önerilen)" (proceed before the DEC-157 moves are checked in). The Editor-only `ColorBlockJam.Hub.Editor` assembly exists only for the Inspector validation that plan requires.
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: independent verifier, then automatic closure

## Stop conditions

- Another live session owns Home.uxml, Home.uss, HubScreen.cs, HubSceneInstaller.cs, HubNavigator.cs or Hub.unity at write time.
- The change would need new art, a package, a project setting, or a PreLevel or Home presenter file.
- The live Editor is in Play Mode, compiling or importing when the scene or asset write is due (wait instead).

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:9e6d64e385ad55f42f37f0bb0f9a06257980a20a2bca6cc82ac86dd5c0a72572
- Evidence fingerprint: none
- Reviewed revision: none
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

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- 2026-09-28 — Created — planning — split from TASK-20260928-014637019-659eace5 on the user's choice "Sadece sekmeler (Önerilen)" in session "Modüler navigasyon butonları [24dfc8]"; the user also chose "Devam et (Önerilen)" to proceed before the DEC-157 moves are checked in
- 2026-09-28 — Started — implementation — direct implement-task; Unity preflight READY WITH WARNINGS (MCP for Unity instance ColorBlockJamCase2026@99077e4df1ecfaa4 proven by SHA1(Assets path) and project/info readback, version 2022.3.62f2; Editor idle and unfocused)
- 2026-09-28 — Implementation checkpoint — implementation — runtime (Runtime/Hub/Tabs, HubNavigator menus, HubScreen, HubSceneInstaller), Editor-only ColorBlockJam.Hub.Editor, Home.uxml/Home.uss, Data/Hub/HubNavigationCatalog.asset (guid 86b05c554bb446942a8b7ab1c8645424, created through SerializedObject without Undo) and the Hub.unity `m_navigationCatalog` reference are in place. The scene save also dropped HubScreen's stale `m_currencyHolderTemplate` value, a field DEC-143 had already removed. Roslyn COMPILE_OK for ColorBlockJam.Hub, .Hub.Editor, .Hub.EditModeTests and .Hub.PlayModeTests; the live Editor compiled them (13:56 local). PROJECT.md row DEC-169 (DEC-167 and DEC-168 were claimed concurrently by other sessions) and the design doc sections 2, 6.1, 6.4, 7, 7.1, 10 and 11 are written. Waiting for the user's `GetLevelGatedStatus` contribution
- 2026-09-29T01:16:19Z — Closed (user confirmed) — closed — explicit user order 2026-09-29 ("açık taskları da kapat üstünde çalışılmayanları artık"); prior state in_progress / implementation. Closure mode user_confirmed: every acceptance criterion result, blocker note and defect row is preserved as recorded, not verified by this closure. Taken over from Modüler navigasyon butonları [24dfc8] implement-task (claude) (idle 858 min).
<!-- TASK-LIFECYCLE:END -->
