# TASK-20260926-235915696-6b7f82f4-player-data-currencies — Player data save service and data-driven currencies

## Identity

- Contract status: ready
- Waiting reason: none
- Action required: none
- Task ID: TASK-20260926-235915696-6b7f82f4-player-data-currencies
- Supersedes: none
- Title: Player data save service and data-driven currencies
- Repository (absolute path): C:/Users/e-fur/Documents/Repos/ColorBlockJamCase2026
- Branch/worktree or Plastic workspace: /main@ColorBlockJamCase2026/ColorBlockJamCase2026@efurk64@unity
- Base revision: cs:10 (head) plus uncommitted workspace changes from other live sessions
- Task kind: feature
- Area: Player persistence and economy (new `ColorBlockJam.Player` assembly, `Runtime/Currencies`)
- Workflow: direct

## Objective and baseline

- Observable objective: Coins, lives, booster stock, the current level index and the three Settings toggles live in one versioned JSON player snapshot owned by one app-scoped save service and stored on disk encrypted and authenticated; every currency is a `CurrencyDefinition` ScriptableObject listed in a `CurrencyCatalog`, so a new currency needs only a new asset and a catalog entry; persistence goes through one `IPlayerSnapshotStore` port that a future backend store can implement without any other layer (SOLID seam, documented sketch only).
- Current behavior/evidence (2026-09-27): no save service exists (`grep` for Save/Snapshot/Wallet classes finds only Level Editor code); `BoosterDefinition` stores a coin-only `m_purchaseCoinPrice`; `ProjectContext.prefab` has one installer (`ProjectScopeInstaller`); PRD 7.6 and DEC-92 define the save boundary; no currency asset exists.
- Expected behavior: as specified in `docs/Hub-And-Player-Data-Design.md` sections 3, 4 and 5: `CurrencyDefinition`/`CurrencyCatalog`/`CurrencyDisplay` in `Runtime/Currencies`; `Coin` (`coin`, start 0, max 999999999, `Amount`) and `Life` (`life`, start 5, max 5, `CountOnIcon`, full label `Full`) assets in that catalog order; `BoosterDefinition` price names its currency (`m_purchasePrice` with `FormerlySerializedAs("m_purchaseCoinPrice")`, `m_purchaseCurrency`), all five booster assets point at `Coin`; `ColorBlockJam.Player` holds `PlayerSnapshot`, `PlayerSnapshotJson` (format 1, `TypeNameHandling.None`), `IPlayerSnapshotStore`, `FilePlayerSnapshotStore` (encrypt, temp write, flush, replace `player-save.dat` with `.bak`) over `IFileOperations`, `PlayerSaveCipher` (AES-256-CBC, random IV per save, HMAC-SHA256 encrypt-then-MAC, `CBJS` container, section 4.5) with `PlayerSaveKey.Derive(deviceId)` (PBKDF2-SHA256), `MemoryPlayerSnapshotStore`, `PlayerSaveService` implementing `IWallet`, `IPlayerProgress`, `IBoosterInventory`, `IPlayerSettings`, `IBoosterShop`, `PlayerProgress.GetPlayableLevelIndex`, and `PlayerDataInstaller` on `ProjectContext.prefab` after `ProjectScopeInstaller`.
- Behavior that must remain unchanged: `ProjectScopeInstaller` bindings and its level/shape loading; Init to Hub routing without Loading; direct Play of every runtime scene; booster definition IDs, names, descriptions, icons, charges and prices (10 coins, 1 charge); the `BoosterCatalog` asset and its validation; the Level Editor and all Game-scene behaviour; no gameplay tick allocation.

## Ownership

