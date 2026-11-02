# TASK-20260927-215425472-f5013490-home-currency-holders-authored — Home currency holders authored in Home.uxml

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260927-215425472-f5013490-home-currency-holders-authored
- Supersedes: none
- Title: Home currency holders authored in Home.uxml
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026@efurk64@unity (Plastic workspace, shared live Editor)
- Base revision: cs:13
- Task kind: UI
- Area: Hub Home top bar currency holders (`Hub/UI/Home.uxml`, `Hub/Home/HomeView.cs`, `Hub/Home/HomePresenter.cs`)
- Workflow: direct

## Objective and baseline

- Observable objective: The life and coin holders of the Home top bar are visible in the Editor and in UI Builder without entering Play Mode, because they are authored in `Home.uxml`. At runtime they show the committed balances from the currency catalog and the save service, as today.
- Current behavior/evidence: user screenshot 2026-09-27 ("üst taraftaki para ve can playmode da geliyor bunun yerine direkt editöre yerleştiriliebilir"). `Home.uxml` has an empty `currency-holders` element. `HomeView.AddCurrencyHolder` instantiates `CurrencyHolder.uxml` once per catalog currency with Show On Home (`HomePresenter` line ~125, `IHomeView.AddCurrencyHolder`). So the holders exist only in Play Mode, and `ClearCurrencyHolders` removes them on unbind.
- Expected behavior:
  - `Home.uxml` places one `CurrencyHolder` template instance per Home currency under `currency-holders`, in top-bar order (Life, then Coin). Each is named from its currency ID (for example `currency-holder-life`, `currency-holder-coin`) and carries design-time preview text ("Full" with 5 on the heart; "0") and its icon, so the Editor and UI Builder show the finished top bar.
  - At runtime, `HomePresenter` binds each Show On Home catalog currency to the authored holder with its ID through `IHomeView`. It keeps the existing display rules: Amount with `CurrencyText.FormatCompact`, CountOnIcon with the Full label. The currency definition's icon and values override the preview.
  - A Show On Home currency without an authored holder logs one designer-language error naming the currency ID and `Home.uxml`, and is skipped. An authored holder whose currency is missing or not Show On Home is hidden.
  - Unbind leaves the authored holders in place and detaches only their bindings; nothing is instantiated or removed at runtime.
  - Adding a currency to Home becomes: a new asset, a catalog entry, and a holder in `Home.uxml`. That is data only, with no code change.
- Behavior that must remain unchanged: holder look and positions (pill, icon, count on the heart, plus badge, press feedback), safe-area behaviour, the tab bar, route and Level button, Settings and popups, the currency catalog and save service, and publish-after-commit updates (DEC-92, DEC-128).

## Ownership

