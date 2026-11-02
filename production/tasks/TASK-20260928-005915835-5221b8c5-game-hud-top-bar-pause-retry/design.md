# Game HUD top bar, pause menu and Retry: design

Decision owner: session "Oyun sahnesi UI hazırlama [4effc5]" (Opus 5.5). Research maps: `research/*.md`
(requirements, session, ui-reuse, ownership, visual, critic). References: `references/*.png` plus the user's
Retry popup screenshot described in section 6 (it exists only in the conversation).

## 1. User decisions (2026-09-28)

- Build the Game-scene HUD top part and the Pause and Retry functions, together with the Booster session [f1ed7e],
  delegating to sub-agents.
- The pause menu is the Hub Settings popup plus the HOME button (DEC-135/136).
- Retry asks for confirmation ("Onay popup'ı sor"), styled like the user's Retry reference (section 6).
- While a booster targets, the info banner replaces the top bar, as in reference 02 ("Referans 02 gibi").
- Approved: `bg_home_coin_holder.png.meta` border and Full Rect, the integration lines in section 8, and a scoped
  Plastic checkin after verification ("Onayla + checkin").

Decisions taken by the owner under those answers:

- Coin plus: press feedback only (Shop locked, RULE-24, same as Home AC-1.6).
- Retry is disabled during the starting-booster opening hold (a restart would discard already debited starting
  boosters) and while a booster write is pending. Pause stays available in the opening hold (its progress survives).
- Won/Failed have no result popup yet. Interim: Pause opens the same menu without a session pause, so HOME stays
  reachable; Retry in Failed opens the confirmation and restarts; Retry in Won does nothing.
- HOME during the Level Editor's Test Level ends Play Mode (DEC-127: test play starts no runtime scene flow).
- The confirmation's "You will lose 1 life!" is placeholder copy: lives stay the static placeholder (NG-3), so no
  life is deducted.
