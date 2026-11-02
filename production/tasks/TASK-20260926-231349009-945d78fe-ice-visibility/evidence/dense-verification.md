# Dense Ice verification — repair handoff

2026-09-27. Authority at verification: `1d430c914e73bf11f6c50cb304c86050376d615e89c0ae8d8192ef8bcd43094f`. **Behavior passes; reference fidelity fails.** Do not close or check in this revision as visually accepted. All results/images below are evidence before the next font/rim repair.

## Tests

| Job | Fresh result |
| --- | --- |
| `a477bd07a65f4c2ebf55e5d76178e9e2` | EditMode20/20 passed,1.9458621s: authoring7 (including generated scalar .98), canvas5, shared preview1, IceRules4, composition3. |
| `7040ebf76b3343b7a9d100acb5ca7385` | PlayMode5/5 passed,.8153383s: real Game Ice lifecycle; attached projected-cell centering/scaling; concave/holed shell pooling/clip/crack/tween reset; solid midpoint/occupied fallback; outlined numeral/cache/picking. |

All25 detailed case names/states are retained in `dense-final-focused-tests.json`. These do not prove art fidelity. The first Play submission returned no job ID and no_unity_session; it supports no acceptance claim. Foreign Hub jobs, including c6f21677d92c4125bb1b155417f72ea0 and a5438da8a2234694b77ddb7ed16dbd26, were allowed to finish and are excluded from Ice test totals. No foreign work was cancelled.

## Actual pixels and failure

- `dense-final-runtime-portrait.png` and `dense-final-runtime-20x9.png`: actual Game ScreenCapture with UI Toolkit,1080×1920/1080×2400. Disposable8×8 fixture: square4, narrow1×2 count6, tall1×4 count10, concave1, ring20, plain Red. Dense cyan bodies, quiet texture, coverage, placement, two-digit10 fit and ring hole work. Frozen base colors are covered as intended.
- **FAIL against supplied reference:** numerals render as thin regular-looking sans rather than chunky bold forms; stud/perimeter rings remain narrow white outlines rather than the broader pale cyan bands in the target. Task owner and independent visual reviewer agreed. The verifier's pixel inspection concurs. No global visual PASS is claimed.
- `dense-final-half-clipped.png`: valid diagnostic pixels show left square frost removed and Red base exposed; right half remains frozen. MPB readback before/after: ClipEnabled1, ClipPlane(1,0,0,3). This proves current clipping only, not completed thaw/zero-opacity reference acceptance.
- `dense-final-preview-user-before-font-rim-fix-restored.png`: valid current user Preview, count1 and same font/rim deficiencies. Other attempted Preview filenames were rejected because the host was absent while late Play-exit restoration returned the Game scene; those filenames support no evidence.
- Further Preview shape/aspect/Arrowhead checks, narrow20, actual thaw/zero-alpha pixels were stopped for repair after the confirmed art failure. They remain pending on the next revision.

Game capture used a fresh live ready SceneContext with nonnull Container and actual Game collaborators. Its disposable database and parentless resolver supplied one learned catalog; no authored JSON was changed. The separate real Game test validates loader/lifecycle behavior. No new framework or Assets test helper was written.

## Font readback

Read from the restored shared Preview label: inline and resolved fontStyle=Bold; resolved font and FontAsset=null; family/style=null. Panel=`BoardOverlayPanelSettings`, textSettings=null, theme=`UnityDefaultRuntimeTheme`; resolved fontSize130.85437, outlineWidth4.36181259. This points to default/inherited font rendering despite the explicit Bold style; it is diagnostic evidence, not an assertion of the exact internal font fallback. The earlier runtime query failed compilation due an unqualified Q extension; the corrected static UQueryExtensions query succeeded on Preview.

## Source/material identity

All seven hashes independently matched at release:

| File | SHA256 |
| --- | --- |
| Editor/BoardKit/BoardIceAssets.cs | e4999f137f0fdb6e505f87fa77e9ea65756fef882e09d3ce44082dfca0ea3d56 |
| Presentation/Board/BlockIceOverlay.cs | b7d19a2e16e59d123ac9640e0fb33b626ab3981cbcd868f53feb07687deba513 |
| Presentation/Board/IceCounterLabels.cs | d0a5461ccbeafc774378ab0fa84be17e7be1b777c28e2f33ebf1890f91af7711 |
| Tests/EditMode/IceAuthoringTests.cs | 078087cade94b531bcbac462a1959bacf370c3fdd87c8fe91758470647719166 |
| Tests/PlayMode/BlockIcePresentationTests.cs | 2d6abfa765d1b53926278535b6177c9f8cf27bd5e3d339e9018af1b2b8378ac3 |
| Art/Shaders/Ice.shadergraph | 1c665413573abd07bf28482b0a30177369fa97fe84427294488dba956dfa2744 |
| Art/Materials/Effects/MAT_Ice.mat | 2844290066bd00c195b1aaa58cc75d80a6db2a50ca91dcf195c92568e50d68da |

Paths in this table are under Assets/ColorBlockJam. Live material opacity=.98, tint(.22,.8,1,1), supplied icetexture.jpg reference, effective importer max1024, ShaderHasError=false. Current Onity is the authorized external v0.4.0.

## Preservation and release

Fresh before12:01:18.8654926Z and after12:19:59.6250799Z observations are retained with full JSON in `dense-final-editor-snapshot.json`; exact draft equality=true. Latest level-01 remains b1LightBlue Square(2,2), b2Pink Square(0,1) Ice1, unsaved=true. No earlier b1(2,3) or historical clean baseline was restored.

Final actual state: tests/play/compile=false; clean LevelEditor; GameView preset28; live Preview host restored through its existing publication method; owned runtime database destroyed; owned clipping removed with fixture return; throttle captured=false and ownership flag cleared. User window focus restored. An initial early scene restore was overwritten by late Play-exit scene restoration; the final explicit settled restore/readback confirmed LevelEditor.

All13 baseline-protected raw file hashes match, including supplied texture, level JSON/asset/meta, shape library and piece catalog files, and LevelEditor.unity. No shared PanelSettings was modified. No production/test/serialized/package/lifecycle/VCS writes by this role. Editor slot released before this evidence-only report was written. Next work belongs to the bounded font/rim repair, followed by fresh visual verification.
