# TASK-20260925-184759859-c962353f-barrier-starts-closed-no-solid-block — The solid Block obstacle is removed; the Barrier is the only board obstacle

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-184759859-c962353f-barrier-starts-closed-no-solid-block
- Supersedes: none
- Title: The solid Block obstacle is removed; the Barrier is the only board obstacle
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:8
- Task kind: refactor
- Area: Level data schema (board objects) and play-area barrier view
- Workflow: direct

## Objective and baseline

- Observable objective: The Barrier (official obstacle, OBJ-05) is the only board obstacle. The `solid-block` board object no longer exists anywhere in runtime code, data or tests owned here. The Barrier keeps its optional `startsOpen` parameter (default false, closed at start); its editor UI moves from the Obstacles section to the Effect Editor window (Level Editor session).
- Current behavior/evidence:
  - User messages 2026-09-25, with a screenshot of the Level Editor Obstacles section (Block, Barrier, "Starts open"):
    - "barier neden eklendi ve cbj de var mı ayrıca starts open bool nedir gereksiz olmuş sil" (why was the Barrier added, does CBJ have it; the starts-open bool is unnecessary, delete it);
    - "barier kulanacaksak block kalkabilir" (if we use the Barrier, the Block can go);
    - correction with a second screenshot: "sadece bariyer olsun o zaman official olan da olan, block kaldır bu bölümden ve starts open da kalkmalı; bu effect olarak eklenebilir olmalı burada durmak yerine 3. windowdan" (only the official Barrier; remove Block from this section; Starts open leaves this section and should be addable as an effect setting from the third window, the Effect Editor). So `startsOpen` stays in the schema.
  - `barrier` has the optional bool `startsOpen` (TASK-20260925-102657710-f7bfcf6a, v2 schema). `BarrierObject.StartsOpen` is read by `EnvironmentView`/`EnvironmentBarrier` for the start height, by the Level Editor toggle, and by validation ("starts closed on block").
  - `solid-block` (`SolidBlockObject`) was added by TASK-20260925-160721096-2c8da893-solid-obstacle-art (Codex, ready_to_close) to the registry, parameters, checks, link.xml, `EnvironmentSolidComposer`, the Level Editor Block brush and `AttemptLevelConverter`. That task's filled-quadrant composition also fills wide Barriers (barrier-view D-001), which stays.
  - No file under `Assets/ColorBlockJam/Data` uses `startsOpen`, `solid-block` or `barrier` (checked 2026-09-25).
- Expected behavior:
  1. **Schema.** `solid-block` is removed from `LevelEffectRegistry`, `LevelEffectParameters`, `LevelEffectChecks` and `link.xml` and is rejected as an unknown type. `barrier` and its optional `startsOpen` (default false) are unchanged. `k_barrierRuntimeImplemented` stays on its own line; report its line.
  2. **View.** `EnvironmentSolidComposer` keeps the filled-quadrant composition for barriers and loses its `solid-block` path; the barrier view and flip API are unchanged.
  3. **Docs.** `docs/Level-Data-Format.md`, `docs/Environment-Layout.md` and the PROJECT.md solid-obstacle sentences and rows describe the removal; the Barrier is the one board obstacle, and its `startsOpen` is set in the Effect Editor.
- Behavior that must remain unchanged:
  - Every other effect and its messages.
  - Void cells.
  - Wide-barrier fill.
  - The barrier flip API and its 0 B allocation.
  - The environment golden tests.
  - The Level Editor preview of barriers.

## Ownership

