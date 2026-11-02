# TASK-20260928-014637019-659eace5-modular-home-route-and-tabs — Modular Home: difficulty-driven route nodes and current-level pulse

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260928-014637019-659eace5-modular-home-route-and-tabs
- Supersedes: none
- Title: Modular Home: difficulty-driven route nodes and current-level pulse
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026@efurk64@unity (Plastic workspace, shared live Editor)
- Base revision: cs:14 plus the pending Hub checkin
- Task kind: feature
- Area: Hub Home route, level data (difficulty), Level Editor
- Workflow: direct

## Objective and baseline

- Observable objective:
  - On Home, the current level node pulses with a looping scale tween.
  - A designer marks a level as Hard in the Level Editor. That level's route node then takes the Hard look from a Hub catalog, whether it is current or locked.
  - The tab and menu catalog moved to TASK-20260928-103425388-64d04aa0-hub-tab-menu-catalog (user decision 2026-09-28, see the transition history).
- Current behavior/evidence:
  - `Home.uxml` authors three route nodes: current `.route-node--current` on `Btn_level_green` (DEC-133), and locked nodes on `Btn_settings_normal` with a lock. Nothing animates.
  - Level JSON `formatVersion` 2 has no difficulty. `LevelDatabaseJson` reads with `MissingMemberHandling.Error`, so an older build rejects an unknown member.
  - The user's requests (2026-09-28):
    - "hub da şuan olduğumuz levele ... scale tween ile loop yapılabilir yeşil renkte olana"
    - "istediğimiz özel level de istediğimiz görünümü kolayca koyabileceğimiz şekilde modüler olmalı"
    - "ui butonları da ... kitleyip level sınırı koyabileceğimiz kapatabileceğimiz ve yeni buton ve menü ekleyebileceğimiz şekilde modüler olmalı"
    - "Level JSON'da, Level Editor'den sadece zor level diye atanması yeterli görselleri catalogdan sağlarız"
- Expected behavior:
  - **Pulse.** `HomeView` owns a PrimeTween looping scale yoyo on the current node's element, through `Tween.Scale(element.transform, …)`: about 1.0 to 1.06 over about 0.8 s with ease in-out, and the parameters come from the node style. It starts when Home binds, stops (`Tween.StopAll` on that target) when Home unbinds, `HubScreen` disables or the scene unloads, and restarts on rebind. Nothing else animates and nothing allocates per frame beyond PrimeTween.
  - **Difficulty in level data.**
    - Add `LevelDifficulty { Normal = 0, Hard = 1 }` as extensible, stored by value and never renumbered, with a JSON string value (`"difficulty": "hard"`) through an explicit converter. `TypeNameHandling` stays None.
    - Level JSON `formatVersion` becomes 3. The reader accepts 1 (legacy), 2 and 3. A missing difficulty means Normal, and the writer writes 3.
    - Level validation keeps its stable messages; an unknown difficulty value is a designer-language error.
    - Runtime play copies the value into the attempt model without mutating authored data. Gameplay ignores it for now.
  - **Level Editor.** A Difficulty dropdown (Normal, Hard) for the selected level, stored on the undoable draft, saved by Save Level, restored by Reload and undo, and shown with human names (memory: editor UI clean names).
  - **Node style catalog.**
    - `LevelNodeStyle` (ScriptableObject): face sprite, face tint, numeral front and back colours, optional badge sprite, and idle animation (None or Pulse, with amplitude and period).
    - `LevelNodeStyleCatalog` (ScriptableObject, `Data/Hub/LevelNodeStyleCatalog.asset`): a Current style, a Locked style, and one face style per difficulty other than Normal. Hard uses supplied sprites only (for example `Btn_settings_normal` or `Btn_level_green` with a red tint); its badge slot is empty until art exists.
    - A plain C# resolver decides each node:
      - the face and numeral colours come from the difficulty style when the level's difficulty has one, else from the state style;
      - the lock overlay comes from the state (Locked);
      - the pulse comes from the Current state.
    - The Inspector shows validation: missing sprite, duplicate difficulty, amplitude out of range.
  - **Tab and menu catalog:** moved to TASK-20260928-103425388-64d04aa0-hub-tab-menu-catalog. Build on its tab bar (Home.uxml `tab-row`, `Runtime/Hub/Tabs/**`) without changing it.
