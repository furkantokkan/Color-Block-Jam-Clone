# Dense counter verification checkpoint

2026-09-27. Numeral-only source handoff; verification is incomplete. This checkpoint does not claim a full visual or acceptance pass.

- Independently read SHA256 values match the handoff:
  - `Assets/ColorBlockJam/Presentation/Board/IceCounterLabels.cs`: `d0a5461ccbeafc774378ab0fa84be17e7be1b777c28e2f33ebf1890f91af7711`.
  - `Assets/ColorBlockJam/Tests/PlayMode/BlockIcePresentationTests.cs`: `2d6abfa765d1b53926278535b6177c9f8cf27bd5e3d339e9018af1b2b8378ac3`.
- The implementation owner's `dense-calibration-numeral-only.png` was manually inspected by the task owner and separate visual reviewer: count1 is centered, pale cyan and dark outlined, approximately100/165=.61 of projected cell height. This verifier did not generate that image. It proves only the current Preview count1 numeral appearance.
- The graph/material remain the old opacity0.9 version. Applying the dense cyan refinement to the two existing assets is pending exact user approval after automatic approval rejected that mutation. Old frost appearance is not accepted against the new reference.

## Readiness observations

Unity CLI1.0.0-beta.11 and repository Unity2022.3.62f2 were verified. CLI status had no Pipeline instances, as expected for the pre-Unity6 version gate.

1. Discovery and targeted registry registration agreed on session `9754599c-e46e-43b0-817e-b17a15e6f2fd`; pinning then returned no connected Unity instances.
2. One bounded retry matched registry/discovery session `7b5d04bd-465a-4c97-813a-511f58cf8011`. Read-only process evidence confirmed PID45732, the exact repository path and Unity2022.3.62f2 executable. Explicit pin and project-info readback succeeded.
3. The following read-only draft/readiness snapshot returned null during another reload. Discovery then showed session `20885dca-a06e-4e43-a35d-be5c6204de34`, connected11:26:00Z. Live verification stopped rather than relying on stale identity.

No tests started, no job exists for this checkpoint, no verifier captures were requested, and no Editor/asset mutations occurred. The fresh draft snapshot did not complete; this verifier made no attempt to restore any old draft or change the user's state. Shell execution required the authorized read-only route because the workspace ACL helper failed; raw process command lines were never printed.

## Pending verification

- Four focused BlockIcePresentationTests: outlined numeral/cache/picking, solid midpoint/occupied fallback, attached-panel centering/projected-cell scaling, and unchanged shell pooling/reset behavior.
- Actual narrow-block counts10/20 and broader counts1/4/6/10/20 on square, concave and holed footprints.
- Actual Game and Preview comparison at1080×1920 and1080×2400, including readable numerals and unobscured Arrowheads.
- Fresh draft/preset/full13-input preservation checks around those eventual transitions.
- IceAuthoring/material verification after approved graph/material regeneration; opacity0.98 expectation must not run against the still-old0.9 material.

Only this evidence report was written. No production/test/serialized/lifecycle/VCS files changed. Editor verification slot released to the task owner; no further Unity calls planned until a stable handoff.
