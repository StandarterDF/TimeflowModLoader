# 10 — Full API reference (index + map)

This chapter answers the question *"what's in the game, and how do I find any given member?"* It is
the entry point to a **generated, exhaustive index** of the whole `Assembly-CSharp.dll`, plus a
curated map of the systems you'll actually touch.

> Goal of this chapter: **any mod should be buildable from here.** You get (1) a complete, greppable
> index of every type/member, (2) a namespace map, (3) a subsystem catalog of the important types,
> and (4) a repeatable workflow for answering new questions.

---

## A. The complete, generated index

`tools/ApiIndexer` is a small Mono.Cecil console tool (outside the solution) that reads the game
assembly and emits three files:

| File | Size (this build) | Use |
|------|-------------------|-----|
| `reference/API/api_index.json` | ~6.7 MB | machine consumption: every type + members as JSON |
| `reference/API/api_index.tsv` | ~2.1 MB | grep: one row per member (`type`, `namespace`, `kind`, `visibility`, `signature`, …) |
| `reference/API/api_index.md` | ~620 KB | humans: signatures grouped by namespace |

Generate/refresh it (the deployment path to the game DLL is the default `..\Timeflow`):

```powershell
dotnet run --project tools\ApiIndexer -c Release -- `
    "..\Timeflow\Timeflow_Data\Managed\Assembly-CSharp.dll" `
    "reference\API"
```

Result on this build: **1381 types** (including nested), **15 413 members**.

> `reference/` is gitignored and regenerated on demand — the index is always reproducible from the
> shipping DLL. The curated summary below lives here in `docs/` so it is version-controlled.

### Reading the index

```powershell
# every member of a type
Select-String -Path reference\API\api_index.tsv -Pattern "^Player\t" | ForEach-Object { ($_ -split "`t")[8] }

# find any method by name across the whole game
Select-String -Path reference\API\api_index.tsv -Pattern "\tMoneyOp\(" | ForEach-Object { ($_ -split "`t")[8] }

