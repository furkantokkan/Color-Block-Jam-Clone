# TASK-20260927-141439926-2949c75f-currency-amount-price — One CurrencyAmount value for prices

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260927-141439926-2949c75f-currency-amount-price
- Supersedes: none
- Title: One CurrencyAmount value for prices
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026@efurk64@unity (Plastic workspace, shared live Editor)
- Base revision: cs:13
- Task kind: refactor
- Area: Currencies and booster pricing (Runtime/Currencies, Runtime/Boosters, Player wallet port, Editor inspector)
- Workflow: direct

## Objective and baseline

- Observable objective: A booster's price is one `CurrencyAmount` (currency + amount) that the Inspector edits on one row, the wallet checks directly and future rewards can reuse; no second price representation remains.
- Current behavior/evidence: `BoosterDefinition` stores the price as two coupled fields, `m_purchasePrice` (`[FormerlySerializedAs("m_purchaseCoinPrice")]`, int, default 10) and `m_purchaseCurrency` (`CurrencyDefinition`). `BoosterDefinition.Validate` checks them together ("price above 0 needs a currency"), `PlayerSaveService.CheckPurchase` re-checks the pair, and `IWallet.CanAfford(CurrencyDefinition, int)` takes them apart again. `ColorBlockJamDataSetup.CreateDefinition` writes both fields. The five shipped assets under `Data/Boosters/Definitions` cost 10 Coin each (`CurrencyCatalogTests.EveryShippedBoosterPaysTenCoinsForOneChargeAndTheCatalogValidates`).
- Expected behavior: A `[Serializable]` `CurrencyAmount` struct in `ColorBlockJam.Runtime.Currencies` (private serialized `m_currency` and non-negative `m_amount`, read-only getters, a constructor, `IsValid` = currency with a valid ID and amount above 0) is the only price type. `BoosterDefinition` serializes one `m_purchaseCost` and exposes `PurchaseCost`; its `Validate` uses `CurrencyAmount` rules with the same designer-language messages. `IWallet.CanAfford(CurrencyAmount cost)` replaces the two-argument form; `PlayerSaveService` purchase checks read `PurchaseCost`. A UI Toolkit `CurrencyAmountDrawer` (`CreatePropertyGUI`) in `ColorBlockJam.Editor` shows currency and amount on one row under the field label. `ColorBlockJamDataSetup` writes `m_purchaseCost`. The five booster assets are migrated in the Editor through `SerializedObject` to `m_purchaseCost` = 10 Coin, keeping every GUID.
- Behavior that must remain unchanged: DEC-85 purchase results (10 coins for 1 charge; insufficient, invalid offer, stock full and failed save change neither balance nor stock; duplicate submission cannot grant twice), DEC-92 prepare-commit-publish order, DEC-128 currency catalog rules, every booster asset GUID and non-price field, Home currency display, and the save snapshot format (prices are not saved).

## Ownership

