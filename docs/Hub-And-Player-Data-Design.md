---
title: "Hub Scene and Player Data: Design"
created: 2026-09-27
modified: 2026-09-28
type: design
status: active
tags: [hub, home, settings, pre-level, currency, save, ui-toolkit, onity]
---

# Hub Scene and Player Data: Design

This is the build spec for the Hub scene (Home, Settings, pre-level popup) and the
player data it reads and writes (currencies, booster stock, level progress, toggles).
It turns the [PRD](Color-Block-Jam-PRD.md) sections 7.1, 7.2, 7.5 and 7.6, the
[visual references](Color-Block-Jam-Visual-References.md) 01 and 06 to 10, and the
user's 2026-09-27 answers into types, files and rules. Paths are relative to
`Assets/ColorBlockJam/` unless noted.

## 1. User decisions (2026-09-27)

| Question | Answer | Effect |
| --- | --- | --- |
| Scope of the Hub work | Whole Hub plus the save service | Four tasks in order: player data, Home, Settings, pre-level and launch (section 12). |
| Tab order | Reference 06 layout | Leaderboard, Shop (locked), Home (active), Collection (locked), Support. |
| Currency structure | One ScriptableObject per currency, easy to add more, ready for a server | `CurrencyDefinition` assets in a `CurrencyCatalog`; one storage port (section 4.4). |
| Server support depth | "No requests to a server; only the place ready, with an example of how it would connect. No HTTP client." Then: "A backend can be added when possible by keeping it SOLID; an extra API layer is unnecessary." | Only the `IPlayerSnapshotStore` seam and a short sketch (section 5). No server, client or API-layer code ships. NG-5 (no backend) stands. |
| Local save safety | "Encrypt and decrypt the local save file; remote is safe but local is not." | AES-256 plus HMAC-SHA256 on the save file (section 4.5). |

Case fit: the brief asks for a working coin inventory whose count stays correct
across level exits (brief 4.3), a Home screen with placeholder tabs, a level button
and top UI (4.1), and Settings toggles with on/off states (4.2). It rewards
separation of data, logic and view, low coupling and easy extension (brief 6).
Everything below serves those points and stays offline.

## 2. Assemblies and folders

| Assembly | Folder | Holds | References |
| --- | --- | --- | --- |
| `ColorBlockJam.Runtime` (existing) | `Runtime/Currencies/` (new folder) | `CurrencyDefinition`, `CurrencyCatalog`, `CurrencyDisplay` authoring data | unchanged asmdef |
| `ColorBlockJam.Runtime` (existing) | `Runtime/Levels/LevelLaunchRequest.cs` (task 4) | the typed Hub-to-Game scene payload | unchanged asmdef |
| `ColorBlockJam.Player` (new) | `Player/` | player snapshot, JSON mapping, stores, `PlayerSaveService`, read/write ports, `PlayerDataInstaller` | `ColorBlockJam.Runtime`, `Onity.DI`, `Onity.Unity`, `Onity.Reactive` (Newtonsoft is a precompiled auto-referenced DLL) |
| `ColorBlockJam.Presentation` (existing) | `Presentation/UI/` (new folder) | `SafeAreaPadding` (reused later by the Game HUD) | unchanged asmdef |
| `ColorBlockJam.UI` (new, DEC-136) | `UI/` (next to `Common/` and `Themes/`) | popups both Hub and Game use: `Settings/ISettingsPopupView`, `SettingsPopupView`, `SettingsPopupPresenter`, `SettingsPopup.uxml`/`.uss`; `Purchase/IBoosterPurchaseView`, `BoosterPurchaseView`, `BoosterPurchasePresenter`, `BoosterPurchasePopup.uxml`/`.uss` (task 4) | `ColorBlockJam.Runtime`, `ColorBlockJam.Player`, `ColorBlockJam.Haptics`, `Onity.Reactive`, `PrimeTween.Runtime` (no Hub, Gameplay, Presentation or scene reference) |
| `ColorBlockJam.Hub` (new) | `Hub/` | `HubSceneInstaller`, `HubScreen`, Home views and presenters, the `SettingsOverlay` and `BoosterPurchaseOverlay` adapters, `PreLevelPresenter`/`PreLevelView`, `HubNavigator`, `HubBackInput`, `LevelLauncher`, Hub UXML/USS | `ColorBlockJam.Runtime`, `ColorBlockJam.Player`, `ColorBlockJam.Presentation`, `ColorBlockJam.UI`, `Onity.DI`, `Onity.Unity`, `Onity.Reactive`, `Unity.InputSystem`, `ZString` |
| `ColorBlockJam.Hub.Editor` (DEC-169) | `Editor/Hub/` | the navigation catalog Inspector and its tab entry drawer (section 7.1) | `ColorBlockJam.Hub`, `ZString`; `includePlatforms: [Editor]` |
| `ColorBlockJam.UI.EditModeTests` (new) | `Tests/UI/EditMode/` | shared popup presenter, view and asset tests | UI, Player, Runtime, Onity.Reactive, test runner; `includePlatforms: [Editor]` |
| `ColorBlockJam.Player.EditModeTests` (new) | `Tests/Player/EditMode/` | player data tests | Player, Runtime, Onity.Reactive, test runner; `includePlatforms: [Editor]` |
| `ColorBlockJam.Hub.EditModeTests` / `.PlayModeTests` (new) | `Tests/Hub/EditMode/`, `Tests/Hub/PlayMode/` | presenter and scene tests | Hub, UI, Player, Runtime, Presentation, Onity.*, Input System, test runner |

Why three new runtime assemblies (PROJECT.md DEC-99 allows a split only for a real
boundary):

- `ColorBlockJam.Player` is the only code that owns durable player files. Both Hub
  and Gameplay read it; neither may write save files. Runtime stays free of
  persistence and reactive state, and the Runtime asmdef does not change.
- `ColorBlockJam.Hub` is the Hub scene's composition root, the counterpart of
  `ColorBlockJam.Gameplay` for the Game scene. Runtime never references it.
- `ColorBlockJam.UI` holds the popups that both scenes show (DEC-136): Game cannot
  reference Hub, so the Settings popup that the pause menu reuses (DEC-135) cannot live
  in Hub. Scene hosts adapt it; it knows no scene, navigator or session.

References point inward: Hub -> UI -> Player -> Runtime; Hub -> Presentation -> Runtime.
Gameplay will reference UI for the pause menu. No cycle. `CurrencyDefinition` sits in Runtime because `BoosterDefinition` (Runtime)
names its purchase currency and Runtime cannot reference Player.

UI assets that are not code:

- `Hub/UI/` — `Hub.uxml` (root), `Home.uxml`, `PreLevel.uxml` and one USS per screen;
  `Hub.uxml` instances the shared `UI/Settings/SettingsPopup.uxml` and
  `UI/Purchase/BoosterPurchasePopup.uxml`.
- `UI/Common/Common.uss` — shared tokens, text styles and the press feedback class.
- `UI/Themes/ColorBlockJamRuntime.tss` — imports Unity's default runtime theme and
  `Common.uss`, sets the default font.
- `Data/UI/HubPanelSettings.asset` — the Hub `PanelSettings`.
- `Data/UI/Fonts/LiberationSans-UITK.asset` — UI Toolkit `FontAsset` made from the
  TextMeshPro essentials `LiberationSans.ttf` (DEC-39: a package font, not an import).
- `Data/Currencies/` — `CurrencyCatalog.asset`, `Coin.asset`, `Life.asset`.

## 3. Currencies

### 3.1 Definitions

`CurrencyDefinition : ScriptableObject` (`Runtime/Currencies/CurrencyDefinition.cs`,
menu `Color Block Jam/Currencies/Currency Definition`):

