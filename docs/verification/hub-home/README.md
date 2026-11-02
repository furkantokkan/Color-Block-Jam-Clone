---
title: Hub Home captures
created: 2026-09-27
modified: 2026-09-27
type: verification
status: active
tags: [hub, home, ui-toolkit, verification]
---

# Hub Home captures

Evidence for contract scenarios 3, 4 and 7 of
`TASK-20260926-235915697-461b4f9c-hub-home-screen`, compared with
[reference 06](../../media/color-block-jam/user-references/06-home-tabs.png).

| File | What |
| --- | --- |
| `home-1080x1920.png` | Home at the 1080 x 1920 reference size, full safe area. |
| `home-1080x2400-notch.png` | Home at 1080 x 2400 (20:9) with a simulated notch safe area: top inset 110 px, bottom inset 70 px. |
| `home-1080x1920-level-pressed.png` | The Level button held down (`.cbj-pressable:active`: scale 0.92, darker tint). |
| `home-1080x1920-shop-pressed.png` | The locked Shop tab held down (scale 0.92, darker icon and lock). |

## How they were made (2026-09-27)

`Assets/ColorBlockJam/Tests/Hub/PlayMode/HubCaptureTests.cs` (`CaptureHome`,
category `Capture`, `[Explicit]`) loads Hub in Play Mode, waits until Home is bound,
swaps the `UIDocument` to a runtime copy of `HubPanelSettings` that renders into a
RenderTexture of the target size, pins the safe area with `HubScreen.SetSafeAreaSource`,
sends a mouse press to the named button for the pressed captures (the UI input module is
paused so the real editor mouse cannot clear `:active`), and writes the PNGs here. It is
ignored unless the Editor process has `CBJ_HUB_CAPTURE=1`, so assembly-wide runs never
rewrite these files. Run in the live 2022.3.62f2 Editor through MCP `run_tests`
(PlayMode, test name `ColorBlockJam.Tests.HubScene.HubCaptureTests.CaptureHome`) after
setting the variable in the Editor process, and cleared again afterwards.

The level data was the active level database (at least 3 levels), saved level index 0, coins 0,
lives 5 (maximum): Editor play uses the in-memory player store, so the captures show the
seeded values, not 1.52k from the reference.

## Recapture after the chief validator fixes (2026-09-27)

All four PNGs were rewritten by `CaptureHome` (PlayMode job `50431af8`) after the fixes for
defects D-001 to D-003 of the Hub Home task:

- D-001, top bar: holders are 300 x 90 with one per-holder rule (margin-left 48), icons 108
  overhanging the pill by 34, plus badges 66 x 68 overhanging by 6. At 1080 wide the heart
  ends at x576 and the coin holder's plus badge starts at x584; the coin ends at x924 and the
  gear starts at x932. Nothing overlaps between controls.
- D-002, vertical bands: route flex-basis 1075 / grow 2.1, a new `home-gap` flex-basis 229 /
  grow 1, no fixed margins on the Level button. Measured Level button top: y1302 at
  1080 x 1920 (tab bar top y1714, gap 227); y1615 at 1080 x 2400 with the 110/70 notch
  (the arithmetic gives y1625 without a notch).
- D-003, tab bar: inactive icons 150 x 165, locked tint #6E7696 with lock 84 x 101; the
  Home icon is 205 x 224 and protrudes about 28 px above the visible edge of the ActiveTab,
  raised 60 px. The active tab button needed `overflow: visible`: before it, the button
  clipped the icon flat at its own top edge.
- Also from the review: locked route nodes 220 x 228, Level text #FFF6D6 with a #18511C outline.

## Comparison with reference 06

Matches the structure: profile box top left, life holder "Full" with 5 on the heart and
a plus badge, coin holder with the balance and a plus badge, settings gear top right, the
route with the current level (green) and the next existing levels locked (blue with a
lock), the large green "Level 1" button, and the tab bar Leaderboard, Shop (lock),
raised Home, Collection (lock), Support. In the 1080 x 2400 notch capture the top bar
starts below the 110 px inset, the tab slots end above the 70 px inset while the bar
fill reaches the bottom edge, the background covers the whole screen (scale-and-crop),
and nothing is clipped or overlaps.

