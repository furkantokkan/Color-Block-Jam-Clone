# Final design, part 2: corrections, tests, ownership, deferred items, questions (sections F–I)

Decision owner for this part: the main session (Opus 5.5). The Fable decision agent wrote
sections A–E (`final-design.md`) and stopped on a usage-credit error before F–I. This part
completes the design and **overrides A–E where it says so**.

## 0. Corrections to sections A–E (binding)

1. **C# 9 only** (Unity 2022.3 compiles `LangVersion 9.0`). No record/record struct, no
   positional `readonly struct X(int A)` syntax, no file-scoped namespaces, no `init`, no global
   usings. The four board events and `BoardEvent` are plain `readonly struct`s with explicit
   constructors and get-only properties.
2. **`DragResolver.TryBegin` has no side effect on refusal.** A block that is not `OnBoard` or
   that a drag gate refuses returns `false` and leaves the resolver inactive. The router keeps
   the press inert until Up (future Ice shake goes through a presenter call on refusal). This
   removes the stuck-active drag in C.4.
3. **Follow lead.** `DragSettings.LeadCells = 0.5f`. `DragResolver.Update` rounds half away from
   zero, so the logical origin changes exactly when the offset reaches ±0.5; a 0.5 lead toward a
   free neighbour makes the view continuous across a step with no hop (C.6 claimed this with 0.45,
   which hops 0.1 cell). Toward a usable door the lead is `min(|delta|, ExitPushCells)`; the exit
   commits at 0.3, so the view never leans further into a door than the commit threshold.
4. **Pick plane height is serialized.** `GameSceneInstaller` has `[SerializeField] float
   m_pickPlaneHeight = ExitMotionSettings.k_blockTopHeight`. The implementer measures the block
   top once from `Prefabs/Board/Pieces` Center renderer bounds (read-only) and sets the constant;
   until the scene step verifies it, it stays UNPROVEN.
5. **Router is injected.** `DragInputRouter` is bound `AsSingle` and injected into `LevelSession`
   (constructor order: `DragInputRouter, IPointerSource, IBoardPresenter, ICountdown,
   IPauseOverlay`). The router owns the `IUiPointerFilter` and `IBoardProjector` dependencies.
6. **Camera framing.** `GameAttemptLoader` calls `BoardCamera.Frame(worldBounds)` once after
   `IBoardPresenter.Build`, exactly like `EnvironmentPreview.FrameMainCamera` does today
   (`BoardCamera` is in `ColorBlockJam.Environment`, being implemented by the board-camera-framing
   task). The installer serializes an optional `BoardCamera m_boardCamera`; a missing one logs one
   warning and play continues.
7. **Shape library.** The installer serializes `BlockShapeLibrary m_shapes` and binds it with
   `BindScriptableObject`. The converter and presenter read `Shapes`/`Get` exactly as
   `Editor/LevelPreview/LevelPreviewHost.cs` does (call `Load()` first if that host does).
8. **Stable tie id = authored block order.** `LevelData` block order is the editor's creation
   order and is stable across attempts; `BlockPicker` ties go to the lowest index. String ID
   ordinal order is not used (`"b10" < "b2"`), recorded in DEC-115.
9. **Test assembly names** stay as in A. EditMode tests reference `Unity.InputSystem` and
   `Unity.InputSystem.TestFramework` only if a test uses `InputTestFixture`.

## F. Test plan

### F.1 `ColorBlockJam.Gameplay.EditModeTests` (`Tests/Gameplay/EditMode/`, Editor only)

References: `ColorBlockJam.Domain`, `ColorBlockJam.Gameplay`, `ColorBlockJam.Runtime`,
`Onity.DI`, `Onity.Reactive`, `Unity.InputSystem`, `Unity.InputSystem.TestFramework`,
`UnityEngine.TestRunner`, `UnityEditor.TestRunner`; `overrideReferences: true`,
`precompiledReferences: ["nunit.framework.dll"]`, `autoReferenced: false`,
`rootNamespace: ColorBlockJam.Tests.Gameplay`. Test files are named `*Tests.cs` and never
`Level*Tests.cs` (that pattern belongs to the level-editor tasks).

