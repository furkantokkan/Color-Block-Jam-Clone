# Final design: block drag and colour-door exit (sections A–E)

Sections F–I (tests, ownership, deferred items, open questions) are in `final-design-2.md` in this folder. `slices.md` orders the implementation; `decisions.md` holds the PROJECT.md rows.

Decision owner notes on the three drafts:
- Structure and domain API follow `draft-domain-solid` (pure `ExitRules`/`MoveRules`, `AttemptHooks`, `BlockPicker`, transition table as a function). Its three-assembly split (Domain, Gameplay, GameScene) is rejected: two new assemblies satisfy "runtime code never depends on presentation" because `ColorBlockJam.Runtime` is untouched, and PROJECT.md splits only for a stable boundary.
- Lifecycle, ownership table, event-buffer-per-tick, polling of exit tweens (no callbacks), deferred `MoveCommitted` after an interrupted drag, and the Unity-null guard on scene unload come from `draft-lifecycle-perf`.
- Exit push threshold (0.3 cell, not 0.5), 1:1 follow with a 0.45 lead, corner-buffer picking, `<Pointer>` bindings and the UI Toolkit panel pick come from `draft-input-feel`. Its interim "held lift" is rejected: no throwaway visual; the outline task fills the `SetHeld` port.
- Every draft agreed on: exit commits during the drag when pushed (DEC-50 reading), `BlockExited` only on exit (no `MoveCommitted`), PRD four-event set, block index as the stable tie id, code-built input map, interim auto-resume without a pause UI, outline and fragments deferred.

Absolute project root: `C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026`. Paths below are relative to `Assets/ColorBlockJam/` unless stated.

---

## A. Assemblies

| Assembly | Folder | References | Settings | Why |
|---|---|---|---|---|
| `ColorBlockJam.Domain` | `Domain/` | none | `noEngineReferences: true`, `autoReferenced: false`, `rootNamespace: ColorBlockJam.Domain` | Pure rules (AR-1, NFR-11, V-E22). Its own `Cell`, `Side`, `BlockColor` value types so no `UnityEngine` type crosses in. |
| `ColorBlockJam.Gameplay` | `Gameplay/` | `ColorBlockJam.Domain`, `ColorBlockJam.Runtime`, `ColorBlockJam.Presentation`, `ColorBlockJam.Environment`, `Onity.DI`, `Onity.Unity`, `Onity.Reactive`, `Onity.Pooling`, `Onity.Factory`, `PrimeTween.Runtime`, `Unity.InputSystem` | `autoReferenced: false`, `rootNamespace: ColorBlockJam.Gameplay` | The attempt feature: session, tick owner, input, level conversion, board presenter and the Game-scene installer. It joins Runtime and Presentation exactly as `ColorBlockJam.Environment` does; `ColorBlockJam.Runtime` stays independent of presentation. |
| `ColorBlockJam.Gameplay.EditModeTests` | `Tests/Gameplay/EditMode/` | `ColorBlockJam.Domain`, `ColorBlockJam.Gameplay`, `ColorBlockJam.Runtime`, `ColorBlockJam.Presentation`, `Onity.DI`, `Onity.Reactive`, `UnityEditor.TestRunner`, `UnityEngine.TestRunner` | `includePlatforms: ["Editor"]`, `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, `autoReferenced: false` | Copies `Tests/Environment/EditMode/ColorBlockJam.Environment.EditModeTests.asmdef`. Runtime is needed for `LevelData`/`BoardLayout`/`BlockShapeLibrary.Load(json)` fixtures; Presentation only for the `BlockShapeType` enum exposed through Runtime is not needed, but the converter returns `BlockShapeType` (Runtime) so Presentation is listed only if a test touches `BlockPieceSpawnData`; drop it if unused. |
| `ColorBlockJam.Gameplay.PlayModeTests` | `Tests/Gameplay/PlayMode/` | `ColorBlockJam.Domain`, `ColorBlockJam.Gameplay`, `ColorBlockJam.Runtime`, `ColorBlockJam.Presentation`, `ColorBlockJam.Environment`, `Onity.DI`, `Onity.Unity`, `Onity.Pooling`, `Onity.Factory`, `PrimeTween.Runtime`, both TestRunners | `includePlatforms: []`, `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, `autoReferenced: false`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]` | Copies `Tests/Environment/PlayMode/ColorBlockJam.Environment.PlayModeTests.asmdef`. |

Dependency graph (arrows point at dependencies; no cycle):

```
Gameplay.PlayModeTests ─┐
Gameplay.EditModeTests ─┼─▶ Gameplay ─▶ Environment ─▶ Presentation ─▶ Runtime
                        │      │  │          │              │
                        │      │  └──────────┴──────────────┘ (Gameplay also references Presentation and Runtime directly)
                        └──────┴─▶ Domain   (Domain references nothing; Runtime does not reference Domain)
