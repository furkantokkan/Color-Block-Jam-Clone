# Game HUD top bar and pause popup: reference measurements and sprite matches

The pause popup in screenshot 1 has the same geometry as the existing Hub Settings popup, so it only needs the HOME button shown and a different scrim. The HUD top bar is almost entirely covered by supplied sprites. The level badge and the Time panel come from one supplied sprite, `Btn_levelBg_normal`; neither exists as a separate sprite. The labels and toggle tracks have no sprite.

Every box is `x0..x1 × y0..y1 (w×h)`. The raw value is in the screenshot's own pixels; the normalised value is scaled by 1080/width (screenshot 4 by 1.1701, screenshot 1 by 1.1947). Converted PNGs are in `…/ae392a45-…/scratchpad/refs/{1..4}.png`. The images are 904×2000 (screenshot 1) and 923×2000 (screenshots 2, 3, 4).

**How the numbers map to the game:**
- **Top offset:** the screenshots show no status bar, so the y values include whatever top inset the phone had.
- **Scaling mode is the main risk.** The normalised values only hold under an Expand match, which the Hub uses: `Data/UI/HubPanelSettings.asset:24-25` has `m_ScreenMatchMode: 2`.
  - The Game overlay panel uses `m_ScreenMatchMode: 0, m_Match: 0.5` (`Data/UI/BoardOverlayPanelSettings.asset:18-25`), and the booster copy has the same values.
  - With that setting a 1080×2340 screen scales by about 1.104, so the canvas is about 978 wide. Sizes taken from these tables would then draw about 10% wider than the reference.
- **Screenshot 3 matches screenshot 4.** All HUD glyph boxes in 3 are within ±1 px of 4, and the top bar is identical.
- **Screenshot 2 has no top bar.** The Vacuum targeting banner takes its place at the top.

## 1. HUD top bar (screenshot 4, 923 px wide)

| Element | Raw | Normalised (1080) |
|---|---|---|
| Level badge and Time panel art together | 58..695 × 96..327 (638×232) | 68..813 × 112..383 (747×271) |
| Level badge circle (outer; about 6 px 3D rim at the bottom) | 58..283 × 96..327 (226×232) | 68..331 × 112..383 (264×271) |
| Badge inner blue disc | 72..266 × 109..300 | 84..311 × 128..351 (228×225) |
| "Level" glyphs (L cap 28, x-height 19, "l" 30) | 120..211 × 147..176 | 140..247 × 172..206 (108×35) |
| "29" glyphs (digit 55–56) | 125..204 × 194..249 | 146..239 × 227..291 (94×66) |
| Time panel, visible part to the right of the badge (about 20 px 3D rim at the bottom) | 283..695 × 108..315 | 331..813 × 126..369 (483×243) |
| Time inner dark field (with its cyan bottom rim) | 348..598 × 185..265 (251×81) | 407..700 × 216..310 (294×95) |
| Clock icon (with outline) | 384..428 × 128..173 | 449..501 × 150..202 (53×54) |
| "Time" glyphs (T cap 31, x-height 21) | 438..533 × 133..164 | 513..624 × 156..192 (112×37) |
| Timer "02:55" glyphs (digit 41–42) | 401..543 × 203..244 | 469..635 × 238..286 (167×49) |
| Retry button | 748..846 × 107..209 (99×103) | 875..990 × 125..245 (116×121) |
| Retry arrow glyph | 768..827 × 123..191 | 899..968 × 144..223 (70×81) |
| Pause button | 748..845 × 219..321 (98×103) | 875..989 × 256..376 (115×121) |
| Pause bars glyph (with outline) | 774..819 × 246..291 | 906..958 × 288..340 (54×54) |
| Coin holder pill (right end hidden under the plus) | 667..≈848 × 339..398 | 780..≈992 × 397..466 (h 70) |
| Coin icon | 672..721 × 346..393 | 786..844 × 405..460 (59×56) |
| "1.52k" glyphs (digit 34, k 38) | 724..838 × 346..383 | 847..981 × 405..448 (135×44) |
| Green plus circle | 846..903 × 340..399 | 990..1057 × 398..467 (68×70) |
| Plus cross (white part) | 861..887 × 356..381 | 1007..1038 × 417..446 (32×30) |

