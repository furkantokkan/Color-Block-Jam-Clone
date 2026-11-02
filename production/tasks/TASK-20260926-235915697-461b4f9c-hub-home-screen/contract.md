# TASK-20260926-235915697-461b4f9c-hub-home-screen — Hub Home screen per reference 06

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260926-235915697-461b4f9c-hub-home-screen
- Supersedes: none
- Title: Hub Home screen per reference 06
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:10 (head) plus uncommitted workspace changes from other live sessions
- Task kind: UI
- Area: Hub scene composition and Home screen (new `ColorBlockJam.Hub` assembly)
- Workflow: direct

## Objective and baseline

- Observable objective: Opening Hub (through Init or directly) shows the UI Toolkit Home screen of reference 06 inside the safe area: top bar with profile box, data-driven currency holders (lives "Full" with 5 on the heart, coins), settings button, a decorative level route, a large "Level N" button reading the saved level, and a five-slot tab bar (Leaderboard, Shop locked, Home active and raised, Collection locked, Support), with shared press feedback on every button.
- Current behavior/evidence (2026-09-27): `Scenes/Hub.unity` holds only `SceneScope` (SceneContext, `m_installers: []`), `Main Camera`, `Directional Light` and an empty `Hub` GameObject; no runtime UXML/USS, UIDocument, EventSystem, safe-area code or Hub assembly exists; no sprite has a nine-slice border; `docs/UI-Asset-Import.md` does not exist; `LoadingPanelSettings.asset` is the only PanelSettings.
- Expected behavior: `docs/Hub-And-Player-Data-Design.md` sections 2, 6 and 7: `ColorBlockJam.Hub` assembly with `HubSceneInstaller`, `HubScreen`, `HomePresenter`/`IHomeView`/`HomeView`, `HubNavigator` (overlay stack; overlays arrive in tasks 3 and 4), `HubBackInput`, `CurrencyText`; `Presentation/UI/SafeAreaPadding`; `UI/Common/Common.uss` with `.cbj-pressable`; `UI/Themes/ColorBlockJamRuntime.tss`; `Data/UI/HubPanelSettings.asset` (Scale With Screen Size, 1080 x 1920, Expand); `Data/UI/Fonts/LiberationSans-UITK.asset` (DEC-39); `Hub/UI/Hub.uxml` and `Home.uxml`/`Home.uss`; Hub.unity gains the installer, the UI GameObject and an `EventSystem` with `InputSystemUIInputModule`; nine-slice borders measured and applied for the stretched sprites; `docs/UI-Asset-Import.md` records all 47 UI sprites.
- Behavior that must remain unchanged: Hub `Main Camera` and `Directional Light` values (DEC-118 clear colour; owned evidence of TASK-20260925-014954719-a9370ca5-board-look-camera-light-graphics AC-3); one SceneContext per scene; Init opens Hub without Loading; Hub-to-Game scope disposal; all Game, Level Editor and player-data behaviour; no sprite file or GUID renamed.

## Ownership