| Field | Type | Rule |
| --- | --- | --- |
| `m_id` | string | Stable save key, for example `coin`. Lowercase letters, digits and `-`, 1 to 32 characters. Never rename a shipped ID (it is a save-data key). |
| `m_displayName` | string | Designer name, for example `Coins`. Required. |
| `m_icon` | Sprite | Required. |
| `m_startingAmount` | int | 0 or more. Granted once, the first time a save lacks this ID. |
| `m_maxAmount` | int | At least 1 and at least `m_startingAmount`. Upper bound for every balance change; a grant that would pass it is refused (overflow). |
| `m_homeDisplay` | `CurrencyDisplay` | `Amount` shows the compact balance in the holder (Coins). `CountOnIcon` shows the count on the icon and the status text in the holder (Lives). |
| `m_fullLabel` | string | Holder text while `CountOnIcon` and the balance equals the maximum, for example `Full`. Empty for `Amount`. |
| `m_showOnHome` | bool | Whether the Home top bar shows a holder for it. |

`Validate(out string error)` returns designer-language messages; `IsValidId(string)` and
`MaxIdLength` (32) expose the ID rule. `CurrencyDisplay` is `Amount = 0`,
`CountOnIcon = 1` (stored by value; never renumber). `CurrencyCatalog`
(`Runtime/Currencies/CurrencyCatalog.cs`, menu `Color Block Jam/Currencies/Catalog`)
holds an ordered `CurrencyDefinition[]`; order is the Home top-bar order. It exposes
`Count`, `GetAt(int)`, `TryGet(string id, out CurrencyDefinition)` and `Validate`
(non-empty, no null entry, every entry valid, no duplicate ID).

Shipped assets:

| Asset | ID | Start | Max | Display | Full label | Icon |
| --- | --- | --- | --- | --- | --- | --- |
| `Data/Currencies/Life.asset` | `life` | 5 | 5 | `CountOnIcon` | `Full` | `Art/UI/Supplied/Icons/Currency/ic_heart.png` |
| `Data/Currencies/Coin.asset` | `coin` | 0 | 999999999 | `Amount` | (empty) | `Art/UI/Supplied/Icons/Currency/ic_coin_single.png` |

Catalog order: Life, Coin (reference 06 shows the life holder left of the coin holder).

### 3.1.1 Timed refill and lives (DEC-174)

Lives are the `life` currency with two optional refill settings every currency has:

| Field | Life | Coin | Meaning |
| --- | --- | --- | --- |
| `Refill Seconds` | 1800 | 0 | One unit is added per interval of UTC wall-clock time below the maximum, also while the app is closed. 0 = no timed refill. |
| `Refill Price` / `Refill Price Currency` | 1 / Coin | 0 / none | One paid refill to the maximum. 0 = no offer. |

