# TASK-20260925-014954719-a9370ca5-board-look-camera-light-graphics — Board look matches the references: camera framing, lighting and graphics settings

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-014954719-a9370ca5-board-look-camera-light-graphics
- Supersedes: none
- Title: Board look matches the references: camera framing, lighting and graphics settings
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: feature
- Area: Board presentation (camera framing, scene lighting, URP render settings)
- Workflow: direct

## Objective and baseline

- Observable objective: The Game scene, and the LevelEditor preview, show the board framed, lit and rendered like the real Color Block Jam screenshots. The mobile render settings follow the CBJ Template's cheap choices.
- Current behavior/evidence:
  - User request 2026-09-25: "referans projeye bakıp kamera açısı ışık ve grafik ayarlarını yap C:\Users\e-fur\Documents\Repos\CBJ Template". Follow-up: "referansları hedef alabiliriz örnek projeyi de aynı zamanda" (target the real-game screenshots and the template together). The pasted screenshots show the same look as `docs/media/color-block-jam/user-references/17-nearest-block-selection.jpg` and `04-time-freeze.png`.
  - Current capture `production/tasks/TASK-20260924-224137662-d526d5ad-board-camera-framing/captures/game-play-level-01.png` (level-01, 1080x1920):
    - The board fills about 0.52 of the width; the real game fills about 0.90.
    - Grey procedural-sky background with vignette corners.
    - Heavy dark-blue shadows over the right and bottom of the floor.
    - Flat, greyish light.
  - Apply plan: `C:\Users\e-fur\AppData\Local\Temp\claude\C--Users-e-fur-Documents-Repos-ColorBlockJamCase2026\13ef871f-bf3e-4dd1-8d6b-319af8745b28\scratchpad\plan.txt` (JSON: `plan.json`). Workflow `wf_9b1b3c8b-663` built it in two steps: four read-only extractors, then one Opus decision owner. It lists 25 changes with current value, reference `file:line`, new value and reason, plus a rejected list, the color-space math and pixel targets.
  - CBJ Template:
    - Camera pitch 75, FOV 60, near 0.3, far 50, no post-processing, no Volume, no renderer features.
    - Directional light color (1, 0.9915804, 0.9198113), intensity 1, soft shadows at strength 0.39.
    - One Mobile quality level with 2x MSAA.
    - Gamma color space; `CameraController` margins side 4 and vertical 12.
  - Our project today:
    - `BoardCamera` margins 4/12.
    - Game and Hub scenes: skybox clear, post-processing on, a Global Volume with `SampleSceneProfile` (Bloom, Vignette 0.25, Neutral tonemapping), directional light Euler (50, -30, 0) at intensity 2 with shadow strength 1, Skybox ambient.
    - `URP-Balanced.asset` (the Android/iOS level): MSAA off, HDR on, shadow distance 50.
    - The Editor quality level is High Fidelity.
- Expected behavior:
  1. Framing keeps pitch 75 and FOV 60. The margins become side 1.5 and vertical 5, calibrated to the real screenshots. `BoardCameraFraming` constants and the serialized Game `BoardCamera` fields both change.
  2. Game Main Camera:
     - Solid Color clear (0.839, 0.894, 0.988, 1), which is #D6E4FC.
     - Far clip plane 50.
     - `renderPostProcessing` off.
     - The Global Volume GameObject is deleted; the profile asset stays.
  3. Game Directional Light: Euler (65, 120, 0), color (1, 0.9915804, 0.9198113), color temperature off, intensity 0.6, shadow strength 0.39, soft shadows. RenderSettings ambient: Flat, (0.68, 0.69, 0.72).
  4. Hub Main Camera: the same solid clear and `renderPostProcessing` off; its Global Volume is deleted.
  5. LevelEditor.unity: RenderSettings ambient only (Flat, 0.68/0.69/0.72); no GameObject is added. `LevelPreviewHost` creates its preview light and camera with the same values, held as named constants.
  6. `URP-Balanced.asset`: MSAA 2x, HDR off, shadow distance 30. `QualitySettings` current level is Balanced, so the Editor Game view matches Android and iOS.
  7. Docs:
     - DEC-103 row updated with the new margins, and a new DEC-104 "Board look" row.
     - `docs/Environment-Layout.md` camera values.
     - "orthographic" corrected to "perspective" in `docs/Color-Block-Jam-Visual-References.md` line 179, the PRD DEC-17 wording and `docs/Art-Asset-PRD-Mapping.md` line 47.
  8. Final tuning by screenshot may move the light intensity within [0.45, 0.8] and the ambient by ±0.05 per channel, and nothing else.
