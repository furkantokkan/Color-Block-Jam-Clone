# Purchase popup verification — 2026-09-30

- Scope: independent sequential verifier; changed only `Assets/ColorBlockJam/Tests/UI/EditMode/BoosterPurchasePopupAssetTests.cs` and this evidence. No production fixes, serialized changes, lifecycle changes, or VCS mutations.
- Source changes: four panel-attached EditMode tests; runtime offer values now differ from authored samples. Preserved the prior border, message/button, hierarchy, and assembly-boundary cases. Temporary Editor windows are closed in `finally`.
- Compilation: official local Roslyn compile checker, Unity 2022.3.62f2, `ColorBlockJam.UI.EditModeTests`: `COMPILE_OK`, 1.0 seconds. Repeated after correcting one assertion that compared `Color` with a boxed `Color32`; the corrected expected value is explicitly cast to `Color`.

## Automated evidence

- MCP job `f0ba0b736c064aeea049d702974add69`: 7/7 old cases passed. **Stale; excluded from acceptance.** Unrelated Gameplay tests temporarily failed to compile because seven call sites omitted `LevelSession.finishDeadline`. Reflection confirmed the four new tests were absent then. The foreign errors subsequently cleared; the verifier changed none of those files.
- One current-suite start disconnected before receiving a job ID. Fresh identity and idle/no-active-tests were confirmed before retrying; no duplicate run was started.
- Current-source MCP job `11f08a4b2d4a48dba50a0f826c5d07e1`: all 11 cases completed. Its uncapped failure list contained only `StandalonePanel_ShowsStyledSampleWithoutRuntimeBinding`, caused by the verifier's boxed color comparison above. The other ten cases had no reported failure. The MCP job exposed progress/failures but returned `result: null`, so a complete per-case result summary is unavailable for this run.
- Failed-only rerun job `0c0fc4dec80b45b9a8891ba520a0bda7`: **1/1 passed**, 0 failed, 0 skipped; runner duration 0.856 seconds. Standalone test duration 0.290 seconds. Production source was unchanged between these runs; only the faulty color assertion changed. No remaining test failure is reported across the focused suite and its failed-only rerun.
- Coverage: standalone display and 1080 x 1920 layout; Common heavy-label styles; runtime font and sample booster/currency icons; matching text layers; all three unbound Hub/Game purchase roots hidden with no inline runtime assignment; hidden state after detach/re-attach; constructor hides a visible standalone popup; runtime offers replace title/amount/description/price and clear null icons; inline opening overrides host CSS; closing/disposal hides; existing message/button/border/boundary behavior.

## Actual UI Builder evidence

- Safely opened the real `Assets/ColorBlockJam/UI/Purchase/BoosterPurchasePopup.uxml` after checking both the Builder window and document had no unsaved changes. A reload interrupted the first inspection and removed the Builder window; a later clean, idle attempt succeeded. Existing other Editor windows were preserved.
- Fresh resolved readback: document path is the actual purchase UXML; both unsaved flags `False`; canvas width `1080`, height `1920`; screen `DisplayStyle.Flex`, layout `(0, 0, 1080, 1920)`; popup layout approximately `(106.67, 409.33, 866, 1102)`.
- Title: `Rocket`, font asset `LiberationSans-UITK`, size `76`. Both text layers match for Rocket, x1, description, and price 10.
- Resolved icon paths: `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Boosters/ic_rocket_purple.png` and `Assets/ColorBlockJam/Art/UI/Supplied/Icons/Currency/ic_coin_single.png`.
- Left the actual purchase document open and clean for artist editing. No Builder save, canvas change, or Library/UIBuilder file edit. Init scene remained clean and Play Mode was never entered. No screenshot was captured; direct Builder document/style/layout readback is the manual evidence.

## Identity, review, and limits

- CLI `1.0.0-beta.11`; repository requires Unity `2022.3.62f2`, so the approved pre-Unity-6 unityMCP transport was used. Full instance `ColorBlockJamCase2026@99077e4df1ecfaa4` was pinned after websocket/process proof, with exact root/version project-info readback after reconnects. Unity PID `48360`, binary `C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Unity.exe`, exact project root. Latest proved plugin session `a1d25335-6ede-4133-82d1-6071cb92a49a` maps log websocket port `65012` to that PID.
- Independent review found no production defect: standalone styles remain authored; named host-instance selectors suppress unbound samples; runtime inline styles override them; sample fields are replaced, including null icons; purchase/economy code is untouched.
- AC-4 read-only review: project/global AGENTS, PROJECT, and both owned local Unity skills contain the modular authoring/sample/runtime initialization rules.
- Console contains unrelated earlier Home gem-holder, WebSocket/import/test-cleanup logs and foreign compiler warnings. No current purchase compile error or remaining focused-test failure was identified. No device/build/PlayMode run was required for this authoring-only scope.

## Observed source SHA-256

- Purchase UXML: `0A9AD6D54AAECF95B61C7184A22A13D212E4F1C7ACB0AE025BB7048BC6EEDA63`
- Purchase USS: `39193CAAB8010A37101D6ED6881976EF1DB13D1C1D2909FA7527A4EF88809B55`
- Purchase view: `81F997919732617125BB899ACE4B9442B6773D07AFA58D24FCA713AE830B3DF5`
- Final test file: `0135FB0DD5EB6436272E0D677031C9BA8B71761DE40F73E3CF77FEB4694317BE`

Next owner: parent implement-task owner reviews this evidence and final identities for readiness. Closure authority remains explicit user only.

## Parent verification

- Re-read the two retained test jobs after fresh websocket/process/session proof
  and exact project-info readback. The full suite has `completed: 11`,
  `total: 11`, only the corrected color-assertion failure, and
  `failures_capped: false`. Its failed-only rerun has `status: succeeded`,
  `completed: 1`, `total: 1`, and no failures. Detailed results remain absent
  after subsequent reloads; do not report an available full per-case summary.
- Independently read the open real Builder preview: clean window, purchase
  screen `Flex`, rectangle `(0, 0, 1080, 1920)`, title `Rocket`, font
  `LiberationSans-UITK`; Editor neither compiling nor playing.
- Production view Roslyn compile check passed. Both local Unity skills passed
  `skill-creator/scripts/quick_validate.py`. Source diff against cs:38 is
  contained to the intended popup preview/startup changes; unrelated dirty
  work is preserved. Final acceptance uses the retained automated run/rerun
  together with direct Builder readback and independent source review.