- The snapshot keeps, per refilling currency, the UTC second its next unit started
  (`currencyRefillStarts` in the save JSON, section 4.2). The count is computed from that
  time when read (`CurrencyRefill.Advance`, plain C#), so nothing ticks and no write
  happens until a unit is due. At the maximum no timer runs.
- `PlayerSaveService` stays the only writer: `TakeAsync` (never below 0, starts the timer),
  `BuyRefillAsync` (price and refill in one snapshot), `RefreshRefillsAsync`, and
  `AddAsync` all advance due refills first. Load applies time spent closed.
- `ILives` / `LivesService` (`Runtime/Player/Lives`) is a stateless adapter that picks the
  `life` currency for Hub and Game.
- Rules: a failed attempt, the in-level Retry's confirm and HOME from a level in progress
  each cost one life, at most one per attempt (`AttemptLifeCost` in `GameMenuFlow`); HOME
  after Won is free. The Home Level button opens the refill popup instead of the pre-level
  popup at 0 lives, and Hub opens that popup when it shows with no life left. With 0 lives
  after a charge, Retry returns to Hub instead of restarting.
- The Home life holder shows the count on the icon, `Full` at the maximum and `mm:ss` to
  the next life below it. `HubScreen` runs one UI Toolkit scheduled item every 250 ms
  while Home is bound and pauses it on unbind; text changes once per shown second.
- The refill popup (`Runtime/Hub/Lives/LifeRefillOverlay`) reuses the shared purchase
  popup layout as a second `BoosterPurchasePopup.uxml` instance (`life-refill-instance`
  in `Hub.uxml`) and opens as the `life-refill` menu on the Hub overlay stack.
- Limit: the device clock drives refills, so a player who moves it moves refills;
  tamper-proof time needs a server (the store seam of section 5).

### 3.2 Prices name their currency

`BoosterDefinition` (Runtime) replaces the coin-only price:

- `[FormerlySerializedAs("m_purchaseCoinPrice")] [SerializeField, Min(0)] int m_purchasePrice = 10;`
- `[SerializeField] CurrencyDefinition m_purchaseCurrency;`
- properties `PurchasePrice`, `PurchaseCurrency`; `PurchaseCoinPrice` is removed (no caller).
- `Validate` requires a purchase currency when the price is above 0.

The five booster assets under `Data/Boosters/Definitions/` get `m_purchaseCurrency` =
`Coin.asset` in the same change, so the project scope's booster-catalog validation
never sees a gap. Code never looks a currency up by a literal ID.

The editor menu `Editor/Data/ColorBlockJamDataSetup.cs` writes `m_purchasePrice` = 10
and `m_purchaseCurrency` = `Coin.asset` when it recreates a missing booster asset
(player-data defect D-001).

Queued (DEC-137): the price becomes one `CurrencyAmount` (currency + amount) with a
one-row Inspector drawer, which rewards will reuse; task
`TASK-20260927-141439926-2949c75f-currency-amount-price` waits for the live owner of
`BoosterDefinition.cs` and `PlayerSaveService.cs`.

### 3.3 Adding a currency

1. `Create > Color Block Jam > Currencies > Currency Definition` in `Data/Currencies/`.
2. Set ID, name, icon, starting amount, maximum and display.
3. Add it to `Data/Currencies/CurrencyCatalog.asset`.
4. For `Show On Home`, add an instance of `CurrencyHolder.uxml` under `currency-holders`
   in `Hub/UI/Home.uxml`, named `currency-holder-<id>` (DEC-143).

No other code changes. On the next launch the save service seeds its starting amount
once and prices can name it. `HomeView` finds the authored holder by that name and
`HomePresenter` shows it when `Show On Home` is set; a `Show On Home` currency with no
matching holder logs one designer-language error naming the ID and `Home.uxml`, and its
balance is not shown until a holder is authored for it.

### 3.4 Display text

`CurrencyText.FormatCompact(int)` (`Runtime/Currencies/CurrencyText.cs`, ZString) is the
one compact amount rule for every screen: Home holders, the shared popups of
`ColorBlockJam.UI` and the Game HUD. It lives with the definitions, as the CBJ
Template keeps its `CurrencyHelper` in the currency module (DEC-137); the Runtime
assembly references ZString for it.

## 4. Player data and the save service (DEC-92)

### 4.1 Snapshot

`PlayerSnapshot` (`Player/PlayerSnapshot.cs`) is an immutable plain C# value:

- `Revision` (long, +1 per commit), `CurrentLevelIndex` (int, 0 or more),
- currency balances by currency ID, booster stock by `BoosterId`,
- `PlayerSettings` (`Music`, `Sound`, `Vibration`, all default on; an immutable
  `readonly struct` with `Get`/`With` by `PlayerSettingId`),
- `With...` methods return a new snapshot; nothing mutates in place.

Balances and stock are stored by string key (currency ID, `BoosterId` name) so keys
this build does not know survive; `TryGetCurrency` and `TryGetBoosterStock` read them.
`PlayerSnapshot.Empty` is the never-saved value (revision 0).

### 4.2 JSON format (version 1)

`PlayerSnapshotJson` maps the snapshot to this JSON with explicit Newtonsoft settings
(`TypeNameHandling.None`, no converters by type name, invariant culture). On disk the
JSON is encrypted and authenticated (section 4.5); in memory and in tests it is plain:

```json
{
  "formatVersion": 1,
  "revision": 3,
  "currentLevelIndex": 0,
  "currencies": { "life": 5, "coin": 20 },
  "boosterStock": { "TimeFreeze": 1, "Rocket": 0, "Hammer": 1, "Vacuum": 1, "ExtraTime": 2 },
  "settings": { "music": true, "sound": true, "vibration": false }
}
```

- Booster keys are `BoosterId` names. Keys the current build does not know are kept
  in memory and written back unchanged, so an older build never deletes newer data.
- A missing currency or booster key means "never seeded": load adds the currency's
  starting amount or the booster's `InitialCharges` once, then commits. Present keys
  are never reseeded (AC-6.7, DEC-78).
- `formatVersion` above the supported version: the file is not overwritten; the
  service loads defaults in memory, logs one error and refuses writes (no data loss).
  `PlayerSnapshotJson.Read` signals it with `PlayerSnapshotVersionException`; the file
  store does not fall back to `.bak` in that case, so an older backup can never be
  written over the newer file.
- Negative, non-integer or out-of-range values, a missing field, a case alias of a
  known field (`Revision`) and trailing content fail validation with
  `InvalidDataException`; the loader falls back to the backup file (section 4.3) and
  then to defaults, logging one warning. Unknown top-level fields are ignored.
  Settings keys may be missing (default on) so later toggles can be added.
- Optional `"currencyRefillStarts": { "life": 1790600000 }` (DEC-174): UTC Unix seconds at
  which each refilling currency's next unit started (section 3.1.1). It is written only
  while a timed currency is below its maximum and may be missing, so saves written before
  it load unchanged and `formatVersion` stays 1. Values must be whole numbers of 0 or more.
- `currentLevelIndex` is stored as saved; a win raises it to at least won + 1 (DEC-159). It
  keeps counting past the last database level: `PlayerProgress.GetPlayableLevelIndex` no
  longer clamps it, and the Game scene plays the database entry
  `PlayerProgress.GetDatabaseIndex(index, levelCount)` = index modulo the level count, so
  Level 6 of five levels plays level 1 and still reads "Level 6" (DEC-44, DEC-173,
  superseding DEC-8).

### 4.3 Stores

```csharp
public interface IPlayerSnapshotStore
{
    // Returns null when no snapshot was ever written or none can be read;
    // throws PlayerSnapshotVersionException for a newer format.
    Task<PlayerSnapshot> LoadAsync(CancellationToken cancellationToken);
    // Completes only after the snapshot is durable; throws on failure.
    Task SaveAsync(PlayerSnapshot snapshot, CancellationToken cancellationToken);
}
```

- `FilePlayerSnapshotStore(string directory, IFileOperations files, PlayerSaveCipher cipher)`
  — players, under `Application.persistentDataPath/player/`.
  Save: serialize to JSON, encrypt (section 4.5), write `player-save.dat.tmp`, flush
  to disk, then replace `player-save.dat` keeping the previous file as
  `player-save.dat.bak` (`File.Replace`; first save uses `File.Move`). Load: read and
  decrypt the main file; if it is missing, fails authentication or is invalid, read
  `.bak`; ignore and delete a stale `.tmp`. File work runs on the thread pool
  (`Task.Run`) and checks the token before it starts; a started save finishes, and the
  replace is its commit point. Any fallback (backup used, or defaults because neither
  file reads) logs exactly one warning from the store and returns the backup or null,
  so no exception reaches the service. `Debug.LogWarning` is the one Unity API this
  worker-thread code calls (Unity documents the log as thread-safe). The contract's AC-5 wording `player-save.json` predates the
  encryption revision; the shipped name is `player-save.dat`.
- `IFileOperations` (exists, read, write-and-flush, replace, move, delete, create
  directory) exists so tests can fail each step (AC-6.12 fault injection).
  `SystemFileOperations` is the only production implementation; its write flushes with
  `FileStream.Flush(true)`.
- `MemoryPlayerSnapshotStore` — Editor Play Mode and tests (RULE-25, DEC-30, AC-6.6).
  It keeps plain JSON text and runs the same `PlayerSnapshotJson` rules, so unknown
  keys and version refusals behave as on a device. The installer picks it when
  `Application.isEditor`.
- The installer reads `Application.persistentDataPath` in `InstallBindings` (main
  thread). No store or service code calls a Unity API after an `await`: Onity runs
  build callbacks with `ConfigureAwait(false)`, so a callback can resume off the main
  thread.

### 4.4 Service and ports

`PlayerSaveService : IDisposable` (`Player/PlayerSaveService.cs`) is the single owner
of the committed snapshot and of every write. It implements five small ports so each
screen depends on only what it uses:

| Port | Members | Used by |
| --- | --- | --- |
| `IWallet` | `IReadOnlyReactiveProperty<int> GetBalance(CurrencyDefinition)`; `bool CanAfford(CurrencyDefinition, int)` | Home, purchase popup, later HUD and win popup (AC-6.4) |
| `IPlayerProgress` | `IReadOnlyReactiveProperty<int> SavedLevelIndex` | Home, pre-level |
| `IBoosterInventory` | `IReadOnlyReactiveProperty<int> GetStock(BoosterId)` | pre-level, later HUD |
| `IPlayerSettings` | `IReadOnlyReactiveProperty<bool> Get(PlayerSettingId)`; `Task<PlayerSaveResult> SetAsync(PlayerSettingId, bool, CancellationToken)` | Settings |
| `IBoosterShop` | `Task<BoosterPurchaseResult> PurchaseAsync(BoosterDefinition, CancellationToken)`; `IReadOnlyReactiveProperty<bool> IsSaving` | purchase popup |

Rules:

1. **Load once.** `LoadAsync` runs in the project scope build (installer order:
   `ProjectScopeInstaller`, then `PlayerDataInstaller`), before Init routes to Hub.
2. **Prepare, commit, publish.** Every write computes the next snapshot from the
   committed one, awaits `SaveAsync`, and only then replaces the committed snapshot
   and publishes changed reactive values. A failed save publishes nothing and returns
   a failure result; nothing is applied twice on retry because nothing was applied.
3. **One writer at a time.** Writes are serialized with one `SemaphoreSlim(1, 1)`.
   Each operation reads the committed snapshot inside the lock. `IsSaving` is true
   while a write is pending; presenters disable Buy while it is true.
4. **Results, not exceptions,** for expected outcomes:
   `PlayerSaveResult { Saved, SaveFailed, Cancelled }`;
   `BoosterPurchaseResult { Purchased, InsufficientFunds, InvalidOffer, StockFull, SaveFailed, Cancelled }`.
   Purchase = one snapshot with the price debited and `PurchaseAmount` granted
   (AC-6.7); stock above 99 is `StockFull`; a null currency or price 0 is `InvalidOffer`.
5. **Owner and stop point.** The service owns one `CancellationTokenSource`; the
   project container disposes the service at the app scope end, which cancels waits.
   A write that already started completes; its result is ignored after disposal.
   Callers pass their own scene token; a Hub view that is gone ignores the result.
6. **Main thread publication.** The service awaits without `ConfigureAwait(false)`,
   so publication returns to the caller's context (the main thread for UI calls).
7. **No Playing work.** Nothing here runs in a gameplay tick. In-game booster debits
   and the win reward are later Game-scene tasks built on the same commit path.

Shipped details:

- `IsSaving` turns true when a write is submitted (including one waiting for the lock)
  and false when none is pending; a rejected request toggles it only for the duration
  of its synchronous check.
- A write before the load finished, or after a refused load, returns `SaveFailed`
  without touching the store. A setting set to its current value returns `Saved`
  without a write.
- Load failures: a newer format or any unexpected store exception keeps defaults in
  memory, logs one error and refuses writes (`WritesRefused`), so the existing save is
  never overwritten; an unreadable save (store returns null) and an
  `InvalidDataException` from a store start from defaults and seed normally. If the
  seeding write itself fails, the seeded values stay in memory and the next successful
  write persists them.
- `GetBalance` and `GetStock` throw for a currency or booster that is not in its
  catalog (a programming error, not a player outcome). `CanAfford` reads the committed
  snapshot. `Committed`, `IsLoaded` and `WritesRefused` expose state for tests and
  diagnostics.
- Stock cap: `MaxBoosterStock = 99`; a purchase whose grant would pass it is
  `StockFull`. `InvalidOffer` also covers a null booster, a booster or currency missing
  from its catalog, and `PurchaseAmount` below 1.

### 4.5 Save encryption (user request 2026-09-27)

The user asked to encrypt the local save: a server keeps its own copy safe, a file on
the phone does not. `PlayerSaveCipher` (`Player/Storage/PlayerSaveCipher.cs`) protects
the bytes between `PlayerSnapshotJson` and the file:

- **Encrypt, then authenticate.** AES-256-CBC with PKCS7 padding and a random 16-byte
  IV per save (`RandomNumberGenerator`), then HMAC-SHA256 over
  `magic | container version | IV | ciphertext`. `AesGcm` is not available on Unity's
  .NET Standard 2.1 profile, so the MAC gives the integrity check.
- **Container:** `"CBJS"` (4 bytes) | container version `1` (1 byte) | IV (16) |
  ciphertext | MAC (32). The JSON keeps its own `formatVersion`.
- **Keys:** `PlayerSaveKey.Derive(string deviceId)` runs PBKDF2-SHA256
  (`Rfc2898DeriveBytes`, 10,000 iterations) over the device ID with a constant app
  salt and splits 64 bytes into a 32-byte encryption key and a 32-byte MAC key. The
  installer reads `SystemInfo.deviceUniqueIdentifier` in `InstallBindings` (main
  thread) and passes it to `PlayerSaveCipher.FromDeviceId`, which derives the key once,
  lazily, on the first encryption or decryption; that happens inside the store's
  thread-pool work, off the main thread. `new PlayerSaveCipher(PlayerSaveKey)` takes a
  fixed key (tests). `PlayerSaveKey` returns copies of its keys.
- **Verify before decrypting.** The MAC is compared in constant time. A wrong magic,
  version, length or MAC, or a padding error, counts as an unreadable file: the store
  tries `.bak`, then the service starts from defaults and logs one warning. Nothing
  throws to a screen.
- **What it protects and what it does not.** It stops reading and hand-editing the
  save and copying it to another device (the key depends on the device ID). The salt
  ships inside the app, so someone with the binary and the device ID can still derive
  the key; tamper-proof currency needs the server authority of section 5. The Android
  ID behind `deviceUniqueIdentifier` stays stable for the same signing key, and on
  iOS the vendor ID only changes after all of the vendor's apps (and this save) are
  removed.
- `MemoryPlayerSnapshotStore` (Editor Play Mode, tests) holds snapshots unencrypted;
  cipher tests use a fixed test key. The IL2CPP smoke build confirms the managed
  `System.Security.Cryptography` types survive stripping (device step).

### 4.6 Installer

`PlayerDataInstaller : MonoInstaller` (`Player/PlayerDataInstaller.cs`) sits on
`Resources/Onity/ProjectContext.prefab` after `ProjectScopeInstaller` in
`m_installers`. Serialized field: `m_currencyCatalog`. It validates the catalog
(throws with the message, like `ProjectScopeInstaller`), binds the `CurrencyCatalog`
with `BindScriptableObject`, binds the store and
`BindInterfacesAndSelfTo<PlayerSaveService>().AsSingle()`, and registers the load as an
async build callback linked to the installer's `destroyCancellationToken`. The
already-bound `BoosterCatalog` reaches the service through constructor injection. A
synchronous build callback resolves the service on the main thread inside `Build()`
(before any async callback), so the async load, which Onity may resume on a worker
thread, only awaits it.

## 5. Where a backend plugs in

User decisions on 2026-09-27: no requests to a server and no HTTP client; "it is
enough that a backend can be added when it is possible, by keeping the design SOLID;
an extra API layer is unnecessary." So the design adds no API or gateway abstraction.
The only seam is the port the service already needs, `IPlayerSnapshotStore`:

- **Dependency inversion.** `PlayerSaveService` depends on `IPlayerSnapshotStore`,
  never on a file or network class. Screens depend only on the section 4.4 ports.
- **Open/closed.** A backend arrives as one new class that implements
  `IPlayerSnapshotStore`, and `PlayerDataInstaller` binds it instead of
  `FilePlayerSnapshotStore`. No screen, port, currency asset or service rule changes.

```csharp
// Sketch for the day a backend exists. Not part of the build (NG-5).
public sealed class BackendPlayerSnapshotStore : IPlayerSnapshotStore
{
    public Task<PlayerSnapshot> LoadAsync(CancellationToken token) { /* read the player's snapshot from the backend */ }
    public Task SaveAsync(PlayerSnapshot snapshot, CancellationToken token) { /* write it; throw on failure so nothing is published */ }
}

// PlayerDataInstaller: container.BindInstance<IPlayerSnapshotStore>(new BackendPlayerSnapshotStore(...));
```

Notes for that day, not for now: the encrypted file store can stay as the offline
copy, and a trusted backend should validate purchases and rewards itself rather than
accept a client-computed snapshot.

## 6. Hub scene composition

### 6.1 Scene objects (`Scenes/Hub.unity`)

```
SceneScope                 (SceneContext, m_installers: [HubSceneInstaller])
├── Main Camera            (unchanged: DEC-118 clear colour)
├── Directional Light      (unchanged)
├── Hub                    (HubSceneInstaller, UIDocument[HubPanelSettings, Hub.uxml], HubScreen)
└── EventSystem            (EventSystem, InputSystemUIInputModule with the package default actions)
```

As shipped (task 2): `HubScreen` also holds the `CurrencyHolder.uxml` template reference,
and `HubSceneInstaller` holds the `HubScreen` reference it shows once the project scope is ready.
DEC-143 removed the template field from `HubScreen`; Unity dropped its stale value from
Hub.unity at the next scene save (DEC-169). `HubSceneInstaller` also holds
`m_navigationCatalog` (`Data/Hub/HubNavigationCatalog.asset`, section 7.1) and throws a
designer-language error when it is missing.

`EventSystem` + `InputSystemUIInputModule` route pointer and Back input to the UI
Toolkit panel through the Input System (AGENTS.md keeps `com.unity.ugui` for this).
No legacy `Input.*` code.

### 6.2 PanelSettings

`Data/UI/HubPanelSettings.asset`: Scale With Screen Size, reference resolution
1080 x 1920, Screen Match Mode **Expand** (the whole reference rectangle always fits:
at 20:9 the width stays 1080 and the height grows to 2400; nothing is cropped), theme
`UI/Themes/ColorBlockJamRuntime.tss`, sort order 0, no target texture, dynamic atlas on.

### 6.3 Startup and ownership

- `HubSceneInstaller` binds `HubNavigator`, the presenters and `LevelLauncher`
  (`AsSingle`; the scene container disposes them when Hub unloads) and registers one
  async build callback. The callback waits for `ProjectContext.Instance.ReadyTask`
  with the same cancellable `Task.Yield` loop as `GameAttemptLoader` (direct Play
  of Hub still works, DEC-98), then calls `HubScreen.Show()`.
- `HubScreen` (thin MonoBehaviour, `[Inject]` fields) creates the view wrappers from
  `UIDocument.rootVisualElement` on `Show()`, binds the presenters to them and applies
  the safe area. `OnDisable` unbinds every presenter, disposes subscriptions and removes
  callbacks. Views never read services; presenters never touch `VisualElement`s.
- Presenters are plain C# with a view interface (`IHomeView`, `ISettingsPopupView`,
  `IPreLevelView`, `IBoosterPurchaseView`), so EditMode tests use fake views.
