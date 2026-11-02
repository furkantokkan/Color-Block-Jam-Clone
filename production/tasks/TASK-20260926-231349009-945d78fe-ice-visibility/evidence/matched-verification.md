# Dense reference match: attempt 2 checkpoint

Independent visual verdict: **FAIL; further shader refinement required.** The larger filled numerals are substantially closer to `dense-reference-target.png`, including 4 and readable 10/20. The stud rings remain narrow bright white instead of broad pale mint/cyan, and the vertical lower band remains almost white instead of saturated cyan with a pale bevel. Root independently inspected and rejected this revision before the longer test matrix.

## Actual pixel evidence

- `matched-final-runtime-portrait.png`: actual Game ScreenCapture, UI included, 1080×1920. Counts 4/6/10/1/20 plus narrow single-cell 20; square, 1×2, 1×4, concave and holed ring. Ring hole remains open. Both-digit labels are visible; the single-cell 20 extends beyond its cell footprint.
- `matched-final-runtime-square4-closeup.png`: actual Game closeup using temporary runtime camera framing. Clearly shows heavier 4, narrow white rings, and pale lower skirt.
- These are valid **before attempt 3** images, not accepted final reference matches. No new Preview/tall/clipping/zero-alpha acceptance images were produced.

## Scope and tests

No tests were started and no job ID exists for this attempt. The previous 25 passes remain historical pre-refinement evidence in `dense-verification.md`; changed shader/counter inputs require fresh focused verification after the next stable handoff.

The disposable 8×8 fixture used the existing parentless Onity resolver pattern, binding a single learned catalog and actual Game EnvironmentView, effect source, session and presenter. No authored database was modified. A Play domain transition left the initial SceneContext ready with a null container and no ProjectContext. Fresh `SceneManager.LoadSceneAsync("Game", Single)` created a live container; real collaborators resolved and fixture session entered Playing with six pieces, but scope IsReady remained false. Thus these images prove rendering with live Game collaborators, not ordinary startup readiness. The real Game integration test remains pending.

## Readiness and preservation

Unity CLI 1.0.0-beta.11; exact project Unity 2022.3.62f2, PID45732. Discovery/registry session initially matched `3c132615-9dbc-4412-82b5-549b6f2ff7a0`. Transport disconnected during pinning and Play reload; fresh minimal project/state readbacks recovered it. An OpenScene response returned false/null despite the scene opening; actual readback confirmed completion before any next transition. No mutation was duplicated and no foreign run was cancelled.

Latest before snapshot: 12:51:50.7255011Z. Cleanup snapshot: 13:01:53.5316394Z. Full draft JSON is identical, with b1 LightBlue Square at (2,2), b2 Pink Square at (0,1), Ice1, unsaved=true. Details: `matched-final-editor-snapshot.json`.

At release: tests/play/compile=false; clean LevelEditor scene; preset28 restored; live Preview host restored using the newest draft; user window focused. Fixture went Home, its temporary database was destroyed, its SessionState handle cleared, and Play stopped. No throttle helper was used. No panel/theme, source, test, serialized asset, lifecycle or VCS changes were made. All13 baseline-protected hashes match, including supplied texture bytes and LevelEditor scene.

## Independently checked revision hashes

Paths below are relative to `Assets/ColorBlockJam`.

| Input | SHA256 |
| --- | --- |
| Editor/BoardKit/BoardIceAssets.cs | f19a049bdca20727172b82ab52683e218b8a7656ea0d098a3fba938a277c48cc |
| Presentation/Board/BlockIceOverlay.cs | b7d19a2e16e59d123ac9640e0fb33b626ab3981cbcd868f53feb07687deba513 |
| Presentation/Board/IceCounterLabels.cs | 791db2d1c5ab899aef9155fbc50fad0710a33b1c7d758aa919e4762ba8f98a4d |
| Tests/EditMode/IceAuthoringTests.cs | 078087cade94b531bcbac462a1959bacf370c3fdd87c8fe91758470647719166 |
| Tests/PlayMode/BlockIcePresentationTests.cs | 5dd66988ac0db13c5e52230dd3170e357181f95dd18876c9eca44cb0006ab139 |
| Art/Shaders/Ice.shadergraph | 073be7d88549c4bbc304fc820ff684d065e4c0f4bae63c5606116fd48e27ca1e |
| Art/Materials/Effects/MAT_Ice.mat | 2844290066bd00c195b1aaa58cc75d80a6db2a50ca91dcf195c92568e50d68da |

Editor ownership explicitly released to root before this report was written. Root owns defect/lifecycle handling; this checkpoint does not close or verify D-008.
