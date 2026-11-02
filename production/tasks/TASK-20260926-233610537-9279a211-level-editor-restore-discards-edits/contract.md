# TASK-20260926-233610537-9279a211-level-editor-restore-discards-edits — Restore discards unsaved edits back to the level's saved state

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260926-233610537-9279a211-level-editor-restore-discards-edits
- Supersedes: none
- Title: Restore discards unsaved edits back to the level's saved state
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:10 plus the uncommitted Level Editor work of 2026-09-26/27 (planning provenance only)
- Task kind: UI
- Area: Level Editor window, JSON Source row (`ColorBlockJam.Editor`)
- Workflow: direct

## Objective and baseline

- Observable objective: When the level has unsaved edits, the Restore button on the JSON Source row is enabled. Pressing it asks for confirmation, then discards the edits and returns the level to its last saved state, the state before editing began. With no unsaved edits it keeps its current job, Restore Previous Save.
- Current behavior/evidence:
  - User messages of 2026-09-27: "buradaki restore eğer kayıt edemediysek bölümü ana haline ilk haline discard edebilir editlemeye başlamadan önceki", then "kastetiğim buradaki kapalı button sağdaki". Their screenshot shows the JSON Source row with the Refresh button and the greyed-out Restore button to its right.
  - `LevelDatabaseWindow` creates `m_restoreButton` ("restore-backup", UndoHistory icon, tooltip "Restore Previous Save: bring back the JSON version that the last save replaced.") and enables it only when `m_saveController.SourceValid && File.Exists(GetBackupPath())` (around line 1434).
  - `RestorePreviousSave` asks `AskToLeaveDraft()` (Save/Discard/Cancel), then writes the backup to disk and reloads (around line 4209).
- Expected behavior:
  - The button is enabled when a level is loaded and either it has unsaved changes (`HasDraftChanges`) or a backup exists. Its enabled state follows every draft change (edit, undo, redo, save, load).
  - With unsaved changes, pressing it:
    - shows a confirmation with a Discard and a Cancel button, Cancel the default, naming the level: "Discard your unsaved changes to {level name} and return it to its last saved state?";
    - on Discard, rebuilds the draft from the level's saved state exactly as the Discard choice of `AskToLeaveDraft` followed by a reload of that level does. That drops pending draft template edits and resets the colour order, and clears the draft's undo history, like Reload and Restore today. The status reads "Discarded unsaved changes: {level name} is back to its saved state." and nothing is written to disk;
    - on Cancel, changes nothing.
  - Without unsaved changes, pressing it runs Restore Previous Save exactly as today.
  - The tooltip names both: "Restore: with unsaved changes, discard them and return the level to its last saved state; otherwise bring back the JSON version that the last save replaced."
  - The confirmation goes through a replaceable seam (for example `ConfirmDiscardChanges`, a `Func<string, bool>` defaulting to `EditorUtility.DisplayDialog`), so tests never open a dialog.
- Behavior that must remain unchanged:
  - Restore Previous Save with no unsaved changes; Reload JSON; Save; `AskToLeaveDraft` on level or database switches and on close.
  - The domain-reload kept draft.
  - Everything else on the top bar.

## Ownership

