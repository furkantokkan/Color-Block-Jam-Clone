# TASK-20260924-195553117-ca96e402-level-database-inspector — Level Database inspector shows its JSON and levels

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-195553117-ca96e402-level-database-inspector
- Supersedes: none
- Title: Level Database inspector shows its JSON and levels
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: UI
- Area: Level authoring (editor inspector)
- Workflow: direct

## Objective and baseline

- Observable objective: Selecting `Assets/ColorBlockJam/Data/Levels/LevelDatabase.asset` shows its assigned level JSON in the Inspector, lets the designer assign another registered level JSON, and lists the levels that JSON contains.
- Current behavior/evidence: User screenshot (2026-09-24): the Inspector shows only the `Script` field. `LevelDatabase.m_activeJsonGuid` (Addressables GUID `b93477650b31db14b8d2e3b792a56b08`) is `[HideInInspector]`; no level list exists. The Level Database window already selects the active JSON through `LevelDatabaseAddressables.GetEntries`, `TryRegister`, `TrySetActive` and `GetActivePath`.
- Expected behavior: A UI Toolkit custom inspector (`[CustomEditor(typeof(LevelDatabase))]`) shows a `JSON Source` `TextAsset` field resolved from the active GUID. Assigning a `.json` under `Data/Levels` registers it as an Addressable when needed and activates it through `LevelDatabaseAddressables` (Undo and dirty handling as that helper does). Below it, a read-only list parsed with `LevelDatabaseJson.TryRead` shows each level's order, ID, name, `width x height`, timer, door count and block count, or the exact parse error. An empty database shows a create-a-level hint. An "Open Level Database Window" button opens the existing window.
- Behavior that must remain unchanged: `LevelDatabase` runtime loading, JSON schema and validation, the Level Database window, `LevelDatabaseAddressables` behavior, Addressables group settings, level JSON content.

## Ownership

