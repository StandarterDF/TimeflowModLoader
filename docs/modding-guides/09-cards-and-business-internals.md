# 09 — Cards, businesses & the economy (deep reference)

This is the deep dive behind [04 — Events & cards](04-events-and-cards.md). Read that first for
the "how to hook an event" recipe; this chapter is the **complete data model**: what every card
field does, how prices/income are computed, how the game reads the JSON database, and how to add
your own content the way the engine expects.

Two verified example mods implement everything described here:

* [`../../src/Timeflow.Examples/CustomBusinessMod.cs`](../../src/Timeflow.Examples/CustomBusinessMod.cs) — adds a *new business* to the Income deck (`example.custom-business`).
* [`../../src/Timeflow.Examples/CustomEventMod.cs`](../../src/Timeflow.Examples/CustomEventMod.cs) — adds a *new happening* with a scripted reward (`example.custom-event`).

Supporting helpers: [`CardFactory.cs`](../../src/Timeflow.Examples/CardFactory.cs) and
[`DeckRegistry.cs`](../../src/Timeflow.Examples/DeckRegistry.cs).

---

## 1. The card model in one picture

`Card` (global namespace, `[Serializable]`) is **pure data**: category, prices, incomes, localised
text dictionaries and a handful of script hooks (`give`, `need`, `action`, `delayed`, `upgrades`).
Almost every gameplay effect is driven by these fields, not by subclasses of `Card`.

Key identity fields:

```csharp
public CardCategory category;
public string id;                    // setter lower-cases it
public string image;                 // sprite name, e.g. "biz_sm_taxi", "BusinessSmall"
public int InternalId;
public byte NetworkId;
```

Text is stored per language in private dictionaries and exposed through getters that fall back to
`"#<lang>:<id>"` when a translation is missing:

| Getter | Setter(s) |
|--------|-----------|
| `title` | `SetTitleDict(dict)` / `SetTitleDictForAllLangs(text)` |
| `subtitle` | `SetSubtitleDict(dict)` |
| `description` | `SetDescriptionDict(dict)` / `SetDescriptionDictForAllLangs(text)` |
| `shortDesc` | `SetShortDescriptionDict(dict)` *(no ForAllLangs overload!)* |
| `logTaked` | `SetLogTakedDict(dict)` *(no ForAllLangs overload!)* |
| `logReject` | `SetLogRejectDict(dict)` |
| `lostDesc` | `SetLostDescDict(dict)` |
| `subcategory` | `SetSubcategoryDict(dict)` / `SetSubcategoryDictForAllLangs(text)` |
| `shortName` | `SetShortNameDict(dict)` |

> Only `SetTitleDictForAllLangs`, `SetDescriptionDictForAllLangs` and
> `SetSubcategoryDictForAllLangs` exist. For the others, build a
> `Dictionary<string,string>` over `Localization.Localizations` yourself — see
> `CardFactory.Localized`.

### Categories

```csharp
public enum CardCategory {
    Dream, Habit, Work, Month, Weekend, People, Routine, News, Happening,
    BusinessSmall, BusinessMedium, BusinessBig,
    RealtySmall, RealtyMedium, RealtyBig,
    HighEducation, Workshop, TimeManagement,
    Stock, Loan, Deposit, Other, Notification, MonthAction, Family, BalanceInfo, DreamPart
}
```

`Card` exposes convenience predicates: `isWork`, `isBusiness`, `isRealty`, `isStock`, `isLoan`,
`isDeposit`, `isHappening`, `isNews`, `isRoutine`, `isWeekend`, `isDream`, `isEducational`,
`isTimeManagement`, `isPeople`, `isFamily`, `isMonth`, plus `isBusinessInProcess`,
`isEducationInProcess`, `isActive`, `isPassive`, `hasLoan`, `isCryptoActive`.

---

## 2. Money, prices and the currency display multiplier

### Sign conventions (verified in the running game)

| Field | Meaning | Sign |
|-------|---------|------|
| `price` | What the asset **costs**; the shop wire does `player.moneyOp(card.price)` | **negative** for a cost |
| `income` | Per-month income/expense line | **positive** income, **negative** expense |

`Player.moneyOp(long value, bool showEffect = true)` **adds** `value` to money. Therefore a card with
`price = -45000` is a 45 000 purchase, and any effect that should *give* money must use a **positive**
number (`moneyOp(+amount)`), not a negative price.

Relevant computed properties:

```csharp
public long price          // getter: stock → last stock price; realty/realtyFactor → computed; loan → monthly*duration; else _price
public long priceOriginal  // "list price" before adjustments
public long income         // GetExpense(playerIncome, withBonuses:false)
public long incomeWithBonus// income + |income| * bonusBusiness (business) / bonusSalary (work)
public long GetExpense(long playerIncome, bool withBonuses)
public float koi           // (incomeWithBonus*12) / -price   ← return-on-investment %, shown on realty
public int hourPriceWithBonus
```

