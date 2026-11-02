# TASK-20260927-160751656-0e93d47c-home-tab-bar-locked-ends — Home tab bar ends become locked "Lvl 50" slots

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260927-160751656-0e93d47c-home-tab-bar-locked-ends
- Supersedes: none
- Title: Home tab bar ends become locked "Lvl 50" slots
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026@efurk64@unity (Plastic workspace, shared live Editor)
- Base revision: cs:13
- Task kind: UI
- Area: Hub Home tab bar (`Hub/UI/Home.uxml`, `Hub/UI/Home.uss`)
- Workflow: direct

## Objective and baseline

- Observable objective: The Home tab bar reads, left to right: a lock with no label, Shop (dimmed, locked, no label), the raised active Home with its "Home" label, Collection (dimmed, locked, no label), and a lock with "Lvl 50" under it, matching the user's reference crop (revised 2026-09-28).
- Current behavior/evidence: `Hub/UI/Home.uxml` lines 83-106 build five `tab cbj-pressable` buttons: `tab-leaderboard` (Leaderboard icon and label), `tab-shop` (`tab--locked`: dimmed icon plus `tab__lock` overlay, label "Shop"), `tab-home` (`tab--active`, raised on `ActiveTab`), `tab-collection` (locked like Shop), `tab-support` (Support icon and label). No tab navigates and each gives press feedback (AC-1.5, DEC-86, DEC-133). Captures: `docs/verification/hub-home/home-1080x1920.png` and the rest of that folder.
- Expected behavior: The leftmost and rightmost slots show only the supplied `Art/UI/Icons/Navigation/lock.png` as their main icon, at the size the other tab icons use. Only the rightmost slot has a label, "Lvl 50", with a white fill and a thin dark outline like the reference crop (thinner than the current tab label outline). The Leaderboard and Support icons disappear. No non-Home tab shows a label: the leftmost slot, Shop and Collection have none. Shop and Collection keep their dimmed icon and lock overlay. Home stays raised and active with its "Home" label unchanged. Every slot keeps its `.cbj-pressable` feedback and navigates nowhere. Element names may keep the reserved feature (for example `tab-leaderboard` and `tab-support`) or be renamed, as long as tests and the design doc agree. User feedback 2026-09-28: "home dışındakilerin outline'ı biraz fazla referanstaki gibi değil sadece sağda Lvl 50 yazmalı, home dışındakilerin altında başlık yazmasına gerek yok".
- Behavior that must remain unchanged: tab bar geometry (height, slot widths, dividers, the fill to the physical bottom edge, the safe-area padding of DEC-132), the raised Home tab, Shop and Collection appearance, every other Home control (top bar, route, Level button), Settings, the pre-level and purchase popups, player data.

## Ownership

