---
title: "Gameplay: Drag and Gate"
created: 2026-09-25
modified: 2026-09-28
type: architecture
status: implemented
tags: [gameplay, domain, session, input, doors, commands]
---
# Gameplay: Drag and Gate

This document covers block drag, the base colour-door (gate) exit and the move commands that apply them. It lists the rules, where each one lives, and what runs when.
- Tasks: `production/tasks/TASK-20260925-012336316-a88e25db-block-drag-gate-exit`, `production/tasks/TASK-20260925-151032072-f1e30581-move-commands` and, for the exit fragments, `production/tasks/TASK-20260927-010019989-11b501c1-gate-exit-fragments`.
- Decisions: DEC-110 to DEC-117, DEC-122 and DEC-131 in `PROJECT.md`.
- Requirements: PRD RULE-9..RULE-18, RULE-28..RULE-30, AC-3.1..AC-3.4, AC-3.13, AC-3.19..AC-3.21, AC-4.12, AC-5.6.

## Assemblies

| Assembly | Folder | Holds | References |
| --- | --- | --- | --- |
| `ColorBlockJam.Domain` | `Assets/ColorBlockJam/Runtime/Domain` | Board model, occupancy, movement, exit, picking, hooks, session table | none (`noEngineReferences`) |
| `ColorBlockJam.Gameplay` | `Assets/ColorBlockJam/Runtime/Gameplay` | Level conversion, session, tick owner, pointer input, presenter, Game-scene installer | Domain, Runtime, Presentation, Environment, Onity, PrimeTween, Input System |
| `ColorBlockJam.Gameplay.EditModeTests` | `Assets/ColorBlockJam/Tests/Gameplay/EditMode` | Rules, session with fakes, input, architecture and zero-allocation tests | test only |
| `ColorBlockJam.Gameplay.PlayModeTests` | `Assets/ColorBlockJam/Tests/Gameplay/PlayMode` | Presenter pooling and exit motion, Won Finish deadline | test only |

`ColorBlockJam.Runtime` references neither Domain nor Gameplay, so runtime contracts never depend on presentation (DEC-110).

## Flow of one attempt

1. **Home.** `GameAttemptLoader` waits for the project scope to load the level JSON. It then does the following:
   - it converts the selected `LevelData` into an immutable `LevelModel` (`AttemptLevelConverter`);
   - it builds the environment;
   - `LevelSession.TryPrepare` creates the attempt: the hooks from `IAttemptHookSource`, a `BoardSimulation`, pooled block roots from `BoardPresenter`, and a per-attempt `CancellationTokenSource` and `CompositeDisposable`.
   It frames `BoardCamera` and calls `Play`. Auto-play is interim until Hub selection exists (DEC-116).
