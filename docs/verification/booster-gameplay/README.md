---
title: Booster gameplay verification
created: 2026-09-27
modified: 2026-09-28
type: verification
status: closed by the user with captures, Profiler and device checks not performed
tags: [boosters, gameplay, hud, verification]
---

# Booster gameplay verification

Evidence for `TASK-20260927-004448215-54655902-booster-gameplay-flow`, checked
against [Booster Gameplay](../../Booster-Gameplay.md) (B-1 to B-32) and PROJECT.md
DEC-146 to DEC-151. Sections still marked pending claim nothing. The user closed
the task on 2026-09-28 ("kapat artık tamamlayıp") before the captures, the
Profiler sample and the device checks were taken.

## Final runs (2026-09-28, after the AC-12 host and the outline merge)

Batchmode runs in the isolated copy after BoardPresenter was merged with the held-outline
session's HeldNeighbourhood hunks. Result files: `Logs/results-final-pm-presenter.xml`
and `Logs/results-final-em-gameplay.xml` in the copy.

| Suite | Filter | Result |
| --- | --- | --- |
| Gameplay PlayMode | HeldOutline, BoardPresenter, Booster, GameScene, Vacuum, Frost, Hud, Purchase, Targeting | 109/109 passed, including `InGamePurchasePopupTests` 7/7, `GameSceneBoosterCompositionTests` 10/10 and `HeldOutlineTests` 9/9 |
| Gameplay, Domain and Player EditMode | all | 629/629 passed, including `InGamePurchaseHostTests` 9/9, `BoosterIntentSessionTests` 21/21 and `BoosterConsumptionTests` 11/11 |

After the CBJ Template particles were added (DEC-152, B-33), with the outline session's ramp files:

| Suite | Filter | Result |
| --- | --- | --- |
| Presentation PlayMode | `BoosterEffectViewTests` | 21/21 passed, including six new particle tests: each cast carries its template particle idle until Play, the Hammer burst starts at the strike in the hit colour and outlives the motion, Suspend holds the show and Resume continues it, Stop clears a running show, the Vacuum suction emits only in its intake window, the Rocket shatters at its target cell in the cell colour |
| Gameplay PlayMode | BoardPresenter, GameScene, Booster, Vacuum, Hud, Purchase, Outline, ExitFragment | 112/112 passed, including `BoardPresenterBoosterAllocationTests` (0 B with every cast and its particle playing) |
| Gameplay and Editor EditMode | Gameplay, Booster, Motion | 545/545 passed |

Superseded assertion: `AHammerTargetKeepsItsRootUntilTheStrikeThenShrinksWithinTheFinishBound` waited 1 s for the cast to return; it now waits for the strike plus the 1.2 s hit burst, because the pool returns a view only after its particle show. The Finish bound assertions are unchanged.

Live-Editor regression fixed before the checkin (reported by the Hub session): five Game-scene tests with a simulated mouse (`GameSceneDragTests`, `GameSceneIceTests`, `GameSceneLearnedShapeTests`, two `HeldOutlineTests`) threw `IndexOutOfRangeException` at `InputManagerStateMonitors.cs:31` in the long-running live Editor only. The Game scene's `InputSystemUIInputModule` referenced the package's shared `DefaultInputActions` asset, whose controls stay resolved against earlier devices; after `InputTestFixture` swaps the input state, enabling it indexes past the new device array. The module's 11 serialized action references are now empty, so it assigns fresh default actions on enable. `GameSceneUiInputTests` resolves the shared asset first and then loads the Game scene under a fixture: red with the old scene, green with the fix. `AFinalVacuumRunsInsideTheSessionsBoundedWonFinishAndTeardownReturnsItsRootsAndCast` now waits for the shrink to start instead of a fixed wall-clock point, which a throttled Editor could reach before the tween's first frame. Windowed copy run after the fix: 55/55 (the regression test, the five input tests, `BoardPresenterBoosterTests`, the HUD, purchase and composition suites).