- Owner/writer role: Hub-and-player-data session (this conversation, "Hub sahnesini oluşturma"), one implementer at a time, then an independent verifier
- Allowed paths (exact or narrow globs):
  - Assets/ColorBlockJam/Runtime/Currencies/**
  - Assets/ColorBlockJam/Runtime/Currencies.meta
  - Assets/ColorBlockJam/Runtime/Boosters/BoosterDefinition.cs
  - Assets/ColorBlockJam/Player/**
  - Assets/ColorBlockJam/Player.meta
  - Assets/ColorBlockJam/Data/Currencies/**
  - Assets/ColorBlockJam/Data/Currencies.meta
  - Assets/ColorBlockJam/Data/Boosters/Definitions/ExtraTime.asset
  - Assets/ColorBlockJam/Data/Boosters/Definitions/Hammer.asset
  - Assets/ColorBlockJam/Data/Boosters/Definitions/Rocket.asset
  - Assets/ColorBlockJam/Data/Boosters/Definitions/TimeFreeze.asset
  - Assets/ColorBlockJam/Data/Boosters/Definitions/Vacuum.asset
  - Assets/ColorBlockJam/Resources/Onity/ProjectContext.prefab
  - Assets/ColorBlockJam/Tests/Player/**
  - Assets/ColorBlockJam/Editor/Data/ColorBlockJamDataSetup.cs (added 2026-09-27 with user approval for defect D-001: booster price field rename and purchase currency only)
  - Assets/ColorBlockJam/Tests/Player.meta
  - Assets/ColorBlockJam/Runtime/ColorBlockJam.Runtime.asmdef (added 2026-09-27 on the user's template-port order: the `ZString` reference only)
  - Assets/ColorBlockJam/Hub/Home/CurrencyText.cs (moved with its .meta to Runtime/Currencies on the user's template-port order)
  - Assets/ColorBlockJam/Hub/Home/CurrencyText.cs.meta
  - Assets/ColorBlockJam/Tests/Hub/EditMode/CurrencyTextTests.cs (moved with its .meta to Tests/Player/EditMode on the same order)
  - Assets/ColorBlockJam/Tests/Hub/EditMode/CurrencyTextTests.cs.meta
  - docs/Hub-And-Player-Data-Design.md
  - docs/verification/player-data/**
  - PROJECT.md
  - production/tasks/TASK-20260926-235915696-6b7f82f4-player-data-currencies/**
- Forbidden paths/actions:
  - Packages/**
  - ProjectSettings/**
  - Assets/ColorBlockJam/Runtime/ColorBlockJam.Runtime.asmdef except its `ZString` reference
  - Assets/ColorBlockJam/Runtime/Composition/ProjectScopeInstaller.cs
  - Assets/ColorBlockJam/Runtime/Levels/**
  - Assets/ColorBlockJam/Runtime/Board/**
  - Assets/ColorBlockJam/Runtime/Boosters/BoosterCatalog.cs
  - Assets/ColorBlockJam/Data/Boosters/BoosterCatalog.asset
  - Assets/ColorBlockJam/Gameplay/**
  - Assets/ColorBlockJam/Hub/** except the CurrencyText move
  - Assets/ColorBlockJam/Scenes/**
  - Assets/ColorBlockJam/Editor/** except the D-001 line in Editor/Data/ColorBlockJamDataSetup.cs
  - Any `m_shapeLibrary` or other `ProjectScopeInstaller` field on ProjectContext.prefab (owned by TASK-20260925-005313341-47831bb7-runtime-shapes-from-json)
  - Hand-editing `.prefab`/`.asset` YAML while the live Editor is reachable
  - Any HTTP client, API or gateway abstraction, server request, cloud project or credential (user decisions 2026-09-27: the store port is the only backend seam)
  - Native keystore/keychain plugins or new crypto packages (managed `System.Security.Cryptography` only)
  - Plastic checkin, shelve or publishing
- Serialized assets: exact paths below
  - Asset: Assets/ColorBlockJam/Resources/Onity/ProjectContext.prefab
    Sole writer: this task's implementer through Unity APIs (add `PlayerDataInstaller`, assign `m_currencyCatalog`, append it to the context's `m_installers` after `ProjectScopeInstaller`)
    Matching `.meta` owner: unchanged
  - Asset: Assets/ColorBlockJam/Data/Currencies/CurrencyCatalog.asset
    Sole writer: this task's implementer (new)
    Matching `.meta` owner: this task (new)
  - Asset: Assets/ColorBlockJam/Data/Currencies/Coin.asset
    Sole writer: this task's implementer (new)
    Matching `.meta` owner: this task (new)
  - Asset: Assets/ColorBlockJam/Data/Currencies/Life.asset
    Sole writer: this task's implementer (new)
    Matching `.meta` owner: this task (new)
  - Asset: Assets/ColorBlockJam/Data/Boosters/Definitions/ExtraTime.asset, Hammer.asset, Rocket.asset, TimeFreeze.asset, Vacuum.asset
    Sole writer: this task's implementer through Unity APIs (assign `m_purchaseCurrency`; `m_purchasePrice` keeps its value)
    Matching `.meta` owner: unchanged
- Other active writers/worktrees checked: 2026-09-27 02:59 local. `TASK-20260925-005313341-47831bb7-runtime-shapes-from-json` (in_progress) waits for user approval to set `m_shapeLibrary` on ProjectContext.prefab; it is not writing now. `TASK-20260924-210432886-2f31f26b-remote-level-json` (ready, not started) owns `ProjectScopeInstaller.cs`, which this task does not touch. No task owns `Runtime/Boosters/**` or `Data/Boosters/Definitions/**`. The live Editor runs other sessions' tests; recheck `mcpforunity://editor/state` and ListAgents before each Editor write.
- Execution dependency: none
- Queue policy: none

## Acceptance scenarios

1. Given `Data/Currencies/CurrencyCatalog.asset`, when inspected, then it lists `Life` then `Coin` with the IDs, amounts, displays and icons of design section 3.1, and `Validate` passes; a catalog with a duplicate ID, an invalid ID, a start above the maximum, a missing icon or a null entry fails with a designer-language message.
2. Given an empty store, when the service loads, then the committed snapshot has `life` 5, `coin` 0, every booster at its `InitialCharges`, level index 0 and all toggles on, and exactly one write happened.
3. Given a saved snapshot that lacks a currency or booster the catalogs now define, when the service loads, then only the missing keys are seeded and written once; existing values are unchanged; a second load writes nothing. A test adds a third, runtime-created currency definition to a catalog and it is seeded without any code change.
4. Given a snapshot JSON with unknown currency or booster keys, when it is loaded and a later write happens, then the unknown keys are written back unchanged; given `formatVersion` 2, the file is not overwritten, defaults load in memory, one error is logged and writes are refused.
5. Given `FilePlayerSnapshotStore`, when a save completes, then `player-save.json` holds the new snapshot and `player-save.json.bak` the previous one; when a fault is injected at the temp write, the flush, or the replace, then a new store instance loads the complete previous snapshot; a corrupt main file loads from `.bak`; a stale `.tmp` is ignored and deleted.
6. Given a subscriber to `GetBalance(coin)`, when a purchase's save is still pending, then the subscriber still sees the old balance; after success both the balance and the stock change; when the save fails, nothing is published and the result is `SaveFailed`.
7. Given 20 coins and Rocket stock 1, when Rocket is purchased, then coins become 10 and stock 2 in one snapshot; with 5 coins the result is `InsufficientFunds` and nothing changes; a null purchase currency or price 0 gives `InvalidOffer`; stock at 99 gives `StockFull`; two purchases started together are serialized and debit twice only if both are affordable from the committed state.
8. Given `IPlayerSettings`, when `SetAsync(Vibration, false)` succeeds, then the value persists across a new service instance on the same store; when the save fails, the committed value is unchanged and the result is `SaveFailed`.
9. Given the Editor, when Play starts from Init, then the service uses `MemoryPlayerSnapshotStore`; a player build uses `FilePlayerSnapshotStore` under `Application.persistentDataPath/player`; the load path calls no Unity API after its first `await`.
10. Given ProjectContext.prefab, when Hub or Game is played directly or through Init, then the project container resolves `IWallet`, `IPlayerProgress`, `IBoosterInventory`, `IPlayerSettings`, `IBoosterShop` and `CurrencyCatalog`, `ProjectScopeInstaller` keeps its existing field values, and `SceneFlowSmokeTests` pass.
11. Given the five booster definition assets, when inspected, then each keeps its price (10) and purchase amount and names `Coin` as its purchase currency; `BoosterCatalog.Validate` passes; a definition with a positive price and no currency fails validation.
12. Given `docs/Hub-And-Player-Data-Design.md` and `PROJECT.md`, when read, then sections 3 to 5 match the shipped types and new decision rows record data-driven currencies, the store seam with the user's no-HTTP-client and no-extra-API-layer decisions, and save encryption (next free DEC numbers after checking PROJECT.md and every contract).
13. Given `FilePlayerSnapshotStore` with a cipher, when a snapshot is saved, then `player-save.dat` starts with `CBJS`, contains no plain JSON text, and two saves of the same snapshot differ (fresh IV); when one byte of the file is flipped, the file is truncated, or a different device key is used, then the file is rejected without an exception reaching the service, `.bak` is tried, and otherwise defaults load with one warning; the cipher round-trips every test snapshot (design section 4.5).
14. Given the user's order of 2026-09-27 to port the CBJ Template currency module's good architecture, when the compact currency formatter is used, then `CurrencyText.FormatCompact` lives in `ColorBlockJam.Runtime.Currencies` (the Runtime assembly references `ZString`) so Hub, shared popups and the Game HUD share it, no `ColorBlockJam.Hub.Home.CurrencyText` remains, Home shows the same text, `CurrencyTextTests` pass in the Player EditMode tests, and PROJECT.md DEC-137 records which template ideas were adopted, queued (`CurrencyAmount`, TASK-20260927-141439926-2949c75f-currency-amount-price) or rejected, with reasons.

## References and constraints

- Story/GDD/ADR/design/log/image: docs/Hub-And-Player-Data-Design.md sections 1 to 5 and 10; docs/Color-Block-Jam-PRD.md 6.8, 7.6 (AC-6.1 to AC-6.14), DEC-9, DEC-10, DEC-12, DEC-30, DEC-78, DEC-85, DEC-92; PROJECT.md Runtime Lifecycle and Architecture Rules; docs/Game-Developer-Case-2026.md 4.3 and 6
- Unity version/platform: 2022.3.62f2; Android and iOS
- Package/architecture constraints: Onity DI/Reactive only; Newtonsoft `com.unity.nuget.newtonsoft-json` 3.2.2 with explicit settings and no `TypeNameHandling`; no new package; no Zenject/UniRx/UniTask; `System.Linq` not needed; plain C# domain logic with `m_camelCase` fields; every async method takes a `CancellationToken`, no `async void`, no fire-and-forget; one-sentence responsibility summary on every class
- Explicit out of scope: Hub UI (tasks 2 to 4), the win reward and level advance, in-game booster debit, the purchase popup UI, a server client or backend, Sprite Atlases, device durability proof (AC-6.12 device half stays a V-D step)

## Verification and risk

- Focused test path/command: MCP `run_tests` EditMode, assembly `ColorBlockJam.Player.EditModeTests`; PlayMode `ColorBlockJam.Tests.PlayMode.SceneFlowSmokeTests`
- Required execution preflight: `unity-cli`
- Required Unity domain workflow: `unity-game-dev`
- Unity compilation/Console evidence: clean compile of all assemblies in the live Editor after `refresh_unity`, no new Console error; staged sources compile before they enter `Assets`
- EditMode: all Player tests pass (scenarios 1 to 9 and 11)
- PlayMode: SceneFlowSmokeTests pass with the new installer (scenario 10)
- Manual/built-player/profiler/device evidence: read back ProjectContext.prefab installer order and field values through the Editor; device fault-injection and kill/relaunch durability are recorded as not run
- Risk: high (new save format, ProjectContext.prefab, new and changed ScriptableObject assets, new assembly and public ports)
- Approval already granted (exact target only): user chose "Tüm Hub + kayıt servisi" and asked for currency ScriptableObjects with a server-ready seam on 2026-09-27; explicit target approval for the serialized assets above is requested with this contract
- Commit/checkin constraint: forbidden
- Current invocation permission: none
- Handoff destination: independent verifier, then /task-cycle or automatic closure

## Stop conditions

- The user has not approved the serialized targets listed above.
- ProjectContext.prefab or a booster definition asset has a live writer, or the runtime-shapes session is applying its approved `m_shapeLibrary` change.
- The change needs `ProjectScopeInstaller.cs`, the Runtime asmdef, a package or a ProjectSettings file.
- A staged compile error would reach the shared Editor.
- Onity cannot run a second installer's async build callback after `ProjectScopeInstaller`'s.

<!-- TASK-LIFECYCLE:START -->
## Lifecycle (managed)

- Task state: closed
- Current phase: closed
- Last cycle verdict: READY_TO_CLOSE
- Contract fingerprint: sha256:c85974337dd47f52f0375899df700987b33cf5f8b0f1801543a94e4dc4340d6b
- Evidence fingerprint: sha256:a6a839dca3b98e4c7853b71efccf0041e3a2f85d2f80cdf27ce8cccb6aac27ce
- Reviewed revision: vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f
- Attempt count: 2
- Last feedback: none
- Blocker: none
- Next action: None
- Closed revision: vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f
- Closed at: 2026-09-27T14:24Z
- Closure mode: criteria_verified
- Closed by: explicit_user_confirmation
- Superseded by: none

### Acceptance criteria

| Criterion | Result | Verification | Freshness |
|---|---|---|---|
| AC-1 | PASS | automated: CurrencyCatalogTests in Player EditMode (147/147 d6a27977) + live readback of CurrencyCatalog.asset | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |
| AC-2 | PASS | automated: PlayerSaveServiceTests.AnEmptyStoreIsSeededWithOneWrite | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |
| AC-3 | PASS | automated: OnlyMissingKeysAreSeededOnceAndANewCurrencyNeedsNoCode | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |
| AC-4 | PASS | automated: PlayerSnapshotJsonTests + UnknownKeysSurviveALaterWrite + newer-format service/file-store tests | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |
| AC-5 | PASS | automated: FilePlayerSnapshotStoreTests (atomic write, .bak, faults at temp/flush/replace, corrupt main, stale tmp); file is player-save.dat per AC-13/design 4.3 | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |
| AC-6 | PASS | automated: APurchasePublishesOnlyAfterItsSaveCommits, AFailedPurchaseSavePublishesNothing | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |
| AC-7 | PASS | automated: PurchaseOutcomesChangeNothingUnlessPurchased, PurchasesStartedTogetherRunOneAtATime | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |
| AC-8 | PASS | automated: ASettingPersistsAcrossServicesAndAFailedSaveKeepsTheCommittedValue | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |
| AC-9 | PASS | manual: isolated-copy PlayMode probe resolved MemoryPlayerSnapshotStore (Editor half); player-build branch by code review | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |
| AC-10 | PASS | automated: workspace SceneFlowSmokeTests 6/6 (job a7b94758f74f431a94ce1ddfb3489e44) with ProjectContext m_shapeLibrary assigned; ProjectContext.prefab readback and isolated-copy port-resolve probe 3/3 | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |
| AC-11 | PASS | automated: EveryShippedBoosterPaysTenCoinsForOneChargeAndTheCatalogValidates, ABoosterWithAPriceAndNoCurrencyFailsValidation + asset readback | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |
| AC-12 | PASS | manual: PROJECT.md DEC-128/129/130 and design sections 3-5 reviewed by the independent verifier | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |
| AC-13 | PASS | automated: cipher tests (CBJS container, fresh IV, tampered/truncated/foreign rejected, stable device key, backup then defaults) | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |
| AC-14 | PASS | automated: EditMode Player+Hub+UI 147/147 (d6a27977), CurrencyTextTests 19/19 as ColorBlockJam.Tests.PlayerData (31b3fee3), Hub PlayMode 14 passed + 2 captures skipped by design (2312bcc0), 0 error CS; no Hub.Home.CurrencyText remains; PROJECT.md DEC-137 and design 3.2/3.4 | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |

Each criterion may be verified by an automated test, manual verification, or
explicit user acceptance. Record the channel in `Verification`; explicit user
acceptance does not require a test log, screenshot, artifact, or extra proof.

### Defect ledger

| ID | Dedupe key | Status | Relation | Maps to | Symptom / repro | Latest evidence | Verified revision |
|---|---|---|---|---|---|---|---|
| [D-001](defects/D-001.md) | task_regression:booster-price-rename:data-setup-writes-m_purchasecoinprice | VERIFIED | TASK_REGRESSION | BoosterDefinition price rename | Create Default Assets with a missing booster asset throws on FindProperty("m_purchaseCoinPrice") and sets no purchase currency | isolated-copy verifier: D001-RESULT PASS (Rocket.asset recreated, price 10, currency Coin, both validations True, 0 error CS) | vcs:plastic:cs:13@ColorBlockJamCase2026@efurk64@unity+owned-sha256:af02245fbc33234ae85161103d47789be9144b66b265751c069a98659922ca7f |

### Transition history

- 2026-09-27 — Created — planning — contract authored from the user's Hub, save service and currency requests
- 2026-09-27 — Revised before any execution — planning — user asked to encrypt the local save file (AC-13, design 4.5) and to keep the backend seam SOLID with no extra API layer
- 2026-09-27 — Approval recorded — planning — the user approved every serialized target listed in this contract ("Hepsini onayla"), and the portrait lock plus UI atlases ("İkisini de şimdi ekle")
- 2026-09-27 — Started — implementation — direct implement-task; unity-cli READY WITH WARNINGS (live Editor shared, running other sessions' tests); unity-game-dev loaded; lifecycle lock acquired
- 2026-09-27 — Verified — verification — implementer wf_01b26e8a-b6f; independent verifier: all 13 criteria PASS (Player EditMode 58/58; isolated-copy regression EditMode 1267/1268, the failure is another session's LevelTestPlayRoundTripTests on null m_shapeLibrary); verifier found one task-caused latent regression outside ownership; direct lock released for task-cycle
- 2026-09-27 — task-cycle — waiting — NEW_DEFECT D-001 (TASK_REGRESSION) recorded BLOCKED; repair path outside ownership -> CONTRACT_CHANGE_REQUIRED
- 2026-09-27 — Contract change — waiting — the user approved adding Editor/Data/ColorBlockJamDataSetup.cs for D-001 ("Evet, T1'e ekle ve düzelt"); authority outside the lifecycle block changed, so the contract fingerprint is recomputed
- 2026-09-27 — D-001 fix — in_progress — CreateDefinition writes m_purchasePrice and assigns Data/Currencies/Coin.asset as m_purchaseCurrency (throws a clear error when Coin is missing); out-of-Editor compile of ColorBlockJam.Editor OK; row FIXED_UNVERIFIED
- 2026-09-27 — task-cycle — waiting — D-001 VERIFIED by an independent isolated-copy run (D001-RESULT PASS); reviewed revision recomputed with the D-001 file; AC-10 set UNPROVEN (workspace dependency on runtime-shapes); lock released
- 2026-09-27 — Contract change — in_progress — user order "iyi olabilecek mimari olarak olanları bize geçir sonra taski kapat": scenario 14 moves the compact formatter to Runtime/Currencies (Runtime gains the ZString reference); the template's CurrencyAmount is queued as TASK-20260927-141439926-2949c75f-currency-amount-price behind the live booster-gameplay-flow owner of BoosterDefinition.cs and PlayerSaveService.cs. The stored contract fingerprint did not match the canonical recomputation (no foreign write since this session's 05:17 lifecycle write); it is recomputed here
- 2026-09-27 — Verified — verification — formatter moved to Runtime/Currencies with its .meta GUIDs; AC-10 PASS in the workspace (SceneFlowSmokeTests 6/6), AC-14 PASS; D-001 and AC-1..9, 11..13 carried (their files unchanged; Player EditMode green at this revision)
- 2026-09-27 — task-done — closed — user close order; criteria_verified (14/14 PASS, D-001 VERIFIED)
<!-- TASK-LIFECYCLE:END -->
