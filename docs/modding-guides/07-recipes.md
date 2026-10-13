# 07 — Recipes (copy-paste cookbook)

Concrete, minimal snippets. Each says **what it targets** and whether it was **verified** in the
real game. Drop them into `OnLoad` (for patches/subscriptions) or `OnUpdate`/`OnGUI` (for
hotkeys/HUD).

> Reminder: if your assembly contains several mods, register patches manually with your mod's
> `Harmony` instance (as here). If it contains one mod, you may use `[HarmonyPatch]` classes.

---

## Hotkey map used by the shipped example mods

Pick keys that don't collide. The loader console is **F10**.

| Key | Mod | Action |
|-----|-----|--------|
| F1 | `example.mood-guard` | prevent mood drops |
| F2 | `example.fast-dice` | force dice value |
| F3 | `example.game-speed` | cycle board speed |
| F4 | `example.hud` | toggle stats HUD |
| F5 | `timeflow.trainer` | toggle HUD |
| F6 | `timeflow.trainer` | add money |
| F7 | `timeflow.trainer` | set income |
| F8 | `timeflow.trainer` | reset income |
| F9 | `example.give-money` | add money (template) |
| F10 | loader | console |

---

## 1. Give money ★ *(verified — trainer, template)*

```csharp
var p = GameManager.instance?.currentPlayerScript;
p?.moneyOp(100000);          // positive adds, negative subtracts; keeps UI/history consistent
```

