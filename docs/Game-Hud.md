# Game HUD: top bar, pause menu and Retry

Decisions: [DEC-153, DEC-154, DEC-155](../PROJECT.md). References: [21 top bar, 22 Retry, 20 pause, 02 targeting](Color-Block-Jam-Visual-References.md#ref-21).
Task: `production/tasks/TASK-20260928-005915835-5221b8c5-game-hud-top-bar-pause-retry` (contract, design, verification captures).

## Layout

`UI/Game/GameplayHud.uxml` (Booster's HUD document, `BoardOverlayPanelSettings`, about 966 units wide at 20:9)
instances two task templates:

| Template | Where | Content |
| --- | --- | --- |
| `UI/Game/TopBar/GameTopBar.uxml` | inside `top-content`, in place of the old timer pill | `btn_level_bg_normal` badge and Time panel ("Level N", clock, "Time", timer field), Retry above Pause, coin holder, Time Freeze counter |
| `UI/Game/Overlays/GameOverlays.uxml` | last child of `hud-root` (above the purchase popup) | the shared Settings popup with HOME, and `RestartConfirmPopup.uxml` |

The top bar keeps Booster's names (`timer-band`, `timer-holder`, `timer-icon`, `timer-label`, `freeze-counter`,
`freeze-icon`, `freeze-bar`, `freeze-fill`, `freeze-label`); Booster's view and `TimerPresenter` write them unchanged.
Every text is a `.cbj-heavy-text` pair. `.hud--frozen` shows a frost layer on the timer field and the freeze counter;
`.hud--targeting` hides the whole bar so the info banner takes the top.

Re-measured from the reference on 2026-09-28 (user: "top bar ... resimdeki gibi olmalı"), at 20:9 (reference pixels
x 1.047): the `btn_level_bg_normal` art is 720 x 265 units (scale 0.815); Retry (tinted to the reference's golden
brown) and Pause are 112 x 116 boxes 25 units beside it, centred on its height; the coin holder hangs under Pause in
the same column and reaches 57 units past its right edge; the freeze counter sits under the Time field in the art's
column. The group is centred with a 4 % right padding, so at 966 units it lands as in the reference and on 16:9 it
stays together. The timer reads `mm:ss` ("02:55", RULE-21).

## Code (`Runtime/Gameplay/Hud`, namespace `ColorBlockJam.Gameplay.Hud`)

| Type | Responsibility |
| --- | --- |
| `IGameSessionControl`, `LevelSessionControl` | the narrow session port the HUD uses (state, pause reason, opening, write pending, Pause/Resume/Restart/GoHome) |
| `TopBar/GameTopBar`, `TopBarView`, `TopBarPresenter` | binds the bar on Home (`IGameTopBar.Show(level)` from the loader), level number, coin text on balance change, button enabled states |
| `AttemptLevelNumber` | the 1-based "Level N" texts shared by the badge and the Retry title |
| `Menu/GameMenuFlow` | the single owner of the in-game overlays: pause, Retry confirmation, Back fallback, background return, HOME |
| `Menu/PauseMenuHost`, `RestartConfirmHost`, `RestartConfirmView`, `OverlayBackInput` | popup hosting and the overlay Escape action |
| `PopupPauseOverlay` | `IPauseOverlay` with `ResumesOnForeground` false, bound only with a HUD |
| `Navigation/GameExitNavigator`, `IHubSceneTransition`, `HubSceneTransition` | HOME: `GoHome()` then the Hub scene flow (Test Level ends Play Mode) |
| `IApplicationPauseListener`, `Session/IGameplayBackFallback` | seams used by `ApplicationPauseRelay` and `DragInputRouter` |

## Rules

| Input | Playing | Paused (our overlay) | Won / Failed |
| --- | --- | --- | --- |
| Entering the state | - | - | Failed (timer at zero): the fail popup (the Retry confirmation) opens at once, after Teardown; the pause menu only if the popup cannot show |
| Pause button | Pause(User), then the menu | covered by the scrim | Won: menu without a session pause; Failed: off |
| Retry button | Pause(User), then the confirmation; off in the opening hold and while a write is pending | covered | Failed: covered by the fail popup; Won: off |
| Back | targeting cancel first, else the menu | close: Resume | Won: close the menu; Failed: like the fail popup's X |
| Backgrounding | Pause(Background) only; no menu opens (DEC-182) | the overlay stays | Won: Finish ends at once |
| Background return | the session resumes; a zero-stock purchase popup hides with it; a pending booster write delays it | the overlay stays; its Close resumes | - |
| HOME | - | GoHome, then Hub (once) | GoHome, then Hub |
| Fail popup X, scrim | - | - | GoHome, then Hub (once); the popup stays up during the exit |
| Confirm Retry | - | Restart once from LevelData, no refund or refill | Restart once |

The fail popup reuses `RestartConfirmPopup.uxml` ("Level N", heart "-1", "You will lose 1 life!", Retry). Lives
(DEC-174): entering Failed, the in-level Retry's confirm and HOME from a level in progress each cost the attempt's one
life through `AttemptLifeCost`; the fail popup's Retry takes no second life, HOME after Won is free, and the overlay's
actions stay off while the life saves. With no life left, Retry returns to Hub, which opens the life refill popup.
Failed tears the board down before the popup opens; the static
result background capture (PROJECT.md, Won Finish) is not built yet, so the popup dims the empty play area.

Nothing here runs in the Playing tick: all updates follow presses, state changes, balance changes or a background
return. Every subscription, UI callback, input action and cancellation source is released by its owner's Dispose.

## Level Complete (DEC-159, DEC-173)

`UI/Game/Overlays/LevelCompletePopup.uxml`, the last popup in `GameOverlays.uxml`, follows reference 05 at 20:9 without
its X2 ad button (sizes in panel units, 1 reference pixel = 1.047 units):

| Part | Built from |
| --- | --- |
| Scrim | 97 % black; UI Toolkit blends in linear space here, so this shows the reference's fifth of the brightness over the top bar, board and booster bar |
| Title | two `ArcTextElement` lines, "LEVEL" and "COMPLETE", on a circle of radius 2700; each letter stacks a dark lower edge, a gold rim, a maroon stroke and a cream face, and the letters overlap so each word gets one outline |
| Coins | eighteen `ic_coin_single` elements in the reference pile's 484 x 312 bounds (a lying coin turns its art so the rim faces down, then squashes) over two `SunburstElement` layers that draw the reference's seven rays, measured from its pixels: a bright beam with a dim beam beside it on its clockwise side, then a dark gap; each beam is evenly bright across with soft sides and fades out about with the square of its distance to the tip |
| Button | one `btn_popup_blue`, 397 x 173, 77 under the coins: Continue over "10" and a coin; Retry after a failed save |
| Confetti | Epic Toon FX `ConfettiBlastRainbow`, shown through `UiParticleOverlay` between the scrim and the content, bursting from behind the coin pile when the popup opens |

While the popup shows, `LevelCompleteHost` puts `hud--result` on the HUD root, so `GameplayHud.uss` fades the booster
bar further and keeps the disabled Retry button opaque under the scrim, as in the reference. The failed-save note sits
in the gap under the coins, so it moves neither the pile nor the button.

`LevelCompleteFlow` drives the one button: Continue loads the next level number once the win save committed, Retry
repeats a failed save, and Back acts as Home after the commit. Level numbers keep counting past the last database
level: Level 6 of the five-level case plays level 1's board and reads "Level 6" (`PlayerProgress.GetDatabaseIndex`,
`LevelLaunchResolution`); `GameAttemptLoader` sets `AttemptLevelNumber` on Home and the top bar only shows it. Only a
Level Editor Test Level outside the active database, which saves nothing, shows Home instead of Continue.

