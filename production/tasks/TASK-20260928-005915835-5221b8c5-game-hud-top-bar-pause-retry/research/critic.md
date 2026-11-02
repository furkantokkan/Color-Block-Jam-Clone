# Completeness critique: Game HUD top bar, Pause and Retry

Path keys: `R` = `C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026`, `A` = `R/Assets/ColorBlockJam`, `T` = `R/production/tasks/TASK-20260927-004448215-54655902-booster-gameplay-flow`. All checks were made at 2026-09-28 03:47 +0300.

## 0. The five maps describe a workspace that no longer exists

The Booster copy-back happened while the maps were being written. Every map claim of the form "the workspace lacks X" or "the two trees differ" is now wrong.

- **The trees are identical.** `diff -rq` between R and the copy finds 0 differences in `Gameplay`, `Domain`, `Player`, `UI`, `Hub`, `Runtime`, `Tests/Gameplay`, `Data/UI` and `Scenes`.
  - Modification times were preserved: `GameSceneInstaller.cs` is 03:33:38 in both trees, and `BoardPresenter.cs` is 03:44:11 in both.
  - Current sha256 prefixes are the same in both trees:
    - `GameSceneInstaller.cs` 736c2915…
    - `Game.unity` d071cfa7…
    - `LevelSession.cs` 14c1c748…
    - the Gameplay asmdef 358ee61f… (it already references `ColorBlockJam.UI` and `ColorBlockJam.Player`)
    - `SessionTransitions.cs` 6ae3c178…
  - The shared workspace therefore now has `Gameplay/UI/**`, the EventSystem and the HUD UIDocument.
- **The DEC numbers were renumbered in the shared files.** `R/PROJECT.md` (written 03:45:26) now has Booster rows DEC-146 to DEC-151 at lines 680-685. The DEC collision in the requirements map (conflict 7) and ownership map (§4) is resolved. **The next free number is DEC-152.** `R/docs` PRD and VR were rewritten at 03:40, so earlier VR line numbers have moved; reference 20 is now at VR:292-296.
- **Booster has not handed over.**
  - `T/contract.md` lifecycle block: state `in_progress`, phase `implementation (isolated copy)`.
  - The Booster lock is still held (`R/Library/ccgs/task-lifecycle-locks/…booster-gameplay-flow.lock`, taken 09-27 16:44).
  - Our editing window opens only after "copy-back, READY_TO_CLOSE and checkin, earlier on request" (`T/implementation-handoff.md:20`). READY_TO_CLOSE and checkin have not happened.
- **The held-outline lock is gone.** Its folder is no longer in the locks directory (directory changed 03:47), but its contract still reads `in_progress/verification` (`R/production/tasks/…9383f38c…/contract.md:127-128`). The ownership map lists it as live.

## 1. Contradictions between the maps

| # | Conflict | What the source actually says |
|---|---|---|
| 1 | Settings popup height with HOME shown: 1886 (requirements map, via HUB) or 1818 (ui-reuse map) | **1818.** The D-002 fix at `A/UI/Settings/SettingsPopup.uss:350-363` is current; `docs/Hub-And-Player-Data-Design.md:603-605` is out of date. The HUB tree's scrim `#0B153D80` is also out of date: the USS at :20-28 uses `#0B153DB3` (D-005). |
| 2 | Back while Paused: the ownership map quotes "UI Toolkit cancel handles Back while Paused" (`T/implementation-handoff.md:18`); the session and ui-reuse maps recommend a code-built Escape action owned by the host | **Booster's own code and its DEC-151 use the host-owned action** (`A/Gameplay/UI/InGamePurchaseHost.cs:31-38, 214-215`; `R/PROJECT.md:685`, "because the polled gameplay Back is off under Paused"). Handoff line 18 contradicts Booster's own implementation, so confirm with Booster before choosing. |
| 3 | Installer shape: 178 lines (session map), or older line numbers (ui-reuse map: NoPauseOverlay L90, HUD branch L138-146), or 167 lines without the outline (ownership map) | **The current file has 178 lines.** `NoPauseOverlay` is at :98, `HeldOutlineView` at :113, `UnavailablePurchaseHost` at :131-133 and the HUD branch at :149-156. The other two maps are out of date. |
| 4 | HUD atlas risk (requirements map, conflict 10): the top bar would "pull in HomeSettings" | **Overstated.** The booster bar already draws HomeSettings sprites `Btn_circle_green_small` and `ic_plus` (`A/Gameplay/UI/GameplayHud.uss:371, 379`; atlas membership checked in `Art/UI/Atlases/HomeSettings.spriteatlasv2`). The top bar adds sprites, not a new atlas. The draw count is still unmeasured. |
| 5 | Targeting banner: the visual map says the reference has no top bar during targeting; the requirements map treats B-22 (banner below the timer) as a requirement | **Both are true, and they conflict.** Reference `02-hammer-targeting.png` shows the whole top bar (level, time, retry, pause, coin) replaced by the banner. DEC-150 (`R/PROJECT.md:684`) and B-22 keep the banner in flow below the timer band (`GameplayHud.uss:168-174`, margin-top 56). With the taller bar, the banner would sit much lower. A decision is needed. |
| 6 | Visual map: Retry looks "inactive at level start" (screenshot 4) | Not supported by any spec. Reference 04 shows Retry at full orange mid-level (01:35 left). The claim remains an unconfirmed inference. |