Fixtures: `Tests/Gameplay/EditMode/Fixtures/BoardFixture.cs` builds `LevelModel`s from ASCII
maps (rows top to bottom, `.` floor, `#` void, letters = block cells with colour per letter) plus
a door list, so every rule test reads like a picture. Fakes live in `Fixtures/`:
`FakePointerSource`, `FakeUiPointerFilter`, `FakeBoardProjector` (orthographic identity and a
skewed perspective variant), `FakeBoardPresenter` (records calls in order), `FakeCountdown`,
`RecordingListener : IBoardEventListener`, `VetoExitGate`, `StayExitGate`, `DenyDragGate`,
`AxisGate`, `DenyCellGate`.

| Test file | Covers | IDs |
|---|---|---|
| `Domain/LevelModelTests.cs` | TryCreate rejects out-of-board, non-floor, overlap, disconnected, door out of edge, overlapping door lanes; `GetDoorAtLane` | AC-8.5, RULE-1..8 |
| `Domain/BoardStateTests.cs` | initial occupancy, `Move`, `Release` (cells freed, status, `RequiredRemaining`), `Reset` equals a fresh state | RULE-2, V-E11 (domain part) |
| `Domain/MoveRulesTests.cs` | `CanOccupy` against edge, void, other block, own cells, cell gate; `IsFlush` per side for I, L, T shapes | RULE-2, RULE-3, RULE-10, XP-3 |
| `Domain/DragResolverTests.cs` | straight drag stops at first blocked cell; a pointer jump across a block stops before it; diagonal dominant axis first, tie X; sliding along a wall; bounded steps; release keeps the reached cell; `MoveCommitted` only when changed; no `MoveCommitted` on exit; `Interrupt` defers it and `FlushPending` publishes it first; `TryBegin` refusal leaves the resolver inactive; drag gate refuses; axis constraint fixed for the whole drag | RULE-9..11, AC-3.2, AC-3.3, V-E8, DEC-111..106, XP-1, XP-2 |
| `Domain/ExitRulesTests.cs` | Fits; NotFlush; WrongColor; TooNarrow (extent wider than span, partial overlap, adjacent same-colour doors); NoDoor; PathBlocked for an L/C notch holding another block; void in the swept path; exit gate veto; `Stay` outcome keeps the block; a wrong-colour door stops movement like a wall | RULE-12..14, AC-3.4, V-E9, XP-4 |
| `Domain/ExitCommitTests.cs` | push ≥ 0.3 commits during the drag, < 0.3 does not; release while flush does not exit; commit frees cells, sets `Exited`, logs and publishes `BlockExited` once; corner block beside two fitting doors leaves once through the larger push (tie X); an exited block cannot be picked or begun | RULE-13, DEC-25, DEC-50, DEC-111 |
| `Domain/FollowOffsetTests.cs` | no lead toward a blocked cell; lead ≤ 0.5 toward a free cell; lead ≤ 0.3 toward a usable door; zero lead toward a wrong door; diagonal corner lead zeroes the smaller axis | DEC-112 |
| `Domain/BlockPickerTests.cs` | direct hit; wall-ring, gate and border presses pick the nearest footprint; L empty corner is not footprint; long vs short block distances; ties go to the lower index; exited excluded; empty board returns −1; skewed projector (perspective) changes the winner where screen distance differs from board distance; `InteractionArea` bounds | AC-3.19, AC-3.20, V-E27, DEC-91, DEC-115 |
| `Domain/SessionTransitionsTests.cs` | every row of the 7.5 table; every other (state, request) pair is rejected (exhaustive loop) | AC-5.6, V-E5 |
| `Domain/SessionStateMachineTests.cs` | pause reasons, Background latch, second terminal request ignored, `AttemptNumber` | RULE-17, V-E4 |
| `Domain/AttemptHooksTests.cs` | gates AND, axes intersect, publish in registration order, empty arrays are no-ops | PRD 8.3, AC-EXT-10 |
| `Gameplay/AttemptLevelConverterTests.cs` | converts a two-block two-door `LevelData` with a `BlockShapeLibrary` loaded from its JSON; unknown shape, unknown colour, invalid level fail with one error; source `LevelData` unchanged after conversion | AC-3.1, AC-8.5 |
| `Gameplay/LevelSessionTests.cs` | ticks do nothing unless Playing; a press over UI never starts a drag; outside the interaction area selects nothing; drag to a matching door → Won after the command phase, presenter `Sync` once for the final exit; Finish then Teardown order (pointer disabled, countdown stopped, tweens stopped, token cancelled, disposables disposed, pieces returned); after Won commands and ticks change nothing; last exit plus timer zero in one tick → Won; Suspend order; Resume flushes a pending `MoveCommitted` first; Background → Paused(Background), foreground auto-resume through `NoPauseOverlay(true)`, Won + background forces `CompleteFinish`; Restart rebuilds a board equal to the first and cancels the old attempt token | RULE-15..18, RULE-29, V-E1, V-E3, V-E4, V-E11, V-E16, V-E26, AC-3.12 (interim), AC-3.13, AC-3.21 |
| `Gameplay/GameplayPointerInputTests.cs` (`InputTestFixture`) | mouse and touch Down/Held/Up; a second touch never becomes the pointer; touch cancel → Cancel; disabled map reports Cancel for a held pointer | RULE-9, DEC-26, DEC-114 |
| `Gameplay/GameplayArchitectureTests.cs` | exactly one `IOnityTickable` implementer across non-test `ColorBlockJam.*` assemblies; no Domain/Gameplay type declares `Update`/`FixedUpdate`/`LateUpdate`; no `async void`; `ColorBlockJam.Domain` references no `UnityEngine*`/`Onity*`; `ColorBlockJam.Runtime` does not reference Domain/Gameplay | NFR-L3, NFR-L6, NFR-11, V-E22 |
| `Gameplay/ZeroAllocationTests.cs` | `Is.Not.AllocatingGCMemory()` on steady `DragResolver.Update`, `ExitRules.Evaluate`, `FollowOffset.Compute`, `BlockPicker.Pick`, `AttemptHooks.Publish` with a listener, `LevelSession.RunTick` with fakes during a held drag (after one warm-up call each) | NFR-1, NFR-P2, V-E23, AC-EXT-6 |