```

Not created: no `.inputactions` asset (the map is built in code, DEC-114), no `Validation` assembly (editor solver is a later task), no change to any existing asmdef.

Assembly names for the architecture test: exactly one `IOnityTickable` implementer across `ColorBlockJam.*` non-test assemblies (`GameplayTickOwner`).

---

## B. Files to create

Namespaces: `ColorBlockJam.Domain.{Board,Movement,Exit,Picking,Hooks,Session}` and `ColorBlockJam.Gameplay.{Levels,Input,Session,Board,Composition}`. Every class has a one-sentence summary comment; every field follows `m_`/`s_`/`k_`.

### B.1 Domain (`Domain/`)

| File | Type and public surface | Responsibility |
|---|---|---|
| `ColorBlockJam.Domain.asmdef` | see A | |
| `Board/Cell.cs` | `readonly struct Cell : IEquatable<Cell>` — `int X, Y`; `Cell(int x, int y)`; `static Cell operator +(Cell, Cell)`, `-`; `==`, `!=`; `static readonly Cell Zero` | An integer board coordinate; cell (x, y) is column x, row y counted from the Bottom edge (matches `IBoardLayout`). |
| `Board/BoardPoint.cs` | `readonly struct BoardPoint` — `float X, Y`; `+`, `-` with `BoardPoint` and `Cell`; `static BoardPoint FromCell(Cell)` | A fractional board-plane position in cell units (pointer projections and visual offsets). |
| `Board/Side.cs` | `enum Side { Top = 0, Bottom = 1, Left = 2, Right = 3 }` | The four board sides; ordinals equal `BoardSide` so the converter casts. |
| `Board/SideMath.cs` | `static class SideMath` — `Cell GetOutward(Side)`; `bool TryGetSide(Cell direction, out Side)`; `bool RunsAlongX(Side)` (Top/Bottom true); `Side Opposite(Side)` | Direction arithmetic for sides. |
| `Board/BlockColor.cs` | `readonly struct BlockColor : IEquatable<BlockColor>` — `int Id`; `==` | A palette identity (the `PaletteColor` ordinal) without the Unity enum. |
| `Board/AxisMask.cs` | `[Flags] enum AxisMask { None = 0, Horizontal = 1, Vertical = 2, Both = 3 }` | Which axes a block may move on (XP-2). |
| `Board/DoorDefinition.cs` | `sealed class DoorDefinition` — `int Index; Side Side; int Start; int Length; BlockColor Color`; `bool Covers(int lo, int hi)` (`lo >= Start && hi < Start + Length`) | One authored door span. |
| `Board/BlockDefinition.cs` | `sealed class BlockDefinition` — `int Index; BlockColor Color; Cell Origin; int CellCount; Cell GetCell(int i)` (relative); `int MinX, MaxX, MinY, MaxY` (relative extents, precomputed); `int Width => MaxX-MinX+1; int Depth(Side)` (extent along the side's outward axis); `int Extent(Side)` (extent along the side) | One authored block footprint and colour. |
| `Board/LevelModel.cs` | `sealed class LevelModel` — `int Width, Height, TimerSeconds; int DoorCount; DoorDefinition GetDoor(int); int BlockCount; BlockDefinition GetBlock(int); bool IsFloor(int x, int y); int GetDoorAtLane(Side side, int lane)` (−1 if none); `static bool TryCreate(int w, int h, bool[] floor, DoorDefinition[] doors, BlockDefinition[] blocks, int timerSeconds, out LevelModel model, out string error)` | The immutable converted level. `TryCreate` re-validates bounds, overlap, 4-connectivity, door spans and precomputes `m_doorAtLane[side * laneCapacity + lane]` with `laneCapacity = max(W, H)`. |
| `Board/BlockStatus.cs` | `enum BlockStatus { OnBoard, Exited, Removed }` | Whether a block still occupies cells. |
| `Board/IBoardReader.cs` | `interface IBoardReader` — `LevelModel Model { get; } int Width, Height, BlockCount; Cell GetOrigin(int block); BlockStatus GetStatus(int block); int GetOccupant(Cell)` (−1 empty, or −2 off-board); `bool IsFloor(Cell)`; `int RequiredRemaining { get; }` | Read-only board access for rules, hooks and the editor solver (AR-10). |
| `Board/BoardState.cs` | `sealed class BoardState : IBoardReader` — `BoardState(LevelModel)`; `void Move(int block, Cell origin)`; `void Release(int block, BlockStatus status)`; `void Reset()` | The mutable attempt occupancy: `int[] m_occupant` (W×H, −1), `Cell[] m_origin`, `BlockStatus[] m_status`. `Reset` rebuilds from the model; no allocation after construction. |
| `Hooks/IDragGate.cs` | `interface IDragGate { bool AllowsDrag(IBoardReader board, int block); }` | XP-1. |
| `Hooks/IAxisConstraint.cs` | `interface IAxisConstraint { AxisMask GetAxes(IBoardReader board, int block); }` | XP-2. |
| `Hooks/ICellGate.cs` | `interface ICellGate { bool AllowsCell(IBoardReader board, int block, Cell cell); }` | XP-3. |
| `Hooks/ExitQuery.cs` | `readonly struct ExitQuery` — `int Block; int Door; Side Side; Cell Origin` | What an exit gate is asked. |
| `Hooks/ExitOutcome.cs` | `enum ExitOutcome { Leave = 0, Stay = 1 }` | Whether a permitted exit removes the block (`Leave`) or keeps it (`Stay`, future Layer peel). |
| `Hooks/IExitGate.cs` | `interface IExitGate { bool AllowsExit(IBoardReader board, in ExitQuery query, ref ExitOutcome outcome); }` | XP-4; gates AND, any gate may set `Stay`. |
| `Hooks/BoardEvents.cs` | `readonly struct BlockExitedEvent(int Block, int Door, Side Side, Cell Origin, ExitOutcome Outcome)`; `readonly struct BlockRemovedEvent(int Block)`; `readonly struct MoveCommittedEvent(int Block, Cell From, Cell To)`; `readonly struct FootprintChangedEvent(int Block)` | The four PRD board events (RULE-28). `BlockRemoved`/`FootprintChanged` are defined for boosters; nothing raises them in this task. |
| `Hooks/IBoardEventListener.cs` | `interface IBoardEventListener { void OnBlockExited(in BlockExitedEvent e); void OnBlockRemoved(in BlockRemovedEvent e); void OnMoveCommitted(in MoveCommittedEvent e); void OnFootprintChanged(in FootprintChangedEvent e); }` | XP-5. |
| `Hooks/AttemptHooks.cs` | `sealed class AttemptHooks` — `static AttemptHooks CreateEmpty()`; `bool AllowsDrag(IBoardReader, int)`; `AxisMask GetAxes(IBoardReader, int)` (start `Both`, AND each); `bool AllowsCell(IBoardReader, int, Cell)`; `bool AllowsExit(IBoardReader, in ExitQuery, out ExitOutcome)`; `void Publish(in BlockExitedEvent)`, `Publish(in BlockRemovedEvent)`, `Publish(in MoveCommittedEvent)`, `Publish(in FootprintChangedEvent)`; `int DragGateCount` etc. | Immutable hook arrays built once per attempt (`IDragGate[]`, `IAxisConstraint[]`, `ICellGate[]`, `IExitGate[]`, `IBoardEventListener[]`) iterated with `for` loops; empty arrays by default (AC-EXT-10). |
| `Hooks/AttemptHooksBuilder.cs` | `sealed class AttemptHooksBuilder` — `Add(IDragGate)`, `Add(IAxisConstraint)`, `Add(ICellGate)`, `Add(IExitGate)`, `Add(IBoardEventListener)`; `AttemptHooks Build()` | Collects hooks in registration order at attempt build (Home); the future effect registry uses it (DEC-72). |
| `Hooks/BoardEventLog.cs` | `sealed class BoardEventLog` — `BoardEventLog(int capacity)`; `int Count`; `ref readonly BoardEvent GetAt(int)`; `void Add(in BoardEvent)` (throws when full); `void Clear()`; nested `readonly struct BoardEvent { BoardEventKind Kind; int Block; int Door; Side Side; Cell From; Cell To; ExitOutcome Outcome }`; `enum BoardEventKind { Exited, Removed, MoveCommitted, FootprintChanged }` | This tick's events for the presenter (view sync) in a preallocated array; capacity `blockCount + 4`. |
| `Movement/MoveRules.cs` | `static class MoveRules` — `bool CanOccupy(IBoardReader board, AttemptHooks hooks, int block, Cell origin)`; `bool IsFlush(IBoardReader board, int block, Cell origin, Side side)` | The cell gate (RULE-2, RULE-3, XP-3) and the flush test. |
| `Movement/DragSettings.cs` | `readonly struct DragSettings` — `int MaxStepsPerTick; float ExitPushCells; float LeadCells; static DragSettings Default => (MaxStepsPerTick: 0 = W+H, ExitPushCells: 0.3f, LeadCells: 0.45f)` | Tunables of the drag (DEC-111, DEC-112). |
| `Movement/DragResult.cs` | `enum DragResult { Unchanged, Moved, Exited }` | What one drag update did. |
| `Movement/DragResolver.cs` | `sealed class DragResolver` — `DragResolver(DragSettings)`; `bool IsActive; int Block; bool IsConsumed; Cell StartOrigin; BoardPoint GrabOffset; AxisMask Axes`; `bool TryBegin(BoardState board, AttemptHooks hooks, int block, BoardPoint pointer)`; `DragResult Update(BoardState board, AttemptHooks hooks, BoardEventLog log, BoardPoint pointer)`; `void End(BoardState board, AttemptHooks hooks, BoardEventLog log)`; `void Interrupt()`; `void FlushPending(AttemptHooks hooks, BoardEventLog log)`; `BoardPoint GetViewOffset(BoardState board, AttemptHooks hooks, BoardPoint pointer)` | The one active drag: cell-by-cell stepping, exit-on-push, deferred `MoveCommitted` after an interruption (RULE-9..14, DEC-MECH-25). |
| `Movement/FollowOffset.cs` | `static class FollowOffset` — `BoardPoint Compute(IBoardReader board, AttemptHooks hooks, int block, Cell origin, BoardPoint desired, AxisMask axes, float leadCells)` | The visual lead of the held block: never toward a blocked cell, up to `leadCells` toward a free cell or a usable door. |
| `Exit/ExitVerdict.cs` | `enum ExitVerdict { Fits, NotFlush, NoDoor, TooNarrow, WrongColor, PathBlocked, Vetoed }` | Why an exit was refused, or that it fits. |
| `Exit/ExitCandidate.cs` | `readonly struct ExitCandidate` — `int Door; Side Side; ExitOutcome Outcome` | A fitting exit. |
| `Exit/ExitRules.cs` | `static class ExitRules` — `ExitVerdict Evaluate(IBoardReader board, AttemptHooks hooks, int block, Cell origin, Side side, out ExitCandidate candidate)` | The pure exit predicate (RULE-12..14, DEC-6, AR-10). |
| `Picking/ScreenPoint.cs` | `readonly struct ScreenPoint` — `float X, Y` | A screen-space position in pixels. |
| `Picking/IFootprintProjector.cs` | `interface IFootprintProjector { ScreenPoint Project(float boardX, float boardY); }` | Projects a board-plane point at block-top height to screen space (fake in tests, camera in the scene). |
| `Picking/InteractionArea.cs` | `static class InteractionArea` — `bool Contains(BoardPoint p, int width, int height)` (`-1 <= p.X <= width && -1 <= p.Y <= height`) | Interior plus the half-cell wall/gate ring plus a half-cell border (DEC-91). |
| `Picking/BlockPicker.cs` | `sealed class BlockPicker` — `BlockPicker(int width, int height)` (allocates `ScreenPoint[(W+1)*(H+1)]`); `int Pick(ScreenPoint press, IBoardReader board, IFootprintProjector projector)` (−1 none) | Once per pointer-down: direct hit, else nearest projected occupied footprint, ties to the lowest block index (AC-3.19). |
| `Session/SessionState.cs` | `enum SessionState { Home, Playing, Paused, Won, Failed }` | The one session state. |
| `Session/PauseReason.cs` | `enum PauseReason { None, User, Background, Saving, Purchase }` | Why the session is Paused; not a state. |
| `Session/SessionRequest.cs` | `enum SessionRequest { OpenPreLevel, ClosePreLevel, Play, Pause, Background, BoosterCommit, ZeroStockTap, Resume, CommitSucceeded, CommitFailed, PurchaseClosed, Restart, Home, AllRequiredCleared, TimerExpired, NextLevel }` | Every request in PRD 7.5. |
| `Session/SessionTransitions.cs` | `static class SessionTransitions` — `bool TryGetTarget(SessionState from, SessionRequest request, out SessionState to)` | The literal 7.5 table as a pure function (AC-5.6, V-E5). |
| `Session/SessionStateMachine.cs` | `sealed class SessionStateMachine` — `SessionState State; PauseReason PauseReason; int AttemptNumber; bool TryApply(SessionRequest request, PauseReason reason = PauseReason.None)` | Holds state and pause reason; the terminal latch is the table itself (no row leaves Won/Failed on a terminal request), `AttemptNumber` increments on every entry into Playing from Home, Won, Failed or Restart. |
| `Session/ICountdown.cs` | `interface ICountdown { void Start(int seconds); void SetPaused(bool paused); void Stop(); bool Tick(float deltaTime); float Remaining { get; } }` | The timer phase slot (RULE-18); `Tick` returns true when it reaches zero. |
| `Session/InactiveCountdown.cs` | `sealed class InactiveCountdown : ICountdown` — `Tick` returns false; `Remaining` stays at the started value | Placeholder until the countdown task binds the real timer; keeps the phase order testable now. |
| `Session/BoardSimulation.cs` | `sealed class BoardSimulation` — `BoardSimulation(LevelModel model, AttemptHooks hooks, DragSettings settings)`; `BoardState Board; DragResolver Drag; BlockPicker Picker; BoardEventLog Events; AttemptHooks Hooks; bool IsCleared => Board.RequiredRemaining == 0`; `void Reset()` | The domain side of one attempt, built on Home and reset on Restart. |

### B.2 Gameplay (`Gameplay/`)

| File | Type and public surface | Responsibility |
|---|---|---|
| `ColorBlockJam.Gameplay.asmdef` | see A | |
| `Levels/BlockVisualSpec.cs` | `readonly struct BlockVisualSpec` — `BlockShapeType SourceType; PaletteColor Color; Vector2Int[] Cells; Vector2Int Origin` | Everything the presenter needs to spawn one block root. |
| `Levels/AttemptLevel.cs` | `sealed class AttemptLevel` — `LevelModel Model; IBoardLayout Layout; string LevelId; int BlockCount; BlockVisualSpec GetVisual(int); string GetBlockId(int)` | One converted level: domain model plus parallel visual specs; immutable and reused by Restart. |
| `Levels/ShapeResolver.cs` | `static class ShapeResolver` — `bool TryResolve(BlockShapeLibrary library, string shapeId, out BlockShapeLibrary.Shape shape)` (ordinal `TypeName` match) | The single place a `shapeId` becomes a library shape; the runtime-shapes-from-json task replaces this one file's lookup. |
| `Levels/AttemptLevelConverter.cs` | `static class AttemptLevelConverter` — `bool TryConvert(LevelData level, IBoardLayout layout, BlockShapeLibrary library, out AttemptLevel result, out string error)` | Converts authored data into the domain model once on Home (AC-8.5); never mutates `LevelData`. |
| `Input/PointerPhase.cs` | `enum PointerPhase { None, Down, Held, Up, Cancel }` | The primary pointer's phase this frame. |
| `Input/PointerSample.cs` | `readonly struct PointerSample` — `PointerPhase Phase; Vector2 ScreenPosition` | One polled reading. |
| `Input/IPointerSource.cs` | `interface IPointerSource { void Enable(); void Disable(); PointerSample Read(); }` | The gameplay pointer port; tests use a fake. |
| `Input/GameplayPointerInput.cs` | `sealed class GameplayPointerInput : IPointerSource, IDisposable` | Builds and owns the code-built `InputActionMap "Gameplay"`, polls the primary pointer, latches the pressing device, reports cancel. |
| `Input/IUiPointerFilter.cs` | `interface IUiPointerFilter { bool IsOverUi(Vector2 screenPosition); }` | Reports a press that landed on UI (AC-3.13). |
| `Input/UiToolkitPointerFilter.cs` | `sealed class UiToolkitPointerFilter : IUiPointerFilter` — `UiToolkitPointerFilter(UIDocument[] documents)` | Picks the top element of each runtime panel; a hit whose `pickingMode != Ignore` is UI. |
| `Input/IBoardProjector.cs` | `interface IBoardProjector { bool TryScreenToBoard(Vector2 screen, out BoardPoint point); IFootprintProjector Footprint { get; } }` | Screen-to-board-plane and board-to-screen conversions. |
| `Input/CameraBoardProjector.cs` | `sealed class CameraBoardProjector : IBoardProjector, IFootprintProjector` — `CameraBoardProjector(Camera camera, Transform boardRoot, float planeHeight)` | Analytic ray/plane projection against the BoardRoot plane at block-top height; no colliders. |
| `Session/IBoardPresenter.cs` | `interface IBoardPresenter { void Build(AttemptLevel level); void Place(IBoardReader board); void Sync(BoardSimulation simulation, BoardPoint pointer); void SetHeld(int block); void SnapHeld(); void SetTweensPaused(bool paused); void StopTweens(); void ReturnPieces(); float FinishSeconds { get; } }` | The presentation port the session drives; views never decide domain state. |
| `Session/IPauseOverlay.cs` | `interface IPauseOverlay { bool ResumesOnForeground { get; } }` | What happens on foreground return with no pause UI. |
| `Session/NoPauseOverlay.cs` | `sealed class NoPauseOverlay : IPauseOverlay` — `NoPauseOverlay(bool resumesOnForeground)` | Interim auto-resume until the pause-popup task (DEC-116). |
| `Session/AttemptLifetime.cs` | `sealed class AttemptLifetime : IDisposable` — `CancellationToken Token; CompositeDisposable Disposables; void Cancel(); void Dispose()` | One attempt's CTS and CompositeDisposable (NFR-L1). |
| `Session/LevelAttempt.cs` | `sealed class LevelAttempt` — `LevelAttempt(AttemptLevel level, BoardSimulation simulation, int generation)`; `AttemptLevel Level; BoardSimulation Simulation; AttemptLifetime Lifetime; int Generation` | Everything one attempt owns, dropped as a unit by Teardown. |
| `Session/DragInputRouter.cs` | `sealed class DragInputRouter` — `DragInputRouter(IPointerSource pointer, IUiPointerFilter ui, IBoardProjector projector)`; `void Run(LevelAttempt attempt, IBoardPresenter presenter)`; `void Reset(LevelAttempt attempt)`; `BoardPoint LastPointer` | Phase-1 command work: pointer samples become pick, drag, release and cancel; UI and area gating. |
| `Session/LevelSession.cs` | `sealed class LevelSession : IDisposable` — ctor `(IPointerSource, IUiPointerFilter, IBoardProjector, IBoardPresenter, ICountdown, IPauseOverlay)`; `IReadOnlyReactiveProperty<SessionState> State; PauseReason PauseReason; LevelAttempt Attempt; bool TryPrepare(AttemptLevel level, out string error); bool Play(); bool Pause(PauseReason reason); bool Resume(); bool Restart(); bool GoHome(); void NotifyBackground(bool backgrounded); void RunTick(float deltaTime); void CompleteFinish(); void Dispose()` | The only owner of attempt lifetime, transitions, Suspend, Teardown and Finish. |
| `Session/GameplayTickOwner.cs` | `sealed class GameplayTickOwner : IOnityTickable` — `GameplayTickOwner(LevelSession)`; `void Tick()` | The project's single `IOnityTickable`; returns unless Playing, else `RunTick(Time.deltaTime)`. |
| `Board/ExitMotionSettings.cs` | `static class ExitMotionSettings` — `const float k_secondsPerCell = 0.12f, k_minSeconds = 0.2f, k_maxSeconds = 0.6f, k_clipOffsetCells = 0.25f, k_settleSeconds = 0.08f, k_blockTopHeight = 0.5f` | Exit and settle tuning; `k_blockTopHeight` is UNPROVEN (measure the stud top from `Pieces/Center.prefab`). |
| `Board/BoardPresenter.cs` | `sealed class BoardPresenter : IBoardPresenter, IDisposable` — `BoardPresenter(EnvironmentView environment, BlockShapeLibrary shapes)` | Owns the level's `BlockPieceFactory` (the one construction site), block roots by index, position sync, settle tween, exit motion and clip plane. |
| `Composition/GameSceneInstaller.cs` | `[DisallowMultipleComponent] sealed class GameSceneInstaller : MonoInstaller` — serialized `EnvironmentView m_environment; BlockShapeLibrary m_shapes; Camera m_camera; UIDocument[] m_uiDocuments; int m_levelIndex; bool m_resumeOnForeground = true` | Validates references, binds the Game-scene services and registers the async attempt build. |
| `Composition/GameAttemptLoader.cs` | `sealed class GameAttemptLoader` — `GameAttemptLoader(int levelIndex)`; `Task LoadAsync(IResolver resolver, CancellationToken ct)` | Waits for the project scope, builds environment and attempt on Home, then calls `Play` (interim auto-start). |
| `Composition/ApplicationPauseRelay.cs` | `sealed class ApplicationPauseRelay : MonoBehaviour` — `[Inject] void Construct(LevelSession)`; `OnApplicationPause(bool)` | Forwards OS backgrounding to the session; no per-frame code. |

### B.3 Tests

Listed in `final-design-2.md` section F.

---

## C. Algorithms and zero-allocation strategy

### C.1 Level to domain conversion (`AttemptLevelConverter.TryConvert`, Home only, allocation allowed)

```
if !level.Validate(out error) return false
W = layout.Width; H = layout.Height
floor = new bool[W*H]; floor[y*W+x] = layout.IsFloor(x, y)          // voids arrive through IsFloor (v2 schema)
doors[i] for i < level.DoorCount:
    d = level.GetDoorAt(i)
    if !BoardLayout.TryParseDoorColor(d.ColorId, out pc) fail("Door {d.Id} has no gate colour")
    doors[i] = new DoorDefinition(i, (Side)(int)d.Side, d.Start, d.Length, new BlockColor((int)pc))
