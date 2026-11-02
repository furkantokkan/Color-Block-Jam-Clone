---
title: Hub Settings popup captures
created: 2026-09-27
modified: 2026-09-27
type: verification
status: active
tags: [hub, settings, ui-toolkit, captures]
---

# Hub Settings popup captures

Evidence for `TASK-20260926-235915698-c5020d50-hub-settings-overlay` scenarios 2 and 10,
compared with [reference 19](../../media/color-block-jam/user-references/19-settings-home.webp)
(design: [Hub-And-Player-Data-Design.md](../../Hub-And-Player-Data-Design.md) section 8).

## How they were made

`HubCaptureTests.CaptureSettings` (PlayMode, `[Explicit]`, runs only when the Editor
process has `CBJ_HUB_CAPTURE=1`) opens Hub, sets Vibration OFF and Sound and Music ON
through the save service (Editor play uses the in-memory store), presses the settings
button and renders the Hub panel into a render texture of the target size. The safe area
is pinned with `HubScreen.SetSafeAreaSource`; the notch capture uses a top inset of 110
and a bottom inset of 70. The test restores the three settings afterwards. Run on
2026-09-27 in the shared Editor (Unity 2022.3.62f2); the variable was cleared again and
read back as unset.

## Captures

| File | Size | State |
| --- | --- | --- |
| `settings-1080x1920.png` | 1080 x 1920 | Vibration OFF, Sound and Music ON, as reference 19 |
| `settings-1080x2400-notch.png` | 1080 x 2400, safe area top 110 / bottom 70 | same state |
| `settings-1080x1920-legal-pressed.png` | 1080 x 1920 | Legal Terms held: scale 0.92 and darkened art (shared press feedback) |
| `settings-1080x1920-home-shown-preview.png` | 1080 x 1920 | component preview only: the shared popup with HOME shown, as the Game pause menu will host it (reference 20). The Hub keeps HOME hidden. |

## Comparison with reference 19

- Structure matches: dimmed Home behind, `bg_popup` shell, overlapping `bg_popup_header`
  "SETTINGS", red close button at the shell's top-right corner, dark `bg_popup_info_holder`
  inset with the Vibration, Sound and Music rows (icon left, toggle right), then Legal
  Terms, Restore Purchases (two lines), Support (with its icon) and Language.
- Proportions: at row y1000 the shell spans x128..952 (reference 19: x128..952) and the
  inset x198..882 (reference: about x195..886). Rows are 150 apart and buttons 221 apart,
  as measured in the reference.
- Toggles: OFF shows a plain light grey knob at the left of a dark navy track; ON shows
  the `btn_toggle_on` knob at the right of a dark green track. In greyscale, the OFF
  toggle's left third averages 180 against 70 for ON and its right third 74 against 116,
  so the states differ without colour.
- Nothing is clipped or distorted: nine-slice borders stay at their source size, icons keep
  their aspect, and every control sits inside the safe area. At 1080 x 2400 the popup is
  centred in the safe area, well below the 110 px notch and above the 70 px bottom inset.
  The scrim dims all of Home, including the tab bar (for example a Home background pixel
  goes from (104, 115, 254) to (75, 84, 190)).
- Measured extents (header top to shell bottom): 1080 x 1920 y133..~1740; 1080 x 2400
  notch y393..~2000 (safe area y110..2330).
- Follow-up for the Game pause host: with HOME shown the popup is 1886 high, so at
  1080 x 1920 the header top sits at y22 and a notched 16:9 safe area would not fit it.
  That host should scale or shorten the popup; the Hub never shows HOME.
- Known difference: the supplied LiberationSans font (DEC-39) is plainer than the
  reference's rounded display font; label sizes follow the reference text widths.

## Home unchanged

`CaptureHome` was re-run in the same session. `home-1080x1920.png` and
`home-1080x2400-notch.png` came out pixel-identical to the stored task 2 captures. The two
pressed Home captures differed only inside the pressed button (transition timing), so the
stored task 2 files were put back unchanged.

## Chief review fixes (D-001..D-005, attempt 3)

The chief validator (Astra) approved with changes: majors D-001 (text weight) and D-002
(HOME variant fit), minors D-003 (title plaque width), D-004 (inset fill colour) and D-005
(scrim). All five are `Assets/ColorBlockJam/UI/Settings/SettingsPopup.uss` changes; D-002
also needed one class toggle in `SettingsPopupView.SetHomeVisible` (compile-checked against
the `ColorBlockJam.UI` Bee rsp before it entered `Assets`, then applied — 0 errors).

- **D-001 — bold text.** `.settings-title` and `.settings-button__label` now set
  `-unity-font-style: bold`, `color: rgb(255, 255, 255)` and an explicit
  `-unity-text-outline-color: rgb(23, 35, 91)` (`#17235B`) instead of relying on the
  `cbj-title` token (which uses `#182A6E`); outline width dropped from 5px to 4px on the
  title and 4px to 3px on labels, per the report. Sizes are unchanged (78/54/50/60px); the
  two-line "Restore Purchases" still fits on two lines in every capture.
