# TASK-20260927-220047618-95397edf-level-editor-validate-level — Validate Level button and a bounded solvability check before Save

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260927-220047618-95397edf-level-editor-validate-level
- Supersedes: none
- Title: Validate Level button and a bounded solvability check before Save
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:14 plus other sessions' uncommitted work of 2026-09-27/28 (planning provenance only)
- Task kind: feature
- Area: Level Editor validation (new editor-only `ColorBlockJam.Editor.Validation` assembly, `LevelDatabaseWindow` bottom bar and Save flow)
- Workflow: trio (subagent implementer, then independent reviewers/verifier, then bugfix if needed; the orchestrating session writes no production code, on the user's order)

## Objective and baseline

- Observable objective: The Level Editor has a "Validate Level" button beside Save Level that reports whether the open level can be solved without boosters, and Save Level warns before it writes a level that is proven unsolvable or could not be confirmed.
- Current behavior/evidence:
  - Case brief optional item (`docs/Game-Developer-Case-2026.md:101`): "Level validation. The editor gives a warning before it saves an unsolvable level." PRD §7.9 (REQ-O2, DEC-68/69/70, `docs/Color-Block-Jam-PRD.md:462-532`) designs a three-stage pipeline.
  - Only stage 1 exists: `LevelData.Validate` (`Runtime/Levels/LevelData.cs:237`) through `LevelSaveController.Validate` (`Editor/Levels/LevelSaveController.cs:85`) and `LevelDraftIssues`, which disable Save Level while an error exists. No static pre-check, solver, state key or solvability message exists anywhere (only the doc comments `Domain/Exit/ExitRules.cs:11` and `Domain/Board/IBoardReader.cs:4` mention a "future editor solver"). PRD "appendix section C" (`:483`) does not exist, so this contract defines the state key and reductions.
  - The bottom bar (`Editor/Levels/LevelDatabaseWindow.cs` `CreateBottomBar` ~:1210) holds Save Level (index 0), Test Level (index 1) and the status label (index 2), all built in C# with `CreateBottomButton` (30 px high, min 120 px, 13 px text). `LevelTestPlayTests.SaveLevelAndTestLevelAreLargerButtonsSideBySideAtTheBottomLeft` (`Tests/EditMode/LevelTestPlayTests.cs:181`) pins that order.
  - `SaveLevel()` (~:4158) runs `m_saveController.Validate`, `RefreshShapeCatalog`, `Prepare`, `ConfirmTemplatePlan`, then `Save`. `TestLevel()` saves unsaved changes first through `SaveLevel()`; the `SaveChanges()` override (~:442) also calls it. All existing confirmations are IMGUI `EditorUtility.DisplayDialog(Complex)` behind `Func` seams that tests replace.
  - The Domain rules are public and read-only: `MoveRules.CanOccupy(IBoardReader, AttemptHooks, int, Cell)`, `ExitRules.Evaluate(IBoardReader, AttemptHooks, int, Cell, Side, out ExitCandidate)`, `LevelModel.TryCreate(...)`, `BlockDefinition`, `DoorDefinition`, `BoardState(LevelModel)`, `AttemptHooks.CreateEmpty()`, `AttemptHooksBuilder`, `IceRules(int[])`, `BoardSimulation(LevelModel, AttemptHooks, DragSettings)` and `CommandExecutor`. `BoardState` mutators are internal and Domain grants `InternalsVisibleTo` only to the Gameplay test assemblies. `ColorBlockJam.Editor` references neither Domain nor Gameplay.
  - Only Ice (`k_iceRuntimeImplemented = true`) has gameplay rules; Arrow, Barrier and every other registered effect are `IsRuntimeImplemented == false` (`Runtime/Levels/LevelEffectRegistry.cs:14-55`). Save refuses barrier levels (barrier save gate) but saves Arrow and other unimplemented effects.
  - The five shipped levels (`Data/Levels/LevelDatabase.json`) are 5x5 or 5x6 boards with 2 to 5 blocks; level-01 has one Ice block (`clearsToThaw` 1).
- Expected behavior:
  - **Button.** The bottom bar order becomes Save Level (index 0), **Validate Level** (index 1), Test Level (index 2), status label (index 3). Validate Level is built with `CreateBottomButton` (same height, width rule and font) and has the tooltip "Check whether this level can be solved without boosters." It is enabled exactly when Save Level's source/selection/no-error conditions hold and the Editor is not playing or entering Play Mode; its blocked tooltips say to fix the errors in Issues first, or that it is unavailable in Play Mode. It never saves, never changes the draft, the Undo stack or the dirty state.
  - **Result text (status label, designer language: colour, shape and cell; no ids or type names):**
    - Solvable: `Solvable in {N} moves.` N counts player moves in the found solution: consecutive steps of the same block merge into one move (DEC-122). The search is shortest in one-cell steps, not necessarily in moves; the design doc says so.
    - Proven unsolvable: `Cannot be solved: {reason}.` Reasons: a PRE-1 or PRE-2 finding naming the block (colour, shape, cell) and the numbers involved, or `no sequence of moves clears every block` after an exhausted search.
    - Unknown: `Not confirmed: the check stopped after {limit}.` where the limit is the one hit first (`250,000 positions` or `3 seconds`).
    - Not checked: `Not checked: {reason}.` Reasons: the level uses a mechanic without solver support (named in designer words, for example "Arrow blocks" or "barriers"), more than 16 blocks, the check was cancelled, or Play Mode.
  - **Pipeline** (one plain C# check shared by the button and Save):
    1. Stage 1 is the existing structural validation; the button is disabled while it fails and Save keeps refusing as today.
    2. Support gate: any block, door or cell-object effect without solver support gives Not checked with no search (DEC-MECH-12: never "Solvable" for rules it ignores). Solver support lives in a table in the Validation assembly keyed by effect type id; today only `ice` is supported. More than 16 blocks gives Not checked (PRD hard limits; the board is at most 8x10 by `LevelData`).
    3. Stage 2 static pre-checks, each Proven unsolvable with a named reason and no search: PRE-1, a block has no door of its colour that it fits through on an otherwise empty board (the message names the colour and the gate width the block needs); PRE-2, an Ice block's count is greater than the number of other blocks.
    4. Stage 3 breadth-first search over one-cell slides and exits with a deterministic move order (block index ascending; +X, -X, +Y, -Y; then exit sides in `Side` order) and a budget of 250,000 visited positions and 3.0 s (constants in one place; no UI override yet). Proven unsolvable only when the search is exhausted; a budget hit is always Unknown.
  - **Rules are not re-implemented (AR-10).** A solver-side builder turns `LevelData` into a Domain `LevelModel` (floor from void cells, doors and block colours through `BoardLayout.TryParseDoorColor`, block cells from the level's authoritative cells). For each block and each origin, the solver asks the Domain predicates once on a board that holds only that block: `MoveRules.CanOccupy` for standing validity and `ExitRules.Evaluate` for each side. It caches the answers as cell bit masks (the block's footprint mask per origin, and for each fitting exit the mask of swept cells that must be empty, obtained by probing the Domain predicate, not by re-deriving the sweep). The search core combines those masks with the occupancy of the other blocks and never branches on an effect type; Ice plugs in through one solver hook (a frozen block cannot start a move or exit while `max(0, clearsToThaw - otherExits) > 0`).
  - **State key:** the origin cell index of every on-board block plus one exited bit per block (at most 16 x 7 + 16 = 128 bits). Ice needs no extra state because its count is a function of the exited set. Exits are exit-monotone for the supported rules (they only free cells and lower Ice counts), so an available exit is taken eagerly; a test proves the eager search and plain breadth-first search agree.
  - **Save Level.** After stage 1 and the barrier gate pass and before `Prepare` or any write, Save runs the check (or reuses the last Validate Level result when the draft has not changed since; any draft change, undo/redo, level switch or JSON switch invalidates it).
    - Solvable or Not checked: no dialog; Save continues as today (Not checked leaves its reason in the status label after the save message).
    - Proven unsolvable or Unknown: a confirmation behind a new `Func` seam, `EditorUtility.DisplayDialogComplex` with the title "Level Cannot Be Solved" or "Level Not Confirmed", the reason, and the buttons "Cancel" (default, Enter), "Keep Editing" (Escape) and "Save Anyway". Only "Save Anyway" continues. Cancel or Keep Editing writes nothing, keeps the level dirty and leaves the status `Save cancelled: {result text}`.
    - Test Level and the `SaveChanges()` override reach the same path through `SaveLevel()`, so an unsaved unsolvable level asks before test play and a declined save starts nothing. Test Level of an unchanged, already saved level runs no check (as today it saves nothing).
  - **Blocking and cancel.** The check runs on the main thread, bounded by the budget. When it runs longer than 0.25 s it shows `EditorUtility.DisplayCancelableProgressBar` (behind a seam; tests use a silent one) with the position count, updated at most every 100 ms, and always clears it. Cancelling gives Not checked ("the check was cancelled"); during Save it cancels the save. The check refuses to start in Play Mode.
- Behavior that must remain unchanged:
  - Stage-1 validation, the barrier save gate, template/piece save plans, Test Level's save-first rule and Play Mode handling, the `SaveChanges()` prompt, Undo, dirty tracking and the draft preview publish.
  - Save Level stays at the bottom-left (index 0) with its size; Test Level keeps its size, tooltip and behaviour.
  - Level JSON format (`formatVersion` 2); no `validation` record is written; runtime loading, Domain rules and every runtime assembly are untouched; the Validation assembly is Editor-only and never enters a player build.

## Ownership

- Owner/writer role: implement-task through subagents (one implementer writes at a time; reviewers and the verifier are read-only)
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Editor/Levels/Validation/**
  - Assets/ColorBlockJam/Editor/Levels/Validation.meta
  - Assets/ColorBlockJam/Editor/ColorBlockJam.Editor.asmdef (one added reference to `ColorBlockJam.Editor.Validation` only)
  - Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs (bottom-bar button and order, `UpdateSaveButton` enable/tooltips, the `SaveLevel` insertion point, result invalidation in `OnDraftChanged`/level switch, the dialog and progress seams; nothing else)
  - Assets/ColorBlockJam/Editor/Levels/LevelValidationFlow.cs (new window-side controller, if the implementer keeps orchestration out of the window)
  - Assets/ColorBlockJam/Editor/Levels/LevelValidationFlow.cs.meta
  - Assets/ColorBlockJam/Tests/Validation/**
  - Assets/ColorBlockJam/Tests/Validation.meta
  - Assets/ColorBlockJam/Tests/EditMode/LevelValidateButtonTests.cs
  - Assets/ColorBlockJam/Tests/EditMode/LevelValidateButtonTests.cs.meta
  - Assets/ColorBlockJam/Tests/EditMode/LevelTestPlayTests.cs (the bottom-bar order assertions only)
  - Assets/ColorBlockJam/Tests/EditMode/ColorBlockJam.Editor.EditModeTests.asmdef (one added reference to `ColorBlockJam.Editor.Validation` only, if the window tests need its types)
  - docs/Level-Editor-Design.md (bottom-bar layout and description, the control mapping table, a new "Validate Level" section, and the deferred-list line that names the solver panel)
  - PROJECT.md (one decision-log row, DEC-143 or the next free number re-read right before writing)
  - production/tasks/TASK-20260927-220047618-95397edf-level-editor-validate-level/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Domain/** (read-only use of its public API; no `InternalsVisibleTo` change)
  - Assets/ColorBlockJam/Gameplay/**
  - Assets/ColorBlockJam/Runtime/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Tests/Gameplay/** (live-locked by the booster task)
  - Assets/ColorBlockJam/Editor/Levels/Canvas/LevelDraftIssues.cs (the Arrow task owns the Arrow PRE-1 issue)
  - Assets/ColorBlockJam/Editor/Levels/LevelSaveController.cs
  - docs/Color-Block-Jam-PRD.md
  - Any other existing test file, and any change to another asserted outcome
  - Any change to the level JSON format, the level reader/writer or the shipped level files
  - Plastic checkin, shelve, branch, or undo of other sessions' pending changes (for example the unowned edits in `LevelCanvasElement.cs`, `LevelEffectEditorWindow.cs` and `docs/Level-Editor-Design.md` line ~382)
  - Changing the user's open Level Editor draft or level JSON; `Undo.RecordObject` on shared Editor objects during verification
- Serialized assets: two new Assembly Definition files and their `.meta` (Unity text assets, no scene/prefab/ScriptableObject):
  - Asset: Assets/ColorBlockJam/Editor/Levels/Validation/ColorBlockJam.Editor.Validation.asmdef
    Sole writer: implement-task implementer subagent
    Matching `.meta` owner: implement-task implementer subagent
  - Asset: Assets/ColorBlockJam/Tests/Validation/EditMode/ColorBlockJam.Editor.Validation.EditModeTests.asmdef
    Sole writer: implement-task implementer subagent
    Matching `.meta` owner: implement-task implementer subagent
  - The one-line reference edits to `Editor/ColorBlockJam.Editor.asmdef` and `Tests/EditMode/ColorBlockJam.Editor.EditModeTests.asmdef` are listed above; nothing else in either file changes.
- Other active writers/worktrees checked (2026-09-27T22:20Z): live lifecycle locks under `Library/ccgs/task-lifecycle-locks/` are 54655902 booster-gameplay-flow (rewrites Domain internals in an isolated copy; adds `IBoardReader.GetFootprint`), 9383f38c held-outline, c1e67965 environment-runs-gate-motion, 78356740 / 43e35c51 / 0e93d47c / f5013490 (Hub session). None holds `Editor/Levels/**`, `Tests/EditMode/**`, `Tests/Validation/**` or `docs/Level-Editor-Design.md`. About fifteen Level Editor contracts in verification list `LevelDatabaseWindow.cs` without a lock. a3ffdc40 arrow-block-gameplay (paused, WAITING_FOR_OWNER) plans the Arrow PRE-1 issue in `LevelDraftIssues`; this task leaves that file alone and reports Arrow levels as Not checked. `PROJECT.md` is shared with the live locks: append one row only, after a fresh read.
- Execution dependency: none. The solver uses only public Domain API whose signatures are the same in the booster task's isolated copy (the copy adds `IBoardReader.GetFootprint`, which this task never implements). Revalidate those signatures before the first write and before final verification (stop condition).
- Queue policy: none

## Acceptance scenarios

1. AC-1: Given the Level Editor with a level selected, when the window is built, then the bottom bar reads Save Level, Validate Level, Test Level, status (indices 0 to 3), Validate Level has the same height and laid-out width rule as the other two buttons, and it is disabled with the matching tooltip while Issues holds an error or the Editor is playing.
2. AC-2: Given each of the five shipped levels, when Validate Level runs, then the status reads `Solvable in {N} moves.`, the draft, Undo stack, dirty state and level file are unchanged, and the reported solution replays through `BoardSimulation.Commands` with the level's Ice hooks: every command is Applied and the board ends cleared.
3. AC-3: Given a level with a block that has no door of its colour, or none it fits through, when the check runs, then it reports `Cannot be solved:` naming the block's colour, shape and cell and the gate width it needs, without searching. Given an Ice block whose count is greater than the number of other blocks, then it reports `Cannot be solved:` with both numbers, without searching.
4. AC-4: Given a level that passes both pre-checks but deadlocks (for example two blocks that block each other's only exits, or two Ice blocks that each wait for the other), when the check runs, then the search is exhausted and it reports `Cannot be solved: no sequence of moves clears every block.`
5. AC-5: Given a solvable level and a budget too small to finish, when the check runs, then it reports `Not confirmed:` naming the limit hit first, never `Cannot be solved`.
6. AC-6: Given a level with an effect that has no solver support (for example Arrow), or more than 16 blocks, when the check runs, then it reports `Not checked:` with that reason and does not search. An EditMode completeness test fails if any effect definition with `IsRuntimeImplemented == true` lacks solver support.
7. AC-7: Given an unsaved draft that is proven unsolvable or not confirmed, when the designer presses Save Level (or Test Level, or the window's save-on-close), then the warning dialog seam is called with the matching title and reason. Cancel or Keep Editing writes nothing (the file is byte-identical), the level stays dirty and Test Level starts nothing. Save Anyway saves exactly as today. A solvable or not-checked draft saves with no dialog.
8. AC-8: Given a Validate Level result, when the draft changes (edit, undo/redo, level or JSON switch), then Save does not reuse the stale result; given no change, Save reuses it and does not search again.
9. AC-9: Given the same level twice, when the check runs, then it returns the same status and the same move list. On the fixture set, the eager-exit search and plain breadth-first search agree on every status. A randomized parity test over reachable states agrees with `CommandExecutor` on which one-cell moves and exits are legal.
10. AC-10: Given the solver's `LevelData → LevelModel` builder, when it builds the shipped levels and the test fixtures, then its floor, doors and blocks equal those `AttemptLevelConverter` produces.
11. AC-11: Given a long check, when it runs past 0.25 s, then the progress seam shows the position count and Cancel stops it with `Not checked: the check was cancelled.`; during Save the save is cancelled. The progress bar is always cleared. The check refuses to start in Play Mode.
12. AC-12: Given the whole change, then the Validation assembly is Editor-only, references only `ColorBlockJam.Domain` and `ColorBlockJam.Runtime`, is referenced by `ColorBlockJam.Editor` without a cycle, no runtime assembly references it, the Level EditMode suite and the new Validation tests pass, and `docs/Level-Editor-Design.md` plus one PROJECT.md decision row describe the button, the pipeline, the texts and the deferrals.
13. AC-13: Given the live Level Editor, when the user clicks Validate Level and saves an unsolvable edit, then they accept the behaviour (explicit user acceptance).

## References and constraints

- Story/GDD/ADR/design/log/image: user order of 2026-09-28 ("task açalım ve save level butonunun yanına validate level button ekleyelim, taski oluşturduktan sonra implemente geç ve sen kod yazma subagentlara yazdır"); `docs/Game-Developer-Case-2026.md:97-111`; PRD §7.9 (`:462-532`), RULE-2/10/12-14/29/33/35, AR-10 (`:828`), §11.8 (`:884-887`), DEC-68/69/70 (`:982-984`), V-VAL-1..13; `docs/Color-Block-Jam-PRD-Mechanics-Catalog.md` (solver support per mechanic, DEC-MECH-12); `docs/Gameplay-Drag-And-Gate.md` (DEC-111/112/122 commands and exits); `docs/Level-Editor-Design.md` (§1 layout, bottom bar, deferred solver panel at ~:490); `docs/Level-Data-Format.md:295-298`.
- Unity version/platform: Unity 2022.3.62f2, Windows Editor only (the assembly never ships)
- Package/architecture constraints:
  - New `ColorBlockJam.Editor.Validation` assembly at `Editor/Levels/Validation/`, `includePlatforms: ["Editor"]`, `autoReferenced: false`, references `ColorBlockJam.Domain` and `ColorBlockJam.Runtime` only; `InternalsVisibleTo` its test assembly. Its public API exposes no Domain types to the window (result status, text, move count, solution steps as plain data).
  - New test assembly `ColorBlockJam.Editor.Validation.EditModeTests` at `Tests/Validation/EditMode/` (Editor-only, `overrideReferences` with nunit like the other test asmdefs) referencing Validation, Domain, Runtime and Gameplay (the converter-parity test only) and the test runners.
  - Plain C# in the search core; no Unity API outside the builder and the editor seams. No `System.Linq` (use ZLinq if a query is needed; ZString for formatted text), no `async void`, no static mutable state, no `Undo.RecordObject`. m_/s_/k_ naming, one-sentence class summaries, clear English names, simple verbs.
  - The window keeps its own conventions: IMGUI dialogs behind `Func` seams, `SetStatus` for messages, `CreateBottomButton` for buttons.
- Decisions taken by the decision owner under the user's order (record as the new DEC row):
  - D1: The check runs synchronously on the main thread with the state/time budget and a cancellable progress bar, instead of the PRD's worker thread (DEC-69), because Save, Test Level and save-on-close are synchronous today and the shipped boards are tiny. A worker thread with live progress stays a follow-up.
  - D2: The warning is an IMGUI `DisplayDialogComplex` behind a seam (Cancel default, Escape = Keep Editing, Save Anyway), matching every existing Level Editor confirmation, instead of a UI Toolkit modal; Enter and Escape both mean Cancel as the PRD asks.
  - D3: The solver builds its own `LevelModel` from `LevelData` (no Gameplay reference in the editor assembly) and an EditMode parity test pins it to `AttemptLevelConverter`.
  - D4: Solver support is a table in the Validation assembly (runtime registry untouched); only Ice is supported; everything else is Not checked until its rules and solver hook land.
  - D5: Moves are counted as DEC-122 player moves (merged runs of one block's steps) over a search that is shortest in one-cell steps.
- Explicit out of scope (deferred, named in the design doc): the level JSON `validation` record and content hash (the reader uses `MissingMemberHandling.Error`), solution and best-attempt replay on the grid, a status chip, per-level badges and "Check all levels", live pre-check warnings in Issues, the budget override UI, a guided heuristic, a worker thread, solver hooks for Arrow, Barrier and other unimplemented effects, README benchmark numbers, and any runtime or Domain change.

## Verification and risk

- Focused test path/command: UnityMCP `run_tests` EditMode on the assembly `ColorBlockJam.Editor.Validation.EditModeTests` and on `ColorBlockJam.Tests.EditMode.LevelValidateButtonTests` and `ColorBlockJam.Tests.EditMode.LevelTestPlayTests`, then the Level suite filter `ColorBlockJam[.]Tests[.]EditMode[.]Level`, once in the final stage (the shared Editor must be idle; queue behind other sessions' runs)
- Required execution preflight: `unity-cli` (pre-Unity-6 MCP identity gate; instance `ColorBlockJamCase2026@99077e4df1ecfaa4`, SHA1 of the Assets path, 2022.3.62f2)
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: the Roslyn compile check (`unity_compile_check.py --project <root> --files <changed files>`) after each change; the new asmdefs need Unity's own compile (`COMPILE_UNVERIFIED` falls back to `refresh_unity` + `read_console` with no new errors)
- EditMode: AC-1 to AC-12 as listed (shipped levels, PRE-1, PRE-2, exhausted deadlock, budget Unknown, unsupported/limit Not checked, completeness, determinism, eager-vs-plain agreement, randomized command parity, replay parity, converter parity, window order/enable/tooltips, Save dialog paths with byte-identical Cancel, stale-result invalidation, progress/cancel seam, Play Mode refusal)
- PlayMode: not applicable
- Manual/built-player/profiler/device evidence: the user's acceptance in the live Level Editor (AC-13); no build (the assembly is Editor-only)
- Risk: medium (a new assembly and assembly references; the Save flow gains a confirmation step)
- Approval already granted (exact target only): the user's order of 2026-09-28 to create this task and implement it, which covers this contract's plan, the two new editor-only asmdefs, the two one-line asmdef reference additions, the window edits listed above, the design-doc section and one PROJECT.md row
- Commit/checkin constraint: allowed-on-explicit-invocation
- Current invocation permission: none
- Handoff destination: `/implement-task TASK-20260927-220047618-95397edf-level-editor-validate-level`

## Contract history (conditions agreed after authoring)

- Revision 1, 2026-09-28T00:05Z, by the owning session (decision owner) after the review workflow wf_d164838d-c0a:
  - Save gate (deviation from AC-7, accepted): Save Level, Test Level and save-on-close run the check only in a window whose serialized `m_checksSolvabilityOnSave` flag `Open()` sets (menu Color Block Jam > Level Editor, or opening the LevelEditor scene). Windows the existing EditMode fixtures create keep today's saves, because about 750 existing Level tests save levels without a gate and the contract forbids changing their outcomes. A window restored from a layout and never reopened through `Open()` saves without the check until reopened once. Validate Level itself is never gated.
  - Level Database dropdown (window edit outside the listed insertion points, accepted): when the Unsaved Level prompt's Save is declined or fails during a JSON switch, the callback now resets only the dropdown (`SetValueWithoutNotify(previousValue)`) instead of reloading the saved level over the edits, so a declined warning keeps the edits as its message promises.
  - PRE-1 wording: a block with no gate of its colour also names the width it needs ("it needs one at least N cells wide").
  - The decision row is DEC-144 (DEC-143 was taken by TASK-20260927-215425472-f5013490).
  - `InternalsVisibleTo` of the Validation assembly names only its own test assembly.

## Stop conditions

- A Domain public signature this contract relies on (`LevelModel.TryCreate`, `BlockDefinition`, `DoorDefinition`, `BoardState(LevelModel)`, `MoveRules.CanOccupy`, `ExitRules.Evaluate`, `AttemptHooks.CreateEmpty`, `AttemptHooksBuilder`, `IceRules(int[])`, `BoardSimulation`, `CommandExecutor`, `GameCommand`) differs when implementation starts or before final verification (for example after the booster task's copy-back): revise the contract.
- Any shipped level reports anything other than Solvable: stop and report it as a level-content finding; do not change the level or weaken the check.
- Reusing Domain predicates would require an `InternalsVisibleTo`, a Domain edit or a Gameplay edit.
- A live lock appears on `Editor/Levels/LevelDatabaseWindow.cs`, `Tests/EditMode/**` or `docs/Level-Editor-Design.md`.
- The user rejects a recorded decision D1 to D5.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:554b8f8ab1261a080ad5619368d4bb3c0aed196f721444a3465a670cc9c259f1
- Evidence fingerprint: sha256:825e4d35a11c342310cbbaa387e58f17228f2b5a3486d575d2c4995008124f14
- Reviewed revision: vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b
- Attempt count: 0
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b
- Closed at: 2026-09-28T08:46:47Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated: LevelTestPlayTests.SaveLevelAndTestLevelAreLargerButtonsSideBySideAtTheBottomLeft (order Save, Validate, Test, status) and LevelValidateButtonTests.ValidateLevelIsOnWhereSaveLevelIsOnAndOffInPlayMode (job 4efb3f38, 26/26) | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b |
| AC-2 | PASS | automated: ShippedLevelSolvabilityTests (five shipped levels Solvable in 2/2/3/5/8 moves; one-cell and player-move replays all Applied and cleared; level unchanged; job 3271a9f8, 32/32) and LevelValidateButtonTests.ValidateLevelShowsTheResultAndChangesNothing (job 4efb3f38) | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b |
| AC-3 | PASS | automated: LevelPreCheckTests no gate of its colour (with width), wider than every gate (width), Ice count above the other blocks (both numbers), no search (job 3271a9f8) | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b |
| AC-4 | PASS | automated: LevelSearchTests.TwoBlocksBlockingEachOthersOnlyExitCannotBeSolvedAfterAnExhaustedSearch and TwoIceBlocksWaitingOnEachOtherCannotBeSolvedAfterAnExhaustedSearch (job 3271a9f8) | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b |
| AC-5 | PASS | automated: LevelSearchTests position and time budget tests name the limit hit first (job 3271a9f8) | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b |
| AC-6 | PASS | automated: LevelSupportGateTests Arrow, barrier and 17 blocks are Not checked without a search; EveryRuntimeImplementedEffectHasSolverSupport (job 3271a9f8) | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b |
| AC-7 | PASS | automated: LevelValidateButtonTests CancelOrKeepEditingWritesNothingAndKeepsTheEdit (Save, Test Level, SaveChanges; byte-identical file, still dirty, no test play), SaveAnywaySavesAsBefore, ASolvableLevelSavesWithoutAQuestion, ALevelTheCheckCannotConfirmAsksWithItsOwnTitle, ANotCheckedLevelSavesAndKeepsTheReasonAfterTheSaveMessage (job 4efb3f38); scope per contract revision 1 (windows flagged by Open) | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b |
| AC-8 | PASS | automated: LevelValidateButtonTests.SaveReusesAnUnchangedResultAndChecksAnEditedDraftAgain (job 4efb3f38) | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b |
| AC-9 | PASS | automated: LevelSearchTests determinism and eager-versus-plain agreement; LevelCommandParityTests.SolverLegalMovesMatchTheCommandExecutorAlongRandomWalks (job 3271a9f8) | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b |
| AC-10 | PASS | automated: LevelSolverModelBuilderTests.TheBuilderMatchesTheAttemptConverterOnShippedLevelsAndFixtures (job 3271a9f8) | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b |
| AC-11 | PASS | automated: LevelSearchTests progress report and cancel; LevelValidateButtonTests.CancelOnTheProgressBarCancelsTheSave and the Play Mode refusal (jobs 3271a9f8, 4efb3f38) | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b |
| AC-12 | PASS | automated: Unity refresh compiled with no CS error; Validation assembly 32/32 (job 3271a9f8); Level EditMode suite 757/757 (job 0a1a2c73); containment review of asmdefs (Editor-only, Domain and Runtime only, no cycle); design doc Validate Level section and PROJECT.md DEC-144 | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b |
| AC-13 | UNPROVEN | none | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:8562e282fbb859c1543820302e0b1c2f965fbc2e2ab86ec97f07430681abd50b |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — contract authored 2026-09-27T22:25Z by session "Rollic game developer case validator [772454]" from the user's order of 2026-09-28; evidence from workflow wf_2ebbd032-1f6 (five readers and a critic); ready
- Started — implementation — 2026-09-27T22:33Z: direct implement-task under the lifecycle lock (Library/ccgs/task-lifecycle-locks); baseline hashes in baseline.md; code is written by workflow subagents on the user's order
- Verification — 2026-09-28T00:10Z: workflow wf_d164838d-c0a (14 subagents) built the Validation assembly and the window integration; four review lenses with adversarial verification confirmed six findings, five fixed in code and one (the stale Unity compile) resolved by the Unity phase; contract revision 1 records the accepted deviations. Unity: compile clean; Validation 32/32 (job 3271a9f8), window tests 26/26 (job 4efb3f38), Level suite 757/757 (job 0a1a2c73). AC-1..AC-12 PASS; AC-13 waits for the user's acceptance in the live Level Editor
- 2026-09-28T08:46:47Z — Closed (user_confirmed) — closed — user order 2026-09-28 ("task dosyalarını serbest bırak dün geceye kadarkileri sadece hiç yapılmayanları açık bırak kilitsiz şekilde"; cut-off 2026-09-28 03:00 chosen by the user). Every worked task up to that cut-off is closed; unmet, unverified or pending criteria, blockers and any defect rows are preserved as recorded, not verified. Latest automated evidence for reference only: combined batch run of 2026-09-28 (isolated copy, 11:14 local snapshot sha256:A5BE49CC…B366 of cs:17 plus pending changes): EditMode 2103/2107 passed (failures: the parked remote-level-json settings test, the order-dependent ShapePrefabTests.JsonImportAndUndo… which passes alone, and a stale DEC-161 CurrencyCatalogTests assertion since fixed by its owner), PlayMode 247/250 passed with 0 failures and 3 skipped.
<!-- TASK-LIFECYCLE:END -->