**Relations between elements (normalised):**
- Retry and Pause are stacked 12 px apart.
- The stack's centre (about y 250) lines up with the centre of the Level/Time art (about y 247).
- The coin holder starts 21 px below Pause.
- Right margins are 90 for the buttons and 23 for the plus. The left margin to the badge is 68.
- The coin row reads icon on the left, then the amount, then a full-height green plus on the right. The Home holder is laid out differently: icon on the right and plus as a small badge at the lower left.

**Font size estimates:** these use `LiberationSans`, the project font (`UI/Themes/ColorBlockJamRuntime.tss:10`). I measured cap height at 0.688 em and digit height at about 0.70 em.

| Label | Normalised cap height | Font size |
|---|---|---|
| "Level" | 32.8 | ≈48 px |
| "29" | 64.9 | ≈92 px |
| "Time" | 36.3 | ≈52 px |
| Timer | 48.6 | ≈68–69 px |
| Coin amount | 39.8 | ≈56 px (the same as Home `.holder-text`) |

**Colours:**
- **Page background:** #CCE3FE–#D2E3FD.
- **Badge:**
  - outline #3A4F73
  - bevel ring #7D92BB–#8FA1CB
  - inner line #5F719D
  - fill #01CAFD at the top to #009BF6 at the bottom
  - bottom rim #666E9D to #313B61
- **Time panel:**
  - outline #5F769A
  - rim #B2CDF6
  - fill #00C7FA to #0094F7
  - bottom rim #5C679E to #252F52
  - a faint lego-diamond pattern is baked into the art
- **Time field:** fill #2958C1 / #2C58C4, bottom highlight #41D4FF.
- **HUD text:**
  - fill #EBEEFC–#F6F8FE
  - outline dark navy #111973–#191D67, about 3–4 px
  - "29" has about 4 px of extra thickness to the lower right, which looks like a drop shadow
- **Coin text:** fill #F8F9FF, outline near-black navy #0A0D15.
- **Coin holder:** #74A7F3. This is the translucent `bg_home_coin_holder` over the background.
- **Plus:** fill #52BF0A to #239A0E, rim #067600, cross #FDE9DB with outline #322C5E.
- **Pause:** fill #00C7FC to #009DF7, bottom edge #1066C8–#144AA2, outline #1E4C90; bars #FDE8DF with outline #24326B.
- **Retry:**
  - In screenshot 3, with the popup dim removed, the fill is about #F5B329 and matches the sprite (#FFB529).
  - In screenshot 4 only the button background is darker, about #C0881E (roughly 75% brightness), while the arrow stays #FFE8DD.
  - I read this as an inactive state at level start, but that is an inference. No disabled sprite is supplied, so it would be a tint on the background only.

## 2. Pause popup (screenshot 1, 904 px wide)

| Element | Raw | Normalised (1080) |
|---|---|---|
| Panel (visible `bg_popup` shell) | 106..797 × 197..1746 | 127..952 × 235..2086 (827×1852) |
| Inner holder area | 163..741 × 351..1678 | 195..885 × 419..2005 (692×1587) |
| Header plaque (trapezoid, 474 wide at top / 418 at bottom) | 214..688 × 172..327 | 256..822 × 205..391 (567×186) |
| "SETTINGS" glyphs (cap 55) | 275..628 × 217..271 | 329..750 × 259..324 (423×66) → ≈96 px font |
| Close button (with ring) | 718..815 × 189..285 | 858..974 × 226..340 (117×116) |
| Row icons: vibration / sound / music | x 280..386; y 378..475 / 506..603 / 627..725 | x 335..461; y 452..567 / 605..720 / 749..866 (≈128×117) |
| Toggle 1 (OFF): track / knob | 445..620 × 400..466 / 445..531 × 388..476 | 532..741 × 478..557 (210×80) / 532..634 × 464..569 (104×106) |
| Toggle 2 (ON): track / knob | 455..630 × 523..589 / 544..628 × 512..600 | 544..753 × 625..704 / 650..750 × 612..717 (102×106) |
| Toggle 3 (ON) | Same as toggle 2 plus 125 raw; track 455..632 × 648..713 | 544..755 × 774..852 |
| Blue buttons (sprite including outer ring): Legal Terms, Restore Purchases, Support, Language | x 247..655; y 756..917, 942..1103, 1128..1289, 1314..1475 | x 295..783 (489×194); y 903 / 1125 / 1348 / 1570; pitch 222 |
| HOME button | 247..655 × 1499..1660 | 295..783 × 1791..1983 (489×194) |
| Label glyphs (cap 34–35 → ≈60 px font) | Legal Terms 319..587 × 812..859; Language 344..562 × 1371..1416; Support 414..600 × 1186..1230; Restore 367..538 × 980..1023 and Purchases 339..566 × 1024..1055 (two lines, baseline pitch 41) | Legal Terms 381..701; Language 411..671; Support 495..717 |
| Support bubbles icon | 310..396 × 1177..1232 | 370..473 × 1406..1472 |
| HOME icon | x 310..390, top 1535; the lower edge blends into the green face | x 370..466 (≈97 wide) |
| "HOME" glyphs (cap 35) | 433..581 × 1556..1591 | 517..694 × 1859..1901 → ≈61 px font |

