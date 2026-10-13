# 08 — Guide for AI agents / LLMs

This file is intentionally **explicit and structured**. If you are an autonomous agent asked to
write, change, debug, or extend a Timeflow mod, read this file first, then only the guide(s) you
need. Human-facing depth lives in the sibling files.

---

## 0. TL;DR contract

* A mod = a `netstandard2.1` class library with one or more `[Mod]` classes deriving from
  `ModBase`, dropped into `<Timeflow>\Mods\`.
* Mods are **disabled by default**; they are enabled in the in-game **Моды** menu, and that state
  is persisted to `Config\mod_states.cfg`.
* Patch the game with HarmonyX (`0Harmony.dll`). **Never** patch methods containing native
  internal calls (`GameObject..ctor`, `AddComponent`, `MonoBehaviour..ctor`).
* Build → auto-deploy → toggle in-game. Logs are the source of truth.

---

## 1. Repository map (paths relative to `Timeflow_MODLOADER\`)

```
Directory.Build.props                 shared MSBuild props (GameRoot/GameManagedDir/VendorDir)
TimeflowModLoader.sln                 solution
doorstop_config.ini                   Doorstop config (deployed next to Timeflow.exe)
src/Timeflow.ModLoader/               THE LOADER (does not depend on Assembly-CSharp at compile time)
  Modding/{IMod,ModBase,ModAttribute,ModInfo}.cs   public mod API
  Config/{ModConfig,ModStateStore}.cs              per-mod config + on/off state
  Builtin/{ModMenuButton,ModsPanel,Texts}.cs       native Mods menu
  Patching/HarmonyProvider.cs                      root + per-mod Harmony instances
  GameEvents.cs, ModManager.cs, Startup.cs, Paths.cs, Logging/ModLog.cs
src/Timeflow.ModTemplate/             minimal example (Example.GiveMoney)
src/Timeflow.Trainer/                 test trainer
src/Timeflow.Examples/                eight example mods of increasing difficulty (manual patches)
docs/                                 MODDING_GUIDE, ARCHITECTURE, TROUBLESHOOTING
docs/modding-guides/                  this handbook
tools/{setup,build,deploy}.ps1        build/deploy scripts
tools/ApiIndexer/                     generates the complete API index (Mono.Cecil)
reference/                            decompilation dumps (gitignored, regenerated)
reference/API/api_index.{json,tsv,md} full type+member index of Assembly-CSharp.dll
```

The game install is `GameRoot` (default `..\Timeflow`, override with
`dotnet build -p:GameRoot="D:\...\Timeflow"`).

---

## 2. Exact commands (Windows PowerShell)

```powershell
# one-time: fetch Doorstop + Harmony into vendor/
powershell -ExecutionPolicy Bypass -File tools\setup.ps1

# build everything
dotnet build TimeflowModLoader.sln -c Release

# deploy the loader into the game
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1

# build + deploy at once
powershell -ExecutionPolicy Bypass -File tools\build.ps1

# add a mod project to the solution
dotnet sln TimeflowModLoader.sln add src\MyMod\MyMod.csproj

# decompile (member dump)
dotnet ilspycmd -t Player "..\Timeflow\Timeflow_Data\Managed\Assembly-CSharp.dll"
dotnet ilspycmd -l c "..\Timeflow\Timeflow_Data\Managed\Assembly-CSharp.dll"
```

Mod DLLs are copied automatically by each mod's `DeployMod` MSBuild target into
`$(GameRoot)\Mods`. A **loaded** DLL cannot be overwritten on Windows — close the game or disable
the mod and restart before rebuilding.

---

## 3. Mod skeleton (canonical form)

```csharp
using HarmonyLib;                        // for manual patching
using Timeflow.ModLoader;                // GameEvents, Paths
using Timeflow.ModLoader.Modding;        // [Mod], ModBase
using Timeflow.ModLoader.Logging;        // ModLog (static), ModLogger
using UnityEngine;

[Mod("author.id", Name = "Name", Version = "1.0.0", Author = "author",
     Description = "...", LoadPriority = 0)]