- Owner/writer role: implementer (unity-feature-implementer lane), then an independent verifier
- Allowed paths (rebased onto docs/Folder-Layout.md; restructure landed in the workspace, checkin pending):
  - Assets/ColorBlockJam/Runtime/Core/Currencies/CurrencyAmount.cs
  - Assets/ColorBlockJam/Runtime/Core/Currencies/CurrencyAmount.cs.meta
  - Assets/ColorBlockJam/Runtime/Core/Boosters/BoosterDefinition.cs
  - Assets/ColorBlockJam/Runtime/Player/IWallet.cs
  - Assets/ColorBlockJam/Runtime/Player/PlayerSaveService.cs
  - Assets/ColorBlockJam/Editor/Core/Currencies/CurrencyAmountDrawer.cs
  - Assets/ColorBlockJam/Editor/Core/Currencies/CurrencyAmountDrawer.cs.meta
  - Assets/ColorBlockJam/Editor/Core/Currencies.meta
  - Assets/ColorBlockJam/Editor/Core/Data/ColorBlockJamDataSetup.cs
  - Assets/ColorBlockJam/Data/Boosters/Definitions/ExtraTime.asset
  - Assets/ColorBlockJam/Data/Boosters/Definitions/Hammer.asset
  - Assets/ColorBlockJam/Data/Boosters/Definitions/Rocket.asset
  - Assets/ColorBlockJam/Data/Boosters/Definitions/TimeFreeze.asset
  - Assets/ColorBlockJam/Data/Boosters/Definitions/Vacuum.asset
  - Assets/ColorBlockJam/Tests/Player/EditMode/CurrencyCatalogTests.cs
  - Assets/ColorBlockJam/Tests/Player/EditMode/PlayerSaveServiceTests.cs
  - Assets/ColorBlockJam/Tests/Player/EditMode/PlayerTestFixtures.cs
  - Assets/ColorBlockJam/Tests/Player/EditMode/CurrencyAmountTests.cs
  - Assets/ColorBlockJam/Tests/Player/EditMode/CurrencyAmountTests.cs.meta
  - Assets/ColorBlockJam/Tests/Hub/EditMode/HomePresenterTests.cs
  - Assets/ColorBlockJam/Tests/UI/**
  - docs/Hub-And-Player-Data-Design.md
  - PROJECT.md
  - production/tasks/TASK-20260927-141439926-2949c75f-currency-amount-price/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/Core/Boosters/BoosterCatalog.cs
  - Assets/ColorBlockJam/Data/Boosters/BoosterCatalog.asset
  - Assets/ColorBlockJam/Runtime/Player/Storage/**
  - Assets/ColorBlockJam/Runtime/Player/PlayerSnapshot.cs
  - Assets/ColorBlockJam/Runtime/Hub/**
  - Assets/ColorBlockJam/UI/Hub/**
  - Assets/ColorBlockJam/UI/Common/**
  - Assets/ColorBlockJam/UI/Purchase/**
  - Assets/ColorBlockJam/UI/Settings/**
  - Assets/ColorBlockJam/UI/Startup/**
  - Assets/ColorBlockJam/UI/Themes/**
  - Assets/ColorBlockJam/Runtime/UI/**
  - Assets/ColorBlockJam/Runtime/Gameplay/**
  - Assets/ColorBlockJam/UI/Game/**
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Resources/Onity/ProjectContext.prefab
  - Hand-editing `.asset` YAML while the live Editor is reachable
  - A legacy fallback field, `ISerializationCallbackReceiver` migration shim or second price property
  - Any save-format change
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Data/Boosters/Definitions/ExtraTime.asset, Hammer.asset, Rocket.asset, TimeFreeze.asset, Vacuum.asset
    Sole writer: this task's implementer through `SerializedObject` in the live Editor, immediately after the compile that introduces `m_purchaseCost` (set `m_purchaseCost.m_currency` = `Data/Currencies/Coin.asset`, `m_purchaseCost.m_amount` = 10, then `AssetDatabase.SaveAssets`)
    Matching `.meta` owner: unchanged
- Other active writers/worktrees checked: 2026-09-27 14:14 UTC. TASK-20260927-004448215-54655902-booster-gameplay-flow holds its lifecycle lock (acquired 13:44 UTC by session "Booster gameplay flow [f1ed7e]") and owns `Runtime/Boosters/*.cs`, `Player/PlayerSaveService.cs`, `Tests/Player/EditMode/PlayerSaveServiceTests.cs`, `Data/Boosters/Definitions/Hammer.asset` and PROJECT.md. TASK-20260926-235915696-6b7f82f4-player-data-currencies (this session) closes before this task starts. TASK-20260926-235915699-78356740-hub-pre-level-launch (not started) will read `PurchaseCost` in its shared purchase popup.
- Execution dependency: TASK-20260927-004448215-54655902-booster-gameplay-flow (stable handoff: its lifecycle lock is released and its owned booster/save files compile green); and the folder-restructure session's Plastic checkin of its pending moves (no write into any allowed path above until that checkin lands, so this task's edits are not carried by the restructure session's own changeset)
- Queue policy: auto-dispatch-after-owner-release

## Acceptance scenarios

1. Given the five shipped booster assets after migration, when inspected and loaded, then each has `PurchaseCost` = 10 `Coin` and its purchase amount unchanged, every GUID is unchanged, `BoosterCatalog.Validate` passes, and no source, asset or test mentions `m_purchasePrice`, `m_purchaseCoinPrice` or `m_purchaseCurrency`.
2. Given `CurrencyAmount`, when constructed with a null currency, an invalid currency ID, amount 0 or a negative amount, then `IsValid` is false; with `Coin` and 10 it is true; a booster whose cost is invalid but above 0 fails `Validate` with a designer-language message naming the booster.
3. Given the purchase tests, when run, then every DEC-85 outcome (purchased, insufficient funds, invalid offer, stock full, save failed, concurrent submissions) behaves exactly as before and `IWallet.CanAfford(CurrencyAmount)` returns the committed-balance answer.
4. Given a booster definition in the Inspector, when viewed, then the price shows as one labelled row with the currency field and the amount field, edits persist and support Undo, and negative amounts cannot be stored.
5. Given `ColorBlockJamDataSetup` "Create Default Assets" with a booster asset missing, when run, then the recreated booster has `PurchaseCost` = 10 Coin and validates.
6. Given PROJECT.md and design section 3, when read, then DEC-137's CurrencyAmount part and the shipped types agree, and rewards are named as the next intended reuse.

## References and constraints

- Story/GDD/ADR/design/log/image: PROJECT.md DEC-85, DEC-92, DEC-128, DEC-129, DEC-137; docs/Hub-And-Player-Data-Design.md section 3; CBJ Template `Assets/Watermelon Core/Modules/Currency/Scripts/CurrencyAmount.cs` and `Editor/CurrencyAmountPropertyDrawer.cs` (behaviour reference only; no template code is copied)
- Unity version/platform: Unity 2022.3.62f2; Android and iOS
- Package/architecture constraints: `CurrencyAmount` stays in `ColorBlockJam.Runtime` (Runtime cannot reference Player); no new package or asmdef reference; UI Toolkit for the drawer; the struct allocates nothing and holds no balance; balances remain only in the save service.
- Explicit out of scope: level-completion rewards (the win flow task reuses this type later), the template's remote-config default override, analytics source/sink tags, change-delta events, floating-coin presentation settings, and any save-format change.

## Verification and risk

- Focused test path/command: `ColorBlockJam.Player.EditModeTests` (CurrencyAmountTests, CurrencyCatalogTests, PlayerSaveServiceTests), `ColorBlockJam.Hub.EditModeTests`, `ColorBlockJam.UI.EditModeTests` through MCP for Unity `run_tests`
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: out-of-Editor compile of Runtime, Player, Editor, Hub and the three test assemblies before the files enter Assets; Console free of new errors after the refresh and the asset migration
- EditMode: the focused suites above green
- PlayMode: `SceneFlowSmokeTests` green (project scope still validates the booster catalog)
- Manual/built-player/profiler/device evidence: Inspector readback of one booster showing the one-row price (screenshot or readback of the drawer's element tree)
- Risk: high (five serialized booster assets)
- Approval already granted (exact target only): none for this task; the user's 2026-09-27 order to port the template's good architecture authorizes the refactor, and the five booster assets need explicit target approval before the migration write
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: independent verifier, then task-cycle closure

## Stop conditions

- The booster-gameplay-flow lock is still held or its owned files do not compile.
- The user has not approved the five booster asset writes.
- Any purchase outcome changes behaviour.
- A booster asset GUID or non-price field changes.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: USER_CONFIRMED
- Contract fingerprint: sha256:dfcded2a25460dc18d3d9f3770ee6b20fd4eb5b5d439f6a68b332ff180fc3762
- Evidence fingerprint: none
- Reviewed revision: none
- Attempt count: 0
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: cs:23 (Plastic workspace head, unreviewed)
- Closed at: 2026-09-29T01:16:19Z
- Closure mode: user_confirmed
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | UNPROVEN | none | not run |
| AC-2 | UNPROVEN | none | not run |
| AC-3 | UNPROVEN | none | not run |
| AC-4 | UNPROVEN | none | not run |
| AC-5 | UNPROVEN | none | not run |
| AC-6 | UNPROVEN | none | not run |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|

### Transition history

- 2026-09-27 — Created — planning — contract authored from the user's order to port the CBJ Template currency module's good architecture; queued behind the live booster-gameplay-flow owner
- 2026-09-28 — Contract change (path rebase) — planning — paths rebased onto the folder restructure (docs/Folder-Layout.md); no scope, criteria, risk or approval change; contract fingerprint recomputed
- 2026-09-29T01:16:19Z — Closed (user confirmed) — closed — explicit user order 2026-09-29 ("açık taskları da kapat üstünde çalışılmayanları artık"); prior state ready / planning. Closure mode user_confirmed: every acceptance criterion result, blocker note and defect row is preserved as recorded, not verified by this closure.
<!-- TASK-LIFECYCLE:END -->