- Owner/writer role: Hub-and-player-data session implementer, then an independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Hub/UI/Home.uxml
  - Assets/ColorBlockJam/Hub/UI/Home.uss
  - Assets/ColorBlockJam/Hub/Home/HomeView.cs (only if a tab label or element is set from code)
  - Assets/ColorBlockJam/Tests/Hub/**
  - docs/verification/hub-home/**
  - docs/Hub-And-Player-Data-Design.md
  - PROJECT.md
  - production/tasks/TASK-20260927-160751656-0e93d47c-home-tab-bar-locked-ends/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Art/UI/** (no new art, no import or border change)
  - Assets/ColorBlockJam/UI/**
  - Assets/ColorBlockJam/Hub/Composition/**
  - Assets/ColorBlockJam/Hub/PreLevel/**
  - Assets/ColorBlockJam/Hub/Purchase/**
  - Assets/ColorBlockJam/Hub/Launch/**
  - Assets/ColorBlockJam/Player/**
  - Assets/ColorBlockJam/Gameplay/**
  - Assets/ColorBlockJam/Scenes/**
  - Navigation from any tab, unlocking Shop or Collection, or level-based unlock logic
  - Plastic checkin, shelve or publishing
- Serialized assets: none
- Other active writers/worktrees checked: 2026-09-27 16:07 UTC. TASK-20260926-235915699-78356740-hub-pre-level-launch (this session, lock held) is implementing now and writes `Tests/Hub/**`, `Hub/UI/Hub.uxml`, `Hub/Composition/**` and Hub captures in the shared Editor. TASK-20260927-004448215-54655902-booster-gameplay-flow (other session) owns Gameplay paths only; no overlap.
- Execution dependency: TASK-20260926-235915699-78356740-hub-pre-level-launch (stable handoff: its Hub suites green and its lifecycle lock released)
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given Home at 1080 x 1920, when captured, then the tab bar shows left to right: a lock icon with no label, Shop (dimmed icon, lock overlay, no label), raised active Home with "Home", Collection (dimmed icon, lock overlay, no label), a lock icon with "Lvl 50" whose outline is as thin as the reference crop's; the end-slot lock icons are the size of a main tab icon; nothing is clipped; the icons stay where they were (vertical centring may change only if a label's removal requires it, and then consistently for all four non-Home slots).
2. Given 1080 x 2400 with the simulated notch (top 110, bottom 70), when captured, then the same tab bar sits inside the bottom safe area with its fill reaching the bottom edge, as before.
3. Given any of the five slots, when pressed and released, then it scales and darkens through `.cbj-pressable` and restores, and no navigation or overlay opens.
4. Given the Home screen outside the tab bar (top bar, route, Level button), when compared with the previous capture, then it is pixel-identical; the tab bar geometry (slot bounds, dividers, raised Home) is unchanged.
5. Given the Hub EditMode and PlayMode suites and `SceneFlowSmokeTests`, when run, then all pass with tests updated to the new end slots.
6. Given PROJECT.md and the design doc's Home section, when read, then a new decision row (next free DEC number after checking PROJECT.md and every contract; DEC-138 is reserved by the pre-level task) records the revision of DEC-133's end slots with the user's answers, and the Home section describes the new tab bar.
7. Given the fresh captures and the user's reference crop, when the chief visual validator (Opus 5.5 read-only reviewer agent, user order 2026-09-27) reviews them, then the verdict is APPROVE, or its remaining findings are fixed.

## References and constraints

- Story/GDD/ADR/design/log/image: user crop of reference 06's tab bar (C:/Users/e-fur/AppData/Local/Temp/claude/C--Users-e-fur-Documents-Repos-ColorBlockJamCase2026/9eedf9d4-2bb5-45f8-8e12-60d163432d44/images/4.png) and docs/media/color-block-jam/user-references/06-home-tabs.png; user answers 2026-09-27: Shop and Collection keep their locks ("Kilitli kalsın"), the left end also gets "Lvl 50" ("yazı olsun Lvl 50"); PROJECT.md DEC-86, DEC-132, DEC-133; docs/Hub-And-Player-Data-Design.md section 7
- Unity version/platform: Unity 2022.3.62f2; Android and iOS portrait
- Package/architecture constraints: UI Toolkit UXML/USS; supplied sprites only; no C# unless a label is set from code
- Explicit out of scope: a real level unlock system, a Leaderboard or Support screen, unlocking Shop or Collection, new tab art

## Verification and risk

- Focused test path/command: MCP `run_tests` EditMode `ColorBlockJam.Hub.EditModeTests`; PlayMode `ColorBlockJam.Hub.PlayModeTests` and group `SceneFlowSmokeTests`
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: no USS/UXML warnings after refresh; 0 "error CS" if C# changes
- EditMode: Hub EditMode suite green
- PlayMode: Hub PlayMode suite green (tab structure and press feedback tests updated)
- Manual/built-player/profiler/device evidence: `HubCaptureTests.CaptureHome` at 1080 x 1920 and 1080 x 2400-notch with `CBJ_HUB_CAPTURE=1` set for that run only; pixel diff outside the tab bar; Opus 5.5 chief review
- Risk: low
- Approval already granted (exact target only): the user's request and answers of 2026-09-27 for this tab bar change
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: independent verifier, Opus 5.5 chief review, then automatic closure

## Stop conditions

- The pre-level task still holds its lock or its Hub suites are not green.
- The change would need new art, an import change or a change outside the allowed paths.
- The tab bar geometry or another Home control changes.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: READY_TO_CLOSE
- Contract fingerprint: sha256:a0620e36beae4644133000aa7975a60dfba3642cfbbb75c5adadeecb91269d44
- Evidence fingerprint: sha256:f5f38b182682887ac1d5264bb732017b4dceef9c449def1079256554d3c2e89c
- Reviewed revision: vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:bfc34ab4f43aad3028b15a5355ded97c203a1d1fe4038b6d4b10fef5da1198a0
- Attempt count: 1
- Last feedback: 2026-09-28 user with a capture and the reference crop: only the right end shows "Lvl 50"; no labels under the other non-Home tabs; the non-Home label outline is too heavy
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:bfc34ab4f43aad3028b15a5355ded97c203a1d1fe4038b6d4b10fef5da1198a0
- Closed at: 2026-09-28T02:26Z
- Closure mode: automatic
- Closed by: implement-task, session Hub sahnesini oluşturma [5c52bd]
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | manual+automated: fresh capture: lock (no label), Shop and Collection dimmed+lock (no labels), Home active, lock + 'Lvl 50' (2px outline); only the label row changed | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:bfc34ab4f43aad3028b15a5355ded97c203a1d1fe4038b6d4b10fef5da1198a0 |
| AC-2 | PASS | manual: 1080x2400-notch capture differs from the last verified one only in the label row | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:bfc34ab4f43aad3028b15a5355ded97c203a1d1fe4038b6d4b10fef5da1198a0 |
| AC-3 | PASS | automated: HubSceneTests presses all 10 Home buttons including the 5 tabs; no navigation | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:bfc34ab4f43aad3028b15a5355ded97c203a1d1fe4038b6d4b10fef5da1198a0 |
| AC-4 | PASS | manual: everything above the tab bar pixel-identical; slot bounds, dividers and raised Home unchanged | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:bfc34ab4f43aad3028b15a5355ded97c203a1d1fe4038b6d4b10fef5da1198a0 |
| AC-5 | PASS | automated: Hub EditMode (218/218 job), Hub PlayMode 17/17 twice, SceneFlowSmokeTests 6/6 | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:bfc34ab4f43aad3028b15a5355ded97c203a1d1fe4038b6d4b10fef5da1198a0 |
| AC-6 | PASS | manual: PROJECT.md DEC-141 amended with the 2026-09-28 revision; design section 7 updated | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:bfc34ab4f43aad3028b15a5355ded97c203a1d1fe4038b6d4b10fef5da1198a0 |
| AC-7 | PASS | manual: Opus 5.5 chief review (C:/Users/e-fur/AppData/Local/Temp/claude/C--Users-e-fur-Documents-Repos-ColorBlockJamCase2026/9eedf9d4-2bb5-45f8-8e12-60d163432d44/tasks/wsy4wjrwt.output): CHIEF VERDICT: APPROVE | vcs:plastic:cs:14@ColorBlockJamCase2026@efurk64@unity+owned-sha256:bfc34ab4f43aad3028b15a5355ded97c203a1d1fe4038b6d4b10fef5da1198a0 |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- 2026-09-27 — Created — planning — contract authored from the user's tab bar request and answers; queued behind the running pre-level task
- 2026-09-27 — Started — implementation — user order "testleri en sona saklayıp development süresinden kazan": test runs, captures, measurements and the chief review move to one final verification pass; development continues with out-of-Editor compile checks only; the pre-level task has the same owner (this session), which orders the Tests/Hub edits, so the queue dependency is satisfied by ordering; unity-cli READY WITH WARNINGS (shared live Editor ColorBlockJamCase2026@99077e4df1ecfaa4); unity-game-dev loaded; lock 12272459…
- 2026-09-28 — Development — implementation — tab-leaderboard and tab-support show lock.png (tab__icon--level-locked) with "Lvl 50"; Shop and Collection unchanged; design section 7 and PROJECT.md DEC-141 (DEC-139 and DEC-140 were taken) updated; Hub assemblies compile out of Editor; no test run (user order to defer tests)
- 2026-09-28 — Contract change — implementation — user feedback on the live Hub: only the right end shows "Lvl 50" (supersedes the earlier answer for the left end), no labels under the non-Home tabs, thinner label outline as in the reference; the running final verification of the previous version becomes stale for this task; contract fingerprint recomputed
- 2026-09-28 — Staged — implementation — scratchpad/stage-tabbar (combined with the staged currency-holder and Hub test-isolation changes): tab-leaderboard, tab-shop and tab-collection labels empty (label boxes kept so icons do not move); tab-support keeps "Lvl 50" with .tab__label--level-locked (outline 3px -> 2px, as the reference crop); Home unchanged; DEC-141 amended in place and design section 7 updated; no test asserts label text; ColorBlockJam.Hub.PlayModeTests compiles out of Editor
- 2026-09-28 — Verified and closed — closed — landing verification wf_438f6bbc-06a on the revised layout: all 7 criteria PASS; Opus 5.5 chief review (C:/Users/e-fur/AppData/Local/Temp/claude/C--Users-e-fur-Documents-Repos-ColorBlockJamCase2026/9eedf9d4-2bb5-45f8-8e12-60d163432d44/tasks/wsy4wjrwt.output): CHIEF VERDICT: APPROVE; TASK_CLOSED automatically
<!-- TASK-LIFECYCLE:END -->