2. **Playing.** `GameplayTickOwner`, the project's only `IOnityTickable`, calls `LevelSession.RunTick` only while the state is Playing. Each tick runs these phases in order:
   1. **Commands.** Every board change here is a command run by `BoardSimulation.Commands` (see [Commands](#commands)).
      - A committed booster intent, or the opening boosters (Extra Time, then Rocket), apply first and once ([Booster Gameplay](Booster-Gameplay.md) B-15).
      - A move that Suspend interrupted runs next.
      - `DragInputRouter` then reads the primary pointer: pick, drag preview, and the command a push-out or a release produces.
      - An attached `ICommandSource` (a replay, a bot) may then submit one command, only while no drag is held.
      - An accepted booster target enters `Paused(Saving)` inside the pointer step, and the tick returns at once: no Won check, timer or sync runs in it. The opening hold skips the pointer and the source.
   2. **Won check.** When no required block remains, the session enters Won and stops.
   3. **Timer slot.** `AttemptCountdown` consumes a running Time Freeze first, then the main clock; the opening hold spends its Playing time before either (DEC-148).
   4. **View sync.** `BoardPresenter` starts exit motions with their fragment bursts, returns finished exits and bursts to their pools and places the held block.
3. **Won.** The latch rejects further commands at once. Input and clocks stop, and only the final exit motion plays, for at most 0.8 foreground seconds. Teardown follows (DEC-93).
   - Backgrounding, Home and scene unload force the cleanup immediately.

`LevelSession` publishes state changes after their owned transition work is ready. Won stops input and clocks, syncs the final exit and installs its Finish deadline (or completes cleanup) before notifying observers; Failed completes Teardown first. Subscribers can therefore request Home or Restart synchronously without interrupting old-attempt cleanup. After `Dispose`, requests return false and ticks, background notifications and Finish callbacks do nothing. `LevelSessionTests` covers these observer and disposal paths.

## Rules and where they live

| Rule | Code |
| --- | --- |
| A block cell is floor, inside the board, not another block's cell, and allowed by every cell gate (RULE-2, RULE-3) | `Domain/Movement/MoveRules.CanOccupy` |
| Movement is cell by cell, dominant axis first (tie X), sliding on the free axis, bounded steps, never passing through anything (RULE-10). The drag moves a preview; the board changes only when its command runs. | `Domain/Movement/DragResolver.Update` over `Domain/Board/PreviewBoard` |
| Release keeps the last reached cell (RULE-11): it becomes one `MoveBlock` command from the start to that cell | `DragResolver.TryEnd`, `MoveBlockHandler`; the presenter settles the view |
| Exit (RULE-12..14):<br>- flush against the side;<br>- one door span covers the block's whole extent along that side;<br>- colours match;<br>- every non-own cell on the outward swept path is floor, empty and allowed by every cell gate, so a closed board object in a notch blocks the exit;<br>- every exit gate agrees. | `Domain/Exit/ExitRules.Evaluate` (pure; the editor solver can reuse it) |
| The exit commits once the held block touches a door it fits and, as drawn, lines up with it within 0.2 cell (`DragSettings.ExitAlignCells`), with no outward push (DEC-164, superseding the 0.3 cell push of DEC-111). A block drawn half a cell beside or short of the door is not taken yet; one pushed into the wall beside the door is, since it is drawn on its cell. The exit then runs straight out: only the outward coordinate changes. The drag produces one `MoveBlock` command with the exit side, and running it frees the cells, logs `BlockExited` and publishes to listeners once. | `DragResolver` exit step, `MoveBlockHandler` |
| A wrong-colour or too-narrow door is never occupiable, so it acts as a wall | `MoveRules.CanOccupy` (doors are outside the board) |
| Arrow (BLK-01, RULE-34):<br>- a Horizontal block changes only its column, a Vertical block only its row; a diagonal pointer is projected onto the axis;<br>- it exits only through a door on a side its axis points to (Left/Right or Top/Bottom); a perpendicular exit is `Vetoed` for every caller;<br>- the axis is fixed for the attempt and keyed by the footprint's authored source, so a Rocket part keeps its parent's axis. | `Domain/Hooks/ArrowAxisConstraint` (XP-2), `Domain/Hooks/ArrowExitGate` (XP-4), registered by `Gameplay/Effects/ArrowEffectModule` |
| Picking (DEC-91, DEC-115):<br>- a direct hit, else the nearest projected occupied footprint;<br>- ties go to authored order;<br>- exited blocks are excluded;<br>- it runs once per press. | `Domain/Picking/BlockPicker`, `InteractionArea` |
| UI presses and presses outside the area never select (AC-3.13, AC-3.21) | `DragInputRouter`, `UiToolkitPointerFilter` |
| Visual follow (DEC-112):<br>- up to 0.5 cell toward a free cell;<br>- never toward a blocked cell or a door (a fitting door is exited on contact, DEC-164). | `Domain/Movement/FollowOffset` |
| Session states, the transition table and the terminal latch (AC-5.6, RULE-17) | `Domain/Session/SessionTransitions`, `SessionStateMachine` |

## Commands

Every player move is one command, and one executor applies it (DEC-122). The pointer, a replay and any other system submit the same data, so a move can be logged, saved and played back.

| Part | Role | Code |
| --- | --- | --- |
| `GameCommand` | Plain data: `Kind`, `Block` (the runtime block ID; IDs below `LevelModel.BlockCount` are the authored indices), `From`, `To`, `Option`. `MoveBlock` means "the block standing on `From` travels to `To`"; `Option` 0 keeps it there and 1 + `Side` leaves through that side. | `Domain/Commands/GameCommand` |
| `CommandExecutor` | The invoker: runs the handler of the command's kind and logs every applied command. It is the only way the board changes during play; `BoardState.Move`, `Release` and `Reset` are internal to the Domain. | `Domain/Commands/CommandExecutor` |
| `MoveBlockHandler` | The receiver, internal to the Domain so only the executor runs it. It checks everything first, in this order: the block is on the board at `From`, the drag gates allow it (XP-1), the move and exit side stay on its axes (XP-2), `To` is reachable in single validated steps (`MoveReach`, XP-3), and an exit fits at `To` (XP-4). Then it moves once and raises one event: `BlockExited`, or `MoveCommitted` (DEC-113). A refusal returns its `CommandOutcome` and changes nothing. | `Domain/Movement/MoveBlockHandler` |
| `CommandLog` | The attempt's applied commands in order, 1,024 slots allocated with the attempt. A full log stops recording and sets `IsTruncated`; play continues. Each slot is a `CommandRecord`: the command plus `Turn` (1-based applied order), `Tick` (1-based Playing tick) and `Seconds` (Playing time so far). `LevelSession.RunTick` calls `CommandExecutor.AdvanceTick(deltaTime)` first, so pause, Saving and Home add no time. The stamps stay out of `GameCommand`, so `ToArray()` still feeds a replay whose commands equal the recording; `GetRecordAt`/`ToRecords()` read the stamps. | `Domain/Commands/CommandLog` |
| `ICommandSource` | Another driver. The session asks it for one command per command phase while no drag is held or interrupted and the board is not cleared, then reports the outcome. `LevelSession.TrySetCommandSource` attaches it to the current attempt; Teardown drops it. | `Domain/Commands/ICommandSource`, `LevelSession` |
| `ReplayCommandSource` | Plays a recorded list in order and stops at the first refusal, reporting its index and outcome. | `Domain/Commands/ReplayCommandSource` |

**The drag is a preview.** `DragResolver` steps the held block over a `PreviewBoard`, which reads exactly as `BoardState` would after `Move(block, origin)` without writing it. `BoardSimulation.Preview` is that board as the player sees it; the presenter reads it for positions. So the feel (follow lead, contact exit, bounded steps) is unchanged, while the committed board changes only when the command runs. Suspend parks the reached move; it runs first after Resume, and Teardown discards it.

**Determinism.** Handlers use integers, a fixed neighbour order and the attempt's hooks only, so the same level and the same command list give the same board and the same events (`ReplayTests`). This rests on one rule for every hook (XP-1..XP-4): **read the `IBoardReader` it is passed, never a cached `BoardState`**, and change its own state only in XP-5 listeners. The drag and the reach search ask hooks about preview boards.

**JSON later.** The log already holds everything a replay needs. The saving task maps `Block` to `AttemptLevel.GetBlockId` and back, and writes the level ID and a rules version next to the list.

**Boosters.** Rocket, Hammer and Vacuum are commands too, run by the same executor and logged for replay (DEC-146, DEC-147; details in [Booster Gameplay](Booster-Gameplay.md)). Time Freeze and Extra Time are clock operations, not commands (DEC-148).

| Kind | Command | Result |
| --- | --- | --- |
| `RemoveCell = 2` (Rocket) | `Block`, `From` = origin at capture, `To` = the absolute cell | removes the cell; each remaining 4-connected part becomes a block |
| `RemoveBlock = 3` (Hammer) | `Block`, `From` = origin | removes the block whole, Ice included |
| `RemoveColor = 4` (Vacuum) | tapped `Block`, `From` = origin, `Option` = colour | removes every on-board block of that colour |

- New outcomes: `FinalCell = 11` (Rocket on a block's only cell), `NotBlockCell = 12`, `ColorMismatch = 13`. Exited or removed targets give `NotOnBoard`. Booster targets are not drag-gated, so Ice and Arrow blocks are valid.
- **Runtime block IDs.** `BoardState` keeps every block's cells in one flat buffer sized to `LevelModel.TotalCellCount`. A split appends new IDs and never reuses one; `IBoardReader.BlockCount` is the number of IDs issued so far, so `for block < BlockCount` loops cover fragments. The part holding the first remaining cell keeps the parent ID. `Reset()` restores the authored blocks.
- **`BlockFootprint`.** `IBoardReader.GetFootprint(block)` returns a readonly struct (`CellCount`, `GetCell(i)`, bounds, `Depth(Side)`, `Color`, `Source` = the authored index). Every rule (moves, reach, drag, exit, picking, preview) reads it, never the authored model, so no rule sees a stale shape after a split.
- **Events.** A split raises one `FootprintChanged` per part, parent first (`Block == Parent`), then the new IDs in ascending order; `BoardEvent.Parent` names the parent and `BoardEvent.From` carries the removed cell. A removal raises `BlockRemoved` with `Cause` (the command kind) and `Source` (the tapped block for Vacuum, the block itself for Hammer), carried as `BoardEvent.Cause` and `BoardEvent.Parent`. Neither is an exit, so exit listeners and Ice counters do not advance. The event log holds `TotalCellCount + 4` entries.

**Headless use.** Tools, tests and solvers may run `BoardSimulation.Commands` without a session. They clear `BoardSimulation.Events` before each command, as a tick does; a full tick log throws before the board changes.

**Replay presentation.** A block moved by a command the pointer did not show settles in a straight line onto its new cell. Its exit starts from its exit cell, as a pushed-out block does. A paced replay along the found path is a follow-up.

## Exit fragments

A valid exit (a `Leave` outcome) bursts same-colour cubes out of the door while the block slides through it (visual reference 15, DEC-131). It ports the look of the CBJ Template's `BlockDestructionParticle` without its prefab, static pool or material instance.

- **Where.** `ExitFragmentPlacement` puts the emitter on the door line (`GetDoorLine`: the floor edge plus `ExitMotionSettings.k_clipOffsetCells`), centred across the lanes the block covers along the door, at the block body's middle height, with +Z pointing out of the board. `BoardPresenter.GetClipPlane` reads the same line, so the cubes appear where the block disappears.
- **What.** `ExitFragmentEmitter` is a Mesh-mode `ParticleSystem` built in code: Unity's built-in Cube and the shared `MAT_Gameplay` in the block's palette colour through one cached `MaterialPropertyBlock`. A Box as wide as the lanes emits 40 cubes per lane per second for the exit's duration. Each cube lives 0.52 s, leaves at speed 9 and is damped to a stop, is 0.1–0.4 in size, spins in 3D and shrinks to nothing. The values live in `ExitFragmentSettings`.
- **Lifecycle.** `ExitFragments` owns one Onity pool, warmed on Home to `min(block count, 4)`. `StartExit` plays one burst; a full pool skips it and never grows. `Sync` returns a burst once its last cube dies. `SetTweensPaused` pauses and resumes it, `StopTweens` stops and clears it, `ReturnPieces` returns it and `Dispose` destroys the emitters. The Won Finish shows the final burst until Teardown clears it; `FinishSeconds` is unchanged.
- **Why built in code.** No prefab, scene or installer change was needed while other tasks owned `GameSceneInstaller` and `Game.unity`, and the tuning is explained in one place. A prefab can later replace the emitter's configuration without changing the pool or the presenter.

## Held outline

While a pointer holds a block, and while Vacuum removes a target, one white outline follows the block's whole silhouette (visual reference 14, the user's 2026-09-28 reference, DEC-89, DEC-145). It comes from the Highlight Plus 2 asset (URP package, `Assets/HighlightPlus`, v32.6.1), chosen by the user on 2026-09-28 over our own footprint-traced mesh.

- **Effect.** `HeldBlockHighlight.Prepare` adds one `HighlightEffect` to a block root on Home (`BoardPresenter.Place`) and re-reads its renderers: white outline, `Highest` quality, group `Children`, contour around the object shape, `AlwaysOnTop`, no glow, inner glow or overlay. Highest quality composes the outline in screen space around all the root's modular tiles together, so no seam shows between tiles, and it stays visible next to walls and neighbouring blocks.
- **Switching.** `SetHeld` switches the held root's outline on. Release, `SnapHeld` (Suspend and Teardown), an exit start and `ReturnPieces` switch it off. A Vacuum target (`StartRemoval`, `RemoveColor`) switches on when its removal starts, and `ReturnPiece` switches every root off, so no root goes back to its pool outlined. A Rocket split part, and a root a Rocket recomposed, is prepared (its tiles re-read) the first time it is switched on again, so the Rocket itself allocates nothing for the outline.
- **Rendering.** `HighlightPlusRenderPassFeature` is on the three URP renderers (`Settings/URP-{Balanced,HighFidelity,Performant}-Renderer.asset`). The vendor code is unchanged; `HighlightPlus.Runtime` and `HighlightPlus.Editor` asmdefs were added so `ColorBlockJam.Gameplay` can reference it.
- **Third-party loop (Performance Rule 6).** Each `HighlightEffect` is a MonoBehaviour on a pooled block root. The render feature enqueues its pass every frame for the cameras that render; an effect draws only while `highlighted`. Owner: `BoardPresenter`, which creates the effects on Home and switches them off at every end of a hold or removal. Stop: the effects are destroyed with their pooled roots when the block factory is disposed (level change or scene unload).
- **Replaced.** Our footprint-traced ribbon (`FootprintOutline`, `BlockOutlineMesh`, `HeldOutlineView`, `BlockOutline.shader`, `MAT_BlockOutline`) was removed.

## Loops, owners and stop points

| What runs | Owner | Starts | Stops |
| --- | --- | --- | --- |
| `GameplayTickOwner.Tick` (Onity context Update) | Game `SceneContext` (AsSingle) | Scene load | Scene unload. It returns at once outside Playing. |
| `InputActionMap "Gameplay"` | `GameplayPointerInput` (AsSingle, `IDisposable`) | `Play`, `Resume` | Disabled by Suspend and Teardown; disposed with the scene |
| Settle and exit tweens (target: block root `Transform`) | `BoardPresenter` | Release (settle), exit commit | Paused by Suspend, stopped by Teardown; exits are polled in `Sync` (no callbacks) |
| Won Finish deadline (`Tween.Delay`, target: `LevelSession`) | `LevelSession` | Won | `CompleteFinish` or Teardown stops the stored handle |
| Block root pool (`BlockPieceFactory`) | `BoardPresenter` (the only construction site) | First build of a level (Home) | Roots are returned at Teardown. The factory is disposed on a level change or scene unload. |
| Arrow marker pools (`BlockArrowMarkers`, one Onity pool per `Arrow_N`) | `ArrowEffectView`, in the `BoardPresenter` effect view list | `TryPrepare` on the first build of a level (Home) | A marker returns before its root (`Detach`, `ReturnAll`): when its exit ends in `Sync`, at Teardown and on Restart. The pools are disposed on a level change or scene unload. |
| Barrier piece tweens (PrimeTween `LocalPositionY`, target: each barrier piece `Transform`) | `EnvironmentView`, driven by `BarrierEffectView` in the `BoardPresenter` effect view list | `Sync` after a flip, one `SetBarrierOpen` per flip of each barrier | Paused by Suspend (`SetPaused` to `SetBarrierTweensPaused`), stopped by Teardown (`StopTweens` to `StopBarrierTweens`). `BindAttempt` snaps without a tween (attempt build, Restart); the environment's `Clear` returns the pieces. |
| Exit fragment bursts (`ExitFragments`: an Onity pool of at most 4 `ParticleSystem`s, simulated by Unity while playing) | `BoardPresenter` | Pool: first build (Home). Burst: exit commit | Paused by Suspend, stopped and cleared by Teardown. `Sync` returns each burst after its last cube dies. The pool is disposed with the presenter. |
| Held and Vacuum outline (Highlight Plus `HighlightEffect` per pooled root; its render feature runs each frame, drawing only highlighted effects) | `BoardPresenter` | Added on Home in `Place`; switched on by `SetHeld` and a Vacuum removal | Switched off by release, `SnapHeld`, an exit start, `ReturnPiece` and `ReturnPieces`; destroyed with the pooled roots |
| Attempt CTS and `CompositeDisposable` | `LevelAttempt.Lifetime` | `TryPrepare` | Teardown steps 4 and 5 |
| `MoveReach` breadth-first search | `MoveBlockHandler.Execute` (one call) | A command with a displacement | The target is found or the queue empties; each origin is queued once |
| Attached `ICommandSource` | `LevelSession` (current attempt) | `TrySetCommandSource` | Detached with `null`, dropped by Teardown; asked only in the Playing command phase |
| Attempt build task | Game `SceneContext` (awaited build callback) | Scene load | Scene build token |
| `ApplicationPauseRelay.OnApplicationPause` | Scene object | OS events | Scene unload; it has no Update |

## Extending

- **Effects (Ice, Arrow, gate effects).** Implement the XP-1..XP-5 interfaces in `Domain/Hooks`: drag gate, axis constraint, cell gate, exit gate, board event listener. Register them from an `IBlockEffectModule` and add one line to the ordered module list in `GameSceneInstaller`; `EffectAttemptHookSource` builds each attempt's hooks from that list ([Block Effects Architecture](Block-Effects-Architecture.md)). The session, the drag resolver and the exit rules do not change (PRD 8.3, AC-EXT-1). A gate reads only the board it is passed (see [Commands](#commands)).
- **Replays, bots, tutorials.** Implement `ICommandSource` and attach it with `LevelSession.TrySetCommandSource`, or run `BoardSimulation.Commands` headless for tools and tests.
- **Timer.** Bind a real `ICountdown`. `Tick` returning true ends the attempt as Failed after the Won check (RULE-18).
- **Pause UI.** Bind an `IPauseOverlay` that keeps a background pause until the player presses Resume.
- **Held outline.** A follow-up task hooks into `IBoardPresenter.SetHeld`/`SnapHeld` (DEC-117). The exit fragments are in place; see [Exit fragments](#exit-fragments).
- **Arrow (BLK-01, DEC-158).** `TASK-20260926-230921278-a3ffdc40-arrow-block-gameplay`, on the shared effect architecture ([Block Effects Architecture](Block-Effects-Architecture.md)). `ArrowEffectModule` (TypeId `arrow`, listed after `IceEffectModule` in `GameSceneInstaller`) reads each authored block's axis on Home (`ArrowEffectLevel`). When some block has one, `AddHooks` registers `ArrowAxisConstraint` and `ArrowExitGate`; its state holds no changing values. Both hooks look the axis up through `board.GetFootprint(block).Source`, so a Rocket part keeps its parent's axis, and a block without Arrow gets `Both`. They allocate nothing per call. `LevelSession`, `AttemptLevel`, the converter, the hook source and the movement and exit algorithms are unchanged.
  - The marker: `ArrowEffectView` wraps the shared `BlockArrowMarkers` in the presenter's effect view list. `TryPrepare` warms one pool per `Arrow_N` on Home, including the Rocket parts a block can show (`FragmentCapacity.AddArrowMarkerCapacity`), and rejects a missing Arrow prefab naming the block. `Attach`, `AttachPart` and `SetFootprint` put the marker, centred on the footprint's bounding box (`ArrowMarkerRun`, the rule the Level Editor canvas and scene preview share), on the block root over its current cells, turned Y90 for Horizontal and tinted with `BoardArt.ArrowColor`. It is a static child, so drag, settle and exit motion carry it with no tick. `SetClipPlane` clips it with its block; `Detach` and `ReturnAll` return it before its root. `BoardPresenter` holds no Arrow code.
  - The centring follows `TASK-20260926-212320271-98b25efa-level-editor-arrow-markers` revision 1 and overrides the catalog's older BLK-01 view rule (DEC-158).
  - Two accepted deviations from the contract (revision 3). A level rejected for a missing Arrow prefab leaves no level built, like Ice, because effect views prepare inside the presenter's shared build step; the error still names the block. Idle pooled markers wait under `BoardRoot`, the only root `BlockEffectViewContext` provides.
- **Barrier (OBJ-05, DEC-163).** `TASK-20260925-103527585-3d1b0a34-barrier-rule`, on the same architecture. `BarrierEffectModule` (TypeId `barrier`, built with the scene's `EnvironmentView` as `IceEffectModule` is built with its material) reads the level's barrier cell objects on Home into `BarrierEffectLevel` (ID, cells, `startsOpen`, authored order). For a level with a barrier, `AddHooks` registers one fresh `Runtime/Domain/Hooks/BarrierRule` per attempt, so Restart restores `startsOpen`.
  - The rule is an `ICellGate` and an `IBoardEventListener`. A closed barrier cell admits only the block the passed reader already shows on it (DEC-MECH-18): the block stays when the barrier closes under it and may leave, and no other block enters until it opens. `MoveRules.CanOccupy` and the exit sweep in `ExitRules` ask it, so a closed barrier in a concave shape's swept path refuses the exit. Every barrier flips once per `BlockExitedEvent` with `Leave` and once per `BlockRemovedEvent`, so a Vacuum of N blocks flips it N times (DEC-MECH-3). `Stay`, moves and Rocket splits flip nothing; a Rocket part (DEC-146) needs no rule state, because the reader shows its new ID on its cells. A per-barrier version counts the flips. Calls allocate nothing.
  - The view: `BarrierEffectView` rejects a level whose play area shows a different number of barriers on Home. `BindAttempt` snaps every barrier with `SetBarrierOpen(id, open, animate: false)`, and `Sync` makes one animated call per flip since the last sync, the last with the rule's state. `SetPaused` and `StopTweens` reach the barrier tweens, so `BoardPresenter`, `LevelSession` and `GameAttemptLoader` are unchanged.
  - Status (2026-09-28): the rule, module, view and tests are in place. `GameSceneInstaller` lists the module last (`Ice, Arrow, Barrier`) once the Arrow task's `GameSceneArrowTests` module-count check is updated. `k_barrierRuntimeImplemented` flips once Validate Level models barriers, because every runtime-implemented effect needs solver support (DEC-144). Until then barrier levels are refused at project load.

## Verification

- EditMode (`ColorBlockJam.Tests.Gameplay`) covers:
  - rules: movement, exit, follow, picking, the transition table, the latch and hooks;
  - commands: the executor and log, every `MoveBlock` refusal, the preview board against a really moved board, the reach search, a seeded walk proving every live drag produces an accepted command, replay determinism with exits, a Stay gate and a cell gate, and command sources in the session;
  - the session with fakes: phase order, Suspend/Teardown order, same-tick Won, backgrounding;
  - pointer input with `InputTestFixture`;
  - the level converter;
  - architecture: a single tick owner, no gameplay Update or async void, Domain without engine references;
  - zero allocation on the tick path;
  - exit fragment placement on all four sides (`ExitFragmentPlacementTests`).
- PlayMode covers presenter pooling, the clipped exit motion, tween cleanup, the 0.8 s Finish cap and level 1 won by dragging in the real Game scene (`GameSceneDragTests`). `ExitFragmentsTests` and `BoardPresenterExitFragmentTests` cover the exit fragments: the capped warm-up, the block colour on the shared material, outward motion, the start on the clip plane, no burst for a move or a Stay outcome, pause, stop, return, Restart and zero allocation. `BoardPresenterArrowMarkerTests` and `BlockArrowMarkersTests` cover the Arrow markers: placement, tint, exit clip, return on exit, Teardown, Restart and a level change, a catalog without the Arrow prefab, markers destroyed with their roots, and no instantiation in Playing. `GameSceneArrowTests` loads the Game scene and checks the markers, a drag along each axis, an exit on an axis side and a refused perpendicular exit.
- Arrow EditMode tests: `ArrowRulesTests` (1x1, I3 and L3 on both axes, diagonal drags, the perpendicular exit `Vetoed` with `AxisRefused`, a Rocket part keeping its parent's axis, zero allocation), `ArrowEffectModuleTests`, `ArrowSessionTests` and the Arrow cases in `ZeroAllocationTests`.
- Barrier tests: EditMode `BarrierRuleTests`, `BarrierEffectLevelTests` and `BarrierSessionTests`; PlayMode `BarrierEffectViewTests` with the real `EnvironmentView`.
- The out-of-Editor compile harness builds all four assemblies as C# 9 with the Onity analyzers, and treats warnings as errors.
- Still unproven:
  - feel on a phone (OQ-8);
  - 0 B per frame on the device (V-M5).
