# Game scene load: Editor baseline (2026-09-28)

Measured in the live Editor (ColorBlockJamCase2026@99077e4df1ecfaa4, Unity 2022.3.62f2,
Enter Play Mode Options: no domain or scene reload, quality level Balanced, VSync 1).
**These are Editor timings.** They include Editor overhead and an Editor-sized managed heap
(Mono heap 1,925 MB, 1,364 MB used), and they are not device timings. No repository file was
changed. The machine was under memory pressure during the run (Unity logged "97% paged memory
used" once), and other sessions compiled scripts and ran tests in between; runs overlapping a
compile are marked and left out of the statistics.

## Method

- Play Mode entered on Init (the project's start scene); Init opens Hub.
- Hub -> Game: `ILevelLauncher.LaunchAsync(new LevelLaunchRequest(4, null))` resolved from the
  Hub SceneContext, the same call `HubLaunchTests` makes (level-05, the largest level:
  5 blocks, 17 cells, 1 ice block). No starting boosters.
- Game -> Hub: `OnitySceneFlow.TransitionAsync(profile, OnitySceneFlowStateId.Hub)`, the planned
  Home route (a full scene-flow transition).
- A probe (PlayerLoop systems at the start of Initialization and the end of PostLateUpdate, plus
  `sceneLoaded` / `sceneUnloaded` / `activeSceneChanged`) logged per-frame Stopwatch times,
  `GC Allocated In Frame`, `Object Count`, the Game SceneContext state and the LevelSession state.
  Times are milliseconds from the launch call. "End of first Playing frame" is the end of
  PostLateUpdate (after rendering) of the frame in which `LevelSession.Play` ran.
- Unity Profiler (ProfilerDriver, no deep profiling) captured 3 loads; its tree gives the native split.
- Per-step build cost: the Game scene was loaded with an unmatched Test Level request in
  SessionState so `GameAttemptLoader` built nothing, then the loader's `TryBuild` steps were
  called one by one with a Stopwatch (the same calls, in the same order). The request was erased
  right after each load.
- Restart: `LevelSession.Pause(User)` then `Restart()`. Next level: no production code exists,
  so the same-scene path was replayed: `Pause` + `GoHome` (Teardown), then the loader's build
  steps for another level, then `Play`.
- Each MCP call compiles a Roslyn snippet in the Editor (about 62 MB GC and 20 to 1,000 ms in one
  frame). The probe shows this as one frame right after the launch call; it is excluded from the
  segments below but is inside the "from launch call" totals (about 20 to 60 ms).

## 1. Hub Play -> Game first Playing frame (warm, unprofiled: A2, A3, A6)

| Segment | A2 | A3 | A6 | min | median | max |
|---|---|---|---|---|---|---|
| Launch call -> Hub unloaded | 52.1 | 45.3 | 75.6 | 45 | 52 | 76 |
| Hub unloaded -> Game `sceneLoaded` (scene swap: GC.Collect, Game Awake/OnEnable) | 611.5 | 666.1 | 689.3 | 612 | 666 | 689 |
| `sceneLoaded` -> end of first Playing frame (OnityContext.Start build + first render) | 54.3 | 60.8 | 42.9 | 43 | 54 | 61 |
| **Launch call -> end of first Playing frame** | **717.9** | **772.2** | **807.8** | **718** | **772** | **808** |
| Next frame: `UnloadUnusedAssets` hitch | 62.7 | 86.8 | 83.7 | 63 | 84 | 87 |
| Launch call -> end of that hitch frame | 814.4 | 903.8 | 926.7 | 814 | 904 | 927 |

Unity's own load log for the same loads: Game `Total Operation Time` 663 / 711 / 765 ms,
`Integration` 614 / 668 / 693 ms, `Deserialize` 16 to 18 ms; `Unloading N unused Assets` 61 / 84 / 81 ms.
Profiled loads (C1P, A4P, A5P; the Profiler adds overhead): 781 / 808 / 866 ms.

Whole scene swap happens inside **one frame**: the frame that unloads Hub, integrates Game,
runs Awake and Start (the whole board build) and renders took 669 / 730 / 737 ms (A2 / A3 / A6).

### Cold: first Game load after a script recompile (domain reload)

| Run | Launch -> first Playing frame | `sceneLoaded` -> end of that frame |
|---|---|---|
| A1 (clean, unprofiled) | 8,589 ms | 7,860 ms |
| D1P (clean, Profiler on) | 8,712 ms | 7,904 ms |
| session 0 trial 1 (contaminated: a background compile and one MCP call ran during it) | 11,266 ms | 9,853 ms |

Two clean samples only: a cold load needs a fresh domain reload (about 80 to 100 s for everyone
on the shared Editor), so it was captured only when other sessions' recompiles provided one.
After re-entering Play Mode *without* a domain reload the first load is warm (C1P: 781 ms), so the
cost belongs to the first Game load after a script recompile, not to the first load per Play session.
Section 8 has the profiled cause.

## 2. Profiler split of the Hub -> Game load frame (A5P / C1P, ms)

| Marker | A5P | C1P |
|---|---|---|
| `Application.LoadLevelAsync Integrate` total | 748.9 | 626.8 |
| - `GC.Collect` (full GC run by the Single-mode scene load) | **552.2** (515.8 + 36.4) | **559.1** |
| - LoadLevelAsync Integrate self (native object integration) | 169.3 | 37.1 |
| - `UnloadScene` Hub (mostly `Physics.RecreatePhysicsScene` 12.3 / 14.5) | 15.2 | 17.1 |
| - `OnityContext.Awake` (installers, `container.Build`) | 5.2 | 5.1 |
| - `UIDocument.OnEnable` (HUD panel) | 4.8 | 5.7 |
| `OnityContext.Start` (GameAttemptLoader build: steps 4-13) | **40.9** (786 KB GC, 258 Instantiate) | **48.6** (782 KB GC) |
| UI Toolkit panel layout + first render | 14.3 | 18.5 |
| Next frame: `GarbageCollectAssetsProfile` (UnloadUnusedAssets, `GC.MarkDependencies` 58.3) | 60.9 | ~60 |

Game -> Hub (H4P): `GC.Collect` 521.3 of 579.1 ms integration; Hub `UIDocument.OnEnable` 8.5,
`Physics.RecreatePhysicsScene` 10.0, Hub panel style 8.9 + render 12.4, then the same
UnloadUnusedAssets frame (59 ms).

## 3. Per-step build cost in a fresh Game scene (level-05; B1, B2, B3; ms)

Pools start empty, as on every Game scene load.

| Step (GameAttemptLoader.TryBuild order) | B1 | B2 | B3 | min | median | max | new Transforms |
|---|---|---|---|---|---|---|---|
| BoardLayout.TryCreate + AttemptLevelConverter.TryConvert | 0.14 | 0.12 | 0.12 | 0.12 | 0.12 | 0.14 | 0 |
| EnvironmentView.Build | 9.84 | 7.87 | 8.67 | 7.87 | 8.67 | 9.84 | 300 |
| BlockEffectContextSource.Prepare | 0.02 | 0.01 | 0.01 | 0.01 | 0.01 | 0.02 | 0 |
| BoardPresenter.TryBuild (factory, markers, fragments, casts, outline, ice) | 11.71 | 11.95 | 9.17 | 9.17 | 11.71 | 11.95 | 283 |
| LevelSession.TryPrepare (hooks, simulation, Place) | 1.02 | 0.51 | 0.40 | 0.40 | 0.51 | 1.02 | 0 |
| BoardCamera frame + ReprojectEffects | 0.06 | 0.08 | 0.04 | 0.04 | 0.06 | 0.08 | 0 |
| IGameplayHud.Show (reticle white copy, UXML clone, timer strings) | 6.38 | 5.47 | 3.86 | 3.86 | 5.47 | 6.38 | 1 |
| LevelSession.Play | 0.29 | 0.17 | 0.22 | 0.17 | 0.22 | 0.29 | 0 |
| **Sum of steps** | 29.5 | 26.3 | 22.5 | 22.5 | 26.3 | 29.5 | 584 |

Loading Game with no build: `sceneLoaded` -> end of frame 9.4 / 9.7 / 8.0 ms, against 43 to 61 ms
with the build, so the build and the first board render add about 35 to 50 ms. The frame right
after the manual build rendered in 7.3 ms (no visible first-render or shader hitch in the Editor;
the Editor compiles shaders asynchronously, so a device shader hitch cannot be seen here).

## 4. Restart (level-05, in Game; ms)

| | 1 | 2 | 3 | min | median | max |
|---|---|---|---|---|---|---|
| `Pause(User)` | 4.00 | 0.09 | 0.04 | 0.04 | 0.09 | 4.00 |
| `Restart()` (Teardown + Place + Start) | 3.81 | 0.62 | 0.44 | 0.44 | 0.62 | 3.81 |

Pools kept (prepared 10 roots, active 5); about 32 KB managed per Restart; the next frames ran in
2 to 3 ms of work. The first call includes JIT.

## 5. Next level in the same scene (replayed; ms)

| Step | idx 3 (level-04) | idx 4 (level-05) | idx 2 (level-03) | min | median | max |
|---|---|---|---|---|---|---|
| Pause + GoHome (Teardown) | 6.48 | 0.49 | 0.49 | 0.49 | 0.49 | 6.48 |
| EnvironmentView.Build (pieces reused) | 2.16 | 1.57 | 2.75 | 1.57 | 2.16 | 2.75 |
| BoardPresenter.TryBuild (factory destroyed and rebuilt) | 6.96 | 7.77 | 5.30 | 5.30 | 6.96 | 7.77 |
| LevelSession.TryPrepare | 0.50 | 0.43 | 0.49 | 0.43 | 0.49 | 0.50 |
| HUD Show + camera + Play | 0.42 | 0.28 | 0.31 | 0.28 | 0.31 | 0.42 |
| **Total** | 16.5 | 10.5 | 9.3 | 9.3 | 10.5 | 16.5 |
| New Transforms from TryBuild | 135 | 246 | 115 | | | |
| Next frame work | 5.5 | 3.5 | 4.9 | | | |

A next level done inside the Game scene costs about 10 to 17 ms in the Editor, against 720 to 810 ms
for a reload of the Game scene.

## 6. Game -> Hub (Home route; unprofiled H1, H2, H3, H5, H6, H7, H8; ms)

| Segment | min | median | max |
|---|---|---|---|
| Game unloaded -> Hub `sceneLoaded` | 581 | 630 | 799 |
| Launch -> end of the swap frame | 637 | 704 | 876 |
| Launch -> Hub bound (start of the next frame) | 686 | 750 | 925 |
| Next frame (UnloadUnusedAssets) | 63 | 78 | 93 |

Samples (end of swap frame / Hub bound / hitch): H1 704/779/85, H2 665/697/63, H3 716/750/73,
H5 876/925/93, H6 637/686/66, H7 713/761/78, H8 700/745/82. Hub `Integration` 580 to 806 ms.

## 7. Allocation and instantiation

- Hub -> Game load frame: 1.6 to 5.1 MB managed allocation (A2 1.66 MB, A3 1.66 MB, A6 5.1 MB with
  2 extra gen-0 GCs); `OnityContext.Start` alone allocates about 785 KB.
- Build: 258 `Instantiate` calls and 37 `AddComponent` calls in the Start frame (Profiler);
  584 new Transforms for level-05 (environment 300, blocks and effects 283, HUD 1);
  `Object Count` rises by about 2,310 from Hub to Game.
- Game -> Hub load frame: about 1.1 to 1.2 MB.
- Restart: about 32 KB; next level: new block factory each time (115 to 246 Transforms).

## 8. Cold load profile (D1P, first Game load after a domain reload)

Frame total 8,625 ms (Profiler). `OnityContext.Start` 7,882 ms, of which **`Mono.JIT` 7,850 ms self
over 992 JIT events**. 984 events take under 5 ms; **8 events take 896 to 1,015 ms each**, all inside
`OnityContext.Start` (GameAttemptLoader build). The rest of the frame matches the warm load:
`GC.Collect` 574.8, LoadLevelAsync Integrate self 37.6, `OnityContext.Awake` 36.1 (14.4 self, also
cold JIT), `Physics.RecreatePhysicsScene` 12.8, UI Toolkit style 10.8. The Editor compiles with
`CodeOptimization.Debug`.

Where the 8 long JIT stalls fall (ms from the start of `OnityContext.Start`, placed by the nearest
static constructor, Awake and Instantiate samples; no deep profiling, so no method names):

| # | At | JIT ms | Position in GameAttemptLoader.TryBuild |
|---|---|---|---|
| 1 | +1 | 1,003 | Start of the build, before `Cell..cctor` (LoadAsync / TrySelectLevel / BoardLayout.TryCreate) |
| 2 | +1,021 | 1,015 | After level conversion begins, before the first environment pool (`OnityPoolDiagnosticsRegistry..cctor`), so EnvironmentView.Build composition |
| 3 | +2,081 | 983 | After 150 environment Instantiates and `EnvironmentGate..cctor`, before the block factory |
| 4 | +3,065 | 989 | Same gap: LevelSession.TryPrepare / hook source / BoardSimulation / BoardPresenter.TryGetPoolNeeds |
| 5 | +4,091 | 956 | After `BlockArrowMarkers..cctor`, before the first block Instantiate (BlockPieceFactory.Prepare) |
| 6 | +5,069 | 953 | After 252 Instantiates (blocks done), before `ExitFragmentEmitter..cctor` (markers / ExitFragments.Create) |
| 7 | +6,032 | 896 | After `ExitFragmentEmitter..cctor`, before `BoosterEffectView.Awake` (ExitFragmentEmitter setup) |
| 8 | +6,947 | 909 | After `GameplayHudView..cctor` (IGameplayHud.Show / TryBind) |

Reading: Mono JIT on the first run after a recompile. A device IL2CPP build compiles ahead of time and
has no JIT, so this cost does not reach the phone. The near-identical ~1 s per stall (instead of a
size-dependent spread) suggests something environmental, such as code-memory allocation while the
machine was at 97% commit or lock contention with a background thread, rather than eight huge
methods. That is not confirmed. ZLinq is not used on this path (no `AsValueEnumerable` in runtime code).

## Top 3 measured costs (Editor)

1. **First Game load after a script recompile: about 7.9 s extra in one frame (A1 7,860 ms,
   D1P 7,904 ms).** Profiled: Mono JIT, eight ~1 s JIT stalls inside the Start build.
   Editor-only; an IL2CPP device build has no JIT.
2. **Full `GC.Collect` inside every Single-mode scene swap: 515 to 575 ms per Hub -> Game and per
   Game -> Hub (about 70% of a warm load).** It scales with the Editor's 1.35 GB managed heap, so a
   device pays far less; the device number was not measured. Next comes about 60 to 90 ms of
   UnloadUnusedAssets (`GC.MarkDependencies`) in the frame after the first Playing frame,
   plus 40 to 170 ms of other native integration.
3. **Board build in a fresh Game scene: 22 to 30 ms of script time (41 to 49 ms under the
   Profiler), all in the one load frame:** BoardPresenter.TryBuild 9 to 12 ms, EnvironmentView.Build
   8 to 10 ms, HUD Show 4 to 6 ms; 258 Instantiate calls, 584 Transforms.

Restart (0.4 to 3.8 ms) and a same-scene next level (9 to 17 ms) are cheap. A warm Hub -> Game takes
718 to 808 ms to the first rendered Playing frame (median 772), then one 63 to 87 ms hitch frame;
Game -> Hub takes 686 to 925 ms (median 750) to a bound Home.

## What this means for the code-trace suspects

- Suspect 1 (full scene swap): confirmed as the dominant warm cost in the Editor, through the
  GC.Collect and UnloadUnusedAssets that come with `LoadSceneMode.Single`, not through asset loading
  (Deserialize 16 to 29 ms). Device size unmeasured.
- Suspect 2 (one-frame rebuild of ~400-470 objects): real, but smaller than estimated: 22 to 30 ms
  of script time, 584 Transforms, 258 Instantiates (level-05). It is still one frame.
- Suspect 3 (no shader warm-up): not measurable in the Editor (async shader compilation); the first
  board render cost 7.3 ms. Needs a device capture.
- Suspect 4 (reticle copy + HUD): HUD Show measured 3.9 to 6.4 ms.
- Suspect 5 (load priority): the whole integration ran inside one frame here, so
  `backgroundLoadingPriority` time-slicing was not what stretched these loads.

## Raw data

- Probe logs: `session0/`, `sessionA/`, `sessionC/`, `sessionD/` (`run_N.txt`, one per armed run).
- Profiler trees: `profile_H4P.txt` (Game -> Hub), `profile_A5P.txt` and `profile_C1P.txt`
  (warm Hub -> Game), `profile_D1P_cold.txt`, `profile_D1P_jit.txt`, `profile_D1P_anchors.txt`,
  `profile_D1P_start_sequence.txt` (cold Hub -> Game).
- Probe code: `probe_install.cs` (frame probe), `probe_breakdown.cs` (per-step build),
  `probe_profiler_frames.cs`, `waitrun.sh`.
- Editor state afterwards: Play Mode off, Init open and clean, Profiler off with no frames kept,
  `profileEditor` off, player loop has no probe systems, no Test Level request in SessionState,
  `playModeStartScene` null (all as found).
