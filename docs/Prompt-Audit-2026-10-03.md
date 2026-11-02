# Prompt audit — 2026-10-03

## Verdict and scope

The reviewed instruction chain needs maintenance: current procedures contain stale authoring instructions, a missing documented connection, and conflicting coordination rules. The most consequential issue is an Editor helper that does not implement the exact-target proof required by the current Unity workflow.

The initial source-based audit in Codex used two independent read-only review lanes. On the user's follow-up, Claude Code **2.1.287** also completed `/doctor prompt-audit`; its findings were checked against local sources and incorporated below. Sources covered the repository `AGENTS.md`, `CLAUDE.md`, relevant `PROJECT.md` sections, both local skills and the Claude adapter, global Claude/Codex instructions, canonical orchestration, relevant installed lifecycle/Unity workflow instructions, and the Claude mobile Unity role. This is not an exhaustive audit of every installed plugin, skill, historical task, or memory file.

Only this report and generated audit evidence under `output/prompt-audit-claude-2026-10-03/` were written as deliverables. No existing instruction, configuration, source, serialized asset, package, or task state was changed. No live Editor selection, test, build, repair, checkin, or other VCS mutation was performed.

Priority: **P1** means a procedure can undermine target/ownership correctness; **P2** means an actionable stale instruction, contradiction, or avoidable context burden. These priorities describe the instructions, not newly reproduced gameplay failures.

## Findings

### PA-01 — P1: the profiling procedure selects an Editor by name

- Instruction: `.agents/skills/cbj-performance-profiling/SKILL.md:44–48` directs the agent to use `live_editor.py` for identity and state.
- Implementation: `.agents/skills/cbj-performance-profiling/scripts/live_editor.py:59–63` returns the first instance whose `name` equals `ColorBlockJamCase2026`. Lines 111–121 send commands to that ID without matching the canonical project path and full Unity version. The `resource` branch at lines 107–109 reads without selecting a proven target.
- Required contract: `C:/Users/e-fur/.codex/skills/unity-cli/SKILL.md:120–156` requires an instance-bound exact root/version match before selection and a project-info readback afterwards; basename and first/only-instance matches are explicitly insufficient.
- Impact: two open checkouts with the same project name can cause capture hooks or other C# commands to run against the wrong Editor. The initial `state.cs` request does not satisfy the required proof before that request.
- Proposed correction: make the skill require the shared identity procedure and make the helper consume a proven full instance identity, root and version. Reject ambiguous/mismatched instances and bind resource reads to the same proven session. Keep Unity 2022's approved MCP route.
- Verification needed for a future fix: two same-named instances with different paths; missing path/version evidence; reconnect; resource and code calls. This audit did not contact either Editor.

### PA-02 — P1: shared orchestration gives incompatible test-lock handoffs

- `C:/Users/e-fur/Documents/FurkanOS/.agents/skills/agent-orchestration/references/tracked-production.md:61–65` says an `AWAITING_TESTS` predecessor has released its lock and paths, so the successor should start before the test result returns.
- The same file at lines 155–161 says direct and cycle owners retain their lock through the implementer/verifier handoff.
- The installed Codex `implement-task/SKILL.md:169–176` and `task-cycle/references/lifecycle-lock.md:64–69` require the lock through verification. Claude's `unity-cli/SKILL.md:256–261` instead describes submitting with `--no-wait`, releasing ownership, then checking result freshness.
- Claude's `task-cycle/references/lifecycle-lock.md:77–94` explicitly defines that release-and-reacquire transition. The provider difference is intentional; the confirmed incompatibility is the common scheduler applying it to Codex without a matching lifecycle contract. For Claude, the common hold-through-verification wording needs its documented test-wait exception.
- The shared profile at lines 83–87 also requires a shared test batch for both callers, while the inspected Codex `unity-cli/SKILL.md` has no corresponding shared-batch procedure. Claude's version does.
- Impact: the common scheduler and the current provider's lifecycle owner can disagree on whether paths are available while verification is pending. This can strand a successor or allow it to modify files that another lane still regards as owned. A status name alone is insufficient release evidence.
- Proposed correction: resolve and forward the provider's verification/ownership policy as explicitly as closure policy. Define one state transition for release, stable source fingerprints, result freshness, and reacquisition. Dispatch only after the actual owner's release is verified; do not infer it solely from `AWAITING_TESTS`.

### PA-03 — P2: the documented Claude MCP entry is absent