- Owner/writer role: Hub-and-player-data session implementer, then an independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Hub/UI/Home.uxml
  - Assets/ColorBlockJam/Hub/UI/Home.uss
  - Assets/ColorBlockJam/Hub/UI/CurrencyHolder.uxml
  - Assets/ColorBlockJam/Hub/Home/HomeView.cs
  - Assets/ColorBlockJam/Hub/Home/IHomeView.cs
  - Assets/ColorBlockJam/Hub/Home/HomePresenter.cs
  - Assets/ColorBlockJam/Hub/Composition/HubScreen.cs (only if the holder template is no longer passed)
  - Assets/ColorBlockJam/Tests/Hub/**
  - docs/Hub-And-Player-Data-Design.md
  - docs/verification/hub-home/**
  - PROJECT.md
  - production/tasks/TASK-20260927-215425472-f5013490-home-currency-holders-authored/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Art/UI/**
  - Assets/ColorBlockJam/Runtime/Currencies/**
  - Assets/ColorBlockJam/Player/**
  - Assets/ColorBlockJam/Data/Currencies/**
  - Assets/ColorBlockJam/UI/**
  - Assets/ColorBlockJam/Gameplay/**
  - Assets/ColorBlockJam/Scenes/**
  - Runtime instantiation of holders or a second (generated) holder path
  - Plastic checkin, shelve or publishing
- Serialized assets: none (UXML/USS text assets only)
- Other active writers/worktrees checked: 2026-09-27 21:54 UTC. The final verification pass of TASK-20260927-160751656-0e93d47c-home-tab-bar-locked-ends and TASK-20260926-235915699-78356740-hub-pre-level-launch (this session) is capturing Home now; TASK-20260927-004448215-54655902-booster-gameplay-flow owns Gameplay paths only.
- Execution dependency: TASK-20260927-160751656-0e93d47c-home-tab-bar-locked-ends (stable handoff: its final verification pass finished, so Home.uxml is not being captured or edited)
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given the Hub scene open in the Editor without Play Mode (and Home.uxml in UI Builder), when viewed, then the top bar shows the life holder (Full, heart with 5, plus badge) and the coin holder (0, coin, plus badge) at their runtime positions.
2. Given Play Mode with committed balances (for example 1520 coins, 5 lives), when Home binds, then the authored holders show "1.52k" and Full/5 exactly as before, no holder is instantiated or duplicated, and a balance change after a commit updates the text.
3. Given a Show On Home catalog currency with no authored holder, when Home binds, then one designer-language error names the currency ID and Home.uxml and Home still works; given an authored holder for a currency that is missing or hidden, then that holder is hidden.
4. Given Home is unbound and bound again (Hub reopened), when bound, then the same authored holders are reused, bindings are disposed on unbind, and no holder is left duplicated or removed.
5. Given fresh captures at 1080 x 1920 and 1080 x 2400-notch, when compared with the approved Home, then they are pixel-identical in the top bar (apart from values under test).
6. Given the Hub EditMode and PlayMode suites and SceneFlowSmokeTests, when run, then all pass with the holder tests updated.
7. Given PROJECT.md and the design doc sections 3.3 and 7, when read, then a decision row (next free DEC number) records that Home's holders are authored in Home.uxml, bound by currency ID, and that adding a Home currency includes a Home.uxml holder.

## References and constraints

- Story/GDD/ADR/design/log/image: user screenshot C:/Users/e-fur/AppData/Local/Temp/claude/C--Users-e-fur-Documents-Repos-ColorBlockJamCase2026/9eedf9d4-2bb5-45f8-8e12-60d163432d44/images/9.png; docs/media/color-block-jam/user-references/06-home-tabs.png; PROJECT.md DEC-128, DEC-132, DEC-133; docs/Hub-And-Player-Data-Design.md sections 3.3 and 7
- Unity version/platform: Unity 2022.3.62f2; Android and iOS portrait
- Package/architecture constraints: UI Toolkit UXML template instances; presenter stays plain C#; zero allocation on balance updates beyond the existing compact formatting; no runtime instantiation
- Explicit out of scope: a new currency, holder art changes, plus-badge behaviour (still press feedback only)

## Verification and risk

- Focused test path/command: MCP `run_tests` EditMode `ColorBlockJam.Hub.EditModeTests` (HomePresenterTests), PlayMode `ColorBlockJam.Hub.PlayModeTests` and group `SceneFlowSmokeTests`
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: out-of-Editor compile of ColorBlockJam.Hub and the Hub test assemblies; no UXML/USS warnings
- EditMode: HomePresenterTests cover binding by ID, missing holder error, hidden holder, rebind
- PlayMode: Hub suites green; holders not duplicated after reopen
- Manual/built-player/profiler/device evidence: an Editor (edit mode) Game-view or UI Builder capture of the top bar; Home captures; Opus 5.5 chief review not required unless the top bar look changes
- Risk: low
- Approval already granted (exact target only): the user's request of 2026-09-27 for this change
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: independent verifier, then automatic closure

## Stop conditions

- The tab bar task's final verification pass is still running.
- The change would need a Runtime, Player, currency data or art change.
- The top bar look or positions change.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: waiting
- Current phase: verification
- Last cycle verdict: BLOCKED
- Contract fingerprint: sha256:c860b887111679290a321348d869d02111b6371ff9e95ee9a6ced799c6cf46d6
- Evidence fingerprint: sha256:a0eeeceb20f4b55314fdccee1457445f903917b8b052f8c61c453508790809a7
- Reviewed revision: vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:f1fecf0a8c441166693dbc87e0d4240cbb9e5e00ad8c5bd7db9f96190702c595
- Attempt count: 2
- Last feedback: User requests correction of the Home UI Builder preview shown in codex-clipboard-84d45288-3604-4bf7-9566-44b1b932eccc.png, and permits inspection of all UI templates.
- Blocker: Historical top-bar pixel baseline is stale; one unchanged legacy purchase expectation fails in the full Hub EditMode gate.
- Next action: Reconcile the historical capture baseline and legacy purchase acceptance before claiming original-task readiness; D-001 is verified.
- Closed revision: none
- Closed at: none
- Closure mode: none
- Closed by: none
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated+manual: Home Builder1080x1920 clean; aligned labels/font/button resets; 14-document panel check PASS | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:f1fecf0a8c441166693dbc87e0d4240cbb9e5e00ad8c5bd7db9f96190702c595 |
| AC-2 | PASS | automated: HomePresenterTests22/22 including saved balances and authored binding; Hub runtime17pass | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:f1fecf0a8c441166693dbc87e0d4240cbb9e5e00ad8c5bd7db9f96190702c595 |
| AC-3 | PASS | automated: HomePresenter missing-holder/unclaimed-holder cases PASS | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:f1fecf0a8c441166693dbc87e0d4240cbb9e5e00ad8c5bd7db9f96190702c595 |
| AC-4 | PASS | automated: HomePresenter rebind tests PASS; Hub runtime17pass | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:f1fecf0a8c441166693dbc87e0d4240cbb9e5e00ad8c5bd7db9f96190702c595 |
| AC-5 | UNPROVEN | manual: fresh RenderTexture captures valid; historical top-bar equality unproven because baseline predates existing art/padding; verification/preview-repair.md | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:f1fecf0a8c441166693dbc87e0d4240cbb9e5e00ad8c5bd7db9f96190702c595 |
| AC-6 | FAIL | automated: focused UI45pass, Hub EditMode103/104 (unchanged purchase assertion FAIL); Hub runtime17 and SceneFlow6pass; full original gate not met | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:f1fecf0a8c441166693dbc87e0d4240cbb9e5e00ad8c5bd7db9f96190702c595 |
| AC-7 | PASS | manual: PROJECT.md DEC143 and design sections3.3/7 read back; authored holder authority preserved | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:f1fecf0a8c441166693dbc87e0d4240cbb9e5e00ad8c5bd7db9f96190702c595 |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| [D-001](defects/D-001.md) | ac_failure:ac-1:authored-home-controls-unstyled-in-builder | VERIFIED | AC_FAILURE | AC-1 | Standalone Home lost shared styling. | Independent Builder/panel and runtime verification; defects/D-001.md and verification/preview-repair.md. | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:f1fecf0a8c441166693dbc87e0d4240cbb9e5e00ad8c5bd7db9f96190702c595 |

### Transition history

- 2026-09-27 — Created — planning — contract authored from the user's request to place the Home currency holders in the editor; queued behind the running final verification pass
- 2026-09-29T22:43Z — Reopened — implementation — task-bug resolved AC-1's UI Builder appearance failure to this task; task-cycle recorded D-001 under the lifecycle lock and began one scoped repair. Current asset paths follow DEC-157; runtime layout and presenter behavior are preserved. Close authority: explicit user.
- 2026-09-28 — Started — implementation — user "devam et"; staging only (outside Assets) until the final verification pass releases Home.uxml; unity-cli READY WITH WARNINGS (shared live Editor); unity-game-dev loaded; lock be164253…
- 2026-09-28 — Staged — implementation — scratchpad/stage-holders: Home.uxml authors currency-holder-life and currency-holder-coin (CurrencyHolder instances with preview overrides), Home.uss edit-time icons, IHomeView TryGetCurrencyHolder/HideCurrencyHolders, HomeView binds authored holders by ID, HomePresenter logs one designer error for a missing holder, HubScreen drops the unused template field (Hub.unity keeps an orphaned serialized value until its next save; scenes are outside this task), tests updated (4 new EditMode cases); ColorBlockJam.Hub, Hub.EditModeTests and Hub.PlayModeTests compile out of Editor; DEC-143 staged
- 2026-09-28 — Verified and closed — closed — landing verification wf_438f6bbc-06a: all 7 criteria PASS; Opus 5.5 chief review (C:/Users/e-fur/AppData/Local/Temp/claude/C--Users-e-fur-Documents-Repos-ColorBlockJamCase2026/9eedf9d4-2bb5-45f8-8e12-60d163432d44/tasks/wsy4wjrwt.output): CHIEF VERDICT: APPROVE; TASK_CLOSED automatically
- Verified D-001 — independent standalone styling and runtime checks pass. Original AC-5 remains unproven against an obsolete image baseline; AC-6 retains one pre-existing purchase assertion failure. Task waits for those legacy acceptance gaps; no automatic closure.
<!-- TASK-LIFECYCLE:END -->