## 2. Gaps no map covered, checked at the sources

1. **Level number: no value reaches the HUD.**
   - `AttemptLevel` exposes only `LevelId` (`A/Gameplay/Levels/AttemptLevel.cs:16-41`), and `IGameplayHud.Show(AttemptLevel)` receives that (`A/Gameplay/UI/IGameplayHud.cs:14`).
   - The resolved index is a local variable in `GameAttemptLoader.TrySelectLevel` (`A/Gameplay/Composition/GameAttemptLoader.cs:265-296`).
   - `LevelDatabase` has `GetAt(int)` and `TryGet(id)` but no index-of lookup (`A/Runtime/Levels/LevelDatabase.cs:94, 100`).
   - "Level N" therefore needs a new field in Booster-owned files, or a `GetAt` scan by `LevelId`. A scan also covers Test Level, which knows only the ID.
2. **The Game→Hub route works with the existing API.** `OnitySceneFlow.TransitionAsync(profile, Hub, null, ct)` checks only that the profile maps the target (`…/SceneFlow/OnitySceneFlowStateMachine.cs:60-70`). It then sets the enter data to null and loads in Single mode (:166-187). There is no from-state guard, so it also works under direct Play of Game.
   - Open question: during Level Editor Test Level, HOME would start the runtime scene flow, which DEC-127 says test play does not do.
3. **After Won or Failed the player is stuck.** No result popup exists: `SessionState.Won`/`Failed` appear only in `LevelSession.cs`.
   - From Failed only Restart and Home are allowed; there is no Pause (`A/Domain/Session/SessionTransitions.cs:104-115`).
   - From Won only NextLevel and Home are allowed (:89-100), and no `LevelSession` method calls NextLevel.
   - So the HUD Retry must branch by state: Playing → `Pause(User)` then `Restart()`; Failed → `Restart()`; Won → nothing. Otherwise HOME is unreachable after a terminal state, because Pause is refused.
4. **Double Retry (AC-4.8, PRD:337).** Pause plus Restart ends back in Playing, so a second click immediately restarts the level again. Retry needs a one-shot guard, for example per `Attempt.Generation`.
5. **Retry during the opening hold loses the starting boosters.**
   - The opening hold blocks only booster input, and "Pause preserves its progress" (`R/PROJECT.md:40-41`; B-16).
   - Teardown runs `ClearOpening`, and Restart starts with no selections (AC-5.7, PRD:358).
   - So Retry during the hold discards starting boosters that were already debited, without a refund. The user must decide whether to block Retry during the hold or accept the loss.
6. **The pause popup can fail to appear, leaving the game paused with no UI.**
   - `Pause()` from Paused only re-sets the same state value, which raises no notification (`A/Gameplay/Session/LevelSession.cs:194-214`).
   - On return to the foreground, the session resumes only when `ResumesOnForeground` is true (:315-327).
   - With a real overlay (`false`), backgrounding during Paused(Saving) or Paused(Purchase) leaves Paused(Background) with no popup. The HUD Pause button is then refused, because from Paused only Background is accepted.
   - The overlay must also show on the foreground signal.
7. **The top bar does not fit the current panel.** `BoardOverlayPanelSettings` uses match 0.5 (`A/Data/UI/BoardOverlayPanelSettings.asset:18-25`).
   - At 1080×2400 the panel is about 966×2147 units, and Booster's rule is "no fixed width exceeds 966" (`GameplayHud.uss:1-7`).
   - The reference layout needs about 1080 units: badge and panel 68..813, buttons to 990, plus margins. Pixel sizes copied from the reference overflow by about 110 units.
   - iOS targets iPhone and iPad (`R/ProjectSettings/ProjectSettings.asset:12`, `targetDevice: 2`). At 1536×2048 this panel is about 1247×1663, so the 1818-tall popup with HOME overflows vertically. Hub's Expand panel gives 1440×1920 there.
   - The same asset also drives "Board effect counters" (the Ice labels) and is an allowed path of the Ice task 945d78fe (contract:113-114).
