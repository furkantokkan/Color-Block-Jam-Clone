# TASK-20260925-172236747-9ac18e21-level-editor-draft-survives-reload — Level Editor: unsaved level edits survive a script or domain reload

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260925-172236747-9ac18e21-level-editor-draft-survives-reload
- Supersedes: none
- Title: Level Editor: unsaved level edits survive a script or domain reload
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:7
- Task kind: bug
- Area: Level authoring (Level Editor window draft lifetime across a script or domain reload)
- Workflow: direct

## Objective and baseline

- Observable objective: A script or domain reload no longer throws away the Level Editor's unsaved level edits; the window shows the same unsaved draft after the reload.
- Current behavior/evidence:
  - `LevelDatabaseWindow.OnDisable` destroys the draft and logs "Level Editor: unsaved level changes were discarded by an editor reload." After the reload, `CreateGUI` rebuilds the draft from the JSON source, so every unsaved edit is gone.
  - `docs/Level-Editor-Design.md` ("As built", 2026-09-25) records it: "A script reload with unsaved edits logs a warning, because Unity asks Save/Discard/Cancel only on close."
  - Many agents compile the shared project, so the user loses work again and again. A verifier also found that the Piece Editor's Save as template imports the shape enum and reloads the domain, which drops unsaved level edits that hold freshly placed `Custom_` blocks.
  - The user asked for bugs like this to be fixed in extra tasks: "böyle buglar oluyor bazen bunları da düzelt ek task açıp" (2026-09-25).
- Expected behavior:
  - Just before a reload (`OnDisable` while `hasUnsavedChanges` is set), the window keeps the draft in `UnityEditor.SessionState`, keyed by the window's instance ID. SessionState survives a domain reload but not an Editor restart. The kept record holds the full `LevelData` JSON of the draft, the active database GUID, the selected level index, the saved entry's level id, a hash of the JSON source text the draft is based on, and the saved-baseline JSON of the dirty check.
  - After the reload, the first draft load restores the record once and removes it, when the same database GUID, level index and level id are selected and the JSON source text still has the same hash. The window then shows the restored draft. `hasUnsavedChanges`, the title's dirty marker, the "Unsaved level changes" status, the preview publish and the undo target all use it. New edits are undoable; undo history from before the reload is not kept, because every draft rebuild clears it (DEC-119).
  - When the database, the level or the source text changed, the kept draft is discarded as it is today. The status line and a console warning name the reason.
  - The restored draft is never written to JSON; only Save Level writes it.
  - The reload log line says the draft was kept, instead of discarded.
  - Closing the window keeps Unity's Save/Discard/Cancel prompt. Save and Discard clear `hasUnsavedChanges` before `OnDisable`, so a closed window keeps nothing.
  - A pending `Custom_` relabel of the open level (`LevelPieceSession.PendingRelabelLevelId`) runs on the restored draft.
- Behavior that must remain unchanged:
  - The Save/Discard/Cancel prompt on close, and the Save/Discard/Cancel dialog on level, database, Reload and Restore switches.
  - Save Level keeps the draft and its undo history; Reload, Restore, level switch and database switch rebuild the draft and clear its undo (DEC-119).
  - A reload without unsaved edits rebuilds the draft from JSON with the "Editing …" status (`LevelEditorShellTests.NameAndTimerShowTheDraftAfterTheGuiIsRebuilt`).
  - Level JSON is written only by Save Level and the other existing explicit commands.

## Ownership