- `AGENTS.md:111–118` says `.mcp.json` starts the pinned `unityMCP` server over stdio.
- `.mcp.json:1–3` currently contains only `{ "mcpServers": {} }`.
- `.codex/config.toml:4–5` does contain the documented Codex HTTP endpoint at `http://127.0.0.1:8082/mcp`.
- Impact: following the project-level Claude startup instructions cannot obtain the promised server from that file. This does not prove that every Claude session lacks a server configured elsewhere.
- Proposed correction: reconcile the documentation with the intended current Claude configuration. If project-scoped stdio is still intended, restore that exact entry with target approval; otherwise document the actual authority and startup route. Do not change Codex's working configuration merely to make the two providers identical.

### PA-04 — P2: board-piece authoring describes a retired data model and API

The active `.agents/skills/board-piece-authoring/SKILL.md` contains related forms of drift:

| Skill instruction | Current evidence | Required correction |
| --- | --- | --- |
| Lines 15–22, 31–59 describe selectable piece databases, generated enum/numeric identities, derived shape prefabs, and a persistent `BlockShapeLibrary.asset`. | `Editor/Core/BoardKit/BoardKitAssets.cs:19–23` names one fixed JSON and one bare root prefab. `BlockShapeLibrary.cs:11–25` and `LoadProject` at line 79 describe editor-only templates, derived `ShapeKey`, and a hidden unsaved wrapper. `PROJECT.md` DEC-193/195 at lines 821/823 records the change. | Describe fixed `Data/Json/Pieces/BlockShapeLibrary.json` with name/cells records and the current template workflow. Remove the retired enum/prefab-generation steps and persistent wrapper reference. |
| Line 24 says the grid is fixed at 7×7; lines 42–48 say Save Level adds shapes through `TryAddPieces` and writes enum/prefab outputs. | `Editor/Core/BoardKit/PieceEditorWindow.cs:193` uses an adjustable grid; `PieceGridDraft.cs:15` takes its limit from `Runtime/Core/Board/ShapeKey.cs:21`, which is 5. DEC-180 at `PROJECT.md:808` keeps level pieces level-only on Save Level; no `TryAddPieces` reference remains in `Editor/Core`. | Describe the 1–5 square-grid control and distinguish Save Level from explicit Save as template. |
| Lines 66–70 prescribe `Prepare(type, count, cellCapacity)`, reference `BlockPiece.SetShape`, and forbid all prepared-pool growth. | `Runtime/Presentation/Board/BlockPieceFactory.cs:81` exposes `Prepare(int count, int cellCapacity)`. Lines 123–129 describe growable shared tiles, while root checkout remains bounded at lines 151–154. `BlockPiece.cs:47/63` provides `SetCells`/`TrySetCells`, with no `SetShape` method. | Use the current signatures and distinguish bounded block roots from the shared tile pool. Do not alter runtime behavior to match an obsolete skill. |
| Lines 80–82 say to author colors in `GamePalette.asset`. | `Runtime/Core/Art/GamePalette.cs:9–11` and 19–38 name atlas cells; `PROJECT.md:538–546` makes `Art/Textures/Atlases/T_GameplayPalette.png` the color authority. | Author color pixels in the atlas; use `GamePalette.asset` for cell mappings. Preserve the derived export direction. |

Source paths in the table are relative to `Assets/ColorBlockJam/`. The stale skill can direct an agent toward missing controls/assets, invalid method calls, or the wrong editable authority. Its referenced board-kit README already marks parts as superseded; that does not update the active skill's imperative steps.

The same palette drift remains in a current rule: `PROJECT.md:547–548` says **Update Shared Art Material** regenerates the atlas, although DEC-202 at line 830 explicitly says it no longer paints it. Correct that current-rule sentence alongside the skill; retain the dated decision history as history.

### PA-05 — P2: the startup import includes the entire decision history

- `CLAUDE.md:2` imports `@PROJECT.md`; that file is **235,553 bytes** and contains the decision log from line 692 onward.
- Measured source sizes: global Claude instructions 55,709 bytes; repository AGENTS 16,234 bytes; PROJECT 235,553 bytes; CLAUDE adapter 23 bytes. Their raw total is **307,519 bytes**, before skills, tools, task material, and conversation. This is a file-size measurement, not a token count or latency benchmark.
- Claude's [official memory documentation](https://code.claude.com/docs/en/memory#import-additional-files) states that imported files expand into startup context; moving text behind an `@` import does not make it load on demand.
- Impact: every task receives extensive historical decisions, old verification narratives, and superseded designs alongside current rules. This raises context cost and the burden of distinguishing present authority from history.
- Additional examples from the native audit: the dated Onity verification at `AGENTS.md:139–143` and purchase-authoring verification at `PROJECT.md:35–42` could live in linked evidence records. These records are not themselves false; preserve their verification limitations when moving them. Supersession markers in the decision log would further clarify history.
- Proposed correction: retain a compact current-rule authority for startup, move dated decisions into a linked decision log, and read feature references when relevant. Preserve stable decision IDs and links. Apply the same principle to unrelated cross-project material in global instructions. No measured speedup or exact token saving is claimed.

