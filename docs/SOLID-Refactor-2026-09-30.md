# SOLID Refactor — 2026-09-30

Applied the four medium-priority findings from the project review. The scope was readable, testable responsibility boundaries while preserving existing gameplay. Existing uncommitted work was retained; no scenes, prefabs, authored data, packages, project settings, or save formats were changed by this refactor.

## Changes

| Area | Change | Preserved behavior |
| --- | --- | --- |
| `Runtime/Player/PlayerSaveService.cs` | Routed mutations through the shared write pipeline; extracted snapshot/economy calculations into `PlayerEconomyRules`. | Serialized writes, atomic publication, no-op revisions, refill rules and initial seeding. A started write uses the service lifetime; caller cancellation can cancel queued work. |
| `Editor/Core/Levels/LevelDatabaseWindow.cs` | Extracted the shared template-deletion sequence into `LevelPieceDeleteOperation`; moved block rotation rules to `Canvas/LevelEditCommands`. `LevelPieceDeleteRoute` uses the same deletion sequence. | Confirmation, file-write/reload checks, draft updates, library deletion, Undo/cache handling, rotation validation and board placement. |
| `Runtime/Gameplay/Board/BoardPresenter.cs` | Extracted cast placement into `BoosterCastPlacement` and removal animation into `BoosterRemovalMotion`. | Cast coordinates, Hammer contact timing, grouped Vacuum removal, tween targets, pooling and cancellation. |
| `Runtime/Gameplay/Session/LevelSession.cs` | Injected `IFinishDeadline`; isolated PrimeTween in the scene-owned `PrimeTweenFinishDeadline`, registered by `GameSceneInstaller`. | The unscaled finish deadline, 0.8-second cap, completion ordering and cancellation on Home, background, teardown and disposal. |

Paths above are relative to `Assets/ColorBlockJam`. Helpers have bounded responsibilities and direct names. The session has a fakeable scheduling boundary; no additional DI or animation framework was introduced. The editor window and board presenter retain their existing orchestration responsibilities.

## Verification

- Independent static reviews checked the Player, Editor and Gameplay changes and their lifecycle/order guarantees; no actionable refactor defects remained.
- Roslyn compilation passed for affected assemblies and dependents after public constructor changes. The final test-only corrections also passed compilation.
- Added 27 test cases: seven save/cancellation cases, six session deadline cases, two PrimeTween adapter cases, eight deletion cases and four rotation cases. Updated existing constructor wiring to supply the scheduler.
- Corrected two existing test setups: synchronize the armed canvas tool before placing a block; mark the first block touch before checking the Time Freeze countdown (DEC-189). Both focused retries succeeded.
- EditMode: 998 cases passed when combining the initial run (997 passed, one inconclusive fixture setup) with its successful one-case retry.
- PlayMode: 81 cases completed; three failures were recorded initially. The Time Freeze fixture correction passed its focused retry, leaving the two pre-existing purchase-popup disagreements below. The MCP result payload for the initial PlayMode run was null; its completed count and uncapped failure list are the retained evidence, rather than a fabricated aggregate pass summary.

| Test job | Evidence |
| --- | --- |
| `67095d066a2f4ddb9d841bb5cea901f9` | EditMode: PlayerData, Gameplay and focused level-editor suites; 997 passed, one inconclusive. |
| `f7c5a9f532a24a2698b03e17bc3e3256` | Canvas recolour fixture retry: succeeded, 1/1 completed, no failures. |
| `37329d6a0cbb477ba2c80451315ce282` | PlayMode: board presenters, finish deadlines, booster composition, dragging, arrows, ice, gameplay HUD and purchase popup; 81/81 completed, three failures initially. |
| `04b972a4d8cd456789ec4faf75993b98` | Time Freeze retry: 1 passed, zero failed or skipped. |

## Existing failures retained for follow-up

`InGamePurchasePopupTests` has two failures:

- `ASecondBuyWhileTheFirstIsSavingIsRefused`
- `BuyGrantsStockRefreshesTheBadgeAndKeepsThePopupOpenWithoutCasting`

Both expect a visible purchase-success message. The existing `BoosterPurchasePresenter` instead raises `CloseRequested` on a successful purchase; `InGamePurchaseHost` closes the popup and the view clears its message. That runtime branch already exists in Plastic changeset 38, while DEC-138 and the tests expect the popup to remain open. These tests use a fake booster store, not the refactored `PlayerSaveService`. Existing purchase behavior was preserved; resolving the product/specification disagreement remains separate work.

The tests ran in Unity 2022.3.62f2 in the shared modified workspace. Android/iOS builds and physical-device visual checks were not performed. This is focused regression evidence, not a claim that every project test or device behavior has been verified.
