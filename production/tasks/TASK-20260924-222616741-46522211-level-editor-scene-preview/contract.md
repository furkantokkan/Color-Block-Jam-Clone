# TASK-20260924-222616741-46522211-level-editor-scene-preview — The LevelEditor scene shows the level selected in the Level Editor window

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260924-222616741-46522211-level-editor-scene-preview
- Supersedes: none
- Title: The LevelEditor scene shows the level selected in the Level Editor window
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:5
- Task kind: feature
- Area: Level authoring (LevelEditor scene preview)
- Workflow: direct

## Objective and baseline

- Observable objective: When the `LevelEditor` scene is open, the level selected or edited in the Level Editor window (`Color Block Jam/Level Editor`) appears in the Scene view as the game would build it: floor, walls, corners and doors, plus every block with its shape, colour and position.
- Current behavior/evidence:
  - User request 2026-09-25: "burada seçtiğimiz gibi editor scene de level yüklenmeli" ("the level we select here should load in the editor scene").
  - `Assets/ColorBlockJam/Scenes/LevelEditor.unity` contains one empty `LevelEditor` GameObject.
  - `LevelDatabaseWindow` keeps its selection private (`m_selectedIndex`, `LevelDraft`) and publishes nothing.
  - The play area is built by `EnvironmentView.Build(IBoardLayout)` (`ColorBlockJam.Environment`, `Prefabs/Environment/Environment.prefab`, `Data/Environment/EnvironmentDatabase.asset`).
  - Blocks come from `BlockPieceFactory` (`ColorBlockJam.Presentation`) with the derived shape prefabs.
  - The user assigned all of this work to this session ("her şeyi sen yap", 2026-09-25), including the window hook.
- Expected behavior:
  1. **Window hook.** `LevelDatabaseWindow` publishes a snapshot of the current draft level:
     - when a level is loaded or selected;
     - after draft field edits, deferred to the next editor tick;
     - after Save, Reload and Restore;
     - `null` when no level is selected or the database is invalid.
     The window gains no other behavior.
  2. **Preview owner.** An editor-only preview owner, in its own Editor assembly, listens only while the `LevelEditor` scene is loaded.
     - It keeps one host root in that scene, flagged `HideFlags.DontSave` on the root and every descendant. The host holds one `Environment.prefab` instance, a blocks root and a directional light.
     - It builds the play area with `EnvironmentView.Build(BoardLayout)`.
     - It creates blocks through `BlockPieceFactory`: shape from `ShapeId`, colour from `ColorId`, footprint cells in the game's cell coordinates under the board root.
     - It frames the Scene view when a different level is selected.
  3. **Invalid drafts.** A draft that does not validate (for example width 0 or a block off the board) keeps the last valid preview and reports the reason once in the window status or the Console, without exceptions.
  4. **Teardown.** The owner releases every pool and destroys its host, with no leaked objects, when:
     - the scene closes;
     - the domain reloads;
     - the Editor quits;
     - the window publishes `null`.
     Selecting levels repeatedly reuses the pools.
  5. **Opening the scene.** A menu command opens `LevelEditor.unity` (after the standard save-modified-scenes prompt) and shows the last published level.
- Behavior that must remain unchanged:
  - Every Level Editor window feature (database dropdown, level fields, block tools, Create/Delete/Save/Reload/Restore, unsaved-draft prompts).
  - Level JSON content, and the `LevelEditor.unity` file bytes (preview objects are never saved).
  - The LevelEditor Play Mode guard.
  - The Game scene and its `EnvironmentPreview`.
  - The runtime assemblies and all prefabs and data assets.

## Ownership