public sealed class MyMod : ModBase
{
    public override void OnLoad()
    {
        Log.Info("loaded");
        GameEvents.SceneLoaded += OnScene;
        Harmony.Patch(AccessTools.Method(typeof(Player), nameof(Player.GetDice)),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(MyMod), nameof(GetDicePostfix))));
    }

    public override void OnUpdate() { /* hotkeys */ }
    public override void OnGUI()    { /* optional HUD */ }

    public override void OnUnload()
    {
        GameEvents.SceneLoaded -= OnScene;   // patches are removed by the loader (UnpatchSelf)
    }

    private void OnScene(UnityEngine.SceneManagement.Scene s,
                         UnityEngine.SceneManagement.LoadSceneMode m) { }

    private static void GetDicePostfix(ref byte __result) { __result = 6; }
}
```

Multiple mods per assembly ⇒ **no `[HarmonyPatch]` attributes**, patch manually as above.
Single mod per assembly ⇒ attribute patches are auto-applied (`Harmony.PatchAll`) before `OnLoad`.

---

## 4. Machine-readable API index

**First stop:** the generated, exhaustive index described in
[10 — Full API reference](10-api-reference.md). Regenerate with:

```powershell
dotnet run --project tools\ApiIndexer -c Release -- "..\Timeflow\Timeflow_Data\Managed\Assembly-CSharp.dll" "reference\API"
# → reference/API/api_index.json (all types+members), .tsv (grep), .md (readable)
```

Everything below is a curated shortcut for planning; the index is the ground truth.

Key game types/members for quick planning (all verified from `Assembly-CSharp.dll`; every type is
in the global namespace unless prefixed).

```json
{
  "singletons": {
    "GameManager":  "GameManager.instance",
    "SettingsManager": "SettingsManager.instance (instance members) + many static tunables",
    "CardsDeck":    "CardsDeck.instance",
    "UIManager":    "UIManager.instance",
    "LogManager":   "LogManager.instance",
    "StockManager": "StockManager.instance",
    "SoundManager": "SoundManager.instance",
    "TutorialManager": "TutorialManager.instance"
  },
  "GameManager": {
    "state": ["InGame", "IsNetworkGame", "IsHost", "Players", "currentPlayerScript", "firstPlayer", "MyPlayer"],
    "board": ["boardCells(GameObject[])", "cellsInMonth", "NextCell", "NextCellIndex", "MonthForCellIndex", "GetCellInfo"],
    "actions": ["OnTurnButton", "OnPreciseMove", "MakeCheatTurn(byte)", "InsertAction", "AddCoAction"],
    "events": ["PlayerMoveStart(Action<Player>)", "PlayerMoveEnd(Action<Player>)"]
  },
  "Player": {
    "identity": ["title", "isBot", "Race", "Difficulty", "cellIndex", "cellsPassed", "yearsPassed", "monthsPassed", "Age", "time"],
    "money": ["money(get-only long)", "moneyOp(long,bool=true)", "MoneyOpValue(long)", "income", "expense", "cashflow", "active", "passive", "salary", "hourPrice", "allMoney", "UpdateBalance()"],
    "mood": ["mood", "moodWithBonus", "BaseMood", "MoodCoeff", "ChangeMood(int)"],
    "dice": ["GetDice()", "GenerateDicesCache()", "CheatTurn(byte)", "PreciseMove(byte)", "DicesCache"],
    "cards": ["GetCardsForCategory(CardCategory)", "AddCardToCategory(CardCategory,Card)", "TakeCard(Card,bool,bool)", "FindCard(string)", "HasCard(string)", "RemoveCardWithID(string,bool,bool,bool)", "cardPerTurn(List<Card>)"],
    "vars": ["variables(Dictionary<string,int>)", "AddValueToVariableForKey(int,string)", "SetValueToVariableForKey(int,string)", "VariableValueForKey(string)"],
    "age": ["ChangeYearsOld(int)", "ChangeYearsPassed(int)"]
  },
  "SettingsManager": {
    "speed": ["GetCellDelay(int)(static float)", "cellDelay", "fadeInOutSpeed", "dicesCacheSize"],
    "balance": ["timeFree", "timeMin", "ageForRetirement", "cellsCountInMonth", "moneyEdgeL/M/H", "cashflowEdge", "successCashflow", "successMoney", "successFactor"],
    "mood": ["moodBase", "moodMonthlyStep", "moodFailure", "moodFired", "moodResign", "moodBoostUp", "moodBoostDown"],
    "bank": ["loanSumMin/Max", "loanPercent", "loanPercentCoeff", "depositShortPercent", "depositLongPercent"],
    "stocks": ["stockDefaultChance", "stockDefaultOffsetUp/Down", "stockMinPrice", "stockMaxPrice"],
    "instance": ["soundVolume", "musicVolume", "envVolume", "uiScale", "currentLanguage", "currentFps", "boardField", "SaveSettings()"]
  },
  "CardsDeck": ["GetCardByID(string)", "GetDeck(CardCategory)", "GetRandomCardInCategory", "GetMonthCard(int)", "GetEmptyCard()", "LoadCustomCharacter(string)"],
  "BoardCell": ["eventScript(EventAbstract)", "ActivateCellEvent(Player)(async)"],
  "EventAbstract": ["ActivateEvent(Player)(async, abstract)", "GetCardCategory()"],
  "EventImpl": ["EventIncome", "EventMonth", "EventEducation", "EventHappening", "EventNews", "EventRoutine", "EventWeekend"],
  "UIManager": ["ShowNotification", "ShowDialogYesNo", "ShowEvent(Card)", "ShowCardView", "ShowTime", "ShowSettings", "ShowMenu", "CloseAllViews"],
  "LogManager": ["Message(string, Dictionary<string,string> = null)"],
  "enums": {
    "CardCategory": ["Dream","Habit","Work","Month","Weekend","People","Routine","News","Happening","BusinessSmall","BusinessMedium","BusinessBig","RealtySmall","RealtyMedium","RealtyBig","HighEducation","Workshop","TimeManagement","Stock","Loan","Deposit","Other","Notification","MonthAction","Family","BalanceInfo","DreamPart"],
    "Race": ["Euro","Asian","Afro"],
    "GameDifficulty": ["Tutorial","Easy","Medium","High","Special","HardCore"],
    "StockTrend": ["None","Free","Up","Down","Sideways","UpOnly","UpSharp","UpSmooth","DownOnly","DownSharp","DownSmooth","DownFixed","UpFixed","DividendsDown","DividendsUp"],
    "BalanceState": ["Undefined","Common","Income","Expense","Active","Passive","Time","Character"]
  }
}
```

---

## 5. Loader facts an agent must not get wrong

1. **Bootstrap uses Harmony on pure-managed Unity methods only** (`UnityEngine.Application::.cctor`
   with `Debug::Log/LogError` fallbacks). Do not add a bootstrap hook that touches native
   internal-call methods.
2. `ModManager.LoadAll()` discovers, sorts (dependencies + `LoadPriority`), and **only enables
   mods whose `mod_states.cfg` says enabled**. Everything else logs
   `Mod disabled in config: <id>`.
3. Enabling calls, in order: create `Harmony` → `Activator.CreateInstance` → `Bind(Info,Harmony)`
   → `Harmony.PatchAll(assembly)` → `OnLoad()`. Disabling calls `OnUnload()` → `UnpatchSelf()`.
4. Callbacks are individually try/caught; a mod failure never crashes the game.
5. `ModLog` is the logger name (not `Logger`), to avoid the `UnityEngine.Logger` clash.

---

## 6. Agent workflow for "add/modify a mod"

1. Read `src/Timeflow.Examples/` for the closest existing pattern; copy its csproj.
2. Add the project: `dotnet sln TimeflowModLoader.sln add ...`.
3. Implement `[Mod]` + `ModBase`; patch manually if the assembly holds several mods.
4. `dotnet build TimeflowModLoader.sln -c Release` (fix all errors/warnings).
5. Launch the game; in the **Моды** menu enable your mod.
6. Verify via `TimeflowModLoader\Logs\ModLoader.log` and, if there's UI, a screenshot.
7. Toggle the mod off in the menu and confirm the effect/patch is gone.

### Verifying with desktop automation (Windows MCP)

* Screenshots are downscaled: **multiply image coordinates by 2.0** for clicks.
* Move the cursor off buttons before focusing the window (focus can emit a click).
* Window automation may drop synthetic key events — focus the game and send keys through the OS
  (e.g. `SendKeys`). This was required to make the F-keys reach the mods.
* Useful checks: read `ModLoader.log` (mod load/unload), `mod_states.cfg` (toggle persisted),
  and any mod-specific log file under `Logs\`.

---

## 7. Failure modes → fixes

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Mod not in the menu | not discovered | DLL must be in `Mods\`; type needs `[Mod]` + public parameterless ctor; check log |
| Mod loads but nothing happens | patch target wrong | `AccessTools.Method` returned null; log the target and verify with `ilspycmd` |
| All GameObject creation breaks (`Internal_CreateGameObject` `MissingMethodException`) | patched a method with native internal calls | patch a pure-managed method instead |
| Dice/money logic desyncs | skipped the original in a Prefix | use a Postfix, or let the original run |
| Patch stays after disable | applied outside the mod's Harmony instance | only use `this.Harmony` |
| Two mods in one DLL interfere | used `[HarmonyPatch]` attributes in a multi-mod assembly | switch to manual patching |
| Config not saved | forgot `Config.Save()` | call it after `Set` |
| Hotkeys ignored | window not focused / keys dropped | focus the game; verify with logs |

---

## 8. Where the truth lives

* `reference/API/api_index.{json,tsv,md}` — **generated complete index** of every type/member (via `tools/ApiIndexer`).
* `reference/*.cs` and `reference/*_dump.txt` — decompiled game code (regenerate with `ilspycmd`).
* `docs/modding-guides/09-cards-and-business-internals.md` — card/business/economy model.
* `docs/modding-guides/10-api-reference.md` — API index usage + subsystem map.
* `docs/ARCHITECTURE.md` — how the loader boots.
* `docs/TROUBLESHOOTING.md` — Doorstop/loader problems.
* `AGENTS.md` (repo root) — build conventions and RE tooling.

When unsure about a game method's signature: **dump it, don't guess.**
