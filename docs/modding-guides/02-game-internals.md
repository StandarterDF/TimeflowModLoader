# 02 — Game internals (the API you'll actually patch)

A practical map of Timeflow's own code. Everything here was extracted from the shipping
`Assembly-CSharp.dll` with `ilspycmd`, and behaviours marked **verified** were observed in-game.

Game script types are in the **global namespace** (no `namespace`). Some helper namespaces exist:
`SaveSystem`, `SaveSystem.Data`, `GameSettings`, `Achievements`, `CoreGameplay`, `DebugUtils`,
`Assets.Scripts.Utils`.

---

## Singletons & entry points

| Type | Access | Notes |
|------|--------|-------|
| `GameManager` | `GameManager.instance` | The match. Null in the loading scene; may be present in the menu (`InGame == false`). |
| `SettingsManager` | `SettingsManager.instance` (**non-static** members) + **static** tunables | User settings + the balance constants. |
| `CardsDeck` | `CardsDeck.instance` | The card database, loaded once at boot. |
| `UIManager` | `UIManager.instance` | Opens/closes every window; helper dialogs. |
| `LogManager` | `LogManager.instance` | The in-game event log (bottom-right feed). |
| `SoundManager` | `SoundManager.instance` | SFX/music. |
| `StockManager` | `StockManager.instance` | Market/stocks. |
| `TutorialManager` | `TutorialManager.instance` | Tutorial flow. |
| `SceneLoader` | scene component | Shows the loading screen then loads `GameScene`. |

> Singletons can be `null` depending on the scene/state. **Always null-check**, and prefer doing
> work on `GameEvents.SceneLoaded` or lazily inside `OnUpdate`.

## Scenes

`SceneLoader` (in the first/loading scene) loads **`GameScene`**, which contains both the main
menu and the actual board gameplay. There is no separate "menu scene". Consequences:

* `GameEvents.SceneLoaded` fires for `IntroScene` (or similar) and then `GameScene`.
* "In the menu" vs "in a game" is `GameManager.instance != null && GameManager.instance.InGame`.
* `GameManager.instance` may exist while `InGame == false` (menu). Don't assume `currentPlayerScript != null`.

---

## `GameManager` — the board & turn model

```csharp
var gm = GameManager.instance;
if (gm != null && gm.InGame) { ... }
```

