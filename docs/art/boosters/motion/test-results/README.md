# Booster motion verification

- Date: 2026-09-24. Unity 2022.3.62f2; PrimeTween 1.4.11.
- Pinned Editor: `ColorBlockJamCase2026@99077e4df1ecfaa4`.
- Assembly: `ColorBlockJam.Presentation.PlayModeTests`.
- Result: **10 passed, 0 failed, 0 skipped**, 4.4702038 seconds of test execution.
- Job: `1b8221dd9b2c45b5a6b61d64433259fb`; complete tool output: [playmode-job.json](playmode-job.json).
- CLI beta.11 verified; live tests used the authorized MCP for Unity transport because this project is below Unity 6.

The tests instantiate the actual Hammer, RocketPurple and Vacuum effect prefabs in PlayMode. They verify idle startup, real-time progression and completion, pause stability beyond the effect duration, explicit resume, replay without duplicate target tweens, rest-pose reset, disable cleanup, and application-pause latching. Exact preview samples verify hammer striking-face contact and rocket tip contact with a translated, rotated and scaled root. Invalid rocket destinations preserve the active flight.

Vacuum checks cover real-time frame changes, 16 distinct sprites sharing one 1024 x 1024 non-readable texture, one renderer and shared material across instances, a valid intake point, and no runtime Animator, Animation, SkinnedMeshRenderer or camera.

No production or serialized scene assets were changed by the verifier. These are presentation checks; they do not claim end-to-end gameplay integration, device performance, allocation profiling or IL2CPP validation. One pre-existing console entry (`You may not pass in objects that are already persistent`) was present before and after this run; no new compilation errors or test failures appeared.

## Tested source fingerprints (SHA-256)

| File | SHA-256 |
| --- | --- |
| BoosterEffectView.cs | `15007B9C644F62241B84CD5F9BC7F7D2E11FA8E52868BDB651CE46BF76EF9DA4` |
| HammerEffectView.cs | `FB3CD914F0AB618338C4700DA7742956BD8AD3F3FACFE25FD9BB896817F99488` |
| RocketEffectView.cs | `A37CF96DCDDB725B3E7263DE5D4C882C4E5E30703920C1FFBD4E446FE21951C5` |
| VacuumEffectView.cs | `FBB737D6E9030AB44FA452B0C36D8D98F5DFE46FFB02C07137E49989D0005782` |
| HammerEffect.prefab | `1F8F5584C5F1472825D1520BE429D034174721E7110708B3654B3EFD14F6EDA6` |
| RocketPurpleEffect.prefab | `18C5B9A4D22330BF6045DEE38554087DC63018DD919ED2FE95F4149E1CAA5DB1` |
| VacuumEffect.prefab | `31C6E776A7D7AADB4CF5FA0C58BBE327E29FF675DC0CE26521D925C6BB9A6421` |

Tests live in `Assets/ColorBlockJam/Tests/Presentation/PlayMode/BoosterEffectViewTests.cs`. Run the named test assembly in the Unity Test Runner's PlayMode tab. Asset-loading checks are Editor-only and intentionally excluded from player test builds.

## Upright vacuum correction regression

- Date: 2026-09-24, 13:14:14–13:14:18 UTC. Same pinned Unity 2022.3.62f2 Editor; MCP HTTP transport at port 8081.
- Result: **11 passed, 0 failed, 0 skipped**, exact NUnit duration **4.3381633 seconds**.
- Job: `8810067fefc14fa59850468ea4072818`; [raw MCP status](vacuum-correction-job.json), [Unity NUnit report](vacuum-correction-results.xml).
- Added `VacuumIntakeStaysCenteredAboveBodyAcrossFramesAndRootTransforms`; preserved the original ten tests. It checks the intake lies near the sprite's horizontal center in the upper half, and remains fixed across twelve full-scale frames at both identity and translated/rotated/nonuniformly scaled root placements. The old lower floor-head intake fails the upper-nozzle assertion.
- The MCP job finished successfully with all eleven tests completed and no failures, but its detailed result payload was null. The matching Unity-generated NUnit report supplies the exact durations and individual passed results.
- Zero console errors after this run. `SampleScene` remained loaded and clean (`isDirty=false`). Runtime C# and Hammer/Rocket prefab SHA-256 values still match the original table; corrected `VacuumEffect.prefab` SHA-256 is `70D886CF637695DB1654E9941BC86089E7633BEAD75CB2D9E5DFFB72F47C1AAA`.
- Verifier edits are limited to the test source and these evidence files; no production assets or code were changed during verification. The test checks the authored intake anchor; visual nozzle alignment remains covered by the separate render review.