- Behavior that must remain unchanged:
  - Pitch 75 and FOV 60.
  - The framing formula and its independence from the previous camera pose.
  - No Update or tick loop on camera or light code.
  - Environment composition and level JSON.
  - The Game scene flow and direct Play of each runtime scene.
  - SSAO on the Balanced renderer.
  - Linear color space (DEC-94).
  - DontSave preview objects never saved into `LevelEditor.unity` (DEC-102).
  - Android and iOS still map to Balanced.
  - High Fidelity and Performant assets unchanged.
  - `ProjectSettings/EditorBuildSettings.asset` (Addressables config) untouched.

## Ownership

- Owner/writer role: camera/light/graphics session "Kamera açısı, ışık ve grafik ayarları" (this conversation), single writer through MCP for Unity over HTTP 127.0.0.1:8082
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Environment/BoardCameraFraming.cs
  - Assets/ColorBlockJam/Environment/BoardCamera.cs
  - Assets/ColorBlockJam/Tests/Environment/EditMode/BoardCameraFramingTests.cs
  - Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewHost.cs
  - Assets/ColorBlockJam/Data/Art/GamePalette.asset (Ground, Wall and cyan/LightBlue entries only; preserve identity and booster swatches)
  - Assets/ColorBlockJam/Art/Materials/Shared/MAT_Gameplay.mat (_Specular and _Smoothness only; preserve shader, textures, instancing and all other properties)
  - Assets/ColorBlockJam/Editor/BoardKit/SharedArtAssets.cs (only synchronize the approved _Specular/_Smoothness defaults; never invoke its broad Build)
  - Assets/ColorBlockJam/Scenes/Game.unity
  - Assets/ColorBlockJam/Scenes/Hub.unity
  - Assets/ColorBlockJam/Scenes/LevelEditor.unity
  - Assets/ColorBlockJam/Settings/URP-Balanced.asset
  - ProjectSettings/QualitySettings.asset
  - PROJECT.md
  - docs/Environment-Layout.md
  - docs/Color-Block-Jam-Visual-References.md
  - docs/Color-Block-Jam-PRD.md
  - docs/Art-Asset-PRD-Mapping.md
  - production/tasks/TASK-20260925-014954719-a9370ca5-board-look-camera-light-graphics/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/EditorBuildSettings.asset
  - ProjectSettings/ProjectSettings.asset
  - ProjectSettings/GraphicsSettings.asset
  - Assets/ColorBlockJam/Settings/URP-Balanced-Renderer.asset
  - Assets/ColorBlockJam/Settings/URP-HighFidelity.asset
  - Assets/ColorBlockJam/Settings/URP-HighFidelity-Renderer.asset
  - Assets/ColorBlockJam/Settings/URP-Performant.asset
  - Assets/ColorBlockJam/Settings/URP-Performant-Renderer.asset
  - Assets/ColorBlockJam/Settings/SampleSceneProfile.asset
  - Assets/ColorBlockJam/Art/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Environment/EnvironmentPreview.cs
  - Assets/ColorBlockJam/Environment/EnvironmentView.cs
  - Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewOwner.cs
  - Assets/ColorBlockJam/Editor/LevelPreview/LevelPreviewBlockMapper.cs
  - Assets/ColorBlockJam/Tests/LevelPreview/**
  - Assets/ColorBlockJam/Scenes/Init.unity
  - Assets/ColorBlockJam/Scenes/Loading.unity
  - Switching the color space to Gamma or changing lightsUseLinearIntensity
  - Removing or disabling SSAO
  - Retuning legacy MAT_Board, editing BoardLit.shader or any other shader
  - All source FBX, model/prefab/atlas changes; palette booster-swatches and unrelated color entries
  - SharedArtAssets.Build or BoardKitAssets.Build (they rewrite unapproved atlas/prefab/JSON targets)
  - Writing through a second transport (Unity CLI or stdio MCP) for the same mutation
  - Saving a scene that has unrelated unsaved changes
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Same-task D-001 repair only: Assets/ColorBlockJam/Data/Art/GamePalette.asset and Assets/ColorBlockJam/Art/Materials/Shared/MAT_Gameplay.mat have one sequential production writer after Editor release. Existing .meta/GUIDs are preserved. The latest repair grants no new scene, camera/light or settings writes; original historical scope remains for evidence only. All broad forbidden Art/Data restrictions have only these exact exceptions.
  - Asset: Assets/ColorBlockJam/Scenes/Game.unity
    Sole writer: this session through the live Editor (MCP for Unity HTTP), only after the camera task's RELEASE and only when the scene has no unrelated unsaved changes
    Matching `.meta` owner: unchanged (no `.meta` edit)
  - Asset: Assets/ColorBlockJam/Scenes/Hub.unity
    Sole writer: this session through the live Editor (MCP for Unity HTTP)
    Matching `.meta` owner: unchanged (no `.meta` edit)
  - Asset: Assets/ColorBlockJam/Scenes/LevelEditor.unity
    Sole writer: this session through the live Editor (MCP for Unity HTTP); RenderSettings block only
    Matching `.meta` owner: unchanged (no `.meta` edit)
  - Asset: Assets/ColorBlockJam/Settings/URP-Balanced.asset
    Sole writer: this session through the live Editor (MCP for Unity HTTP)
    Matching `.meta` owner: unchanged (no `.meta` edit)
  - Asset: ProjectSettings/QualitySettings.asset
    Sole writer: this session through the live Editor (MCP for Unity HTTP); `m_CurrentQuality` only
    Matching `.meta` owner: not applicable
- Other active writers/worktrees checked:
  - `TASK-20260924-224137662-d526d5ad-board-camera-framing` (environment/play-area session "Ruled tile level sistem") is in its independent verification step.
    - It holds `BoardCamera*.cs`, `BoardCameraFramingTests.cs`, `Game.unity`, `LevelPreviewHost.cs`, the DEC-103 row and the camera section of `docs/Environment-Layout.md`.
    - Its owner replied on 2026-09-25: it will close at 4/12 and then send an explicit RELEASE of those paths to this session, with the ETA given in its message.
    - It never writes `Hub.unity` or `LevelEditor.unity`.
  - `TASK-20260925-012336316-a88e25db-block-drag-gate-exit` (gameplay session) queues its `Game.unity` step after the camera task; it runs after this task finishes with `Game.unity`.
  - Later block-chain tasks (`TASK-20260925-002022060-4c9dac46-block-runtime-objects`, `TASK-20260925-005313341-47831bb7-runtime-shapes-from-json`) will rebase on the `LevelPreviewHost` constants.
  - No open contract claims `URP-Balanced.asset` or `QualitySettings.asset`.
  - The Plastic workspace holds pending changes from other sessions; they are preserved.
- Execution dependency:
  - Paths held by the camera task (`BoardCamera*.cs`, `BoardCameraFramingTests.cs`, `Game.unity`, `LevelPreviewHost.cs`, the DEC-103 row, the camera section of `docs/Environment-Layout.md`): wait for the explicit RELEASE message from the owner of TASK-20260924-224137662-d526d5ad-board-camera-framing.
  - May proceed before the release: `URP-Balanced.asset`, `QualitySettings.asset`, `Hub.unity`, `LevelEditor.unity`, the new DEC-104 row and the three wording docs.
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given `BoardCameraFraming` with margins 1.5/5, when `BoardCameraFramingTests` runs, then:
   - 9x5 bounds give distance 16.166 (width-limited) and 5x11 give 13.856 (depth-limited);
   - all 8 bounds corners stay inside the frustum for every existing case at aspects 1080/1920 and 1080/2400;
   - the pose still ignores the previous camera position.
2. Given `Game.unity` played directly with level-01 at 1080x1920, when the environment builds, then:
   - the camera sits at about (0, 10.026, -2.590) with Euler (75, 0, 0);
   - the board spans viewport x 0.067 to 0.933 and y 0.25 to 0.77 (±0.02).
   At 1080x2400 the whole board is visible, spanning x 0.077 to 0.923 (±0.02).
3. Given that Game capture, when pixels are sampled, then:
   - the background at the corners and the top centre is #D6E4FC ±2 per channel, with no darker corners;
   - no Bloom, UberPost or color-grading pass runs;
   - Game and Hub contain no `Volume` component, and both cameras have post-processing off.
4. Given that Game capture, when board pixels are sampled, then:
   - a representative flat lit floor-cell top is #596999 ±15 per channel, superseding the older #6C7B98 target under the approved 2026-09-27 reference calibration;
   - a representative flat lit wall top is #32385D ±15 per channel, superseding #3C4667;
   - a representative cyan block top is #049DF0 ±15 per channel and cyan gate top is #049BF2 ±15, excluding studs/specular highlights; the approved cyan/LightBlue (type 7) palette entry may be calibrated only if material-only tuning does not reach those samples. No unsupported pink target is invented;
   - shadowed floor next to a wall is 70 to 85% of the lit floor's luminance;
   - stud highlights sit toward the upper left and shadows fall down-right, shorter than about a third of a cell.
5. Given the project settings, when they are read back, then:
   - `URP-Balanced.asset` has MSAA 2x, HDR off and shadow distance 30;
   - `QualitySettings` current level is Balanced, and Android and iPhone still default to Balanced;
   - `URP-Balanced-Renderer.asset` still has its active SSAO feature;
   - `EditorBuildSettings.asset` is byte-identical to before.
6. Given `LevelEditor.unity` loaded with level-01 selected in the Level Editor window, when the preview camera renders, then:
   - its light, ambient, background and framing produce the same pixel targets as the Game capture within the same tolerances;
   - `LevelEditor.unity` still holds only its `LevelEditor` GameObject;
   - the LevelPreview EditMode tests pass.
7. Given the change set, when the Environment and LevelPreview EditMode suites and `SceneFlowSmokeTests` run, then all pass with 0 project errors or warnings in the Console.
8. Given the docs, when they are read, then:
   - DEC-103 shows pitch 75, FOV 60 and margins 1.5/5;
   - DEC-104 records the board look decision;
   - the Visual-References, PRD DEC-17 and Art-Asset-PRD-Mapping wording says perspective;
   - `docs/Environment-Layout.md` shows the new camera values.

## References and constraints

- Story/GDD/ADR/design/log/image:
  - The user messages of 2026-09-25 and their two pasted real-game screenshots.
  - `docs/media/color-block-jam/user-references/17-nearest-block-selection.jpg`, `04-time-freeze.png`, `02-hammer-targeting.png`, `14-held-block-outline.png`.
  - CBJ Template `Assets/Project Files/Game/Scenes/Game.unity` (camera 1555-1698, light 2461-2610, RenderSettings 13-41), `Assets/Project Files/Data/Pipeline/URP Asset.asset`, `URP Renderer.asset`, `ProjectSettings/QualitySettings.asset`, `Scripts/Controllers/CameraController.cs`.
  - Apply plan `plan.txt` / `plan.json` (scratchpad path above).
  - PROJECT.md DEC-94, DEC-96, DEC-102, DEC-103; `docs/Rendering-Reference-Review.md` rule 4; `docs/Android-Player-Settings.md`.
- Unity version/platform: Unity 2022.3.62f2, URP 14.0.12, Android and iOS portrait 1080x1920 and 20:9.
- Package/architecture constraints:
  - No package change.
  - Camera framing stays plain C# plus a thin component that is framed once per build (Performance Rule 4).
  - Constants carry a one-line reason (Performance Rule 7).
  - A solid clear colour replaces the template's skybox and ground plane. A UI Toolkit panel cannot sit behind the 3D board.
- Explicit out of scope:
  - Render scale 0.95, shadowmap 512, shadow bias 5 and one-quality-level consolidation from the template (rejected in the plan).
  - Frame-rate or sleep-timeout init.
  - Graphics API order.
  - Legacy MAT_Board and BoardLit.shader changes. The user-approved same-task repair may tune only current shared MAT_Gameplay specular/smoothness and the specified GamePalette entries, preserving Linear/MPB tint/clipping, existing lighting and booster swatches.
  - A true gradient background.
  - A HUD safe-area framing fit.
  - Device performance claims (NFR-P5).

## Verification and risk

- Focused test path/command: `Assets/ColorBlockJam/Tests/Environment/EditMode/BoardCameraFramingTests.cs` through MCP for Unity `run_tests` (EditMode, filter `BoardCameraFramingTests`), then assemblies `ColorBlockJam.Environment.EditModeTests` and `ColorBlockJam.Editor.LevelPreview.Tests`, then PlayMode `SceneFlowSmokeTests`
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: `refresh_unity` compile after the C# edits; `read_console` shows 0 project errors or warnings
- EditMode: `BoardCameraFramingTests` (updated cases), Environment EditMode suite, LevelPreview EditMode suite
- PlayMode: `SceneFlowSmokeTests` (includes `GameRunsWhenOpenedDirectly`)
- Manual/built-player/profiler/device evidence:
  - Game level-01 captures at 1080x1920 and 1080x2400 through a RenderTexture, with the pixel samples of AC-3 and AC-4.
  - LevelEditor preview capture for AC-6.
  - Frame Debugger pass list at Editor quality Balanced.
  - SHA-256 before and after for each scene and asset, and readback of the serialized values.
  - Device checks (NFR-P5, MSAA plus SSAO cost on the Android phone) stay open and are not claimed.
- Risk: high
- Approval already granted (exact target only): the user's request of 2026-09-25 to set the camera angle, light and graphics settings from the references. It covers exactly the scenes, `URP-Balanced.asset`, `QualitySettings.asset` `m_CurrentQuality`, the camera code and the docs listed under Allowed paths.
- Same-task D-001 authority amendment: User 2026-09-27 answered the concrete Brick_End/model, EnvironmentDatabase, GamePalette and shared MAT_Gameplay target question "oyuna uyarla onaylıyorum". Root explicitly assigns palette/material color calibration here, preserving the original task and D-001. This authorizes exactly the two assets and narrow regeneration-default source listed above, reference AC-4 calibration and retained evidence; no shader, new light/camera/settings, atlas or other asset mutation. See authority-amendment-20260927.md.
- Commit/checkin constraint: forbidden
- Current invocation permission: Authority amendment and task-cycle ledger/evidence documentation only. Production and Editor work remains queued after stable Ice → Hub ownership release.
- Handoff destination: this session implements it after the RELEASE, then messages "Ruled tile level sistem" (`LevelPreviewHost.cs` done) and the gameplay session (`Game.unity` free).

## Stop conditions

- The camera task's owner has not sent an explicit RELEASE for a held path: do not write that path.
- The live Editor is compiling, importing, in Play Mode for another session, running tests, or not answering: wait; do not write.
- A target scene has unrelated unsaved changes, or the Editor window title shows `*` for it: do not save it.
- D-001 targets cannot be reached through the approved current shared material and specified palette entries with lighting fixed: stop, retain D-001 and present measured residuals/exact additional targets. No shader/light/atlas change or replacement task is inferred.
- Any change appears in a forbidden path, such as `EditorBuildSettings.asset`: stop and restore from the pre-change backup.
- A test fails outside the updated framing cases: stop and report.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:ab016b0f4117d05c42bedf99b2b511a271fb549f4ee14540eef2f95c9772f0ad
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 1
- Last feedback: User 2026-09-27: "renkler de referanstaki gibi gözükmüyor"; current attachment compared with supplied target.
- Blocker: WAITING_FOR_OWNER — Ice retains live Editor; Hub has the next Editor slot. Palette/shared-material authority is approved; historical AC-2 remains unresolved.
- Next action: None
- Waiting reason: owner
- Action required: none; await stable queued ownership release
- Closed revision: vcs:plastic:cs:17@ColorBlockJamCase2026@efurk64@unity (unreviewed; workspace has pending changes)
- Closed at: 2026-09-28T08:46:46Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | STALE | historical automated: BoardCameraFramingTests 11/11 (isolated copy editmode2.xml); not reverified on cs:13 | none |
| AC-2 | FAIL | historical manual capture: pose exact, x 0.067–0.933, 20:9 x 0.077–0.923; y 0.227–0.748 is 0.022–0.023 below the prediction; not reverified on cs:13 | none |
| AC-3 | STALE | historical manual capture: background #D6E4FC in the corners and top centre, volumes 0, post off; Frame Debugger not run (live MCP down); not reverified on cs:13 | none |
| AC-4 | FAIL | user observation 2026-09-27 and current/target attachments: colors visibly differ; historical pixel capture #7D89A4 floor and #626971 walls remains supporting evidence, not a fresh measurement | none |
| AC-5 | STALE | historical read back: MSAA 2, HDR off, shadow 30, quality Balanced, Android/iPhone → Balanced, SSAO and EditorBuildSettings unchanged; not reverified on cs:13 | none |
| AC-6 | STALE | historical automated capture test: preview equals Game within 1/255; LevelEditor root count and dirty flag asserted; not reverified on cs:13 | none |
| AC-7 | STALE | historical automated: EditMode 70/70, PlayMode 27/27, 0 batch-log errors; not reverified on cs:13 | none |
| AC-8 | STALE | historical manual: DEC-103 updated, DEC-118 added, wording in 3 docs, Environment-Layout camera values; not reverified on cs:13 | none |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| [D-001](defects/D-001.md) | ac_failure:ac-4:board-colors-miss-reference | OPEN | AC_FAILURE | AC-4 | Floor and wall colors appear lighter and greyer than the reference. | Approved 2026-09-27 same-task palette/shared-material authority; reference samples; live repair pending | none |

### Transition history

- Created — planning — contract authored
- 2026-09-25 06:15 — ready → in_progress (implementation). Camera task owner sent RELEASE; the live MCP bridge was down, so serialized edits went through the isolated batchmode copy `%TEMP%\cbj-boardlook-c27b5b` only.
- 2026-09-25 06:40 — implementation → verification. Two tuning runs: intensity 0.6/ambient 0.68 (plan start), then 0.45/0.63 (final, at the tuning bounds). Five serialized files were copied back byte-verified. DEC-104 was already cited by the gameplay contract, so the board look is DEC-118.
- 2026-09-25 — Game.unity handed to the gameplay session (TASK-20260925-012336316-a88e25db-block-drag-gate-exit). It released the scene at sha256 3e35a928…, which adds a Gameplay GameObject and removes EnvironmentPreview. Checked read-only: every DEC-118 value (margins, clear, far plane, post off, no Volume, light, ambient) is unchanged. The AC captures were taken on 9086dcd3.
- 2026-09-27T12:40:45Z — in_progress → waiting — NEW_DEFECT D-001, AC_FAILURE mapped to AC-4; ledger was empty and no linked record existed. Preserved existing task identity and all authority outside this block. CONTRACT_CHANGE_REQUIRED: the modern shared material/shader paths are forbidden; no production attempt consumed (count stays 1). Historical passes are STALE on the current read-only byte snapshot. Root retains color implementation authorization pending exact target authority and Ice Editor release.
- 2026-09-27 approved task-change — User's exact-target answer "oyuna uyarla onaylıyorum" accepted by root. Explicit authority amendment retains original task and D-001; shader remains forbidden. Old verification identities invalidated, attempt count unchanged. Production/Editor remain queued after owners; see authority-amendment-20260927.md.
- 2026-09-28T08:46:46Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