Defect D-001 after the checkin (reported by the held-outline session on cs:16): `BoosterSerializedAssetTests.GameScene_WiresTheHudDocument_EventSystem_AndInstallerFields` still asserted the shared `DefaultInputActions` GUID that the input fix removed; the EditMode suite had last run before that fix. The test now asserts that `m_ActionsAsset` and the ten action references stay empty. Copy run at cs:16 with the updated test: Gameplay EditMode 542/542, including this test.

Re-run at cs:17 (the held-outline task's Highlight Plus change, which also switches the outline on held and Vacuum-target roots inside `BoardPresenter`), with the same test update: Gameplay EditMode 522/522 (the 20 `FootprintOutlineTests` left with the ribbon outline), Gameplay PlayMode 125/125 (every `BoardPresenterBooster*`, lifecycle, HUD, purchase, composition and `GameSceneUiInputTests` fixture), Player EditMode 88/88 and `BoosterEffectViewTests` 21/21.

Defect D-002 (reported by the Hub session from the live Editor at 97% paged memory): `AFinalVacuumRunsInsideTheSessionsBoundedWonFinishAndTeardownReturnsItsRootsAndCast` failed with "Teardown waits for the final presentation". The Finish deadline is a real-time delay of at most 0.8 s, and the shrink runs on game time after `0.13 × Duration`. So one frame as long as the rest of the window ends the bounded Finish before any frame inside it (the DEC-93 timeout). A temporary 1 s sleep before the wait loop reproduced the exact failure in the copy. The test now accepts that path only when a frame of at least `finishSeconds − intakeStart` was measured, and keeps the shrink checks for normal frames and the cleanup checks for both. Copy runs: the fix passes under the same 1 s frame, and Gameplay PlayMode is 125/125 without it.

The live Editor (ColorBlockJamCase2026@99077e4df1ecfaa4, 2022.3.62f2, root read
back) finished its script compilation after the copy-back without errors; its
Console holds only ONITY006 test warnings and foreign "referenced script is
missing" warnings. Every script that `Game.unity` references resolves. The
checkin set was proven to compile on the server head with the rest of the
workspace reset to it (18 assemblies, 0 errors).

## Automated results

Full batchmode runs on 2026-09-28 in the isolated copy (Unity 2022.3.62f2), after
fixer round 1. Result files: `Logs/results-fix1r-edit2.xml` (EditMode) and
`Logs/results-fix1r-play3.xml` (PlayMode) in the copy.

| Suite | Assemblies | Result |
| --- | --- | --- |
| Domain and Gameplay EditMode | `ColorBlockJam.Gameplay.EditModeTests` | 513/513 passed |
| Player EditMode | `ColorBlockJam.Player.EditModeTests` | 88/88 passed |
| Editor EditMode | `ColorBlockJam.Editor.EditModeTests`, `ColorBlockJam.Editor.LevelPreview.Tests` | 939/940 and 22/22 passed; 1 skipped: `LevelTestPlayRoundTripTests` is run by name |
| Other EditMode | Environment, Hub, UI | 91/91, 81/81, 56/56 passed |
| Gameplay PlayMode | `ColorBlockJam.Gameplay.PlayModeTests` | 113/113 passed |
| Environment PlayMode | `ColorBlockJam.Environment.PlayModeTests` | 24/24 passed |
| Presentation PlayMode | `ColorBlockJam.Presentation.PlayModeTests` | 41/42: 1 foreign order-dependent failure (below) |
| Hub PlayMode | `ColorBlockJam.Hub.PlayModeTests` | 15/20: 1 failed, 1 inconclusive (foreign, below), 3 capture tests run by name |
| Compile, all 22 assemblies | full list | 0 errors, 0 project warnings |

Totals: EditMode 1792 (1791 passed, 1 skipped); PlayMode 199 (193 passed,
2 failed, 1 inconclusive, 3 skipped).

### Classified failures (none in Booster-owned tests)

- `HubLaunchTests.LevelOpensThePreLevelPopup_CloseAndBackReturnHome_AndSpendNothing`
  (failed) and `HubLaunchTests.PlayInHub_OpensGameOnce_WithTheShownLevel_AndCarriesTheSelectionWithoutSpending`
  (inconclusive): foreign and order-dependent. Both pass alone. The earlier
  `LaunchRequest_PlaysTheRequestedIndex_InsteadOfTheSerializedOne` launches Game
  with Extra Time and Rocket through the real project scope, so the correct
  Booster opening debit (B-16, DEC-92) spends the Editor store's seeded charges
  and `HubTestSupport` never resets them. Owner: the Hub pre-level launch task.
  This leaves AC-13 without a passing end-to-end Hub-to-Game proof in the full run.
- `BoardArtContainerTests.GameSceneResolvesOneBoardArtWithLoadedShapesAndReleasesItWithTheScene`:
  foreign and order-dependent. It passes alone. After `HubStartupFailureTests`
  leaves Hub loaded, its additive Game load makes Unity log "There can be only
  one active Event System". The shipped flow loads scenes single, so no runtime
  guard is added (Booster-Gameplay.md section 6). Owner: the test's owner (unload
  Hub first or expect the warning).
