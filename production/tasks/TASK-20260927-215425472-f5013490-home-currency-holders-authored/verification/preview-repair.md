# Home preview repair

- Cause: standalone Home depended on the runtime panel's Common styles and font. Explicit dependencies, equal Level 1 sample layers and screen clipping repair the UI Builder preview without changing presenter bindings.
- Independent verification: Home clean at 1080 x 1920, aligned heavy labels and expected font/button resets. HomePresenterTests 22/22; Hub PlayMode 17/17; SceneFlow smoke 6/6. CaptureHome produced four valid images under docs/verification/hub-home. Root inspected the fresh 1920 image visually.
- D-001 is verified. AC-2/3/4 presenter coverage and AC-7 authored-holder documentation remain valid; PROJECT.md DEC-143 and design sections 3.3/7 were read back.
- AC-5 remains unproven: preserved historical PNGs differ in existing art and life-text padding as well as the permitted coin value. See baseline/ and top-bar-comparison.json. These differences are not evidence that this scoped repair changed those pre-existing rules.
- AC-6 is not fully passing: the additional Hub EditMode run passed 103/104; Purchase_RefreshesTheSelectorCount_WithoutSelecting_AndStaysOpen expects an open popup, whereas both cs:38 and current BoosterPurchasePresenter close after a successful purchase. No purchase behavior was changed by this task.
- The separate template task records all 14 documents' authoring verification and 45 passing focused cases. No automatic closure is claimed for this reopened historical task.
