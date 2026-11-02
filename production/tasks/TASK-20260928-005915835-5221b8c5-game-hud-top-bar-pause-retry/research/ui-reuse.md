# Game HUD reuse map: Settings popup, purchase popup pattern, currency holder, theme, and top-area contract

**Path roots used below**
- **WS** = `C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026/Assets/ColorBlockJam`
- **CP** = `C:/Users/e-fur/AppData/Local/Temp/cbj-booster-f1ed7e/Assets/ColorBlockJam`

**Parity between the workspace and the copy:** `diff -rq` finds no difference between WS and CP in `UI/`, `Hub/`, `Presentation/UI/` or `Runtime/Currencies/`. `BoardOverlayPanelSettings.asset` is also identical. Only CP has `Gameplay/UI/` (WS has no such folder). CP's `Scenes/Game.unity` is newer: it has the HUD document and an EventSystem.

---

## 1. Settings popup public API and hosting

### View: `WS/UI/Settings/SettingsPopupView.cs`
- **Constructor.** `new SettingsPopupView(VisualElement screen)` takes the `settings-screen` element (L54-57).
  - It requires `settings-scrim`, `settings-close-button`, `home-button`, `settings-message` and the three toggles (L58-64).
  - If any of these is missing it throws `InvalidOperationException` (L148-158).
- **Name constants** (L20-37): `ScreenName`, `ScrimName`, `SafeAreaName = "settings-safe-area"`, `HomeButtonName`, `WithHomeClass = "settings-screen--with-home"`, and others.
- **Events** (L83-87): `TogglePressed(PlayerSettingId)`, `CloseRequested`, `HomePressed`.
- **The scrim closes the popup by itself.** The view adds a `Clickable` to the scrim that raises `CloseRequested` (L78-80). An in-game host must not register a second scrim callback. The purchase popup is different: its host registers the scrim `ClickEvent` itself (see section 2).
- **`SetVisible`** writes inline `display` (L89-92). The USS default is `display: none` (`SettingsPopup.uss` L12-19).
- **`SetHomeVisible(bool)`** (L106-114) toggles `home-button` display and adds `settings-screen--with-home`.
  - That class tightens row and button spacing (`SettingsPopup.uss` L361-367).
  - With HOME shown the popup is about 1818 px tall at 1080 x 1920 (L354-359).
- **`Dispose`** (L116-135) removes the handlers but does not hide the popup. The host must hide it first.

### Presenter: `WS/UI/Settings/SettingsPopupPresenter.cs`
- **Dependency.** The constructor needs only `IPlayerSettings` (L48-59). `IPlayerSettings` is declared in `WS/Player/IPlayerSettings.cs` (L8-13). `PlayerSaveService` provides it in the project scope (`WS/Player/PlayerDataInstaller.cs` L43), so a Game `SceneContext` child can resolve it.
- **`Bind(ISettingsPopupView, CancellationToken hostToken)`** (L89-123):
  - It throws if already bound.
  - It applies the stored HOME and visibility flags (L115-116).
  - It subscribes to the three settings.
- **`Unbind()`** (L126-146) cancels pending saves.
- **`Show()` / `Hide()`** (L149-172) remember the visible state even while unbound.
- **`SetHomeVisible(bool)`** (L175-179) keeps its value across binds.
- **`CloseRequested`** (L62) fires for the close button and the scrim.
- **`HomeRequested`** (L65) fires only while HOME is visible (L229-236).
- The remarks already name the Game use: "a later Game pause flow maps it to Resume" (L16-18).

### How the Hub hosts it
- **DI.** `HubSceneInstaller.cs` L40-41 binds `SettingsPopupPresenter` and `SettingsOverlay` with `AsSingle`.
- **UXML.** `Hub.uxml` L6 declares the Template. L11 places `<ui:Instance ... picking-mode="Ignore" style="position:absolute;left:0;top:0;right:0;bottom:0;">` inside `overlay-layer` (L10).
- **Bind** (`HubScreen.cs`):
  - It finds the screen and safe area by name (L145-146) and creates the view (L160).
  - It creates `SafeAreaPadding(home, settingsSafeArea, All)` (L169). It measures against `home` because the popup root is `display: none` while closed (L168).
  - It then binds the overlay with `destroyCancellationToken` and registers it with the navigator (L190-191).
