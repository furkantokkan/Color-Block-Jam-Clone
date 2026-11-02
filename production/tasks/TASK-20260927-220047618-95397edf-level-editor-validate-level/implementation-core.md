# Implementation handoff: solvability core (implementer 1 of 2)

Written 2026-09-28. Scope: the editor-only check and its tests. The window, docs and PROJECT.md are
implementer 2's.

## Stop-condition check (Step 0)

- Domain signatures match the contract: `LevelModel.TryCreate(int, int, bool[], DoorDefinition[], BlockDefinition[], int, out LevelModel, out string)`,
  `BlockDefinition(int, BlockColor, Cell, Cell[])`, `DoorDefinition(int, Side, int, int, BlockColor)`, `BoardState(LevelModel)`,
  `MoveRules.CanOccupy(IBoardReader, AttemptHooks, int, Cell)`, `ExitRules.Evaluate(IBoardReader, AttemptHooks, int, Cell, Side, out ExitCandidate)`
  (+ `ExitVerdict`, `ExitCandidate`), `AttemptHooks.CreateEmpty()`, `AttemptHooksBuilder`, `IceRules(int[])`,
  `BoardSimulation(LevelModel, AttemptHooks, DragSettings)`, `CommandExecutor.Execute(in GameCommand)` / `CommandOutcome`,
  `GameCommand.Move` / `GameCommand.MoveAndExit`, `DragSettings.Default`, `Cell`, `Side`, `BlockColor`.
- `MoveBlockHandler.Execute` accepts an exit with `From == To` (only "no move and no exit" is `NoChange`).
- `GameplayArchitectureTests` has no rule against new assemblies referencing `ColorBlockJam.Domain`.
- No Domain, Gameplay or Runtime file was touched; no `InternalsVisibleTo` was added to Domain.

## Files

Production, `Assets/ColorBlockJam/Editor/Levels/Validation/` (each with a `.meta`; folder `Validation.meta`):

| File | Responsibility |
| --- | --- |
| `ColorBlockJam.Editor.Validation.asmdef` | Editor-only, `autoReferenced: false`, references `ColorBlockJam.Domain` and `ColorBlockJam.Runtime` only |
| `AssemblyInfo.cs` | `InternalsVisibleTo` the Validation tests and `ColorBlockJam.Editor.EditModeTests` |
| `LevelSolvabilityCheck.cs` | Public facade: support gate, 16-block limit, builder, pre-checks, search |
| `LevelCheckResult.cs`, `LevelCheckStatus.cs`, `LevelCheckLimit.cs`, `LevelSolutionStep.cs` | Plain-data result (no Domain type in the public API) |
| `LevelCheckOptions.cs`, `LevelBlockDescriber.cs` | Budget constants (250,000 positions, 3.0 s, 16 blocks), eager-exit switch, describer override |
| `ILevelCheckProgress.cs`, `SilentLevelCheckProgress.cs` | Progress/cancel seam and its silent default |
| `LevelSolverSupport.cs` | Support table keyed by effect type id (only `ice`), first unsupported mechanic in designer words |
| `LevelSolverModelBuilder.cs` | `LevelData` to Domain `LevelModel`, same semantics as `AttemptLevelConverter`'s model half |
| `LevelSolverTables.cs` | Domain predicates asked once per block and origin, cached as masks (probe-derived swept masks) |
| `LevelSolverMoves.cs` | Legal slide/exit test from the masks plus the Ice hook |
| `LevelPreChecks.cs` | PRE-1 (single-block reachability to a fitting exit) and PRE-2 (Ice count vs other blocks) |
| `LevelSolverSearch.cs` | Bounded BFS, eager exits, budget, progress/cancel, step reconstruction |
| `LevelStateKey.cs`, `CellMask.cs` | 128-bit state key and 128-bit cell set |
| `LevelDesignerText.cs` | Colour/shape/cell/mechanic/limit wording |

Tests, `Assets/ColorBlockJam/Tests/Validation/EditMode/` (asmdef `ColorBlockJam.Editor.Validation.EditModeTests`,
Editor-only, `overrideReferences` + `nunit.framework.dll`, `defineConstraints: UNITY_INCLUDE_TESTS`, references
Validation, Domain, Runtime, Gameplay, both test runners). The live Editor auto-imported the new folders while they were
being written and generated the test files' and test folders' `.meta` files itself (fresh GUIDs, standard format); they
were kept. The production `.meta` files carry the GUIDs written here (the Editor reimported them).

- `ValidationLevelFixture.cs` (ASCII map to `LevelData`, shipped-level loader, `BoardSimulation` with Ice hooks, step/move to command), `ValidationFixtures.cs` (named levels)
- `ShippedLevelSolvabilityTests.cs` (AC-2), `LevelPreCheckTests.cs` (AC-3), `LevelSearchTests.cs` (AC-4, AC-5, AC-9 determinism and eager vs plain, AC-11 cancel/progress),
  `LevelSupportGateTests.cs` (AC-6 incl. completeness), `LevelCommandParityTests.cs` (AC-9 randomized parity, seed 20260928),
  `LevelSolverModelBuilderTests.cs` (AC-10), `LevelStateKeyTests.cs`

## API for implementer 2 (the window)

