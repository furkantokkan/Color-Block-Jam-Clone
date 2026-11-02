# Reference match attempt 3: partial independent verification

**Visual Game channels PASS; full verification pending.** Work stopped at the user's instruction to continue only after Hub completes. No focused tests were started, no job ID exists, and no checkin or lifecycle changes were made by the verifier.

| Channel | Result | Evidence / limitation |
| --- | --- | --- |
| Game 1080×1920 reference appearance | PASS | `reference-match3-final-runtime-portrait.png`: actual UI-inclusive Game pixels, independently opened. Broad mint/cyan rings, quiet cyan body, blue lower band and chunky outlined numerals. Root and independent visual reviewer also accepted this image. |
| Game 1080×2400 projection | PASS | `reference-match3-final-runtime-20x9.png`: independently opened; framed board and labels aligned to all six blocks. |
| Counts / footprints | PASS rendered channels | Both images show 1/4/6/10/ring20 and single-cell20, square, 1×2, 1×4, concave and holed ring. Ring hole stays open. Single-cell20 is tight but visible and contained. |
| Focused20 EditMode +5 PlayMode | NOT_RUN | User prioritized Hub before test submission. Counter10→9 and20→0→9 test execution remains required. Previous25 passes describe an earlier revision, not these changed inputs. |
| Current Preview both aspects / Arrow | NOT_RUN | Implementer's final Preview4 calibration was accepted separately by root; this verifier did not produce final current Preview fixture/Arrow evidence. |
| Current half-clipping / zero-alpha vs hidden | NOT_RUN | New shader needs fresh checks; historical images are not current proof. |
| Final square4 closeup | NOT_RUN | Optional capture skipped at user pause. |

The transient8×8 fixture used a parentless Onity resolver with a sole learned catalog and real Game EnvironmentView, effect source, session and presenter. No authored database was changed. Initial portrait scope was IsReady=true and Container!=null. An external forced synchronous recompile subsequently disposed the runtime container; the verifier reloaded Game at runtime, reacquired live collaborators, and recreated the disposable fixture for the tall capture. Its container was valid but IsReady=false, so the tall fixture is rendering evidence, not ordinary startup proof. Fresh real Game integration tests remain pending.

## Preservation and release

Before snapshot at13:26:45.4737221Z: exact unsaved draft retained, b1 LightBlue Square(2,2), b2 Pink Square(0,1)Ice1; preset28; clean LevelEditor. See `reference-match3-editor-snapshot.json` for full JSON.

Owned runtime fixture went Home; disposable database was destroyed and its SessionState handle cleared; Play stopped. The owned NoThrottle helper restored captured settings and its ownership flag was cleared. Final actual guard readback: compiling=false, Play=false, TestRunnerApi.IsRunActive=false, clean Game scene. All13 protected hashes match the original preservation baseline, including supplied texture bytes. No source, tests, serialized assets, panel/theme, lifecycle or VCS files were modified.

**Editor released to root/Hub. Scene and view restoration is DEFERRED by explicit scheduling instruction:** current known scene is clean Game; last known preset18 is1080×2400. Original LevelEditor/preset28 must be considered on resume only after inspecting the newest Hub/user state. Do not overwrite a later scene, preset or unsaved draft. Post-release draft was not reread; full before/after equality is therefore not claimed. No further Unity call followed release.

## Reviewed final hashes

Paths relative to `Assets/ColorBlockJam`; all seven independently matched at release.

| Input | SHA256 |
| --- | --- |
| Editor/BoardKit/BoardIceAssets.cs | f11db4f5d50d214c7bc446cd96fd1c3d2991e61f8bce3d193e9b40e1ea22bdaf |
| Presentation/Board/BlockIceOverlay.cs | b7d19a2e16e59d123ac9640e0fb33b626ab3981cbcd868f53feb07687deba513 |
| Presentation/Board/IceCounterLabels.cs | 69cb4aa2a027b2623ec0ddbc3370690329da9980f84b3beb9244c1ec13a0e556 |
| Tests/EditMode/IceAuthoringTests.cs | 078087cade94b531bcbac462a1959bacf370c3fdd87c8fe91758470647719166 |
| Tests/PlayMode/BlockIcePresentationTests.cs | d051d732be9de99d1ab2841b2b3359b300088472be8f2b64dec93e3621333254 |
| Art/Shaders/Ice.shadergraph | fab0c91231d68226f80572b396c84d2880be93ddf1bf51b4c43ca8fbbaedd904 |
| Art/Materials/Effects/MAT_Ice.mat | 9a27939c194a1a8b02a9eb238c46b8c3141891754c23e2a3d1c29cb694c242c4 |

Unity CLI1.0.0-beta.11; exact Unity2022.3.62f2/PID45732/project path proved. Pre-Unity6 approved unityMCP transport used. No foreign run cancelled. Historical `reference-final-*` and previous reports remain untouched; all new artifacts use `reference-match3-*`.