- **`SettingsOverlay.cs`:**
  - `Bind` forces HOME hidden before binding and subscribes to `CloseRequested` (L39-50).
  - `Show()` and `Hide()` forward to the presenter (L66-75).
  - A close request calls `navigator.Close(Settings)` (L82-85).
- **Opening.** `HomePresenter.OnSettingsPressed` calls `m_navigator.OpenSettings()` (`HomePresenter.cs` L196-200). `HubNavigator.Open` pushes the overlay and calls `Show` (L69-80). `Close` and `CloseTop` pop the stack and call `Hide` (L82-105).
- **Back.** `HubBackInput` is a code-built `<Keyboard>/escape` InputAction (L17-18, L37-47). Its `performed` callback calls `navigator.CloseTop()` (L64-77). `HubScreen` enables it in Bind (L200) and disables it in Unbind (L210).
- **Unbind order.** `navigator.Unregister(Settings)` (it hides an open popup), then `overlay.Unbind()`, then `view.Dispose()` (`HubScreen.cs` L223-227).

### What an in-game pause host must do (derived from the code above)
1. **UXML placement.** Declare a Template for `project://database/Assets/ColorBlockJam/UI/Settings/SettingsPopup.uxml` and add an `<ui:Instance>` as a late child of `hud-root` (after `booster-bar`).
   - Give the instance `picking-mode="Ignore"` and the absolute full-size inline style, as `CP/Gameplay/UI/GameplayHud.uxml` L103 does.
   - The popup brings its own style sheet (`SettingsPopup.uxml` L5). It relies on `Common.uss` and the font that `hud-root` provides (section 4).
2. **Picking.** Inside the popup, the screen is Ignore, the scrim is Position, the safe area is Ignore and the popup is Position (`SettingsPopup.uxml` L6, 9, 12, 14). All its Labels are already Ignore, which the HUD layout test requires (`CP/Tests/Gameplay/PlayMode/GameplayHudLayoutTests.cs` L107).
   - The TemplateContainer must be Ignore. A full-screen Position container would make `UiToolkitPointerFilter` veto every board press (`CP/Gameplay/Input/UiToolkitPointerFilter.cs` L45-48).
3. **Sorting and draw order.** The HUD UIDocument has sorting order 30. The "Board effect counters" document has 20. Both use `BoardOverlayPanelSettings` (`CP/Scenes/Game.unity` L659-663 and L826-829).
   - Within the HUD, later siblings draw on top. The purchase instance is currently last (L103).
   - A purchase popup stays shown under a Background pause, because it is hidden only when the state leaves Paused (`CP/Gameplay/Boosters/BoosterUseCoordinator.cs` L265-270). The pause popup must either come after it or hide it.
4. **Binding.**
   - Create the view over `root.Q(SettingsPopupView.ScreenName)`.
   - Call `presenter.SetHomeVisible(true)`, then `Bind(view, <host lifetime token>)`.
   - Map `CloseRequested` to Resume and `HomeRequested` to Home.
   - Pad `settings-safe-area` on all edges with `SafeAreaPadding(<hud-root or document root>, safeArea, All)`, never measured against `settings-screen`.
   - Hide the popup before calling `view.Dispose()`.
5. **Presenter source.** Two existing patterns: bind it `AsSingle` in the Game installer as the Hub does, or `new` it inside the host as `InGamePurchaseHost` does for its presenter (`CP/Gameplay/UI/InGamePurchaseHost.cs` L75).
   - Either way, Gameplay must reference `ColorBlockJam.UI` and `ColorBlockJam.Player`. CP's `Gameplay.asmdef` has both (L7, L10). WS's does not (L5-8).
6. **Back.**
   - While Playing, Back only cancels targeting (`CP/Gameplay/Session/DragInputRouter.cs` L72-77).
   - The gameplay map is disabled under Paused, so `WasBackPressed` reports false (`CP/Gameplay/Input/GameplayPointerInput.cs` L130-136).
   - The pause host therefore needs its own code-built escape action, enabled only while the popup shows (the pattern in `InGamePurchaseHost.cs` L37-38, 111, 123, 213-215).
   - Opening the pause menu with Back from Playing is not implemented anywhere (not found).
   - If the purchase and pause Back actions are enabled at the same time, both will fire on one press.