blocks[i], visuals[i], ids[i] for i < level.BlockCount:
    b = level.GetBlockAt(i)                                            // GetBlockAt returns a clone; never written
    if !ShapeResolver.TryResolve(library, b.ShapeId, out shape) fail("Block {b.Id} uses unknown shape")
    if shape.Prefab == null fail("Block {b.Id} shape has no prefab")
    if !BoardLayout.TryParseDoorColor(b.ColorId, out pc) fail("Block {b.Id} uses unknown colour")
    cells = new Cell[b.CellCount]; vcells = new Vector2Int[b.CellCount]  (copied from b.GetCellAt)
    blocks[i] = new BlockDefinition(i, new BlockColor((int)pc), new Cell(b.Origin.x, b.Origin.y), cells)
    visuals[i] = new BlockVisualSpec(shape.Type, pc, vcells, b.Origin); ids[i] = b.Id
if !LevelModel.TryCreate(W, H, floor, doors, blocks, level.TimerSeconds, out model, out error) return false
result = new AttemptLevel(model, layout, level.Id, visuals, ids)
```

The block index is the stable id (authored order) used for pick ties. `LevelModel.TryCreate` checks each door lane range, that a door lane is not double-booked, that every block cell is floor and inside the board, no overlap, 4-connected, and that `Length >= 1`. `m_doorAtLane` is `int[4 * max(W,H)]` filled with −1 then door indices.

### C.2 Occupancy (`BoardState`)

- Constructor: `m_occupant = new int[W*H]` filled −1; `m_origin[i] = def.Origin`; `m_status[i] = OnBoard`; place every block (throw if a cell is already taken: guard on validated data); `RequiredRemaining = BlockCount` (every block is required, RULE-29; XP-8 later).
- `Move(b, o)`: clear the cells of b at the old origin (only where the cell holds b), write b at the new origin, `m_origin[b] = o`. O(cells).
- `Release(b, status)`: clear cells, `m_status[b] = status`, `RequiredRemaining--` when the previous status was `OnBoard`.
- `GetOccupant(cell)`: −2 when outside the board, else `m_occupant[y*W+x]`.
- `Reset()`: clear the array, re-place every block from the model, `RequiredRemaining = BlockCount`.

### C.3 Cell gate (`MoveRules.CanOccupy`)

```
def = board.Model.GetBlock(b)
for i < def.CellCount:
    a = origin + def.GetCell(i)
    if a.X < 0 || a.Y < 0 || a.X >= W || a.Y >= H  return false     // outer wall ring and every door segment
    if !board.IsFloor(a)                             return false     // void / interior wall (RULE-3)
    o = board.GetOccupant(a); if o != -1 && o != b   return false     // RULE-2
    if !hooks.AllowsCell(board, b, a)                return false     // XP-3, AND