- **D-002 — HOME variant fit.** Added a `.settings-screen--with-home` modifier, toggled on
  the screen root by `SetHomeVisible(bool)` alongside the HOME button's display. While
  active it tightens `.settings-row` height (150 to 138px, -12 x 3 rows) and
  `.settings-holder > .unity-button.settings-button` margin-bottom (22 to 16px, -6 x 5
  buttons), a 66px cut. Measured on the fresh `settings-1080x1920-home-shown-preview.png`
  (1080 x 1920, no notch): popup top (header's overhanging upper rim) at y31, popup bottom
  at about y1892 — clearance 31px top, 28px bottom, both comfortably over the requested
  ~24px and no clipping at y0 (previously cropped). The hidden-HOME layout is untouched:
  EveryHomeButton PlayMode test and the plain `settings-1080x1920.png` /
  `settings-1080x2400-notch.png` captures are unaffected by this modifier (it only applies
  while HOME is shown).
- **D-003 — title plaque width.** `.settings-header` width 532 to 588px, left 197 to 169px
  (centred: (926-588)/2 = 169). Measured visible plaque width on the fresh capture (contiguous
  non-background span at y135..165, where the trapezoid is flat) is about 546-549px, up from
  the defect's reported ~520px; side-by-side against reference 19 (whose plaque spans about
  460 of 904px, i.e. ~550 at 1080 width) this is a close match. The chief's own figure (575)
  was stated as approximate; this implementer's pixel measurement is reported here instead of
  restating that estimate.
