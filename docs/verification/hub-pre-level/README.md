---
title: Hub pre-level and purchase popup captures
created: 2026-09-27
modified: 2026-09-27
type: verification
status: active
tags: [hub, pre-level, purchase, boosters, ui-toolkit, captures]
---

# Hub pre-level and purchase popup captures

Evidence for `TASK-20260926-235915699-78356740-hub-pre-level-launch` scenarios 2, 3, 4, 5
and 10, compared with references
[09](../../media/color-block-jam/user-references/09-pre-level-unselected.png),
[10](../../media/color-block-jam/user-references/10-pre-level-selected.png) and
[01](../../media/color-block-jam/user-references/01-booster-purchase.png), and with the
user's real-game screenshots of 2026-09-27 (pre-level unselected and selected, Hub
purchase over Home). Design: [Hub-And-Player-Data-Design.md](../../Hub-And-Player-Data-Design.md)
sections 9.1 and 9.2.

## How they were made

`PreLevelCaptureTests.CapturePreLevelAndPurchase` (PlayMode, `[Explicit]`, runs only when
the Editor process has `CBJ_HUB_CAPTURE=1`) loads Hub, disables the scene's own Hub screen
and renders a clone of the Hub object into a render texture of the target size. The clone
is bound to its own presenters over an isolated `PlayerSaveService` with a seeded
in-memory snapshot (15 coins, Extra Time 2, Rocket 0), so Buy runs a real purchase through
the real save service; the same views, UXML, USS and presenters as the scene. Presses are
synthetic pointer events on the laid-out elements. The safe area is pinned with
`HubScreen.SetSafeAreaSource`; the notch set uses a top inset of 110 and a bottom inset of
70. Run on 2026-09-27 (round 2, job `fab214fb494a481b9436abf5a08e9319`, 3/3 with the Home
and Settings capture tests) in the shared Editor (Unity 2022.3.62f2); the variable was
cleared again and read back as null.

## Captures

Each state exists at 1080 x 1920 and at 1080 x 2400 with the simulated notch
(`-1080x2400-notch` in place of `-1080x1920`).

| File (1080 x 1920) | Sequence step | State |
| --- | --- | --- |
| `pre-level-1080x1920-empty-stock.png` | Level pressed | `LEVEL 1`; Extra Time: blue square, red badge `2`; Rocket: blue square, `ic_plus` badge (stock 0) |
| `purchase-1080x1920.png` | Rocket selector pressed | Purchase popup alone over the dimmed Home: `Rocket`, icon, `x1`, description, Buy with Coin and `10`; the pre-level popup is hidden (covered) |
| `purchase-1080x1920-purchased.png` | Historical capture, before success auto-close | 15 -> 5 coins, Rocket stock 0 -> 1; the current popup closes immediately after success |
| `purchase-1080x1920-not-enough-coins.png` | Reopen purchase popup; Buy pressed | "Not enough coins", nothing changes |
| `pre-level-1080x1920.png` | Purchase succeeds and popup closes | Pre-level popup shown again; Rocket now blue with red badge `1`, not selected |
| `pre-level-1080x1920-selected.png` | Both selectors pressed | Both squares green with a check badge |

The test also asserts that the pre-level screen is `display: none` while the purchase
popup is open and `display: flex` after it closes.

## Comparison with the references

- Selectors (user feedback 2026-09-27, "butonlar kare olmalı"): rounded squares as in
  references 09/10. Unselected uses the supplied `Btn_settings_normal` at its aspect ratio
  (204 x 212) with the booster icon and a red count badge (USS disc, white rim, heavy white
  numeral) overhanging the lower-right corner, as in reference 09. Selected tints the same
  square green (`rgb(140, 255, 26)` multiply) with a green check badge, as in reference 10.
  `Btn_level_green` was compared side by side against the reference crop and rejected: its
  gold rim does not appear in the reference. The tinted square is a purer green than the
  reference's yellow-green because the art's red channel is near 0, so a multiply tint
  cannot add yellow.
- Zero stock keeps the blue square with the `ic_plus` badge on `Btn_circle_green_small`,
  the same badge as Home's plus buttons.
- Labels `Extra Time` and `Rocket` stay under the squares; they do not clash with the
  badges (DEC-47 keeps the Extra Time text because the supplied art has no hourglass).
- Purchase popup (user feedback 2026-09-27, "ui'ın üst üste binmesi"): it now shows alone
  over the dimmed Home, as the real-game Hub purchase screenshot; no pre-level edge or
  second close button is visible behind it. The rocket art is unchanged; no sparkles and
  no red price were added.
- Deliberate differences: the level-70 reward panel of reference 09 is omitted (out of
  scope); the supplied purple Rocket and the Clock replace the reference's red rocket and
  hourglass (DEC-87); the price is the case offer (10 coins for 1 charge, DEC-85).
- Nothing is clipped at 1080 x 1920 or at 1080 x 2400 with the notch: the popups centre in
  the safe area and the Home top bar and tab bar stay inside it.

## Regression check

The Home and Settings captures (`../hub-home`, `../hub-settings`) were re-rendered in the
same run and compared pixel by pixel with the stored files: all eight are identical
(no pixel differs).