Remaining differences, all known and accepted by the contract or the art set:

- The profile box is empty: avatar art is out of scope.
- The current route node uses `Btn_level_green` as a fixed square, so it reads as a round
  green badge rather than the reference's studded green square (no such sprite is supplied).
- Text uses LiberationSans (DEC-39); the reference uses a rounded heavy display font, so
  labels look lighter.
- The reference's event widget, "Lvl 50" gate, 29 to 31 numbering, and a locked first tab
  are not adopted (contract out of scope; DEC-133 fixes the tab order).
- Device 20:9 layout (V-D3) and the idle-Home Profiler check were not run.

## Cycle 3: D-004, D-005, D-006 (2026-09-27)

All four PNGs were rewritten by `CaptureHome` after fixing the three minor differences from
the chief validator's round-2 review (`C:/Users/e-fur/.agent-context/runs/astra-hub-home-visual-20260927T030859/final.md`):

- D-004, route rails fading toward the top: `.route-rail` now has an opaque
  `background-color: #F4C542` backing under `bg_home_line.png` (its alpha gradient can no
  longer erase the rail) plus a 1 px `#936526` border on each side. `.route-rails` extends
  from `top: -335px` (was `0`) so the rails run continuously behind the top bar to the
  screen top on both the no-notch and notch captures (the top bar's own 225 px plus the
  larger 110 px notch inset). Verified: the rails are solid gold end to end in both
  `home-1080x1920.png` and `home-1080x2400-notch.png`, with no fade.
- D-005, undersized plus glyphs: `.plus-button__icon` grew from 38 x 38 to 72 x 72
  (`flex-shrink: 0`, tint `#FFFFFF`) inside the unchanged 66 x 68 badge. Verified: a clearly
  visible white cross in both badges in `home-1080x1920.png`.
