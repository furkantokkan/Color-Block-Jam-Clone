# TASK-20260929-224511984-eaa2cae5-purchase-popup-authoring — Make the shared purchase popup editable in UI Builder

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260929-224511984-eaa2cae5-purchase-popup-authoring
- Supersedes: none
- Title: Make the shared purchase popup editable in UI Builder
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:38
- Task kind: UI
- Area: Shared purchase popup authoring and Unity authoring instructions
- Workflow: trio

## Objective and baseline

- Observable objective: Artists can open and edit the actual shared purchase popup with its styles and representative content without toggling visibility or running the game.
- Current behavior/evidence: The user's attached UI Builder screenshot is blank. BoosterPurchasePopup.uss hides its standalone root; title, description and icons have no authored sample. Runtime supplies offers, and Game binds the purchase presenter lazily on first show.
- Expected behavior: The standalone template is visible with shared styles and sample text/icons. Embedded Hub/Game instances stay hidden before binding, and runtime owns actual offer data and subsequent visibility. Instructions teach this modular authoring contract.
- Behavior that must remain unchanged: Purchase and life refill share one template; runtime SetOffer replaces all sample fields; hidden popups do not block input; lazy Game binding, prices, purchases, sprite borders and gameplay remain unchanged.

## Ownership

- Owner/writer role: current implementer, then independent verifier sequentially
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/UI/Purchase/BoosterPurchasePopup.uxml
  - Assets/ColorBlockJam/UI/Purchase/BoosterPurchasePopup.uss
  - Assets/ColorBlockJam/Runtime/UI/Purchase/BoosterPurchaseView.cs
  - Assets/ColorBlockJam/Tests/UI/EditMode/BoosterPurchasePopupAssetTests.cs
  - AGENTS.md
  - PROJECT.md
  - C:/Users/e-fur/.codex/AGENTS.md
  - C:/Users/e-fur/.codex/skills/unity-cli/SKILL.md
  - C:/Users/e-fur/.codex/skills/unity-game-dev/SKILL.md
  - production/tasks/TASK-20260929-224511984-eaa2cae5-purchase-popup-authoring/contract.md
  - production/tasks/TASK-20260929-224511984-eaa2cae5-purchase-popup-authoring/verification/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Prefabs/**
  - Assets/ColorBlockJam/Data/**
  - Assets/ColorBlockJam/Art/**
  - Assets/**/*.meta
  - Library/UIBuilder/** file edits
  - Other task contracts, defect records, checkins, publishing and unrelated cleanup
- Serialized assets: none; only existing UXML/USS text sources are changed; their GUIDs and importers stay unchanged
- Other active writers/worktrees checked: The live home-currency task owns Home UI, not these purchase paths. The canvas-preview task owns preview settings and is ready_to_close; this change is distinct from its unchanged runtime-layout criterion. No purchase-path writer or duplicate authoring contract was found.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given the actual standalone purchase UXML, when it is attached to an Editor UI panel without gameplay code, then its root is visible with Common styles, the runtime font, matching sample text layers and sample booster/currency icons.
2. Given freshly cloned Hub and GameplayHud UXML before purchase binding, then every embedded purchase screen is hidden, including the life refill instance; constructing a standalone purchase view hides it too.
3. Given a runtime offer, when SetOffer and SetVisible are called, then both text layers and both icons are replaced, the popup opens and closes, and disposal hides it. Existing button/message and sprite-border tests continue to pass.
4. Given the project/global instructions and the two owned local Unity skills, then they specify modular UXML/USS authoring, standalone previews, shared styling, representative sample data and runtime-only dynamic assignment with safe host initialization.

## References and constraints

- Story/GDD/ADR/design/log/image: Current conversation and screenshot; raw request: "bunu skill ve md lere ekleyip düzelt"; steering: "devam et insanlar tarafından editlenebilir ve modüler olmalı değerler runtime da atansa bile eğer runtime da atanması gerekliyse atanmalı".
- Unity version/platform: Unity 2022.3.62f2, Android/iOS, portrait 1080 x 1920.
- Package/architecture constraints: Existing UI Toolkit, PrimeTween, shared ColorBlockJam.UI view and presenter. Exact Unity identity proven using current websocket port/process/session registration and project-info readback. Use unity-cli and the approved pre-Unity-6 unityMCP route.
- Explicit out of scope: Redesigning other screens, changing gameplay/economy, adding assemblies, new preview frameworks, packages, serialized assets and automatic closure.

## Verification and risk

- Focused test path/command: BoosterPurchasePopupAssetTests through the pinned unityMCP EditMode runner; verifier adds panel-attached tests of standalone and unbound host clones.
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: Roslyn compile check after each C# change; current-revision compile and relevant Console check.
- EditMode: All focused purchase popup tests pass, including panel-resolved authoring and host visibility.
- PlayMode: not required; existing host initialization stays authored and existing presenter/view boundaries are verified in EditMode.
- Manual/built-player/profiler/device evidence: Inspect the actual popup in UI Builder at 1080 x 1920, preserving unsaved documents; no device build required for this authoring change.
- Risk: low
- Approval already granted (exact target only): The user approves the proposed popup authoring correction and additions to relevant skills/Markdown; latest continue explicitly authorizes execution.
- Commit/checkin constraint: forbidden
- Current invocation permission: implement and verify this task
- Handoff destination: independent verifier, then current owner for readiness; closure requires the user

## Stop conditions

- Preserve unsaved UI Builder work; do not save another document or change any protected serialized asset.
- Stop if an overlapping live writer appears or host initialization cannot preserve the hidden-before-bind behavior.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:0373777886bb96cc00ff5e08cfebc386eece21e360372cb757b47697f7fa0698
- Evidence fingerprint: sha256:3c42189d9135f5678368e204ce2b05e6ae02832eb9b343bd2a9c46c061af3f02
- Reviewed revision: vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:3bf017f02c8acd8ea7f3e9894e80aec0f98c08d02404943154fb18ec808d7995
- Attempt count: 1
- Last feedback: Artist editing and modularity required; runtime assignment only for actual dynamic state/data.
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:3bf017f02c8acd8ea7f3e9894e80aec0f98c08d02404943154fb18ec808d7995
- Closed at: 2026-09-29T23:15:23Z
- Closure mode: user_confirmed
- Closure status: closed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 Standalone authoring preview | PASS | Failed-only EditMode job 0c0fc4dec80b45b9a8891ba520a0bda7 succeeded 1/1; independent real Builder readback 1080x1920, visible, styled samples; verification/evidence.md | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:3bf017f02c8acd8ea7f3e9894e80aec0f98c08d02404943154fb18ec808d7995 |
| AC-2 Hidden runtime initialization | PASS | Completed 11-case suite 11f08a4b2d4a48dba50a0f826c5d07e1 with no failure in host/constructor cases; unbound three-instance and reattach coverage; verification/evidence.md | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:3bf017f02c8acd8ea7f3e9894e80aec0f98c08d02404943154fb18ec808d7995 |
| AC-3 Dynamic offers and preserved behavior | PASS | Completed suite with no failure in offer/open/close/dispose/null-icon/message/button/border/boundary cases; independent source review; verification/evidence.md | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:3bf017f02c8acd8ea7f3e9894e80aec0f98c08d02404943154fb18ec808d7995 |
| AC-4 Durable authoring instructions | PASS | Independent document review; both local skills pass quick_validate.py; verification/evidence.md | vcs:plastic:cs:38@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity+owned-sha256:3bf017f02c8acd8ea7f3e9894e80aec0f98c08d02404943154fb18ec808d7995 |

### Verification channels

- Roslyn compile: PASS: unity_compile_check.py for BoosterPurchaseView.cs and BoosterPurchasePopupAssetTests.cs
- EditMode: PASS with detailed-summary limitation: completed11, uncapped sole assertion failure corrected; failed-only rerun succeeded1/1; verification/evidence.md
- Manual authoring: PASS: real Builder screen Flex1080x1920, correct font/sample icons, clean document; verification/evidence.md
- Skill validation: PASS: quick_validate.py unity-cli and unity-game-dev; verification/evidence.md

Review inputs and canonical channel/criterion stream: `verification/review-identity.json`.
The generated identity manifest is diagnostic and excluded from its own source digest.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- Created — planning — bounded authoring revision; distinct from canvas-preview configuration and runtime purchase behavior.
- Started — implementation — exact project/Editor readback passed; current owner serializes this task and holds its Library/ccgs claim; no parallel production writer.
- Verified — ready_to_close — independent focused panel tests and corrected failed-only rerun, direct real UI Builder readback, Roslyn compilation and local skill validation support all criteria; detailed full-run result summary unavailable and documented. Defect ledger stays empty; no checkin or closure.
- Closed — user_confirmed — explicit user order "taski kapat ve skillerimizi de güncelle öğrendiklerimizle"; host_local_manual closure lock. Four recorded PASS criteria, verification channels, empty defect ledger and the detailed test-summary limitation are preserved. No tests rerun or checkin; additional skill learnings follow separately.
<!-- TASK-LIFECYCLE:END -->
