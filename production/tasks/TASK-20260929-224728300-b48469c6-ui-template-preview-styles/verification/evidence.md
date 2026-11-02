# Independent verification

Verified 2026-09-29 23:23 UTC by the sequential test-verifier. Production and lifecycle were read-only. Authorized writes were limited to the three test files below, generated evidence, and saved UI Builder preview dimensions. No scene, prefab, data, art, package, assembly-reference or existing meta edits were made.

## Target and source snapshot

- Unity CLI `1.0.0-beta.11`; project Unity `2022.3.62f2`, so the approved pre-Unity-6 unityMCP transport was used for live checks.
- Exact target `ColorBlockJamCase2026@99077e4df1ecfaa4`, project `C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026`, PID `48360`, executable `C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Unity.exe`.
- ProjectIdentityUtility's SHA1 UTF8 Application.dataPath calculation and the project-local server-launch-8082.log registration independently bind the full hash and bridge session. Final bridge session `2b017dab-1d04-4a82-bd41-88e44596f50b`; project/info reconfirmed root/version before live operations.
- Contract fingerprint at handoff: `8b6799bb0bb69361dcfd4d218fd468963095fb058b7ce3726902d85495168f00`. `source-hashes.json` records the final SHA256 snapshot of owned sources and relevant shared dependencies.
- Final Editor readback: idle EditMode, no running tests or compilation, only `Assets/ColorBlockJam/Scenes/Init.unity`, scene clean. Capture environment restored to null. Home reopened in Builder, 1080 x 1920, no unsaved changes.

## Current template criteria

| Criterion | Result | Evidence |
| --- | --- | --- |
| AC1 references/imports | PASS | All 14 documents import, clone and resolve qualified stylesheet/template references. |
| AC2 standalone authoring | PASS | Default-theme attached panels at 1080 x 1920: visible authored roots/content, LiberationSans-UITK, equal paired text and aligned heavy layers, transparent pressable fill and reset borders, Home heart icon and Level 1 sample, opaque standalone fade. |
| AC3 host safety and replacement | PASS | Unbound Hub/HUD embedded overlays stay closed through detach/reattach; inline SetVisible opens/closes them. PreLevel removes samples, clones authored selectors, replaces paired labels/counts, preserves template on rebind, clears null icons on an actual attached panel, and routes/disposes selector callbacks by slot. ScreenFade has positive execution order and synchronous enable/re-enable hiding. |
| AC4 authoring guide | PASS | docs/UI-Authoring.md covers reference canvas, local/shared styles, standalone/host visibility, representative samples and runtime replacement. |

ScreenFade's focused EditMode test invokes lifecycle callbacks after UIDocument construction and asserts execution-order metadata; it does not claim a device first-frame capture. The required runtime smoke suite also passed.

## Test and compilation evidence

- Roslyn final check: `COMPILE_OK` for Hub.EditModeTests and UI.EditModeTests after the final test edits.
- Initial final focused job `5880eba4f31d4425b411e06f1d53fc8b`: 44 cases, 42 pass, two test assertion failures. One assumed an inline StyleKeyword representation; the other expected the obsolete three assembly references.
- Only failed cases plus the attached-panel null-icon test ran after repair: job `a1dac9bd4cc24384bd5fd53745463b24`, 3/3 pass. **45 distinct focused cases pass at the final revision**: 42 unaffected passing cases plus these three. HomePresenter's 22 tests are included.
- The Settings assertion repair was explicitly authorized. The production assembly already had the approved five dependencies at cs:38: ColorBlockJam.Runtime, ColorBlockJam.Player, ColorBlockJam.Haptics, Onity.Reactive, PrimeTween.Runtime. The no-scene/no-composition assertion remains; no assembly references changed. PROJECT.md DEC-196 records the stale baseline assertion.
- Additional required Hub EditMode job `e30be88f94914070843ca545e4b914b0`: 104 selected, 103 pass, one baseline failure in `BoosterPurchaseOverlayTests.Purchase_RefreshesTheSelectorCount_WithoutSelecting_AndStaysOpen` (expected open, actual closed). cs:38 and current BoosterPurchasePresenter already close on Purchased. No purchase source/test repair was made.
- Required Hub PlayMode and SceneFlow smoke job `3bf3cfbfaa994f86a4501a83759fa413`: 26 selected, **23 pass, zero fail, three capture cases ignored** with capture environment unset. Passing cases comprise 17 Hub runtime and six SceneFlow smoke cases.
- Explicit Home capture job `c2be0f8d307c4750baa72ee879d9bd81`: 1/1 pass. No Settings capture ran.
- Infrastructure-only attempts without executable test results were not counted as passes: `a27083b0b3e2420fab74aa8d75f0e99e` initialized zero tests; a prior dispatch collided with an unrelated active runner. Passing cases were not broadly rerun afterward.