return true
```

A door is never an occupiable cell; leaving the board happens only through `ExitRules` + commit. Every mismatched or too-narrow door is therefore a wall automatically (RULE-14).

`MoveRules.IsFlush(board, b, origin, side)`: Top `origin.Y + def.MaxY == H-1`; Bottom `origin.Y + def.MinY == 0`; Left `origin.X + def.MinX == 0`; Right `origin.X + def.MaxX == W-1`.

### C.4 Per-tick drag stepping (`DragResolver`)

`TryBegin(board, hooks, b, pointer)`:
```
if IsActive || board.GetStatus(b) != OnBoard return false
if !hooks.AllowsDrag(board, b)  { m_block = b; m_consumed = true; m_active = true; return false }   // pointer is owned but inert until Up (Ice feedback later)
m_axes = hooks.GetAxes(board, b)                     // fixed for the whole drag (DEC-MECH-25)
m_block = b; m_start = m_cur = board.GetOrigin(b)
m_grab = pointer - BoardPoint.FromCell(m_cur)        // kept for nearest picks too: a border press never teleports (AC-3.20)
m_consumed = false; m_active = true; return true
```

`Update(board, hooks, log, pointer)`:
```
if !m_active || m_consumed return Unchanged
def = board.Model.GetBlock(m_block)
desired = pointer - m_grab
desired.X = clamp(desired.X, -def.MinX - 1, (W-1-def.MaxX) + 1)      // at most one cell beyond flush
desired.Y = clamp(desired.Y, -def.MinY - 1, (H-1-def.MaxY) + 1)
if (m_axes & Horizontal) == 0: desired.X = m_cur.X
if (m_axes & Vertical) == 0:   desired.Y = m_cur.Y
target = Cell(RoundHalfAwayFromZero(desired.X), RoundHalfAwayFromZero(desired.Y))
maxSteps = settings.MaxStepsPerTick > 0 ? settings.MaxStepsPerTick : W + H
result = Unchanged
for step < maxSteps:
    dx = sign(target.X - m_cur.X); dy = sign(target.Y - m_cur.Y)
    if dx == 0 && dy == 0 break
    primaryX = |desired.X - m_cur.X| >= |desired.Y - m_cur.Y|             // dominant axis first; tie → X
    p = primaryX ? (dx,0) : (0,dy);  s = primaryX ? (0,dy) : (dx,0)
    if p == Zero: swap(p, s)
    if TryStep(p) { result = Moved; continue }
    if s != Zero && TryStep(s) { result = Moved; continue }               // slide along the free axis
    break                                                                   // blocked on every wanted axis
