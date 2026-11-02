# Reference refinement verification

Independent verifier, 2026-09-27. Current reference refinement passes the focused behavioral and rendered checks below. This report supplements the historical `independent-verification.md`; it does not retroactively change that checkpoint. Only evidence files were written by this role. Lifecycle, defect classification and checkin belong to the task owner.

## Fresh tests

| Job | Result | Interpretation |
| --- | --- | --- |
| `3244c93baff7426bbfda9a876b2d2154` | 19 completed; 18 passed, 1 failed | Focused IceRules, BlockEffectComposition, IceAuthoring, LevelCanvasEffectLayer and PreviewIce. Only failure was exact Color equality in IceShellUsesAuthoredMaterialTintAndOpacity. Individual result details were absent from this failed MCP response; do not claim an independently retained 18-name callback list. |
| `becd018ce4224cac925582c632f42ea7` | 1/1 passed, 0.4303661 s | Repaired IceShellUsesAuthoredMaterialTintAndOpacity, after forced source import, fresh compiled DLL and independently inspected 1e-6 tolerance in IL. |
| `4ed24ac79867428381aeb6a94b9b95fe` | 5/5 passed, 1.3795407 s | Real Game Ice lifecycle plus all four BlockIcePresentation tests; detailed names/results independently inspected. Current Onity v0.4.0. |

The five final PlayMode methods were LoaderPreparesSharedContextAndIceThawsAndRestartsInRealGameScene, AttachedCounterIsCentredBelowProjectedSolidMidpoint, ConcaveAndHoledShellsUseSharedMaterialAndResetClipCracksAndTweensOnReturn, CounterComposesSnowflakeAndCachedCountsAndIgnoresPicking, and CounterUsesContinuousSolidMidpointAndDeterministicOccupiedFallback.

D009 was a test precision issue: expected R=0.23999995, actual R=0.23999992, delta=-2.98023224e-8; G=0.72, B=0.96, A=1 and opacity=0.9 matched. The owner changed only the four-channel tolerance assertion. No production change was justified by that failure.

Initialization timeouts `e12843dae9e245ccba625352e795cace` / `af439f862423454ca5dceabe7b92181a` and mixed collector job `e616795c2b7c4ccab018ee82b09a8378` support no acceptance claim. The pinned package's TestRunnerNoThrottle helper was applied for owned verification, restored automatically after tests, and explicitly restored after owned captures; its SettingsCaptured flag was false at release. No foreign runner was cancelled.

## Actual rendered evidence

All final captures use GameView ScreenCapture with no camera argument, including the actual UI Toolkit badge. The verifier opened and inspected each image.

| Image | Observed result |
| --- | --- |
| `reference-final-preview-user-portrait.png` | Actual latest unsaved draft: Pink square Ice1, cyan textured frost, pale perimeter/stud rims, white snowflake+1 centered below projected midpoint; plain cyan b1 remains unchanged. 1080×1920. |
| `reference-final-preview-shapes-portrait.png` | Shared preview square1, Pink concave20, LightBlue ring20; supplied texture follows kit surface; ring hole open; Arrow markers and counters readable. 1080×1920. |
| `reference-final-preview-shapes-20x9-valid.png` | Same shared preview at 1080×2400, all labels aligned on their blocks and Arrow markers retained. Before final Reproject, actual Screen=1080×2400 and panel root=960×2133.33 were verified. |
| `reference-final-runtime-portrait.png` | Actual Game scene, square1/concave20/ring20, visible frost/studs and aligned badges. 1080×1920. |
| `reference-final-runtime-20x9.png` | Actual Game scene, same fixture, framed board and aligned 1/20 counters at 1080×2400. Runtime Arrow gameplay remains gated. |
| `reference-final-half-clipped.png` | Square's left frost half disappears, exposing red; right half remains frozen. MPB before and after capture: ClipEnabled=1, ClipPlane=(1,0,0,2.5), 16 shell renderers. No reload occurred between those observations. |

Runtime boot independently produced a live ready Game SceneContext with nonnull Container and a live ProjectContext. The shape capture fixture used a disposable database and parentless isolated resolver with one learned catalog, borrowing the actual Game EnvironmentView, effect source, session and presenter. These captures prove production rendering/projection; the separate passing real Game test proves loader/lifecycle behavior. No authored database was changed.

