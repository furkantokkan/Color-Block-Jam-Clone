# Independent final verification

2026-09-27. Authority: `sha256:3381dfdaacf6364c4e99663c09c69bf0b06e2d2d7ba841d9ced83a4c0eb221e1`. All six criteria pass through combined focused tests, source review and actual rendered evidence. No remaining production defect found. Verification wrote only this report, generated hash snapshot and captures.

| Criterion | Result and evidence |
| --- | --- |
| AC-1 | PASS. Actual canvas has snowflakes, 1/20, Arrow and readable IDs. Badges start centered below the ID; synthetic E1–E7 overflow shows every badge in neighboring columns. Focused authoring test proves add/change/remove/Undo; isolated production controller probe proves Undo/Redo; retained tests prove JSON roundtrip and restored canvas snapshots. Actual user preview shows Ice. |
| AC-2 | PASS. Focused domain/presentation and real Game tests cover frozen pointer/direct Move refusal, exit refusal, occupancy/collision and bounded feedback. MoveBlockHandler checks IDragGate before movement axes. |
| AC-3 | PASS. Retained tests cover other-block actual Leave decrement once, same-sync label update, zero thaw, ignored self/removal/move/Stay, publication and generic forwarding. Runtime Arrow remains gated false; editor/preview Arrow markers preserved. |
| AC-4 | PASS. Shared preview and actual Game pixels show square/concave/ring shells following the kit, empty hole, recognizable base colors/studs. Source/tests verify shared material and per-renderer properties. |
| AC-5 | PASS. Retained real Game/focused tests cover pause/restart/failure/Home/unload/preview disposal, rollback, authored reset and warmed zero-allocation assertions. These are test assertions, not device profiler claims. |
| AC-6 | PASS. Actual UI-inclusive Game captures at 1080×1920 and 1080×2400 have aligned readable 1/20 counters. Tests/source cover pointer ignoring and cached strings. Protected authored inputs and live user state preserved. |

## Rendered evidence

- `final-preview-user-portrait.png`: repaired live user draft b2 Pink Ice 1, textured pale frost/cyan rim with Pink/studs retained.
- `final-preview-shapes-portrait.png`: shared LevelPreviewHost square/concave/ring, 1/20 and editor-only Arrow.
- `final-runtime-portrait-valid.png`: actual Game ScreenCapture including UI Toolkit, 1080×1920.
- `final-runtime-20x9.png`: actual Game ScreenCapture including UI Toolkit, 1080×2400; settled frame, aligned counters.
- `final-canvas-current.png`: actual LevelCanvasElement rendered on owned transient UIDocument, cloned PanelSettings and RenderTexture during Play rendering; not a desktop crop.
- `final-canvas-visible-columns.png`: same canvas with synthetic E1–E7 generic test badges; visible overflow columns, no authored new effects.
- `preview-user-draft-portrait-1.png`: BEFORE D004 repair; visible counter but absent frost. Diagnostic only.

Verifier and lifecycle owner inspected the accepted pixels. Black, obsolete layout and stale projection files were removed; they support no acceptance claim. Shape captures use transient data and an isolated resolver seeded with actual Game scene collaborators and one custom catalog. They prove production presentation/projection; ordinary startup is separately covered by real Game tests. No authored JSON/scene was altered. Accepted channels are preview portrait and runtime both aspects; discarded preview 20:9 captures are not claimed as passes.

## Tests

| Job | Result | Scope |
| --- | --- | --- |
| `5140d4aeaeb14a70bd61947dfcdefcb0` | 44/44 EditMode passed | Original focused coverage. Terminal summary independently read; details expired, original evidence retained by lifecycle owner. |
| `e1215b0fa7134262a7464abbfcc28bf2` | 13 valid standalone PlayMode passes | Presentation/Arrow. Three original startup failures were repaired and replaced by next job; this job is not wholly passing. |
| `773ea43f5fa1421f9991e9e34110d080` | 3/3 PlayMode passed, 3.416 s | Independently inspected GameSceneIce, GameSceneDrag, GameSceneLearnedShape. Includes reachable failure, lifecycle/generic/allocations, bound custom catalog and ordinary unbound startup. |
| `494815ffc5784dd5af07f732626f5a70` | 22/22 EditMode passed, 1.574 s | Independently inspected HLSL 4, JSON 1, canvas layers/layout 5, ID labels 5, Arrow 7. |
| `3161f89864f3436e9028289a28242ecc` | 1/1 EditMode passed, 1.594 s | Independently run LevelEffectEditorTests.BlockEffectsAreAddedChangedAndRemovedThroughTheDraft. |

Jobs overlap; do not sum them as unique tests. Final in-memory manual production LevelDraftController probe used its own transient draft: edit Ice 1→20, Undo→exact count-1 JSON, Redo→exact count-20 JSON. Both comparisons passed; user draft JSON comparison unchanged=true. Dispose cleared only the owned Undo target. No broad suite repeated.

| Defect | Current verification |
| --- | --- |
| D001 | PASS: reachable test countdown failure and pause-before-Home in real Game Ice test. |
| D002 | PASS: vector snowflake/blue badge and current below-name/column layout in actual pixels. |
| D003 | PASS: registered catalog collection lookup avoids unbound concrete auto-resolution; real Game startup tests pass. |
| D004 | PASS: actual numeric slot values repaired, generated HLSL regressions pass, live user preview and Game frost visible. |

## Preservation and release

Fresh instance `ColorBlockJamCase2026@99077e4df1ecfaa4`, session `5f9801bd-06d0-4be8-88d6-2f120448a8c8`, Unity 2022.3.62f2: idle, no tests/compilation, Play stopped. LevelEditor scene clean; existing GameView size index 28 (1080×1920) restored. No owned temporary GameObjects remain; shared PanelSettings targetTexture was never changed.

Latest live level-01 draft preserved: b1 LightBlue Square (2,3); b2 Pink Square (0,1), Ice clearsToThaw=1; unsaved=true. Full draft JSON compared unchanged during Undo/Redo. Historical baseline was never restored over newer edits. Foreign Hub runners were never cancelled.

Effective ice TextureImporter max=1024; imported dimensions=1024×1024; Android/iPhone max=1024. `verifier-snapshot.json` records final relevant source/artifact SHA256 rows and all 13 protected inputs: all 13 match `Library/ccgs/ice-baseline-945d78fe/preserved-before.json`, zero diffs. Lifecycle owner's canonical review-inputs.json was not modified. Onity remains v0.3.14. No production, asset, package, lifecycle or VCS writes by this verifier.

Verification complete; Editor slot released to lifecycle owner. Device build/performance and lifecycle closure/checkin are outside this verification claim.
