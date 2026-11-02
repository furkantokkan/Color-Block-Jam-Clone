# TASK-20260929-221118474-d89af766-level-editor-place-once — Return to selection after placing a piece

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260929-221118474-d89af766-level-editor-place-once
- Supersedes: none (revises only the repeated-placement clause of select-mode-placement AC-2)
- Title: Return to selection after placing a piece
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:38 plus existing unrelated uncommitted work
- Task kind: UI
- Area: Level Editor piece placement
- Workflow: direct

## Objective and baseline

- Observable objective: After one successful palette-piece placement, the pointer returns to ordinary Select without holding another piece.
- Current behavior/evidence: The screenshot shows Select active with Square still armed. `LevelDatabaseWindow.PlaceBlock` deliberately leaves the piece armed for repeated placement, as specified by the earlier select-mode-placement contract. Raw request: "template den block seçip koyduktan sonra selection mode geri dönsün elimde hala bloğu taşımaya devam ediyorum".
- Expected behavior: A successful placement disarms the piece, clears its ghost and palette arming outline, and keeps Select active. A second free-cell click adds nothing until a piece is picked again. A refused placement retains the armed piece for another attempt.
- Behavior that must remain unchanged: Existing placement geometry, color, named-piece membership, one Undo step and one preview publish per placement; current empty selection after placing; normal selection and drag; pre-placement rotation, mirroring and gate color pickup; Pen, freehand and obstacle brushes; authored data writes only through the existing explicit Save actions.

## Ownership

- Owner/writer role: current Codex session /root, implementer; sequential read-only verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Core/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Tests/Editor/EditMode/LevelCanvasBlockToolsTests.cs
  - docs/Level-Editor-Design.md
  - production/tasks/TASK-20260929-221118474-d89af766-level-editor-place-once/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Art/**
  - Editing serialized assets or their metadata
  - Saving live designer drafts or changing Play Mode
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked: no active lifecycle claims; existing dirty work is preserved; implementation and verification run sequentially in this workspace.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given a picked palette piece, when a free cell accepts its placement, then exactly one block is added with the picked shape and color, Select remains active, no shape or ghost is armed, and the palette arming outline disappears.
2. Given that successful placement, when another empty cell is clicked without picking a piece again, then no block is added; ordinary block selection and drag remain available.
3. Given an armed piece, when a placement overlaps or leaves the board, then the draft stays unchanged, the refusal names the cell, and the piece remains armed; a later valid placement disarms it.
4. Given a successful placement, when Undo/Redo is used, then the single placement is removed/restored without rearming; placement preview publishes once, and the existing tool, gate-color and piece-edit regressions pass.

## References and constraints

- Story/GDD/ADR/design/log/image: docs/Level-Editor-Design.md; earlier select-mode-placement contract; C:/Users/e-fur/AppData/Local/Temp/codex-clipboard-661e6360-2f2b-4744-a103-9a262e026ca0.png
- Unity version/platform: 2022.3.62f2, Editor only
- Package/architecture constraints: reuse SetTool(Select) and existing SyncTool; no new public API, assembly, package or serialized asset.
- Explicit out of scope: altering Pen/freehand behavior, brushes, runtime gameplay or authored data.

## Verification and risk

- Focused test path/command: LevelCanvasBlockToolsTests, LevelEditorToolStateTests, LevelPenEditTests, LevelPlusTileTests and LevelPieceEditControllerTests through pinned unityMCP EditMode tests.
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: Roslyn COMPILE_OK and no Unity compilation errors after script refresh.
- EditMode: placement success, refusal, next click, Undo/Redo and the named preservation fixtures pass.
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence: not required; real UI pointer events verify the Editor behavior in isolated window fixtures.
- Risk: low
- Approval already granted (exact target only): requested scoped source change and focused verification.
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: read-only verifier, then $task-done TASK-20260929-221118474-d89af766-level-editor-place-once on explicit user closure.

## Stop conditions

- Stop for a conflicting writer, unproven live project identity, or a required edit outside the declared paths.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: in_progress
- Current phase: verification
- Last cycle verdict: CONTINUE_SAME_TASK
- Contract fingerprint: sha256:f541db6fed81bcf7a82ccd0b0d7cc56850cb46060d15a2b18b2312a4901b6a4b
- Evidence fingerprint: sha256:bf5d8074919c59e70103e8628856ffc942d2f7548ce7c5298db719ec89aaae79
- Reviewed revision: vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:e6b28f36e40a0b6a9cd791470e68b70ce6c3a8ce187c2257f5fc34d85c6b9e11
- Attempt count: 1
- Last feedback: user requested one-shot palette placement on 2026-09-30 local date
- Blocker: required preservation suite has three failures in unchanged LevelPlusTileTests, outside the placement branch
- Next action: resolve the existing Plus naming/palette test ownership before further repair; repeat only failing tests, then complete verification
- Closed revision: none
- Closed at: none
- Closure mode: none
- Closed by: none
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Successful placement disarms the piece and updates the UI | PASS | independent source review; updated pointer scenario completed without a listed failure in job 78983bac61fa4e7a957e64e4789c7841; evidence.md | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:e6b28f36e40a0b6a9cd791470e68b70ce6c3a8ce187c2257f5fc34d85c6b9e11 |
| AC-2 A later empty click does not place another block; selection and drag remain | PASS | independent source review; new second-click/selection scenario and existing drag test in completed job; evidence.md | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:e6b28f36e40a0b6a9cd791470e68b70ce6c3a8ce187c2257f5fc34d85c6b9e11 |
| AC-3 Refused placement preserves the armed piece | PASS | independent source review; new overlap/off-board scenario in completed job; evidence.md | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:e6b28f36e40a0b6a9cd791470e68b70ce6c3a8ce187c2257f5fc34d85c6b9e11 |
| AC-4 Undo, preview publication and tool/piece preservation | UNPROVEN | Undo/Redo and publish scenarios report no failure; full required suite has three unrelated Plus failures; exact passed/skipped counts unavailable; evidence.md | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:e6b28f36e40a0b6a9cd791470e68b70ce6c3a8ce187c2257f5fc34d85c6b9e11 |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — bounded behavior revision from the user request; task-bug semantic gate routed to create-task, no existing acceptance failure claimed.
- Started — implementation — user request authorizes scoped implementation; pinned Editor and sole-writer checks passed.
- Verified — verification — scoped placement behavior supported, Roslyn COMPILE_OK and Console zero errors; independent verifier confirmed three unchanged Plus scenarios outside PlaceBlock; broader suite gap remains, no closure.
<!-- TASK-LIFECYCLE:END -->