**Colours:**
- **Scrim:** about 49% black; the background goes from #CCE3FE to #6B7581. The Hub Settings scrim is different: `rgba(11,21,61,0.702)` at `UI/Settings/SettingsPopup.uss:28`.
- **Panel:** rim #9BBAE8, inner line #1D3E87, frame #00BBFF, holder area #0C69CB.
- **Header:** fill #01CFFF to #019EFF, rim #A9CBF6.
- **"SETTINGS" text:** fill #FCF8FF to #E1DBFF, outline #221C67 / #0F187B.
- **Close button:** red #FF0236, cream X #FDF7D4, lavender ring #868FC5.
- **Toggles:**
  - OFF track #213976, OFF knob #D6E6FE to #93C2E2
  - ON track #005408, ON knob #72F60A to #00A002
- **Blue button:**
  - face #00E6FC to #00BBFF to #009BFD
  - bottom edge #0056C1 to #024C9C
  - outline #155399
  - ring #3C75EF
- **Blue button labels:** fill #F2F0FE, outline #212D7B.
- **HOME button:** face #04CE00 to #00A301, highlight band #A2F80C.
- **HOME label:** cream #F8F6D7 with a dark green outline #0A6506, not navy.

**Match with the existing Settings popup (`UI/Settings/SettingsPopup.uss`):**
- The shell sits at x128..951 (lines 45–60). I measured 127..952.
- Buttons are 494×199 and 221 apart (line 284). I measured 489×194 and a pitch of 222.
- The Legal Terms label is at x381..701 (line 315). I measured exactly that.
- The toggle tracks are rgb(33,57,118) = #213976 (line 207) and rgb(0,84,8) = #005408 (line 214). Both match exactly.

Only the scrim differs.

## 3. Supplied sprites for each element

The size columns come from PNG headers. "Opaque area" is the alpha bounding box. Borders are listed as L,T,R,B.