- Owner/writer role: root implementer; sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseInspector.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseInspector.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/LevelDatabaseInspectorTests.cs
  - production/tasks/TASK-20260924-195553117-ca96e402-level-database-inspector/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/Levels/**
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseAddressables.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseFile.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelDraft.cs
  - Assets/ColorBlockJam/Editor/ColorBlockJam.Editor.asmdef
  - Assets/ColorBlockJam/Data/Levels/** (no manual edit; only the helper's own active-GUID write when a designer assigns a JSON)
  - Assets/AddressableAssetsData/**
  - Plastic checkin, shelve or publishing
- Serialized assets: none (the inspector writes `LevelDatabase.asset` only through the existing `LevelDatabaseAddressables.TrySetActive` path when a designer assigns a JSON; automated tests use a temporary database instance)
- Other active writers/worktrees checked: No task contract owns `Runtime/Levels/**` or `Editor/Levels/**`. A separate level-pipeline session edited those files until 22:13 on 2026-09-24 and owns the delegated `voidCells` schema; this task adds one new editor file and edits none of theirs.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given the project database with its active JSON, when `LevelDatabase.asset` is selected, then the Inspector shows a `JSON Source` field holding `Data/Levels/LevelDatabase.json`.
2. Given a JSON with levels, when the inspector draws, then each level appears in file order with ID, name, `width x height`, timer, door count and block count; an empty `levels` array shows a create-a-level hint; an invalid JSON shows the `LevelDatabaseJson` error instead of a list.
3. Given a designer assigns another `.json` under `Data/Levels`, when the field changes, then `LevelDatabaseAddressables` registers and activates it, the list refreshes, and Undo restores the previous active JSON; a non-level asset is rejected with a message and no change.
4. Given the inspector exists, then runtime assemblies, the Level Database window and level JSON files are unchanged, and the Editor compiles with zero errors.

## References and constraints

- Story/GDD/ADR/design/log/image: User screenshot of the LevelDatabase Inspector (2026-09-24); PROJECT.md level-data rule (canonical JSON with a ScriptableObject reference); AGENTS.md UI Toolkit rule for editor tooling.
- Unity version/platform: 2022.3.62f2; Editor only
- Package/architecture constraints: UI Toolkit (`CreateInspectorGUI`), no IMGUI; ZString for labels built per row; no `System.Linq`; editor-only assembly `ColorBlockJam.Editor`.
- Explicit out of scope: Schema changes, void-cell painting, importing reference levels into `LevelDatabase.json`, runtime loading changes.

## Verification and risk

- Focused test path/command: `Assets/ColorBlockJam/Tests/EditMode/LevelDatabaseInspectorTests.cs` through MCP for Unity `run_tests` (EditMode filter).
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: Clean compile and zero Console errors after the change.
- EditMode: Acceptance 1-3 (inspector builds the field and rows from a temporary database and JSON; invalid/empty cases; assignment path rejects a non-level asset).
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence: Inspector screenshot of `LevelDatabase.asset` with its JSON and level rows.
- Risk: low
- Approval already granted (exact target only): User request 2026-09-24 ("level database'e json atanmalı ve levelleri göstermeli üstünde").
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: independent sequential verifier, then automatic closure

## Stop conditions

- The level-pipeline session starts editing `Editor/Levels/**` or `LevelDatabaseAddressables` changes its API: re-read before writing and queue behind that writer.
- MCP for Unity is disconnected when compile, tests or the screenshot need the live Editor.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: TASK_CLOSED
- Contract fingerprint: sha256:832b2e6145bfd86fe9339cde284dd7f650459f4882c554c94cd1a57730c28049
- Evidence fingerprint: sha256:8c328522eddf823d002315442b389ecbf44392d2504e40c9377107ed689d1eab
- Reviewed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:f0fffde3c2e4738394bb68c336dcc079a714bc20d2401490846ef0c6cfc7cd1e
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: none
- Closed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:f0fffde3c2e4738394bb68c336dcc079a714bc20d2401490846ef0c6cfc7cd1e
- Closed at: 2026-09-24T21:00:14Z
- Closure mode: automatic
- Closed by: implement-task
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Active JSON shown | PASS | automated: EditMode InspectorShowsTheProjectDatabaseActiveJson passed (docs/verification/environment/editmode.xml) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:f0fffde3c2e4738394bb68c336dcc079a714bc20d2401490846ef0c6cfc7cd1e |
| AC-2 Level list, empty and invalid states | PASS | automated: DescribeListsEveryLevelInFileOrder, DescribeExplainsAnEmptyOrInvalidJson passed | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:f0fffde3c2e4738394bb68c336dcc079a714bc20d2401490846ef0c6cfc7cd1e |
| AC-3 Assign, register, activate, Undo | PASS | automated: AssigningAJsonOutsideTheLevelFolderChangesNothing passed; isolated-copy check assign=True, 9 rows, undoRestored=True (inspector-assignment.txt) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:f0fffde3c2e4738394bb68c336dcc079a714bc20d2401490846ef0c6cfc7cd1e |
| AC-4 No runtime/window/data change, clean compile | PASS | only the two new files; live Editor compiled ColorBlockJam.Editor and EditModeTests with zero errors at 23:48 local; independent verifier VERIFIED | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:f0fffde3c2e4738394bb68c336dcc079a714bc20d2401490846ef0c6cfc7cd1e |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- 2026-09-24 — TASK_CLOSED — automatic closure after independent verification; 4/4 inspector EditMode tests, isolated-copy assign/Undo check and live-Editor clean compile. Evidence gap: no Inspector screenshot (MCP for Unity was not connected); the automated tests cover AC-1.
- Created — planning — routed by task-bug (NO_TASK_MATCH, planned change) from the user's Inspector screenshot
- 2026-09-24 — in_progress — user said "taski bitirene kadar devam et"; direct initial execution; preflight READY WITH WARNINGS reused from this session (MCP stdio 0 instances); lifecycle manually serialized (Plastic).
<!-- TASK-LIFECYCLE:END -->