Log every change by patching `Player.moneyOp` (verified — trainer's postfix proved toggling):

```csharp
Harmony.Patch(AccessTools.Method(typeof(Player), nameof(Player.moneyOp)),
    postfix: new HarmonyMethod(AccessTools.Method(typeof(MyMod), nameof(OnMoneyOp))));

private static void OnMoneyOp(Player __instance, long value)
    => ModLog.Debug($"[money] {value:+#;-#;0} -> {__instance.money}");
```

## 2. Force a dice value ★★ *(verified — `example.fast-dice`)*

Postfix (lets the game maintain its dice cache, only overrides the result):

```csharp
Harmony.Patch(AccessTools.Method(typeof(Player), nameof(Player.GetDice)),
    postfix: new HarmonyMethod(AccessTools.Method(typeof(MyMod), nameof(GetDicePostfix))));

private static bool _diceOn;
private static byte _diceValue = 6;
private static void GetDicePostfix(ref byte __result) { if (_diceOn) __result = _diceValue; }
```

## 3. Speed up the board ★★ *(verified — `example.game-speed`)*

```csharp
Harmony.Patch(AccessTools.Method(typeof(SettingsManager), "GetCellDelay"),
    postfix: new HarmonyMethod(AccessTools.Method(typeof(MyMod), nameof(DelayPostfix))));

private static float _mult = 1f;   // >1 == faster
private static void DelayPostfix(ref float __result) { if (_mult > 1f && __result > 0f) __result /= _mult; }
```

Alternative one-shot global tweak: `SettingsManager.cellDelay = 0.02f;`
(but the per-cell calculation may still apply; patching `GetCellDelay` is authoritative).

## 4. Prevent mood drops ★★★ *(verified — `example.mood-guard`)*

```csharp
Harmony.Patch(AccessTools.Method(typeof(Player), nameof(Player.ChangeMood)),
    prefix: new HarmonyMethod(AccessTools.Method(typeof(MyMod), nameof(MoodPrefix))));

private static bool _guard;
private static bool MoodPrefix(int value) => !(_guard && value < 0);   // false == skip original
```

Related: `Player.BaseMood`, `Player.MoodCoeff`, `Player.mood`, and the constants
`SettingsManager.moodFailure`, `moodFired`, etc.

## 5. Set free time ★

`Player.time` is a **public `int`**:

```csharp
p.time = 999;
```

## 6. Change age ★

```csharp
p.ChangeYearsOld(25);      // set absolute age
p.ChangeYearsPassed(-3);   // shift years passed
```

## 7. Add a persistent income bonus ★★

`Player.income` is a **public field**, but `UpdateBalance()` recomputes income from the player's
cards, so a raw assignment can be overwritten. To make it stick, add your bonus *after* every
recalculation:

```csharp
Harmony.Patch(AccessTools.Method(typeof(Player), nameof(Player.UpdateBalance)),
    postfix: new HarmonyMethod(AccessTools.Method(typeof(MyMod), nameof(BonusPostfix))));

private static long _incomeBonus = 0;
private static void BonusPostfix(Player __instance) { __instance.income += _incomeBonus; }
```

`Player.cashflow` is `income + expense` and updates automatically; `active`/`passive` are separate
fields if the UI rows matter to you.

## 8. Make bets on the market ★★★

`StockManager.instance` exposes `StockTrend`, and `SetTrendManual(targets, trend, offset, duration)`:

```csharp
StockManager.instance.SetTrendManual(SettingsManager.stockCardSilverId,
    StockTrend.UpSharp, /*offset*/ 0.2f, /*duration*/ 6);
```

Enum `StockTrend`: `None, Free, Up, Down, Sideways, UpOnly, UpSharp, UpSmooth, DownOnly,
DownSharp, DownSmooth, DownFixed, UpFixed, DividendsDown, DividendsUp`.

## 9. Log all game messages to a file ★★★ *(verified — `example.event-log`)*

```csharp
Harmony.Patch(AccessTools.Method(typeof(LogManager), nameof(LogManager.Message)),
    postfix: new HarmonyMethod(AccessTools.Method(typeof(MyMod), nameof(OnLog))));

private static void OnLog(string message) { /* append to Paths.LogsDir file */ }
```

Combine with `Player.MoveEnd` (sync) and `GameManager.OnTurnButton` for a full trace. See
`EventLoggerMod.cs`.

## 10. Stats HUD ★★ *(verified — `example.hud`)*

```csharp
public override void OnGUI()
{
    var p = GameManager.instance?.currentPlayerScript;
    GUILayout.BeginArea(new Rect(10, 10, 320, 160), GUI.skin.box);
    if (p != null)
    {
        GUILayout.Label($"Money {p.money:N0}  Flow {p.cashflow:N0}");
        GUILayout.Label($"Time {p.time}  Mood {p.mood}  Age {p.Age}");
        GUILayout.Label($"Success {p.success}%  Cell {p.cellIndex}");
    }
    GUILayout.EndArea();
}
```

Read a **private** field via reflection (verified pattern):

```csharp
var f = AccessTools.Field(typeof(GameManager), "_gameState");
var state = f?.GetValue(GameManager.instance);
```

## 11. Custom "Happening" event ★★★

Inject a card into the deck a Happening cell draws from (see
[04-events-and-cards.md](04-events-and-cards.md) for the full story):

```csharp
Harmony.Patch(AccessTools.Method(typeof(EventHappening), "ActivateEvent"),
    prefix: new HarmonyMethod(AccessTools.Method(typeof(MyMod), nameof(Feed))));

private static void Feed(Player player)
{
    if (player.cardPerTurn.Exists(c => c.id == "mymod.lottery")) return;
    player.cardPerTurn.Insert(0, BuildLotteryCard());
}
```

## 12. Toast / dialog ★★

```csharp
await UIManager.instance.ShowNotification("MyMod", "Turn started!", ct);
await UIManager.instance.ShowDialogYesNo("MyMod", "Continue?", ok => { /* ... */ });
LogManager.instance.Message("<color=#ff6600>MyMod:</color> hello");
```

## 13. Per-save counter ★★

```csharp
p.AddValueToVariableForKey(+1, "mymod.runs");
var runs = p.VariableValueForKey("mymod.runs");
```

See [06-config-and-save-data.md](06-config-and-save-data.md).

## 14. Menu button of your own (native look) ★★★

Clone a live menu button and wire it (see `Builtin/ModMenuButton.cs`). Patch
`MenuView.SetupMenuView` and, in the postfix, instantiate `_settingsButton`'s GameObject into its
parent, strip localisation/hint/EventTrigger components, set the label and `onClick`.

## 15. Block a specific card / clean up bad events ★★★

```csharp
// Before a happening picks its card, drop unwanted entries from the turn deck.
private static void Before(Player player)
    => player.cardPerTurn.RemoveAll(c => c != null && c.category == CardCategory.Habit);
```

## 16. Grant a specific card ★★

```csharp
var card = CardsDeck.instance.GetCardByID("серебро");   // e.g. the silver stock
player.AddCardToCategory(card.category, card);
```

---

## Things that are easy to get wrong

* `Player.money` is **get-only** → use `moneyOp`.
* `income` may be recomputed → use the `UpdateBalance` postfix.
* Async methods fire their prefix/postfix at **call time**, not completion → prefer sync hooks
  for "after" logic.
* Card data is **shared** → `DeepCopy()` before editing, or expect to change the whole game.
* Never leave a patch applied when the mod is disabled — always go through your `Harmony`
  instance so `UnpatchSelf()` can clean up.

Next: [08 — AI agents](08-ai-agents.md).
