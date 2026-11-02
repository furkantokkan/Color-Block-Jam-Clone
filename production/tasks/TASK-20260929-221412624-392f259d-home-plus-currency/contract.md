# TASK-20260929-221412624-392f259d-home-plus-currency — Give each Home plus button its own currency

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260929-221412624-392f259d-home-plus-currency
- Supersedes: none
- Title: Give each Home plus button its own currency
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:38 plus unrelated pending work
- Task kind: bug
- Area: Hub Home dummy currency reward
- Workflow: direct

## Objective and baseline

- Observable objective: The life plus grants only one life; the coin plus grants only 100 coins.
- Current behavior/evidence: User: "Artı butonlarını nyeri karışmış ikiside altın veriyor biri can diğeri altın vermeli". Screenshot: C:/Users/e-fur/AppData/Local/Temp/codex-clipboard-16a97b61-3e49-4e9b-9068-7f48866e589e.png. DummyCurrencyReward currently hooks both buttons to a combined life-and-coin grant. DEC-161 records that older ad-hoc behavior; no existing task owns this new separate-reward outcome. The life-economy task explicitly preserves the old combined reward, so it is not a recurrence against that contract.
- Expected behavior: Bind by authored holder currency ID, independently of visual order. A full life balance stays full without granting coins. Ignore unrelated plus buttons.
- Behavior that must remain unchanged: Save-owner clamping and commit-before-publish, reward amounts, single pending grant, detach/dispose behavior, Home layout and life refill economy.

## Ownership

- Owner/writer role: current session implementer; sequential independent read-only verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Runtime/Hub/Home/DummyCurrencyReward.cs
  - Assets/ColorBlockJam/Runtime/Hub/Composition/HubSceneInstaller.cs
  - Assets/ColorBlockJam/Tests/Hub/EditMode/DummyCurrencyRewardTests.cs
  - Assets/ColorBlockJam/Tests/Hub/EditMode/DummyCurrencyRewardTests.cs.meta
  - production/tasks/TASK-20260929-221412624-392f259d-home-plus-currency/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Resources/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/UI/**
  - Assets/ColorBlockJam/Runtime/Player/**
  - VCS checkin, shelve, push or publishing
- Serialized assets: none
- Other active writers/worktrees checked: Plastic status lists unrelated dirty work; target reward and installer have no pending changes. New test path and unique task directory have no other owner. Sequential lifecycle writer in this session; no cross-terminal lock safety claimed in this Plastic workspace.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given the authored Home tree, pressing life plus grants life by 1 and never coin; pressing coin plus grants coin by 100 and never life.
2. Given life at its maximum, pressing life plus never grants coins; save-service clamping remains authoritative.
3. Given reordered holders or an unrelated plus button, currency routing remains correct and the unrelated button grants nothing.
4. Given repeated Attach, Detach, Dispose or a pending save, no duplicate grant or stale callback is accepted; pending work receives cancellation on Dispose.

## References and constraints

- Story/GDD/ADR/design/log/image: User screenshot above; PROJECT.md DEC-161 (old combined behavior superseded by this request), DEC-143 authored holder IDs.
- Unity version/platform: 2022.3.62f2, Android/iOS portrait
- Package/architecture constraints: Existing UI Toolkit, ICurrencyGrant and CurrencyCatalog; no public API change or new assembly.
- Explicit out of scope: Layout, paid refill, shop, save schema, new currencies, device build.

## Verification and risk

- Focused test path/command: EditMode ColorBlockJam.Tests.Hub.DummyCurrencyRewardTests
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: Roslyn compile check for changed C# files; exact-target live Console if available.
- EditMode: Authored Home UXML callback dispatch, reward isolation, full life, order, rebind/detach/dispose and pending-save regression tests.
- PlayMode: not required; no scene or MonoBehaviour changes
- Manual/built-player/profiler/device evidence: not required for callback-only change; device verification not claimed
- Risk: low
- Approval already granted (exact target only): User repair request authorizes scoped source/test changes.
- Commit/checkin constraint: forbidden
- Current invocation permission: implement
- Handoff destination: independent read-only verifier

## Stop conditions

- A live writer changes an owned file.
- Required fix needs a protected serialized target or public API change.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: ready_to_close
- Current phase: verification
- Last cycle verdict: READY_TO_CLOSE
- Contract fingerprint: sha256:d2e9078204217bd91b9a16efb4b5be1f75611bcd25406b8f75f3b34a7ab5ae09
- Evidence fingerprint: sha256:aee1be23cc6d5b65f57aff1bedcce75b030ba9ee69c15ec062c2ca8a731a8ddb
- Reviewed revision: vcs:plastic:cs:38@ColorBlockJamCase2026@efurk64@unity+owned-sha256:7151318ac5e74a1c4cee7fbb7222d5cdb85a3722afdb5dec21b61fc8d17d555e
- Attempt count: 1
- Last feedback: none
- Blocker: none
- Next action: $task-done TASK-20260929-221412624-392f259d-home-plus-currency
- Closed revision: none
- Closed at: none
- Closure mode: none
- Closed by: none
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated: PlusGrantsOnlyItsHolderCurrency life/coin, job 561a4aff521240dc8fa384a0274307c1 PASS 7/7 | vcs:plastic:cs:38@ColorBlockJamCase2026@efurk64@unity+owned-sha256:7151318ac5e74a1c4cee7fbb7222d5cdb85a3722afdb5dec21b61fc8d17d555e |
| AC-2 | PASS | automated: FullLifeDoesNotGrantCoins; unchanged save-owner clamp | vcs:plastic:cs:38@ColorBlockJamCase2026@efurk64@unity+owned-sha256:7151318ac5e74a1c4cee7fbb7222d5cdb85a3722afdb5dec21b61fc8d17d555e |
| AC-3 | PASS | automated: RoutingUsesHolderIdsAndIgnoresUnrelatedPlusButtons | vcs:plastic:cs:38@ColorBlockJamCase2026@efurk64@unity+owned-sha256:7151318ac5e74a1c4cee7fbb7222d5cdb85a3722afdb5dec21b61fc8d17d555e |
| AC-4 | PASS | automated: ReattachDoesNotDuplicateAndDetachRemovesBothCallbacks, DisposeCancelsPendingGrantAndCannotReattach, PendingGrantBlocksBothButtonsUntilSaveCompletes | vcs:plastic:cs:38@ColorBlockJamCase2026@efurk64@unity+owned-sha256:7151318ac5e74a1c4cee7fbb7222d5cdb85a3722afdb5dec21b61fc8d17d555e |

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- 2026-09-30 — Created — planning — task-bug NO_TASK_MATCH; separate reward outcome replaces the older DEC-161 ad-hoc behavior.
- 2026-09-30 — Started — implementation — user repair authorization; unity-cli READY WITH WARNINGS, unity-game-dev; sole lifecycle writer serialized manually in Plastic workspace.
- 2026-09-30 — Verified — verification — all four criteria PASS at reviewed revision; Roslyn COMPILE_OK; exact Editor identity proven using instance discovery, process/project/version and TCP connection evidence, full-ID pin and project readback; 7/7 EditMode tests PASS (job 561a4aff521240dc8fa384a0274307c1, batch cdc6e3902302); independent verifier PASS; Console errors 0; empty defect ledger preserved; READY_TO_CLOSE, no checkin or closure.
<!-- TASK-LIFECYCLE:END -->