### The display multiplier — a trap when you read the UI

The UI does **not** print raw money. Every money string goes through
`Localization.units.CurrencyFormat(...)`, which multiplies by
`TemplateCurrencySettings.CurrentCurrencyMod` (a **per-language** display factor sourced from
`defaultCurrencyMods`). So an internal value of `12000` can read as `24 000 ₽` on screen.

*Internal* values (what `income`, `price`, `moneyOp` use) are the ones you set; the multiplier is
purely cosmetic. Do not "correct" your values against the screen — verified with both example mods
(in `ru` the factor was `2`).

---

## 3. How the game loads its database (and the JSON schema)

`CardsDeck` is a `DontDestroyOnLoad` MonoBehaviour singleton (`CardsDeck.instance`). At startup
`LoadDatabase(progress)`:

1. builds `_cardDeck = EmptyCardsDecks()` — a
   `Dictionary<CardCategory, HashSet<Card>>` (the **master database**);
2. deserialises character settings (`customCharacter`) and parses its embedded card files
   (`Resources/Database/BinaryCards/*`, BSON → `JSONObject`);
3. for every JSON object calls the private `CreateCard(JSONObject)` which starts from
   `DefaultCardForCategory(category)` and then maps each present field;
4. also supports `LoadCustomCharacter(string filePath)` / `GetCustomCharacter(string json)` — the
   game's own data-driven content path.

`DefaultCardForCategory` sets the category-independent defaults:

```csharp
card.image = category.ToString();
card.reject = isBusiness || isRealty || isEducational || isWork || isStock || isDream;
card.canBeSaled = isBusiness || isRealty;
card.loanSumMax = -1;
card.loanSumMin = SettingsManager.loanSumMin; // + loanPercent / loanYearsMin / loanYearsMax
// stock category: default chance/offsets/min/max price from SettingsManager
```

### JSON keys accepted by `CreateCard` (complete, from the parser)

Grouped for readability — the names are literal JSON keys:

**Identity / presentation**
`image`, `title`, `subtitle`, `shortname`, `description`, `shortDesc`, `log`, `logReject`,
`lostDesc`, `subcategory`, `color` (`{red,green,blue}`), `networkId`, `review`, `demo`,
`isNamedCard`, `predictable`, `dreamParts`.

**Economics**
`price`, `priceMin`, `priceMax`, `pricePercent`, `priceQuit`, `priceFired`, `income`, `time`,
`duration`, `sale` (canBeSaled), `book`, `instant`, `holy`, `forAll`, `priceAdjust`,
`realtyFactor`, `costInPoints`, `finalPoints`, `chance`, `botRate`, `actionInterval`.

**Businesses / realty**
`delay`, `delayed`, `upgrades`, `industry`, `target`, `targetIndustry`, `targetRealty`,
`targetRealtyBonus`, `bonusBusiness`, `bonusBusiness_Ind`, `bonusSalary`, `bonusSalary_Ind`,
`bonusRealty_Ind`, `bonusBasket`, `bonusBasket_Ind`, `bonusMargin`, `bonusMood`, `moodCoeff`.

**Scripts / conditions**
`action`, `give`, `need`, `shield`, `remove`, `removeActive`, `reject`, `rejectGive`, `reuse`.

**Ownership / timers**
`hasOwnCard`, `ownCardSetter`, `resetOwnCard`, `maxOwnCardTime`, `hasOwnVar`, `ownVarSetter`,
`resetOwnVar`, `maxOwnVarTime`, `linkedCards`.

**Loans**
`loanSumMin`, `loanSumMax`, `loanYearsMin`, `loanYearsMax`, `loanPercent`.

**Stocks**
`shortPosition`, `mutualMember`, `trend`, `trendChance`, `trendDuration`, `trendOffset`,
`offsetDownMin/Max`, `offsetUpMin/Max`, `stockRandomTrendFactorMin/Max`, `dividendMax`,
`zeroDividendChance`, `maxPriceReset`, `minPriceReset`.

**Containers** (nested objects/arrays; see `ParseCardOptionsProperty`, `ParseUpgradesProperty`,
`ParseDelayedProperty`, `ParsePriceResetDataFromJSONObject`)
`options`, `upgrades`, `delayed`, `notification`, `notificationIncome`, `notificationTime`,
`notificationPrice`, `maxPriceReset`, `minPriceReset`.

> The parser is forgiving: any key not present is left at the category default. The safest way to
> craft a card *in code* is therefore to clone an existing card of the same category and overwrite
> the fields you care about (see §6 and `CardFactory`).

---

## 4. The scripted-effect mini-language

Several string fields are parsed by the game. **All comparisons are case-insensitive; lists are
comma-separated** (`Helpers.SeparateString`).