- Owner/writer role: environment/play-area session (this conversation), sequential independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs
  - Assets/ColorBlockJam/Editor/LevelPreview.meta
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Assets/ColorBlockJam/Tests/LevelPreview.meta
  - Assets/ColorBlockJam/Tests/LevelPreview/**
  - docs/Level-Editor-Scene-Preview.md
  - PROJECT.md
  - production/tasks/TASK-20260924-222616741-46522211-level-editor-scene-preview/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Presentation/**
  - Assets/ColorBlockJam/Environment/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Data/**
  - Assets/AddressableAssetsData/**
  - Assets/ColorBlockJam/Editor/ColorBlockJam.Editor.asmdef
  - Assets/ColorBlockJam/Editor/BoardKit/**
  - Saving any scene, or leaving preview objects without `DontSave`
  - Plastic checkin, shelve or publishing
- Serialized assets: none (new `.asmdef` files and scripts only; the scene is never written)
- Other active writers/worktrees checked: The level-pipeline session owns `Editor/Levels/**`. The user assigned the window hook to this session on 2026-09-25. The window was last written at 01:00 on 2026-09-25 by the base-shape migration; edits must re-read and change only the hook lines. The board-kit session owns `Presentation/Board` and the shape prefabs, which are consumed read-only.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given the `LevelEditor` scene is open, when level 1 is selected in the window, then the Scene view shows its 4x5 play area with its doors and its two blocks (a LightBlue Square at (2,2) and a Pink Square at (0,1)), framed.
2. Given the preview shows level 1, when level 3 is selected, then the preview shows level 3 and the pooled object count does not grow beyond the pools' needs.
3. Given a level is shown, when a draft field changes (width, a door, or a block move or type), then the preview updates on the next editor tick. An invalid draft keeps the last valid preview and reports the reason without an exception.
4. Given a preview is shown, when the scene is saved, then `LevelEditor.unity` is byte-identical to before, and every preview object carries `DontSave`.
5. Given a preview is shown, when the scene closes, a domain reload runs, or the window publishes `null`, then no preview object or pooled clone remains in any loaded scene.
6. Given the Game scene is open instead, when a level is selected, then nothing is created in the Game scene.
7. Given the mapping from `LevelData` to block spawn data, when EditMode tests run, then shape, colour, cells and position match the level, and an unknown shape or colour is rejected with the block ID.

## References and constraints

- Story/GDD/ADR/design/log/image: user messages 2026-09-25 (Level Editor screenshot, "her şeyi sen yap"); PROJECT.md "Scenes and Onity Scopes" (LevelEditor authoring-only), Performance Rule 3 (pools), DEC-100; `docs/Environment-Layout.md`.
- Unity version/platform: 2022.3.62f2; Editor only
- Package/architecture constraints:
  - No new package.
  - UI Toolkit window.
  - The preview uses the existing Environment, Presentation and Runtime APIs unchanged.
  - Editor-only assembly, never in player builds.
  - The owner and stop point of every subscription are named in the code.
  - No `Update` or tick loop: rebuilds happen only on window events.
- Explicit out of scope:
  - Painting void cells or effects.
  - Editing blocks in the Scene view.
  - Game-scene level selection.
  - Visual gameplay (movement, exits).
  - Remote levels.

## Verification and risk

- Focused test path/command: `Assets/ColorBlockJam/Tests/LevelPreview/**` EditMode tests
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile and Console after the change and after a domain reload
- EditMode: acceptance 7, plus the host lifecycle (build → rebuild → release leaves no objects) where it can run without the scene file
- PlayMode: not applicable (LevelEditor is edit-only)
- Manual/built-player/profiler/device evidence:
  - Acceptance 1–6 in the live Editor (MCP HTTP), with a Scene-view capture of level 1 and the file hash of `LevelEditor.unity` before and after a save.
  - User acceptance of the look.
- Risk: medium (a new editor assembly, and a cross-system event from the window)
- Approval already granted (exact target only): user order 2026-09-25 "her şeyi sen yap": the window hook, a new editor assembly and the preview
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: /implement-task TASK-20260924-222616741-46522211-level-editor-scene-preview, then an independent verifier

## Stop conditions

- The preview needs a change in the Environment, Presentation or Runtime code: stop and record the needed API.
- The window changed since it was read: re-read, then edit only the hook lines.
- Any path would write `LevelEditor.unity`, a prefab or a data asset.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: TASK_CLOSED
- Contract fingerprint: sha256:5d230182b1dfec5d9e6c38fcc5ee6abf0478225074bea01435358e51cf8926c7
- Evidence fingerprint: sha256:b2aacb47c9663daea619829d70bdc72087b2fe555e73dbc4a0f89ea40dc8e536
- Reviewed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b4f5a06f9f4d6784d16286f96f6c93fc3d00cf4f9eea1535e3401558c54cd4bc
- Attempt count: 2
- Last feedback: verifier AC-3 FAIL (D-001), fixed and re-verified live
- Blocker: none
- Next action: none
- Closed revision: vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b4f5a06f9f4d6784d16286f96f6c93fc3d00cf4f9eea1535e3401558c54cd4bc
- Closed at: 2026-09-24T23:29Z
- Closure mode: automatic
- Closed by: task-cycle (D-001 VERIFIED; all criteria PASS)
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Level 1 appears in the LevelEditor scene | PASS | manual: live window-driven publish, hierarchy + capture level1-preview.png (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b4f5a06f9f4d6784d16286f96f6c93fc3d00cf4f9eea1535e3401558c54cd4bc |
| AC-2 Selection change rebuilds with pooled objects | PASS | manual: live level cycling, object count stable (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b4f5a06f9f4d6784d16286f96f6c93fc3d00cf4f9eea1535e3401558c54cd4bc |
| AC-3 Draft edits update; invalid drafts are safe | PASS | manual: live Width field edit 6x5/4x5 after D-001 fix; width-0 keeps last preview with one warning (defects/D-001.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b4f5a06f9f4d6784d16286f96f6c93fc3d00cf4f9eea1535e3401558c54cd4bc |
| AC-4 Scene file never contains preview objects | PASS | automated/manual: SaveScene saveAsCopy byte-identical, 0 objects without DontSave (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b4f5a06f9f4d6784d16286f96f6c93fc3d00cf4f9eea1535e3401558c54cd4bc |
| AC-5 Teardown leaves nothing behind | PASS | manual: null publish, CloseScene, domain reloads (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b4f5a06f9f4d6784d16286f96f6c93fc3d00cf4f9eea1535e3401558c54cd4bc |
| AC-6 Other scenes are untouched | PASS | manual: Game-only publish created nothing (evidence.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b4f5a06f9f4d6784d16286f96f6c93fc3d00cf4f9eea1535e3401558c54cd4bc |
| AC-7 Level-to-spawn mapping tested | PASS | automated: LevelPreview EditMode 9/9 job 6b9e90a9 | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b4f5a06f9f4d6784d16286f96f6c93fc3d00cf4f9eea1535e3401558c54cd4bc |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| D-001 | level-preview/draft-publish/plain-bound-fields | VERIFIED | AC_FAILURE | AC-3 | Width/Height/Timer/Name edits did not republish (only SerializedPropertyChangeEvent was hooked) | [defects/D-001.md](defects/D-001.md) | vcs:plastic:cs:5@ColorBlockJamCase2026@efurk64@unity+owned-sha256:b4f5a06f9f4d6784d16286f96f6c93fc3d00cf4f9eea1535e3401558c54cd4bc |

### Transition history

- Created — planning — contract authored from the user's LevelEditor scene request; the user assigned the window hook to this session
- 2026-09-24T23:29Z — in_progress — workflow: implement, 3 reviewers (5 findings, all fixed), verifier
- 2026-09-24T23:29Z — D-001 OPEN → VERIFIED — plain bound field edits now republish via TrackSerializedObjectValue; live and test evidence
- 2026-09-24T23:29Z — TASK_CLOSED — AC-1..AC-7 PASS at the reviewed revision
<!-- TASK-LIFECYCLE:END -->