7. **Session calls** (`CP/Gameplay/Session/LevelSession.cs`):

   | Action | Call | Notes |
   | --- | --- | --- |
   | Pause | `Pause(PauseReason.User)` (L195-215) | Allowed only from Playing (`Domain/Session/SessionTransitions.cs` L46-55; the Paused rows L67-87 have no Pause). |
   | Close / scrim / Back | `Resume()` (L221-231) | Refused while a booster write is pending. |
   | HOME | `GoHome()` (L264-279) | The Game-to-Hub scene transition is not found in Gameplay: "scene navigation belongs to the Hub task" (L261). |
   | Background | `NotifyBackground` (L287-326) | Resume on foreground depends on `IPauseOverlay.ResumesOnForeground` (`Session/IPauseOverlay.cs` L4-8). The installer currently binds the interim `NoPauseOverlay` (`CP/Gameplay/Composition/GameSceneInstaller.cs` L90; `Session/NoPauseOverlay.cs` L3-5). The pause popup is meant to replace it. |

8. **Retry: a blocking finding.** `Restart()` (L237-258) is accepted only from Paused or Failed. The Playing rows of `SessionTransitions.cs` (L46-65) have no Restart. This file is identical in WS and CP.
   - A HUD Retry pressed during Playing therefore returns false. It needs either Pause-then-Restart or a change to the Domain transition table.
   - Restart is also refused while a write is pending (L239).
9. **Fit on the Game panel.**
   - The popup is 926 px wide with `flex-shrink: 0` (`SettingsPopup.uss` L50-51), and its USS assumes Expand panels (L2-4).
   - The board overlay panel is MatchWidthOrHeight with match 0.5 (section 4), about 966 units wide at 20:9 (`GameplayHud.uss` L2-4).
   - By that arithmetic the popup fits. No capture confirms it.

---

## 2. How the booster copy embeds the shared purchase popup (the pattern to mirror)

- **UXML** (`CP/Gameplay/UI/GameplayHud.uxml`):
  - L10 declares `<ui:Template name="BoosterPurchasePopup" src=".../UI/Purchase/BoosterPurchasePopup.uxml"/>`.
  - L11-12 add `Common.uss` and `GameplayHud.uss`.
  - L101-103 place `<ui:Instance template="BoosterPurchasePopup" name="booster-purchase-instance" picking-mode="Ignore" style="position:absolute;left:0;top:0;right:0;bottom:0;"/>` as the last child of `hud-root`.
- **DI** (`GameSceneInstaller.cs`):
  - Without a HUD document, L118-123 bind `UnavailablePurchaseHost`.
  - With one, L138-146 bind `GameplayHudContext(m_hudDocument, …)`, then `InGamePurchaseHost` (`AsSingle`) before `GameplayHud`, so the HUD is disposed first.
- **Host** (`InGamePurchaseHost.cs`):
  - The `[Inject]` constructor takes `GameplayHudContext`, `LevelSession`, `IBoosterShop` and `IBoosterInventory` (L57-63). A test constructor takes a `VisualElement` root instead (L66-70).
  - It owns the presenter (L75) and a lifetime `CancellationTokenSource` (L44).
  - `TryShow` (L99-113) binds lazily through `TryBind` (L174-217). `TryBind`:
    - uses `m_context.Document.rootVisualElement` (L186);
    - finds the screen, scrim and safe area by name (L192-194) and logs one error if they are missing (L195-201);
    - creates the view and calls `presenter.Bind(view, m_lifetime.Token)` plus `CloseRequested` (L203-205);
    - registers a scrim `ClickEvent` (L206-207);
    - applies `SafeAreaPadding(root, safeArea, All)` (L210-211);
    - creates the escape InputAction (L213-215).
  - Showing enables Back (L111). `Hide` disables Back and hides the presenter (L116-125).
  - `Close` calls `Hide()` and then `m_session.ClosePurchase()` (L131-140).
  - `Dispose` (L142-170): Back action, scrim callback, presenter, view, safe area, token.