- `SceneFlowSmokeTests.InitKeepsTheLoadingScreenUntilHubIsReady`: foreign and
  intermittent. It failed once in four full PlayMode runs (`sceneCount` 2 instead
  of 1 just after Init unloaded) and passed in the other three.

### Superseded assertions

- `BoardPresenterBlockedFeedbackTests.AFrozenBlockShakesOnlyItsShell` (Booster's
  own) asserted the pre-merge shell-only Ice shake. The merged Ice refinement
  punches the frozen block's root, so it is replaced by
  `AFrozenBlockPunchesItsRootOnceThroughItsIceView`.
- `GameSceneIceTests`: the Ice task's `GC.GetAllocatedBytesForCurrentThread`
  checks read 0 in this Mono runtime (vacuous). The Recorder-based constraint now
  holds steady Sync at 0 B and a count change at UI Toolkit's two numeral copies
  (B-31, DEC-148). The Ice owner's shared copy still has the old check.
- `BoardPresenterArrowMarkerTests`: 2 prepared markers become 5 (B-19, AC-16),
  cited in the test.

### Acceptance evidence and gaps

| AC | Automated evidence | Still open |
| --- | --- | --- |
| AC-1, AC-2 | HUD layout, composition and bar presenter tests; `TheScenesPointerFilterVetoesTheBoosterBarButNeverTheBoard` resolves the Game scene's own filter | Captures at 1080 x 1920 and 1080 x 2400 |
| AC-3, AC-4 | `AttemptCountdownTests`, intent session and opening tests | none automated |
| AC-5 to AC-9 | Domain split, handler, target and replay tests; presenter Rocket, Hammer and Vacuum tests | Cast placement check in the live Editor |
| AC-10 | Intent session and coordinator tests | Shipped scene keeps the DEC-116 foreground resume (Booster-Gameplay.md section 6, Open) |
| AC-11 | `Home/RestartAfterACommittedUseKeepsItsChargeSpentAndItsDefinitionsUnchanged`; `AGatedDebitKeepsTheSessionSavingUntilItResolves` checks the board over the open write; `BoosterConsumptionTests` | none automated |
| AC-12 | Coordinator tests with a fake host; EditMode `InGamePurchaseHostTests` (real popup view over a HUD clone: zero-stock press under `Paused(Purchase)`, Buy follows `IsSaving`, close and Back resume, backgrounded close keeps the pause, Home/Restart/foreground/Dispose hide, missing popup blocks); PlayMode `InGamePurchasePopupTests` (laid-out panel: empty bar button opens above the bar, Buy grants and refreshes without casting, refused and failed purchases change nothing, duplicate Buy while saving refused, close button and scrim tap resume, late result shows nothing); `GameSceneBoosterCompositionTests.TheSceneHostsTheSharedPurchasePopupHiddenInTheHudAndShowsItUnderPausedPurchase` (composed scene binds the real host). All passed in the final runs | Capture `purchase-1080x1920.png`; the shipped DEC-116 foreground resume hides the popup and resumes (same interim as AC-10) |
| AC-13, AC-14 | `GameSceneBoosterCompositionTests` launches, opening and background tests | Hub end-to-end test blocked by the foreign stock leak above; live Hub-to-Game check |
| AC-15 | Presenter lifecycle, frost, cast and teardown tests | Tracker and Profiler checks in the live Editor |
| AC-16 | Allocation tests; `AFrozenRectangleAtItsLargestPartCountUsesOnlyWarmedRootsShellsAndLabelsOnEveryAttempt`, the cross and learned-shape splits, Arrow diagonal parts | Editor Profiler sample |
| AC-17 | Existing Ice, Arrow, drag, gate and command suites pass; `ABarrierStyleListenerFlipsOncePerClearedBlockAndNeverForASplitOrAMove` | The foreign failures above |
| AC-18 | Booster-Gameplay.md, PROJECT.md DEC-146 to DEC-151, this file | Final diff review |