- Every `Subscribe` result goes to the presenter's `CompositeDisposable`, disposed on
  unbind. Every async call passes `HubScreen.destroyCancellationToken` (or a presenter
  token linked to it).

### 6.4 Navigation and Back

`HubNavigator` keeps one overlay stack (`Settings`, `PreLevel`, `BoosterPurchase`).
Opening pushes and shows; closing pops and hides. Only the top overlay receives
input: each overlay has a full-screen scrim with `pickingMode = Position`, so taps
never reach Home underneath (AC-2.6). `HubBackInput` owns one code-built
`InputAction` (`<Keyboard>/escape`, which Android Back maps to), enabled while Hub
is shown and disposed in `OnDisable`; `performed` asks the navigator to close the top
overlay. With no overlay open Back does nothing on Home.

Tab menus (DEC-169) share this stack. `HubNavigator.RegisterMenu(menuId, overlay)`
registers any `IHubOverlay` under a string ID; `UnregisterMenu` hides and removes it.
`IHubMenuNavigator` (`HasMenu`, `OpenMenu`) is the only part the tab presenter sees.
The stack holds overlay references, so Back closes a menu like any overlay. Settings,
pre-level and purchase keep their `HubOverlayId` routes. Menus use string IDs because a
tab names its menu in data: a new menu needs no enum value and no navigator change. Onity
has no collection injection, so `HubScreen` registers each menu after binding its view
and unregisters it in `OnDisable`.

