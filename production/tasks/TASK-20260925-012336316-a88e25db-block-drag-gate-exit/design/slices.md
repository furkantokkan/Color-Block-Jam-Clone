# Implementation slices

Design authority: `final-design.md` (A–E) and `final-design-2.md` (section 0 corrections override A–E; F–I).
All slices write into the **staging tree** and never into the Unity project directly:

- Staging root: `C:/Users/e-fur/AppData/Local/Temp/claude/C--Users-e-fur-Documents-Repos-ColorBlockJamCase2026/13a82b48-5433-45e2-ba99-591a70c39b96/scratchpad/stage/`
- A project path `Assets/ColorBlockJam/X` is written as `<staging root>/Assets/ColorBlockJam/X`.
- Compile check: `bash <scratchpad>/harness/build.sh <Domain|Gameplay|GameplayEditModeTests|GameplayPlayModeTests|all>` (C# 9, netstandard2.1, Onity analyzers, warnings are errors). A slice is done only when its projects print `BUILD OK`.
- Do not create `.meta` files (Unity generates them). Do not touch the live Unity Editor or the project `Assets/` folder. The main session copies the staging tree into the project after review.

Common code rules (PROJECT.md, AGENTS.md, global CLAUDE.md):
- C# 9 only: no records, no positional/primary-constructor structs, no file-scoped namespaces, no `init`, no global usings, no `default` interface members relying on newer runtimes.
- Every type starts with a `/// <summary>` one-sentence responsibility ("This does X"); add "It is here because Y (DEC-xxx)" where not obvious. Every Subscribe/async/tween/pool/loop names `Owner:` and `Stops:` in a one-line comment.
- Naming: private fields `m_camelCase`, private static `s_camelCase`, constants `k_camelCase`; simple verbs; no Manager/Helper.
- No `System.Linq` in Domain or Gameplay; no LINQ, closures, boxing, `foreach` over interfaces, string building or delegates in hot paths; `for` loops over arrays.
- No `Update`/`FixedUpdate`/`LateUpdate`, coroutines, `Invoke`, static events, static mutable state, `async void`, fire-and-forget, `Time.timeScale`, legacy `Input.*`.
- No dead code, no commented-out code, no unguarded `Debug.Log` (warnings for real failures only).
- Decision IDs for comments: DEC-110 assemblies; DEC-111 exit on push ≥ 0.3 while flush; DEC-112 integer logical cells + follow lead (0.5 free / 0.3 door) + dominant-axis stepping; DEC-113 exit raises BlockExited only, MoveCommitted deferred after interruption; DEC-114 code-built input map polled in the tick; DEC-115 screen-space projected-footprint picking, ties = authored order; DEC-116 interim foreground auto-resume and auto-Play until pause/Hub UI; DEC-117 outline/fragments in a follow-up task behind `SetHeld`/exit events.

## S1 — Domain + domain tests

Write paths:
- `Assets/ColorBlockJam/Domain/**` (all Domain types of design B.1, with section 0 corrections 1, 2, 3, 8)
- `Assets/ColorBlockJam/Domain/ColorBlockJam.Domain.asmdef`:
  `{"name":"ColorBlockJam.Domain","rootNamespace":"ColorBlockJam.Domain","references":[],"includePlatforms":[],"excludePlatforms":[],"allowUnsafeCode":false,"overrideReferences":false,"precompiledReferences":[],"autoReferenced":false,"defineConstraints":[],"versionDefines":[],"noEngineReferences":true}`
- `Assets/ColorBlockJam/Tests/Gameplay/EditMode/ColorBlockJam.Gameplay.EditModeTests.asmdef` (initially references `ColorBlockJam.Domain`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`; `includePlatforms: ["Editor"]`, `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, `autoReferenced: false`, `rootNamespace: "ColorBlockJam.Tests.Gameplay"`)
- `Assets/ColorBlockJam/Tests/Gameplay/EditMode/Fixtures/**` (domain fixtures and fakes: `BoardFixture` ASCII builder, `RecordingListener`, gate fakes)
- `Assets/ColorBlockJam/Tests/Gameplay/EditMode/Domain/**` (tests listed in design F.1 rows "Domain/*")

Spec: design B.1, C.2–C.9 (domain parts), D.1. Namespaces `ColorBlockJam.Domain.{Board,Hooks,Movement,Exit,Picking,Session}`. Specific rules:
- `Cell` is `readonly struct` with `X`, `Y`, equality, `+`, `-`, `GetHashCode` (no allocation).
- `BoardEventLog.Add` throws `InvalidOperationException` when full (capacity `blockCount + 4` is set by `BoardSimulation`).
- `DragResolver`: TryBegin never mutates on refusal; `Update` per C.4 with `RoundHalfAwayFromZero` = `(int)Math.Floor(v + 0.5)` for positive and the mirror for negative values (write a private helper); lead per section 0.3; `GetViewOffset` delegates to `FollowOffset.Compute(... settings.LeadCells, settings.ExitPushCells)`.
- `ExitRules.Evaluate` per C.5 exactly; `FollowOffset.Compute` per C.6 with the section 0.3 correction (door lead ≤ ExitPushCells).
- `BlockPicker` per C.8 (the only allocation is the corner buffer in the constructor).
- `SessionTransitions`/`SessionStateMachine` per D.1; the terminal latch comes from the table.
- `ICountdown` + `InactiveCountdown` per B.1.
- Tests use NUnit and, for zero-allocation, `UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory()` (that assertion needs `UnityEngine.TestRunner`; keep zero-alloc tests in `Gameplay/ZeroAllocationTests.cs` of S2 if the Domain-only test files would otherwise need UnityEngine — Domain tests may reference UnityEngine through the test asmdef, that is fine).
Done when `build.sh Domain` and `build.sh GameplayEditModeTests` are `BUILD OK` (GameplayEditModeTests compiles S1's tests; Gameplay project may be empty).

## S2 — Gameplay runtime + tests

Write paths:
- `Assets/ColorBlockJam/Gameplay/ColorBlockJam.Gameplay.asmdef`:
  `{"name":"ColorBlockJam.Gameplay","rootNamespace":"ColorBlockJam.Gameplay","references":["ColorBlockJam.Domain","ColorBlockJam.Runtime","ColorBlockJam.Presentation","ColorBlockJam.Environment","Onity.DI","Onity.Unity","Onity.Reactive","Onity.Pooling","Onity.Factory","PrimeTween.Runtime","Unity.InputSystem"],"includePlatforms":[],"excludePlatforms":[],"allowUnsafeCode":false,"overrideReferences":false,"precompiledReferences":[],"autoReferenced":false,"defineConstraints":[],"versionDefines":[],"noEngineReferences":false}`
- `Assets/ColorBlockJam/Gameplay/Levels/**`, `Gameplay/Input/**`, `Gameplay/Session/**` (design B.2 rows Levels/Input/Session, including `GameplayTickOwner`, `IBoardPresenter`, `IPauseOverlay`, `NoPauseOverlay`, `AttemptLifetime`, `LevelAttempt`, `DragInputRouter`, `LevelSession`)
- EditMode test asmdef: add references `ColorBlockJam.Gameplay`, `ColorBlockJam.Runtime`, `Onity.DI`, `Onity.Reactive`, `Unity.InputSystem`, `Unity.InputSystem.TestFramework`
- `Assets/ColorBlockJam/Tests/Gameplay/EditMode/Gameplay/**` and new fakes in `Fixtures/` (`FakePointerSource`, `FakeUiPointerFilter`, `FakeBoardProjector`, `FakeBoardPresenter`, `FakeCountdown`)

Spec: design B.2, C.1, C.8 router, D.2–D.5, E (`RunTick`, `GameplayTickOwner`), section 0 corrections 5–8. `LevelSession` constructor: `(DragInputRouter router, IPointerSource pointer, IBoardPresenter presenter, ICountdown countdown, IPauseOverlay pauseOverlay)`; it exposes `IReadOnlyReactiveProperty<SessionState> State`. Finish deadline uses `PrimeTween.Tween.Delay(this, seconds, static s => s.CompleteFinish(), useUnscaledTime: true)` with the handle stored and stopped (check the exact overload in `Library/PackageCache/com.kyrylokuzyk.primetween@1.4.11/Runtime`). `UiToolkitPointerFilter` flips y for `RuntimePanelUtils.ScreenToPanel` (`new Vector2(p.x, Screen.height - p.y)`), ignores documents whose panel is null, and treats a picked element with `pickingMode != Ignore` as UI. `CameraBoardProjector` per C.8. `AttemptLevelConverter` per C.1 (use `BoardLayout.TryParseDoorColor` for both door and block colours; `ShapeResolver` matches `Shape.TypeName` ordinally).
Tests: design F.1 rows `Gameplay/*` (converter, session with fakes, pointer input with `InputTestFixture`, architecture, zero allocation).
Done when `build.sh all` prints `BUILD OK` for Domain, Gameplay and GameplayEditModeTests.

## S3 — Gameplay presentation, composition, PlayMode tests

Write paths:
- `Assets/ColorBlockJam/Gameplay/Board/**` (`ExitMotionSettings`, `BoardPresenter`)
- `Assets/ColorBlockJam/Gameplay/Composition/**` (`GameSceneInstaller`, `GameAttemptLoader`, `ApplicationPauseRelay`)
- `Assets/ColorBlockJam/Tests/Gameplay/PlayMode/ColorBlockJam.Gameplay.PlayModeTests.asmdef` (references `ColorBlockJam.Domain`, `ColorBlockJam.Gameplay`, `ColorBlockJam.Runtime`, `ColorBlockJam.Presentation`, `ColorBlockJam.Environment`, `Onity.DI`, `Onity.Unity`, `Onity.Pooling`, `Onity.Factory`, `PrimeTween.Runtime`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`; `includePlatforms: []`, `overrideReferences: true`, `precompiledReferences: ["nunit.framework.dll"]`, `autoReferenced: false`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, `rootNamespace: "ColorBlockJam.Tests.Gameplay"`)
- `Assets/ColorBlockJam/Tests/Gameplay/PlayMode/**` (design F.2)

Spec: design B.2 rows Board/Composition, C.6–C.8 presenter parts, C.10, D.2–D.4, E (`BoardPresenter.Sync`, clip plane C.12), section 0 corrections 4, 6, 7.
- `BoardPresenter` is the single `new BlockPieceFactory(...)` site. It creates a `Blocks` child under `EnvironmentView.BoardRoot` once, prepares the factory per source type with (count of blocks of that type, max cell count of those blocks) the first time a level is built, disposes and recreates the factory when a different `AttemptLevel` is built, and never calls `Instantiate`/`Destroy` during an attempt. Read `Editor/LevelPreview/LevelPreviewHost.cs` (`PrepareFactory`, `Build`) for the existing factory usage and follow it. Note: task `TASK-20260925-002022060-4c9dac46-block-runtime-objects` may later change `Prepare` capacity semantics; keep all factory calls inside `BoardPresenter`.
- Measure `k_blockTopHeight` from the piece prefabs (`Assets/ColorBlockJam/Prefabs/Board/Pieces/*.prefab`, their mesh/transform data, or the board-kit README under `docs/art/board-kit/`) and record the source in its comment; if not measurable from files, keep 0.5 and mark it UNPROVEN in the comment.
- `GameSceneInstaller : MonoInstaller` (`[DisallowMultipleComponent]`) serializes `EnvironmentView m_environment`, `BlockShapeLibrary m_shapes`, `Camera m_camera`, `BoardCamera m_boardCamera` (optional), `UIDocument[] m_uiDocuments`, `int m_levelIndex`, `float m_pickPlaneHeight`, `bool m_resumeOnForeground = true`; validates required references (throw `InvalidOperationException` like `EnvironmentSceneInstaller`), binds with `BindScriptableObject`, `BindInstance` for scene objects, `BindInterfacesAndSelfTo<T>().AsSingle()` for `LevelSession`, `GameplayTickOwner`, `BoardPresenter`, `GameplayPointerInput`, `DragInputRouter`, and registers `GameAttemptLoader.LoadAsync` with `RegisterBuildCallbackAsync`. Check the exact Onity APIs in `Library/PackageCache/com.onity.framework@ad0bfd5dca` (`MonoInstaller`, `OnityContainer`, `TypeBindingBuilder`, `MultiTypeBindingBuilder`, `ProjectContext.ReadyTask`) and copy the waiting pattern of `Environment/EnvironmentPreview.cs` `BuildAsync`.
- `ApplicationPauseRelay` has only `[Inject] Construct(LevelSession)` and `OnApplicationPause(bool)`.
Tests: design F.2.
Done when `build.sh all` prints `BUILD OK` for all four projects.
