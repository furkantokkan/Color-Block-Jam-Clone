# TASK-20260929-224728300-b48469c6-ui-template-preview-styles — Make UI templates carry their authoring styles

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260929-224728300-b48469c6-ui-template-preview-styles
- Supersedes: none
- Title: Make UI templates carry their authoring styles
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity (cs:38)
- Base revision: cs:38
- Task kind: UI
- Area: UI Toolkit standalone template authoring
- Workflow: direct

## Objective and baseline

- Observable objective: Runtime UI templates opened individually in UI Builder have their shared button/text styling and existing runtime font without relying on a parent UIDocument theme.
- Current behavior/evidence: User requests "bunu düzel ui kısmını inceleyip", then permits the full UI scope with "bütün ui şablonlarına bakabilrsin". The 13-document inventory shows that only GameplayHud and the newly repaired Home load Common.uss directly; other text-bearing templates inherit their styling from a parent. StartupLoading and ScreenFade use an unqualified Style element. CurrencyHolder inherits its entire layout from Home.uss.
- Expected behavior: Templates carry shared styles/font and preview visibly with identified authoring samples. Host-scoped rules hide embedded overlays before binding. Pre-level selectors clone authored UXML instead of constructing layout in C#. Samples never become runtime data. Canvas dimensions remain 1080 x 1920.
- Behavior that must remain unchanged: Runtime names, bindings, positions, dimensions, visibility, data, scenes, panels, art, input, gameplay and safe areas. The Home defect stays on its original task. Latest user AGENTS instructions supersede the original proposal to preview hidden overlays using temporary toggles.

## Ownership

