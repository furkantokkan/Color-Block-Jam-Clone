# TASK-20260927-001142693-43e35c51-ui-atlases-portrait-lock — UI Sprite Atlases and portrait orientation lock

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260927-001142693-43e35c51-ui-atlases-portrait-lock
- Supersedes: none
- Title: UI Sprite Atlases and portrait orientation lock
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:10 (head) plus uncommitted workspace changes from other live sessions
- Task kind: performance
- Area: UI texture packing and Player orientation (ProjectSettings, `Art/UI/Atlases`)
- Workflow: direct

## Objective and baseline

- Observable objective: The app runs portrait-only on Android and iOS, and the UI sprites are packed into two Sprite Atlases (Home and Settings; HUD and popups) so the Hub screens draw from shared atlas textures, with measured UI draw counts recorded.
- Current behavior/evidence (2026-09-27): `ProjectSettings.asset` `defaultScreenOrientation: 4` (auto-rotation) with all four `allowedAutorotateTo*` flags on, so a phone can turn the 1080 x 1920 layout sideways; `EditorSettings.asset` `m_SpritePackerMode: 0` (disabled); no `.spriteatlas`/`.spriteatlasv2` asset exists; PROJECT.md Performance Rule 2 and PRD NFR-P6 require two UI atlases and Home at 6 UI draws or fewer.
- Expected behavior: default orientation Portrait through `PlayerSettings.defaultInterfaceOrientation` (no auto-rotation); sprite packer mode Sprite Atlas V2 enabled in the Editor and builds; `Art/UI/Atlases/HomeSettings.spriteatlasv2` and `HudPopups.spriteatlasv2` with Allow Rotation off, Tight Packing off, Padding 4, Android and iOS ASTC 6x6 overrides; every sprite under `Art/UI` in exactly one atlas except the full-screen backgrounds (`bg_home_screen`, `bg_menu`, `bg_block_texture`), which stay single textures as a recorded exception (one full-screen draw each; packing them would force a 4096 atlas); membership, sizes and memory recorded in `docs/UI-Asset-Import.md`; Home, Settings and popup UI draw counts measured before and after.
- Behavior that must remain unchanged: every other Player and Editor setting (IL2CPP, ARM64, rendering values of docs/Android-Player-Settings.md, quality levels); sprite import settings and GUIDs; Hub visuals (captures before and after match); Game scene rendering.

## Ownership

