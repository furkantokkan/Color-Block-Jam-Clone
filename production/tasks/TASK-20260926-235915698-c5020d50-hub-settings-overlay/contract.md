# TASK-20260926-235915698-c5020d50-hub-settings-overlay — Hub Settings popup (reference 19) as a shared component

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260926-235915698-c5020d50-hub-settings-overlay
- Supersedes: none
- Title: Hub Settings popup (reference 19) as a shared component
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:13 (head) plus uncommitted workspace changes from other live sessions
- Task kind: UI
- Area: Settings popup (new shared `ColorBlockJam.UI` assembly) and its Hub host (`ColorBlockJam.Hub`)
- Workflow: direct

## Objective and baseline

- Observable objective: The Home settings button opens the Settings popup of reference 19 over the dimmed Home: SETTINGS title, close button, Vibration, Sound and Music toggles that persist through the save service and show clearly different ON and OFF states, and placeholder buttons Legal Terms, Restore Purchases, Support and Language with press feedback only; close, Android Back and a tap outside the popup return to Home. The popup is a shared component that the in-game pause menu (reference 20) can reuse with its HOME button shown.
- Current behavior/evidence (2026-09-27): task 2 is closed; the Home settings press asks `HubNavigator.OpenSettings`. An interrupted first implementation of this task left `Hub/Settings/*.cs`, `Hub/UI/Settings.uxml/.uss`, `HubSettingsTests.cs`/`SettingsPresenterTests.cs` and edits in `HubScreen.cs`, `HubSceneInstaller.cs`, `Hub.uxml` and `Tests/Hub/**` (built for the superseded `bg_menu` design, with temporary debug output in `HubSettingsTests.cs`); the user supplied references 19 and 20 on 2026-09-27 (PROJECT.md DEC-134, DEC-135, DEC-136).
- Expected behavior: `docs/Hub-And-Player-Data-Design.md` section 8 as revised for DEC-134/DEC-136:
  - New assembly `ColorBlockJam.UI` at `Assets/ColorBlockJam/UI` (references `ColorBlockJam.Runtime`, `ColorBlockJam.Player`, `Onity.Reactive`; no scene or Hub reference) holding `Settings/ISettingsPopupView.cs`, `SettingsPopupView.cs`, `SettingsPopupPresenter.cs` and `Settings/SettingsPopup.uxml`/`.uss`. The presenter binds `IPlayerSettings` (Vibration, Sound, Music), flips the shown state at once, commits through `SetAsync`, restores the committed value with a short message on failure, ignores taps on a toggle while its save is pending, raises close requests, and exposes a HOME button that stays hidden unless a host shows it.
  - Layout per reference 19: scrim (#0B153D80, pickingMode Position) over the host screen; `bg_popup` shell; `bg_popup_header` "SETTINGS" overlapping the top edge; `Popup_Close_Button` at the top-right corner; `bg_popup_info_holder` inset holding the three rows (icon left, toggle right) and four `Btn_popup_blue` placeholder buttons (Legal Terms, Restore Purchases, Support with `Support_icon`, Language); the hidden HOME button is `Btn_popup_green` with `home_icon` and "HOME".
  - Toggle per DEC-134: ON = `btn_toggle_on` knob at the right of a dark green track; OFF = a plain light grey knob at the left of a dark navy track; the knob moves with a USS transition.
  - Hub host: `Hub/Settings/SettingsOverlay.cs` adapts the shared presenter to `IHubOverlay` and registers with `HubNavigator` (Back and close go through the navigator); the popup UXML is instanced in `Hub.uxml`'s overlay layer; the interrupted first attempt's Hub-only Settings files are replaced or removed.
- Behavior that must remain unchanged: Home layout and behaviour (task 2 captures and tests); player-data format and service; Hub.unity; press feedback style; no Settings state outside the save service; the Game scene.

## Ownership

- Owner/writer role: Hub-and-player-data session, one implementer at a time, then an independent verifier and the chief validator
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/UI/ColorBlockJam.UI.asmdef
  - Assets/ColorBlockJam/UI/ColorBlockJam.UI.asmdef.meta
  - Assets/ColorBlockJam/UI/Settings/**
  - Assets/ColorBlockJam/UI/Settings.meta
  - Assets/ColorBlockJam/UI/Common/Common.uss
  - Assets/ColorBlockJam/Hub/ColorBlockJam.Hub.asmdef (add the `ColorBlockJam.UI` reference only)
  - Assets/ColorBlockJam/Hub/Settings/**
  - Assets/ColorBlockJam/Hub/Settings.meta
  - Assets/ColorBlockJam/Hub/UI/Settings.uxml
  - Assets/ColorBlockJam/Hub/UI/Settings.uxml.meta
  - Assets/ColorBlockJam/Hub/UI/Settings.uss
  - Assets/ColorBlockJam/Hub/UI/Settings.uss.meta
  - Assets/ColorBlockJam/Hub/UI/Hub.uxml
  - Assets/ColorBlockJam/Hub/Composition/HubSceneInstaller.cs
  - Assets/ColorBlockJam/Hub/Composition/HubScreen.cs
  - Assets/ColorBlockJam/Art/UI/Panels/bg_popup.png.meta
  - Assets/ColorBlockJam/Art/UI/Panels/bg_popup_header.png.meta
  - Assets/ColorBlockJam/Art/UI/Panels/bg_popup_info_holder.png.meta
  - Assets/ColorBlockJam/Art/UI/Buttons/Btn_popup_blue.png.meta
  - Assets/ColorBlockJam/Art/UI/Buttons/Btn_popup_green.png.meta
  - Assets/ColorBlockJam/Tests/Hub/**
  - Assets/ColorBlockJam/Tests/UI/**
  - Assets/ColorBlockJam/Tests/UI.meta
  - docs/UI-Asset-Import.md
  - docs/Hub-And-Player-Data-Design.md
  - docs/verification/hub-settings/**
  - production/tasks/TASK-20260926-235915698-c5020d50-hub-settings-overlay/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Player/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Gameplay/** (the pause menu and HUD restart button belong to the Game-scene HUD work)
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Hub/Home/** except a defect found here, which goes to task 2 through task-bug
  - Assets/ColorBlockJam/UI/Themes/** (theme unchanged)
  - Any other `Assets/ColorBlockJam/Art/UI/**` file or `.meta`
  - Audio playback, haptics, legal pages, store restore, language switching (placeholders only)
  - Plastic checkin, shelve or publishing (the user ordered one checkin after the whole Hub is done; the coordinator does it)
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Art/UI/Panels/bg_popup.png.meta, Assets/ColorBlockJam/Art/UI/Panels/bg_popup_header.png.meta, Assets/ColorBlockJam/Art/UI/Panels/bg_popup_info_holder.png.meta, Assets/ColorBlockJam/Art/UI/Buttons/Btn_popup_blue.png.meta, Assets/ColorBlockJam/Art/UI/Buttons/Btn_popup_green.png.meta
    Sole writer: this task's implementer through `TextureImporter.spriteBorder` only, with the measured values in `docs/UI-Asset-Import.md`
    Matching `.meta` owner: this task (task 4 reuses these borders and no longer writes `bg_popup` or `Btn_popup_green`)
- Other active writers/worktrees checked: 2026-09-27 14:30 local. Task 2 closed; task 4 not started (its contract gets the matching revision before it starts). The ice-visibility and Level Editor sessions write Gameplay/Presentation/Board files, not these paths.
- Execution dependency: TASK-20260926-235915697-461b4f9c-hub-home-screen (closed)
- Queue policy: none

## Acceptance scenarios

1. Given Home, when the settings button is pressed, then the Settings popup of reference 19 opens over the dimmed Home: SETTINGS title tab, close button, Vibration, Sound and Music rows (in that order) with icons and toggles, and Legal Terms, Restore Purchases, Support (with its icon) and Language buttons; the HOME button is not shown (AC-1.3).
2. Given a toggle, when it is ON and when it is OFF, then ON shows the `btn_toggle_on` knob at the right of a dark green track and OFF a plain light grey knob at the left of a dark navy track, and a greyscale capture of both states still differs (AC-2.1, AC-2.2, DEC-134).
3. Given a toggle, when it is tapped, then the shown state flips at once and `IPlayerSettings.SetAsync` receives the new value; when that save fails, the toggle returns to the committed value and a short message shows; taps on that toggle are ignored while its save is pending (AC-2.2, AC-2.5).
4. Given changed toggles, when Hub is left and reopened, or the service is recreated on the same store, then the toggles show the saved values (AC-2.5, DEC-15).
5. Given Settings open, when the close button, a tap outside the popup, or Android Back (Escape) is used, then Settings closes and Home is interactive again; while Settings is open, Home buttons under it receive no press (AC-2.3, AC-2.3a, AC-2.6).
6. Given Legal Terms, Restore Purchases, Support or Language, when pressed, then each gives the shared press feedback and does nothing else (AC-2.4).
7. Given the stretched sprites, when inspected, then `bg_popup`, `bg_popup_header`, `bg_popup_info_holder`, `Btn_popup_blue` and `Btn_popup_green` borders equal the measured values in `docs/UI-Asset-Import.md` and the USS slice values; `bg_menu` is not used.
8. Given the shared component, when inspected and tested, then `ColorBlockJam.UI` has no reference to Hub, Gameplay or scene types; the presenter and view are covered by EditMode tests with fake views/ports; a host can show the HOME button and receive its press (EditMode test), and the Hub host keeps it hidden.
9. Given the test runner, when the Hub and UI EditMode suites, the Hub PlayMode suite (opening and closing Settings in the real Hub scene) and SceneFlowSmokeTests run, then all pass; the interrupted attempt's temporary debug output is gone.
10. Given captures at 1080 x 1920 (one toggle ON, one OFF) and 1080 x 2400 with a simulated notch (top 110, bottom 70), when Settings is open, then nothing is clipped, overlapping or under the notch, the popup is not distorted, and the scrim covers Home; captures are stored under docs/verification/hub-settings/.
11. Given the captures and reference 19, when the user-named chief validator (Astra, `gpt-6-astra`) reviews them, then the verdict is APPROVE, or its remaining findings are fixed.

## References and constraints

- Story/GDD/ADR/design/log/image: docs/media/color-block-jam/user-references/19-settings-home.webp and 20-pause-settings-game.webp; docs/Color-Block-Jam-Visual-References.md refs 19 and 20; PROJECT.md DEC-134, DEC-135, DEC-136; docs/Hub-And-Player-Data-Design.md sections 2, 6.4, 8, 10; docs/Color-Block-Jam-PRD.md 7.2 (AC-2.1 to AC-2.6), DEC-14, DEC-15, DEC-45, NG-3; docs/Game-Developer-Case-2026.md 4.2
- Unity version/platform: 2022.3.62f2; Android and iOS portrait
- Package/architecture constraints: UI Toolkit, USS transitions, Onity reactive ports, presenter plain C#, cancellable awaits linked to the host's lifetime token, no Update/coroutines/static mutable state, ZString for dynamic text
- Explicit out of scope: the in-game pause menu itself, the HUD restart button, audio, haptics, real legal/support/restore/language pages

## Verification and risk

- Focused test path/command: MCP `run_tests` EditMode assemblies `ColorBlockJam.Hub.EditModeTests` and the new `ColorBlockJam.UI.EditModeTests`; PlayMode assembly `ColorBlockJam.Hub.PlayModeTests`; `ColorBlockJam.Tests.PlayMode.SceneFlowSmokeTests`
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile after `refresh_unity`; no Console error when opening and closing Settings
- EditMode: shared presenter/view, Hub adapter, navigator registration (scenarios 2, 3, 5, 6, 8)
- PlayMode: open/close through the real Hub scene (scenarios 1, 4, 5, 9)
- Manual/built-player/profiler/device evidence: captures (scenarios 2, 10) and the chief validator (scenario 11)
- Risk: medium (new assembly, five sprite import borders; no scene change)
- Approval already granted (exact target only): user approved the Hub scope and sprite borders on 2026-09-27 and supplied references 19 and 20 with the instruction to follow them; the shared assembly follows from DEC-135/DEC-136
- Commit/checkin constraint: forbidden for this task (the coordinator checks in the finished Hub)
- Current invocation permission: none
- Handoff destination: independent verifier, chief validator, then /task-cycle or automatic closure

## Stop conditions

- A live writer on the Hub or UI paths.
- The shared component would need a Hub, Gameplay or scene reference.
- A required visual needs new art or a sprite outside the five listed metas.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: chief validator round 3 APPROVE; all 11 criteria PASS and D-001..D-005 VERIFIED at the reviewed revision
- Contract fingerprint: sha256:bd7c4cf9d03ef91e6370299f23eb375f17a5630331f0410d033d1da3fd7d6e6d
- Evidence fingerprint: sha256:ad43505f5903c9b2d35694a8b7ffd33a5bedd31d23ad19e092d4f197e9933b9d
- Reviewed revision: vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269
- Attempt count: 4
- Last feedback: none
- Blocker: none
- Next action: none (closed); Plastic checkin with the rest of the Hub work on the user's order
- Closed revision: vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269
- Closed at: 2026-09-27T14:13Z
- Closure mode: automatic
- Closed by: task-cycle, session Hub sahnesini oluşturma [5c52bd]
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated+manual: SettingsButton_OpensThePopupAboveHome (Hub PlayMode 1f804b82) and verifier captures vs reference 19 | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |
| AC-2 | PASS | manual+automated: AssertToggle; greyscale OFF 209 vs ON 51 (verifier captures) | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |
| AC-3 | PASS | automated: SettingsPopupPresenterTests flip/save/restore/pending (EditMode 798d55fa) | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |
| AC-4 | PASS | automated: ToggleTap_FlipsAtOnce_CommitsAndSurvivesReopeningHub + Player ASettingPersistsAcrossServices | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |
| AC-5 | PASS | automated: close/outside/Back tests (PlayMode 1f804b82, EditMode) and WhileOpen_EveryHomeButtonIsUnderTheScrim | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |
| AC-6 | PASS | automated+manual: Placeholders_GiveFeedbackOnly; legal-pressed capture | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |
| AC-7 | PASS | automated+manual: SettingsPopupAssetTests 5 borders = importer = USS; Popup_DoesNotUseBgMenu | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |
| AC-8 | PASS | automated: asmdef/compiled references UI->Player,Onity.Reactive only; HostShowsHome_ThenHomePress_RaisesHomeRequested; Hub hides HOME | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |
| AC-9 | PASS | automated: EditMode UI+Hub+Player 147/147 (7ab7c91f), Hub PlayMode 14 passed + 2 capture tests skipped by design (1c351e27), SceneFlowSmokeTests 6/6 (7a6ab899) | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |
| AC-10 | PASS | manual: verifier captures pixel-identical to stored; scrim dims all; notch safe area; nothing clipped | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |
| AC-11 | PASS | chief validator round 3 (01a0e334): CHIEF VERDICT: APPROVE on the cycle-4 captures | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| [D-001](defects/D-001.md) | ac_failure:ac-1:settings-text-not-bold | VERIFIED | AC_FAILURE | AC-1 | Settings text is thin instead of bold | cycle 4 verifier VERIFIED; chief validator round 3 APPROVE | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |
| [D-002](defects/D-002.md) | ac_failure:ac-8:home-variant-header-cropped | VERIFIED | AC_FAILURE | AC-8 | HOME variant crops the title plaque at the top of a 1080 x 1920 screen | chief validator Settings review | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |
| [D-003](defects/D-003.md) | ac_failure:ac-1:title-plaque-narrow | VERIFIED | AC_FAILURE | AC-1 | Title plaque narrower than the reference | chief validator Settings review | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |
| [D-004](defects/D-004.md) | ac_failure:ac-1:inset-fill-desaturated | VERIFIED | AC_FAILURE | AC-1 | Inset panel fill less saturated than the reference | chief validator Settings review | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |
| [D-005](defects/D-005.md) | ac_failure:ac-1:scrim-too-light | VERIFIED | AC_FAILURE | AC-1 | Scrim lighter than the reference | chief validator Settings review | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:3c1b6b839dff71f58df933b5141f8c732555bebf0f5f7ee6bd7cab77638b7269 |

### Transition history

- 2026-09-27 — Created — planning — contract authored from the user's Hub request and PRD 7.2
- 2026-09-27 — Approval recorded — planning — the user approved every serialized target listed in this contract ("Hepsini onayla"), and the portrait lock plus UI atlases ("İkisini de şimdi ekle")
- 2026-09-27 — Revised before execution — planning — bg_menu becomes the uniformly scaled Settings panel (full-screen cover showed its frame bands) and the toggle uses Btn_circle_green_small / a grey USS knob instead of the "On"-baked btn_toggle_on (chief validator round 1); PROJECT.md added for the superseding decision row; decided by the coordinating decision owner
- 2026-09-27 — Started — implementation — predecessor TASK-20260926-235915697-461b4f9c closed (stable handoff); unity-cli READY WITH WARNINGS (shared Editor); unity-game-dev loaded; lock acquired
- 2026-09-27 — Contract change — implementation — the user supplied references 19 (Settings on Home) and 20 (in-game pause = Settings + HOME) with "pause menüsü bu şekilde refleri güncelle"; the first implementation run was stopped mid-way (it followed the superseded bg_menu design); authority rewritten for DEC-134/135/136: shared ColorBlockJam.UI popup, reference-19 layout and toggles, four placeholder buttons, bg_popup and Btn_popup_green borders moved here from task 4; scenarios 10-11 added (captures, chief validator)
- 2026-09-27 — Verified — verification — relaunch implemented the shared ColorBlockJam.UI Settings popup per reference 19; independent verifier 10/10 PASS, no defects
- 2026-09-27 — task-cycle — defect-cycle — chief validator Settings review: NEW_DEFECT D-001..D-005 (no blocker)
- 2026-09-27 — task-cycle — verification — cycle 3 fixed and verified D-001..D-005
- 2026-09-27 — task-cycle — defect-cycle — chief validator round 2 kept D-001 open (CONFIRMED_RECURRENCE); the user approved a fourth attempt with the two-layer text approach
- 2026-09-27 — task-cycle — verification — cycle 4 fixed D-001 with two-layer heavy text; independent verifier VERIFIED it with no regression
- 2026-09-27 — task-cycle — closed — chief validator round 3 APPROVE; TASK_CLOSED automatically at the reviewed revision
<!-- TASK-LIFECYCLE:END -->
