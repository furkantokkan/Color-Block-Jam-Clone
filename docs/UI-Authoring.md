# UI authoring

Open the actual UXML under `Assets/ColorBlockJam/UI` in UI Builder. Set that document's Canvas width and height to **1080 x 1920**, save through UI Builder, reopen and check the saved dimensions. Fit Canvas changes zoom only. Never edit `Library/UIBuilder` settings while the Editor is open.

- Every standalone screen/widget imports its styles and the existing runtime font. `Common.uss` supplies button resets and paired heavy text; `Document.uss` supplies the font. Keep both text layers equal.
- Popup documents preview visibly without gameplay or temporary visibility edits. Open SettingsPopup, PreLevel, BoosterPurchasePopup, RestartConfirmPopup or LevelCompletePopup directly. GameOverlays previews Settings; Hub and GameplayHud preview their ordinary screens.
- Host-only rules in Hub.uss, GameplayHud.uss and GameOverlays.uss initialize embedded overlays closed before binding. Runtime views override display inline to open/close them. Do not hide the standalone document to fix a host.
- Comments identify representative values and icons. Runtime presenters replace samples from the level catalog, player save and offer data; samples are never economy or progression defaults. Optional feedback and targeting states stay controlled by their existing state classes.
- PreLevel includes two representative BoosterSelector instances. Edit BoosterSelector.uxml to change every selector's layout. PreLevelView caches that authored template, removes preview items on bind and clones it for the current catalog; only icon, label, count and state change at runtime. Rebinding keeps the template reference.
- GameplayHud includes representative booster icons; ShowSlot replaces or clears them. Home binds its authored CurrencyHolder instances by ID and replaces the Level/route values. CurrencyHolder itself previews a representative coin holder.
- ScreenFade previews black. Its view runs after UIDocument initialization and hides it synchronously on enable, including re-enable; loading and scene-transition behavior still owns its opacity and visibility.

Do not construct reusable screen or popup layout in C#. Keep static content, fonts and styles in UXML/USS. Preserve element names used by the views, sprite borders and existing reference geometry.

## Verification note

Independent verification passed 45 focused UI/Hub tests, including all 14 documents' imports, standalone styling/visibility, unbound host reattachment, sample replacement, open/close and fade initialization. Every UI Builder document was reopened clean at 1080 x 1920. Hub PlayMode and SceneFlow passed 23/23 runnable cases; the explicit Home capture test produced fresh 1920 and 2400-notch images.

The additional Hub EditMode run passed 103/104: its remaining legacy purchase test expects the popup to stay open, while the unchanged cs:38 presenter closes it after purchase. The older Home screenshot baseline also predates existing art/padding changes, so exact historical top-bar pixel equality is unproven. These gaps are recorded separately from the verified authoring repair. No device build was run.