- Owner/writer role: current implementer, then independent read-only verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/UI/Hub/CurrencyHolder.uxml
  - Assets/ColorBlockJam/UI/Hub/CurrencyHolder.uss
  - Assets/ColorBlockJam/UI/Hub/Hub.uxml
  - Assets/ColorBlockJam/UI/Hub/Hub.uss
  - Assets/ColorBlockJam/UI/Common/Document.uss
  - Assets/ColorBlockJam/UI/Hub/BoosterSelector.uxml
  - Assets/ColorBlockJam/UI/Game/GameplayHud.uxml
  - Assets/ColorBlockJam/UI/Game/GameplayHud.uss
  - Assets/ColorBlockJam/UI/Common/ScreenFade.uss
  - Assets/ColorBlockJam/Runtime/Hub/PreLevel/PreLevelView.cs
  - Assets/ColorBlockJam/Runtime/Core/SceneFlow/ScreenFadeView.cs
  - Assets/ColorBlockJam/Tests/Hub/EditMode/PreLevelAssetTests.cs
  - Assets/ColorBlockJam/Tests/UI/EditMode/UiTemplateAuthoringTests.cs
  - Assets/ColorBlockJam/Tests/UI/EditMode/SettingsPopupAssetTests.cs
  - Assets/ColorBlockJam/UI/Hub/PreLevel.uxml
  - Assets/ColorBlockJam/UI/Hub/PreLevel.uss
  - Assets/ColorBlockJam/UI/Settings/SettingsPopup.uxml
  - Assets/ColorBlockJam/UI/Settings/SettingsPopup.uss
  - Assets/ColorBlockJam/UI/Game/TopBar/GameTopBar.uxml
  - Assets/ColorBlockJam/UI/Game/TopBar/GameTopBar.uss
  - Assets/ColorBlockJam/UI/Game/Overlays/GameOverlays.uxml
  - Assets/ColorBlockJam/UI/Game/Overlays/GameOverlays.uss
  - Assets/ColorBlockJam/UI/Game/Overlays/LevelCompletePopup.uxml
  - Assets/ColorBlockJam/UI/Game/Overlays/LevelCompletePopup.uss
  - Assets/ColorBlockJam/UI/Game/Overlays/RestartConfirmPopup.uxml
  - Assets/ColorBlockJam/UI/Game/Overlays/RestartConfirmPopup.uss
  - Assets/ColorBlockJam/UI/Startup/StartupLoading.uxml
  - Assets/ColorBlockJam/UI/Startup/StartupLoading.uss
  - Assets/ColorBlockJam/UI/Common/ScreenFade.uxml
  - docs/UI-Authoring.md
  - production/tasks/TASK-20260929-224728300-b48469c6-ui-template-preview-styles/verification/**
  - production/tasks/TASK-20260929-224728300-b48469c6-ui-template-preview-styles/contract.md
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Runtime files outside the two declared view files
  - Assets/ColorBlockJam/UI/Purchase/** (active purchase-authoring owner)
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Art/**
  - Assets/ColorBlockJam/UI/Common/Common.uss
  - Assets/ColorBlockJam/UI/Hub/Home.uxml
  - Library/UIBuilder/** direct file writes
  - Existing .meta files
  - Plastic checkin, shelve, package changes or publishing
- Serialized assets: none
- Other active writers/worktrees checked: Home verifier returned and released the Editor. Purchase task TASK-20260929-224511984-eaa2cae5-purchase-popup-authoring owns Purchase sources and its tests; preserve those files. Common.uss and LevelCompletePopup.uss were already dirty; preserve existing content. Declared writes do not overlap.
- Execution dependency: Home verifier handoff received. Wait for purchase owner release before final aggregate live Editor verification; source edits remain disjoint.
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given each of the 13 runtime UXML documents, when imported and cloned in the exact Editor, then stylesheet and template references resolve, no unqualified Style element remains, and no new importer errors occur.
2. Given standalone templates on a default-theme 1080 x 1920 preview without gameplay code or visibility toggles, then each previews visibly with shared styling, font and identified representative content. Pre-level selector layout is reusable UXML.
3. Given unbound Hub and GameplayHud clones including reattachment/re-enable, then embedded overlays are closed before rendering. Binding replaces samples and open/close/disposal work; focused asset/presenter tests pass.
4. Given docs/UI-Authoring.md, then it explains canvas dimensions, local styles, standalone versus host visibility and samples versus runtime data.

## References and constraints

- Story/GDD/ADR/design/log/image: User Home UI Builder screenshot C:/Users/e-fur/AppData/Local/Temp/codex-clipboard-84d45288-3604-4bf7-9566-44b1b932eccc.png; PROJECT.md DEC-39, DEC-143, DEC-157, DEC-169; docs/Hub-And-Player-Data-Design.md; preceding authored-holder defect D-001.
- Unity version/platform: Unity 2022.3.62f2; Android/iOS portrait, 1080 x 1920 reference.
- Package/architecture constraints: Existing UI Toolkit, Common.uss and LiberationSans-UITK FontAsset; use approved unityMCP for this pre-Unity-6 Editor after exact identity proof.
- Explicit out of scope: Runtime redesign, new fonts/art, economy/catalog changes, serialized assets, unrelated Common.uss changes. Binding discards/replaces authoring samples.

## Verification and risk

- Focused test path/command: Existing SettingsPopupAssetTests, PreLevelAssetTests and HomePresenterTests, plus live import/clone/style inspection of every UXML.
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: Roslyn compile check for owned C# changes; verify imports and relevant Console diagnostics.
- EditMode: Existing focused asset and presenter tests once after final edits.
- PlayMode: Not required for adding the same existing shared styles and font; compare composed resolved layout and hidden defaults in the Editor.
- Manual/built-player/profiler/device evidence: Exact-Editor standalone template and composed layout/font readback; inspect Home visually. Device build not required for this authoring-only change.
- Risk: low
- Approval already granted (exact target only): Current user request to fix the UI and inspect all templates; source/documentation only.
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: independent verifier, then implement-task acceptance finalization

## Stop conditions

- Another live writer owns an exact changed template or the shared Editor.
- A fix would change runtime dimensions, economy/catalog behavior, art, panels, packages or scenes.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: ready_to_close
- Current phase: verification
- Last cycle verdict: READY_TO_CLOSE
- Contract fingerprint: sha256:0083dd03bb8aa1df2ae4574315e422e75f6396aee13fa4fc7d7874921bf1535f
- Evidence fingerprint: sha256:b6241cd9aba961368b718f71a1f3aaa41aebf10a747476b599923c4f5395f639
- Reviewed revision: vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:f1dae09a943e69193ba1e20ec4e09e7e43073273c9a189dd18a167eb76ecd208
- Attempt count: 1
- Last feedback: Latest user instructions require visible standalone documents, modular authored layout and safe hidden host initialization.
- Blocker: none
- Next action: $task-done TASK-20260929-224728300-b48469c6-ui-template-preview-styles
- Closed revision: none
- Closed at: none
- Closure mode: none
- Closed by: none
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated: 14-document import/reference check, job5880eba4f31d4425b411e06f1d53fc8b | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:f1dae09a943e69193ba1e20ec4e09e7e43073273c9a189dd18a167eb76ecd208 |
| AC-2 | PASS | automated+manual: all14 standalone panels styled/visible; Builder documents reopened clean1080x1920; evidence.md | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:f1dae09a943e69193ba1e20ec4e09e7e43073273c9a189dd18a167eb76ecd208 |
| AC-3 | PASS | automated: 45 focused cases; host reattachment, sample replacement, attached null-icon clearing, open/close and fade callbacks; job5880eba4f31d4425b411e06f1d53fc8b plus repair a1dac9bd4cc24384bd5fd53745463b24; runtime smoke23pass | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:f1dae09a943e69193ba1e20ec4e09e7e43073273c9a189dd18a167eb76ecd208 |
| AC-4 | PASS | manual: docs/UI-Authoring.md read back, modular layout/visibility/sample rules recorded | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:f1dae09a943e69193ba1e20ec4e09e7e43073273c9a189dd18a167eb76ecd208 |

Each criterion may be verified by an automated test, manual verification, or explicit user acceptance. Explicit user acceptance requires no additional artifact.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — standalone styling is a separate outcome from the prior canvas-only task (which forbids UXML/USS edits) and the Home authored-holder defect. Execution remains authorized and is queued after the sole live Editor verifier.
- Started — implementation — Home verifier released the Editor; purchase sources remain owned by their other task. User's latest explicit authoring rules revise acceptance and source scope before implementation.
- Verification repair — the existing Settings assembly assertion still expects three references. PROJECT.md DEC-196 records this pre-existing drift after Haptics; the current approved UI assembly has Runtime, Player, Haptics, Onity.Reactive and PrimeTween.Runtime. Authorize a surgical test-only expectation update preserving the no-scene/composition boundary; no assembly/source dependency is changed.
- Verified — all four template-authoring criteria PASS at the reviewed source identity; 45 focused cases, 14 saved/reopened canvases and 23 runtime smoke cases pass. Empty defect ledger preserved. Closure remains explicit user only.
<!-- TASK-LIFECYCLE:END -->