The confetti's owner is `LevelCompleteHost`: it builds the stage once on Home (`IOnityInitializable`), a disabled
orthographic camera on the built-in UI layer 1000 units below the board plus one instance of the effect; `Show`
creates a screen-sized render texture and starts the camera and the burst, and one PrimeTween delay ends the show after
the effect's longest particle life (2.2 s), turning the camera off and releasing the texture. `Hide` and `Dispose`
stop it at once. UI Toolkit panels draw after every camera in Unity 2022.3, which is why the effect goes through a
render texture instead of the world.

## Verification

Isolated-copy runs and a rehearsal on the live workspace: EditMode Hud 113/113, PlayMode Hud 20/20, full EditMode and
PlayMode suites green, chief visual validator APPROVE on captures at 1080 x 1920 and 1080 x 2400 (safe-area inset).
Device checks (IL2CPP, Android Back and backgrounding on the phone) remain release checks.
Fail popup (DEC-160), 2026-09-28 on the live workspace: EditMode Hud 127/127, PlayMode Hud 21/21 including
`GameHudFailTests` (real Game scene: countdown to zero, Failed, popup with the level title, Pause off, Retry rebuilds).
Close-to-Home is covered in EditMode only; no device check yet.
Level Complete (DEC-173), 2026-09-28: EditMode `ColorBlockJam.Tests.Gameplay.Hud.LevelComplete` 25/25 through the
shared batch in the live Editor; PlayMode HUD and Hub 59/59 in a mirror copy (the unfocused live Editor cannot run
PlayMode); chief visual validator APPROVE on captures at 1080 x 1920 and 1080 x 2400. UI Toolkit's output here steps
in 1/255 linear units, in the Game view back buffer too, so the rays' faint tips can show thin rings; check them on the
phone. If they show, dither the ray tail.
