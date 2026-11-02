# Implementation

- Shared Common styles and the existing LiberationSans-UITK font are explicit standalone dependencies. ScreenFade/StartupLoading Style elements now use the ui namespace. All 14 UXML files parse and their referenced files exist.
- Standalone Settings, PreLevel, RestartConfirm and LevelComplete roots are visible. Hub/HUD/composition rules initialize embedded overlays closed before binding; inline view visibility still opens them.
- CurrencyHolder and HUD include representative icons/data. PreLevel uses BoosterSelector.uxml for both authoring samples and runtime catalog clones; binding discards samples and replaces labels/counts/icons, including null icons. The source reference survives rebind.
- ScreenFade previews opaque black; its view runs after UIDocument initialization and hides synchronously on enable.
- Roslyn: ColorBlockJam.Runtime COMPILE_OK for ScreenFadeView; ColorBlockJam.Hub COMPILE_OK for final PreLevelView. No packages, scenes, prefab/data assets, existing meta files or checkins changed.
- Purchase popup sources are owned by the separate purchase-authoring task; those bytes were preserved. Its claim released before the aggregate Editor verification handoff.
- Independent verifier owns only PreLevelAssetTests and UiTemplateAuthoringTests, plus the sole Editor verification lane. Final evidence is recorded separately.
- Verification-only repair also updated SettingsPopupAssetTests to the five already approved assembly dependencies (Haptics and PrimeTween were absent from its old expectation). No assembly reference was added.
