# 04 — Events & cards (observe, modify, create)

This is the guide for "how do I make a thing happen on the board" and "how do I change what the
game's events/cards do". It documents the real flow, then gives recipes.

---

## The model

The board is a ring of `boardCells` (`GameObject[]` on `GameManager`). Each cell has a
`BoardCell` component:

```csharp
public class BoardCell : MonoBehaviour
{
    public EventAbstract eventScript;   // what happens when you land here
    public float speed;
    public bool isVirtualCell;
    public async UniTask ActivateCellEvent(Player player);   // entry point
}
```

Every event script derives from `EventAbstract`:

```csharp
public abstract class EventAbstract : MonoBehaviour
{
    public abstract UniTask ActivateEvent(Player player);
    public CardCategory GetCardCategory();
}
```

Concrete scripts: `EventIncome`, `EventMonth`, `EventEducation`, `EventHappening`, `EventNews`,
`EventRoutine`, `EventWeekend`.

### What actually happens on a landing (verified from source)

* `EventMonth` → `CardsDeck.instance.GetMonthCard(month)` → `UIManager.ShowEvent(card)`.
* `EventIncome` → `UIManager.ShowChoice(ChoiceType.Income, ct)` (an income/expense choice).
* `EventHappening` → picks a **random card from `player.cardPerTurn`** and shows it:
  ```csharp
  Card card = CardsDeck.instance.GetRandomCardInList(new List<Card>(player.cardPerTurn));
  await UIManager.instance.ShowEvent(card);
  ```
  Bots go through `AIManager.instance.DecisionEventView(card)` instead of the UI.

`player.cardPerTurn` is a **public `List<Card>` on `Player`**. This is the single most useful fact
for event modding: the "deck for this turn" is just a list you can read and edit.

---

## Observing events (logging / reacting)

Three good hooks, in order of reliability:

| Hook | Sync? | Fires |
|------|-------|-------|
| `Player.MoveEnd()` (`private void`) | ✅ sync | when a pawn finishes moving — after the cell was processed |
| `BoardCell.ActivateCellEvent(Player)` | ⚠️ async stub | when a cell begins activating |
| `GameManager.PlayerMoveEnd` / `Player.PlayerMoveEnd` **events** | — | subscribe in `OnLoad`, unsubscribe in `OnUnload` |

**Verified** (`example.event-log`): a Postfix on `Player.MoveEnd` logs `[move] <name> -> cell N
(passed M)` for every step, and a Postfix on `GameManager.OnTurnButton` logs a turn press.

```csharp
public override void OnLoad()
{
    Harmony.Patch(AccessTools.Method(typeof(Player), "MoveEnd"),
        postfix: new HarmonyMethod(AccessTools.Method(typeof(MyMod), nameof(OnMoveEnd))));
}

private static void OnMoveEnd(Player __instance)
    => ModLog.Info($"{__instance.title} -> cell {__instance.cellIndex}");
```

You can also subscribe to the game's own C# events instead of patching (no Harmony needed):

```csharp
private void Hook()
{
    var gm = GameManager.instance;
    if (gm != null) gm.PlayerMoveEnd += OnGameMoveEnd;   // Action<Player>
}

private void Unhook()
{
    var gm = GameManager.instance;
    if (gm != null) gm.PlayerMoveEnd -= OnGameMoveEnd;
}
```

> Game singletons are recreated between sessions; re-hook on `GameEvents.SceneLoaded`, and always
> unhook in `OnUnload`.

---

## Modifying existing events

### A. Veto/nerf a whole event category

Because the concrete `ActivateEvent` methods are `async UniTask`, a **Prefix still runs before the
body** (the body is queued as a state machine but the method's synchronous prologue is what you
see first). For pure guarding you can return `false` to skip the event, or mutate the inputs:

```csharp
[HarmonyPatch(typeof(EventIncome))]   // one mod per assembly, or manual AccessTools
[HarmonyPatch("ActivateEvent")]
static class NoIncomeEvents
{
    static bool Prefix(Player player) => !ModSettings.BlockIncome;
}
```

For a multi-mod assembly, do it manually:

```csharp
Harmony.Patch(AccessTools.Method(typeof(EventHappening), "ActivateEvent"),
    prefix: new HarmonyMethod(AccessTools.Method(typeof(MyMod), nameof(BeforeHappening))));

private static void BeforeHappening(Player player)
{
    // Runs just before the card is chosen; edit the deck it draws from.
    if (_noBadCards)
        player.cardPerTurn.RemoveAll(c => c != null && c.title != null && c.title.StartsWith("Плох"));
}
```

**Verified pattern:** the synchronous prologue of the async method sees `player.cardPerTurn`
before the `await`, so mutating it in a Prefix changes which card is shown.

### B. Change what a card does

Card behaviour is largely **data**, not code. The cleanest modification is to intercept the card
as the game hands it over:

```csharp
// Postfix on the random picker: bias or replace the chosen card.
[HarmonyPatch(typeof(CardsDeck), nameof(CardsDeck.GetRandomCardInList))]
static class FairRng
{
    static void Postfix(ref Card __result)
    {
        if (__result == null) return;
        __result = ModData.Rebalance(__result);   // return a clone with edited fields
    }
}
```

Or intercept at application time (works for every path, including saved cards):

```csharp
[HarmonyPatch(typeof(Player), nameof(Player.TakeCard))]
static class TweakTakenCard
{
    static void Prefix(Card takeCard)
    {
        if (takeCard == null || !ModSettings.BuffIncomes) return;
        takeCard.income *= 2;         // e.g. double income for every card taken
    }
}
```