- Owner/writer role: implement-task (single writer)
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs (Restore button state, handler, tooltip and seam only)
  - Assets/ColorBlockJam/Editor/Levels/LevelDraftController.cs (only if the discard rebuild needs a controller method)
  - Assets/ColorBlockJam/Tests/EditMode/LevelRestoreDiscardTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelRestoreDiscardTests.cs.meta
  - docs/Level-Editor-Design.md (the Reload/Restore lines only)
  - production/tasks/TASK-20260926-233610537-9279a211-level-editor-restore-discards-edits/contract.md (managed lifecycle block only)
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Editor/Levels/Canvas/** and Assets/ColorBlockJam/Editor/LevelPreview/** (TASK-20260926-231349009-945d78fe, Ice, holds them)
  - Any other test file
  - Plastic checkin, shelve, branch, or undo of other sessions' pending changes
  - Pressing the live Restore, or changing the user's open draft or level JSON
- Serialized assets: none
- Other active writers/worktrees checked (2026-09-26T23:46Z):
  - TASK-20260926-231349009-945d78fe-ice-visibility (Codex) holds a lock. Its paths do not include LevelDatabaseWindow.cs.
  - TASK-20260926-212320311-949d4415 (this session) holds a lock with a pending tooltip rerun in LevelDatabaseWindow.cs. It is a different region (the Pen tooltip), and this task runs after that rerun.
  - The Unity Editor is shared: check editor state before every refresh or test run.
- Execution dependency: none (same-session sequencing after the gate task's rerun)
- Queue policy: none

## Acceptance scenarios

1. AC-1: Given a loaded level with unsaved edits and no backup, when the Level Editor shows, then Restore is enabled. After Save, Undo back to the saved state, or a level switch, with no backup, it is disabled.
2. AC-2: Given unsaved edits, when Restore is pressed and the confirmation returns Discard, then the draft equals the saved level, `hasUnsavedChanges` is false, the status names the level, and the level JSON file is byte-identical to before.
3. AC-3: Given unsaved edits, when the confirmation returns Cancel, then the draft and its undo stack are unchanged.
4. AC-4: Given no unsaved edits and a backup, when Restore is pressed, then Restore Previous Save runs as before.
5. AC-5: Given the Level EditMode suite, when it runs, then it passes, and the tooltip names both behaviours.
6. AC-6: Given the live Level Editor, when the user edits and presses Restore, then they accept the behaviour (explicit user acceptance).

## References and constraints

- Story/GDD/ADR/design/log/image: the user messages above and their screenshot of the JSON Source row; docs/Level-Editor-Design.md (Reload/Restore).
- Unity version/platform: Unity 2022.3.62f2, Windows Editor only
- Package/architecture constraints: UI Toolkit; the existing save and draft controllers; m_/k_ naming; class and method comments in the file's style.
- Explicit out of scope: an undoable discard; changes to Reload JSON; backups of more than one version.

## Verification and risk

- Focused test path/command: UnityMCP `run_tests` EditMode `ColorBlockJam.Tests.EditMode.LevelRestoreDiscardTests`, then the Level suite `ColorBlockJam[.]Tests[.]EditMode[.]Level`
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: a clean compile; check `mcpforunity://editor/state` first
- EditMode: button state, Discard, Cancel, no-changes path and file byte-equality, through a test window and the confirmation seam
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence: the user's acceptance (AC-6)
- Risk: low
- Approval already granted (exact target only): the user's messages above
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: `/implement-task TASK-20260926-233610537-9279a211-level-editor-restore-discards-edits`

## Stop conditions

- The discard needs a change outside the allowed paths.
- Another writer takes LevelDatabaseWindow.cs.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:1f0f369f8195a298cb6f68b723b147144f87933f5b699e1e9cd48a0155b0ff0d
- Evidence fingerprint: sha256:9cb1b533426b964009d46aab21d5483c57073c5799a3335bb8779ffa24298a88
- Reviewed revision: vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:fc42981f4315d4437e879446d9f7b61a91a5cac872981d565f2bd19f7e6112f3
- Attempt count: 0
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:fc42981f4315d4437e879446d9f7b61a91a5cac872981d565f2bd19f7e6112f3
- Closed at: 2026-09-27T17:24:20Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated: LevelRestoreDiscardTests.UnsavedChangesEnableRestoreWithoutABackup and SaveLevelDisablesRestoreWithoutABackup (job c2d8031a) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:fc42981f4315d4437e879446d9f7b61a91a5cac872981d565f2bd19f7e6112f3 |
| AC-2 | PASS | automated: DiscardReturnsTheDraftToTheSavedLevelWithoutWritingTheJson (saved draft, pending template edit dropped, status, JSON byte-identical) (job c2d8031a) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:fc42981f4315d4437e879446d9f7b61a91a5cac872981d565f2bd19f7e6112f3 |
| AC-3 | PASS | automated: CancelKeepsTheDraftAndItsUndo (job c2d8031a) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:fc42981f4315d4437e879446d9f7b61a91a5cac872981d565f2bd19f7e6112f3 |
| AC-4 | PASS | automated: WithoutUnsavedChangesRestoreBringsBackThePreviousSave (through the write seam, never the real database) (job c2d8031a) | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:fc42981f4315d4437e879446d9f7b61a91a5cac872981d565f2bd19f7e6112f3 |
| AC-5 | PASS | automated: Level suite 729/729 (job 8144f72a); TheTooltipNamesBothJobs | vcs:plastic:cs:10@ColorBlockJamCase2026@efurk64@unity+owned-sha256:fc42981f4315d4437e879446d9f7b61a91a5cac872981d565f2bd19f7e6112f3 |
| AC-6 | UNPROVEN | none | none |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored 2026-09-26T23:47Z by session "Edit dropdown ve colors kareleri" from the user's messages of 2026-09-27
- Started — implementation — 2026-09-26T23:48Z: the user's request is the execution order; LevelDatabaseWindow.cs is outside the Ice task's paths.
- Verification — 2026-09-27T00:12Z: workflow wf_e340c36e-5d1 implemented RestoreLevel, DiscardDraftChanges (LoadSelectedDraft, no disk write), UpdateRestoreButton on every draft change, the ConfirmDiscardChanges seam and the tooltip, with 6 LevelRestoreDiscardTests. Two lenses found 3 findings and skeptics confirmed all 3. The confirmation made Discard the Enter default, so it now follows the file's ConfirmTemplateSave pattern: DisplayDialogComplex(Cancel, Keep Editing, Discard) == 2, where Enter picks Cancel and Esc picks Keep Editing. The template-edit assertion was tightened. Focused 15/15 (job c2d8031a, with the gate task's tooltip tests) and the Level suite 729/729 (job 8144f72a). A known limit: Discard returns to the version loaded in the editor, not a newer file edited outside it.
- 2026-09-27T17:24:20Z Closed by explicit user confirmation (task-done, user_confirmed): the user accepted the live result on 2026-09-27 ('bunlar okay', item 6, the Restore button; AC-6) and ordered 'tasklari kapat izin veriyorum'. Criteria rows are preserved as recorded, not re-verified; owned files may have changed after the reviewed revision through other sessions.
<!-- TASK-LIFECYCLE:END -->