| Element | Sprite (GUID) | PNG size / opaque area | Notes |
|---|---|---|---|
| Level badge and Time panel | `Buttons/Btn_levelBg_normal.png` (`34af3ba7…`) | 883×325 / 814×297 (x35..848, y14..310) | One sprite: a circle joined to a patterned bar. The reference draws it at a uniform 0.916 scale at 1080 wide, which gives a full element about 808×298 at (36, 100). Measured border 323,119,75,131, not applied (`docs/UI-Asset-Import.md:119`, note at 183ff). The importer border is 0 (`.meta:52`). It is packed in the **HomeSettings** atlas (`UI-Asset-Import.md:307`), not HudPopups. There is no separate circle or panel sprite. |
| Time inner field | `Panels/bg_gameplay_time_holder.png` (`d1532cda…`) | 151×115 / 149×111 | Fill #2B58C3 and the cyan bottom rim match. Measured border 37,39,37,40, not applied (`UI-Asset-Import.md:116`). It needs a horizontal 9-slice to reach 294×95. **The booster task owns this sprite** (`UI-Asset-Import.md:263-266, 348`). In the booster copy its `.meta:52` border is 50/50/50/50, and it is used as a 320×112 timer pill in the middle of the screen (`cbj-booster…/Gameplay/UI/GameplayHud.uss:73-105`; element names `timer-holder`, `timer-icon`, `timer-label` at `GameplayHud.uxml:21-24`). That layout conflicts with the target bar. |
| Clock icon | `Icons/Gameplay/ic_time.png` (`195b567d…`) | 512×527 / 512×516 | Fixed class. Drawn about 53×54. |
| Retry background and arrow | `Buttons/Btn_square_retry.png` (`3ee174f6…`) + `Icons/Gameplay/ic_restart.png` (`ae45d62a…`) | 512×531 / 504×512; 350×350 / 198×234 | Fixed. Button drawn 116×121. For a 70×81 visible arrow, the whole `ic_restart` element is about 124 px square because of its transparent padding. The sprite outline is blue (#2063C9); the reference outline is darker. |
| Pause background and bars | `Buttons/Btn_settings_normal.png` (`90307f3a…`) + `Icons/Gameplay/ic_pause.png` (`6b78223f…`) | 134×139 / 134×135; 128×132 / fully opaque | Fixed. Button drawn 115×121; bars about 54×56. |
| Coin holder | `Panels/bg_home_coin_holder.png` (`e1e3b04b…`) | 101×84 / 101×84 | #629CF4 at alpha 214 over the background gives #73A8F6, an exact match. Measured border 24,24,25,24, not applied (`UI-Asset-Import.md:114`). HomeSettings atlas (`:327`). Not used by any screen yet. |
| Coin icon | `Icons/Currency/Coin_Single.png` (`f6855176…`) | 512×512 / 499×508 | Drawn about 59×56. |
| Plus | `Buttons/Btn_circle_green_small.png` (`5d010ec8…`) + `Icons/Currency/ic_plus.png` (`ecc7d784…`) | 139×143 / 139×138; 152×152 / 65×69 | Circle drawn 68×70. For the 32×30 visible cross the `ic_plus` element is about 75 px. |
| HOME icon | `Icons/Navigation/home_icon.png` (`efc9c1ff…`) | 153×167 | Already used at 96×105 (`UI-Asset-Import.md:145`). |
| Popup parts | `bg_popup` 976², `bg_popup_header` 612×204, `bg_popup_info_holder` 522×335, `Btn_popup_blue` 244×211, `Btn_popup_green` 425×316, `Popup_Close_Button` 150×155, `btn_toggle_on` 136×140, `Blockjam_Settings_*` 108×108, `Support_icon` 388×273 | — | All are already wired and sliced in `SettingsPopup.uss`. |

**No supplied sprite; build these in USS or as labels:**
- the "Level", number, "Time", timer and coin amount labels
- the "SETTINGS" and button labels
- the OFF knob and both toggle tracks (already done in USS under DEC-134)
- a disabled look for Retry

A standalone badge circle or Time panel is also not supplied. Use `Btn_levelBg_normal` as a whole. `Btn_booster_normal` (187²) looks similar to the badge circle but is the booster-slot art.

## 4. Existing Hub coin holder styles to reuse

**`Hub/UI/CurrencyHolder.uxml:5-15`:**
- `holder-pill` > `holder-text` with `.cbj-dark-text`
- `holder-icon` > `icon-count` with `.cbj-title`
- `plus-button` with `.cbj-pressable` > `.plus-button__icon.cbj-press-tint`

The coin instance is at `Hub/UI/Home.uxml:91-93`.

**`Hub/UI/Home.uss`:**

| Lines | Rule | Values |
|---|---|---|
| 69–81 | `.top-bar` | absolute, row, 170 high, margin-top 40, padding 36 / 32 |
| 118–124 | `.currency-holder` | 300×110, margin-left 48 |
| 136–138 | `.currency-holder--coin .holder-icon` | `Coin_Single` |
| 141–150 | `.holder-pill` | height 90, `bg_home_resource_holder` (the cream pill, not the in-game blue one), slices 40/45/40/52, `-unity-slice-scale: 0.5px` |
| 152–156 | `.holder-text` | 56 px, padding-left 16, padding-right 80 |
| 159–168 | `.holder-icon` | absolute, right −34, 108×108, scale-to-fit |
| 170–173 | `.icon-count` | 58 px, outline 3 |
| 176–186 | `.plus-button` | absolute, left −6, bottom −14, 66×68, `Btn_circle_green_small` |
| 190–197 | `.plus-button__icon` | 72×72 `ic_plus`, tint white |

**`UI/Common/Common.uss`:**

| Lines | What it defines |
|---|---|
| 7–12 | Tokens: title #FFFFFF, outline rgb(24,42,110), pressed tint rgb(205,205,205), dark text rgb(38,46,108) |
| 20–44 and 51–56 | `.cbj-pressable`: scale 0.92 and tint while pressed, 0.08 s |
| 63–65 | Disabled opacity 0.6 |
| 68–75 | `.cbj-title`: bold, outline 2 px |
| 78–83 | `.cbj-dark-text` |
| 96–113 | `.cbj-heavy-text` back/front layers, used for the heavy outlined labels |

For the in-game holder, reuse the icon, plus and text sizing rules. The pill sprite, the text colour and the left-icon/right-plus layout have to differ from Home.