> If you mutate a card that is shared by the deck, **clone it first** with `Card.DeepCopy()` and
> edit the copy, otherwise your change leaks into the database. Snapshot/restore helpers exist:
> `card.SaveOriginal()` / `card.RestoreOriginal()`.

### C. Rewrite an entire card category at load time

To globally rebalance (e.g. all `Work` salaries), edit the deck once when it is available:

```csharp
// GameEvents.SceneLoaded or after CardsDeck.instance is non-null
foreach (var card in CardsDeck.instance.GetDeck(CardCategory.Work))
    if (card.income > 0) card.income = (long)(card.income * 1.25f);
```

Keep the originals if you want to restore on unload (store `SaveOriginal()` results, or the
original lists).

---

## Creating a custom event / card

There is no runtime "register an EventAbstract subclass on a cell" API, but there doesn't need to
be: **events are driven by cards**, so a custom card *is* a custom event.

### Recipe: a brand-new "Happening" event

```csharp
public override void OnLoad()
{
    Harmony.Patch(AccessTools.Method(typeof(EventHappening), "ActivateEvent"),
        prefix: new HarmonyMethod(AccessTools.Method(typeof(MyMod), nameof(FeedCustomHappening))));
}

private static void FeedCustomHappening(Player player)
{
    if (player.cardPerTurn == null) return;
    player.cardPerTurn.Add(BuildCard());   // join this turn's draw pool
}

private static Card BuildCard()
{
    // Clone a same-category card so image/flags/defaults are valid, then sanitise + overwrite.
    var card = CardFactory.Create(CardCategory.Happening, "mymod.lottery");

    // Localised text. NOTE: only Title/Description/Subcategory have the *ForAllLangs helpers.
    card.SetTitleDictForAllLangs("Лотерейный билет");
    card.SetSubtitleDict(CardFactory.Localized("Джекпот"));
    card.SetDescriptionDictForAllLangs("Вы выиграли джекпот!");
    card.SetLogTakedDict(CardFactory.Localized("Выигрыш в лотерею"));

    // Magnitude. give/need are the scripted mini-language; for real money use an action delegate.
    card.price = 0;
    card.income = 0;
    card.duration = 0;               // do not store it as a permanent card
    card.give = "jackpot=1";         // a marker variable (NOT money!)
    card.action = GiveJackpot;       // runs inside Player.TakeCard() as the current player
    return card;
}

private static void GiveJackpot()
{
    var player = GameManager.instance.currentPlayerScript;
    if (player != null) player.moneyOp(100000);   // moneyOp ADDS the value
}
```

Notes:

* The `give` / `need` / `remove` / `shield` strings are the game's own effect language.
  **`give` cannot move money** — `money=100000` only defines a variable named `money`. Use a
  positive `price`, an `action` delegate, or `income` instead. Full reference:
  [09 — Cards, businesses & the economy](09-cards-and-business-internals.md#4-the-scripted-effect-mini-language).
* `Card` has constructors `Card()` and `Card(string id)`, but prefer **cloning** a card of the same
  category (`DeepCopy`) and sanitising it (see `CardFactory.Create`).
* The card is drawn by `EventHappening` from `player.cardPerTurn`; injecting there is enough. For
  permanent content, add it to the master deck instead (see below).
* Money signs: `Player.moneyOp(value)` **adds** `value`; asset `price` is **negative** for a cost.
  The UI may print values scaled by a per-language currency factor (`TemplateCurrencySettings.CurrentCurrencyMod`),
  so on-screen numbers can be `2×` the internal ones.


### Recipe: add a permanent card to the master deck

`CardsDeck.GetDeck(category)` returns a **new `List<Card>` copy**, so adding to it is a no-op. To add
real content you must reach into the master database (`CardsDeck._cardDeck`, a private
`Dictionary<CardCategory, HashSet<Card>>`) — exactly what `DeckRegistry` (in the examples project)
does:

```csharp
// reference the game type, then write to the private dictionary field.
var field = AccessTools.Field(typeof(CardsDeck), "_cardDeck");
var table = (Dictionary<CardCategory, HashSet<Card>>)field.GetValue(CardsDeck.instance);
table[CardCategory.Happening].Add(BuildCard());
```

The card then participates in the normal `GetAllowedDeckTask` / `GetRandomCardInList` flow (must
pass `Player.canGetACard`: satisfied `need`, no `shield`, not already owned unless `reuse`).
`example.custom-business` uses this to inject a buyable business; remove your instance in
`OnUnload` so disabling the mod really removes it.

See [09 — Cards, businesses & the economy](09-cards-and-business-internals.md#7-adding-your-own-card-to-the-game)
for the full recipe and the list of fields to sanitise.


### Recipe: intercept the popup instead of the card

```csharp
[HarmonyPatch(typeof(UIManager), nameof(UIManager.ShowEvent))]
static class RecolorEvents
{
    static void Prefix(ref Card card)
    {
        // runs whenever any event popup is about to show
    }
}
```

---

## Where custom content already lives (a lead)

`CardsDeck` exposes `LoadCustomCharacter(string filePath)` and `GetCustomCharacter(string json)`,
which means the game itself can build content from JSON. Inspecting their bodies (via
`ilspycmd -t CardsDeck`) is the best reference if you want to ship a data-driven card pack rather
than code.

---

## Checklist

1. Decide: observe, modify, or create.
2. Prefer **sync** hooks (`Player.MoveEnd`, `Player.TakeCard`) over async ones for "after" logic.
3. Modify **copies** (`DeepCopy`) unless you intend a global change.
4. Undo everything in `OnUnload` (patches via `UnpatchSelf`, subscriptions by hand, injected
   cards by removal).
5. Log with `Log`/`ModLog` and test with `example.event-log` running alongside.

Next: [05 — UI & HUD](05-ui-and-hud.md).