### `give` — apply variables / mood when the card is taken

`Player.ReadCustomVariables(give)` runs `key=value` pairs. `AddValueToVariableForKey` special-cases
only three keys, everything else becomes a **clamped player variable**:

| Key | Effect |
|-----|--------|
| `SettingsManager.kBaseMood` (`basemood`) | adds to `BaseMood` (clamped ≥ 0) |
| `SettingsManager.kMood` (`mood`) | `ChangeMoodVariable(v)` |
| `SettingsManager.kCryptoScam` (`cryptoscam`) | adds to `CryptoScam` (clamped ≥ 0) |
| *anything else* | `variables[key] += value`, clamped to `[variableValueMin, variableValueMax]`; value `0` removes the key |

**`give` does NOT move money.** `give = "money=100000"` only defines a variable literally named
`money`. To actually pay the player use a card `action` delegate (below), a positive `price`, or
`income`. (This corrects an older recipe in chapter 04.)

### `need` / `shield` — the conditions for being offered

`Player.canGetACard` evaluates `need` (must be **true**) and `shield` (if **true**, the card is
blocked). `ConditionStringToBool*` supports:

* bare token → true if the player has that give-variable (or `!token` means "does not have it");
* comparisons `key>N`, `key<N`, `key=N`;
* special keys: `SettingsManager.kMoney`, `kSuccess`, `kTime`, `kHourPrice`, `kMood`, `kAge`
  (e.g. `need="money>100000"`).

```csharp
bool ConditionStringToBool(string str, HashSet<string> givesCopy, bool checkForReadiness = true)
```

### `remove` / `removeActive`

`remove` deletes cards/variables that declare matching `give` flags
(`RemoveCardsThatGivesVars`); `removeActive` clears active effects (`removeActive` items).

---

## 5. `action` — the `ActionCard` delegate

`public delegate void ActionCard();`

* A card's `action` field, if non-null, is invoked inside `Player.TakeCard` **as the current
  player** (`GameManager.instance.currentPlayerScript`).
* The JSON parser builds delegates from a small verb list via `CardsDeck.DelegateParse(string)`
  (`DelegateParseToList`): `уволить`, `повысить`, `понизить`, `аудит`, `налоговая`, `работа`, …
* **From code you can assign any delegate yourself** — this is the most flexible hook for a custom
  effect and needs no Harmony patch. It survives `DeepCopy` (delegate reference is copied).

```csharp
private static void AwardReward()
{
    var player = GameManager.instance.currentPlayerScript;
    if (player != null) player.moneyOp(12000);   // +12 000
}
// ...
_card.action = AwardReward;
```

This is exactly what `example.custom-event` does.

---

## 6. Businesses in depth

### What makes a business

* `category ∈ { BusinessSmall, BusinessMedium, BusinessBig }` → `isBusiness == true`.
* `income > 0` → it contributes to `Player.income`.
* `price < 0` → the shop charges the player on purchase.
* `canBeSaled = true`, `reject = true`, `image` a business sprite.
* Optional `delayed` list = the "under construction / opening soon" phase; optional `upgrades` list.
* `industry` + `targets`/`targetIndustry` connect it to bonus cards.

### The build/open lifecycle

```csharp
public bool isBusinessInProcess => isBusiness && delay > 0 && upgradeLevel == 0;
public int  delay => delayed.Count > 0 ? delayed[0].delay : 0;
```

When a business has a `delayed` stage, taking it starts "in process": it costs money and mood but
does not yet pay income. When the delay elapses the game calls `Card.UpdateForDelayed(delayedEvent)`
which copies income/time/bonuses from the delayed event and (implicitly) marks it open
(`upgradeLevel != 0`). A business **without** a `delayed` list is open immediately — the simplest
case, and what the example mod uses.

### How income is summed (`Player.UpdateBalance`)

```csharp
foreach (var c in GetAllCards()) {
    if (c.income != 0 && c.isBusiness && !c.isBusinessInProcess)
        c.bonusBusiness = BonusForCard(c);
    if (c.income > 0) income = Helpers.SafeLongAddition(income, c.incomeWithBonus);
    if (c.income < 0) expense = Helpers.SafeLongAddition(expense, c.GetExpense(income, true));
    if (c.isActive)  active  += c.isStock ? ... : Math.Abs(c.price);
    if (c.isPassive) passive += Math.Abs(c.price);
    num += c.time;
}
```

* `incomeWithBonus` for business = `income + |income| * bonusBusiness`.
* `BonusForCard(card)` sums `bonusBusiness` of non-asset cards whose `IsCardHaveTarget(card.id)` is
  true. **`IsCardHaveTarget` returns true when the bonus card's `targets` is empty** — i.e. an
  empty-target bonus card buffs *every* business. This is why a business can show double income in
  a save that already owns such a card.