- **Pause wiring** (`BoosterUseCoordinator.cs`):
  - A press on an empty booster calls `purchaseHost.TryShow(def)`, then `session.TryOpenPurchase()`, which enters `Pause(PauseReason.Purchase)` (`LevelSession.cs` L415-423). If the pause is refused, it calls `Hide()` (L86-99).
  - Leaving Paused hides the popup (L265-270).
  - `ClosePurchase` (L179-188).
- **What differs for Settings:**
  - Do not add a scrim callback (see section 1).
  - Call `SetHomeVisible(true)`.
  - Resume uses `Resume()`, not `ClosePurchase()`.

---

## 3. Hub CurrencyHolder template: reuse in Game

- **Template** (`WS/Hub/UI/CurrencyHolder.uxml` L5-15). The file has no `<Style>`.

  | Element | Type and classes |
  | --- | --- |
  | `currency-holder` | `.currency-holder` |
  | `holder-pill` | VisualElement |
  | `holder-text` | Label, `.holder-text .cbj-dark-text` |
  | `holder-icon` | VisualElement |
  | `icon-count` | Label, `.icon-count .cbj-title` |
  | `plus-button` | Button, `.plus-button .cbj-pressable`, with a child `.plus-button__icon .cbj-press-tint` |

  All are Ignore except the Button.
- **Styles and sprites.** All styling lives in `WS/Hub/UI/Home.uss` L107-197:
  - Holder: 300 x 110 with a 48 px left margin (L118-124).
  - Pill: `bg_home_resource_holder`, nine-slice 40/45/40/52 at scale 0.5 (L141-150).
  - Coin icon: `Coin_Single.png` through `.currency-holder--coin .holder-icon` (L136-138).
  - Icon position: absolute, overhanging the right end by 34 px (L159-168).
  - Plus badge: `Btn_circle_green_small` (L176-186) with `ic_plus` (L190-197).
- **Instances and lookup.** `Home.uxml` L86-94 has `currency-holder-life` and `currency-holder-coin` with modifier classes. `HomeView` finds holders by the prefix `currency-holder-` (`HomeView.cs` L67-77). The wrapper class `CurrencyHolderView` is private and nested (L195-236).
- **Balance binding.**
  - `HomePresenter.BindCurrencies` (L117-146) calls `m_wallet.GetBalance(def).Subscribe(binding.OnBalanceChanged)` (L144).
  - `CurrencyHolderBinding.Show` (L221-240) skips an unchanged balance, formats with `CurrencyText.FormatCompact`, and calls `SetText` for Amount-mode currencies (L239).
  - `FormatCompact(1480)` returns `"1.48k"` (`WS/Runtime/Currencies/CurrencyText.cs` L45-72). Each call allocates one string, so the HUD should format only when the balance changes, as Home does.
- **Coin data.** `Data/Currencies/Coin.asset` has id `coin` (L15) and `m_homeDisplay: 0`, the Amount mode (L20).
  - Look it up with `CurrencyCatalog.TryGet` (`CurrencyCatalog.cs` L24-41).
  - The catalog and wallet are bound in the project scope (`PlayerDataInstaller.cs` L38, L43).
- **Reuse as-is: no.**
  - **Styles don't travel.** The UXML can be referenced by asset path, but no styles come with it.
    - Importing `Home.uss` into the HUD is unsafe. Its `.unity-button.settings-button` rule (L199-205) also matches the Settings popup's buttons, which carry the `settings-button` class (`SettingsPopup.uxml` L56, 62, 68, 75, 83).
    - The popup's own selector `.settings-holder > .unity-button.settings-button` (`SettingsPopup.uss` L285) wins on size and background, but Home's `-unity-background-scale-mode: scale-to-fit` would still apply. Whether that breaks the nine-slice is not verified.
    - `Home.uss` also defines generic `.safe-area`, `.safe-content` and `.top-bar` rules (L32, 45, 69).
  - **Code doesn't travel.**
    - `ICurrencyHolderView` is in `ColorBlockJam.Hub` (`IHomeView.cs` L46-51). `CurrencyHolderView` and `CurrencyHolderBinding` are private.
    - Neither Gameplay asmdef references Hub, and DEC-136 says Game cannot.
  - **Reusable pieces:**
    - `CurrencyText` (in Runtime). Gameplay references Runtime, and Runtime references ZString (`Runtime.asmdef` L4). Gameplay itself has no ZString (`CP/Gameplay/UI/HudText.cs` L8), which is fine because only `CurrencyText` calls it.
    - `IWallet` (in Player). CP's Gameplay references Player; WS's does not.
