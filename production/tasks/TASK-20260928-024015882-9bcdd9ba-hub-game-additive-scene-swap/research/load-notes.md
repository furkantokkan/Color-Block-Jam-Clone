# Game load investigation notes (read-only, 2026-09-28)

Onity root: Library/PackageCache/com.onity.framework@8929214072 (v0.4.0)

## Current path, Hub Play -> Game
PreLevelPresenter.OnPlayPressed -> LevelLauncher.LaunchAsync -> OnityLevelSceneTransition
-> OnitySceneFlow.TransitionAsync(profile, Gameplay, request) -> new OnitySceneFlowStateMachine
-> SetActiveEnterData(request) -> OnitySceneLoader.LoadSingleAsync("Game") (Single, activate on load,
Task.Yield polling). Profile: routeViaLoading=0, no Loading scene (DEC-140).
Game activation: SceneContext.Awake (container Build, sync) -> Start (async void) -> BuildAsync() with NO
token -> GameAttemptLoader.LoadAsync: synchronous TryBuild in the same frame:
EnvironmentView.Build (lazy PrefabComponentPool.Get, no prewarm; pools live in Game scene),
BlockEffectContextSource.Prepare, LevelSession.TryPrepare -> BoardPresenter.TryBuild (new BlockPieceFactory,
roots = sum of max-independent-set per block, each root warms reachable pieces; markers; ExitFragments
(ParticleSystem built in code); BoosterCasts 2x Hammer/Rocket/Vacuum prefabs with Shuriken (DEC-152);
HeldOutlineView.Prepare), camera Frame, GameplayHud.Show (RocketReticleView white copy: GetPixels32 on
512x512 target.png + new Texture2D + Apply every Game load), then session.Play.
Everything is rebuilt per Game scene load. Only the activator delegate cache (ActivatorCompiler,
static ConcurrentDictionary) survives; TypeInjectionPlan cache is per container (m_planMap).

## Restart / Next level
LevelSession.Restart: in-scene; presenter.TryBuild(same level) early-returns; EnvironmentView.Build(same
layout+level) only resets barriers/gates -> already cheap. No Restart UI exists yet (HUD task
TASK-20260928-005915835 is ready, not done). Next Level has no implementation. A different level in the
same scene: EnvironmentView reuses pools (grow-only), BoardPresenter DisposeFactory + new factory (destroys
and re-instantiates every block root and piece).

## Levels (Data/Levels/LevelDatabase.json): 5 levels, 2-5 blocks, 6-17 cells, warmed roots 4-10.
So Instantiate count for blocks is small; environment, casts (particles), HUD, shaders likely dominate.
Not measured (no live Editor per task rules).

## Other facts
- No ShaderVariantCollection, GraphicsSettings m_PreloadedShaders empty.
- No Application.targetFrameRate / backgroundLoadingPriority set anywhere.
- EnterPlayModeOptions: domain+scene reload disabled (Editor).
- No [OnityGenerateActivator] in project code; Onity.SourceGen.dll shipped in Analyzers/.
- Hub and Game each have an EventSystem; Hub/Game panels both sort order 0 (Game HUD doc 30, counters 20).
- Onity has no ProfilerMarkers; diagnostics: Task Tracker (start/complete UTC per tracked scene op),
  Container Diagnostics (per-binding resolve ms), Pool Monitor, Observable Tracker, Monitor.