8. **The timer holder's slice borders are too thick for the reference field.** `bg_gameplay_time_holder` now has 50/50/50/50 on its importer (`.png.meta:52`, identical in both trees). The reference's dark field is about 294×95 units, but top plus bottom borders total 100. That needs `-unity-slice-scale` below 1 (compare `GameplayHud.uss:73`).
9. **Freeze counter placement.** Reference 04 puts the snowflake bar directly under the Time panel, level with the coin row (about x 305-605, y 320-372 in the 923-wide image). The timer field also gets an ice overlay with icicles. No map measured this. Booster's `freeze-counter` sits in flow under `timer-band` (`GameplayHud.uxml:27-33`).
10. **Tests constrain the shared template.**
    - `A/Tests/Gameplay/PlayMode/GameplayHudLayoutTests.cs:59-121` requires:
      - the named elements exist;
      - `top-content`, `timer-band`, `timer-holder` and `freeze-counter` are Ignore;
      - every Label is Ignore and every Button is Position. This also covers a Settings popup placed under `hud-root`.
      - the timer holder does not block board presses (`timer-holder` is not over UI).
    - `Q` returns the first matching name, and a missing name throws (`GameplayHudView.cs:276`; `GameplayHud.TryBind` at `GameplayHud.cs:153-164` has no catch). The old timer block must therefore leave `GameplayHud.uxml` in the same change that adds our template.
11. **Ownership details the maps missed.**
    - Booster's contract also owns `Domain/Session/*.cs` (`T/contract.md:55-56`). A Playing→Restart row in the transition table is therefore a Booster edit.
    - The handoff carve-out names only `Gameplay/Hud/**` (`T/implementation-handoff.md:17`). Test folders `Tests/Gameplay/{EditMode,PlayMode}/**` stay Booster-owned, so our tests have no home yet.
12. **Nothing verified yet.**
    - Booster postponed its tests (`T/implementation-handoff.md:13`), and the task folder holds no captures. The HUD we would extend has never been seen or tested.
    - Board clearance under a taller top bar (AC-3.11) is unverified. Camera framing uses fixed world margins with no HUD term (`A/Environment/BoardCameraFraming.cs:17-18, 40-41`).
    - Whether the `:root` variables in `Common.uss` (`.cbj-title` uses `var(--cbj-title-color)`, :67-75) resolve through a UXML `<Style>` (`GameplayHud.uxml:11`) is also unverified.

## 3. Top risks when working alongside the Booster session

1. **The files keep changing under us.** Booster copied back between about 03:33 and 03:45 (BoardPresenter 03:44, PROJECT.md 03:45) and still holds its lock. Every file the bar and pause need is Booster-owned until READY_TO_CLOSE and checkin:
   - `GameplayHud.uxml`/`.uss`, `GameplayHudView`
   - `GameSceneInstaller`, `Game.unity`
   - `DragInputRouter`, `LevelSession`, `SessionTransitions`
   - the asmdef and the Gameplay tests

   Until then, write only under `Gameplay/Hud/**`, and ask for the window explicitly.
2. **One wrong element name hides the whole HUD, booster bar included.** Swapping in the top-bar template must happen in the same change as Booster's `.uxml` edit and pass the layout tests in gap 10.
3. **Retry through Pause(User) + Restart briefly enters Paused(User).** A pause overlay that subscribes to `State` would show and hide the popup, and enable and disable its Back action, inside one call. Avoid it with an internal-restart flag or a Booster-owned session method.
4. **Back can be handled twice.** Depending on how the handoff:18 versus DEC-151 question is settled, one press can reach:
   - the pause and purchase Escape actions, both enabled when the purchase popup stays open under a Background pause;
   - the router's polled Back in the tick right after a resume.
5. **The dead-end states** in gaps 3 and 6 leave the player with no way out.
6. **The panel decision (966 units wide, iPad fit)** changes an asset shared with Ice and with the booster bar's percentage layout.
7. **Concurrent doc writes.** Booster writes `PROJECT.md`, the PRD and VR in place. Reserve DEC-152 onward with Booster before writing, so the numbers do not collide again.