- Owner/writer role: Level Editor draft-reload implementer (subagent of the Level Editor session), then an independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs (only `OnEnable`, `OnDisable`, `CreateGUI`, `LoadSelectedDraft` and new private helpers they call)
  - Assets/ColorBlockJam/Editor/Levels/LevelDraft.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelDraftReloadTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelDraftReloadTests.cs.meta
  - docs/Level-Editor-Design.md (the "As built" reload note only)
  - PROJECT.md (only the DEC-119 row sentence about reloads, if needed)
  - production/tasks/TASK-20260925-172236747-9ac18e21-level-editor-draft-survives-reload/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Editor/BoardKit/**
  - Assets/ColorBlockJam/Editor/LevelPreview/**
  - Any other method of Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs (held by the concurrent Level Editor agent)
  - Writing level JSON except through Save Level
  - Clicking in, or changing, the user's open windows and drafts in the shared Editor
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked: another agent of the same session is editing other parts of `LevelDatabaseWindow.cs` now (the "+" level-piece handlers, re-apply, re-stamp and relabel code, the Pen freehand stroke), under TASK-20260925-082521498-8e54a900-level-editor-custom-pieces and TASK-20260925-142546927-22e06638-level-editor-transform-tools. This task edits only the four named methods and adds new private helpers, re-reads the file before each edit, uses small exact replacements and keeps every saved state compiling. No open task lists `LevelDraft.cs` or `LevelDraftReloadTests.cs`.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given a loaded level with unsaved edits (Level Name, Timer and an added `Custom_` block), when the window runs `OnDisable`, `OnEnable` and `CreateGUI` as a domain reload does, then the draft's full `LevelData` JSON equals the one before, the fields show the edited values, `hasUnsavedChanges` is true, the status reads "Unsaved level changes…", the published preview level is the edited draft, and the JSON source file on disk is unchanged.
2. Given a restored draft, when the Timer is edited again and that step is undone, then the draft is back at the restored value and still unsaved; restoring and undoing never write the JSON.
3. Given a kept draft whose database GUID, level id or JSON source hash no longer matches, when the window reloads, then the saved level is loaded as today, `hasUnsavedChanges` is false, and the status line and a console warning name the reason.
4. Given a window whose edits were discarded (as the close prompt's Discard does), when it runs `OnDisable`, then nothing is kept and the rebuilt window shows the saved level.
5. Given the Level EditMode groups, then they all pass.
6. V-1: the user edits a level, triggers a script reload (for example a compile), and sees the unsaved edits and the dirty marker still in the Level Editor.

## References and constraints

- Story/GDD/ADR/design/log/image:
  - PROJECT.md DEC-119 (one undoable draft; every draft rebuild clears its undo).
  - docs/Level-Editor-Design.md, the "As built" note on script reloads.
  - The user's message of 2026-09-25: "böyle buglar oluyor bazen bunları da düzelt ek task açıp".
  - The verifier's finding that Save as template's enum import and domain reload drop unsaved `Custom_` edits (TASK-20260925-145158848-5858b722-piece-editor-level-link, D4-A relabel).
- Unity version/platform: 2022.3.62f2; Editor only
- Package/architecture constraints: editor-only C#; `UnityEditor.SessionState` and `JsonUtility` (the dirty check's own format); no new package, asmdef or serialized asset.
- Explicit out of scope:
  - Keeping undo history across a reload.
  - Keeping a draft across an Editor restart, a layout reload that recreates the window, or a window close.
  - Merging a kept draft into a JSON source that changed on disk.

## Verification and risk

- Focused test path/command: `Assets/ColorBlockJam/Tests/EditMode/LevelDraftReloadTests.cs`, then every `ColorBlockJam.Tests.EditMode.Level` group through MCP for Unity `run_tests`
- Required execution preflight: `unity-preflight`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: zero `error CS` after a script refresh
- EditMode: `LevelDraftReloadTests` and all Level groups pass
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence: V-1 user check in the Level Editor
- Risk: low
- Approval already granted (exact target only): none
- Commit/checkin constraint: forbidden
- Current invocation permission: user "böyle buglar oluyor bazen bunları da düzelt ek task açıp" (2026-09-25), dispatched by the Level Editor session
- Handoff destination: $implement-task TASK-20260925-172236747-9ac18e21-level-editor-draft-survives-reload, then an independent verifier

## Stop conditions

- The fix needs a change outside the four named window methods and their new helpers: stop and coordinate with the concurrent Level Editor agent.
- The restore would need to write level JSON: stop.
- The shared Editor is busy (compiling, importing or running tests): wait; never interrupt another session's run.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:9bffd042c3ef77f0369f92b8063f2f7a7423f5f77b1ea05b4eb4b3462b641aba
- Evidence fingerprint: sha256:178871c951f0621a042abefc5edb9df0f2e4dfd37e1f38d7c0a8a38f4c263ca4
- Reviewed revision: vcs:cs:7+owned-sha256:94b4c398f232ffe5252027cdf82afba253466cd3f4054a24912b2e9d6f73275d
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:cs:7+owned-sha256:94b4c398f232ffe5252027cdf82afba253466cd3f4054a24912b2e9d6f73275d
- Closed at: 2026-09-28T08:46:47Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Unsaved edits survive a reload with the full draft, dirty state, status and preview | PASS | automated: LevelDraftReloadTests (full draft incl. a Custom_ block, dirty flag, status, preview after a simulated reload); Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 463/463 pass; LevelDraftReloadTests 7/7; independent verifier: VERIFY: PASS (two low notes: a same-level pending relabel can overwrite the discard status line while the Console warning stays; a layout reload leaves an unused session copy until the Editor restarts) | vcs:cs:7+owned-sha256:94b4c398f232ffe5252027cdf82afba253466cd3f4054a24912b2e9d6f73275d |
| AC-2 Undo works on the restored draft and nothing writes JSON | PASS | automated: LevelDraftReloadTests (undo on the restored draft; JSON file untouched); Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 463/463 pass; LevelDraftReloadTests 7/7; independent verifier: VERIFY: PASS (two low notes: a same-level pending relabel can overwrite the discard status line while the Console warning stays; a layout reload leaves an unused session copy until the Editor restarts) | vcs:cs:7+owned-sha256:94b4c398f232ffe5252027cdf82afba253466cd3f4054a24912b2e9d6f73275d |
| AC-3 A changed database, level or source discards the kept draft and says why | PASS | automated: LevelDraftReloadTests (changed database, level or source discards with a status and Console reason, 3 cases); Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 463/463 pass; LevelDraftReloadTests 7/7; independent verifier: VERIFY: PASS (two low notes: a same-level pending relabel can overwrite the discard status line while the Console warning stays; a layout reload leaves an unused session copy until the Editor restarts) | vcs:cs:7+owned-sha256:94b4c398f232ffe5252027cdf82afba253466cd3f4054a24912b2e9d6f73275d |
| AC-4 A discarded close keeps nothing | PASS | automated: LevelDraftReloadTests (Discard and a clean window keep nothing); Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 463/463 pass; LevelDraftReloadTests 7/7; independent verifier: VERIFY: PASS (two low notes: a same-level pending relabel can overwrite the discard status line while the Console warning stays; a layout reload leaves an unused session copy until the Editor restarts) | vcs:cs:7+owned-sha256:94b4c398f232ffe5252027cdf82afba253466cd3f4054a24912b2e9d6f73275d |
| AC-5 Level EditMode groups pass | PASS | automated: every Level group; Level EditMode groups 2026-09-25 (live Editor, MCP HTTP): 463/463 pass; LevelDraftReloadTests 7/7; independent verifier: VERIFY: PASS (two low notes: a same-level pending relabel can overwrite the discard status line while the Console warning stays; a layout reload leaves an unused session copy until the Editor restarts) | vcs:cs:7+owned-sha256:94b4c398f232ffe5252027cdf82afba253466cd3f4054a24912b2e9d6f73275d |
| V-1 User check in the Level Editor | UNPROVEN | manual: the user edits a level, a compile runs, and the edits and the asterisk are still there; pending | vcs:cs:7+owned-sha256:94b4c398f232ffe5252027cdf82afba253466cd3f4054a24912b2e9d6f73275d |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored from the user's "böyle buglar oluyor bazen bunları da düzelt ek task açıp" (2026-09-25) and the verifier's Save as template finding
- Started — implementation — 2026-09-25T17:24Z: direct implement-task run dispatched by the Level Editor session
- Verification — 2026-09-25T17:49Z: implemented by an agent; 463/463; independent verifier VERIFY: PASS; V-1 awaits the user
- Updated — verification — 2026-09-26: TASK-20260926-140520462-2196c1d1-level-editor-pen-edit-templates added `LevelDraft.m_templateEdits` (additive), carried by `KeepDraftForReload`/`RestoreDraftAfterReload` under the same database, level and source match and dropped on the discard paths, with the new case `APendingTemplateFootprintSurvivesAReloadWithItsDraft` in `LevelDraftReloadTests`; that task's run: Level EditMode 592/592. The reviewed revision is stale because owned files changed; re-stamp at closure; V-1 unchanged.
- 2026-09-28T08:46:47Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