// exit-on-push, once per tick (DEC-111): the outward push must be at least ExitPushCells
pushX = desired.X - m_cur.X; pushY = desired.Y - m_cur.Y
first  = |pushX| >= |pushY| ? X-axis : Y-axis;  second = the other
for axis in (first, second):
    d = axis == X ? (sign(pushX), 0) : (0, sign(pushY)); push = axis == X ? |pushX| : |pushY|
    if d == Zero || push < settings.ExitPushCells continue
    if !SideMath.TryGetSide(d, out side) continue
    if !MoveRules.IsFlush(board, m_block, m_cur, side) continue
    if ExitRules.Evaluate(board, hooks, m_block, m_cur, side, out c) != Fits continue
    CommitExit(board, hooks, log, c); return Exited
return result

TryStep(d): n = m_cur + d; if !MoveRules.CanOccupy(board, hooks, m_block, n) return false; board.Move(m_block, n); m_cur = n; return true
```

Properties: every landing origin is validated, so tunnelling is impossible even for a pointer jump of many cells (RULE-10); a diagonal blocked in both directions stops (never cuts a corner because intermediate origins are checked); cost bounded by `maxSteps × cells`; a fast pointer catches up over frames; the logical origin is always an integer cell.

`CommitExit(board, hooks, log, c)`:
```
if c.Outcome == Leave: board.Release(m_block, Exited)                       // frees cells at once (RULE-13)
evt = BlockExitedEvent(m_block, c.Door, c.Side, m_cur, c.Outcome)
log.Add(BoardEvent.Exited(evt)); hooks.Publish(in evt)                       // XP-5, command phase only
m_consumed = true                                                             // pointer owns nothing until Up
```
Exactly once: status becomes `Exited` before any later evaluation; the second axis is tried only when the first did not fit; a corner block beside two fitting doors leaves through the axis with the larger push (tie → X).

`End(board, hooks, log)` (pointer Up or Cancel while Playing): if `!m_consumed && m_cur != m_start` → `MoveCommittedEvent(m_block, m_start, m_cur)` to log and hooks; then clear. A drag that exited raises no `MoveCommitted` (DEC-113).

`Interrupt()` (Suspend, Teardown): clears the drag without publishing; if the board changed and no exit happened, stores `m_pending = (block, from, to)`. `FlushPending(hooks, log)` publishes it as the first command of the next command phase (listeners are notified only in the command phase while Playing, RULE-30). Teardown drops the pending record with the attempt.

### C.5 Exit predicate (`ExitRules.Evaluate`)

```
def = board.Model.GetBlock(b)
1 Flush:   if !MoveRules.IsFlush(board, b, origin, side) return NotFlush
2 Extent:  (lo, hi) = SideMath.RunsAlongX(side) ? (origin.X+def.MinX, origin.X+def.MaxX) : (origin.Y+def.MinY, origin.Y+def.MaxY)
           d = board.Model.GetDoorAtLane(side, lo)
           if d < 0: return (any lane in [lo,hi] has a door) ? TooNarrow : NoDoor
           door = board.Model.GetDoor(d); if !door.Covers(lo, hi) return TooNarrow           // whole width (DEC-6)
3 Colour:  if def.Color != door.Color return WrongColor                                     // RULE-14
4 Path:    out = SideMath.GetOutward(side)
           for each cell i: a = origin + def.GetCell(i) + out
               while a inside the board: if !board.IsFloor(a) || (occ = board.GetOccupant(a)) != -1 && occ != b return PathBlocked; a += out
           // whole swept path: concave shapes (L arm, C notch) and future interior voids
5 Hooks:   query = ExitQuery(b, d, side, origin); outcome = Leave
           if !hooks.AllowsExit(board, in query, out outcome) return Vetoed                      // XP-4, AND