### PA-06 — P2: waiting guidance contradicts the automatic owner queue

- Canonical `agent-orchestration/SKILL.md:138–139`: “Do not wait inside the session for another owner's paths” and defer the dependent chain.
- Its `references/tracked-production.md:127–134` instead requires keeping the invocation alive, monitoring completion/claim release, and dispatching the successor automatically. Codex global `AGENTS.md:115–119` expressly requires the latter behavior for an authorized task behind a live owner.
- Impact: an agent following the broad fast-finish advice can end the invocation even though a specific authorized successor is supposed to resume automatically. Higher-priority user instructions resolve the immediate choice, but the procedure still communicates inconsistent behavior.
- Proposed correction: scope the no-wait rule to optional or unarmed follow-up chains. Explicitly preserve `WAITING_FOR_OWNER` monitoring and automatic dispatch when the provider supports the wake signal and the user has authorized the work. Retain the existing honest limitation when no wake signal is available.

### PA-07 — P2: the profiling skill still recommends a removed package

- `.agents/skills/cbj-performance-profiling/SKILL.md:136` includes ZLinq in its current allocation-reduction tools.
- `AGENTS.md:213–214` states that ZLinq and NuGetForUnity were removed on 2026-09-29 and gameplay uses plain loops. The current package manifest has no ZLinq entry.
- Impact: a performance task can be steered toward an unavailable library or an unnecessary package-install decision.
- Proposed correction: replace the ZLinq recommendation with the current plain-loop guidance. Keep ZString, prebuilt strings, pooling, and measured before/after evidence.

### PA-08 — P2: the Claude mobile Unity role conflicts with its operating policies

The native audit identified `C:/Users/e-fur/.claude/agents/mobile-unity-game-dev.md`. A separate read-only review and direct source checks confirmed these bounded issues:

| Issue | Evidence and impact | Proposed correction |
| --- | --- | --- |
| Git isolation in a Plastic project | Agent line 6 fixes `isolation: worktree`; repository `AGENTS.md:15` identifies a Plastic workspace. Global Claude instructions at line 609 require Git branches/worktrees for parallel writers. Canonical orchestration at lines 78–86 offers sequential role handoffs but no explicit Plastic alternative for independent writers. The Git-isolated role has no established invocation route here; no launch failure was reproduced. | Explicitly select sequential writers for this workspace and an invocation without Git worktree isolation. Define a separate policy before treating copied Plastic workspaces as approved parallel isolation. |
| Fixed model bypasses task-based routing | Agent line 4 sets `model: opus` for a broad implementation/debugging/optimization role. Global Claude instructions at lines 80–82 require variable-complexity Unity roles to use `inherit`; lines 100–104 explain that agent frontmatter otherwise controls the model. | Use `model: inherit`, unless the user deliberately selects a fixed exception. This is an inconsistency within Claude's own policy, not a comparison with Codex model routing. |
| Conflicting memory instructions | Agent lines 61–69 direct it to save project conventions, architecture and solved issues; lines 145–148 forbid those categories, and line 203 says user-scoped learnings should stay general. The same file also embeds static directory/existence and empty-memory assertions at lines 73 and 207. | Keep one consistent memory policy and remove frozen runtime-state assertions. The audit did not read the actual memory store, prove it nonempty, or verify that the harness duplicates this text. |

These are configuration/procedure findings. No role, memory, model preference or isolation setting was edited.

## Claude CLI follow-up and adjudication

The native run completed successfully in about **4 minutes 22 seconds**: process exit `0`, result `success`, no permission denials, empty stderr. It used `claude-opus-5-5` at high effort, session `1d45c3be-97b0-437a-ba02-04f576fa8524`. The invocation used plan permission mode, disabled hooks for that invocation, supplied an empty strict MCP configuration, and restricted tools to `Read`, `Glob`, `Grep`, and `Skill`. The event log records 45 calls, all to `Read`, `Glob`, or `Grep`.