- Owner/writer role: environment/play-area session (this conversation), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Runtime/Levels/LevelEffectRegistry.cs
  - Assets/ColorBlockJam/Runtime/Levels/LevelEffectParameters.cs
  - Assets/ColorBlockJam/Runtime/Levels/LevelEffectChecks.cs
  - Assets/ColorBlockJam/Runtime/Levels/LevelEffectValidator.cs
  - Assets/ColorBlockJam/Runtime/Levels/link.xml
  - Assets/ColorBlockJam/Environment/EnvironmentView.cs
  - Assets/ColorBlockJam/Environment/EnvironmentBarrierComposer.cs
  - Assets/ColorBlockJam/Environment/EnvironmentSolidComposer.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelCellObjectTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelDatabaseJsonTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelEffectTests.cs
  - Assets/ColorBlockJam/Tests/Environment/**
  - Assets/ColorBlockJam/Tests/LevelPreview/LevelPreviewTests.cs
  - docs/Level-Data-Format.md
  - docs/Environment-Layout.md
  - PROJECT.md
  - production/tasks/TASK-20260925-184759859-c962353f-barrier-starts-closed-no-solid-block/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Editor/Levels/** and the Level Editor tests (Level Editor session "Parça template boş kareler" removes the Block brush and the Starts open toggle first)
  - Assets/ColorBlockJam/Gameplay/** and Assets/ColorBlockJam/Tests/Gameplay/** (gameplay session removes the `solid-block` handling in AttemptLevelConverter first)
  - Assets/ColorBlockJam/Data/**, Assets/ColorBlockJam/Scenes/**, Assets/ColorBlockJam/Prefabs/**
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked:
  - Level Editor session: Editor/Levels/** (Block brush removal; Starts open toggle moves to the Effect Editor window).
  - Gameplay session: AttemptLevelConverter.cs and AttemptLevelConverterTests.cs.
  - TASK-20260925-160721096-2c8da893 (Codex, ready_to_close): its `solid-block` scope is withdrawn by this user decision; its barrier fill stays.
- Execution dependency: the Level Editor session reports "EDITOR OBSTACLES CLEAN" (Block brush removed, Starts open toggle moved to the Effect Editor) and the gameplay session reports its AttemptLevelConverter cleanup compiles (no reference to `SolidBlockObject` outside this task's paths)
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given barrier JSON with and without `startsOpen`, then both load as before (closed by default, open when true) and a closed barrier on a block is still rejected (EditMode; unchanged behaviour).
2. Given a `solid-block` cell object, then reading rejects it as an unknown type; `rg "SolidBlockObject|solid-block" Assets/ColorBlockJam` finds nothing outside docs history and task folders (EditMode plus grep).
3. Given a level with a straight and a 2x2 barrier, then the play area and the LevelEditor preview show both filled at their start heights; `SetBarrierOpen` sinks and raises them; the 0 B flip test passes (EditMode/PlayMode).
4. Given all EditMode and PlayMode assemblies, then they pass (the full gate, including the Level Editor and Gameplay tests after their owners' cleanup).

## References and constraints

- Story/GDD/ADR/design/log/image: user messages 2026-09-25 (above); catalog OBJ-05; docs/Level-Data-Format.md; TASK-20260925-102657710-f7bfcf6a; TASK-20260925-102657924-08cc082b; TASK-20260925-160721096-2c8da893.
- Unity version/platform: 2022.3.62f2
- Package/architecture constraints: no TypeNameHandling; registry-driven effects; views never change domain state.
- Explicit out of scope: the gameplay Barrier rule, the Level Editor UI (other owners), other obstacles.

## Verification and risk

- Focused test path/command: LevelCellObjectTests, LevelDatabaseJsonTests, LevelEffectTests, Tests/Environment/**, LevelPreviewTests
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console
- EditMode: acceptance 1, 2, 4
- PlayMode: acceptance 3, 4
- Manual/built-player/profiler/device evidence: a LevelEditor preview capture of the raised barriers
- Risk: medium (schema key removal across three sessions)
- Approval already granted (exact target only): the user messages above
- Commit/checkin constraint: forbidden
- Current invocation permission: the user's order ("sil", "kalkabilir")
- Handoff destination: /implement-task TASK-20260925-184759859-c962353f-barrier-starts-closed-no-solid-block, then an independent verifier

## Stop conditions

- A remaining reference outside this task's paths after both owners report clean.
- Removing `solid-block` would also remove the wide-barrier fill.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: TASK_CLOSED
- Contract fingerprint: sha256:86ccef1fde2b2d901b39b821321c67918132c1c7e52e3789d948a6f3b8b18780
- Evidence fingerprint: sha256:29c43ddd00ad6daacd3e1b7c1900a000f2da0ce769ea58d8ab52e44e068a7c0f
- Reviewed revision: vcs:plastic:cs:9@ColorBlockJamCase2026@efurk64@unity+owned-sha256:0a507a34c75a84793f8433d98cad64e0f839a96419410f5fb5934ff75ba8a140
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:9@ColorBlockJamCase2026@efurk64@unity+owned-sha256:0a507a34c75a84793f8433d98cad64e0f839a96419410f5fb5934ff75ba8a140
- Closed at: 2026-09-25T22:27Z
- Closure mode: criteria_verified
- Closed by: implement-task (direct, empty ledger; independent verifier PASS)
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Barrier and startsOpen unchanged | PASS | independent verifier (chain wf_3b45c785-27f, evidence.md); later full gate on the runtime-shapes verify (EditMode 1024/0, PlayMode 56/56): barrier JSON with/without startsOpen round-trips; closed-on-block rejected | vcs:plastic:cs:9@ColorBlockJamCase2026@efurk64@unity+owned-sha256:0a507a34c75a84793f8433d98cad64e0f839a96419410f5fb5934ff75ba8a140 |
| AC-2 solid-block gone | PASS | independent verifier (chain wf_3b45c785-27f, evidence.md); later full gate on the runtime-shapes verify (EditMode 1024/0, PlayMode 56/56): solid-block rejected as unknown type; rg finds only the rejection fixture | vcs:plastic:cs:9@ColorBlockJamCase2026@efurk64@unity+owned-sha256:0a507a34c75a84793f8433d98cad64e0f839a96419410f5fb5934ff75ba8a140 |
| AC-3 Barriers raised and filled; flip unchanged | PASS | independent verifier (chain wf_3b45c785-27f, evidence.md); later full gate on the runtime-shapes verify (EditMode 1024/0, PlayMode 56/56): LevelPreviewHost capture, straight and 2x2 barriers raised and filled, flip sinks/raises; Environment PlayMode | vcs:plastic:cs:9@ColorBlockJamCase2026@efurk64@unity+owned-sha256:0a507a34c75a84793f8433d98cad64e0f839a96419410f5fb5934ff75ba8a140 |
| AC-4 Full gate green | PASS | independent verifier (chain wf_3b45c785-27f, evidence.md); later full gate on the runtime-shapes verify (EditMode 1024/0, PlayMode 56/56): full EditMode and PlayMode | vcs:plastic:cs:9@ColorBlockJamCase2026@efurk64@unity+owned-sha256:0a507a34c75a84793f8433d98cad64e0f839a96419410f5fb5934ff75ba8a140 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — user removed the Barrier's startsOpen flag and the solid Block obstacle (2026-09-25)
- 2026-09-25T18:50Z — contract revised before execution — user correction: startsOpen stays (moves to the Effect Editor window); only the solid Block obstacle is removed
- 2026-09-25T22:27Z — in_progress — chain: implement, 3 reviewers (2 doc findings), fix, independent verify: AC-1..AC-4 PASS; k_barrierRuntimeImplemented on LevelEffectRegistry.cs line 51
- 2026-09-25T22:27Z — TASK_CLOSED — all criteria PASS at the reviewed revision
<!-- TASK-LIFECYCLE:END -->
