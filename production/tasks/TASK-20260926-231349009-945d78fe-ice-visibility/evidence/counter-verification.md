# Counter follow-up verification checkpoint

2026-09-27. **Superseded checkpoint, not visual acceptance.** User requested another reference-driven revision of both counter and frost appearance. Tests below passed, but new appearance must be independently evaluated after the revision. No production/test/assets/lifecycle writes by verifier.

- Final checkpoint IceCounterLabels.cs SHA256: `8b6759af424b8f9ec8f7ae84e54b9226e175011b36369db825e7ded937f659b0`. Final change after geometry tests only reversed snowflake forks outward; midpoint/projection/count logic unchanged.
- Stable BlockIcePresentationTests.cs SHA256: `6918aa1b2e88dcc5f625219588422c259deb1cf2086748348daae796dffb0503`.
- Repaired GameSceneIceTests.cs SHA256: `0c3787829aecb8a46fe7c0b401fd54a1f31b63e0c6c9656bc6e0f56fd723ff21`.
- Presentation PlayMode job `b6bb700b21d74f86b80b0d97af57d29d`: 4/4 PASS. Real attached panel test proves badge center at projected solid midpoint, top at anchor+8 panel pixels, visible icon geometry and stable position for counts 1/20. Other tests cover deterministic concave/hole fallback, cached strings/pointer ignoring, shell lifecycle regression.
- Preview EditMode job `14723701f1844bf18d329218ae31f6c3`: 1/1 PASS, Ice/Arrow coexistence and temporary view disposal.
- Real Game Ice PlayMode job `411a3ae5120b4218bf6cd72da73ad62e`: 1/1 FAIL. Inner InvalidOperationException: “The Game scene requires one project shape catalog.” Existing VerifyBoundLearnedCatalog creates a child of the scene resolver at source line154, then reflects Load at159; parent+child now aggregate two catalogs, rejected by GameAttemptLoader line120. Earlier drag/thaw/restart/failure assertions precede helper call at137; assertions after this helper are not claimed. No verifier fix applied; handed back for isolated test-resolver repair.

Actual inspected UI-inclusive GameView captures:

- `counter-preview-user-portrait.png`: latest live b2 Pink Ice1, square badge horizontally centered below midpoint, clear white snowflake and 1 on blue capsule. Frost/basecolor preserved.
- `counter-preview-shapes-portrait.png`: transient shared preview square1, concave20 and ring20; clear icon/two-digit text and Arrow coexistence. Solid square midpoint is continuous; concave/hole labels use occupied fallback rather than an empty hole.

## Repaired test and final checkpoint pixels

Repaired real Game Ice job `a3567b64b7714f84bcc43336683c0db5`: **1/1 PASS**, 1.665 s, including drag/thaw/restart/failure, warmed allocation assertions, isolated custom-catalog loading and intentional ambiguous-catalog rejection. It replaces the failed job above (D007 test helper repair); no loader production change.

Final outward-fork icon captures independently inspected by verifier and root:

- `counter-final-preview-user-portrait-1.png`: current user Pink square1, centered badge just below midpoint, clear snowflake/1.
- `counter-final-preview-shapes-20x9.png`: actual 1080×2400 shared preview square1/concave20/ring20, readable icons/digits and Arrow coexistence, ring hole open.

These are now BEFORE images for the renewed reference refinement. They demonstrate this checkpoint's projection/icon behavior, not acceptance of the user's newly clarified desired appearance. Earlier pre-fork images are historical only.

Runtime fixture capture was not proven: fresh active Game SceneContext instance84886 reported active/enabled/IsReady=true but Container=null, and ProjectContext.Instance=false. TestRunnerApi.IsRunActive=false; no capture fixture could resolve its actual collaborators. Cause was not established. No boot/Onity fix or reload workaround was attempted after user redirected to visual refinement. Blank runtime and early empty preview captures were deleted; no runtime pixel pass claimed.

Prior D001–D004 behavior evidence remains relevant to unchanged gameplay/shader dependencies at this checkpoint, reinforced by the fresh real Game test and preview shell/coexistence pixels. D004's supplied texture is still bound; renewed reference similarity is nevertheless unverified and must not be inferred from those older passes. Historical independent-verification.md was preserved unchanged.

## Final safe restoration

Play stopped; TestRunnerApi.IsRunActive=false. LevelEditor reopened only after clean Game scene and confirmed EditMode. Final live exact draft remains b1(2,3), b2Pink(0,1) Ice1, unsaved=true; LevelEditor clean; GameView28 restored; no owned temporary GameObjects. MAT_Ice.GetTexture(_IceTexture) resolves `Assets/ColorBlockJam/Art/Textures/icetexture.jpg`.

`counter-verifier-snapshot.json` records current relevant source hashes, external ProjectScopeInstaller/ProjectContext binding dependencies, and 13/13 protected baseline matches (zero diffs). `counter-editor-before.json` exact observation timestamp is unavailable; separately known file timestamp is recorded without a guessed live time. Editor slot released for reference refinement; root owns lifecycle.

Before/after live draft unchanged: level-01 b1(2,3), b2 Pink(0,1), Ice1, unsaved=true. Scene LevelEditor clean, Play stopped, GameView preset28 retained. Preview restored from current live GetDraftLevel, never historical snapshot. `counter-editor-before.json` stores draft; `counter-protected-before.json` proves all13 protected hashes match original baseline. Editor slot released to lifecycle owner for test-only repair. Runtime and 1080×2400 captures remain pending.