- **D-004 — inset fill colour.** Added `-unity-background-image-tint-color: rgb(77, 255, 255)`
  (`#4DFFFF`, the report's suggested starting tint) to `.settings-holder`. Sampled centre of
  the fresh capture (x850, y500/900/1300, all three rows): `rgb(7, 103, 198)` (`#0767C6`),
  close to the target `#1065C5` (`rgb(16, 101, 197)`).
- **D-005 — scrim.** `.settings-scrim` background-color alpha 0.5 to 0.702 (`rgba(11, 21, 61,
  0.702)` = `#0B153DB3`, exactly the report's target), keeping the same RGB.

### Verification

- EditMode: `ColorBlockJam.UI.EditModeTests` + `ColorBlockJam.Hub.EditModeTests`, 89/89
  passed (job `015049a6d1ee4041906779bd2ed96380`).
- PlayMode: `ColorBlockJam.Hub.PlayModeTests`, 16/16 passed. The full-suite run (job
  `9fdff63122844d4abbd4fc93e5874722`) flagged one failure in
  `HubSceneTests.EveryHomeButton_ScalesAndDarkensWhilePressed_AndOnlySettingsNavigates` whose
  message was an unhandled `MCP-FOR-UNITY` WebSocket log line from the shared Editor's own
  tooling connection, not a test assertion; re-run alone (job
  `17913f5661ea48eabb856cbf45e37132`) passed cleanly, confirming it was tooling noise, not a
  regression from this cycle's USS/view changes (which touch none of that test's Home-button
  code paths).
- Console: no compile error or warning under `Assets/ColorBlockJam/UI/**` after
  `refresh_unity`; the one project warning present (`BlockIceOverlay.cs` obsolete
  `Tween.LocalScale`) predates this task and is outside its allowed paths.
- Recaptured with `CBJ_HUB_CAPTURE=1` (set and cleared via `execute_code` on the Editor
  process, verified null afterwards): `settings-1080x1920.png`,
  `settings-1080x2400-notch.png`, `settings-1080x1920-legal-pressed.png` and
  `settings-1080x1920-home-shown-preview.png` all regenerated; visually compared against
  reference 19 (bold white text with the new outline colour throughout, wider title plaque,
  more saturated cyan-blue inset, darker scrim, nothing clipped in the notch capture, Legal
  Terms' pressed state still shrinks/darkens in place without moving other controls).
- Home unaffected: `CaptureHome` was not re-run this cycle because another live session held
  Play mode on `Game.unity` for the whole verification window (`unity status` showed
  `isPlaying: true` throughout; per the shared-Editor rule this implementer never interrupts
  another session's Play mode or messages it). This cycle's changes are confined to
  `.settings-*` selectors, the new `.settings-screen--with-home` modifier and one line in
  `SettingsPopupView.SetHomeVisible` — none referenced by any Hub Home UXML/USS/C# — and
  `ColorBlockJam.Hub.EditModeTests` (89/89 combined) plus the Hub PlayMode suite (16/16,
  `EveryHomeButton_ScalesAndDarkensWhilePressed_AndOnlySettingsNavigates` included) both
  passed against the changed sources, so Home's own behaviour is exercised and unaffected;
  only the pixel-capture re-confirmation from the previous cycle's note was skipped.

## Heavy text (attempt 4)

D-001 recurred: the chief validator's round-2 review (`C:/Users/e-fur/.agent-context/runs/astra-hub-settings2-20260927T131742/final.md`)
judged the rendered SETTINGS title and the four/five placeholder labels still thin against
reference 19/20, because `-unity-font-style: bold` on the supplied LiberationSans (DEC-39,
regular weight only) is a synthetic bold that barely widens the rendered strokes. Style
declarations are not evidence of rendered weight; only the pixel result is.

**Fix.** `SettingsPopup.uxml`/`SettingsPopup.uss` only. Each of the six texts (`SETTINGS`,
`Legal Terms`, `Restore Purchases`, `Support`, `Language`, `HOME`) is now a `.cbj-heavy-text`
container around two same-text `Label`s: a `__back` layer (fill and outline both `#17235B`)
drawn first, absolutely positioned to fill the container, and a `__front` layer (fill and
outline both `#FFFFFF`) drawn on top in normal flow, sizing the container. The extra white
outline on the front layer fills in the antialiased edge of the regular-weight glyph, so the
rendered white stroke reads thicker without touching the shared FontAsset, `Common.uss`, or
any Home UXML/USS. The front layer keeps the original element name (`settings-title`, and the
label inside `home-button`) so the existing name-based test queries still resolve it
unchanged; no test file needed editing. Outline widths, tuned in two live-Editor iterations
against reference 19 (measured below), settled at:

| Layer | `.settings-title` (78px) | `.settings-button__label` (54/50/60px) |
| --- | --- | --- |
| `__back` (navy) | 8px | 5px |
| `__front` (white) | 2px | 1px |

**Measured white stroke width** (median run-length of near-white pixels across sampled rows,
1080-wide coordinates; reference 19 is 904 wide, scaled by 1080/904 = 1.1947):

| Text | Before this fix | After | Reference 19 (scaled) |
| --- | --- | --- | --- |
| SETTINGS title | 7.0px | 13.0px | 11.7px |
| Legal Terms label | 4.0px | 8.0px | 5.9px |

The title lands within 11% of the reference. The label is heavier than the reference by
about 36%, a deliberate residual: a first attempt at exact parity (front outline 2px, back
5px) measured 9.0px, clearly overshooting, and the next whole-pixel step down (front outline
1px, kept) measured 8.0px, still over but closer than either 9.0px or the un-outlined 4.0px
baseline at 0px. Fractional outline widths were not tried this cycle; a future pass could
retune with one to land closer to 5.9px if the residual still matters. 8px was kept over 9px
because it reads unambiguously "heavy" without approaching the fill of adjacent strokes.
Side-by-side crops of
`SETTINGS`/`Legal Terms` against reference 19 (scaled to 1080 width) confirm the letterforms
stay separated with no merging or clipping at either width, and Legal Terms' pressed capture
shows the two-layer text still scales and darkens together with the button (single `scale`/
tint transition on the shared parent `Button`, unaffected by the container split).

**Verification.**

- EditMode: `ColorBlockJam.UI.EditModeTests` + `ColorBlockJam.Hub.EditModeTests`, 89/89 passed
  (jobs `a32075a8af174362ba3425c1a659d9eb` before the outline-width tuning pass and
  `6a368fa9eb0e46d58fb7edaef7c7ed6d` after it).
- PlayMode: `ColorBlockJam.Hub.PlayModeTests`, 16/16 (14 passed, `CaptureHome`/`CaptureSettings`
  skipped by design without the capture variable) both before (job
  `9240d1f9056940f7973b836a404e645c`) and after (job `96bb9cb46fd442d199c2d2cabf358bc2`) the
  tuning pass; 0 failures either run.
- Console: `refresh_unity` after every edit showed no error or warning mentioning `.uxml`,
  `.uss` or `Settings` beyond the pre-existing unrelated `BlockIceOverlay.cs` warning and
  another live session's own fault-injection log lines (`SettingsPopupPresenter.cs:256`,
  from that session's save-failure tests, not this task's paths).
- Recaptured with `CBJ_HUB_CAPTURE=1` (set and cleared via `execute_code`, verified null
  afterwards) through `HubCaptureTests.CaptureSettings`: `settings-1080x1920.png` (job
  `651a7b0fa2be4f82a50b52e1813f6842`), `settings-1080x2400-notch.png`,
  `settings-1080x1920-legal-pressed.png` and `settings-1080x1920-home-shown-preview.png` all
  regenerated in the same run. Visually: heavy white-on-navy lettering throughout at both
  sizes, nothing clipped in the notch capture, Legal Terms' pressed state still darkens and
  shrinks in place, and the HOME preview's green button shows the same heavy-text treatment
  with no overflow past the button's rim.
- Home unaffected: this cycle touches only `SettingsPopup.uxml`/`SettingsPopup.uss`, neither
  referenced by any Home UXML/USS/C#; `CaptureHome` was not re-run (same shared-Editor
  constraint as the attempt-3 note above applied again this cycle).

**Residual difference.** The button-label white stroke stays about 36% wider than reference
19's at the closest whole-pixel outline step tried this cycle (see table above); a future pass
could retune with a fractional front-outline width, which was not explored here.