* Taking a business also adds `SettingsManager.moodBusiness` mood (unless depressed).

### Taking a card (`Player.TakeCard`)

```
DeepCopy → ChangeRealtyIncome → add to player's _cardsDeck (if duration != 0) → work/realty/business
side-effects (mood, WorksCounter, FirstEvents) → ApplyPriceAdjust → RemoveVars/RemoveCards →
ReadCustomVariables(give) → achievements → card.action?.Invoke() → UpdateBalance → bookkeeping →
log → network broadcast → momentalEvents
```

Note: `TakeCard` never touches money. Money is moved by the **caller** — the shop UI does
`moneyOp(card.price)` then `TakeCard(...)`; a mod that grants a card directly can choose whether to
charge.

---

## 7. Adding your own card to the game

There are two decks in play:

* **Master database** — `CardsDeck._cardDeck` (private `Dictionary<CardCategory, HashSet<Card>>`).
  Everything the game offers is read from here.
* **Player inventory** — `Player._cardsDeck` (per player). This is what `GetAllCards()`,
  `UpdateBalance`, `FindCard`, saves etc. use.

### Option A — inject into the master database (appears as normal content)

Add your card to `_cardDeck[category]`; the game then offers it everywhere (Income choice, market,
happening pool) exactly like a shipped card. `example.custom-business` does this via
`DeckRegistry.Add(card)` (reflection over the private field).

Eligibility is then decided by `CardsDeck.GetAllowedDeckTask`:

```csharp
player.canGetACard(card, giveCopy)
// passes when: need is satisfied, shield is not, the player doesn't already own it (unless reuse),
// and (for bots) card.botRate != 0
```

and the winner is picked by `GetRandomCardInList`, a **weighted** draw on `Card.chance` that
avoids repeating the last card dropped from the same category.

### Option B — inject into `player.cardPerTurn` (per-turn, event-specific)

For happenings/news/routine, `GameManager.MakeTurn` sets
`player.cardPerTurn = await CardsDeck.instance.GetAllowedDeckTask(eventCardsCategory, player)`, and
`EventHappening.ActivateEvent` draws from that list. A Harmony **Prefix** on `ActivateEvent` can add
or replace cards before the draw — see `example.custom-event`.

### Building the card object

Prefer cloning: `existing.DeepCopy()` then overwrite. Watch out for fields that silently change
behaviour:

| Field | Why it matters when cloning |
|-------|-----------------------------|
| `realtyFactor`, `basePrice` | if `realtyFactor` is true, `price` is **computed** from `basePrice`+economy and your `_price` is ignored |
| `logTaked` / `logReject` / `lostDesc` / `shortDesc` / `subcategory` / `shortName` | copied by reference — you'll inherit the template's text |
| `delayed` / `upgrades` / `options` | copied & deep-cloned — clear them for a plain asset |
| `bonus*`, `targets`, `targetIndustry`, `industry` | leftover bonuses/targets change income and buffs |
| `action` | delegate copied — replace or null it |

`CardFactory.Create(category, id)` performs exactly this sanitising. See it for the canonical list.

---

## 8. Verified results (this build)

| Feature | Evidence |
|---------|----------|
| Custom business injected into `BusinessSmall` | `Built custom card 'mod:timeflow_studio' (image=biz_sm_taxi)` + `registered in the Income deck` |
| Granted via `F6` | player panel: income `0 → 6400` (internal 3200 × language currency factor), assets up, mood raised, log `Вы приобрели бизнес «Timeflow Studio»` |
| Custom happening with `action` reward | `F7` → event window "Неожиданная удача"; accepting added money (`+24 000 ₽` on screen = internal `+12000`) |
| Currency factor explains "doubling" | `CurrencyFormat` multiplies by `TemplateCurrencySettings.CurrentCurrencyMod` |

---

## 9. Gotchas checklist

1. **Money sign:** `moneyOp` adds; asset prices are negative; rewards use positive amounts.
2. **`give` is not a money command** — only `basemood`/`mood`/`cryptoscam` are special.
3. **Currency multiplier** is cosmetic; set internal values.
4. **Clone ships extra state** — sanitise `realtyFactor`, `basePrice`, text dicts, `delayed`,
   `upgrades`, `options`, bonuses, `targets`, `action`.
5. **Two decks** — master (`CardsDeck._cardDeck`) vs inventory (`Player._cardsDeck`).
6. **Remove injected cards in `OnUnload`** so disabling the mod really disables it.
7. **`SetShortDescriptionDictForAllLangs` / `SetLogTakedDictForAllLangs` don't exist** — build the
   dictionary yourself.
8. Cards are shared objects in the master deck: **never mutate them in place** unless you mean a
   global change; `DeepCopy` first.

Next: [10 — Full API reference](10-api-reference.md).