### 6.5 Safe area and background

`Presentation/UI/SafeAreaPadding` converts `Screen.safeArea` into panel-space padding:
`static SafeAreaInsets Calculate(Rect safeArea, Vector2 screenSize, Vector2 panelSize)`
plus an instance per padded element that recalculates on the full-screen element's
`GeometryChangedEvent` (resolution or orientation change), never per frame. Each instance
pads only its `SafeAreaEdges`: `safe-area` takes the left, top and right insets, and the
`tab-bar` (a sibling below it, so its fill reaches the physical bottom edge as in reference 06)
takes the left, right and bottom insets, which keeps the five tab slots inside the safe area.
`SetSource(Rect, Vector2)` pins an explicit surface instead of `Screen` for a panel that
renders into a texture or a simulated notch (`HubScreen.SetSafeAreaSource`). The background
element is a sibling outside both: `bg_home_screen` with
`-unity-background-scale-mode: scale-and-crop` (DEC-42). All interactive UI is inside the
safe area (AC-1.4a).

### 6.6 Shared styles

`UI/Common/Common.uss`:

- `.cbj-pressable` on every `Button`: `transition-property: scale, -unity-background-image-tint-color; transition-duration: 0.08s;`
  and `.cbj-pressable:active { scale: 0.92 0.92; -unity-background-image-tint-color: rgb(205, 205, 205); }`
  (DEC-14: one shared style, no per-button code; DEC-45: USS transitions for UI).
- Title text: bold, white, `-unity-text-outline-width: 2px`, dark blue outline.
- Nine-slice sprites use `-unity-slice-left/top/right/bottom` equal to their measured
  Sprite Editor borders (`docs/UI-Asset-Import.md`).

## 7. Home screen (task 2, reference 06)

Element tree (`Hub/UI/Home.uxml`), sizes in reference pixels at 1080 width, taken
from reference 06 scaled by 1080/946; the capture check compares proportions. Revised
2026-09-27 by the chief validator review (defects D-001 to D-003 of the Hub Home task):

```
home-screen
├── home-background                     bg_home_screen, cover
├── safe-area                           padded left/top/right
│   ├── top-bar (row, 170 high, margins 40 top / 15 bottom = 225 band)
│   │   ├── profile-button              bg_profile_box_blue, fixed 158x158, press feedback (AC-1.6)
│   │   ├── currency-holders (row)      one authored CurrencyHolder instance per Home currency (DEC-143)
│   │   │   └── currency-holder-<id>    ui:Instance "currency-holder-life", "currency-holder-coin", shown or
│   │   │       │                       hidden by HomePresenter, never added or removed
│   │   │       └── currency-holder     300 wide, margin-left 48 (one rule for N holders)
│   │   │           ├── holder-pill     bg_home_resource_holder sliced, 90 high (slice-scale 0.5px)
│   │   │           │   └── holder-text "Full"/count or compact amount ("0", "950", "1.52k")
│   │   │           ├── holder-icon     definition icon 108, overhanging the pill's right end by 34
│   │   │           │   └── icon-count  CountOnIcon only ("5")
│   │   │           └── plus-button     btn_circle_green_small 66x68 + ic_plus, overhanging bottom-left by 6
│   │   └── settings-button             ic_settings (opens Settings, AC-1.3)
│   ├── level-route (basis 1075, grow 2.1)  decorative, pickingMode Ignore
│   │   ├── route-rails                 two bg_home_line strips tinted gold (#F4C542) with a blue fill between
│   │   └── route-node x 3              current level: btn_level_green 220 square with its number;
│   │                                   the next existing levels (at most 2): btn_settings_normal 220x228
│   │                                   + ic_lock.png 88x105, numbers #A0D6FF
│   ├── level-button                    btn_level_green sliced (slice-scale 0.5px), 490x185, "Level N" (84 px,
│   │                                   #FFF6D6 with a #18511C outline)
│   └── home-gap (basis 229, grow 1)    empty band above the tab bar
└── tab-bar (fill to the bottom edge, padded left/right/bottom)
    └── tab-row (height 200, one slot per catalog tab, section 7.1; icons 150x165; shipped look below)
        ├── tab-leaderboard             Locked, Lock Only: ic_lock.png (undimmed, DEC-141), no label
        ├── tab-shop                    Locked, Dimmed Icon With Lock: ic_shop.png dimmed (#6E7696) + ic_lock.png 84x101
        ├── tab-home                    Available, active: bg_active_tab sliced (slice-scale 0.8px), raised 60 px,
        │                               overflow visible, ic_home 205x224 protruding about 30 px + "Home"
        ├── tab-collection              Locked, Dimmed Icon With Lock: ic_block_collection.png dimmed + ic_lock.png
        └── tab-support                 Level Gated 50, Lock Only: ic_lock.png + "Lvl 50" (thin 2px outline)
```