- Timer text stays `m:ss` (RULE-21, Booster's prebuilt table); the reference's `mm:ss` is not adopted.
- The HUD panel stays `BoardOverlayPanelSettings` (match 0.5): at 20:9 it is about 966 units wide, so no fixed width
  may exceed 966 and the bar must fit with side margins.

## 2. Paths and ownership

Development happens in the isolated copy `%TEMP%/cbj-hud-4effc5` (a snapshot of the Booster copy after its
held-outline merge; baseline manifest in the session scratchpad). The shared workspace is written only in the
integration window (section 8), after Booster's checkin and the folder-reorg move, at the final paths:

| Content | Copy path (pre-move) | Final path (post-move) |
|---|---|---|
| New C# | `Assets/ColorBlockJam/Gameplay/Hud/**` | `Assets/ColorBlockJam/Runtime/Gameplay/Hud/**` |
| New UXML/USS | `Assets/ColorBlockJam/UI/Game/TopBar/**`, `UI/Game/Overlays/**` | same (UI/ does not move) |
| New back-fallback interface | `Gameplay/Session/IGameplayBackFallback.cs` | `Runtime/Gameplay/Session/IGameplayBackFallback.cs` |
| New tests | `Tests/Gameplay/EditMode/Hud/**`, `Tests/Gameplay/PlayMode/Hud/**` | same (Tests/Gameplay does not move) |

Namespaces: `ColorBlockJam.Gameplay.Hud` (sub-namespaces `.TopBar`, `.Menu`, `.Navigation` allowed). Same
`ColorBlockJam.Gameplay` assembly (it already references Runtime, Player, UI, Onity, Input System). No new asmdef.

## 3. Top bar template (`UI/Game/TopBar/GameTopBar.uxml` + `GameTopBar.uss`)

Instanced inside Booster's `top-content` in place of its `timer-band` block. It keeps Booster's element names so
`GameplayHudView`/`TimerPresenter` work unchanged: `timer-band`, `timer-holder`, `timer-icon`, `timer-label` (Label),
`freeze-counter`, `freeze-icon`, `freeze-bar`, `freeze-fill`, `freeze-label` (Label). Each name appears once under
`hud-root`. Every container and Label is `picking-mode="Ignore"`; only Buttons are `Position`.

Target look: `references/ref-hud-top-bar-level29.png` (measurements in `research/visual.md` section 1).

```
game-top-bar (row wrapper, align-self: stretch; hidden under .hud--targeting)
  game-top-bar__main (row, space-between, align-items center)
    level-time            Btn_levelBg_normal whole sprite, scale-to-fit, ~640 x 236 (source 883 x 325)
      level-badge         absolute over the circle (~4..31 % x)
        level-caption     Label "Level"        (~44 px, cbj-title)
        level-number      heavy pair, front named "level-number" (~84 px)
      time-area           absolute over the bar (~36..94 % x)
        time-caption-row  timer-icon (ic_time ~46 px) + Label "Time" (~48 px)
        timer-band
          timer-holder    bg_gameplay_time_holder, slice 50 (Booster border) scaled with -unity-slice-scale
                          so a ~270 x 86 field keeps its rounded ends
            timer-label   Label (~64 px, 3 px outline)
    top-buttons (column, gap ~12)
      retry-button        Button, Btn_square_retry ~104 x 108, child icon ic_restart (cbj-press-tint)
      pause-button        Button, Btn_settings_normal ~104 x 108, child icon ic_pause ~50 x 52
  game-top-bar__sub (row, space-between, margin-top ~8)
    freeze-counter        under the Time field (Booster's children kept; shown only under .hud--frozen)
    coin-holder           bg_home_coin_holder pill, nine-slice 24/24/25/24 (USS left top right bottom), ~64 high
      coin-icon           Coin_Single ~54 px, left
      coin-amount         Label, compact amount (~52 px, cbj-title)
      coin-plus-button    Button, Btn_circle_green_small ~64 x 66 + ic_plus, right end
```

Sizes are starting points at the 1080 x 1920 reference; tune them against the reference captures. Hard rules: the
whole bar fits 966 units wide with at least 24 units at each side; icons and buttons keep their pixel aspect
(fixed-class sprites use scale-to-fit); only the nine-slice sprites stretch. Styles:

- `GameTopBar.uxml` loads `GameTopBar.uss` with `<Style>`; it relies on `hud-root`'s font and `Common.uss`.
- The timer/freeze layout rules move out of Booster's `GameplayHud.uss` into `GameTopBar.uss` at integration
  (section 8), keeping the state semantics: `.hud--frozen .timer-holder` icy tint, `.freeze-counter { display: none }`,
  `.hud--frozen .freeze-counter { display: flex }`, the freeze bar fill width written by the view as a percentage.
- `.hud--targeting .game-top-bar { display: none; }` so Booster's in-flow banner rises to the top (reference 02).
- Retry/Pause use `.cbj-pressable`; disabled uses Common.uss `:disabled`.
- All sprites used are already packed: `Btn_levelBg_normal`, `Btn_settings_normal`, `bg_home_coin_holder`,
  `Coin_Single`, `Btn_circle_green_small`, `ic_plus` (HomeSettings); `bg_gameplay_time_holder`, `ic_time`,
  `Btn_square_retry`, `ic_restart`, `ic_pause`, `Btn_popup_green`, popup shell (HudPopups); `Heart` (HomeSettings).
  No atlas change.

## 4. C# responsibilities

Every class starts with a one-sentence responsibility summary; every Subscribe/async/action names its owner and stop
point (PROJECT.md Rules 3-4, Performance Rule 7). No `Update`, coroutines, `async void`, static mutable state or
`Time.timeScale`.

- `IGameSessionControl` + `LevelSessionControl`: the narrow session port the HUD uses (State, PauseReason,
  IsOpening, IsWritePending as reactive values; Pause(User), Resume, Restart, GoHome). The adapter wraps
  `LevelSession` and, when bound, `BoosterUseCoordinator.IsBusy`. Lets the flow be tested with a fake.
- `GameTopBar` (`IGameTopBar.Show(AttemptLevel)`): binds lazily to the HUD document through `GameplayHudContext`
  (Booster's pattern), owns `TopBarView` and `TopBarPresenter`; disposed by the scene container before the session.
- `TopBarPresenter`: level number = 1 + index of the attempt's `LevelId` in the active `LevelDatabase` (one scan on
  Home when Show runs; works for Hub launch, direct Play and Test Level); coin amount from `IWallet` +
  `CurrencyCatalog` coin, formatted with `CurrencyText.FormatCompact` only when the balance changes; button enabled
  states from State/IsOpening/IsWritePending changes (never per tick). Forwards Retry/Pause presses to the flow;
  coin plus does nothing beyond press feedback.
- `GameMenuFlow` (the owner of the in-game overlays): one overlay at a time (None, PauseMenu, RestartConfirm).
  Implements `IGameplayBackFallback` and `IApplicationPauseListener`. Rules:
  - Pause press or Back fallback: Playing -> `Pause(User)`, show PauseMenu only if accepted. Won/Failed -> show
    PauseMenu without a session call. Otherwise nothing.
  - Retry press: Playing (not opening, no write pending) -> `Pause(User)`, show RestartConfirm. Failed -> show
    RestartConfirm. Otherwise nothing.
  - Close, scrim or overlay Back: Paused -> `Resume()`, hide only if accepted (a pending write keeps it open with
    Close disabled). Won/Failed -> hide.
  - Confirm Retry: `Restart()` once (button disabled after the press); hide on success.
  - HOME: `IGameExitNavigator.TryGoHome()` once.
  - State -> Paused with reason Background and no overlay -> show PauseMenu. State -> Playing or Home -> hide.
  - Foreground (`OnApplicationPauseChanged(false)`): Paused with reason Background and no overlay -> show
    PauseMenu. This covers Paused(Saving)/Paused(Purchase) -> Background, which raises no State notification.
    A focus change without backgrounding never opens the menu.
- `PauseMenuHost`: hosts the shared `SettingsPopupView`/`SettingsPopupPresenter` (IPlayerSettings from the project
  scope), `SetHomeVisible(true)`, maps CloseRequested (close and scrim) and HomeRequested to the flow, pads
  `settings-safe-area` with `SafeAreaPadding(<hud root>, safeArea, All)`, hides before disposing the view. It adds
  no scrim callback of its own (the view already raises CloseRequested for the scrim).
- `RestartConfirmHost` + view: section 6; header text "Level N" from the top bar's number.
- `OverlayBackInput`: one code-built `<Keyboard>/escape` action, enabled only while the flow shows an overlay,
  `performed` -> flow's overlay Back. Disabled and disposed with the flow.
- `PopupPauseOverlay : IPauseOverlay` (`ResumesOnForeground => false`): bound instead of `NoPauseOverlay` only when
  the HUD document exists, so `LevelSession` never auto-resumes behind the menu. Without a HUD, the existing
  `NoPauseOverlay` binding stays (a HUD-less scene must not stay paused with no UI). A separate class avoids a
  constructor cycle (LevelSession needs IPauseOverlay; the flow needs LevelSession).
- `GameExitNavigator` (`IGameExitNavigator`): `GoHome()` must succeed first; then, in the Editor during Test Level
  (`LevelTestPlayRequest`), end Play Mode; otherwise `OnitySceneFlow.TransitionAsync(profile, OnitySceneFlowStateId.Hub,
  null, lifetimeToken)` with a one-launch guard and a lifetime `CancellationTokenSource` cancelled in Dispose
  (mirror `Hub/Launch/LevelLauncher`). The Single load unloads Game and disposes it mid-await: catch
  `OperationCanceledException` and touch nothing afterwards; log other failures and release the guard. No loading
  screen (DEC-140).

## 5. Back routing (one handler per state)

| State | Handler |
|---|---|
| Playing, targeting active | Booster router cancels targeting (unchanged) |
| Playing, no targeting | router -> `IGameplayBackFallback.OnBack()` -> pause menu |
| Playing, opening hold | not read (router does not run); the Pause button still works |
| Paused(Purchase) | Booster's purchase host (unchanged) |
| Paused with our overlay | `OverlayBackInput` -> Resume / cancel confirmation |
| Paused(Background) with the purchase popup still up | our pause menu shows on top; its Close -> Resume, and Booster hides its popup when State leaves Paused |
| Won/Failed with our menu | `OverlayBackInput` -> hide |

Verify that one Escape press is handled once (the gameplay map is disabled while Paused, and an action enabled after
the press does not see it).

## 6. Retry confirmation popup (`UI/Game/Overlays/RestartConfirmPopup.uxml` + `.uss`)

User reference (2026-09-28, conversation only): the dimmed game behind a cyan `bg_popup` shell; a
`bg_popup_header` tab reading "Level 34"; the red `Popup_Close_Button` at the top-right corner; a
`bg_popup_info_holder` inset holding a dark blue icon box with a broken red heart and a white "-1"; below it
"You will lose 1 life!" in white heavy text; a green `Btn_popup_green` button "Retry". Same shell layout as the
booster purchase popup (reference 01); copy its sizes from `UI/Purchase/BoosterPurchasePopup.uss` into the new USS
with a comment naming the source (do not import or edit the purchase USS).

No broken-heart art is supplied: use `Icons/Currency/Heart.png` whole with a heavy "-1" label over it. Element names:
`restart-confirm-screen` (Ignore, hidden by default), `restart-confirm-scrim` (Position), `restart-confirm-safe-area`
(Ignore), `restart-confirm-popup` (Position), `restart-confirm-title` (front heavy label), `restart-confirm-heart`,
`restart-confirm-penalty` ("-1"), `restart-confirm-message`, `restart-confirm-retry` (Button),
`restart-confirm-close` (Button). All Labels Ignore; every title/button label is a `.cbj-heavy-text` pair.

`UI/Game/Overlays/GameOverlays.uxml` holds one full-size Ignore instance of the shared
`UI/Settings/SettingsPopup.uxml` and one of `RestartConfirmPopup.uxml`, so Booster's `GameplayHud.uxml` needs a
single instance line for both (drawn after the purchase popup, on top).

## 7. Performance and lifecycle

- The Playing tick path gains no work: all updates are event-driven on transitions, balance changes or presses.
- Text allocation happens on Home (level number), on balance change and on overlay show, never per frame.
- Each host owns its subscriptions in a `CompositeDisposable` cleared on unbind/dispose; the navigator owns its CTS;
  the back action is disabled on hide and disposed with the flow; views unregister callbacks in Dispose.
- Register the new singletons after `LevelSession` in the installer so the container disposes them first.

## 8. Integration window (shared workspace, after Booster checkin and the reorg move)

Booster [f1ed7e] granted the window for its files after its checkin; the reorg session [7d9d8c] moves folders first.
Re-read each file and its live hash before editing; apply only these hunks:

1. `UI/Game/GameplayHud.uxml` (moved by the reorg): declare the GameTopBar and GameOverlays templates; replace the
   `timer-band` + `freeze-counter` block inside `top-content` with the GameTopBar instance; add the GameOverlays
   instance after `booster-purchase-instance`.
2. `UI/Game/GameplayHud.uss`: move the timer/freeze layout rules into `GameTopBar.uss` (state semantics kept).
3. `Runtime/Gameplay/Composition/GameSceneInstaller.cs`: HUD branch binds `PopupPauseOverlay` (instead of
   `NoPauseOverlay`), `LevelSessionControl`, `GameExitNavigator`, the hosts, `OverlayBackInput`, `GameMenuFlow`,
   `GameTopBar`.
4. `Runtime/Gameplay/Composition/GameAttemptLoader.cs`: after `IGameplayHud.Show(level)`, `TryResolve` the
   `IGameTopBar` and `Show(level)`.
5. `Runtime/Gameplay/Session/DragInputRouter.cs`: when Back was pressed and targeting is inactive, call the optional
   back fallback (set through a setter or optional injection; absent = today's behaviour).
6. `Runtime/Gameplay/Composition/ApplicationPauseRelay.cs`: after `NotifyBackground`, forward to the optional
   `IApplicationPauseListener`.
7. `Art/UI/Panels/bg_home_coin_holder.png.meta`: border 24/24/25/24 (L,B,R,T per the importer's order recorded in
   docs/UI-Asset-Import.md), Mesh Type Full Rect; GUID unchanged; through the live Editor importer API, not YAML.
8. Docs: new DEC rows in PROJECT.md (next free numbers at write time), docs/UI-Asset-Import.md coin-holder row,
   docs/Color-Block-Jam-Visual-References.md reference 21 (HUD top bar, from `references/ref-hud-top-bar-level29.png`
   copied to `docs/media/color-block-jam/user-references/21-game-hud-top-bar.png`) and a text-only entry for the Retry
   popup reference, docs/Game-Hud.md (short design summary).

No Game.unity edit is expected (the HUD UIDocument, EventSystem and ApplicationPauseRelay already exist; the unused
`m_resumeOnForeground` value stays for the HUD-less path). If one becomes necessary, stop and ask.

## 9. Verification

- Roslyn compile check after each change (copy); Unity compile in the copy; later the live Editor via MCP.
- EditMode (`Tests/Gameplay/EditMode/Hud/**`): flow rules in section 4 with fakes (every row of section 5 that is
  flow-owned), top-bar presenter (level number incl. a Test-Level-style ID, coin formatting only on change, enabled
  matrix), navigator guard with a fake transition.
- PlayMode (`Tests/Gameplay/PlayMode/Hud/**`): the real Game composition: elements exist once; Booster's timer still
  updates; Pause -> Paused(User) with the Settings popup and HOME visible; Close -> Playing with unchanged time;
  Retry -> confirmation -> Restart (generation + 1, Playing); keyboard Escape via the Input System test fixture;
  targeting hides the bar; top-bar buttons veto board presses and empty bar areas do not; lifecycle cleanup.
- Booster's existing HUD/Gameplay suites still pass (`GameplayHudLayoutTests` constrains names and picking modes).
- Captures at 1080 x 1920 and 1080 x 2400 (windowed copy run, UI Toolkit): idle HUD, frozen HUD, targeting,
  pause menu, Retry confirmation. Chief visual validator: an Opus 5.5 read-only reviewer against the references.
- Board clearance under the taller bar (AC-3.11) is checked on those captures; camera framing belongs to the
  Environment owner, so a clash is reported, not fixed here.