- Home's `plus-button` has no click handler; `HomeView` wires only the level and settings buttons (L90-93).

---

## 4. Shared classes, fonts and themes

**`WS/UI/Common/Common.uss` classes:**

| Selector | Lines | Effect |
| --- | --- | --- |
| `:root` tokens | 7-12 | `--cbj-title-color`, `--cbj-title-outline-color`, `--cbj-pressed-tint`, `--cbj-dark-text-color` |
| `.unity-button.cbj-pressable`, `.cbj-pressable` | 20-44 | Resets the default button look; 0.08 s transitions |
| `:hover` / `:focus` | 46-49 | Keeps the background transparent |
| `:active` | 51-56 | Scale 0.92 and pressed tint |
| `.cbj-pressable:active > .cbj-press-tint` | 59-61 | Tints direct children only |
| `:disabled` | 63-65 | Opacity 0.6 |
| `.cbj-title` | 68-75 | Bold white text, 2 px navy outline, centred |
| `.cbj-dark-text` | 78-83 | Dark bold text for light panels |
| `.cbj-heavy-text`, `__back`, `__front` | 96-113 | Two-layer heavy text; both layers must carry the same text |

**Hub panel versus Game panel:**

| | Hub panel | Game panel |
| --- | --- | --- |
| Asset | `WS/Data/UI/HubPanelSettings.asset` | `Data/UI/BoardOverlayPanelSettings.asset` |
| Theme | `ColorBlockJamRuntime.tss` (L15, GUID `67cafb…`) | `Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss` only (L15, GUID `510b6e…`) |
| Theme contents | Imports the default theme and `Common.uss` and sets the LiberationSans font on `:root` (`ColorBlockJamRuntime.tss` L1-11) | None of the project styles or font |
| Scale | Expand (`m_ScreenMatchMode: 2`, L24), 1080 x 1920 | MatchWidthOrHeight, match 0.5 (L24-25) |

- **Consequence for the Game panel:** `GameplayHud.uxml` imports `Common.uss` itself (L11), and `GameplayHud.uss` sets `-unity-font-definition` on `.hud-root` (L21). A test checks the font (`GameplayHudLayoutTests.cs` L137-141).
- Any popup or template placed under `hud-root` inherits both. A separate UIDocument would need its own imports.
- **Unverified:** whether the `:root` custom properties in `Common.uss` resolve when the sheet is attached with `<Style>` on `hud-root` instead of through the theme. The title colours and the press tint use them.
- **EventSystem:** CP's `Game.unity` has an EventSystem (L137) with the Input System UI module script (L167). WS's `Game.unity` has none; only the "Board effect counters" document is there (L666-687).

---

## 5. HUD top area in the copy and what a separate top-bar template must keep

- **Structure** (`GameplayHud.uxml` L18-51), all nodes Ignore except `info-banner` (Position) and `info-close` (Button):
  - `top-area`
    - `top-content`
      - `timer-band`
        - `timer-holder`: `timer-icon`, `timer-label` (L24, Label `.cbj-title`, text "0:00")
        - `freeze-counter`: `freeze-icon`, `freeze-bar` containing `freeze-fill` and `freeze-label`
      - `info-banner`: `info-icon`, `info-text`, `info-header-row` / `info-header` / heavy pair `info-title-back` and `info-title`, `info-close`
  - The Level badge, "Time" caption, Retry, Pause and coin holder do not exist yet.
