# TASK-20260924-221851550-ed53750d-addressables-settings-registration — Level Editor finds the Addressables settings again

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-221851550-ed53750d-addressables-settings-registration
- Supersedes: none
- Title: Level Editor finds the Addressables settings again
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: bug
- Area: Level authoring (Addressables settings registration)
- Workflow: direct

## Objective and baseline

- Observable objective: The Level Editor window's "Level Database" dropdown lists `LevelDatabase` again, and the window loads that database's levels in the live Editor.
- Current behavior/evidence: User report 2026-09-25, "level database seçilmiyor". The user's screenshot shows the dropdown as "No Databases" with the status "The active level JSON is not in the Level Database Addressables group." Live-Editor diagnosis over MCP HTTP (`ColorBlockJamCase2026@99077e4df1ecfaa4`):
  - `AddressableAssetSettingsDefaultObject.GetSettings(false)` returns `null`, so `LevelDatabaseAddressables.GetEntries()` returns an empty list.
  - `LevelDatabase.asset` has the active GUID `b93477650b31db14b8d2e3b792a56b08`, which equals the GUID of `LevelDatabase.json`. The JSON imports as a `TextAsset`.
  - `Assets/AddressableAssetsData/AssetGroups/Level Database.asset` lists that GUID.
  - `ProjectSettings/EditorBuildSettings.asset` has `m_configObjects: {}`. The `com.unity.addressableassets` config object is missing; it should point to `Assets/AddressableAssetsData/DefaultObject.asset` (GUID `1a5f6faa0e2b5304893ecbadc126cc9d`).
  - Addressables 1.22.3 resolves its settings only through that config object (`AddressableAssetSettingsDefaultObject.Settings`). The Addressables data assets date from 22:05 on 2026-09-24, but `EditorBuildSettings.asset` was last written at 18:49 the same day. A fresh batchmode copy whose `EditorBuildSettings.asset` has the entry lists the database correctly.
- Expected behavior:
  1. Register the existing `DefaultObject.asset` as the `com.unity.addressableassets` config object through the Unity API, in one transport. This persists it in `ProjectSettings/EditorBuildSettings.asset`; no Addressables data asset is created or replaced.
  2. The window then lists `LevelDatabase`, and Reload JSON loads its five levels.
  3. An EditMode regression test fails whenever the config object is missing or does not resolve to the committed `AddressableAssetSettings.asset`.
- Behavior that must remain unchanged:
  - `Assets/AddressableAssetsData/**` content and GUIDs, including the `Level Database` group, its entry and the settings asset.
  - `LevelDatabase.asset`'s active GUID, and the level JSON content.
  - The `LevelDatabaseAddressables`, `LevelDatabaseWindow` and inspector code.
  - The `EditorBuildSettings` scene list.

## Ownership

