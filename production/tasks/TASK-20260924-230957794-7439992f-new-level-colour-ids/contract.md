# TASK-20260924-230957794-7439992f-new-level-colour-ids — New levels use valid palette colour ids

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-230957794-7439992f-new-level-colour-ids
- Supersedes: none
- Title: New levels use valid palette colour ids
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: bug
- Area: Level authoring (Level Editor window, Create Level)
- Workflow: direct

## Objective and baseline

- Observable objective: A level created with the Level Editor's "+" (Create Level) builds in the LevelEditor preview and the Game scene without a colour error.
- Current behavior/evidence: Found on 2026-09-25 by the Level Editor design review and verified in code.
  - `LevelDatabaseWindow.CreateLevel` seeds door `d1` and block `b1` with the colour id `"red"` (`LevelDatabaseWindow.cs:679`, `:683`).
  - The shared colour rule `BoardLayout.TryParseDoorColor` compares exact `PaletteColor` names ordinally (`BoardLayout.cs:134-147`), so `"red"` is rejected. `BoardLayout.TryCreate` fails, and the environment and preview skip the level.
  - The planned v2 validation (`TASK-20260924-215645373-33a6a7a4-level-json-v2-effects`, rule 7) will also reject it at Save.
  - Task-bug routing: NO_TASK_MATCH (`CreateLevel` has no owning contract).
- Expected behavior: `CreateLevel` seeds `"Red"` (the `PaletteColor.Red` name) for both the starter door and block. Nothing else changes.
- Behavior that must remain unchanged: The Create Level flow (ids, size, timer, starter shape, selection, dialogs), existing level files, and all other window behavior.

## Ownership

- Owner/writer role: environment/play-area session (this conversation, by "her şeyi sen yap"), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelCreateColourTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelCreateColourTests.cs.meta
  - production/tasks/TASK-20260924-230957794-7439992f-new-level-colour-ids/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Data/**
  - Any change other than the two colour literals and a focused test
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked: Another live session edited `LevelDatabaseWindow.cs` at 01:55 on 2026-09-25 (no contract). The user decided to wait for it to finish. Run this before `TASK-20260924-230915885-717889da-level-editor-shell` (a two-line change).
- Execution dependency: the other live writer of `LevelDatabaseWindow.cs` has finished (file stable, or the user confirms); `TASK-20260924-222616741-46522211-level-editor-scene-preview` verifier handoff
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given Create Level, when the new level is snapshotted, then its door and block colour ids parse with `BoardLayout.TryParseDoorColor`, and `BoardLayout.TryCreate` succeeds (EditMode, through the window's creation path or its extracted seed).
2. Given the LevelEditor preview, when "+" is clicked, then the new level appears without a colour warning (live check; the level JSON is saved only if the scenario creates a scratch database).

## References and constraints

- Story/GDD/ADR/design/log/image: `docs/Level-Editor-Design.md` section 4 (the starter-colour note); `BoardLayout.TryParseDoorColor`.
- Unity version/platform: 2022.3.62f2; Editor
- Package/architecture constraints: none beyond the project rules.
- Explicit out of scope: case-insensitive colour parsing, and migrating existing JSON.

## Verification and risk

- Focused test path/command: `LevelCreateColourTests` (EditMode)
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console
- EditMode: acceptance 1
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence: acceptance 2 in the live Editor with a scratch database, or user acceptance
- Risk: low
- Approval already granted (exact target only): user order "her şeyi sen yap" (2026-09-25) for `LevelDatabaseWindow.cs`
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260924-230957794-7439992f-new-level-colour-ids, then an independent verifier

## Stop conditions

- The other writer already changed the literals: verify, record, and close without an edit.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: TASK_CLOSED
- Contract fingerprint: sha256:43a2d1011c5c221df56f3100ce23d15634627c15722f05a59001ea1d47addf5d
- Evidence fingerprint: sha256:516db7679eb91f26364d2453589407d419df3fe95ce995afde639005a5b6d565
- Reviewed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e6b8d7cd86b73a82efd678f89cdbec011615c5a19f25441442f71b716ec3729d
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: none
- Closed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e6b8d7cd86b73a82efd678f89cdbec011615c5a19f25441442f71b716ec3729d
- Closed at: 2026-09-25T09:08Z
- Closure mode: automatic
- Closed by: implement-task (direct, empty ledger; independent verifier PASS)
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 New level colours parse | PASS | EditMode test run in a fresh isolated copy (%TEMP%\CBJcolourverify2-ff0896), started with Unity.exe -batchmode -runTests on three test assemblies: ColorBlockJam (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e6b8d7cd86b73a82efd678f89cdbec011615c5a19f25441442f71b716ec3729d |
| AC-2 New level previews without warning | PASS | manual: live Editor check through LevelPreviewOwner.OnLevelPublished with the starter seed, 3x3 and 1 block, 0 warnings (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:e6b8d7cd86b73a82efd678f89cdbec011615c5a19f25441442f71b716ec3729d |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — task-bug NO_TASK_MATCH from the design review finding (lowercase "red" in CreateLevel)
- 2026-09-25T09:08Z — in_progress — task-chain workflow: implement, 3 reviewers (0 findings), fix, independent verify (tests in an isolated copy while MCP was down)
- 2026-09-25T09:08Z — TASK_CLOSED — all criteria PASS at the reviewed revision
<!-- TASK-LIFECYCLE:END -->
