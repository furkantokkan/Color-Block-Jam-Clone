# TASK-20260924-224138113-766ebc15-level-editor-compact-actions — Level Editor: compact icon actions beside their fields, Save alone at the bottom left

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-224138113-766ebc15-level-editor-compact-actions
- Supersedes: none
- Title: Level Editor: compact icon actions beside their fields, Save alone at the bottom left
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: UI
- Area: Level authoring (Level Editor window)
- Workflow: direct

## Objective and baseline

- Observable objective: The Level Editor window puts each action next to the field it acts on:
  - A small Refresh button on the JSON Source row.
  - Small add (+) and delete icon buttons on the Target Level row, beside the existing reorder arrows.
  - Only Save Level at the bottom left.
- Current behavior/evidence:
  - User requests and screenshots 2026-09-25:
    - "reload json bu tarafa taşınıp refresh yazabilir küçük bir button olarak" ("Reload JSON could move over here and say Refresh, as a small button"), with an arrow from "Reload JSON" to the JSON Source row.
    - "delete level de aynı şekilde buraya taşınabilir ve add de aynı şekilde artı olarak ikonu olabilir alt tarafta sol alt tarafta save olsa yeterli" ("Delete Level can move here the same way, and Add can be a plus icon the same way; at the bottom, Save at the bottom left is enough"), with an arrow from "Delete Level" to the reorder arrows on the Target Level row.
  - `LevelDatabaseWindow.CreateGUI` builds `m_sourceField` (a disabled `ObjectField`), the Target Level dropdown row, an actions row (Create Level, Delete Level, Save Level) and a source-actions row (Reload JSON, Restore Previous Save).
- Expected behavior:
  1. JSON Source row: the field grows. At its right:
     - "Refresh" (tooltip "Reload the level JSON from disk") calls `ReloadDatabase`.
     - "Restore" (tooltip "Restore the previously saved JSON") calls `RestorePreviousSave`.
     Both are small and keep their current enable rules. Restore moves here, beside the file it restores, because the bottom row keeps only Save.
  2. Target Level row: after the existing reorder arrows, a small "+" icon button (tooltip "Create Level") calls `CreateLevel`, and a small delete icon button (tooltip "Delete Level") calls `DeleteLevel`. Both keep their current enable rules and dialogs. They use Unity built-in editor icons (`EditorGUIUtility.IconContent`, for example `Toolbar Plus` and `TreeEditor.Trash`), with a text fallback when an icon is missing.
  3. Bottom: one "Save Level" button, left-aligned and not full width, with its current enable rule and behaviour.
  4. The old Create/Delete/Save row and the Reload/Restore row are removed.
- Behavior that must remain unchanged:
  - The semantics of Reload, Restore, Create, Delete and Save: unsaved-draft prompts, dialogs, status texts, backups.
  - All other controls, including those added by the level-pipeline session (Delete Database, reorder arrows, Doors/Blocks foldouts, block tools).
  - The `DraftLevelPublished` hook.

## Ownership

- Owner/writer role: environment/play-area session (this conversation, by user order "her şeyi sen yap"), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - production/tasks/TASK-20260924-224138113-766ebc15-level-editor-compact-actions/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseAddressables.cs
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseFile.cs
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Scenes/**
  - Changing the behaviour of any action, or controls other than the ones named above
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked: The level-pipeline session edits `LevelDatabaseWindow.cs` (Delete Database, reorder arrows); `TASK-20260924-222616741-46522211-level-editor-scene-preview` added the hook. Re-read the file immediately before the edit and change only the layout lines named here.
- Execution dependency: TASK-20260924-222616741-46522211-level-editor-scene-preview reaches a stable verifier handoff (it may still edit the same file)
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given the window is open, then:
   - the JSON Source row shows the field plus small Refresh and Restore buttons;
   - the Target Level row shows the arrows plus the "+" and delete icon buttons;
   - the bottom shows only a left-aligned Save Level button;
   - no Reload JSON, Create Level or Delete Level text button remains.
2. Given a clean draft, when Refresh is clicked, then the database reloads and the status reads "Reloaded <file> from disk."; with an unsaved draft, the existing prompt appears.
3. Given "+" is clicked, then a level is created and selected as before; given the delete icon is clicked, then the existing confirmation dialog appears and Delete behaves as before.
4. Given no database or no selection, then each button's enabled state matches the old button's rule.
5. Given the other controls, then they behave as before.

## References and constraints

- Story/GDD/ADR/design/log/image: user screenshots 2026-09-25 (arrows to the JSON Source row and to the reorder arrows).
- Unity version/platform: 2022.3.62f2; Editor
- Package/architecture constraints: UI Toolkit; inline styles like the existing rows; built-in editor icons only.
- Explicit out of scope: other window redesign, a USS theme, and keyboard shortcuts.

## Verification and risk

- Focused test path/command: live window inspection (UI Toolkit query of rows and buttons, enabled states) plus existing EditMode tests
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console
- EditMode: existing level editor and LevelPreview tests stay green
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence:
  - A capture of the window.
  - Refresh, "+" and delete exercised in the live Editor without saving level JSON changes (the level JSON hash is unchanged at the end).
  - User acceptance of the look.
- Risk: low
- Approval already granted (exact target only): user requests 2026-09-25 and "her şeyi sen yap" for `LevelDatabaseWindow.cs`
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260924-224138113-766ebc15-level-editor-compact-actions, then an independent verifier

## Stop conditions

- The window changed in the same lines since it was read: re-read and reapply only this change.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: none
- Contract fingerprint: sha256:4e863956156e24aa3940a7a62a54de59d565b75ea3c0b822bafca357d19ed7dd
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 0
- Last feedback: none
- Blocker: none
- Next action: none
- Closed revision: none
- Closed at: 2026-09-24T23:09Z
- Closure mode: superseded
- Closed by: task-done --superseded-by (user decision: the two-pane redesign absorbs these actions)
- Superseded by: TASK-20260924-230915885-717889da-level-editor-shell

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Compact layout | UNPROVEN | none | none |
| AC-2 Refresh reloads with prompt | UNPROVEN | none | none |
| AC-3 Add and delete icons work | UNPROVEN | none | none |
| AC-4 Enable rules kept | UNPROVEN | none | none |
| AC-5 Other controls unchanged | UNPROVEN | none | none |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's two window-layout screenshots
- 2026-09-24T23:09Z — closed (superseded) — the "+"/"×" buttons were added by another live session; the Refresh/Restore icons and Save-alone move into TASK-20260924-230915885-717889da-level-editor-shell
<!-- TASK-LIFECYCLE:END -->
