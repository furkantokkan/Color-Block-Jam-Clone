# Game scene API map: session, input and navigation (research only, no files changed)

**Path keys**
- **B** = `C:/Users/e-fur/AppData/Local/Temp/cbj-booster-f1ed7e/Assets/ColorBlockJam` (the booster session's copy, primary source)
- **W** = `C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026/Assets/ColorBlockJam` (the shared workspace)
- **O** = `C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026/Library/PackageCache/com.onity.framework@8929214072`

**The booster copy changed while I was reading it.** `B/Gameplay/Composition/GameSceneInstaller.cs` was rewritten at 03:33:38 (the held-outline binding was merged in), and `B/Scenes/Game.unity` changed at 03:32:28. All B line numbers below come from the state at about 03:34. Snapshots of that state are in the scratchpad: `…/scratchpad/GameSceneInstaller.booster.0333.cs`, `Game.booster.unity`, `LevelSession.booster.cs` and `GameAttemptLoader.booster.cs`.

---

## (a) Public API of `LevelSession` (B/Gameplay/Session/LevelSession.cs)

| Member | Line | Preconditions and refusals | Effect |
|---|---|---|---|
| `IReadOnlyReactiveProperty<SessionState> State` | 85 | — | See the note on notifications below this table. |
| `PauseReason PauseReason` | 87 | A plain getter, not reactive. It is `None` outside Paused (Domain/Session/SessionStateMachine.cs:43). | — |
| `bool Pause(PauseReason)` | 195–215 | Refused after disposal, or when the transition table refuses it. `TryPause` maps User→`Pause`, Background→`Background`, Saving→`BoosterCommit`, Purchase→`ZeroStockTap`; `None` is refused (SessionStateMachine.cs:12–18, 53–64). From Paused, only Background is accepted (SessionTransitions.cs:78–80), so `Pause(User)` while already Paused returns false (Tests/…/BoosterIntentSessionTests.cs:288). It is **not** blocked during the opening. | From Playing it runs Suspend (733–740), then sets State to Paused (213). |
| `bool Resume()` | 221–231 | Refused after disposal, while `m_writePending`, or when the table refuses. Resume is accepted from Paused with **any** reason, because `MatchesPauseReason` defaults to true (SessionStateMachine.cs:68–80). | Resumes tweens, then the clock, then the pointer (590–595), and sets State to Playing. |
| `bool Restart()` | 237–258 | Refused after disposal, when `m_level == null`, while `m_writePending`, or when the table refuses. | Runs Teardown, rebuilds the attempt with `TryCommitAttempt`, then `Start()`, and sets State to Playing. |
| `bool GoHome()` | 264–279 | Refused after disposal, while `m_writePending`, or when the table refuses. | Runs Teardown and sets State to Home. It does **not** change scenes: the doc comment says scene navigation belongs to the Hub task (261). |
| `void NotifyBackground(bool)` | 287–326 | — | **Going to background:** on Home it latches `IsHomeBackgrounded`; from Playing or Paused it calls `Pause(Background)`; on Won it calls `CompleteFinish()`. **Returning to foreground:** it calls `Resume()` only when the reason is Background **and** `IPauseOverlay.ResumesOnForeground` is true. If a booster write is still pending, that resume waits (318–322 and 400–408). |
| `bool IsBoosterWritePending` | 113 | A plain bool that raises no event. It becomes true at 359 and false at 382 and 676. | The reactive equivalent is `BoosterUseCoordinator.IsBusy` (B/Gameplay/Boosters/BoosterUseCoordinator.cs:67, set at 224 and 261). |
| `IReadOnlyReactiveProperty<bool> IsOpening` | 101 | Written only by `UpdateHud` (698) and `ResetHud` (711). | True from `Play(plan)` (181–190) until `SpendHold` uses up the hold (643–656). |
| `bool TryOpenPurchase()` | 415–423 | Refused when a booster intent exists or during the opening. | Calls `Pause(Purchase)`. |
| `bool ClosePurchase()` | 426–436 | Applies `TryApply(PurchaseClosed)`, which is accepted **only while the reason is Purchase** (SessionStateMachine.cs:75–76). After backgrounding the reason is Background, so it returns false and the session stays Paused. | Resumes play and sets State to Playing. |

**State notifications**
- `ReactiveProperty.SetValue` notifies only when the value changes (O/Runtime/Reactive/Scripts/ReactiveProperty.cs:106–117).
- `Subscribe` sends the current value immediately by default (51–65).
- So when `Pause(Background)` hits an already Paused session, it sets Paused again at line 213 and **subscribers are not told**.

**Other relevant members**
- `Attempt` (90); `Attempt.Generation` is at LevelAttempt.cs:28.
- `DisplayedSeconds`, `FreezeDisplayedSeconds`, `IsFrozen` and `BoardRevision` (93–104).
- `HasCommittedBooster` (116), `Play()` and `Play(in OpeningPlan)` (153–192), `TryPrepare` (126–151), `CompleteFinish` (528–537), `RunTick` (460–525).

**Next Level:** the table accepts Won→`NextLevel` (SessionTransitions.cs:93–95), but no `LevelSession` method uses it (a grep finds only the enum and the table).

## (b) Is Restart or GoHome allowed?

| From | Restart | GoHome |
|---|---|---|
| Home | No (SessionTransitions.cs:30–44) | No |
| **Playing** | **No: the table has no Restart row for Playing (46–65)** | **No** |
| Paused(User) | Yes (75) | Yes (81) |
| Paused(Background) | Yes, unless a write is pending | Yes, unless a write is pending |
| Paused(Purchase) | Yes (the reason check defaults to true) | Yes |
| Paused(Saving) while the write is pending | No (LevelSession.cs:239; test at 286) | No (266; test at 287) |
| Won | No; only NextLevel or Home (89–102) | Yes; it stops the Finish deadline (271–275, 752–756) |
| Failed | Yes (108–110) | Yes (111–113) |

- Paused(Saving) ends by itself once the write resolves (392–397), so the session never sits in it after the write finishes.
- **A HUD Retry button pressed during play needs `Pause(PauseReason.User)` and then `Restart()`,** or a change to the domain transition table (which has tests).

**What both do (Teardown, 745–767, when an attempt exists)**
- **Input:** `m_pointer.Disable()` (747). `m_router.Reset` also cancels targeting (DragInputRouter.cs:133–140).
- **Countdown:** `Stop()` (750) also clears any freeze (AttemptCountdown.cs:45–50). Restart then calls `Start(level.TimerSeconds)` (584; AttemptCountdown.cs:25–37). That is the full authored time, **without** Extra Time. After GoHome the clock stays stopped.
- **Tweens:** `StopTweens` (751) stops effects, fragments, casts, frost and gates (BoardPresenter.cs:658–677). The Finish tween is stopped at 752–756.
- **Attempt lifetime:** cancelled and disposed (758–759).
- **Pools:** `ReturnPieces` (760) returns effects, fragments, casts and roots and resets gates (BoardPresenter.cs:684–699). On Restart, `TryCommitAttempt` (565–580) creates new hooks and a new simulation. `TryBuild` sees the same `AttemptLevel` and returns early, so the **pools are kept** (BoardPresenter.cs:366–370). It then places the blocks and increments the generation.
  - The environment is not rebuilt; only the loader builds it (GameAttemptLoader.cs:221–222).
  - `BlockEffectContextSource.Prepare` also runs only in the loader (224).
- **Booster intents:** a committed but unapplied intent is thrown away without a refund (`ClearIntent` at 763; doc comments 234–235 and 261–262). A pending write refuses both calls.
- **Opening:** `ClearOpening` runs (765). Restart's `Start()` (582–587) never starts it again, and the loader hands out the starting selection only once (GameAttemptLoader.TryTake 82–87; installer comment 123–124). A restarted level therefore has no starting boosters.
- **HUD:** `ResetHud` runs (766, 706–713). `IGameplayHud.Show` is **not** called again; only the loader calls it (237–241).
- **Notifications:** Restart goes straight from Paused (or Failed) to Playing, with no Home in between. GoHome sends Home. The use coordinator hides the purchase popup whenever State leaves Paused (BoosterUseCoordinator.cs:265–271).

## (c) How the Back key is read and dispatched in B

**Back is read in three places, all bound to `<Keyboard>/escape`:**
1. **Gameplay input map.** `GameplayPointerInput` adds a "Back" action (Input/GameplayPointerInput.cs:41). `WasBackPressed()` returns `m_map.enabled && m_back.WasPressedThisFrame()` (134–137). It therefore reads false whenever the map is disabled: every Paused state, Won and Teardown (LevelSession.cs:735, 747, 774).
2. **Its only reader is `DragInputRouter.Run`** (Session/DragInputRouter.cs:72–77). It checks Back before reading the pointer.
   - `m_targeting.Cancel()` runs unconditionally, and Cancel does nothing when no mode is set (BoosterTargeting.cs:66–72). So with no targeting, Back does nothing.
   - `Run` is called only from `RunTick` while Playing **and not during the opening hold** (LevelSession.cs:480–484). During the opening, Back is not read at all.
3. **Purchase popup.** `InGamePurchaseHost` owns its own action "PurchaseBack" with a `performed` callback (UI/InGamePurchaseHost.cs:37–38, 213–215). It is enabled only while the popup shows (111, 123) and calls `Close()`, which hides the popup and calls `ClosePurchase()` (131–140).

The Hub has a separate `HubBackInput` that only closes the top Hub overlay (W/Hub/Navigation/HubBackInput.cs:37–77).

**Who handles Back in each state today**

| State | Handler |
|---|---|
| Playing | Cancels targeting, otherwise nothing |
| Paused(Purchase) | Purchase close |
| Paused(User, Background or Saving) | Nothing |
| Won, Failed, Home | Nothing |

**Where a pause step can go**
- **While Playing,** in `DragInputRouter.Run` right after line 73: `if back: targeting.IsActive ? Cancel() : <pause sink>`. `IsActive` is at BoosterTargeting.cs:30.
  - The router cannot see the session, because the session depends on the router (constructor at 72–73). It needs an injected sink, following the `IBoosterTargetSink` pattern.
  - A pause raised inside the router step is already handled: `RunTick` checks the state right after the router (486–489), the same path an accepted booster target uses.
  - To also pause during the opening hold, Back must be read in `RunTick` before the `holding` check at 480.
- **While the pause popup shows,** the popup needs its own Back action, following the `InGamePurchaseHost` pattern, because the gameplay map is disabled under Paused. It must never be enabled while the purchase popup shows.
- **To verify on device:** when Escape closes a popup and play resumes in the same frame, the next tick must not also see that press through the router.

## (d) `IPauseOverlay` and what a real pause overlay needs

- **The interface** has one member, `bool ResumesOnForeground { get; }` (Session/IPauseOverlay.cs:4–8).
- **It is read** only at LevelSession.cs:315–316 and 403–404.
- **It is bound** as `new NoPauseOverlay(m_resumeOnForeground)` (installer line 98), and Game.unity sets `m_resumeOnForeground: 1` (line 770). `NoPauseOverlay` is in NoPauseOverlay.cs:7–15, and both copies are identical.

**A real pause overlay must:**
1. **Return `ResumesOnForeground = false`,** so backgrounding leaves the session in Paused(Background) and the popup shows (DEC-135).
2. **Show and hide itself.** The session never calls it. Subscribe to `State` and read `PauseReason`; show on Paused with reason User or Background.
   - **Problem:** a change from Paused(Purchase or Saving) to Paused(Background) raises no State notification (line 213 plus ReactiveProperty.cs:110–112).
   - The overlay therefore needs its own background/foreground signal. `ApplicationPauseRelay` only forwards to the session (Composition/ApplicationPauseRelay.cs:22–29).
3. **Take over from the purchase popup.** If the app backgrounds while the purchase popup is open, the popup stays, because the coordinator only hides it when State leaves Paused. Closing it then calls `ClosePurchase()`, which returns false, and the session is left Paused with **no UI** (InGamePurchaseHost.cs:131–140). The pause overlay must appear in that case.
4. **Handle a refused Resume.** `Resume()` fails while a booster write is pending (223). Disable the button or retry, using `BoosterUseCoordinator.IsBusy`. A booster committed while in the background applies on the first tick after Resume (600–617).
5. **HOME:** `GoHome()`, then the scene transition in (e). **Retry from the popup** (if kept): `Restart()`.
6. **Back:** its own `InputAction`, enabled only while the popup shows.
7. **New top-bar buttons** (Pause, Retry, coin plus) need `picking-mode="Position"` inside the HUD document. The HUD document is added to the board's pointer veto automatically (installer 163–176; UiToolkitPointerFilter.cs:26–53).
8. **Reuse the shared Settings popup.**
   - `W/UI/Settings/SettingsPopupPresenter.cs`: `CloseRequested` (62), `HomeRequested` (65), `Bind(view, token)` (89), `Show` (149), `Hide` (162), `SetHomeVisible` (175).
   - `SettingsPopupView.cs:20–37`: screen `settings-screen`, button `home-button`.
   - The Hub adapter to follow is `W/Hub/Settings/SettingsOverlay.cs:39–50`.
   - It needs `IPlayerSettings`, which the project scope provides (`BindInterfacesAndSelfTo<PlayerSaveService>` at W/Player/PlayerDataInstaller.cs:43).
   - B's Gameplay assembly already references `ColorBlockJam.UI` and `ColorBlockJam.Player` (B/Gameplay/ColorBlockJam.Gameplay.asmdef:7, 10). **W's does not.**

## (e) Returning from Game to Hub

**Nothing does this yet in either copy.** The only code that targets `OnitySceneFlowStateId.Hub` is `StartupLoader` (W/Runtime/Startup/StartupLoader.cs:39).

**How Hub opens Game today**
1. `PreLevelPresenter.OnPlayPressed` builds a `LevelLaunchRequest` and calls `LaunchAsync` with its bind token (W/Hub/PreLevel/PreLevelPresenter.cs:263–283, 290–300).
2. `LevelLauncher` (W/Hub/Launch/LevelLauncher.cs):
   - allows one launch at a time (44–49);
   - links the caller's token with its own lifetime token, which Hub unload cancels (53–63, 67–68);
   - returns Cancelled on `OperationCanceledException` and Failed (with a log) on other errors, and releases its guard in both cases; Completed keeps the guard on (70–86).
3. `OnityLevelSceneTransition.TransitionAsync` calls `OnitySceneFlow.TransitionAsync(m_profile, OnitySceneFlowStateId.Gameplay, request, ct)` (W/Hub/Launch/OnityLevelSceneTransition.cs:22–30).
   - Hub bindings: HubSceneInstaller.cs:46–47.
   - The profile is bound in the project scope (ProjectScopeInstaller.cs:25, 53), so Game can inject it too.
4. Inside Onity:
   - `OnitySceneFlow.TransitionAsync` (O/Runtime/Unity/Scripts/SceneFlow/OnitySceneFlow.cs:20–28) calls `ExecuteTransitionPlanAsync` (OnitySceneFlowStateMachine.cs:166–187).
   - That calls `SetActiveEnterData`, then `LoadSingleAsync`, which loads in **Single** mode (Async/OnitySceneLoader.cs:21–29).
   - The token is checked every frame while loading (103–108), but a load Unity has started still completes.
5. **Profile** `W/Data/SceneFlow/SceneFlowProfile.asset` (guid a1fc21a5…, referenced at ProjectContext.prefab:49):
   - loading-scene route off (15), bootstrap `Init` (16), no loading scene (17);
   - gameplay `Game` (19, 22–23), hub `Hub` (20, 24–25).
   - Build Settings list Init, Hub, Game (ProjectSettings/EditorBuildSettings.asset:9, 12, 15).
6. The Game scene takes the enter data once, in `GameAttemptLoader.LoadAsync` (B:101, W:61). **Hub reads no enter data;** that call is the only consumer of the transition store.

**Suggested Game→Hub path (based on the existing patterns; none of this exists yet)**
- **Steps:** call `session.GoHome()` first. It is allowed from Paused, Won and Failed, and refused while a write is pending. Then call `OnitySceneFlow.TransitionAsync(profile, OnitySceneFlowStateId.Hub, null, token)`.
- **Owner:** **not `GameAttemptLoader`.** It is a build callback that ends after the first Play (93–135) and has no later lifetime.
  - Use a new Game-scene singleton that mirrors `LevelLauncher`: a one-launch flag plus a lifetime token cancelled in `Dispose`.
  - The Single load unloads Game, which disposes that singleton while it is still awaiting. It must catch `OperationCanceledException` and touch nothing after the await.
- **Hub side:** Hub rebuilds itself. `HubSceneInstaller` waits for the project scope, which is already ready (61–89), and shows Home. Hub.unity has its own EventSystem (line 270).

## (f) HUD documents, PanelSettings and installer bindings

**Game.unity in B (as of 03:32)**
- **"Gameplay HUD"** is a scene root (630–646, with Transform father 0 at 677). Its UIDocument (647–663) uses `BoardOverlayPanelSettings` (guid a78dcd4c…, line 659), `GameplayHud.uxml` (guid 922097ae…, 661) and sort order **30** (663).
- **"Board effect counters"** UIDocument (808–829): same PanelSettings, no source file, sort order 20.
- **EventSystem** under SceneScope (125–214): `InputSystemUIInputModule` with the default actions asset, plus `EventSystem`.
- **SceneScope's SceneContext** (232–250) lists two installers (244–246), with auto-inject on (248) and async build callbacks on (249).
- **Installer fields** (755–770) include `m_hudDocument` (765) and an empty `m_uiDocuments` (761).
- **W's Game.unity has neither the EventSystem nor the HUD document** (its roots are at 706–708).

**PanelSettings: `Data/UI/BoardOverlayPanelSettings.asset`** (the same file in both copies)
- Theme is `UnityDefaultRuntimeTheme.tss` (guid 510b6ee1…, lines 15–16).
- Scale With Screen Size, 1080×1920, match width-or-height 0.5 (18, 23–25).
- Because it does not load the project theme, `GameplayHud.uxml` imports `Common.uss` and `GameplayHud.uss` itself (uxml 4–5, 11–12).
- By contrast, Hub's panel uses `ColorBlockJamRuntime.tss` with Expand matching.

**Where a top bar attaches in the UXML**
- `top-area` > `top-content` > `timer-band` (`timer-holder`, `timer-icon`, `timer-label`), then `freeze-counter`, then `info-banner` (uxml 18–51). Everything there is `Ignore` except the banner.
- `GameplayHudView.TopAreaName = "top-area"` (21), exposed as `TopArea` (135).
- `GameplayHud` pads `top-area` from the safe area on the left, top and right (GameplayHud.cs:168).
- **The timer label already belongs to the booster session:** `TimerPresenter` subscribes to `DisplayedSeconds`, `FreezeDisplayedSeconds` and `IsFrozen` (TimerPresenter.cs:74–76).
- The purchase popup is a template instance inside the same HUD document (uxml 10, 103).

**Installer bindings in B (`GameSceneInstaller.cs`)**

| Line | Binding |
|---|---|
| 93 | `BindScriptableObject(m_shapes)` |
| 94 | `BindInstance(art)` |
| 95 | `BindInstance(m_environment)` |
| 96 | `BindInstance<IUiPointerFilter>(UiToolkitPointerFilter(GetPointerVetoDocuments()))` |
| 97 | `IBoardProjector` |
| 98 | `IPauseOverlay` |
| 102–104 | `AttemptCountdown` as `ICountdown` and `IFreezeCountdown` |
| 107–110 | effects |
| 113 | `HeldOutlineView` |
| 117–121 | `BindInterfacesAndSelfTo<…>().AsSingle()` for `GameplayPointerInput`, `BoardPresenter`, `DragInputRouter`, `LevelSession`, `GameplayTickOwner` |
| 125–127 | the loader as `IStartingBoosterSource`; `ISeedSource` |
| 131–134 | `UnavailablePurchaseHost` when there is no HUD document |
| 137 | `TimeFreezeFrostContext` |
| 142–145 | `BoosterTargeting`, `TimeFreezeFrostView`, `StartingBoosterPreparer`, `BoosterUseCoordinator` |
| 149–157 | HUD branch: `BindInstance(GameplayHudContext)`, `InGamePurchaseHost`, `GameplayHud` |
| 160 | `RegisterBuildCallbackAsync(loader.LoadAsync)` |

**Patterns to follow**
- The container disposes in reverse registration order (comments at 115–116, 139–141, 153–154). A pause host or top-bar presenter should be registered after the session so it is disposed first.
- The loader uses `TryResolve` for optional parts (237–241, 128, 158) and `Resolve` for required ones.
- `GameplayHud` and `InGamePurchaseHost` bind lazily, on the first `Show` or `TryShow` (GameplayHud.cs:153–187; InGamePurchaseHost.cs:172–217). `InGamePurchaseHost` marks one constructor overload with `[Inject]` (57–63).
- `ApplicationPauseRelay` uses `[Inject] Construct(...)` (16–20).

## (g) Differences between W and B

**`LevelSession.cs`** (W: 416 lines, changed 09-26 22:19; B: 807 lines, changed 09-27 19:57)
- B adds:
  - the freeze clock taken from the countdown (79);
  - HUD reactive values (40–45, 92–110) with `UpdateHud` and `ResetHud`;
  - booster intents: `TrySubmitBooster`, `CompleteBoosterCommit`, `IsBoosterWritePending`, `HasCommittedBooster`, `SupportsTimeBoosters`;
  - `Play(in OpeningPlan)` with the opening hold;
  - `TryOpenPurchase` and `ClosePurchase`;
  - pending-write guards on Resume, Restart and GoHome (223, 239, 266);
  - the Home background latch and the deferred foreground resume;
  - new `RunTick` phases with early returns (473–496) and a hold-aware timer (507–520);
  - Teardown clearing the intent, the opening and the HUD values.
- The existing public signatures are unchanged.

**`GameSceneInstaller.cs`** (W: 113 lines, 09-28 02:25; B: 178 lines, 09-28 03:33)
- Both bind `HeldOutlineView` and the outline material (W:99, B:113) and `NoPauseOverlay` (W:87, B:98).
- W binds `InactiveCountdown` (90).
- B adds the HUD, frost and reticle fields, the automatic HUD pointer veto, the starting-booster and seed sources, the purchase-host fallback, the booster singletons and the HUD branch.

**`GameAttemptLoader.cs`** (W: 194 lines; B: 320 lines, 09-27 23:20)
- B adds: `IStartingBoosterSource`, `ReadStartingBoosters`, `TryTake`, and `LoadWithStartingBoostersAsync` (149–201: a token linked to the attempt, a switch back to the main thread, a wait while backgrounded on Home, then `SetOpening` and `Play(plan)`). It also has a `TryBuild` that calls `IGameplayHud.Show` (205–244).
- Level selection and camera framing are the same in both. Both still start play automatically, and neither has pause or Home navigation.

**Other differences**
- B adds `IBackButtonSource`, the Back action and the queued tap release in `GameplayPointerInput`, and targeting plus Back reading in `DragInputRouter`.
- B's asmdef adds the Player and UI references.
- B's Game.unity adds the EventSystem and the HUD document.

**Identical in both copies:** the `Domain/Session` state-machine files, `IPauseOverlay`, `NoPauseOverlay`, `GameplayTickOwner`, `ApplicationPauseRelay`, `LevelLaunchResolution`, `UiToolkitPointerFilter` and `BoardOverlayPanelSettings.asset`.

**File-ownership risk:** the top bar, pause and retry work touches `GameplayHud.uxml`/`.uss`, `GameplayHudView`, `GameSceneInstaller` and `Game.unity`. All of these are in the booster session's unintegrated set, and that copy was still changing during this research.