- **USS** (`GameplayHud.uss`):

  | Selector | Lines | Rule |
  | --- | --- | --- |
  | `.hud-root` | 13-22 | Absolute, column, `justify-content: space-between`, sets the font |
  | `.top-area` | 60-62 | `flex-shrink: 0` |
  | `.top-content` | 64-67 | `align-items: center`, `padding-top: 28px` |
  | `.timer-band` | 69-71 | Centred |
  | `.timer-holder` | 74-89 | 320 x 112, `bg_gameplay_time_holder` sliced 50, tint transition |
  | `.timer-icon` | 92-100 | Absolute at left -36 |
  | `.timer-label` | 102-105 | 64 px, 3 px outline |
  | `.hud--frozen .timer-holder` | 108-110 | Icy tint |
  | `.freeze-counter` | 112-118 | `display: none` |
  | `.hud--frozen .freeze-counter` | 120-122 | Shows the counter |
  | `.freeze-bar`, `.freeze-fill` | 132-160 | The view writes the fill width as a percentage |
  | `.info-banner` | 168-184 | `display: none`, `margin-top: 56px` below the band |
  | `.hud--targeting .info-banner` | 186-188 | Shows the banner |

- **How the view queries.**
  - `GameplayHud.TryBind` finds `hud-root` with `Document.rootVisualElement.Q("hud-root")` (`GameplayHud.cs` L155-156).
  - The `GameplayHudView` constructor calls `Require` with `root.Q<T>(name)` from `hud-root` (`GameplayHudView.cs` L84-97, L271-280). `Q` descends into TemplateContainers.
  - Booster buttons are queried from `BoosterBar` (L107).
  - A missing name throws inside the constructor. `TryBind` has no catch (`GameplayHud.cs` L153-187), so one missing element breaks the whole HUD, booster bar included.
- **Safe area.** `SafeAreaPadding(hud, TopArea, Left | Top | Right)` and `SafeAreaPadding(hud, BoosterBar, Left | Right | Bottom)` (`GameplayHud.cs` L168-179). A test asserts the top-area padding (`GameplayHudCompositionTests.cs` L225).
- **TimerPresenter** (`CP/Gameplay/UI/TimerPresenter.cs`) writes only through `IGameplayHudView.SetTimer`, `SetFreezeCounter` and `SetFrozen` (L111-137). It uses prebuilt `HudText` strings (`HudText.cs` L44-69).

**What a separately owned top-bar template must keep:**
1. **Queried names**, exactly once each under `hud-root` and of the right type:
   - `top-area`
   - `timer-label` (Label)
   - `freeze-fill`
   - `freeze-label` (Label)
   - `info-banner`, `info-icon`, `info-text` (Label), `info-title` (Label), `info-title-back` (Label), `info-close` (Button)
2. **Names and classes the tests check:** `timer-band`, `timer-holder`, `freeze-counter` exist (`GameplayHudLayoutTests.cs` L63-71). `top-content`, `timer-band`, `timer-holder` and `freeze-counter` are Ignore (L94-98). The timer does not veto board presses (L121).
3. **Classes the root-state selectors target:** `timer-holder`, `freeze-counter` and `info-banner`, kept as descendants of `hud-root`. The view toggles `hud--frozen` and `hud--targeting` on `hud-root` (`GameplayHudView.cs` L198, 203, 213).
4. **Picking modes:**
   - Every Label must be Ignore and every Button Position (`GameplayHudLayoutTests.cs` L107-108).
   - Containers must be Ignore, including the `<ui:Instance>` TemplateContainer.
5. **Placement:**
   - Put the bar inside `top-content`, not as a new flow child of `hud-root`. `hud-root` uses space-between, so a new flow child would be pushed to the middle.
   - Absolute children must also sit under `top-content`, because absolute children ignore `top-area`'s safe-area padding (the D-008 note at `GameplayHud.uxml` L18).
   - Add no padding rule to `top-area`; `SafeAreaPadding` writes all four paddings inline (`SafeAreaPadding.cs` L94-97).
6. **Banner position.** The info banner sits in flow below the timer band (`margin-top: 56px`). A taller top bar moves it down.
7. **Pattern for your own elements:** a separate view that takes `GameplayHudContext` and queries its own names from the same tree, as `InGamePurchaseHost` does, without changing `GameplayHudView` or `IGameplayHudView`.
8. **Ownership.** Moving the timer and freeze elements into your template transfers files the booster session owns (`GameplayHud.uxml`/`.uss`). That needs its agreement.