- Coordination (haptics session "Haptic feedback oyun entegrasyonu", DEC-156, 2026-09-28): the HubScreen/Home.uxml haptics hook moved to TASK-20260928-103425388-64d04aa0-hub-tab-menu-catalog with the tabs.
- Behavior that must remain unchanged: Home look apart from the pulse, and the approved captures within tolerance. Also unchanged: the currency holders (DEC-143), the Level button and pre-level flow, Settings, purchase, the launch request (DEC-138), existing level files (read as Normal), the Test Level flow (DEC-127), the level JSON writer's atomic save and backups, and every gameplay rule.

## Ownership

- Owner/writer role: Hub-and-player-data session, one implementer at a time, then an independent verifier
- Allowed paths (rebased onto docs/Folder-Layout.md; restructure landed in the workspace):
  - Assets/ColorBlockJam/UI/Hub/Home.uxml
  - Assets/ColorBlockJam/UI/Hub/Home.uss
  - Assets/ColorBlockJam/Runtime/Hub/Home/**
  - Assets/ColorBlockJam/Runtime/Hub/Composition/HubSceneInstaller.cs (style catalog binding only)
  - Assets/ColorBlockJam/Runtime/Hub/Composition/HubScreen.cs (style catalog wiring only, if HomeView needs it)
  - Assets/ColorBlockJam/Runtime/Hub/Styles/** (new: LevelNodeStyle, LevelNodeStyleCatalog, resolver)
  - Assets/ColorBlockJam/Data/Hub/LevelNodeStyleCatalog.asset and its style assets (new; the `Data/Hub` folder comes from TASK-20260928-103425388-64d04aa0)
  - Assets/ColorBlockJam/Runtime/Core/Levels/LevelDifficulty.cs (new)
  - Assets/ColorBlockJam/Runtime/Core/Levels/LevelData.cs
  - Assets/ColorBlockJam/Runtime/Core/Levels/LevelDatabaseJson.cs
  - Assets/ColorBlockJam/Runtime/Core/Levels/LevelValidation*.cs (difficulty value only)
  - Assets/ColorBlockJam/Editor/Core/Levels/** (the Difficulty field in the Level Editor window and its draft)
  - Assets/ColorBlockJam/Scenes/Hub.unity (HubSceneInstaller catalog references only)
  - Assets/ColorBlockJam/Tests/Hub/**
  - Assets/ColorBlockJam/Tests/Editor/EditMode/** (level JSON and Level Editor difficulty tests only)
  - docs/Hub-And-Player-Data-Design.md
  - docs/Level-Data-Format.md
  - docs/Level-Editor-Design.md
  - docs/verification/hub-home/**
  - PROJECT.md
  - production/tasks/TASK-20260928-014637019-659eace5-modular-home-route-and-tabs/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Art/UI/** (no new art; supplied sprites only)
  - Assets/ColorBlockJam/Data/Levels/*.json (no authored level changes; tests use their own fixtures)
  - Assets/ColorBlockJam/Runtime/Gameplay/**
  - Assets/ColorBlockJam/UI/Game/**
  - Assets/ColorBlockJam/Runtime/Domain/**
  - Assets/ColorBlockJam/Runtime/Player/**
  - Assets/ColorBlockJam/UI/Purchase/** (Settings and purchase popups)
  - Assets/ColorBlockJam/UI/Settings/** (Settings and purchase popups)
  - Assets/ColorBlockJam/Runtime/UI/Purchase/** (Settings and purchase popups)
  - Assets/ColorBlockJam/Runtime/UI/Settings/** (Settings and purchase popups)
  - Gameplay behaviour driven by difficulty (future work)
  - `TypeNameHandling` other than None
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Data/Hub/LevelNodeStyleCatalog.asset and its style assets (new)
    Sole writer: this task's implementer through ScriptableObject APIs
    Matching `.meta` owner: this task (new)
  - Asset: Assets/ColorBlockJam/Scenes/Hub.unity
    Sole writer: this task's implementer through SerializedObject on HubSceneInstaller (style catalog reference only; the navigation catalog reference belongs to TASK-20260928-103425388-64d04aa0)
    Matching `.meta` owner: unchanged
- Other active writers/worktrees checked: 2026-09-28 01:46 UTC.
  - This session holds the pre-level, tab bar and currency holder locks (landing verification running) and is preparing the Hub checkin.
  - "Klasör düzenleme ve script organizasyonu" waits for that checkin, then moves folders.
  - "Haptic feedback oyun entegrasyonu" asked to add haptic USS classes and a ClickEvent hook in HubScreen and Home.uxml. It was told to wait, or to send its hook design for inclusion.
  - booster-gameplay-flow owns Gameplay, Player and Runtime/Boosters paths.
  - No live owner of Runtime/Levels or Editor/Levels.
- Execution dependency: the restructure session's checkin of its moves (no write into moved files until it lands, so its changeset does not carry this task's edits); and TASK-20260928-103425388-64d04aa0-hub-tab-menu-catalog, which shares Home.uxml, Home.uss, HubScreen.cs, HubSceneInstaller.cs and Hub.unity (start after its stable handoff)
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given Home with the current level shown, when bound, then the current node pulses (looping scale yoyo within the style's amplitude); when Home unbinds, HubScreen is disabled or Game loads, then `Tween.GetTweensCount` for that target is 0; after a rebind it pulses again.
2. Given a level JSON with `"difficulty": "hard"` and formatVersion 3, when loaded, then that level's difficulty is Hard. A formatVersion 2 or legacy file loads with every level Normal. An unknown difficulty value fails validation with a designer-language message. A save round-trips difficulty and writes formatVersion 3.
3. Given the Level Editor with a level selected, when the Difficulty dropdown is set to Hard and Save Level runs, then the file stores it; Undo and Reload restore the previous value; the dropdown shows human names.
4. Given the style catalog, when a Hard level is current, then its node shows the Hard face and still pulses; when a Hard level is locked, it shows the Hard face with the lock overlay; Normal levels look exactly as today.
5. Given the style catalog in the Inspector, when an entry is invalid (missing sprite, duplicate difficulty, amplitude out of range), then a validation message names it.
6. Given the Hub, UI, Player, Gameplay and level EditMode suites, the Hub PlayMode suite and SceneFlowSmokeTests, when run, then all pass; Home captures at 1080 x 1920 and 1080 x 2400-notch match the approved Home apart from the pulse phase.
7. Given PROJECT.md, docs/Level-Data-Format.md, docs/Level-Editor-Design.md and design section 7, when read, then they describe difficulty (formatVersion 3), the style catalog and the pulse ownership, with a new decision row (next free DEC number).
8. Given the captures, including one fixture Home with a Hard current level and one with a Hard locked level, when the chief validator (Opus 5.5 read-only reviewer) reviews them, then the verdict is APPROVE or its findings are fixed.

## References and constraints

- Story/GDD/ADR/design/log/image:
  - user screenshot C:/Users/e-fur/AppData/Local/Temp/claude/C--Users-e-fur-Documents-Repos-ColorBlockJamCase2026/9eedf9d4-2bb5-45f8-8e12-60d163432d44/images/12.png;
  - reference docs/media/color-block-jam/user-references/06-home-tabs.png (hard level 30 is red with a badge);
  - PROJECT.md DEC-86, DEC-127, DEC-133, DEC-138, DEC-141, DEC-143;
  - docs/Level-Data-Format.md;
  - docs/Level-Editor-Design.md;
  - docs/Hub-And-Player-Data-Design.md section 7.
- Unity version/platform: Unity 2022.3.62f2; Android and iOS portrait
- Package/architecture constraints:
  - PrimeTween 1.4.11 `Tween.Scale(ITransform, …)` for the pulse; the tween belongs to HomeView (PROJECT.md rule 5).
  - Onity DI, with catalogs bound by HubSceneInstaller.
  - Presenters stay plain C#; UI Toolkit; Newtonsoft with explicit converters.
  - No new package.
- Explicit out of scope: gameplay rules for Hard levels (timer, rewards), new art (skull or bomb badges), a Leaderboard, Support, Shop or Collection screen, unlocking Shop or Collection, remote config of tabs.

## Verification and risk

- Focused test path/command: MCP `run_tests` EditMode `ColorBlockJam.Hub.EditModeTests` and the level JSON/Level Editor EditMode tests; PlayMode `ColorBlockJam.Hub.PlayModeTests` and group `SceneFlowSmokeTests`
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: out-of-Editor compile of every changed assembly first; no UXML/USS warnings
- EditMode: style resolution, presenters, JSON round-trip and version handling, Level Editor difficulty field and undo
- PlayMode: pulse lifecycle (tween count)
- Manual/built-player/profiler/device evidence: Home captures (normal, Hard current, Hard locked fixtures); Opus 5.5 chief review
- Risk: high (level JSON schema version, Level Editor, new serialized catalogs, Hub.unity references)
- Approval already granted (exact target only): the user approved this plan on 2026-09-28 ("Onaylıyorum"), including the new catalog assets. The user also chose that difficulty lives in the level JSON and Level Editor with visuals from the catalog ("Level JSON'da, Level Editor'den sadece zor level diye atanması yeterli görselleri catalogdan sağlarız").
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: independent verifier, Opus 5.5 chief review, then automatic closure

## Stop conditions

- The Hub checkin or the folder restructure has not landed, or the moved tree does not compile.
- Another live session owns Runtime/Levels, Editor/Levels, Hub.unity or Home.uxml at dispatch time.
- The change would need gameplay rules for difficulty, new art or a package.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:55b43ebe9c4bd5f707463b30582686b304f35d8c868f0714871803fc1c5a0b44
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 0
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
| AC-1 | UNPROVEN | none | not run |
| AC-2 | UNPROVEN | none | not run |
| AC-3 | UNPROVEN | none | not run |
| AC-4 | UNPROVEN | none | not run |
| AC-5 | UNPROVEN | none | not run |
| AC-6 | UNPROVEN | none | not run |
| AC-7 | UNPROVEN | none | not run |
| AC-8 | UNPROVEN | none | not run |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- 2026-09-28 — Created — planning — contract authored from the user's modular Home requests and plan approval; draft until the Hub checkin and the folder restructure land (paths move)
- 2026-09-28 — Clarified — planning — user: "Enum yapısında koyabiliriz default Normal olur, Hard olur ikinci seçenek"; matches the contract's LevelDifficulty { Normal = 0, Hard = 1 } with Normal as the default (a missing value reads as Normal)
- 2026-09-28 — Revised in draft — planning — the haptics session handed over its hook design (IHaptics injected into HubScreen, ButtonHaptics attach/detach, cbj-haptic-confirm/tap classes on the Home Level button, Settings button and tab slots) to avoid a second writer on HubScreen.cs and Home.uxml; contract fingerprint recomputed
- 2026-09-28 — Contract change (path rebase) — planning — paths rebased onto the folder restructure (docs/Folder-Layout.md); no scope, criteria, risk or approval change; contract fingerprint recomputed; set ready
- 2026-09-28 — Contract change (split) — planning — user decision in session "Modüler navigasyon butonları [24dfc8]" ("Sadece sekmeler (Önerilen)"): the tab and menu catalog (former AC-5, AC-6 and the tab half of AC-7 and AC-9) and the haptics hook moved to TASK-20260928-103425388-64d04aa0-hub-tab-menu-catalog; acceptance renumbered AC-1..AC-8 (former AC-7..AC-10 are now AC-5..AC-8); that task is an added execution dependency for the shared Hub files; contract fingerprint recomputed; stays ready. Observation: `Runtime/Hub/Home/HomeView.cs` already carries an ad-hoc current-node pulse (PrimeTween yoyo 1.0-1.05 over 0.8 s, added by session "Ana menü işareti tween animasyonu"); AC-1 must account for it
- 2026-09-29T01:16:19Z — Closed (user confirmed) — closed — explicit user order 2026-09-29 ("açık taskları da kapat üstünde çalışılmayanları artık"); prior state ready / planning. Closure mode user_confirmed: every acceptance criterion result, blocker note and defect row is preserved as recorded, not verified by this closure.
<!-- TASK-LIFECYCLE:END -->