- D-006, small profile frame: `.unity-button.profile-button` keeps its 158 x 158 slot and
  press scaling but now sets `overflow: visible` and centers one new child,
  `.profile-button__frame` (220 x 220, `flex-shrink: 0`, `profile_box_blue.png`,
  scale-to-fit), added in `Home.uxml`. Two Unity-specific traps found and fixed along the
  way: a `Button`'s default theme clips overflow, so the 220 px child was invisible past the
  158 px box until `overflow: visible` was added; and a flex child's default
  `flex-shrink: 1` shrank the 220 px child back down toward the 158 px slot on both axes
  until `flex-shrink: 0` was added (matching `.plus-button__icon`'s existing rule). Verified
  by measuring the rendered frame's opaque pixel span in `home-1080x1920.png`: ~152 x 148 px
  visible, versus ~112 px before the fix and the ~150 px target.
- The child's tint is left undeclared (engine default is already white) rather than set
  explicitly, because an explicit `-unity-background-image-tint-color: rgb(255,255,255)` on
  `.profile-button__frame` out-cascaded `.cbj-pressable:active > .cbj-press-tint`'s darken
  rule by matching the same property at a lower but still-applied specificity, which failed
  `HubSceneTests.EveryHomeButton_ScalesAndDarkensWhilePressed_AndNavigatesNowhere` for
  `profile-button` (tint stayed 1.0 instead of darkening). Removing the redundant base
  declaration (the tab icons already follow this pattern) fixed it; the pressed-state rule
  is unaffected and still darkens the frame on press.

Checks: EditMode `ColorBlockJam.Hub.EditModeTests` 50/50 passed (job `a55ebd71`, rerun clean
after each fix); PlayMode `ColorBlockJam.Hub.PlayModeTests` 6/6 passed with `CaptureHome`
explicit-skipped as designed (final job `b29c87b6`); `CaptureHome` itself passed with
`CBJ_HUB_CAPTURE=1` (final job `b992761`), and the variable was cleared and verified `null`
immediately after. No USS/UXML import warnings in the Console after any refresh. Diffing the
pressed captures against the unpressed one confirms real, localized changes: level-pressed
bbox (295,1300)-(785,1485), shop-pressed bbox (253,1722)-(379,1906).

Residual, unrelated to D-004/D-005/D-006: three stale `-unity-slice-scale` console warnings
seen mid-cycle (holder-pill, level-button, tab--active) referenced pre-edit line numbers and
did not reappear after a clean refresh; nothing in this cycle touches `-unity-slice-scale`.

## Cycle 4: D-007 (2026-09-27)

`route-rails`' `top: -335px` (cycle 3, D-004) made the rails run behind `top-bar`, but
`level-route` still followed `top-bar` in `Home.uxml`, and UI Toolkit paints later siblings
over earlier ones with no z-index, so the rails and blue centre drew over the life holder's
heart and its "5". Fix, exactly the coordinator's required approach:

- `Home.uxml`: `top-bar` moved to the end of `safe-area`'s children, after `level-route`,
  `level-button` and `home-gap`, so it paints last.
- `Home.uss`: `.top-bar` is now `position: absolute; left: 0; right: 0; top: 0` with its
  existing `height: 170px` and `margin-top: 40px` folded into that position (so it still
  occupies y40-y210, unchanged); the `margin-bottom: 15px` it used to reserve for the next
  flow sibling is dropped. `.level-route` gains `margin-top: 225px` (40 + 170 + 15, the exact
  band the top bar used to occupy in flow), which keeps every band below it — route, Level
  button, gap, tab bar — at its prior y position, since a margin on a flex item counts toward
  flex-basis consumption the same way the removed in-flow top-bar did.
- No C# change: `HubScreen.Bind` and `HomeView` resolve every element with `Q<T>(name)` or by
  class, never by child index, so making `top-bar` the last `safe-area` child does not affect
  them. `HubSceneTests.s_namedElements` is a presence list, not an order assertion.

Checks: EditMode `ColorBlockJam.Hub.EditModeTests` 50/50 passed (job `82558cef`). PlayMode
`ColorBlockJam.Hub.PlayModeTests` 6/6 passed with `CaptureHome` explicit-skipped as designed
(job `35203129`); `CaptureHome` itself passed with `CBJ_HUB_CAPTURE=1` set only for that run
(job `d518e238`), and the variable was cleared and verified `null` immediately after. No
USS/UXML import warnings or compile errors in the Console after the refresh.

All four PNGs were rewritten by that `CaptureHome` run. Visual confirmation:

- `home-1080x1920.png`: the heart and "5" are fully visible in front of the rails, the coin
  and gear are visible, both green plus badges are visible; the gold rails and blue centre
  are still continuous from behind the top bar down through the route to the Level button;
  the route nodes, Level button and tab bar are all at their cycle-3 positions.
- `home-1080x2400-notch.png`: same result under the 110/70 px notch safe area — heart, "5",
  coin, gear and both plus badges fully visible above the rails, rails continuous to the
  screen top behind the (now inset) top bar, nothing clipped or shifted.
- `home-1080x1920-level-pressed.png` and `home-1080x1920-shop-pressed.png`: same top-bar
  visibility and rail continuity as the base capture; the pressed control still shows its
  scale/tint feedback and nothing else moved.

## Cycle 5: D-008 (2026-09-27)

Cycle 4's `position: absolute; top: 0` on `.top-bar` positions from its parent's border box,
not its padding box: `SafeAreaPadding` writes the safe-area inset as `padding-top` on
`#safe-area` (an inline style), but an absolute child of `#safe-area` ignored that padding and
stayed at the un-inset y, so on the 2400-notch capture the whole top bar sat back at its
1080 x 1920 position, inside the 110 px notch. Fix, exactly the coordinator's required
approach:

- `Home.uxml`: added one padding-free wrapper, `safe-content` (picking-mode `Ignore`), as the
  sole child of `#safe-area`, holding `level-route`, `level-button`, `home-gap` and `top-bar`
  in their existing order (`top-bar` last, so D-007's paint order is unchanged). No element was
  reordered, only re-parented one level down.
- `Home.uss`: added `.safe-content { flex-grow: 1; flex-direction: column; }` (no padding),
  duplicating `.safe-area`'s own layout rules, which stay on `.safe-area` unchanged because it
  still has to grow to fill the space `home-screen`'s column layout leaves it beside `tab-bar`.
  `.top-bar` keeps `position: absolute; top: 0; left: 0; right: 0` and `.level-route` keeps its
  `margin-top: 225px` (both untouched from cycle 4). Because `safe-content` is now the normal-flow
  child that receives `#safe-area`'s padding-top, it starts below the inset like any flow child
  always did, and `top-bar`'s absolute offsets resolve against `safe-content`'s own (unpadded)
  border box, which already sits at the padded position — so the inset reaches it again.
- No other C# or test change: `HubScreen.Bind` and `HomeView` resolve every element with
  `Q<T>(name)`, which searches the whole subtree regardless of nesting depth, so inserting
  `safe-content` between `#safe-area` and its former children is invisible to them.
  `HomeView`'s only child-index walk (`route.childCount` in the `level-route` constructor loop)
  is over `level-route`'s own children, untouched by this change. `HubSceneTests.s_namedElements`
  is a curated presence list, not an exhaustive element inventory (it already omits `home-gap`,
  route nodes and currency-holder instances), so `safe-content` was not added to it.

Checks: EditMode `ColorBlockJam.Hub.EditModeTests` 50/50 passed (job `7d9f13d2`). PlayMode
`ColorBlockJam.Hub.PlayModeTests` 6/6 passed with `CaptureHome` explicit-skipped as designed
(job `c1edabe3`); `CaptureHome` itself passed with `CBJ_HUB_CAPTURE=1` set only for that run
(job `4df8386e`), and the variable was cleared and verified `null` immediately after. No
USS/UXML import warnings or compile errors in the Console after the refresh.

All four PNGs were rewritten by that `CaptureHome` run. Pixel measurements (columns sampled
directly from the PNGs, not eyeballed):

1. At 2400-notch every top-bar control is below y110: the profile frame's outer edge starts at
   y158 and its solid body at y172-173 (column x110), the settings gear at y178 (column x990),
   and the life heart at y186 (column x500, first red pixel) — all comfortably below the 110 px
   inset line, and the heart's y186 is exactly the 1920 capture's y76 plus the 110 px inset
   (110 + 76 = 186), confirming the shift is exactly the safe-area inset, not an arbitrary drop.
2. At 1920 the top bar is back at its cycle-3 position: profile solid body top ~y48 (matches the
   original D-008 report's "profile top y48"), heart first red pixel y76.
3. The heart and its "5" draw above the rails at both sizes: sampling the rail's own centre
   column (x540, inside the `route-rails` 494-586 px span) at the heart's rows returns solid red
   ((248, 43, 34) at 1920 y90, and the same colour at notch y200, exactly 110 px lower) instead of
   the rail's gold or the route-fill's blue, so the top bar still paints over the rails there.
4. Level button top: column x540 (the button's horizontal centre) shows the sprite's top
   highlight edge beginning at y1300 at 1920 and y1614 at 2400-notch — both match the cycle-3/4
   target positions, so the flex bands below `safe-content` did not shift.
5. Nothing else moved: `tab-bar` is a sibling of `#safe-area`, outside the touched subtree, so it
   keeps its own safe-area padding and position untouched; `HubSceneTests` (50 EditMode + 6
   PlayMode, including the press scale/tint test over every Home button) passed unchanged, and
   the two pressed captures still show only their own control's scale/tint feedback with the same
   top-bar and rail layout as the base capture.

## Cycle 6: Route nodes and heavy text (D-009, D-010, D-011) (2026-09-27)

User feedback with reference 06 raised two defects, and the resulting fix regressed a third
during the same cycle:

- D-009, the current-level node rendered as a circle: `.route-node` drew `Btn_level_green.png`
  at 220 x 220 with no declared slice, so UI Toolkit applied the sprite's own importer border
  (L97 T164 R97 B162, set for `.level-button`'s sliced use of the same texture) and 326 px of
  top + bottom border could not fit 220 px, rounding the corners into a circle. Fix: declare the
  *same* border the sprite already carries (matching, not overriding it, so no "Sprite borders are
  overridden" warning) plus `-unity-slice-scale: 0.5px` to downscale that declared border
  uniformly at render time, landing at 163 px of vertical border for 220 px of height and
  restoring the square-with-rounded-corners art (`.level-button`'s own proven technique for the
  same sprite).
- D-010, thin route numerals and Level label: both now use the two-layer `.cbj-heavy-text` pair
  from the Settings popup's D-001 fix (`Common.uss`, moved there from `Home.uss` so both screens
  share it) — a `__back` layer (dark fill matching each node's face, wide outline) under a
  `__front` layer (light fill, thinner outline), thickening the rendered stroke without touching
  the shared LiberationSans FontAsset.
- D-011 (regression from the D-009 fix): `.route-node--locked` stacks on the same element as
  `.route-node` (Home.uxml never replaces one class with the other), so it inherited the new
  slice declarations too. `Btn_settings_normal.png` (the locked nodes' sprite) carries no
  importer border (0,0,0,0, imported "fixed"), so a 97/164/97/162 slice left only its four corner
  regions on screen — the rail visible through the middle. Fix: moved the slice declarations off
  the shared `.route-node` base class onto a new `.route-node--current` modifier, applied only to
  Home.uxml's slot-0 element (always the unlocked current level, per `HomePresenter`'s
  `slot > 0` locked rule); `.route-node--locked` now declares no slice at all, so
  `Btn_settings_normal` draws with its own (absent) border and the whole blue rounded square is
  back, with no "Sprite borders are overridden" warning.

Checks: EditMode `ColorBlockJam.Hub.EditModeTests` + `ColorBlockJam.UI.EditModeTests` 70/70
passed (job `836b439f`). PlayMode `ColorBlockJam.Hub.PlayModeTests` 14/16 passed with
`CaptureHome`/`CaptureSettings` explicit-skipped as designed (job `302e3281`); both capture
tests passed with `CBJ_HUB_CAPTURE=1` set only for that run (job `bd84eb1d`), and the variable
was cleared and verified `null` immediately after. No "Sprite borders are overridden" or other
new USS/UXML warnings in the Console after the refresh.

All four Home PNGs and all four Settings PNGs were rewritten by that `CaptureHome`/
`CaptureSettings` run. Visual confirmation against reference 06
(`docs/media/color-block-jam/user-references/06-home-tabs.png`):

- `home-1080x1920.png` and `home-1080x2400-notch.png`: node "1" (current) is a green rounded
  square with a gold rim, matching reference node 29; nodes "2" and "3" (locked) are whole blue
  rounded squares with their lock icons, matching reference nodes 30/31's silhouette; every
  numeral and the "Level 1" label render with visibly heavy white strokes and a dark outline,
  comparable in weight to reference 06.
- `home-1080x1920-level-pressed.png` and `home-1080x1920-shop-pressed.png`: same route-node and
  text rendering as the base capture; only the pressed control's own scale/tint feedback differs.
- Settings pixel diff against the chief-validator-approved baseline
  (`verify-settings-c4/fresh/settings-*.png`): `settings-1080x1920.png`,
  `settings-1080x2400-notch.png` and `settings-1080x1920-home-shown-preview.png` are
  byte-for-byte identical (zero-pixel diff); `settings-1080x1920-legal-pressed.png` differs by
  2423 pixels of 2,073,600 (max channel delta 87) inside the Legal Terms button's own bounding
  box — consistent with sub-pixel anti-aliasing jitter from the 0.08 s press-scale transition
  being sampled at a slightly different point across two separate test runs, not a rendering
  change: this cycle touched only `Home.uss` and `Home.uxml`, never `Settings.uss` or the shared
  popup, and the button is visually indistinguishable in a side-by-side crop.