```csharp
using ColorBlockJam.Editor.Levels.Validation;

LevelCheckResult result = LevelSolvabilityCheck.Run(LevelData level);                       // defaults, silent
LevelCheckResult result = LevelSolvabilityCheck.Run(LevelData level, LevelCheckOptions options, ILevelCheckProgress progress);
// result.Status: Solvable | Unsolvable | Unknown | NotChecked
// result.Text: the full status sentence; result.Reason: the part after the prefix (null for Solvable)
// result.MoveCount, result.Steps (IReadOnlyList<LevelSolutionStep>), result.VisitedPositions,
// result.ElapsedSeconds, result.LimitHit (None | Positions | Time)
public interface ILevelCheckProgress { bool IsCancelRequested { get; } void Report(int positions, double elapsedSeconds); }
// LevelCheckOptions { MaxPositions, MaxSeconds, EagerExits, BlockDescriber } and
// LevelCheckOptions.DefaultMaxPositions / DefaultMaxSeconds / MaxBlocks
public delegate string LevelBlockDescriber(LevelData level, int blockIndex);
```

- The caller owns stage 1, the Play Mode refusal (`Not checked: ...` text of its own), the 0.25 s delay before showing a
  progress bar and clearing it. `Report` is called at the first expansion and then at most every 100 ms (checked every
  1024 expansions); a cancel returns `Not checked: the check was cancelled.`
- The check runs synchronously and only reads the `LevelData`; pass the draft's `LevelData`.
- Designer texts: `Solvable in N moves.` (`1 move.` for one), `Cannot be solved: {reason}.`,
  `Not confirmed: the check stopped after 250,000 positions.` / `... after 3 seconds.`,
  `Not checked: this level uses Arrow blocks, which the check does not cover yet.`,
  `Not checked: this level has 17 blocks and the check covers at most 16.`,
  `Not checked: the level has errors; fix them in Issues first.` (defensive builder refusal).
- The default block text is `the {Colour} {Shape} block at (x,y)`: colour and shape ids split into words, `Custom_`
  ids read "custom", and the cell is the block's lowest, then leftmost, board cell in `LevelDraftIssues`'s zero-based
  `(x,y)` form. The window may pass `BlockDescriber` to use the piece library's display names.

## Decisions and how steps map to commands

- Tables: per block, a one-block `LevelModel` (same floor and doors). Standing = `MoveRules.CanOccupy` with the block at
  its authored origin; exit fit = `ExitRules.Evaluate(...) == Fits` on a board showing the block at the origin (as
  `MoveBlockHandler`'s target view does); swept mask = every other floor cell `c` where a board with the block at the
  origin plus a one-cell probe block at `c` makes `Evaluate` return `PathBlocked`. The search never re-derives geometry.
- Slot = cell index of the footprint box's bottom-left corner (same slot rule as `MoveReach`), 7 bits.
- State key: slot of block `i` at bits `7i`, exited bit of block `i` at bit `112 + i`; an exited block's slot is 0.
- Successor order: block ascending; slides +X, -X, +Y, -Y then that block's exits in `Side` order. Eager mode: when any
  exit exists in a position, only the available exits are expanded. Eager and plain BFS find solutions of equal step
  count (tested), because moving an available exit earlier never lengthens a solution.
- Ice hook: a block is held while `clearsToThaw - exitedCount > 0` (exited blocks are always other blocks).
- Steps map to commands: a slide step is `GameCommand.Move(block, From, To)`; an exit step is
  `GameCommand.MoveAndExit(block, From, From, side)`. A DEC-122 player move is a run of one block's consecutive steps:
  `Move(block, firstFrom, lastTo)` or `MoveAndExit(block, firstFrom, lastTo, side)` when the run ends in an exit. Both
  forms are replayed in the tests.

## Deviations and notes

- No ZString: the contract limits the assembly to Domain and Runtime references, so texts use `string` concatenation
  and one `StringBuilder` (editor-only, not a hot path).
- `Solvable in 1 move.` uses the singular for one move; every other count reads `moves`.
- PRE-1 has a third reason, `cannot reach a {Colour} gate it fits through`, when a same-colour gate is wide enough but
  unreachable; the gate-width reason names the smallest extent the block needs among its colour's gate sides.
- `ElapsedSeconds` and the time budget include table building and pre-checks.

## Verification so far

- Roslyn compile check (`unity_compile_check.py` on all 28 `.cs` files): `COMPILE_OK` for both new assemblies (the Editor
  had already generated their projects). With the `.asmdef` files listed it returns `COMPILE_UNVERIFIED` as expected.
- The live Editor auto-compiled both assemblies (`Library/ScriptAssemblies/ColorBlockJam.Editor.Validation*.dll`); the
  Editor log shows no compile error for them. No Unity test run, refresh or Editor mutation was requested.
- Out-of-Editor smoke run (scratch harness under Unity's Mono, not evidence for closure): 28 of 30 tests passed; the two
  `LevelSolverModelBuilderTests` need `ScriptableObject.CreateInstance` and can only run in Unity. Shipped levels:
  level-01 Solvable in 2 moves (4 steps, 16 positions), level-02 2 moves (6 steps, 114), level-03 3 moves (9 steps, 1,547),
  level-04 5 moves (19 steps, 5,136), level-05 8 moves (20 steps, 25,892; about 28 ms).