### F.2 `ColorBlockJam.Gameplay.PlayModeTests` (`Tests/Gameplay/PlayMode/`)

References as in A; `defineConstraints: ["UNITY_INCLUDE_TESTS"]`. Uses `#if UNITY_EDITOR`
`AssetDatabase` loads like `EnvironmentViewLifecycleTests`/`BoardKitLifecycleTests`.

| Test file | Covers | IDs |
|---|---|---|
| `BoardPresenterLifecycleTests.cs` | instantiate `Environment.prefab`, `Construct(database)`, real `BlockShapeLibrary.asset`; `Build` + `Place` puts roots at `CellToLocal(origin)` under BoardRoot; an exit event starts a clipped `LocalPosition` tween on the piece `Transform`; after it ends the piece is returned; `StopTweens` + `ReturnPieces` leave `Tween.GetTweensCount(t) == 0` and factory `ActiveCount == 0`; `Build` for the same level keeps `PreparedCount` (no new instantiation); `SetTweensPaused(true)` freezes position | AC-3.16, AC-3.18 (motion part), NFR-L7, V-M1, V-M20 (partial) |
| `FinishDeadlineTests.cs` | a session with the real presenter and fakes for input: the last exit latches Won, input is disabled at once, Teardown completes within 0.8 s + one frame; `NotifyBackground(true)` during Finish tears down immediately | AC-4.12, DEC-93, V-BST-9 (door-exit part) |

### F.3 Verification commands

- Out-of-Editor compile (no Editor impact): `scratchpad/harness/build.sh all` (C# 9,
  netstandard2.1, Onity analyzers, warnings as errors).
- Live Editor (MCP HTTP 8082, only when `editor/state` is idle and no tests run):
  `refresh_unity` → console 0 errors/warnings from new assemblies → `run_tests` EditMode filter
  `ColorBlockJam.Tests.Gameplay` → PlayMode filter `ColorBlockJam.Tests.Gameplay`.
- Regression: EditMode `ColorBlockJam.Tests.Environment` and the Presentation PlayMode suite
  still pass (no shared file changed).

## G. Ownership and risk