| Member | Type | Meaning |
|--------|------|---------|
| `instance` | `GameManager` | Singleton. |
| `InGame` | `bool` | A match is running (not menu/loading). |
| `Players` | `List<Player>` | All players (incl. bots). |
| `currentPlayerScript` | `Player` (get, private set) | Whose turn it is. |
| `firstPlayer` | `Player` | Player 0. |
| `MyPlayer` | `Player` (get, private set) | Local human player (network). |
| `boardCells` | `GameObject[]` | The board ring; one `GameObject` per cell. |
| `cellsInMonth` | `int` | `boardCells.Length / 12`. |
| `IsNetworkGame`, `IsHost`, `IsAuthHost` | `bool` | Multiplayer state. |
| `NextCell(GameObject cell, int delta = 1)` | `GameObject` | Cell N steps ahead. |
| `NextCellIndex(int cellIndex, int delta = 1)` | `byte` | Index N steps ahead. |
| `MonthForCellIndex(int cellIndex)` | `int` | Month (1..12) for a cell. |
| `GetCellInfo(int cellIndex)` | `string` | Debug text about a cell. |
| `OnTurnButton()` | `void` | Fired when the player presses **Ход/Бросить кубик**. |
| `OnPreciseMove(byte movesCount)` | `void` | Precise-move (точный ход) button. |
| `MakeCheatTurn(byte dicesResult)` | `void` | Debug/cheat: take a turn with a forced dice value. |
| `InsertAction(BasePlayerAction action)` | `void` | Queue an action (the game's own action system). |
| `AddCoAction(BasePlayerAction coAction, bool onlyOne = false)` | `void` | Queue a parallel action. |
| `event PlayerMoveStart` | `Action<Player>` | A pawn started moving. |
| `event PlayerMoveEnd` | `Action<Player>` | A pawn finished moving. |

**Verified:** patching `GameManager.OnTurnButton` (Postfix) fires exactly once per turn-button
press; see `example.event-log`.

### The action system (advanced)

Game actions are `BasePlayerAction` subclasses inserted with `InsertAction`. This is the *safe*
way to make the game do something (it is how the debug views work). To call something immediately
instead, prefer the public methods on `Player` (`moneyOp`, `ChangeMood`, `ChangeYearsOld`, ...).

---

## `Player` — the character

`Player : MonoBehaviour`. One instance per participant (humans + bots). The local human is
`GameManager.instance.firstPlayer` / `MyPlayer` / `currentPlayerScript` depending on context.

### Identity / state

| Member | Type | Notes |
|--------|------|-------|
| `title` | `string` | Display name. |
| `isBot` | `bool` | Bot flag. |
| `Race` | `Race` | `Euro` / `Asian` / `Afro`. |
| `Difficulty` | `GameDifficulty` | Difficulty this player was created with. |
| `cellIndex` | `byte` | Current board cell. |
| `cellsPassed` | `int` | Total cells ever passed. |
| `yearsPassed` / `monthsPassed` | `int` | Derived from `cellsPassed` and `cellsInMonth`. |
| `Age` | `int` | Age in years. |
| `time` | `int` | Free time (hours). |
| `turnsPassed`, `MovesCounter`, `Moves`, `Months` | `int` | Counters. |
| `IsMoving` | `bool` (get, private set) | Pawn is animating. |
| `isBankrupt` | `bool` | `money < 0`. |

### Money & economy

| Member | Type | Notes |
|--------|------|-------|
| `money` | `long` (**get-only**) | Read the balance. **Cannot be assigned.** |
| `moneyOp(long value, bool showEffect = true)` | `void` | **The** way to change money. Positive adds, negative subtracts; handles UI/effects/log. |
| `MoneyOpValue(long value)` | `void` | Lower-level variant. |
| `income`, `expense` | `long` | Monthly income / expenses. |
| `cashflow` | `long` | `income + expense` (get-only). |
| `active`, `passive` | `long` | Active/passive income rows. |
| `salary`, `hourPrice` | `long`/`int` | Work figures. |
| `allMoney` | `long` | Net worth. |
| `success` | `int` | Success track (win condition). |
| `UpdateBalance()` | `void` | Recomputes the balance views. |
| `MakeSnapForHistory()` | `void` | Push a history snapshot. |

> `money` being read-only is deliberate. For a cheat, patch or call `moneyOp`. Prefer
> `player.moneyOp(delta)`; it keeps the HUD and the balance history consistent.

### Mood

| Member | Notes |
|--------|-------|
| `mood` | `int` property (current mood). |
| `moodWithBonus` | `int`, mood including bonuses. |
| `BaseMood` | `int` field. |
| `MoodCoeff` | `int` mood coefficient. |
| `ChangeMood(int value)` | **Central** method for mood changes; a Prefix can veto negative deltas. |

**Verified:** `example.mood-guard` patches `ChangeMood` and blocks negative values.

### Dice & movement

| Member | Notes |
|--------|-------|
| `GetDice()` | `byte` — returns the next roll. |
| `GenerateDicesCache()` | Refills the roll cache. |
| `CheatTurn(byte dicesResult)` | Take a turn with a fixed roll. |
| `PreciseMove(byte movesCount)` | Use a precise move. |
| `DicesCache` | `List<byte>` (get, private set) — the pre-rolled queue. |
| `OnStartMove()` / `OnEndMove()` | Move hooks. `Move(start, end)` is the async animation. |
| `speed` | `float` — pawn animation speed. |

**Verified:** a Postfix on `GetDice` rewriting `__result` makes every roll the same value, and the
cell advance matches it exactly (`example.fast-dice`).

### Cards, jobs, business, stocks

| Member | Notes |
|--------|-------|
| `GetCardsForCategory(CardCategory)` | `HashSet<Card>` the player owns in a category. |
| `AddCardToCategory(category, card)` | Grant a card. |
| `TakeCard(Card takeCard, bool instantAction, bool book)` | Apply a card. |
| `FindCard(string searchCardID)` / `HasCard(id)` | Look up owned cards. |
| `RemoveCardWithID(string cardID, bool instantAction, bool book = false, bool removeDepends = true)` | Remove. |
| `GetWork()`, `GetBusiness()`, `GetRealty()` | Current possessions. |
| `FireFromWork(Card work, bool manually, bool fired = true)` / `RejectCard(Card)` | Job lifecycle. |
| `SellStock(...)`, `BuyStock(...)` | Stock ops. |
| `TakeLoan(...)`, `CloseLoan(...)`, `OpenLongDeposit(long)` | Banking. |
| `AddLotToMarket(MarketLot)`, `SellLot(...)`, `BuyLot(...)` | Market. |
| `variables` | `Dictionary<string,int>` — arbitrary per-player variables (used by cards/events). |
| `AddValueToVariableForKey(int addValue, string key)`, `SetValueToVariableForKey(int newValue, string key)`, `VariableValueForKey(string key)` | Safe accessors for those variables. |

### Other useful members

* `ChangeYearsOld(int age)` / `ChangeYearsPassed(int years)` — ageing.
* `OnGameOverAndContinue(bool asWinner, bool asFailByAge, bool asBuyFullGame)`, `OnFail()`,
  `OnSkipFail()` — game-over flow.
* Events: `PlayerMoveStart`, `PlayerMoveEnd`, `PlayerStatusChanged`, `NewChatMessage`, `LotsCountChanged`, `StockCardsChanged`.

---

## `SettingsManager` — balance constants (the cheat target)

Most balance numbers are **`static` fields**, so patching one affects the whole game:

| Field | Type | Meaning |
|-------|------|---------|
| `cellDelay` | `float` (0.1) | Base delay between board cells. |
| `GetCellDelay(int dices)` | `static float` | **Actual** per-move delay; patch this to change game speed. |
| `timeFree` | `int` (200) | Starting free time. |
| `timeMin` | `int` (-100) | Minimum time. |
| `ageForRetirement` | `int` (60) | Retirement age. |
| `cellsCountInMonth` | `int` (8) | Cells per month. |
| `dicesCacheSize` | `int` (3) | Roll queue size. |
| `moodBase`, `moodMonthlyStep`, `moodFailure` (-20), `moodFired` (-30), `moodResign`, ... | `int` | Mood deltas. |
| `successCashflow`, `successMoney` | `List<int>` | Win thresholds. |
| `loanSumMin/Max`, `loanPercent`, `loanPercentCoeff`, `depositShortPercent`, `depositLongPercent` | numbers | Banking. |
| `stockDefaultChance`, `stockDefaultOffsetUp/Down`, `stockMinPrice`, `stockMaxPrice` | numbers | Stocks. |
| `moneyEdgeL/M/H`, `cashflowEdge`, `timeEdgeL/M/H` | `int` | UI colouring thresholds. |
| `PreciseMoves` | `ValueByDifficulty[]` | Precise moves per difficulty. |
| `WinSettings` | `Dictionary<GameDifficulty, WinSettingsData>` | Win conditions. |
| `successFactor`, `chanceUp/Down/Work`, `actionInterval` | `int` | Various RNG. |

**Instance** (per-user) members: `instance`, `soundVolume`, `musicVolume`, `envVolume`,
`uiScale`, `currentLanguage` (`"ru"`/`"en"`), `currentFps`, `boardField`, `SaveSettings()`.

**Verified:** a Postfix on `SettingsManager.GetCellDelay` dividing the result speeds up pawn
movement and the board (`example.game-speed`).

---

## `CardsDeck` — the card database

`CardsDeck.instance` is loaded once (`LoadDatabase(Action<float> progress)`).

| Member | Notes |
|--------|-------|
| `GetCardByID(string searchID)` | Exact card by id. Returns `null` if unknown. |
| `GetDeck(CardCategory category)` | All cards of a category. |
| `GetAllowedDeck(CardCategory, Player)` | Cards legal for a player. |
| `GetRandomCardInCategory(CardCategory, Player)` | One random card. |
| `GetRandomCardsInCategory(CardCategory, int, Player, bool)` | Several. |
| `GetMonthCard(int month)` | The month card. |
| `GetWorkInvitation(Player)`, `GetWeekendCard(Player)`, `GetDreamPartByID(string)` | Specific decks. |
| `GetEmptyCard()` | A blank `Card`. |
| `workCards`, `educationCards`, `othersCards`, `dreamCards`, `balanceInfoCards`, `characterCards` | Raw lists. |
| `stockCardsIds` | Ids of market instruments. |

To add a card at runtime, build/modify a `Card` (see `Card.DeepCopy()`, `Card(string id)`) and
grant it via `Player.AddCardToCategory` / `TakeCard`. See
[04-events-and-cards.md](04-events-and-cards.md).

## `Card` in a nutshell

`Card` is a plain class (not a `MonoBehaviour`) with a **lot** of public fields. The most useful
ones:

| Field/property | Notes |
|----------------|-------|
| `id`, `title`, `subtitle`, `description`, `shortDesc`, `name` | Text (localised dictionaries under the hood). |
| `category` | `CardCategory`. |
| `price`, `income`, `incomeWithBonus`, `basePrice` | Economy. |
| `instant`, `holy`, `canBeSaled`, `book`, `reject`, `reuse`, `demo` | Flags. |
| `action` | `ActionCard` delegate(s) — most card behaviour is a parsed delegate! |
| `options`, `upgrades`, `delayed`, `momentalEvents` | Sub-structures. |
| `variables`/`give`/`need` semantics: `give`, `need`, `shield`, `remove`, `removeActive` | Scripted effects (string mini-language). |
| `SetTitleDict`, `GetDescriptionDict`, `SetDescriptionDictForAllLangs(...)` | Localisation. |
| `DeepCopy()` | Clone a card before editing it (do **not** mutate the deck's original). |
| `SaveOriginal()` / `RestoreOriginal()` | Snapshot/restore a card's values. |

Card "effects" are largely **data**, driven by `action`/`give`/`need` strings, not code. That
means you can create new cards by cloning a similar one and editing fields.

## `Localization`

```csharp
string s = Localization.StringForKey("some.key");
string money = Localization.CurrencyFormat(player.money);   // "1 234 567 ₽"
string lang = Localization.currentLanguage;                 // == SettingsManager.instance.currentLanguage
```

`Localization.StringFromMood(int)`, `StringFromCyan?`, etc. exist for UI text. `Localizations`
lists every supported language; `EnabledLocalizations` is what the build ships (`ru`, `en`).

## UI entry points

`UIManager.instance` drives nearly everything. Highlights:

| Method | Notes |
|--------|-------|
| `ShowNotification(string title, string text, CancellationToken ct, bool alwaysOnTop=false)` | Toast/notification. |
| `ShowDialogYesNo(string title, string msg)` / `... (Action<bool> callback)` | Yes/No dialog. |
| `ShowCustomDialog(string msg, string lBtn, string rBtn, Action<bool>)` | Custom buttons. |
| `ShowEvent(Card)` / `ShowEventCards(List<Card>)` | Card event popups. |
| `ShowTime(bool edit, bool fromLoan, Card card = null, bool mayClose = false)` | Time view. |
| `ShowCardView(CancellationToken, Card)` | Card detail popup. |
| `ShowMenu()`, `HideMenu()`, `OpenWindow()`, `CloseAllViews()` | Window flow. |
| `ShowSettings()`, `ShowAboutWindow()`, `ShowLoadGame()` | Specific windows. |

`TimeView.ShowTime(...)` (component) and `UIManager.ShowTime(...)` both exist.

`LogManager.instance.Message(string message, Dictionary<string,string> vars = null)` writes to
the in-game event feed. **Verified:** a Postfix on it captures every game log line
(`example.event-log`).

---

## Events & board cells

Each board cell is a `GameObject` with a `BoardCell` component:

```csharp
public class BoardCell : MonoBehaviour
{
    public EventAbstract eventScript;   // the cell's event (may be null)
    public float speed;
    public bool isVirtualCell;
    public async UniTask ActivateCellEvent(Player player);
}
```

`EventAbstract` is the base for `EventIncome`, `EventMonth`, `EventEducation`, `EventHappening`,
`EventNews`, `EventRoutine`, `EventWeekend`:

```csharp
public abstract class EventAbstract : MonoBehaviour
{
    public abstract UniTask ActivateEvent(Player player);
    public CardCategory GetCardCategory();
}
```

Full guide + recipes: [04-events-and-cards.md](04-events-and-cards.md).

---

## Save data (what persists)

`SaveSystem.SavesProvider` writes/reads `SaveSystem.Data.GameData`:

```
GameData
├─ int SaveVersion
├─ int CurrentPlayerIndex
├─ List<PlayerData> Players
├─ List<Card> Stocks
└─ float CurrencyMod / string CurrencySymbol / int CurrencyPosition

PlayerData
├─ string Title, int Difficulty, List<Card> Dream, byte CellIndex
├─ long Money, int Age, int Mood, int BaseMood, int MoodCoeff
├─ Dictionary<string,int> Variables, Dictionary<string,int> OwnVarsTimes
├─ List<List<Card>> Decks, List<int> DicesCache
├─ BalanceHistory History, ...
```

The player's own `variables` dictionary is **saved**, so storing progress in
`player.AddValueToVariableForKey(...)` is durable. See
[06-config-and-save-data.md](06-config-and-save-data.md).

---

## Enums (verified values)

```csharp
enum CardCategory { Dream, Habit, Work, Month, Weekend, People, Routine, News, Happening,
    BusinessSmall, BusinessMedium, BusinessBig, RealtySmall, RealtyMedium, RealtyBig,
    HighEducation, Workshop, TimeManagement, Stock, Loan, Deposit, Other, Notification,
    MonthAction, Family, BalanceInfo, DreamPart }

enum Race { Euro, Asian, Afro }

enum GameDifficulty { Tutorial, Easy, Medium, High, Special, HardCore }

enum StockTrend : byte { None, Free, Up, Down, Sideways, UpOnly, UpSharp, UpSmooth,
    DownOnly, DownSharp, DownSmooth, DownFixed, UpFixed, DividendsDown, DividendsUp }

enum BalanceState { Undefined, Common, Income, Expense, Active, Passive, Time, Character }
```

---

## How to find anything else

```powershell
# list every type
dotnet ilspycmd -l c "..\Timeflow\Timeflow_Data\Managed\Assembly-CSharp.dll"

# dump one type (full source)
dotnet ilspycmd -t Player "..\Timeflow\Timeflow_Data\Managed\Assembly-CSharp.dll" > reference\Player.cs

# public members only (fast)
dotnet ilspycmd -t Player "..\Timeflow\Timeflow_Data\Managed\Assembly-CSharp.dll" |
  Select-String '^\s*(public|protected)\s'
```

The repo's `reference/` folder already contains dumps (`Player.cs`, `GameManager.cs`,
`SettingsManager.cs`, `api_dump.txt`, `core_dump.txt`, ...) — regenerated on demand and gitignored.

Next: [03 — Harmony patching](03-harmony-patching.md).