candidate = ExitCandidate(d, side, outcome); return Fits
```

Pure and non-mutating, so the editor solver calls it for any origin (AR-10). Only `Fits` passes; the verdict order affects only the reported reason. Adjacent same-colour doors are separate doors (each lane maps to one door index; `Covers` is per door).

**When an exit commits.** Only inside `DragResolver.Update` (command phase) when the flush block is pushed outward by at least 0.3 cell and the verdict is `Fits`. A release while flush without that push keeps the block (DEC-111 reading of DEC-50 and DEC-25; OQ-8 stays open for the phone feel test).

### C.6 Visual follow offset (`FollowOffset.Compute`)

```
dx = (axes & Horizontal) != 0 ? desired.X - origin.X : 0;  dy likewise
lx = Lead(dx, Cell(sign dx, 0)); ly = Lead(dy, Cell(0, sign dy))
Lead(delta, d):
    if |delta| < 1e-4 return 0
    if MoveRules.CanOccupy(board, hooks, block, origin + d) return clamp(delta, -lead, lead)
    if SideMath.TryGetSide(d, out side) && MoveRules.IsFlush(board, block, origin, side)
       && ExitRules.Evaluate(board, hooks, block, origin, side, out _) == Fits return clamp(delta, -lead, lead)
    return 0                                                                  // blocked: no visible overlap
if lx != 0 && ly != 0 && !MoveRules.CanOccupy(board, hooks, block, origin + Cell(sign lx, sign ly)):
    zero the lead with the smaller magnitude                                  // no corner overlap
return BoardPoint(lx, ly)
```

Runs only for the held block in view sync: at most three `CanOccupy` calls plus one `Evaluate` when flush, no allocation. Visual position = `CellToLocal(origin) + (lx, 0, ly)` is continuous across a step: when the pointer crosses +0.5 the origin advances and the offset flips to −0.5+ε, so the view follows the finger 1:1 without a jump (DEC-112). The lean toward a usable door reaches 0.3 and the exit commits in the next command phase.

### C.7 Release snap

The logical origin is already the last reachable cell on the path (RULE-11, AC-3.3), so no snap search exists. The presenter's `SetHeld(-1)` on release runs `Tween.StopAll(t)` then `Tween.LocalPosition(t, layout.CellToLocal(origin), k_settleSeconds, Ease.OutQuad)` on the block `Transform`. Suspend and Teardown use `SnapHeld()` (a direct `localPosition` write, no tween) so no tween is created and then paused.

### C.8 Picking (`DragInputRouter` on Down, then `BlockPicker.Pick`)

Router gating, in this order (AC-3.13, AC-3.21, AC-BST-0.5):
```
sample = pointer.Read()
if sample.Phase == Down:
    if m_ownedUntilUp return                                                 // a second Down while owned is impossible with one primary pointer; guard anyway
    m_ownedUntilUp = true; m_inert = true
    if ui.IsOverUi(sample.ScreenPosition) return                             // UI-owned press: stays inert until Up
    if !projector.TryScreenToBoard(sample.ScreenPosition, out bp) return
    if !InteractionArea.Contains(bp, W, H) return
    b = picker.Pick(new ScreenPoint(x, y), board, projector.Footprint)      // once per press
    if b < 0 return                                                          // empty board: nothing
    if drag.TryBegin(board, hooks, b, bp) { m_inert = false; presenter.SetHeld(b) }
elif sample.Phase == Held && !m_inert:
    if projector.TryScreenToBoard(pos, out bp) m_lastPointer = bp            // a pointer that leaves the plane keeps the last point
    drag.Update(board, hooks, log, m_lastPointer)                            // may return Exited → presenter.SetHeld(-1) happens through the drag consumed flag
elif sample.Phase == Up || Cancel:
    if !m_inert drag.End(board, hooks, log); presenter.SetHeld(-1)
    m_ownedUntilUp = false; m_inert = true
```
Future flags (booster targeting, opening hold) are checked before the UI test; they are fields on `LevelAttempt` that no code sets yet.

`BlockPicker.Pick(p, board, projector)`:
```
for gy in 0..H: for gx in 0..W: m_corner[gy*(W+1)+gx] = projector.Project(gx - 0.5f, gy - 0.5f)   // cell (x,y) spans [x-0.5, x+0.5]
best = -1; bestD2 = +inf
for b in 0..BlockCount-1 ascending:                                         // strict < keeps the lowest index on ties
    if board.GetStatus(b) != OnBoard continue                                // exited / mid-exit excluded
    origin = board.GetOrigin(b); def = board.Model.GetBlock(b)
    for each cell c of def: a = origin + c
        q0..q3 = corners (a.X, a.Y), (a.X+1, a.Y), (a.X+1, a.Y+1), (a.X, a.Y+1)
        d2 = InsideConvexQuad(p, q) ? 0 : min over the four edges of PointSegmentDistanceSq(p, edge)
        if d2 < bestD2 { bestD2 = d2; best = b }