An initial tall preview capture used stale panel geometry during aspect switching; it was rejected. The next rebuild briefly reported Screen=2462×1927 and a NaN panel. A settled separate Reproject at the actual portrait geometry produced the accepted image above. Both rejected verifier images were removed; no acceptance relies on them. This is a capture-order limitation, not a claim that transient invalid panel geometry is acceptable.

The shader owner's `reference-alpha-zero.png` versus `reference-shell-hidden.png` was independently compared again: 1080×1920, only five pixels differ, maximum RGB difference [0,1,0]. This supports no residual halo at zero opacity. Generated shader uses SrcAlpha blending and graph PreserveSpecular=false. The owner's old `reference-half-clipped.png` is invalid and is not used; the fresh final clipping image replaces its evidentiary role.

## Acceptance coverage and retained evidence

| Criterion | Current result |
| --- | --- |
| AC-1 | PASS: retained authoring Undo/Redo and canvas under-name/visible-column evidence remains applicable because those production paths were unchanged. Fresh focused canvas/JSON/preview coverage plus actual user preview confirms Ice remains visible. |
| AC-2 | PASS: fresh real Game test covers frozen pointer refusal, unchanged piece position and bounded local feedback; retained direct Move/exit/occupancy tests remain applicable. |
| AC-3 | PASS: fresh real Game test covers Leave decrement, count update and zero thaw. Focused IceRules/composition coverage remains current; editor Arrow markers visibly coexist. |
| AC-4 | PASS: new preview and actual Game pixels show revised cyan frost and pale rims on square/concave/holed kit surfaces, using the supplied texture; material and shell tests pass. |
| AC-5 | PASS: fresh real Game covers restart, reachable failure, Home, generic lifecycle and warmed allocation assertions; fresh presentation tests cover clip/crack/tween return reset. No device profiling claim is made. |
| AC-6 | PASS: actual UI-inclusive preview/Game 1080×1920 and 1080×2400 evidence plus attached-panel geometry tests show readable snowflake+1/20 below solid midpoint; hole fallback stays occupied. Picking is ignored and cached count behavior passes. |

D001 failure harness is freshly covered by real Game; D002 canvas proof remains valid under unchanged canvas production dependencies; D003 catalog lookup/helper behavior is freshly covered under Onity v0.4.0; D004 graph numeric-slot visibility is superseded by fresh HLSL/material tests and visible frost. New counter/reference rendering is evidenced by the images above. Historical screenshots are not presented as proof of the new frost appearance.

## Preservation and release

Registry registration matched discovery session `fda0e3f3-01d8-4a21-accd-5f652fa89c51`, instance `ColorBlockJamCase2026@99077e4df1ecfaa4`. Pinned project info confirmed the exact repository and Unity 2022.3.62f2. Fresh actual TestRunnerApi confirmed idle before capture and at release.

At 10:42:06.0886039Z: Play=false, compilation=false, LevelEditor clean, original GameView preset28 restored, latest level-01 full draft JSON unchanged from the fresh before observation, unsaved=true. b1 LightBlue Square(2,3); b2 Pink Square(0,1), Ice clearsToThaw=1. The owned runtime database was destroyed, temporary clipping disappeared with fixture return, shared PanelSettings was never changed, throttling helper captured=false. User window was returned to focus. No historical draft baseline was restored over newer edits.

Live MAT_Ice texture path is `Assets/ColorBlockJam/Art/Textures/icetexture.jpg`; effective importer max1024, imported1024×1024, Android/iPhone max1024. ShaderHasError=false. All 13 protected hashes match the original baseline, including supplied texture bytes, authored databases, library/catalog assets and LevelEditor scene.

`reference-verifier-snapshot.json` records actual source/artifact hashes, all 13 preservation rows and relevant external binding/package hashes. The seven production/material handoff hashes and repaired test hash match. Onity v0.4.0 is an authorized external dependency update; manifest/lock and current ProjectContext/ProjectScopeInstaller hashes are included separately. No production, tests, serialized assets, package settings, lifecycle or VCS writes were made by this verifier. The owner's canonical review-inputs manifest was not modified.

Editor verification slot released. No remaining production failure observed in this bounded verification; reference likeness remains a visual review judgment, not pixel identity to a reference photograph.