DEC-141 revises DEC-133's end slots; revised again 2026-09-28 from user feedback on
the live capture ("home dışındakilerin outline'ı biraz fazla ... sadece sağda Lvl 50
yazmalı, home dışındakilerin altında başlık yazmasına gerek yok"). The leftmost and
rightmost tabs show only `ic_lock.png` at the same size as the other tab icons and no
longer show the Leaderboard or Support icon; only the **rightmost** (`tab-support`)
still carries the label `Lvl 50`, now in `.tab__label--level-locked`, which drops back
to `cbj-title`'s own 2px outline (Common.uss) instead of `.tab__label`'s 3px, matching
the reference crop's thinner stroke. The leftmost tab (`tab-leaderboard`) and the two
locked tabs (`tab-shop`, `tab-collection`) show no label at all: each keeps its
`ui:Label` in the tree with empty text instead of removing the element, so the same
`.tab__label` font size and top margin reserve the same line height and the icon above
does not shift (contract AC-1). Both end tabs keep their element names
(`tab-leaderboard`, `tab-support`) as reserved feature slots and the `.cbj-pressable`
feedback; nothing about their geometry, order or navigation (none) changes. Shop and
Collection keep their DEC-86 dimmed-icon-plus-lock look; only their label disappeared.

Vertical bands (reference 06): top bar 225, route, Level button 185, empty gap, tab bar
206 (6 border + 200 row). Controls keep their size; the route and the gap absorb extra
height. Their flex-basis values give the exact 1920 bands (route 1075, gap 229) and the
2.1 : 1 flex-grow shares extra height as the reference does at 2400 (route +325, gap +155),
so the Level button top is at y1300 at 1080 x 1920 and y1625 at 1080 x 2400 (full safe area).
At 1080 wide, two currency holders sit at x242 and x590; the first icon ends at x576, 8 px
before the second plus badge, and the last icon ends at x924, 8 px before the settings gear.

`btn_level_bg_normal` is not used on Home: reference 06 shows one green Level pill.

DEC-143: the currency holders are authored template instances in `Home.uxml`, not
elements `HomeView` instantiates at runtime, so the Editor and UI Builder show the
finished top bar without Play Mode. Each instance carries design-time preview
attributes (`AttributeOverrides`: "Full"/"5" for life, "0" for coin) and a modifier
class (`currency-holder--life`, `currency-holder--coin`) that sets the design-time icon
in `Home.uss`; `HomePresenter` overrides the icon and text once bound, exactly as before.


Behaviour (`HomePresenter`):

- Level label = `ZString.Concat("Level ", index + 1)` with
  `index = PlayerProgress.GetPlayableLevelIndex(saved, levelDatabase.Count)`; updated
  only when the saved index changes (AC-1.2).
- On bind, `HomeView.HideCurrencyHolders()` hides every authored holder, then each
  catalog currency with `Show On Home` is looked up by ID with
  `HomeView.TryGetCurrencyHolder(id, icon, countOnIcon, out holder)`, which shows the
  matching `currency-holder-<id>` element and configures its icon/count-on-icon mode.
  A currency with no matching holder logs one `Debug.LogError` naming the ID and
  `Home.uxml`, and is skipped; an authored holder no `Show On Home` currency claims
  stays hidden. Unbind detaches the balance subscription only: the holders are never
  removed, and the next bind reuses the same authored elements.
- Each shown holder subscribes to `IWallet.GetBalance(definition)` and formats only on
  change: `Amount` uses `CurrencyText.FormatCompact` (section 3.4; below 1,000 as is; then `1.52k`,
  `12.5k`, `125k`, `1.52m`, two significant decimals, never rounding up across a unit);
  `CountOnIcon` shows the count on the icon, and the holder text is `m_fullLabel` at the
  maximum and the count below it (AC-1.7, AC-1.8). Lives stay 5/Full: nothing changes them.
- Level press -> `HubNavigator.OpenPreLevel()` (the pre-level popup, section 9).
  Settings press -> `OpenSettings()` (task 3).
- Profile and both plus buttons: press feedback only, no navigation (AC-1.6).
- Tabs follow the navigation catalog (section 7.1, DEC-169). In the shipped catalog no tab
  names a menu, so every tab press gives feedback only and Home stays active (AC-1.5).
  Shop and Collection are visibly locked with `ic_lock.png` and a dimmed icon (DEC-86), with
  no label. The leftmost tab is a plain, undimmed `ic_lock.png` (DEC-141). The rightmost
  (`tab-support`) is Level Gated at 50 and carries `Lvl 50`, with a thinner outline than
  the other tab labels (DEC-141, revised 2026-09-28).
- Level button disabled with its label `Level -` when the project scope failed or was
  cancelled, binds no `LevelCatalog` (checked through the binding source, because Onity
  builds any unbound concrete type on demand), or the database is empty.

### 7.1 Tab bar catalog (DEC-169)

`Data/Hub/HubNavigationCatalog.asset` (`HubNavigationCatalog`, menu
`Color Block Jam/Hub/Navigation Catalog`) lists one `HubTabDefinition` per Home tab, in
tab-bar order:

| Field | Rule |
| --- | --- |
| ID | Lowercase letters, digits and `-`, 1 to 32 characters. Home.uxml names the slot `tab-<id>`. |
| Label | Text under the icon while the tab is open; empty shows none but keeps the line height. |
| Icon | Required. Drawn inline while open, and dimmed under the lock for Dimmed Icon With Lock. |
| Availability | `Available` (open), `Locked`, `LevelGated` (locked until the player reaches Unlock Level, then open without a restart) or `Hidden` (not shown). Stored by value: 0, 1, 2, 3. |
| Unlock Level | Level Gated only; 1 or more. |
| Locked Look | `DimmedIconWithLock` (DEC-86) or `LockOnly` (ic_lock.png as the icon, DEC-141). |
| Show Label While Locked | Level Gated shows `Lvl N`; Locked shows its own label. |
| Menu ID | The menu an open tab opens (section 6.4); empty gives press feedback only. |

`Validate` names the broken entry (bad or duplicate ID, missing icon, unlock level below 1,
bad menu ID). The Editor-only `ColorBlockJam.Hub.Editor` assembly shows that message at the
top of the catalog Inspector and hides the fields an entry does not use.

- `HubTabRules` (plain C#) turns an entry and `IPlayerProgress.SavedLevelIndex` into
  `Hidden`, `Unlocked` or `Locked`, and a label.
- `HubTabPresenter` (Hub scene, AsSingle) binds the catalog to the authored slots by ID,
  as DEC-143 binds the holders. On bind it hides every slot, then shows each tab from its
  status. It follows the saved level, so a Level Gated tab opens without a restart, and it
  writes a slot only when that tab's status changes. An open tab with a menu ID calls
  `IHubMenuNavigator.OpenMenu`; a locked tab gives press feedback only. An entry with no
  slot, or a menu ID with no registered menu, logs one designer-language error per bind.
- `HubTabBarView` (thin, UI Toolkit) finds the `tab-<id>` Buttons in `tab-row` once. It
  shows or hides them, draws the catalog icon inline (the atlas sprite), writes the label
  and toggles one state class: `tab--locked` (dimmed icon plus `tab-lock`) or
  `tab--lock-only` (ic_lock.png from USS; the inline icon is cleared). It sets
  `tab--no-divider` on the last shown slot and on both sides of the active tab, so hiding a
  tab leaves no stray divider. Every slot authors the same parts (icon, `tab-lock`,
  label); the classes and texts in Home.uxml only preview the shipped catalog.

Adding a tab: add a `tab-<id>` Button to `tab-row` in Home.uxml with the same three
children (and a design-time icon class if wanted), then add its catalog entry. Adding a
menu: implement it as an `IHubOverlay` with its view, bind it in `HubSceneInstaller`,
register it in `HubScreen` with `m_navigator.RegisterMenu("<menu-id>", menu)` after its
view binds (and `UnregisterMenu` in Unbind), and put the menu ID on the tab. No presenter,
view or navigator code changes.

## 8. Settings (task 3, reference 19, DEC-134, DEC-136)

The Settings popup is a shared component in `ColorBlockJam.UI`: Hub shows it as an
overlay on Home, and the Game pause menu (DEC-135) reuses it with its HOME button shown.
It is an overlay on `Home`, not a session state.

Element tree (`UI/Settings/SettingsPopup.uxml`), sizes in reference pixels at 1080 width
measured from reference 19 scaled by 1080/904:

```
settings-screen                  absolute fill, display none until shown
├── settings-scrim               #0B153D80, pickingMode Position: blocks the host, a tap closes
└── settings-safe-area           padded by the host from its safe area; centres the popup
    └── settings-popup           bg_popup sliced (106/74/107/104), 926 wide, pickable
        ├── settings-holder      bg_popup_info_holder sliced (43/26/42/35), 721 wide
        │   ├── vibration-row    ic_settings_vibration 128 + toggle 212x104, row 150 high
        │   ├── sound-row        ic_settings_sound + toggle
        │   ├── music-row        ic_settings_music + toggle
        │   ├── legal-terms-button        btn_popup_blue sliced (46/69/47/65), 494x199, 22 apart
        │   ├── restore-purchases-button  two-line label
        │   ├── support-button            ic_support 110x77 + "Support"
        │   ├── language-button
        │   └── home-button      btn_popup_green (slice-scale 0.63) + ic_home + "HOME", hidden
        ├── settings-header      bg_popup_header sliced (79/90/79/90), 532x204, 21 above the shell
        │   └── settings-title   "SETTINGS"
        ├── settings-close-button  btn_popup_close 117x121 at the top-right corner
        └── settings-message     save-failure pill over the shell's bottom edge, hidden
```

Slice values are USS order (left, top, right, bottom) and equal the importer borders in
`docs/UI-Asset-Import.md`. The popup is 1665 high with HOME hidden (1886 with it shown), so
it fits a 1080 x 1920 panel and the safe area of 1080 x 2400 with a notch. `bg_menu` is not
used (it was the superseded first design). Label sizes follow reference 19's text widths;
the supplied LiberationSans font (DEC-39) is plainer than the reference's rounded font.

- Toggle (DEC-134, AC-2.1, AC-2.2): a 204 x 78 pill track and a 104 knob. ON: the
  supplied `btn_toggle_on` knob (its baked "On" is the ON label) at the right of a dark
  green track (#005408). OFF: a plain light grey knob at the left of a dark navy track
  (#213976). Position, knob art and track colour all differ, so a greyscale image still
  tells them apart. The knob moves (`translate`) and the two knob faces cross-fade
  (`opacity`) with 0.15 s USS transitions (DEC-45). Supersedes DEC-24's "OFF" label.
- `SettingsPopupPresenter` (plain C#): `Bind(view, hostToken)`, `Unbind`, `Show`, `Hide`,
  `SetHomeVisible(bool)`, events `CloseRequested` and `HomeRequested`. A tap flips the shown
  state at once, then `IPlayerSettings.SetAsync` commits; a failed or cancelled save
  restores the committed value and shows "Couldn't save. Try again." (AC-2.5, DEC-15);
  taps on a toggle are ignored while its save is pending. Each save is an owned task linked
  to the host token and cancelled by `Unbind`; a stale result is ignored. Toggles store and
  show state only (NG-3).
- `SettingsPopupView` wraps the subtree: display, toggle class, message, HOME display, and
  the toggle, close, scrim and HOME clicks. Legal Terms, Restore Purchases, Support and
  Language have no handler: shared press feedback only (brief 4.2, AC-2.4). On open, the
  popup card scales from 0.96 to 1 over 0.15 s with PrimeTween; the scrim does not scale and
  the animation uses unscaled time.
- Hub host: `Hub/Settings/SettingsOverlay` implements `IHubOverlay` over the shared
  presenter, keeps HOME hidden, and maps `CloseRequested` (close button, scrim tap) to
  `HubNavigator.Close(Settings)`. `HubScreen` binds it with `destroyCancellationToken`,
  registers it with the navigator and pads `settings-safe-area` from the safe area; Back
  goes `HubBackInput` -> navigator (AC-2.3, AC-2.3a, AC-2.6).
- Game host (later, DEC-135): the pause flow shows HOME, maps `CloseRequested` to Resume
  and `HomeRequested` to teardown and Hub; toggles commit after Suspend, outside a tick.

## 9. Pre-level popup, purchase and launch (task 4, DEC-138)

As shipped. Types and element names are the contract with the tests
(`Tests/Hub/EditMode/PreLevel*`, `BoosterPurchaseOverlayTests`, `LevelLauncherTests`,
`Tests/UI/EditMode/BoosterPurchase*`, `Tests/Hub/PlayMode/HubLaunchTests`).

### 9.1 Pre-level popup (references 09 and 10)

- Files: `Hub/PreLevel/IPreLevelView` (with `PreLevelSelectorState { Available,
  Selected, OutOfStock }`), `PreLevelPresenter`, `PreLevelView`; `Hub/UI/PreLevel.uxml`/`.uss`,
  instanced in `Hub.uxml`'s overlay layer after Settings.
- Tree: `pre-level-screen` (hidden until shown) > `pre-level-scrim` (pickable, blocks Home,
  does not close) > `pre-level-safe-area` (padded on all edges by `HubScreen`) >
  `pre-level-popup` (`bg_popup`, 956 wide) with `pre-level-holder` (`bg_popup_info_holder`,
  727 wide: "Select Boosters:" and `pre-level-selectors`), `pre-level-play`
  (`btn_popup_green` 404 x 169, slice scale 0.63), `pre-level-header` (`bg_popup_header`
  with `LEVEL N`) and `pre-level-close` (`btn_popup_close`). Shell, header, close and
  holder follow the Settings popup geometry. The level-70 reward panel is omitted.
- On show, the pre-level popup card scales from 0.96 to 1 over 0.15 s with PrimeTween and
  unscaled time; its full-screen scrim stays fixed.
- Every title and button label is a two-layer `.cbj-heavy-text` pair (Common.uss);
  the view writes the same text into both layers.
- Selectors come from `BoosterCatalog` entries whose `UseContexts` has `PreLevel`, in
  catalog order. `BoosterCatalog.asset` lists Extra Time first, so the order is Extra
  Time, Rocket (reference 09) while the in-game order stays Time Freeze, Rocket, Hammer,
  Vacuum. Extra Time shows `ic_clock.png` and its name `Extra Time` below the square (DEC-47,
  AF-16); Rocket shows `ic_rocket_purple.png` and `Rocket`.
- `PreLevelView` builds each selector in code as a rounded square (user feedback
  2026-09-27, references 09/10): the supplied blue `btn_settings_normal` (fixed, 204 x 212)
  with the icon and a red count badge at its lower right (a USS disc with a white rim; no
  supplied sprite has that colour). Selected: the same square tinted green
  (`-unity-background-image-tint-color`; `btn_level_green` was compared and rejected for its
  gold rim) with a `btn_circle_green_small` check badge whose white check is drawn by two
  borders of a rotated box (no check sprite exists). Zero stock: the blue square with an
  `ic_plus` badge on `btn_circle_green_small`, as on Home; a tap opens the purchase popup.
- `PreLevelPresenter` (plain C#, `IHubOverlay`, bound to itself only) shows `LEVEL N` from
  `PlayerProgress.GetPlayableLevelIndex`, the same rule as the Home button. It subscribes to
  each selector's committed stock (one `CompositeDisposable`) and redraws a selector only
  when its look changes. A tap toggles the selection; a zero-stock tap raises
  `PurchaseRequested` and never selects (AC-1.9, DEC-85). Selection is presenter state
  only; opening, toggling and closing spend nothing, and close or Back clears it (AC-1.1a).

### 9.2 Purchase popup (reference 01, shared, DEC-136)

- Shared files in `ColorBlockJam.UI`: `UI/Purchase/IBoosterPurchaseView`,
  `BoosterPurchaseView`, `BoosterPurchasePresenter`, `BoosterPurchasePopup.uxml`/`.uss`.
  They reference Runtime, Player and Onity.Reactive only; the in-game zero-stock purchase
  will host the same view and presenter from its paused overlay.
- Tree: `booster-purchase-screen` > scrim > `booster-purchase-safe-area` >
  `booster-purchase-popup` (`bg_popup`, 866 wide) with the holder (icon box with the icon
  and `x{PurchaseAmount}`, then the description), a reserved message row, the Buy button
  (`btn_popup_green`, "Buy" above the currency icon and price), the header (booster name)
  and the close button.
- `BoosterPurchasePresenter.Show(booster)` reads the price in one place (`TryGetPrice`:
  `PurchasePrice` and `PurchaseCurrency`; the queued `CurrencyAmount` replaces it there)
  and formats numbers with `CurrencyText.FormatCompact`.
- Buy -> `IBoosterShop.PurchaseAsync` with a token linked to the host token; Buy is
  disabled while `IsSaving` is true or its own purchase is pending (no double submit).
  `Purchased`: requests close immediately after the grant commits (AC-6.9). The Hub returns
  to the pre-level popup, which refreshes from committed stock; nothing is auto-selected. The
  in-game host closes the purchase popup and resumes play. No success message is shown.
  `InsufficientFunds`: "Not enough coins" (the currency's name). `InvalidOffer`,
  `StockFull`, `SaveFailed` (and a thrown fault, logged): their own message; nothing
  changes (AC-6.8). A result after the popup closed shows nothing.
- The presenter raises `CloseRequested`; it knows no navigator. The Hub adapter
  `Hub/Purchase/BoosterPurchaseOverlay` (`IHubOverlay`, bound to itself only) opens it
  above the pre-level popup through `HubNavigator`, and maps close to
  `HubNavigator.Close(BoosterPurchase)`. The popups never overlap (user feedback
  2026-09-27, reference 8): `Open(booster, below)` covers the pre-level popup through
  `ICoverableOverlay.SetCovered(true)`, which hides it (scrim included) while it stays on the
  navigator stack, so the purchase popup shows alone over the dimmed Home. Close, Back or
  unregistering uncovers it: the pre-level popup shows again with its title and selection
  kept, and a covered popup ignores Play and selector presses. The Shop stays locked.
- `HubScreen` binds both views, registers `HubOverlayId.PreLevel` and
  `HubOverlayId.BoosterPurchase` explicitly, and connects `PurchaseRequested` to
  `BoosterPurchaseOverlay.Open(booster, preLevelPresenter)`; `OnDisable` undoes each step.
- On open, the purchase card scales from 0.96 to 1 over 0.15 s with PrimeTween and unscaled
  time; the full-screen scrim remains fixed.

### 9.3 Launch

- `LevelLaunchRequest : IOnitySceneEnterData` (`Runtime/Levels/`): the playable
  `LevelIndex` and the selected starting `BoosterId`s in selector order (an immutable copy).
- `Hub/Launch/LevelLauncher` (`ILevelLauncher`) runs one transition through
  `ILevelSceneTransition`; `OnityLevelSceneTransition` calls
  `OnitySceneFlow.TransitionAsync(profile, OnitySceneFlowStateId.Gameplay, request, token)`.
  While a launch is in flight or after it completed, `LaunchAsync` returns
  `LevelLaunchResult.Ignored`; a failed load logs once and allows Play again; Hub unload
  disposes the launcher and cancels the wait (not a started load). `PreLevelPresenter`
  disables Play on the first press.
- `GameAttemptLoader` consumes the request once through
  `OnitySceneTransitionStore.TryConsumeActiveEnterData<LevelLaunchRequest>` before any wait.
  The pure `Gameplay/Composition/LevelLaunchResolution` chooses the request, then the Level
  Editor's Test Level (DEC-127), then the installer's serialized index (direct Play of Game),
  and clamps an out-of-range request to the last level with one warning. This retires the
  Hub half of DEC-116; the foreground-resume interim stays until the pause popup.
- Starting boosters travel in the request, but applying them (prepare, commit the
  debit on Home, opening sequence, AC-5.7) belongs to the booster opening task. Until
  it lands the Game scene ignores the selection, so nothing is spent.
- Boundary (narrowed 2026-09-27, `HubLaunchTests`): Hub and the launcher spend nothing.
  Opening the pre-level popup, selecting or deselecting a booster, and Play all leave
  stock unchanged, including at the instant the one transition starts. The Game-scene
  opening (booster-gameplay-flow, its AC-13/AC-14) is the sole owner of any debit; this
  task's tests assert nothing about stock once the Game scene is ready.

## 10. Lifecycle table

| Work | Owner | Stop point |
| --- | --- | --- |
| Player load | project scope build callback | build token; service disposal |
| Player writes | `PlayerSaveService` semaphore and CTS | completes, or ignored after app scope disposal |
| Hub startup wait | Hub build callback | Hub scene build token |
| Reactive subscriptions | each presenter's `CompositeDisposable` | presenter unbind (`HubScreen.OnDisable`) |
| Geometry/safe-area callbacks | `SafeAreaPadding` instance | `HubScreen.OnDisable` unregisters |
| Back `InputAction` | `HubBackInput` | `HubScreen.OnDisable` disables and disposes |
| Tab clicks and the saved-level subscription | `HubTabBarView`, `HubTabPresenter` | `HubScreen.OnDisable`: presenter unbind, view dispose |
| Button haptics click callback (DEC-156) | `ButtonHaptics` created by `HubScreen` | `HubScreen.OnDisable` detaches it |
| Purchase/settings awaits | presenter, token linked to `HubScreen.destroyCancellationToken` | Hub unload |
| Scene transition | `LevelLauncher` (one in flight) | Hub unload cancels the token |
| UI animation | USS transitions on the elements | element removal |

No `Update`, coroutine, `Invoke`, static event or static mutable state is added.

## 11. Verification

- EditMode: currency catalog/definition validation; snapshot JSON round trip, unknown
  keys preserved, future version refused; file store atomic write, backup recovery,
  stale temp, fault injection at each step (AC-6.12 Editor half); cipher round trip,
  fresh IV per save, tampered byte, wrong key and truncated file rejected, and the file
  on disk contains no plain JSON; service seed-once,
  publish-after-commit, failure publishes nothing, purchase outcomes, serialized writes;
  Home/Settings/pre-level/purchase presenters with fake views; `CurrencyText`,
  `SafeAreaPadding`, `HubNavigator`, launch request resolution; tab rules, tab presenter
  (states, reactive unlock, press routing, error logs), tab bar view classes and dividers,
  navigation catalog validation and Inspector message, navigator menus, and the shipped
  catalog against Home.uxml (DEC-169).
- PlayMode: Hub opens with one `SceneContext`, an `EventSystem` and the named root
  elements; Settings opens and closes; pre-level Play reaches Game with the requested
  index; `SceneFlowSmokeTests` stay green.
- Manual captures at 1080 x 1920 and 1080 x 2400 (with a simulated notch safe area)
  compared with reference 06/09/10; device 20:9 check stays a separate V-D3 step.
- The Profiler shows no Hub script sample per frame while Home is idle.

## 12. Tasks

| Order | Task | Owns |
| --- | --- | --- |
| 1 | `TASK-20260926-235915696-6b7f82f4-player-data-currencies` | sections 3, 4 and 5 |
| 2 | `TASK-20260926-235915697-461b4f9c-hub-home-screen` | sections 6 and 7, `docs/UI-Asset-Import.md` |
| 3 | `TASK-20260926-235915698-c5020d50-hub-settings-overlay` | section 8 |
| 4 | `TASK-20260926-235915699-78356740-hub-pre-level-launch` | section 9 |

Follow-ups outside these tasks: UI Sprite Atlases (Performance Rule 2, NFR-P6; needs
the `EditorSettings` sprite packer mode, a ProjectSettings change) and a portrait-only
orientation lock (`ProjectSettings.asset` currently allows auto-rotation to landscape).
Both need explicit approval of their ProjectSettings targets.