| Original finding | Native result | Consolidated result |
| --- | --- | --- |
| PA-01 | Confirmed | Retained. |
| PA-02 | Confirmed for Codex; qualified for Claude | Retained with the provider-specific test-wait exception clarified. |
| PA-03 | Unverified: Claude chose not to read configuration files | Retained after direct local inspection; `.mcp.json` has an empty `mcpServers` object. |
| PA-04 | Confirmed | Expanded with the grid and Save Level drift. |
| PA-05 | Confirmed import; bytes not remeasured by Claude | Retained with the root reviewer's measured bytes and matching fingerprints. No token estimate adopted. |
| PA-06 | Confirmed | Retained. |
| PA-07 | Confirmed | Retained. |

Native additions were not accepted indiscriminately: historical decision rows and dated verification narratives extend PA-05, rather than becoming separate correctness failures. The role's isolation/model/memory issues became PA-08 after source review. The installed-version sentence at global Claude lines 20–22 is explicitly dated 2026-09-29; the newer observed CLI version is an optional wording cleanup, not proof that the dated record is false. The word "preflight" in a sentence does not by itself prove use of the retired standalone command. Claimed harness memory duplication remains unverified.

Generated evidence: [native report](../output/prompt-audit-claude-2026-10-03/claude-report.md), [run metadata](../output/prompt-audit-claude-2026-10-03/run-metadata.json), and [post-run fingerprints](../output/prompt-audit-claude-2026-10-03/post-run-fingerprints.json). The unedited native report contains candidate findings; the adjudicated findings above are the final audit conclusions.

## Deliberate differences and checks that did not become findings

- Claude's verified automatic task closure and Codex's explicit-user closure are intentional provider policies. The canonical orchestration already forwards closure authority; they should not be unified by deleting either preference.
- Unity 2022.3.62f2's MCP exception is intentional. The project should not be upgraded or given the Unity 6 Pipeline package to satisfy a generic example.
- Codex HTTP and Claude stdio are not inherently contradictory; the missing declared project entry is the finding.
- Historical decision rows are not treated as current commands simply because they contain old names. The board skill is actionable because it presents retired behavior as current steps.
- Local orchestration aliases inspected here point to the existing canonical FurkanOS procedure and forward arguments. They are not broken paths.
- Installed Unity CLI reported `1.0.0-beta.11`; `releases --lts`, `projects info`, and `editors --installed` remain present in its command help. Those examples were not flagged as obsolete.
- No finding was based solely on a model name, reasoning keyword, uppercase emphasis, or document length. The context finding identifies a concrete always-loaded import and measured bytes.

## Evidence and limits

Snapshot date: 2026-10-03. Relevant declarations were cross-checked against files and source signatures. All five fingerprints below matched the initial audit before the native follow-up and remained unchanged afterwards. `cm status . --header --short` exited successfully but returned no text, so this report does not infer a clean workspace or a current changeset from it.

Selected SHA-256 fingerprints captured during the audit:

| File | SHA-256 |
| --- | --- |
| Repository AGENTS.md | `9E28E81C8344FBDFCF1CA4D98812F427CBA5075E748980385D247F8B0EF85001` |
| Repository CLAUDE.md | `B67328484B2CFED9218EF1DF252B98C6B2101FBA75791DE3682C38C0083060BD` |
| PROJECT.md | `4D0CAFEEA0575FD8FFE6A1DEE31859E7B61EFC215E36F0798EDD678E92FD4F4E` |
| Global Codex AGENTS.md | `BE0A9F865BCACD20B64B999F91024CCE5D6A6ECEB0317295944684FADB5403BC` |
| Global Claude CLAUDE.md | `5B6F51C95399EF51FE9AEA2A85F4D607B0CF29D25217B02D6427C578D5B3982E` |

These are documentation and static-source findings, not an end-to-end qualification of agent behavior. The global-review delegate's deeper skill comparison was timeboxed before completion; the main reviewer directly read and cross-checked the reported shared-procedure clauses. A future correction should first reconcile target identity and lifecycle ownership, then update stale authoring/configuration guidance, then reduce startup context. Existing approval, asset-ownership, closure, and verification rules remain in force.

Completion note: added and then updated this report with the completed native Claude audit, qualified comparisons, and verified role-policy findings; no remediation was applied. Skills used for the audit procedure: `unity-agentic-workflow` and canonical `agent-orchestration`; `unity-cli` and lifecycle skills were inspected as audit subjects. Follow-up priority remains target identity and lifecycle ownership, then stale authoring/configuration guidance, then startup-context reduction.
