# TASK-20260924-210432886-2f31f26b-remote-level-json — Remote-updatable level JSON through Addressables

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-210432886-2f31f26b-remote-level-json
- Supersedes: none
- Title: Remote-updatable level JSON through Addressables
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: feature
- Area: Level data delivery (Addressables content updates)
- Workflow: direct

## Objective and baseline

- Observable objective: A level JSON fix can be published after release as an Addressables content update; an installed game picks it up on its next online launch, while a first or offline launch still plays the shipped levels.
- Current behavior/evidence (2026-09-24): `LevelDatabase.LoadAsync` loads the active JSON with `Addressables.LoadAssetAsync<TextAsset>(m_activeJsonGuid)` from the local `Level Database` group (`BundledAssetGroupSchema` build/load paths = `Local.*`, `ContentUpdateGroupSchema.m_StaticContent: 0`). `AddressableAssetSettings`: `m_BuildRemoteCatalog: 0`, empty remote catalog paths, `m_DisableCatalogUpdateOnStart: 0`, `m_CatalogRequestsTimeout: 0`, one `Default` profile whose `Remote.LoadPath` is the local hosting service. No code calls `CheckForCatalogUpdates`/`UpdateCatalogs`.
- Expected behavior (user decisions 2026-09-24: level JSON stays Addressable and must be remotely fixable; hosting URL is decided later, the mechanism ships now):
  1. **Offline-safe baseline:** the `Level Database` group stays local and becomes `Cannot Change Post Release` (`m_StaticContent: 1`), so the player build always contains the shipped JSON.
  2. **Remote catalog:** `Build Remote Catalog` on, catalog build/load paths = `Remote.BuildPath`/`Remote.LoadPath`; `Disable Catalog Update On Startup` on (the game checks explicitly); a catalog request timeout (about 10 s) so no request waits forever.
  3. **Profiles:** keep `Default` (local Hosting Service for Editor testing). Add a `Release` profile whose `Remote.LoadPath` is a clearly invalid placeholder until the user supplies the URL, for example `https://configure-level-content-url.invalid/[BuildTarget]`.
  4. **Startup update (owned, bounded):** inside the existing ProjectScope build callback, before loading the JSON, run `Addressables.CheckForCatalogUpdates` and, when it returns catalogs, `Addressables.UpdateCatalogs`, then make sure the level JSON's dependencies are downloaded before continuing. Pass the build callback's cancellation token; failures, timeouts and offline starts log one warning and continue with the cached or shipped catalog. No polling after startup, no `async void`, every handle released (PROJECT.md lifecycle rules).
  5. **Publishing tool:** an editor command (for example `Color Block Jam/Data/Build Level Content Update`) that validates every level JSON in the group with `LevelDatabaseJson`, refuses to run while the release URL is the placeholder, runs the Addressables content-update check and `BuildContentUpdate` against the saved content state, and reports the `ServerData/[BuildTarget]` folder to upload.
  6. **Content state:** the player build's `addressables_content_state.bin` is kept per released platform (the content update needs it).
- Behavior that must remain unchanged: JSON schema and validation, the Level Editor/Level Database window workflow, level Save/Reload/Restore, `LevelDatabase` public API, direct Play of every scene, the play-area environment and all other systems.

## Ownership