- Owner/writer role: Hub-and-player-data session, one implementer at a time, then an independent verifier
- Allowed paths (exact or narrow globs):
  - ProjectSettings/ProjectSettings.asset (default orientation only, through `PlayerSettings`)
  - ProjectSettings/EditorSettings.asset (sprite packer mode only, through `EditorSettings`)
  - Assets/ColorBlockJam/Art/UI/Atlases/**
  - Assets/ColorBlockJam/Art/UI/**/*.png.meta of the 44 sprites packed in HomeSettings or HudPopups (compression fields only: default Compression None and the Android/iOS overrides removed; added 2026-09-28 on the user's answer "Sprite'lar sıkıştırılmasın"; the membership list is in docs/UI-Asset-Import.md)
  - AGENTS.md (the UI sprite import-settings bullet only)
  - Assets/ColorBlockJam/Art/UI/Atlases.meta
  - Assets/ColorBlockJam/Tests/Hub/**
  - docs/UI-Asset-Import.md
  - docs/Android-Player-Settings.md
  - docs/verification/ui-atlases/**
  - PROJECT.md
  - production/tasks/TASK-20260927-001142693-43e35c51-ui-atlases-portrait-lock/**
- Forbidden paths/actions:
  - Packages/**
  - Any other ProjectSettings file or field (quality, graphics, scripting backend, architectures, input handler)
  - Sprite `.png` files, and every importer setting other than the compression fields above (no Max Size, border, sprite mode, filter or mipmap change); the three full-screen background metas
  - Assets/ColorBlockJam/Art/UI/Panels/bg_popup_info_holder.png.meta, Assets/ColorBlockJam/Art/UI/Panels/bg_gameplay_time_holder.png.meta and Assets/ColorBlockJam/Art/UI/Buttons/Btn_booster_normal.png.meta while TASK-20260927-004448215-54655902-booster-gameplay-flow owns them (change them only after its release or in a window it grants)
  - Assets/ColorBlockJam/Hub/** and UXML/USS (report visual regressions to the owning task)
  - Assets/ColorBlockJam/Scenes/**
  - Hand-editing ProjectSettings YAML while the live Editor is reachable
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: ProjectSettings/ProjectSettings.asset
    Sole writer: this task's implementer through `PlayerSettings.defaultInterfaceOrientation` and `AssetDatabase.SaveAssets`
    Matching `.meta` owner: not applicable
  - Asset: ProjectSettings/EditorSettings.asset
    Sole writer: this task's implementer through `EditorSettings.spritePackerMode`
    Matching `.meta` owner: not applicable
  - Asset: Assets/ColorBlockJam/Art/UI/Atlases/HomeSettings.spriteatlasv2, Assets/ColorBlockJam/Art/UI/Atlases/HudPopups.spriteatlasv2
    Sole writer: this task's implementer (new, through the Sprite Atlas V2 editor API)
    Matching `.meta` owner: this task (new)
- Other active writers/worktrees checked: 2026-09-27 03:11 local. TASK-20260925-014954719-a9370ca5-board-look-camera-light-graphics (in_progress, verification, waiting on a user decision) lists `ProjectSettings.asset` for its finished quality work; it is not writing. Recheck ListAgents and the file mtime before the write; save only the orientation field's owner asset.
- Execution dependency: TASK-20260926-235915699-78356740-hub-pre-level-launch (stable handoff: all Hub screens exist and their suites are green, so draw counts cover Home, Settings and popups)
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given the Player settings, when read back, then the default orientation is Portrait for Android and iOS, auto-rotation is off, and no other Player setting differs from its pre-task value.
2. Given the Editor settings, when read back, then the sprite packer mode is Sprite Atlas V2 enabled, and no other Editor setting changed.
3. Given the two atlases, when inspected, then each sprite under `Art/UI` except the three full-screen backgrounds belongs to exactly one atlas as listed in `docs/UI-Asset-Import.md`; both atlases use Allow Rotation off, Tight Packing off, Padding 4 and ASTC 6x6 overrides for Android and iOS; their packed sizes and estimated device memory are recorded.
4. Given Play Mode from Init, when Home, Settings, the pre-level popup and the purchase popup are shown, then their UI draw or batch counts are measured with the Frame Debugger (or the UI Toolkit Debugger when the Frame Debugger cannot isolate the panel) before and after packing and recorded under docs/verification/ui-atlases/; Home reaches 6 UI draws or fewer, or the gap and its cause are recorded (NFR-P6).
5. Given captures of Home, Settings and both popups at 1080 x 1920 before and after packing, when compared, then they match within compression tolerance.
6. Given the test runner, when the Hub suites and SceneFlowSmokeTests run, then all pass.
7. Given PROJECT.md and docs/Android-Player-Settings.md, when read, then a decision row records the two-atlas split, the background exception and the portrait lock, and the settings doc lists the orientation change.

## References and constraints

- Story/GDD/ADR/design/log/image: PROJECT.md Performance Rule 2; docs/Color-Block-Jam-PRD.md NFR-P6, NFR-4, AC-1.4; AGENTS.md Art Assets (atlas packing settings); docs/Hub-And-Player-Data-Design.md section 12; user decision 2026-09-27 ("İkisini de şimdi ekle")
- Unity version/platform: 2022.3.62f2; Android and iOS portrait
- Package/architecture constraints: built-in 2D Sprite (Sprite Atlas V2) only; no package change
- Explicit out of scope: gameplay HUD layout (no HUD screen exists yet; its sprites are still packed), device memory profiling (recorded as not run), sprite import compression changes

## Verification and risk

- Focused test path/command: MCP `run_tests` Hub EditMode/PlayMode assemblies and `ColorBlockJam.Tests.PlayMode.SceneFlowSmokeTests`
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-optimization`
- Unity compilation/Console evidence: no Console error or packing warning after enabling the packer and packing both atlases
- EditMode: not applicable beyond the existing suites
- PlayMode: existing Hub and scene-flow suites stay green
- Manual/built-player/profiler/device evidence: settings readback, atlas previews, draw counts before/after, capture comparison; device portrait check and ASTC memory on device recorded as not run
- Risk: high (two ProjectSettings files and new atlas assets)
- Approval already granted (exact target only): the user approved adding the portrait lock and the UI Sprite Atlases now, including the EditorSettings sprite packer change, on 2026-09-27
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: independent verifier, then /task-cycle or automatic closure

## Stop conditions

- Another session is writing either ProjectSettings file.
- Packing needs a change to a sprite's import settings or a package.
- Enabling the packer changes Hub visuals beyond compression tolerance.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: READY_TO_CLOSE
- Contract fingerprint: sha256:a2e31ae804e73627dbb39ee35521c2737d72964fefffc35350a2bc671321cea6
- Evidence fingerprint: sha256:eb57acaddb010544e37c02a4ca7931a30a30d54f42e921e72566d25d1ae18db2
- Reviewed revision: vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:1c6ed0426ba4e05a73784cbc0457ee67f618075f08ff88400f1e3d0aac641b2e
- Attempt count: 1
- Last feedback: 2026-09-28 user: packed sprites uncompressed so only the atlases compress ("Sprite'lar sıkıştırılmasın")
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:1c6ed0426ba4e05a73784cbc0457ee67f618075f08ff88400f1e3d0aac641b2e
- Closed at: 2026-09-28T00:15Z
- Closure mode: automatic
- Closed by: implement-task, session Hub sahnesini oluşturma [5c52bd]
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | manual readback: PlayerSettings.defaultInterfaceOrientation Portrait; ProjectSettings.asset diff only defaultScreenOrientation 4->0 | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:1c6ed0426ba4e05a73784cbc0457ee67f618075f08ff88400f1e3d0aac641b2e |
| AC-2 | PASS | manual readback: EditorSettings.spritePackerMode 5 (Sprite Atlas V2 enabled); EditorSettings.asset diff only m_SpritePackerMode 0->5 | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:1c6ed0426ba4e05a73784cbc0457ee67f618075f08ff88400f1e3d0aac641b2e |
| AC-3 | PASS | manual readback: 44 sprites in exactly one atlas (HomeSettings 25, HudPopups 19), backgrounds excluded, Allow Rotation off, Tight Packing off, Padding 4, Android/iOS ASTC 6x6; sizes and memory in docs/UI-Asset-Import.md | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:1c6ed0426ba4e05a73784cbc0457ee67f618075f08ff88400f1e3d0aac641b2e |
| AC-4 | PASS | manual: Frame Debugger draw calls packer off -> on: Home 3 -> 1, Settings 5 -> 1, pre-level 5 -> 2, purchase 6 -> 3 (NFR-P6 met); docs/verification/ui-atlases/ | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:1c6ed0426ba4e05a73784cbc0457ee67f618075f08ff88400f1e3d0aac641b2e |
| AC-5 | PASS | manual: packer off/on captures match within ASTC tolerance (mean delta 0.17-0.44, edges only); Opus 5.5 chief review CHIEF VERDICT: APPROVE | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:1c6ed0426ba4e05a73784cbc0457ee67f618075f08ff88400f1e3d0aac641b2e |
| AC-6 | PASS | automated: with the packer on, Hub PlayMode 17 passed + 3 capture tests skipped by design, SceneFlowSmokeTests 6/6, Hub EditMode 81/81; final-pass suites 523/523 EditMode and 73 PlayMode passed | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:1c6ed0426ba4e05a73784cbc0457ee67f618075f08ff88400f1e3d0aac641b2e |
| AC-7 | PASS | manual: PROJECT.md DEC-142 and docs/Android-Player-Settings.md portrait section read back | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:1c6ed0426ba4e05a73784cbc0457ee67f618075f08ff88400f1e3d0aac641b2e |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- 2026-09-27 — Created — planning — contract authored after the user approved adding the portrait lock and UI atlases now
- 2026-09-27 — Approval recorded — planning — the user approved every serialized target listed in this contract ("Hepsini onayla"), and the portrait lock plus UI atlases ("İkisini de şimdi ekle")
- 2026-09-27 — Started — implementation — user order "testleri en sona saklayıp development süresinden kazan": test runs, captures, measurements and the chief review move to one final verification pass; development continues with out-of-Editor compile checks only; the before/after draw counts (scenario 4) and captures (scenario 5) are measured in the final pass by toggling the sprite packer mode; unity-cli READY WITH WARNINGS (shared live Editor); unity-game-dev loaded; lock ae0c650c…
- 2026-09-28 — Contract change — implementation — development result: portrait lock and Sprite Atlas V2 packer set (ProjectSettings defaultScreenOrientation 4->0, EditorSettings m_SpritePackerMode 0->5, nothing else); HomeSettings (25) and HudPopups (19) created and packed (4.46 MiB estimated ASTC 6x6); packing warned that all 44 sources are compressed. The user chose uncompressed sources ("Sprite'lar sıkıştırılmasın"): the 44 packed sprites' compression fields and AGENTS.md's import bullet enter the allowed paths; three metas stay with the booster owner until it releases them. The decision row is DEC-142 (DEC-139, DEC-140 and DEC-141 are taken); contract fingerprint recomputed
- 2026-09-28 — Development — implementation — 42 packed sprites set to Compression None (one textureCompression line each; bg_popup_info_holder is not booster-owned, confirmed by that owner); repack warns only for bg_gameplay_time_holder and Btn_booster_normal, which the booster owner brings uncompressed in its own versions (agreed); DEC-142 in PROJECT.md; docs/UI-Asset-Import.md, docs/Android-Player-Settings.md and AGENTS.md updated (backgrounds keep their own importer compression; target.png deliberately unpacked); no test run (user order to defer tests)
- 2026-09-28 — Verified and closed — closed — final verification pass (wf_3ea5c9c9-c98): all 7 criteria PASS; device portrait check and device ASTC memory recorded as not run; bg_gameplay_time_holder and Btn_booster_normal arrive uncompressed with the booster task (agreed); optional polish from the chief: atlas padding 8 to avoid a 6x6 ASTC fleck at bg_popup's corner (needs a user decision on AGENTS.md's Padding 4); TASK_CLOSED automatically
<!-- TASK-LIFECYCLE:END -->