return best                                                                  // d2 == 0 is a direct hit; no radius cutoff
```
`InsideConvexQuad` uses the sign of the four edge cross products (all ≥ 0 or all ≤ 0). Only occupied cells count, so the empty corner of an L is not footprint. Cost O((W+1)(H+1) + total cells) once per press; all buffers are preallocated in the constructor.

`CameraBoardProjector`:
- plane: point `P = boardRoot.TransformPoint(0, planeHeight, 0)`, normal `n = boardRoot.up`.
- `TryScreenToBoard`: `ray = camera.ScreenPointToRay(screen)`; `denom = dot(n, ray.direction)`; if `|denom| < 1e-6` false; `t = dot(n, P - ray.origin) / denom`; if `t < 0` false; `local = boardRoot.InverseTransformPoint(ray.GetPoint(t))`; `point = BoardPoint(local.x, local.z)`.
- `Project(x, y)`: `s = camera.WorldToScreenPoint(boardRoot.TransformPoint(x, planeHeight, y))`; return `ScreenPoint(s.x, s.y)` (points behind the camera cannot occur for a framed board; if `s.z < 0` return a far point so the cell never wins).
- Drag and pick share the plane so the grab offset is consistent. `planeHeight = ExitMotionSettings.k_blockTopHeight`.

### C.9 Won detection

Checked once per tick, after the command phase and before the timer slot: `if simulation.IsCleared → EnterWon; return`. A last exit in the frame the timer would reach zero therefore gives Won (RULE-18, DEC-29, V-E16). Boosters later reduce `RequiredRemaining` through the same `Release` and are covered by the same check.

### C.10 Finish deadline (DEC-93)

`EnterWon`:
1. `m_machine.TryApply(AllRequiredCleared)` latches; `m_state.SetValue(Won)`.
2. `m_pointer.Disable()`; `m_router.Reset(attempt)` (drag is already consumed; no event); `m_presenter.SnapHeld()`.
3. `m_countdown.Stop()`.
4. Reward hook slot: empty (save task).
5. `m_presenter.Sync(simulation, m_router.LastPointer)` once: starts the final exit motion from this tick's log. Presentation only.
6. `seconds = min(0.8f, m_presenter.FinishSeconds)`; if `seconds <= 0` → `CompleteFinish()`; else `m_finish = Tween.Delay(this, seconds, static s => s.CompleteFinish(), useUnscaledTime: true)`; `m_finishPending = true`.

`CompleteFinish()`: if `!m_finishPending` return; clear the flag; `m_finish.Stop()`; `Teardown()`. Idempotent; the callback captures nothing (target overload). Forced completion: `NotifyBackground(true)`, `GoHome`, `Restart`, `Dispose` call it first. Exit motions last at most `k_maxSeconds = 0.6`, under the bound. The deadline runs on PrimeTween's own loop (already present) with a stored handle; no second tick owner and no `OnityTask` runner is introduced.

### C.11 Zero-allocation strategy per hot-path call

| Call (Playing tick) | Strategy |
|---|---|
| `GameplayPointerInput.Read` | `WasPressedThisFrame`, `IsPressed`, `WasReleasedThisFrame`, `ReadValue<Vector2>()` on cached `InputAction` fields; returns a struct. No callbacks, no `CallbackContext`. |
| `UiToolkitPointerFilter.IsOverUi` | Only on Down. `panel.Pick(RuntimePanelUtils.ScreenToPanel(panel, p))` over a cached `IPanel[]`; UNPROVEN whether `Pick` allocates. |
| `CameraBoardProjector` | Struct math only (`ScreenPointToRay`, `TransformPoint`, `WorldToScreenPoint`). |
| `BlockPicker.Pick` | Preallocated `ScreenPoint[]`; loops over arrays. |
| `DragResolver.Update` | Loops over `Cell` structs; `BoardState` arrays; `AttemptHooks` arrays iterated with `for`; `ExitRules.Evaluate` has `out` structs only. |
| `BoardEventLog.Add` | Writes into a preallocated `BoardEvent[]`. |
| `AttemptHooks.Publish` | `for` over `IBoardEventListener[]` with `in` struct parameters: no boxing, no delegates. |
| `BoardPresenter.Sync` | Reads `BoardEventLog` by `ref readonly`; `Transform.localPosition` writes; `Tween.LocalPosition` returns a struct handle (PrimeTween pools its tweens; capacity default 200); `Tween.isAlive` polling; `ModularBlockView.SetClipPlane` writes an existing MPB. No closures, strings, LINQ. |
| `ReactiveProperty<SessionState>.SetValue` | Only on transitions, never in steady state. |
| `Time.deltaTime` | Read once in `GameplayTickOwner.Tick`. |

Load-time allocations (Home): conversion, `BoardSimulation`, factory `Prepare`, `Place`, hook builder. Recorded for the README loop/allocation table.

---

## D. Session and lifecycle

### D.1 States and the transition table (`SessionTransitions.TryGetTarget`)

| From | Request | To | Reason recorded |
|---|---|---|---|
| Home | OpenPreLevel, ClosePreLevel | Home | — |
| Home | Play | Playing | `AttemptNumber++` |
| Playing | Pause | Paused | User |
| Playing | Background | Paused | Background |
| Playing | BoosterCommit | Paused | Saving |
| Playing | ZeroStockTap | Paused | Purchase |
| Paused | Resume | Playing | reason cleared |
| Paused | CommitSucceeded, CommitFailed | Playing | reason cleared |
| Paused | PurchaseClosed | Playing | reason cleared |
| Paused | Background | Paused | reason becomes Background (latch) |
| Paused | Restart | Playing | `AttemptNumber++` |
| Paused | Home | Home | — |
| Playing | AllRequiredCleared | Won | — |
| Playing | TimerExpired | Failed | — |
| Won | NextLevel | Playing | `AttemptNumber++` (guarded by the future reward commit) |
| Won | Home | Home | — |
| Failed | Home | Home | — |
| Failed | Restart | Playing | `AttemptNumber++` |

Every other pair returns false and changes nothing (AC-5.6). Terminal latch (RULE-17, V-E4): no row leaves Won or Failed on `AllRequiredCleared` or `TimerExpired`, so a second terminal request is rejected by the table; `LevelSession` also ignores tick work outside Playing. Saving and Purchase rows exist only in the table; nothing calls them yet.

### D.2 Ownership

`LevelSession` is bound `BindInterfacesAndSelfTo<LevelSession>().AsSingle()` in the Game `SceneContext`, so Onity disposes it with the scene (`AsSingle` singletons are disposed; `BindInstance` objects are not).

| Resource | Owner | Stop point |
|---|---|---|
| `SessionStateMachine`, `ReactiveProperty<SessionState>` | `LevelSession` | `Dispose` (scene unload) disposes the property |
| `LevelAttempt` (simulation, `AttemptLifetime` CTS + `CompositeDisposable`) | `LevelSession` | Teardown steps 4–5; a new attempt gets a new lifetime (`CompositeDisposable.Dispose` is final) |
| `InputActionMap "Gameplay"` | `GameplayPointerInput` (scene singleton, `IDisposable`) | Disabled by Suspend/Teardown; disposed with the container. Enabled only by `Play` and `Resume`. |
| Block roots (`BlockPieceFactory`) | `BoardPresenter` (scene singleton, `IDisposable`) | Returned at every Teardown; factory disposed on level change and in `Dispose` |
| Exit and settle tweens | `BoardPresenter`; target = the piece `Transform` | `Tween.SetPausedAll(true, t)` on Suspend; `Tween.StopAll(t)` at Teardown step 3; completion polled by `isAlive` in `Sync` |
| Finish deadline (`Tween.Delay`, target = `LevelSession`) | `LevelSession` | Stopped in `CompleteFinish` and Teardown step 3 |
| Attempt build task | `GameAttemptLoader.LoadAsync(resolver, ct)` | Onity awaits it (`RegisterBuildCallbackAsync`); the scene build token cancels it; it is never fire-and-forget |
| `ApplicationPauseRelay` | scene GameObject | Destroyed with the scene; no `Update` |
| Environment pieces | `EnvironmentView` (unchanged) | Its own `Clear`/`Release` |

### D.3 Operations (fixed steps)

| Operation | Allowed from | Steps |
|---|---|---|
| `TryPrepare(level)` | Home; allocation allowed | `hooks = new AttemptHooksBuilder().Build()` (the future registry adds hooks here); `simulation = new BoardSimulation(level.Model, hooks, DragSettings.Default)`; `attempt = new LevelAttempt(level, simulation, ++m_generation)`; `presenter.Build(level)` (prepares pools the first time this level is shown); `presenter.Place(simulation.Board)` |
| `Play()` | Home with an attempt prepared | `TryApply(Play)`; `countdown.Start(model.TimerSeconds)`; `pointer.Enable()`; state → Playing |
| **Suspend** (`Pause(reason)`, `NotifyBackground(true)` in Playing) | Playing | 1 `pointer.Disable()`; `router.Reset(attempt)` (`drag.Interrupt()`, keeps a pending `MoveCommitted`); `presenter.SnapHeld()`. 2 `countdown.SetPaused(true)`. 3 `presenter.SetTweensPaused(true)` (`Tween.SetPausedAll(true, t)` per checked-out piece). Then state → Paused(reason). |
| `Resume()` | Paused | `TryApply(Resume)`; `presenter.SetTweensPaused(false)`; `countdown.SetPaused(false)`; `pointer.Enable()`. The pending `MoveCommitted` flushes as the first command of the next tick. |
| **Teardown** (Won after Finish, Failed, Home, Restart, NextLevel, Dispose) | any with an attempt | 1 `pointer.Disable()`; `router.Reset(attempt)`; `presenter.SnapHeld()`. 2 `countdown.Stop()`. 3 `presenter.StopTweens()` (`Tween.StopAll(t)` per piece); `m_finish.Stop()`; `m_finishPending = false`. 4 `attempt.Lifetime.Cancel()`. 5 `attempt.Lifetime.Dispose()` (disposes the `CompositeDisposable`). 6 `presenter.ReturnPieces()` (clip reset, `factory.Return`). Then `attempt = null`. |
| `EnterFailed` (timer slot) | Playing | `TryApply(TimerExpired)`; Teardown immediately (static capture slot noted, out of scope). |
| `Restart()` | Paused, Failed | Teardown; `TryPrepare(sameLevel)` (the immutable `AttemptLevel`; `presenter.Build` returns early for the same level, pools survive); `Play()`. Nothing crosses attempts (AC-5.3, V-E11). |
| `GoHome()` | Paused, Won, Failed | `CompleteFinish()` if pending; Teardown; state → Home. Scene navigation is the Hub task's. |
| `Dispose()` | scene unload | `CompleteFinish()`; Teardown if an attempt exists; dispose the reactive property. Unity-null guard: `BoardPresenter.ReturnPieces`/`Dispose` skip factory calls when the blocks root is already destroyed and only drop references. |

### D.4 Backgrounding without a pause UI

- `ApplicationPauseRelay.OnApplicationPause(true)` → `session.NotifyBackground(true)`: Playing → Suspend with reason Background (AC-3.12); Won with Finish pending → `CompleteFinish()` (forced cleanup, DEC-93); Paused → reason latched to Background; Home/Failed → nothing.
- `OnApplicationPause(false)` → `session.NotifyBackground(false)`: if state is Paused(Background) and `pauseOverlay.ResumesOnForeground` → `Resume()`. Otherwise stay Paused (the PROJECT.md latch). `NoPauseOverlay(true)` is the interim binding; the pause-popup task binds a real overlay and removes the flag (DEC-116).
- No `OnApplicationFocus` handling: the Input System releases the press control on focus loss, which the next tick reads as Up.

### D.5 Input map

`GameplayPointerInput` constructor: `m_map = new InputActionMap("Gameplay")`; `m_point = m_map.AddAction("Point", InputActionType.Value, "<Pointer>/position")` with `expectedControlType: "Vector2"`; `m_press = m_map.AddAction("Press", InputActionType.Button, "<Pointer>/press")`; `m_touchPhase = m_map.AddAction("TouchPhase", InputActionType.Value, "<Touchscreen>/primaryTouch/phase")`. `<Pointer>` covers Mouse, Pen and Touchscreen (primary touch only), so secondary touches never bind (DEC-26). `Enable`/`Disable` forward to the map. `Read()`:
```
pos = m_point.ReadValue<Vector2>()
if !m_map.enabled: phase = m_held ? Cancel : None; m_held = false; return
if m_press.WasPressedThisFrame() && !m_held: m_held = true; m_device = m_press.activeControl?.device; return Down
if m_held && m_press.activeControl != null && m_press.activeControl.device != m_device: m_held = false; return Cancel
if m_held && m_press.WasReleasedThisFrame(): m_held = false; return (m_touchPhase.ReadValue<TouchPhase>() == Canceled ? Cancel : Up)
if m_held && m_press.IsPressed(): return Held
if m_held: m_held = false; return Cancel                                  // press vanished without a release event
return None
```
`Dispose()` disables and disposes the map. `WasPressedThisFrame` ignores enablement, so the session state gate in the tick owner is the real guard. `ReadValue<TouchPhase>` is UNPROVEN on device; if it throws in EditMode it is read only when `m_device is Touchscreen`.

---

## E. Tick owner phases

`GameplayTickOwner` is bound `BindInterfacesAndSelfTo<GameplayTickOwner>().AsSingle()` in `GameSceneInstaller`; Onity collects it as the container's only `IOnityTickable`.

```csharp
/// <summary>The project's single IOnityTickable: forwards one frame to the session only while Playing.</summary>
public void Tick()
{
    if (m_session.State.Value != SessionState.Playing) return;   // Home (async build still running), Paused, Won, Failed
    m_session.RunTick(Time.deltaTime);
}
```

`LevelSession.RunTick(float deltaTime)`:
```
attempt = m_attempt; sim = attempt.Simulation
sim.Events.Clear()                                  // the log holds exactly this tick's events
// Phase 1: queued commands (RULE-18, AR-4, DEC-59)
sim.Drag.FlushPending(sim.Hooks, sim.Events)        // 1a a MoveCommitted deferred by an interruption
m_router.Run(attempt, m_presenter)                  // 1b the pointer command: pick / drag / exit / release
                                                    // 1c booster intents: empty slot; a Saving pause returns here