- Owner/writer role: level-pipeline session (the session that owns `Runtime/Core/Levels/**`, `Editor/Core/Levels/**` and the Addressables setup); sequential independent verifier
- Path layout: rebased on 2026-09-28 to the DEC-157 folder layout (`docs/Folder-Layout.md`); the project-scope level load still lives in `ProjectScopeInstaller.cs` (its build callback), and `Runtime/Core/Startup/StartupLoader.cs` (DEC-140) only waits for the project scope's ready task, so no other file joins this contract.
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Runtime/Core/Levels/LevelDatabase.cs
  - Assets/ColorBlockJam/Runtime/Core/Composition/ProjectScopeInstaller.cs
  - Assets/ColorBlockJam/Editor/Core/Levels/LevelDatabaseAddressables.cs
  - Assets/ColorBlockJam/Editor/Core/Levels/LevelContentUpdate.cs
  - Assets/ColorBlockJam/Editor/Core/Levels/LevelContentUpdate.cs.meta
  - Assets/AddressableAssetsData/AddressableAssetSettings.asset
  - Assets/AddressableAssetsData/AssetGroups/Schemas/Level Database_ContentUpdateGroupSchema.asset
  - Assets/AddressableAssetsData/AssetGroups/Schemas/Level Database_BundledAssetGroupSchema.asset
  - Assets/AddressableAssetsData/Android/**
  - Assets/AddressableAssetsData/iOS/**
  - Assets/ColorBlockJam/Tests/Editor/EditMode/LevelDatabaseAddressablesTests.cs
  - Assets/ColorBlockJam/Tests/Editor/EditMode/LevelContentUpdateTests.cs
  - Assets/ColorBlockJam/Tests/Editor/EditMode/LevelContentUpdateTests.cs.meta
  - docs/Level-Content-Updates.md
  - docs/verification/level-content-updates/**
  - README.md
  - PROJECT.md
  - production/tasks/TASK-20260924-210432886-2f31f26b-remote-level-json/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/Environment/**
  - Assets/ColorBlockJam/Editor/Environment/**
  - Assets/ColorBlockJam/Runtime/Core/Layout/**
  - Assets/ColorBlockJam/Runtime/Core/Board/**
  - Assets/ColorBlockJam/Runtime/Presentation/**
  - Assets/ColorBlockJam/Editor/Core/BoardKit/**
  - Assets/ColorBlockJam/Data/Levels/*.json content (no level edits in this task)
  - Assets/ColorBlockJam/Scenes/**
  - Uploading content to any server, creating cloud projects or buckets, entering real hosting URLs or credentials
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/AddressableAssetsData/AddressableAssetSettings.asset
    Sole writer: level-pipeline session (through the Addressables API or window, not manual YAML)
    Matching `.meta` owner: unchanged
  - Asset: Assets/AddressableAssetsData/AssetGroups/Schemas/Level Database_ContentUpdateGroupSchema.asset
    Sole writer: level-pipeline session
    Matching `.meta` owner: unchanged
  - Asset: Assets/AddressableAssetsData/AssetGroups/Schemas/Level Database_BundledAssetGroupSchema.asset
    Sole writer: level-pipeline session
    Matching `.meta` owner: unchanged
- Other active writers/worktrees checked: The level-pipeline session edited `Editor/Levels/LevelDatabaseWindow.cs` and `Tests/EditMode/LevelDatabaseTests.cs` at 00:00 local on 2026-09-25 and is the intended owner. The play-area environment task (`TASK-20260924-185655569-dea430a7-play-area-environment`) and the board-kit session must not be touched. `PROJECT.md` and `README.md` are shared: append-only edits, re-read right before each edit.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given the Addressables settings, when inspected, then Build Remote Catalog is on with Remote catalog paths, Disable Catalog Update On Startup is on, the catalog request timeout is set, the `Level Database` group uses local paths with `Cannot Change Post Release`, and a `Release` profile exists whose `Remote.LoadPath` is the documented placeholder.
2. Given a first launch without network (or with the placeholder URL), when the ProjectScope loads, then the catalog check fails with one warning, the shipped level JSON loads, and every scene plays as before.
3. Given a released build and a changed level JSON, when the editor command builds a content update and the output is served (Addressables Hosting Service is acceptable for verification), then the next online launch updates the catalog, downloads the JSON bundle before continuing, and `LevelDatabase` returns the changed levels.
4. Given an update was downloaded once, when the game relaunches offline, then it plays the updated levels from cache; given the app was killed after the catalog update but before the bundle download, when it relaunches offline, then it still loads a valid level JSON (document the chosen recovery).
5. Given an invalid level JSON or the placeholder URL, when the editor command runs, then it refuses before building and names the file or setting.
6. Given the startup path, then the update runs once, under the ProjectScope build callback's cancellation token, with no `async void`, no polling after startup, and every Addressables handle released; the existing level database and scene-flow tests still pass.

## References and constraints

- Story/GDD/ADR/design/log/image: User requests on 2026-09-24/25 ("level json formatı addressable olsun ileride uzaktan fix atabilmemiz için", hosting URL later, level-pipeline session implements); PROJECT.md level-data and lifecycle rules; README "Authored data"; `Editor/Core/Levels/LevelDatabaseAddressables.cs`.
- Unity version/platform: 2022.3.62f2, `com.unity.addressables` 1.22.3; Android and iOS
- Package/architecture constraints: No new packages. Use the Addressables content-update workflow (`ContentUpdateScript`) rather than a custom downloader. JSON stays the single level authority; Newtonsoft with `TypeNameHandling.None`. Record the decision as a new PROJECT.md decision row.
- Explicit out of scope: Choosing or configuring the hosting provider, uploading content, Remote Config, level schema changes (`voidCells` is a separate handoff), Level Editor UI changes beyond the publish command.

## Verification and risk

- Focused test path/command: `LevelDatabaseAddressablesTests` and `LevelContentUpdateTests` (EditMode, `Assets/ColorBlockJam/Tests/Editor/EditMode`), existing `SceneFlowSmokeTests` (PlayMode, `Assets/ColorBlockJam/Tests/Presentation/PlayMode`).
- Required execution preflight: `unity-cli` project readiness (`unity-preflight` is retired and routes there)
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: Clean compile and Console after the settings and code change.
- EditMode: Acceptance 1 and 5 (settings/profile/group assertions; publish-command refusals).
- PlayMode: Acceptance 2 and 6 (offline or placeholder start, lifecycle), existing scene-flow smoke tests.
- Manual/built-player/profiler/device evidence: Acceptance 3 and 4 with a player build and the Addressables Hosting Service (online update, offline relaunch, interrupted update); Android device check when the real URL exists.
- Risk: high
- Approval already granted (exact target only): User on 2026-09-24/25 approved making the level JSON remotely updatable through Addressables, changing the Addressables settings, profiles and `Level Database` group schemas above, and adding the startup update and publish command; the hosting URL stays a placeholder.
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: level-pipeline session runs `/implement-task TASK-20260924-210432886-2f31f26b-remote-level-json`, then an independent verifier

## Stop conditions

- Another writer is editing the same Addressables or `Runtime/Levels` files: re-read and serialize.
- A step would need a real hosting URL, credentials, a cloud project or an upload: stop and ask the user.
- The content-update check reports restrictions the design does not cover: record them and ask before moving groups.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:1ae112ff254cb9d0244cf2ee25e5e9b93cb26a4f485f001bd7dfe77204695d02
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 0
- Last feedback: none
- Blocker: user decision 2026-09-28: remote level JSON is not needed now; the Addressables settings step (settings-step.md) must not be applied
- Next action: None
- Closed revision: cs:23 (Plastic workspace head, unreviewed)
- Closed at: 2026-09-29T01:16:19Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Remote catalog, profiles and static level group | UNPROVEN | pending: EditMode LevelContentUpdateTests.ProjectSettingsShipLevelsLocallyAndCheckARemoteCatalog (needs the settings-step.md write first), ConfigureTurnsOnTheRemoteCatalogAndAStaticLocalLevelGroup, ConfigureKeepsAnExistingReleaseUrlAndActiveProfile, ConfigureRefusesWithoutTheLevelGroupAndChangesNothing; LevelDatabaseAddressablesTests.CreatedGroupKeepsShippedLevelsLocalAndStatic | none |
| AC-2 Offline or placeholder first launch | UNPROVEN | pending: PlayMode SceneFlowSmokeTests.ProjectScopeLoadsActiveAddressableLevelJson and InitKeepsTheLoadingScreenUntilHubIsReady (Editor, Use Asset Database); manual Android build with the Release placeholder (one warning in logcat, levels play) | none |
| AC-3 Online content update applies | UNPROVEN | pending: manual player build with Default + Hosting Service, Build Level Content Update, serve ServerData | none |
| AC-4 Offline relaunch and interrupted update | UNPROVEN | pending: manual, same build (offline relaunch; bundle missing after catalog update, then offline, falls back to the shipped catalog) | none |
| AC-5 Publish command refusals | UNPROVEN | pending: EditMode LevelContentUpdateTests.PlaceholderUrlRefusesBeforeBuilding, InvalidLevelJsonRefusesAndNamesTheFile, MissingContentStateRefusesAndNamesThePath | none |
| AC-6 Owned, bounded startup update | UNPROVEN | pending: EditMode LevelContentUpdateTests.StartupUpdateUsesNoAsyncVoid; existing scene-flow PlayMode tests; code review of ProjectScopeInstaller.LevelContentUpdater | none |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's remote-update request; implementation assigned to the level-pipeline session by user decision
- 2026-09-28 — Contract change (path rebase) — in_progress — dispatched by the orchestrating session "Görevleri inceleme ve dağıtma [8ea911]" at workspace head cs:17; lifecycle lock held by "Görevleri inceleme ve dağıtma [8ea911] / remote-level-json-implementer". Every normative path rebased with the DEC-157 mapping (`docs/Folder-Layout.md`): Runtime/Levels -> Runtime/Core/Levels, Runtime/Composition -> Runtime/Core/Composition, Editor/Levels -> Editor/Core/Levels, Tests/EditMode -> Tests/Editor/EditMode, SceneFlowSmokeTests in Tests/Presentation/PlayMode, Environment -> Runtime/Environment and Editor/Environment, Runtime/Layout and Runtime/Board -> Runtime/Core/..., Presentation -> Runtime/Presentation, Editor/BoardKit -> Editor/Core/BoardKit; the new test file's `.meta` is listed with it. The project-scope level load still lives in ProjectScopeInstaller's build callback (StartupLoader only awaits the ready task), so no file joins. `unity-preflight` routes to `unity-cli`. Dated evidence lines keep their original paths. Scope, acceptance and risk unchanged; contract fingerprint recomputed.
- 2026-09-28 — Implementation complete — in_progress — Code:
  - `ProjectScopeInstaller.LevelContentUpdater` runs once before `LevelDatabase.LoadAsync`: catalog check and update, the JSON bundle download, and a fallback to the shipped catalog when the updated bundle is missing and offline. It logs one warning, releases every handle, and returns without a request when no catalog can update.
  - `LevelContentUpdate`: `TryConfigure`, `TryValidate`, `TryBuild` and the menu command `Color Block Jam/Data/Build Level Content Update`.
  - `LevelDatabaseAddressables.CreateGroup` makes a recreated group static.
  - Tests in `LevelContentUpdateTests`, plus one in `LevelDatabaseAddressablesTests`.
  - Docs: `docs/Level-Content-Updates.md` and PROJECT.md DEC-162. DEC-160 and DEC-161 were already taken.
  - README hunks were added at 10:59, before the coordinator's README freeze; the exact text is in `readme-hunk.md`.

  The Roslyn compile check passes for 23 assemblies (`docs/verification/level-content-updates/compile-check.txt`). The Addressables settings write is pending because the Editor was in Play Mode (`settings-step.md`). The Addressables assets and `EditorBuildSettings` are byte-identical to the baseline. No Unity test ran (user order). No checkin was made.
- 2026-09-28T08:27:52Z — Parked — waiting — user order 2026-09-28 ("remote dan json çekmemize şu anda gerek yok"). The orchestrator stopped the task before the Addressables settings write: AddressableAssetSettings and the Level Database schemas are unchanged on disk, so the startup check stays inert (no locator can update content). The code, tests, docs, PROJECT.md DEC-162 row and README text already written stay in the workspace pending the user's keep-or-remove decision. Acceptance results unchanged.
- 2026-09-29T01:16:19Z — Closed (user confirmed) — closed — explicit user order 2026-09-29 ("açık taskları da kapat üstünde çalışılmayanları artık"); prior state waiting / parked by user decision (implementation in workspace, Addressables settings step not applied). Closure mode user_confirmed: every acceptance criterion result, blocker note and defect row is preserved as recorded, not verified by this closure.
<!-- TASK-LIFECYCLE:END -->
