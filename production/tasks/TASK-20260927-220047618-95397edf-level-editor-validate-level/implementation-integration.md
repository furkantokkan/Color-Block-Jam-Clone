# Implementation handoff: window integration (implementer 2 of 2)

Written 2026-09-28. Scope: the Level Editor window wiring, its tests, the design doc section and the PROJECT.md row.
The Validation assembly (implementer 1) was used as delivered; no file under `Editor/Levels/Validation/` or
`Tests/Validation/` was changed.

## Files

| File | Change |
| --- | --- |
| `Assets/ColorBlockJam/Editor/ColorBlockJam.Editor.asmdef` | One added reference: `"ColorBlockJam.Editor.Validation"` |
| `Assets/ColorBlockJam/Editor/Levels/LevelValidationFlow.cs` (+ `.meta`, GUID `eb06451eb8124eb09633135845d95776`) | New. `LevelValidationFlow` (run/reuse/refuse, progress adapter, warning text) and the `LevelValidationOutcome` struct |
| `Assets/ColorBlockJam/Editor/Levels/LevelDatabaseWindow.cs` | Button, order, enable/tooltips, `SaveLevel` insertion, `ValidateLevel`, `ConfirmSolvability`, dialog seam, `Invalidate` in `OnDraftChanged`, two comment updates |
| `Assets/ColorBlockJam/Tests/EditMode/ColorBlockJam.Editor.EditModeTests.asmdef` | One added reference: `"ColorBlockJam.Editor.Validation"` (the tests use `LevelCheckOptions`) |
| `Assets/ColorBlockJam/Tests/EditMode/LevelTestPlayTests.cs` | Only `SaveLevelAndTestLevelAreLargerButtonsSideBySideAtTheBottomLeft`: save 0, validate 1, test 2, status 3; the style loop covers the validate button; its laid-out height equals Save Level's |
| `Assets/ColorBlockJam/Tests/EditMode/LevelValidateButtonTests.cs` (+ `.meta`, GUID `a7b6b6c9ce234501b21ca5fec06f8abf`) | New, 10 tests |
| `docs/Level-Editor-Design.md` | §1 ASCII bottom bar, bottom-bar bullet, Test Level bullet wording, new "Validate Level" section (end of §2), §6 rows (Validate Level added, Test Level "right of Validate Level"), deferred-list line. Line ~382 (other session's gate-effects wording) untouched |
| `PROJECT.md` | One row appended at the end of the decision table: DEC-144 |

## Window changes (LevelDatabaseWindow.cs)

- Constants `k_validateTooltip` ("Check whether this level can be solved without boosters."), `k_validateBlockedTooltip`,
  `k_validatePlayingTooltip` (all `internal`).
- Fields `m_validateLevelButton`, `readonly LevelValidationFlow m_validationFlow`.
- Seams: `internal Func<string, string, bool> ConfirmUnsolvableSave` (title, message; default
  `DisplayDialogComplex(title, message, "Cancel", "Keep Editing", "Save Anyway") == 2`) and
  `internal LevelValidationFlow ValidationFlow` (exposes the flow's seams).
- `CreateBottomBar`: Save Level, `CreateBottomButton("validate-level", "Validate Level", k_validateTooltip, ValidateLevel)`, Test Level, status.
- `UpdateSaveButton`: validate enabled = Save's `canSave` and `!m_validationFlow.IsPlaying()`; tooltip Play Mode, then errors, then default.
- `OnDraftChanged`: first line `m_validationFlow.Invalidate()` (covers edits, undo/redo, level switch and JSON switch through `LoadSelectedDraft`, and the save's own refresh).
- `SaveLevel`: after `m_saveController.Validate` (stage 1 + barrier gate) and before `RefreshShapeCatalog`/`Prepare`/any write:
  `if (m_opensEffectEditor && !ConfirmSolvability(validation.Candidate, out checkNote)) return false;`. After a reloaded save a Not checked sentence is appended to the status.
- `ValidateLevel()`: `SetStatus(m_validationFlow.Check(GetDraftLevel()).Text)`; no draft, Undo or dirty change.
- `ConfirmSolvability`: Unsolvable/Unknown call the seam; a declined answer or a cancelled check sets `Save cancelled: {result text}` and returns false (nothing written, draft stays dirty, Test Level starts nothing, `SaveChanges()` keeps the window open as with any failed save).

## LevelValidationFlow (internal, ColorBlockJam.Editor)

- `Check(LevelData)`: Play Mode (`IsPlaying` seam, default `EditorApplication.isPlayingOrWillChangePlaymode`) returns
  `Not checked: the Editor is in Play Mode.` with no search. Otherwise it reuses the kept result when the revision and
  the level's `JsonUtility` JSON are unchanged, else runs `LevelSolvabilityCheck.Run(level, Options, progress)`.
  A cancelled result is not kept. `RunCount` counts real searches.
- `Invalidate()` bumps the revision.
- Progress: `ShowProgress(info, fraction) -> cancel` (default `EditorUtility.DisplayCancelableProgressBar("Validate Level", ...)`),
  `ClearProgress` (default `EditorUtility.ClearProgressBar`, called in `finally` after every run), `ProgressDelaySeconds`
  (0.25; tests set 0), updates at most every 100 ms, info "Checked N positions. Cancel stops the check.".
- `Options` (null = core defaults; tests set a small budget).
- `GetWarningTitle` ("Level Cannot Be Solved" / "Level Not Confirmed"), `GetWarningMessage` (result sentence, then
  "Save {level} anyway? Cancel or Keep Editing writes nothing and keeps your changes; Save Anyway writes the level as it is.").
- `LevelValidationOutcome { Status, Text, IsCancelled, NeedsConfirmation, SaveNote }`. It exists because
  `LevelCheckResult`'s factories are internal to the Validation assembly (not visible to `ColorBlockJam.Editor`), and the
  Play Mode refusal needs a result of its own.

## Texts added by this side

- `Not checked: the Editor is in Play Mode.`
- `Save cancelled: {result text}` (for example `Save cancelled: Cannot be solved: the Red Single block at (0,0) has no Red gate.`)
- Every other sentence is the core's `LevelCheckResult.Text`.

## Deviations and decisions to confirm

1. **Save's check runs only in a designer window.** `SaveLevel` checks solvability only when `m_opensEffectEditor` is
   true, the flag `Open()` sets and a window a test creates leaves off (its existing comment was extended). Reason: most
   existing Level Editor fixtures (`LevelTestPlayTests`, `LevelPenEditTests`, `LevelFreehandPieceTests`,
   `LevelRestoreDiscardTests`, `LevelCanvasBlockToolsTests`, `LevelEditorShellTests`, `LevelPieceDeleteTests`, ...)
   save gate-less levels, which PRE-1 reports as unsolvable. Without the gate, Save in those tests would open a real
   modal `DisplayDialogComplex` (hanging an interactive run) or, in batch mode, pick Cancel and fail their asserted
   writes, which the contract forbids. Reusing the existing serialized flag (instead of a new one) means the user's
   already open Level Editor window, created by `Open()` earlier, gets the check at once without a reopen.
   `LevelValidateButtonTests` sets `OpensEffectEditor = true` (and a no-op `ShowEffectEditor`) to reach it. The Validate
   Level button itself is not gated. If the decision owner prefers a separate flag, it is a one-line change in `SaveLevel`
   plus the flag.
2. **DEC number is DEC-144, not DEC-143.** PROJECT.md currently ends at DEC-142, but
   `TASK-20260927-215425472-f5013490-home-currency-holders-authored` has a DEC-143 row staged for landing
   (contract lines 113 and 145). The log will read DEC-142, DEC-144 until that task lands its row.
3. Validate Level reuses a fresh kept result too (not only Save): a second click on an unchanged draft shows the same
   sentence without searching again.
4. The Save Level default dialog keeps the codebase's convention: Enter = Cancel, Escape and the close box = Keep Editing.
5. The Play Mode tooltip reads "Validate Level is unavailable in Play Mode. Stop Play Mode to check the level."; the
   errors tooltip reads "Fix the errors in Issues first, such as overlapping blocks. Validate Level checks only a valid level."
6. The Undo stack is not asserted directly in the window tests; they assert the draft JSON, the dirty marker, the file
   text and that no write happened. The click path makes no Undo call.

## Tests (not run in this phase)

`LevelValidateButtonTests`: `ValidateLevelIsOnWhereSaveLevelIsOnAndOffInPlayMode`, `ValidateLevelShowsTheResultAndChangesNothing`,
`ValidateLevelNamesWhyALevelCannotBeSolved`, `CancelOrKeepEditingWritesNothingAndKeepsTheEdit` (Save, Test Level, `SaveChanges()`),
`SaveAnywaySavesAsBefore`, `ASolvableLevelSavesWithoutAQuestion`, `ALevelTheCheckCannotConfirmAsksWithItsOwnTitle`,
`ANotCheckedLevelSavesAndKeepsTheReasonAfterTheSaveMessage`, `SaveReusesAnUnchangedResultAndChecksAnEditedDraftAgain`,
`CancelOnTheProgressBarCancelsTheSave`. The solvable fixture expects the exact text `Solvable in 1 move.` (one Red single
at (1,1), a one-cell Red gate on the left edge at row 1).

## Verification in this phase

- `unity_compile_check.py` on the changed files: `COMPILE_UNVERIFIED` (the two `.asmdef` changes; generated projects are
  stale). Without the asmdefs it reports `COMPILE_ERRORS` only because the stale `ColorBlockJam.Editor.csproj` lacks the new
  reference (CS0234 on `ColorBlockJam.Editor.Levels.Validation`).
- Scratch Roslyn build (scratchpad copies of the generated `.csproj` files with absolute paths, package/project references
  pointed at `Library/ScriptAssemblies`, plus `ColorBlockJam.Editor.Validation.dll`): `ColorBlockJam.Editor` 0 errors;
  `ColorBlockJam.Editor.EditModeTests` (against that new Editor DLL, with `LevelValidateButtonTests.cs` added) 0 errors.
- Unity's own compile, the EditMode runs and AC-13 belong to the final Unity phase.

## Review fixes (2026-09-28)

Findings from the independent review, fixed inside the Allowed paths. No Unity refresh, test run or Editor mutation
was made in this phase.

- **containment/F1 (major, no code change).** The shared Editor's last compile of `ColorBlockJam.Editor` ran before the
  asmdef gained the `ColorBlockJam.Editor.Validation` reference and failed (CS0234 on `LevelValidationFlow.cs`). The
  source on disk is consistent: `Editor/ColorBlockJam.Editor.asmdef` and `Tests/EditMode/ColorBlockJam.Editor.EditModeTests.asmdef`
  both name `ColorBlockJam.Editor.Validation`. Still open for the final Unity phase: one `refresh_unity`, then
  `read_console` showing `ColorBlockJam.Editor` and `ColorBlockJam.Editor.EditModeTests` compile with no new errors,
  recorded before any test run. Out-of-Editor evidence now: a patched Roslyn build (the check script's own MSBuild
  path, with the two missing asmdef references added in memory to the stale generated projects) compiles
  `ColorBlockJam.Editor.Validation`, `ColorBlockJam.Editor` and `ColorBlockJam.Editor.EditModeTests` with 0 errors.
- **containment/F2 (minor).** Save Level's check no longer rides on the Effect Editor flag. `LevelDatabaseWindow` has its
  own serialized `m_checksSolvabilityOnSave` (`internal bool ChecksSolvabilityOnSave`), which `Open()` sets beside
  `m_opensEffectEditor`; `SaveLevel` tests it. The Effect Editor flag's comment is back to its own purpose.
  `LevelValidateButtonTests.OpenWindow` sets `ChecksSolvabilityOnSave` instead of `OpensEffectEditor`. Every other
  fixture keeps its outcome (a test-created window still has the flag off). The remaining deviation from AC-7's
  unconditional wording (a window never set by `Open()`, for example one restored from an older layout, saves without
  the check until it is opened through `Open()` once) is recorded in the DEC-144 row of PROJECT.md and in the design
  doc; the contract history needs the orchestrator to record it (this phase may not edit the contract).
  Note for AC-13: after Unity reloads, the user's already open Level Editor window has the new flag off until the
  Color Block Jam Level Editor menu item (or opening the LevelEditor scene) calls `Open()` once.
- **containment/F3 (minor).** `Editor/Levels/Validation/AssemblyInfo.cs` now grants `InternalsVisibleTo` only to
  `ColorBlockJam.Editor.Validation.EditModeTests`. The window tests use only the public `LevelCheckOptions`.
- **containment/F4 (minor).** PRE-1's no-gate reason now names the width: `{block} has no {Colour} gate; it needs one
  at least N cell(s) wide`, where N is the block's smaller extent (the narrowest gate it could use).
  `LevelPreCheckTests` updates the two exact-text assertions and adds
  `ABlockWithNoGateOfItsColourNamesTheNarrowestGateItCouldUse` (a 2 x 2 Red block, "at least 2 cells wide") with the new
  fixture `ValidationFixtures.SquareWithoutGateOfItsColour`.
- **editor-flow/F1 (major).** The Level Database dropdown callback no longer calls `RefreshLevelSelection()` when
  `AskToLeaveDraft()` returns false (Cancel, or a declined or failed Save). It puts the dropdown back with
  `m_databaseChoiceField.SetValueWithoutNotify(change.previousValue)`, as the Target Level field does, so the dirty draft,
  its undo history and the `Save cancelled: ...` status survive. `AskToLeaveDraft`'s "Unsaved Level" prompt has no seam,
  so this path has no automated test; it belongs to the AC-13 manual check.
- **tests/TA-1 (major).** New fixture `ValidationFixtures.NotchBlockedExit` (rows `A..`, `AB.`, `AA.`; Red top gate
  x 0-1, Orange right gate at row 1), added to `CreateSearchable`, so determinism, eager-versus-plain, randomized
  command parity (appended last, so earlier walks keep their seeds) and converter parity all cover a PathBlocked start
  state. New test `LevelSearchTests.AnExitWhosePathHoldsAnotherBlockWaitsUntilThatBlockMoves` asserts the Red L's top
  exit fits with swept mask exactly cells 4 and 7, the check is Solvable, and step 0 is the Orange block's slide.

Changed files in this pass: `Editor/Levels/LevelDatabaseWindow.cs`, `Editor/Levels/Validation/AssemblyInfo.cs`,
`Editor/Levels/Validation/LevelPreChecks.cs`, `Tests/EditMode/LevelValidateButtonTests.cs`,
`Tests/Validation/EditMode/LevelPreCheckTests.cs`, `Tests/Validation/EditMode/LevelSearchTests.cs`,
`Tests/Validation/EditMode/ValidationFixtures.cs`, `docs/Level-Editor-Design.md` (Save Level flag bullet, database-switch
bullet, PRE-1 wording, text example, tests list), `PROJECT.md` (DEC-144 row: the recorded deviation).

Compile evidence: `unity_compile_check.py` on the Validation files: `COMPILE_OK` (both Validation assemblies). On the
window files: `COMPILE_ERRORS` only from the stale generated `ColorBlockJam.Editor.csproj` (CS0234/CS0246 in
`LevelValidationFlow.cs` for the missing Validation reference, cascading into the test assembly); the patched build
above compiles all three assemblies with 0 errors.