if (State != Playing) return
// Won check: once, after the command phase (RULE-29)
if (sim.IsCleared) { EnterWon(); return; }
// Phase 2: timer slot (ICountdown; InactiveCountdown now)
if (m_countdown.Tick(deltaTime)) { EnterFailed(); return; }
// Phase 3: view sync
m_presenter.Sync(sim, m_router.LastPointer)
```

`BoardPresenter.Sync(sim, pointer)`:
1. For `i < sim.Events.Count`: `ref readonly e = sim.Events.GetAt(i)`; if `e.Kind == Exited` → `StartExit(e.Block, e.Side)`: `Tween.StopAll(t)`; compute the world clip plane (C.12 below); `piece.View.SetClipPlane(plane, true)`; `m_exitTween[b] = Tween.LocalPosition(t, layout.CellToLocal(origin) + outward * (depth + 1), dur, Ease.InQuad)` with `dur = clamp(k_secondsPerCell * (depth + 1), k_minSeconds, k_maxSeconds)`, `depth = def.Depth(side)`; `m_exiting[b] = true`; `m_lastExitSeconds = dur`.
2. For each `b` with `m_exiting[b]` and `!m_exitTween[b].isAlive`: `SetClipPlane(default, false)`; `factory.Return(piece)`; `m_pieces[b] = null`; `m_exiting[b] = false`.
3. Held block (`sim.Drag.IsActive && !sim.Drag.IsConsumed`): `off = sim.Drag.GetViewOffset(board, hooks, pointer)`; `t.localPosition = layout.CellToLocal(origin) + new Vector3(off.X, 0, off.Y)`.

`SetHeld(b)`: records `m_held`; on `b == -1` with a previous held block still on board: `Tween.StopAll(t)`; settle tween. (Outline task hooks in here.) `SnapHeld()`: writes `CellToLocal(origin)` directly and clears `m_held`.

Clip plane (C.12): `outwardLocal = (o.X, 0, o.Y)` from `SideMath.GetOutward(side)`; `edgeCoord = side == Top ? H-1+0.5 : side == Bottom ? -0.5 : side == Left ? -0.5 : W-1+0.5`; `lineLocal = along the outward axis at edgeCoord + k_clipOffsetCells * sign(outward)`, the other coordinate 0; `P = boardRoot.TransformPoint(lineLocal)`; `n = boardRoot.TransformDirection(-outwardLocal)` (inward); `plane = (n.x, n.y, n.z, -dot(n, P))`. `BoardLit.shader` keeps `dot(float4(posWS,1), plane) >= 0`, the board side, so geometry beyond the door line disappears. The offset 0.25 puts the plane at the door piece's centre line (`DoorSlot.LocalPosition` is `cell + outward * 0.75`); UNPROVEN visually.

Pool return resets the clip anyway (`BlockPiece.OnPoolRelease → ModularBlockView.Clear → BoardPieceView.OnPoolRelease → ResetState`), so the explicit reset is a guard, not a dependency.