Final test SHA256:

- PreLevelAssetTests.cs: `5f35cabb3f3c4132e4d09c14f3e4a074e89388782a4a5cb77ea89ac0943f0f2c`
- UiTemplateAuthoringTests.cs: `1f7f8fef9e3a750d4884ce815a62313159202c3944abdd19b0a7e48729b09462`
- SettingsPopupAssetTests.cs: `922a4e61b3fdc4208e8e80c737f6a547d0cb765b41a1bc5a40d64b5fafaa3acc`

## Saved Builder canvas readback

Every document was loaded through Builder, its own Canvas width/height inspected, and then reopened to verify persisted **1080 x 1920** with no unsaved source changes:

1. Hub/Home.uxml
2. Hub/Hub.uxml
3. Hub/CurrencyHolder.uxml
4. Hub/PreLevel.uxml
5. Hub/BoosterSelector.uxml
6. Settings/SettingsPopup.uxml
7. Purchase/BoosterPurchasePopup.uxml
8. Game/GameplayHud.uxml
9. Game/TopBar/GameTopBar.uxml
10. Game/Overlays/GameOverlays.uxml
11. Game/Overlays/RestartConfirmPopup.uxml
12. Game/Overlays/LevelCompletePopup.uxml
13. Startup/StartupLoading.uxml
14. Common/ScreenFade.uxml

The 13 existing documents were already correct. New BoosterSelector initially used 350 x 450; its actual `canvas-width`/`canvas-height` IntegerFields were set to 1080/1920 and BuilderDocument.SaveSettingsToDisk invoked. Reopening confirmed persistence. Library/UIBuilder JSON was never manually edited. Home was left open clean after the final runtime suites.

## Original Home task limits and fresh graphical evidence

- Original task `TASK-20260927-215425472-f5013490-home-currency-holders-authored` AC5 remains **UNPROVEN: stale baseline**. Existing four PNGs were preserved in that task's verification/baseline before explicit CaptureHome regenerated docs/verification/hub-home images through the existing RenderTexture workflow. Fresh captures were viewed and are valid Home images.
- Fresh 1080 x 1920 SHA256: `70fef343c5038e3b1467752a5878a29b13fcd6a627ef819e12bd6c6206e90a69`; baseline `84874e4806ab3356d3de83cb444255c5afb0323b37e89826490a48a5abf5a4e2`.
- Fresh 1080 x 2400 notch SHA256: `c8db0bb1bf8ae354dfcef19a2501716251c2ab010b4c9859732b191976dce7f2`; baseline `9e2f39da3d4907963955b058e226c3a22020b571a57c0b8035813b1aee7ebdd9`.
- Exact RGB top-bar comparison is unequal: 64,869/259,200 changed pixels at 1920, 64,846/259,200 at notch. Both relative diff bounds `(37,48,1048,199)`. Baseline predates existing life-text padding/art changes and current tab locking; balances also differ. This comparison cannot establish task-caused regression or pixel-identical preservation. No broad layout/art repair was made.
- Comparison JSON and valid diff strips: original task verification/top-bar-comparison.json, home-1080x1920-top-bar-diff.png, home-1080x2400-notch-top-bar-diff.png. Fresh user-visible image: docs/verification/hub-home/home-1080x1920.png.
- Original AC6 is **not all passing** because of the unchanged purchase-overlay baseline assertion above. Hub runtime and SceneFlow smoke pass. These gaps must not be used to claim the original task ready to close.
- The unrelated runner's three gameplay/purchase failures were observed separately, not counted in these jobs and not repaired. Parent independently established the freeze-label scenario and purchase behavior as pre-existing baseline drift.

No lifecycle state, defect ledger, lock ownership, commit or closure was changed by this verifier. Editor lane is returned to the parent.