- Owner/writer role: Hub-and-player-data session, one implementer at a time, then an independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Hub/**
  - Assets/ColorBlockJam/Hub.meta
  - Assets/ColorBlockJam/Presentation/UI/**
  - Assets/ColorBlockJam/Presentation/UI.meta
  - Assets/ColorBlockJam/UI/**
  - Assets/ColorBlockJam/UI.meta
  - Assets/ColorBlockJam/Data/UI/HubPanelSettings.asset
  - Assets/ColorBlockJam/Data/UI/HubPanelSettings.asset.meta
  - Assets/ColorBlockJam/Data/UI/Fonts/**
  - Assets/ColorBlockJam/Data/UI/Fonts.meta
  - Assets/ColorBlockJam/Scenes/Hub.unity
  - Assets/ColorBlockJam/Art/UI/Panels/bg_home_resource_holder.png.meta
  - Assets/ColorBlockJam/Art/UI/Buttons/Btn_level_green.png.meta
  - Assets/ColorBlockJam/Art/UI/Panels/ActiveTab.png.meta
  - Assets/ColorBlockJam/Tests/Hub/**
  - Assets/ColorBlockJam/Tests/Hub.meta
  - docs/UI-Asset-Import.md
  - docs/Hub-And-Player-Data-Design.md
  - docs/verification/hub-home/**
  - PROJECT.md
  - production/tasks/TASK-20260926-235915697-461b4f9c-hub-home-screen/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Player/** (task 1 output; report defects to task 1)
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Gameplay/**
  - Assets/ColorBlockJam/Editor/**
  - Assets/ColorBlockJam/Presentation/ColorBlockJam.Presentation.asmdef
  - Assets/ColorBlockJam/Scenes/Game.unity
  - Assets/ColorBlockJam/Scenes/Init.unity
  - Assets/ColorBlockJam/Scenes/Loading.unity
  - Assets/ColorBlockJam/Scenes/LevelEditor.unity
  - Assets/ColorBlockJam/Data/UI/LoadingPanelSettings.asset
  - Any other `Assets/ColorBlockJam/Art/UI/**` file or `.meta`
  - Changing Hub `Main Camera` or `Directional Light` values
  - UGUI canvases, TextMeshPro UI, legacy `Input.*`, PrimeTween for UI animation (DEC-45), `Update`/coroutines in Hub code
  - Sprite Atlas assets and the EditorSettings sprite packer mode (follow-up task)
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Scenes/Hub.unity
    Sole writer: this task's implementer through Unity APIs, scene opened additively so the live Editor's active scene is untouched, saved once, then closed
    Matching `.meta` owner: unchanged
  - Asset: Assets/ColorBlockJam/Data/UI/HubPanelSettings.asset
    Sole writer: this task's implementer (new)
    Matching `.meta` owner: this task (new)
  - Asset: Assets/ColorBlockJam/Data/UI/Fonts/LiberationSans-UITK.asset
    Sole writer: this task's implementer (new, created from `Assets/TextMesh Pro/Fonts/LiberationSans.ttf` with the UI Toolkit FontAsset API)
    Matching `.meta` owner: this task (new)
  - Asset: Assets/ColorBlockJam/Art/UI/Panels/bg_home_resource_holder.png.meta, Assets/ColorBlockJam/Art/UI/Buttons/Btn_level_green.png.meta, Assets/ColorBlockJam/Art/UI/Panels/ActiveTab.png.meta
    Sole writer: this task's implementer through `TextureImporter.spriteBorder` only (no other importer field)
    Matching `.meta` owner: this task
- Other active writers/worktrees checked: 2026-09-27 02:59 local. TASK-20260925-014954719-a9370ca5-board-look-camera-light-graphics (in_progress, verification, waiting on a user decision) lists Hub.unity for its already-applied camera change; this task preserves those values and only adds objects. No task owns `Hub/**`, `UI/**`, `Presentation/UI/**` or the three sprite metas. Recheck ListAgents and the Editor's dirty scenes before opening Hub.unity.
- Execution dependency: TASK-20260926-235915696-6b7f82f4-player-data-currencies must be implemented and its ports bound in ProjectContext (stable handoff: its EditMode suite green in the live Editor)
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given Hub.unity, when inspected, then `SceneScope` lists `HubSceneInstaller`, a `Hub` GameObject under it holds `UIDocument` (HubPanelSettings, `Hub.uxml`) and `HubScreen`, an `EventSystem` with `InputSystemUIInputModule` exists, camera and light values equal the pre-task values, and the scene has exactly one SceneContext.
2. Given `HubPanelSettings.asset`, when inspected, then it uses Scale With Screen Size, 1080 x 1920, Expand, the `ColorBlockJamRuntime.tss` theme whose default font is the LiberationSans UI Toolkit FontAsset, and the dynamic atlas is on.
3. Given the Game view at 1080 x 1920 in Play Mode from Init, when Home shows, then a capture shows reference 06's structure: profile box top left, a life holder with "Full" and 5 on the heart, a coin holder with the balance, both plus badges, a settings gear top right, the route with the current level node, a large green "Level 1" button, and the tab bar Leaderboard, Shop (lock), raised Home, Collection (lock), Support; the capture is stored under docs/verification/hub-home/.
4. Given 1080 x 2400 with a simulated notch safe area (top 110 px, bottom 70 px), when Home shows, then every interactive element lies inside the safe area, the background covers the full screen without letterbox or stretching, and nothing is clipped or overlaps (AC-1.4, AC-1.4a, DEC-42).
5. Given saved level index 0 and 5 levels, then the button reads "Level 1"; given saved index 7 and 5 levels, then it reads "Level 5" (DEC-8, DEC-44); the label is rebuilt only when the saved index changes (AC-1.2).
6. Given the currency catalog, when Home binds, then one holder per `Show On Home` definition appears in catalog order; the coin holder shows 0, 950, 1.52k (1520), 12.5k (12500) and 1.52m (1520000) for those balances; the life holder shows "Full" with 5 on the icon at its maximum; text changes only when a balance changes (AC-1.7, AC-1.8, AC-6.4).
7. Given any Home button (level, settings, profile, both plus badges, all five tabs), when pressed and released, then it scales to 0.92 and darkens while pressed and restores on release through `.cbj-pressable`; no tab navigates, Home stays active, and Shop and Collection show `lock.png` with a dimmed icon (AC-1.5, AC-1.6, DEC-14, DEC-86).
8. Given the settings or level button, when pressed, then `HomePresenter` asks `HubNavigator` for `OpenSettings` or `OpenPreLevel`; while no overlay is registered nothing opens and nothing is logged as an error.
9. Given `HubNavigator` and `HubBackInput` with fake overlays, when overlays open and Escape is performed, then only the top overlay closes; with no overlay Back does nothing; disabling Hub disposes the `InputAction`.
10. Given Hub played directly while the project scope is still loading, when it becomes ready, then Home binds once; given a failed project scope or empty database, the level button reads "Level -" and is disabled; Hub code declares no `Update`, coroutine, `Invoke` or static mutable state, and every subscription is disposed on `OnDisable`.
11. Given `SafeAreaPadding.Calculate`, when tested with a full-screen safe area, a top notch, a bottom inset and a panel scale different from 1, then the padding equals the inset in panel units.
12. Given `docs/UI-Asset-Import.md`, when read, then it classes all 47 sprites under `Art/UI` as nine-slice or fixed with size, measured border, method, confidence and import settings, marks which borders are applied, and the three applied borders equal the Sprite Editor values and the USS `-unity-slice-*` values.
13. Given the test runner, when `ColorBlockJam.Hub.EditModeTests`, `ColorBlockJam.Hub.PlayModeTests` and `SceneFlowSmokeTests` run, then all pass; PROJECT.md records the Hub assembly/composition and the reference 06 tab order decisions.

## References and constraints

- Story/GDD/ADR/design/log/image: docs/Hub-And-Player-Data-Design.md sections 2, 6, 7, 10, 11; docs/media/color-block-jam/user-references/06-home-tabs.png, 07, 08; docs/Color-Block-Jam-Visual-References.md refs 06 to 08; docs/Color-Block-Jam-PRD.md 7.1 (AC-1.2 to AC-1.8), DEC-14, DEC-18, DEC-39, DEC-40, DEC-41, DEC-42, DEC-45, DEC-86, NFR-4, V-M11; docs/UI-Asset-Mapping.md; AGENTS.md Art Assets; PROJECT.md DEC-98, DEC-99
- Unity version/platform: 2022.3.62f2; Android and iOS portrait, 1080 x 1920 reference, 20:9 target
- Package/architecture constraints: UI Toolkit only; Input System UI module; Onity DI/Reactive; ZString for dynamic text; views thin, presenters plain C#; USS transitions for UI motion; no allocation per frame and no per-frame Hub work
- Explicit out of scope: Settings overlay (task 3); pre-level, purchase and launch (task 4); Sprite Atlases and ProjectSettings orientation (follow-ups); the reference's event widget, avatar art, level numbers 29 to 31 and "Lvl 50" gate; working tab pages

## Verification and risk

- Focused test path/command: MCP `run_tests` EditMode assembly `ColorBlockJam.Hub.EditModeTests`; PlayMode assembly `ColorBlockJam.Hub.PlayModeTests` and `ColorBlockJam.Tests.PlayMode.SceneFlowSmokeTests`
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile after `refresh_unity`; no Console error or warning from Hub code when entering Play from Init
- EditMode: presenters, navigator, back input, currency text, safe area (scenarios 5 to 9 and 11)
- PlayMode: Hub scene composition and element presence (scenarios 1 and 10)
- Manual/built-player/profiler/device evidence: Game view captures at 1080 x 1920 and 1080 x 2400 with a simulated notch compared with reference 06 (scenarios 3, 4, 7); Profiler idle-Home check; device 20:9 check stays V-D3 and is recorded as not run
- Risk: high (scene, new PanelSettings/FontAsset assets, sprite import borders, new assembly)
- Approval already granted (exact target only): user chose the whole Hub scope and the reference 06 tab order on 2026-09-27; explicit target approval for the serialized assets above is requested with this contract
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: independent verifier, then /task-cycle or automatic closure

## Stop conditions

- The user has not approved the serialized targets listed above.
- Hub.unity is open and dirty in the live Editor, or another session writes it.
- The player-data ports are missing or task 1 is not verified.
- The layout needs a sprite outside the three listed metas to be sliced, a new art import, a package or a ProjectSettings change.
- UI Toolkit in 2022.3 cannot deliver the required press feedback through USS alone.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: Opus 5.5 chief validator APPROVE; all 13 criteria PASS and D-001..D-011 VERIFIED at the reviewed revision
- Contract fingerprint: sha256:f02a8e85e2c5430e89f34802bc160cd0748e6900d25c4dae8b9fec8a768a1ee9
- Evidence fingerprint: sha256:0bed78e9ac0c97b9e58b4db0149e8069f1e4aba57599d6313f47db216d110d67
- Reviewed revision: vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc
- Attempt count: 6
- Last feedback: 2026-09-27 user: route nodes must be squares like reference 06; check Home against the references
- Blocker: none
- Next action: none (closed); Plastic checkin with the rest of the Hub work on the user's order
- Closed revision: vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc
- Closed at: 2026-09-27T15:35Z
- Closure mode: automatic
- Closed by: task-cycle, session Hub sahnesini oluşturma [5c52bd]
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated+manual: HubSceneTests composition (job bfd3ef34) and Hub.unity readback | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| AC-2 | PASS | manual: HubPanelSettings/theme/FontAsset readback in the live Editor | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| AC-3 | PASS | manual: cycle-6 verifier fresh captures and the Opus 5.5 chief validator: CHIEF VERDICT: APPROVE (runs/opus-hub-home-c6-20260927/final.md) | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| AC-4 | PASS | manual: cycle-6 verifier notch capture: changes confined to the route (x385-694), top bar inside the safe area unchanged | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| AC-5 | PASS | automated: HomePresenterTests level label cases (job 022202b1) | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| AC-6 | PASS | automated: CurrencyTextTests 19 cases and holder tests; capture shows Full/5 | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| AC-7 | PASS | automated+manual: Hub PlayMode 14 passed + 2 captures skipped by design (96732b06); level/shop pressed captures (cycle-6 verifier) | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| AC-8 | PASS | automated: Presses_AskTheNavigator and no-overlay tests | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| AC-9 | PASS | automated: HubNavigatorTests 8 and HubBackInputTests 2 (InputTestFixture Escape) | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| AC-10 | PASS | automated: HubStartupFailureTests faulted/cancelled/empty container (job 2a8484a3) | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| AC-11 | PASS | automated: SafeAreaPaddingTests 7 | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| AC-12 | PASS | manual: UI-Asset-Import.md covers 47 sprites; applied borders equal importer and USS | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| AC-13 | PASS | automated: EditMode Hub+UI+Player 147/147 (6086aaaf), Hub PlayMode 14 passed + 2 skipped by design (96732b06), SceneFlowSmokeTests 6/6 (c9ff7ac6); PROJECT.md DEC-132/133 | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| [D-001](defects/D-001.md) | ac_failure:ac-4:coin-plus-badge-overlaps-life-heart | VERIFIED | AC_FAILURE | AC-4 | Coin holder plus badge covers the life holder heart by about 21 px at 1080 wide | verifier cycle 2 fresh-capture repro passes (verifier capture crop + Astra blocker 1) | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| [D-002](defects/D-002.md) | ac_failure:ac-3:home-bands-level-button-low-fixed-gap | VERIFIED | AC_FAILURE | AC-3 | Level button near y1419/1899 and a fixed 110 px gap instead of reference 06 bands | verifier cycle 2 fresh-capture repro passes (Astra major 3 band table) | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| [D-003](defects/D-003.md) | ac_failure:ac-3:tab-icons-small-home-icon-not-raised | VERIFIED | AC_FAILURE | AC-3 | Tab icons small; Home icon does not protrude above ActiveTab | verifier cycle 2 fresh-capture repro passes (Astra minor 7) | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| [D-004](defects/D-004.md) | ac_failure:ac-3:route-rails-fade-toward-top | VERIFIED | AC_FAILURE | AC-3 | Route rails fade out toward the top | Astra round 2 minor 1 | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| [D-005](defects/D-005.md) | ac_failure:ac-3:plus-glyph-undersized | VERIFIED | AC_FAILURE | AC-3 | Plus glyphs undersized inside their badges | Astra round 2 minor 2 | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| [D-006](defects/D-006.md) | ac_failure:ac-3:profile-frame-small | VERIFIED | AC_FAILURE | AC-3 | Profile frame looks small in its slot | Astra round 2 minor 3 | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| [D-007](defects/D-007.md) | task_regression:ac-4:route-rails-drawn-over-top-bar-heart | VERIFIED | TASK_REGRESSION | AC-4 | Rails extended to the screen top draw over the life holder heart and its count | coordinator check of the cycle-3 capture | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| [D-008](defects/D-008.md) | task_regression:ac-4:top-bar-ignores-top-safe-area-inset | VERIFIED | TASK_REGRESSION | AC-4 | After the cycle-4 reorder the absolute top bar sits inside the 110 px top inset at 1080x2400 | cycle-4 verifier notch capture | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| [D-009](defects/D-009.md) | ac_failure:ac-3:current-route-node-renders-circular | VERIFIED | AC_FAILURE | AC-3 | Current-level route node renders as a circle | cycle-6 independent verifier (wf_bc47f785-414) fresh captures | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| [D-010](defects/D-010.md) | ac_failure:ac-3:route-numerals-and-level-label-thin | VERIFIED | AC_FAILURE | AC-3 | Route numerals and Level label are thin versus reference 06 | cycle-6 independent verifier (wf_bc47f785-414) fresh captures | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |
| [D-011](defects/D-011.md) | task_regression:ac-3:locked-route-nodes-render-corner-fragments | VERIFIED | TASK_REGRESSION | AC-3 | Locked route nodes render as four corner fragments | cycle-6 independent verifier (wf_bc47f785-414) fresh captures | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:85c08e37a2b45c6b02daae995546e06d908718b7ae7ffb60b52aaa918587f8bc |

### Transition history

- 2026-09-27 — Created — planning — contract authored from the user's Hub request and reference 06
- 2026-09-27 — Approval recorded — planning — the user approved every serialized target listed in this contract ("Hepsini onayla"), and the portrait lock plus UI atlases ("İkisini de şimdi ekle")
- 2026-09-27 — Started — implementation — predecessor handoff: task 1 ports verified (Player EditMode 58/58, acceptance 13/13; its D-001 is an editor data-setup regression that does not affect Hub); unity-cli READY WITH WARNINGS; unity-game-dev loaded; lock acquired
- 2026-09-27 — task-cycle — defect-cycle — implementer wf_917d3b0a-799 finished; verifier 11/13; NEW_DEFECT D-001 (AC-4), D-002 and D-003 (AC-3, from the user-named chief validator Astra); lock held
- 2026-09-27 — task-cycle — defect-cycle — cycle 2 verified D-001..D-003 and AC-10; Astra round 2 found three minor resemblance gaps (NEW_DEFECT D-004, D-005, D-006)
- 2026-09-27 — task-cycle — defect-cycle — cycle 3 verified D-004..D-006; coordinator found TASK_REGRESSION D-007 in the cycle-3 capture (NEW_DEFECT)
- 2026-09-27 — task-cycle — defect-cycle — cycle 4 fixed D-007 paint order; its verifier found TASK_REGRESSION D-008 (top bar ignores the notch inset), caused by the coordinator's cycle-4 instruction
- 2026-09-27 — task-cycle — verification — cycle 5 (user-approved beyond the 3-attempt auto limit) verified D-007 and D-008; tests Hub+Player EditMode 108/108, Hub PlayMode 6/6 + 1 gated skip, SceneFlowSmokeTests 6/6
- 2026-09-27T10:57Z — Closed — automatic — chief validator Astra round 3 APPROVE; every criterion PASS and every defect VERIFIED at the closed revision
- 2026-09-27 — task-cycle — reopened — user feedback with reference 06: NEW_DEFECT D-009 (current node circular) and D-010 (thin route/Level text); AC-3 FAIL
- 2026-09-27 — task-cycle — reopened — user screenshot: NEW_DEFECT D-011 (TASK_REGRESSION, locked nodes as corner fragments); cycle-6 workflow stopped before final captures
- 2026-09-27 — task-cycle — reopened — user order "astra onayı yerine artık opus 5.5 kullan": the chief visual validator is now an Opus 5.5 read-only reviewer agent instead of Codex gpt-6-astra; the cycle-6 chief review uses it
- 2026-09-27 — task-cycle — verification — cycle 6 fixed D-009..D-011 (Home.uss/uxml, HomeView, Common.uss, SettingsPopup.uss move); independent verifier VERIFIED all three with no regression
- 2026-09-27 — task-cycle — closed — Opus 5.5 chief validator APPROVE; TASK_CLOSED automatically at the reviewed revision
<!-- TASK-LIFECYCLE:END -->