## Captures at 1080 x 1920 and 1080 x 2400

| File | What | Status |
| --- | --- | --- |
| `hud-1080x1920.png` | Timer band, booster bar with count and plus badges | pending |
| `hud-1080x2400.png` | The same at 20:9 with the safe area | pending |
| `rocket-targeting-1080x1920.png` | Banner under the timer, crosshairs on every eligible cell | pending |
| `rocket-split-1080x1920.png` | Bridge cell removed, two independent parts | pending |
| `hammer-cast-1080x1920.png` | Mallet at the block's top centre; whole block removed | pending |
| `vacuum-cast-1080x2400.png` | Tapped block white, matching roots moving into the intake | pending |
| `opening-1080x1920.png` | Extra Time and Rocket opening icons, countdown held | pending |
| `purchase-1080x1920.png` | Zero-stock purchase popup over the dimmed board and bar, under `Paused(Purchase)` | pending |

## Profiler sample

- Steady Playing, targeting and frozen ticks: GC Alloc per frame: pending.
- Timer and freeze-counter text changes (B-31): allocation per shown-second
  change: pending. Decision on pre-built digit elements: pending.
- No `Instantiate` during Rocket split, Hammer and Vacuum casts: pending.
- No gameplay sample outside Playing while the result popup is open: pending.

## Frost proof

- `frost-proof.png` (540 x 960): `TimeFreezeOverlay.mat` rendered in batchmode by
  the copy-only setup script with `_FrostAmount` 1 over a mid-grey (128, 128, 128)
  background. The centre stays mid-grey and the screen edges turn cyan-white
  (left edge RGBA 164, 207, 246; top edge 162, 208, 248); at `_FrostAmount` 0
  every sampled pixel stays mid-grey. It proves the graph's edge mask and
  amount only, not the in-game fade, pause or teardown below.
- Time Freeze on: screen-edge frost fades in, HUD stays above it: pending.
- Pause keeps the frost and both clocks; Resume continues: pending.
- Expiry and Teardown clear the frost (`_FrostAmount` 0, quad hidden): pending.

## Pending device checks

- Android IL2CPP smoke build with the booster flow: pending.
- Device Profiler: 0 B steady Playing and the B-31 text allocation: pending.
- HUD layout and crosshair contrast on the 20:9 phone: pending.
- Backgrounding during a booster debit and during the starting commit (B-30): pending.
- Full flow on the delivered APK: pending.