**Allowed write paths (this task):**
- `Assets/ColorBlockJam/Domain/**` (new)
- `Assets/ColorBlockJam/Gameplay/**` (new)
- `Assets/ColorBlockJam/Tests/Gameplay/**` (new)
- `docs/Gameplay-Drag-And-Gate.md` (new)
- `PROJECT.md` (append decision rows DEC-110..DEC-117; one Asset and Assembly Boundaries bullet)
- `production/tasks/<this task>/**`

**Scene step only (separate approval):** `Assets/ColorBlockJam/Scenes/Game.unity` via the live
Editor: add a `GameplayScope` GameObject child of `SceneScope` with `GameSceneInstaller` and
`ApplicationPauseRelay`; append the installer to `SceneContext.m_installers`; set
`EnvironmentSceneInstaller.m_preview` to none and remove the `EnvironmentPreview` component
(its script file stays for the environment owner to delete). The scene is saved only when it has
no unrelated unsaved changes.

**Forbidden:** Presentation asmdef and `Presentation/**` sources, `Runtime/**`, `Environment/**`
sources and asmdefs, existing test asmdefs, `Prefabs/**`, `Data/**`, `Art/**`, `Packages/**`,
`ProjectSettings/**` (including `activeInputHandler`), other scenes, any Plastic checkin.

**Risk tiers:** new assemblies and public contracts = medium (one plan approval). Game.unity =
high (explicit target approval). No package, project setting, prefab or ScriptableObject change.

**Sequencing:** code and tests run now; they touch no claimed file. The scene step waits until
the board-camera-framing session (currently writing `Environment/BoardCamera*.cs`, next
`Game.unity`) releases Game.unity, because both edit the same serialized scene and only one
writer is allowed.

**Shared-Editor safety:** every slice compiles in the harness first; the live Editor recompiles
only after a green harness build, so a compile error in new code never blocks other sessions.

## H. Deferred items and UNPROVEN

| Follow-up task (proposed title) | Why deferred | Port left in place |
|---|---|---|
| Held-block white silhouette outline (AC-3.17, DEC-89) | Needs the board shader/material owned by board-kit | `IBoardPresenter.SetHeld(int)` |
| Gate-exit cube fragments (AC-3.18 fragments, DEC-90) | Needs a pooled effect prefab/material | Exit events in `BoardEventLog`; presenter `StartExit` |
| Level countdown and Failed (RULE-19..22, AC-3.5..3.8) | Separate timer task | `ICountdown`, timer phase slot |
| Pause/result popups and HUD (AC-4.x, AC-3.12 popup) | UI tasks | `IPauseOverlay`, session operations |
| Hub level selection / pre-level flow (AC-1.1a) | Hub task | `GameAttemptLoader` (interim auto-Play, level index) |
| Ice and Arrow effect registry (REQ-O4, REQ-O5) | Selected mechanics, later | `AttemptHooksBuilder`, XP-1..XP-5 |
| Editor validation solver (REQ-O2, AR-10) | Later task | pure `MoveRules`/`ExitRules` |
| Remove `EnvironmentPreview` scaffold file | Environment owner | — |

UNPROVEN until the scene step or the phone: pick-plane height, clip-plane position at the door,
drag feel thresholds (0.3 push, OQ-8 remains open), `TouchPhase` read on device, UI Toolkit
`Pick` allocation, 0 B per frame on the phone (V-M5), in-scene feel at 1080×1920 and 20:9.

## I. Questions for the user (approval gate)

1. Approve the plan: new `ColorBlockJam.Domain` and `ColorBlockJam.Gameplay` assemblies
   (Gameplay references Runtime, Presentation, Environment, Onity, PrimeTween and
   `Unity.InputSystem`) plus two test assemblies. Recommended: approve.
2. Game scene step: queue it after board-camera-framing releases Game.unity (recommended), take
   Game.unity now, or leave the scene to the user.
3. Held outline and cube fragments: defer with ports in place (recommended), or add an interim
   MPB white tint outline now (no shader change), or full shader work (touches board-kit files).
4. Gate scope: base colour door with XP hooks only (recommended), or also catalog gate effects
   (GTE-01..11; they need the effect JSON schema from level-json-v2-effects first).