# map a Cyrillic/English term to a type (search the JSON)
Select-String -Path reference\API\api_index.json -Pattern "BusinessUpgrade"
```

For method **bodies**, decompile the single type:

```powershell
dotnet ilspycmd -t Player "..\Timeflow\Timeflow_Data\Managed\Assembly-CSharp.dll"
# or the whole assembly to one file (used to build the guides):
dotnet ilspycmd -o reference\full --nested-directories "..\Timeflow\Timeflow_Data\Managed\Assembly-CSharp.dll"
```

---

## B. Namespace map

The **game's own gameplay code lives in the global namespace** (982 of the 1381 types). Everything
else is engine, packages or a few game sub-systems:

| Namespace | Types | Notes |
|-----------|------:|-------|
| *(global)* | 982 | `Player`, `GameManager`, `Card`, `CardsDeck`, `UIManager`, `SettingsManager`, `LogManager`, `Localization`, `StockManager`, `Marketplace`, `Bookkeeper`, `AIManager`, `SceneLoader`, `BoardCell`, … |
| `UnityEngine.UI.Extensions` (+ `.ColorPicker`, `.Examples`, `.Tweens`) | 125+ | bundled uGUI extensions (color pickers, effects) |
| `CoreGameplay` | 25 | supporting gameplay helpers |
| `Assets.Scripts.Utils` | 19 | utilities |
| `Utlils` / `Utils` (+ `.VersionChecker`) | 13 | helpers (note the misspelled `Utlils` folder namespace) |
| `NetworkGame` (+ `.Data`) | 25 | co-op/multiplayer layer on Unity Netcode |
| `Achievements` | 12 | `AchievementsManager`, achievement definitions |
| `SaveSystem` (+ `.Data`) | 8 | save serialisation structures |
| `Views` (+ `.FinalPoints`, `.PreciseMoves`, `.CircleIcons`, `.CharacterEditor`) | 20+ | window/view controllers |
| `Badges`, `Donates`, `IAP`, `DemoVersion`, `GameSettings`, `Additional`, `DebugUtils` | — | meta-features |
| `GooglePlayGames.*` | 40+ | Android integration (present but not used on PC) |
| `DG.Tweening`, `Cysharp.*`, `YMMJSONUtils`, `SpineUtils` | — | third-party libs (DOTween, UniTask, JSON, Spine) |

For modding, **assume global namespace** unless the type name says otherwise.

---

## C. Subsystem catalog (the types you'll actually use)

Curated highlights — exhaustive member lists are in `reference/API/api_index.*`.

### C.1 Game flow & state

| Type | Purpose | Key members |
|------|---------|-------------|
| `GameManager` | the match singleton | `instance`, `InGame`, `Players`, `MyPlayer`, `firstPlayer`, `currentPlayerScript`, `boardCells`, `cellsInMonth`, `OnTurnButton()`, `MakeTurn`, `MakeCheatTurn`, `InsertAction`, `AddCoAction`, `IsNetworkGame`, events `PlayerMoveEnd` |
| `SceneLoader` | scene bootstrapping | loads only `GameScene`; `LoadLocalization` |
| `BoardsManager` | board generation | builds `boardCells` |
| `BoardCell` | one ring cell | `eventScript`, `ActivateCellEvent(Player)`, `isVirtualCell` |
| `EventAbstract` | base for cell events | `ActivateEvent(Player)` (async), `GetCardCategory()` |
| `EventHappening` / `EventIncome` / `EventRoutine` / `EventNews` / `EventMonth` / `EventWeekend` / `EventEducation` | concrete cells | see chapter 04 |
| `TimeView` | the turn/time UI | time management interactions |

### C.2 Player

| Type | Purpose | Key members |
|------|---------|-------------|
| `Player` | one pawn's full state | `money` (get-only), `moneyOp(long,bool=true)`, `income`, `expense`, `active`, `passive`, `cashflow`, `time`, `salary`, `Age`, `mood`, `ChangeMood(int)`, `success`, `Difficulty`, `GUID`, `title`, `isBot`, `cardPerTurn`, `GetDice()`, `TakeCard(Card,bool,bool)`, `RejectCard`, `FindCard`, `HasCard`, `GetAllCards`, `GetCardsForCategory`, `AddCardToCategory`, `UpdateBalance`, `canGetACard`, `ConditionStringToBool`, `ReadCustomVariables`, `AddValueToVariableForKey`, `VariableValueForKey`, `GetBusiness`, `money*`, `BuyStock`, `TakeLoan`, `FireFromWork`, `BoostOnWork` |
| `ConsumerBasket` | basket/expense model | `income`, `bonusBasket`, `title` |
| `Bookkeeper` | accounting entries | `BookTaking`, `BookAction` |

### C.3 Cards & economy

| Type | Purpose | Key members |
|------|---------|-------------|
| `Card` | the data model | `category`, `id`, `image`, `price`, `priceOriginal`, `income`, `incomeWithBonus`, `time`, `duration`, `chance`, `isBusiness`, `isRealty`, `isActive`, `isPassive`, `give/need/shield/remove/removeActive`, `action`, `delayed`, `upgrades`, `options`, `DeepCopy`, `Set*DictForAllLangs`, `GetExpense`, `koi`, `IsCardHaveTarget` |
| `CardsDeck` | master card database | `instance`, `GetDeck`, `GetCardByID`, `GetAllowedDeck(Task)`, `GetRandomCardInList`, `GetRandomCardsInCategory`, `GetMonthCard`, `GetWeekendCard`, `GetEmptyCard`, `GetDreamPartByID`, `LoadDatabase`, `LoadCustomCharacter`, `LoadCharacterCard` |
| `CardOption` | a card's choices | `title`, `price`, `income`, `time`, `need`, `give`, `shield`, `remove`, `delayed` |
| `DelayedEvent` | "later" effect / build phase | `delay`, `chance`, `price`, `income`, `time`, `give`, `bonus*`, `targets` |
| `BusinessUpgrade` | business upgrade step | `price`, `pricePercent`, `income`, `time`, `delay`, `need`, `give`, `delayed` |
| `PriceResetData` | price drift | `rndMin`, `rndMax`, `SetLogDict`, `SetNotificationDict` |
| `MarketLot` | a listing on the marketplace | constructor `(Card, Player, long markup, int lifetime)` |
| `Marketplace` | player market | `instance.HasCard(id)`, `LifetimeForOpportunity` |
| `StockManager` | stocks/crypto | `instance`, `StockPriceForID`, `realtyFactor`, `realtyBonus`, `SetTrendManual`, `stock*` |
| `CardsDeck.EmptyCardsDecks()` | fresh database | returns `Dictionary<CardCategory, HashSet<Card>>` |

### C.4 UI

| Type | Purpose | Key members |
|------|---------|-------------|
| `UIManager` | window hub | `instance`, `ShowEvent(Card)`, `ShowEventCards(List<Card>)`, `ShowChoice(ChoiceType, ct)`, `ShowDialogYesNo`, `ShowNotification(title,text,ct,bool)`, `ShowCard`, `GetImageByName`, `dreamImages`, `eventImages`, `Lock/UnLockCommonBtns` |
| `MenuView` / `SettingsView` / `EventView` / `NotificationView` / `BankView` / `ChoiceView` | concrete windows | `ShowWithCard`, `WaitForClosed`, etc. |
| `BaseView` | common window base | `Show`, `Hide`, `WaitForClosed`, `LastShowedWindow` |
| `Localization` | i18n | `currentLanguage`, `Localizations`, `EnabledLocalizations`, `StringForKey`, `DictionaryForKey`, `RichStrFromTaggedStr`, `units.CurrencyFormat` |
| `LogManager` | in-game log feed | `instance`, `Message(string, Dictionary<string,string> vars = null)` |
| `SoundManager` | audio | `instance`, play methods |

### C.5 Settings, saves, achievements, platform

| Type | Purpose | Key members |
|------|---------|-------------|
| `SettingsManager` | global balance/config | `instance`, `kMoney`/`kMood`/`kBaseMood`/`kCryptoScam`/`kTime`/`kAge`/`kSuccess`/`kHourPrice`, `mood*`, `loan*`, `stock*`, `GetCellDelay`, `GetMaxMood`, `MovesMinByAge/MaxByAge`, `WinSettings`, `variableValueMin/Max` |
| `SaveSystem` (+ `SaveSystem.Data`) | persistence | save/load of players and world |
| `Achievements.AchievementsManager` | achievements | `Instance`, `CheckSpecialAchievement` |
| `MonoBehaviourSingleton<T>` | singleton base | used by many managers |
| `SteamManager` | Steamworks | via `com.rlabrecque.steamworks.net` |
| `TutorialManager` | tutorial | `TutorialInProcess`, `instance.returnCardForCurrentStep` |
| `Helpers` | utility kit | `SeparateString`, `MoneyCeil/Round/Floor`, `SafeLongAddition/Multiply`, `idFromRandomString`, `VarsDictWithCard`, `StringWithVars` |

---

## D. How to answer *any* modding question (workflow)

1. **Name it.** Guess the English type name (`Player`, `CardsDeck`, `StockManager`, …) or search the
   index text: `Select-String reference\API\api_index.tsv -Pattern "tax"`.
2. **List its shape.** Filter the TSV by the first column (type) to get all members and signatures.
3. **Read the body** only if you must: `dotnet ilspycmd -t <Type> <Assembly-CSharp.dll>` (bodies are
   in `reference/full/Assembly-CSharp.decompiled.cs`, one file, grep-friendly).
4. **Find a hook.** Prefer a method that runs at the moment you care about. Sync methods
   (`Player.MoveEnd`, `Player.TakeCard`, `Player.UpdateBalance`) are far easier to patch than
   `async UniTask` ones (whose body lives in a state machine).
5. **Patch it.** `Harmony.Patch(AccessTools.Method(...), prefix/postfix)`; see
   [03 — Harmony patching](03-harmony-patching.md).
6. **Verify at runtime.** Run `example.event-log` to see when methods fire, watch the panel, check
   `TimeflowModLoader\Logs\ModLoader.log`.

### Reading values out of the game

* Public fields/properties: use them directly (`player.money`, `player.income`, `GameManager.instance.InGame`).
* Private state: `AccessTools.Field(typeof(T), "_name")` or `AccessTools.Property`.
* The game logs a lot via `UnityEngine.Debug.Log`; `redirect_output_log=true` mirrors it to
  `output_log.txt`, and the loader's `Debug::Log` hook means much of it also lands in `ModLoader.log`.

---

## E. Cross-cutting conventions & quirks

| Quirk | Detail |
|-------|--------|
| Global namespace | game types have **no namespace**. `Card`/`Player` resolve directly. |
| Singletons | `GameManager.instance`, `CardsDeck.instance`, `UIManager.instance`, `StockManager.instance`, `LogManager.instance`, … may be **null in menus** — always null-check. |
| Async everywhere | most UI/flow methods return `Cysharp.Threading.Tasks.UniTask`. You can call one and discard the task **only if** you reference UniTask; via reflection you can avoid the reference (see `CustomEventMod.TriggerNow`). Patch only the synchronous prologue. |
| Money | `moneyOp(value)` **adds**; asset `price` is **negative**; display is scaled by `TemplateCurrencySettings.CurrentCurrencyMod`. |
| Cards | shared objects; `DeepCopy` before mutating; two decks (master vs inventory). See chapter 09. |
| `give` language | only `basemood`/`mood`/`cryptoscam` are special; everything else is a clamped variable. `money=` is **not** a payment. |
| Events | `GameManager.PlayerMoveEnd`, `Player.*` C# events exist — subscribe instead of patching when possible; unsubscribe in `OnUnload`. |
| Localization | text dicts keyed by `Localization.currentLanguage`; missing entries render `#<lang>:<id>`. |
| `SettingsManager` tunables | many are `public static` (e.g. `moodBusiness`, `loanPercent`, `variableValueMax`) — easy to tweak, but changes are process-wide; restore in `OnUnload`. |

---

## F. Where to go next

| Question | Guide |
|----------|-------|
| Build & run a first mod | [00 — Quickstart](00-quickstart.md) |
| Mod lifecycle, logging, config | [01 — Mod anatomy](01-mod-anatomy.md) |
| Game singletons & the board | [02 — Game internals](02-game-internals.md) |
| Prefix/Postfix/Transpiler | [03 — Harmony patching](03-harmony-patching.md) |
| Events & cards (recipes) | [04 — Events & cards](04-events-and-cards.md) |
| Native & IMGUI UI | [05 — UI & HUD](05-ui-and-hud.md) |
| Config & save data | [06 — Config & save data](06-config-and-save-data.md) |
| Cookbook | [07 — Recipes](07-recipes.md) |
| For AI agents | [08 — AI agents](08-ai-agents.md) |
| Cards/businesses/economy | [09 — Cards & business internals](09-cards-and-business-internals.md) |

Back to the [guide index](README.md).