- Owner/writer role: environment/play-area session (this conversation), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - ProjectSettings/EditorBuildSettings.asset
  - Assets/ColorBlockJam/Tests/EditMode/AddressablesSettingsRegistrationTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/AddressablesSettingsRegistrationTests.cs.meta
  - production/tasks/TASK-20260924-221851550-ed53750d-addressables-settings-registration/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/** except ProjectSettings/EditorBuildSettings.asset
  - Assets/AddressableAssetsData/**
  - Assets/ColorBlockJam/Editor/Levels/**
  - Assets/ColorBlockJam/Data/Levels/**
  - Changing the EditorBuildSettings scene list
  - Calling `AddressableAssetSettingsDefaultObject.GetSettings(true)` or any API that can create or replace settings assets
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: ProjectSettings/EditorBuildSettings.asset
    Sole writer: environment/play-area session through the live Editor API (`EditorBuildSettings.AddConfigObject`)
    Matching `.meta` owner: not applicable (ProjectSettings file)
- Other active writers/worktrees checked: The level-pipeline session owns `Editor/Levels/**` and holds ready Addressables tasks (`TASK-20260924-210432886-2f31f26b-remote-level-json`); this task edits neither its code nor `AddressableAssetsData`. The live Editor runs this project in HTTPLocal MCP mode.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given the live Editor with the missing config object, when the existing `DefaultObject.asset` is registered, then `GetSettings(false)` returns `Assets/AddressableAssetsData/AddressableAssetSettings.asset`, `LevelDatabaseAddressables.GetEntries()` lists `LevelDatabase.json`, and `ProjectSettings/EditorBuildSettings.asset` contains `com.unity.addressableassets` with GUID `1a5f6faa0e2b5304893ecbadc126cc9d` and an unchanged scene list.
2. Given the repaired Editor, when the user opens or reloads the Level Editor window, then the dropdown shows `LevelDatabase` and the Target Level list shows its levels.
3. Given the config object is missing or points elsewhere, when `AddressablesSettingsRegistrationTests` runs, then it fails with a message naming the missing registration.
4. Given the repair, then `Assets/AddressableAssetsData/**` files are byte-identical to before.

## References and constraints

- Story/GDD/ADR/design/log/image: user screenshot and message 2026-09-25; `Library/PackageCache/com.unity.addressables@1.22.3/Editor/AddressableAssetSettingsDefaultObject.cs` (Settings getter/setter); AGENTS.md level-data bullet (Level Database Addressables group).
- Unity version/platform: 2022.3.62f2; Editor
- Package/architecture constraints: Addressables 1.22.3 stays; no new package; the live-Editor mutation goes through one MCP transport only.
- Explicit out of scope: Level Editor code changes, the remote catalog, the LevelEditor scene preview, and root-causing which past copy-back dropped the entry beyond recording it.

## Verification and risk

- Focused test path/command: `ColorBlockJam.Tests.EditMode.AddressablesSettingsRegistrationTests` (EditMode)
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile; no Addressables errors after the repair
- EditMode: acceptance 1 and 3 through the focused test; existing `LevelDatabaseAddressablesTests` stay green
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence: acceptance 2 as the live window state (read over MCP) or user confirmation; acceptance 4 by file hashes
- Risk: high (project settings write)
- Approval already granted (exact target only): none; `ProjectSettings/EditorBuildSettings.asset` needs the user's explicit go before the write
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260924-221851550-ed53750d-addressables-settings-registration, then an independent verifier

## Stop conditions

- The user declines the `EditorBuildSettings.asset` write.
- `DefaultObject.asset` does not reference `AddressableAssetSettings.asset` (GUID `a7274985f03c24f41abdf9b5c1c09c7d`): stop, do not create new settings.
- Another writer changes `ProjectSettings/EditorBuildSettings.asset` or `Assets/AddressableAssetsData/**` during the repair.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: TASK_CLOSED
- Contract fingerprint: sha256:5c1758156fafc27baa5fe74bece2bb7b01acf4d2e417a057290334f34ab5a285
- Evidence fingerprint: sha256:2f9967805d8f711eec63cb4340956027eb7c13bcc5907e2919e325446b8fdb5b
- Reviewed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:659b2c62e971299d5e03ae0a53ac3e1567653357d8744cafb80c633ec1c0b240
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: none
- Closed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:659b2c62e971299d5e03ae0a53ac3e1567653357d8744cafb80c633ec1c0b240
- Closed at: 2026-09-24T22:25Z
- Closure mode: automatic
- Closed by: implement-task (direct, empty ledger)
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Config object restored, entries listed | PASS | live Editor API result + AddressablesSettingsRegistrationTests 3/3 (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:659b2c62e971299d5e03ae0a53ac3e1567653357d8744cafb80c633ec1c0b240 |
| AC-2 Window lists LevelDatabase and its levels | PASS | manual: live window read over MCP (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:659b2c62e971299d5e03ae0a53ac3e1567653357d8744cafb80c633ec1c0b240 |
| AC-3 Regression test guards the registration | PASS | automated: EditMode 7/7 job 9bcf21d9 (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:659b2c62e971299d5e03ae0a53ac3e1567653357d8744cafb80c633ec1c0b240 |
| AC-4 AddressableAssetsData unchanged | PASS | automated: sha256 snapshot compare (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:659b2c62e971299d5e03ae0a53ac3e1567653357d8744cafb80c633ec1c0b240 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's "level database seçilmiyor" report via task-bug NO_TASK_MATCH
- 2026-09-24T22:25Z — in_progress — user approved the EditorBuildSettings write; config object re-registered through the live Editor API
- 2026-09-24T22:25Z — TASK_CLOSED — AC-1..AC-4 PASS at the reviewed revision; ledger empty
<!-- TASK-LIFECYCLE:END -->
