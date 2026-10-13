# Modding guide

Everything a mod author needs: project setup, the API, and how to touch the game.

> **Deeper material:** the full handbook lives in
> **[`docs/modding-guides/`](modding-guides/README.md)** — quickstart, the game's own API
> (`02-game-internals.md`), Harmony patching, events & cards, UI/HUD, config & save data, a recipe
> cookbook, and a guide written for AI agents. There are also six runnable example mods in
> `src/Timeflow.Examples/`.

## 1. Create a mod project

The quickest start is to copy `src/Timeflow.ModTemplate` (the `Example.GiveMoney` project).
A mod is a `netstandard2.1` class library with these references (all `Private=false`):

- `Timeflow.ModLoader.dll` — the API
- `0Harmony.dll` — for Harmony attributes
- `Assembly-CSharp.dll` — the game's code (so you can reference game types)
- the `UnityEngine.*` modules you use

`Directory.Build.props` already defines `$(GameManagedDir)` and `$(VendorDir)`, so:

```xml
<Reference Include="Assembly-CSharp">
  <HintPath>$(GameManagedDir)\Assembly-CSharp.dll</HintPath>
  <Private>false</Private>
</Reference>
```

Build with `dotnet build`, then copy the DLL into `<Timeflow>\Mods\` (the template's
`DeployMod` target does this automatically).

### Enabling a mod

For safety, **mods are disabled by default**. The first time a mod is present you switch it on
in-game:

1. Launch the game and, on the main menu, click **Моды / Mods** (the button sits right under
   **Настройки / Settings**).
2. The menu panel turns into the mod list: one row per mod showing its name and state.
3. Click a row to toggle that mod **on the fly** — patched methods are applied/unpatched
   immediately, no restart needed.
4. Click **Назад / Back** to return to the menu.

The choice is written to `<Timeflow>\TimeflowModLoader\Config\mod_states.cfg`
(`<mod id>=enabled|disabled`) and restored on the next launch.

## 2. Write a mod

```csharp
using Timeflow.ModLoader;
using Timeflow.ModLoader.Modding;
using UnityEngine;

[Mod("you.my-mod",
     Name = "My Mod",
     Version = "1.0.0",
     Author = "you",
     Description = "Does something.",
     Dependencies = new[] { "other.mod" },   // optional
     LoadPriority = 10)]                      // optional, higher loads first
public sealed class MyMod : ModBase
{
    public override void OnLoad()
    {
        Log.Info("Hello from " + Info.Name + " v" + Info.Version);

        var speed = Config.GetFloat("speedMultiplier", 1.5f);
        Config.Set("speedMultiplier", speed);
        Config.Save();

        GameEvents.SceneLoaded += OnSceneLoaded;
    }

    public override void OnUpdate()
    {
        if (Input.GetKeyDown(KeyCode.F9))
            Log.Info("F9 pressed");
    }

    public override void OnUnload()
    {
        GameEvents.SceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        => Log.Info("scene " + scene.name);
}
```

### `[Mod]` parameters

| Property | Meaning |
| --- | --- |
| `Id` (ctor arg) | Unique id, e.g. `author.modname`. Required. |
| `Name`, `Version`, `Author`, `Description` | Metadata, shown in logs. |
| `Dependencies` | Ids that must load first. Missing dep ⇒ this mod is skipped. |
| `LoadPriority` | Higher loads earlier when there is no ordering constraint. |

### `ModBase` members

| Member | Purpose |
| --- | --- |
| `Info` | `ModInfo` (id, name, version, assembly path, type). |
| `Log` | `ModLogger` — `Log.Info/Warn/Error/Debug`. Every line is tagged with the mod id. |
| `Harmony` | A `Harmony` instance bound to this mod. |
| `Config` | `ModConfig` — persistent INI file at `TimeflowModLoader\Config\<id>.cfg`. |
| `OnLoad/OnUpdate/OnFixedUpdate/OnLateUpdate/OnGUI/OnUnload` | Virtual lifecycle callbacks. |

## 3. Harmony patches

Attribute-based patches in your mod assembly are applied **automatically** — the loader runs
`Harmony.PatchAll(modAssembly)` before `OnLoad`. You do not need to call `PatchAll` yourself.

```csharp
[HarmonyPatch(typeof(Player), nameof(Player.moneyOp))]
internal static class MoneyPatch
{
    [HarmonyPostfix]
    static void Postfix(Player __instance, long value)
        => ModLog.Debug($"[{__instance.name}] {value:+#;-#;0} -> {__instance.money}");
}
```

If you prefer manual control, call `Harmony.Patch(...)` from `OnLoad`.

> Use **`ModLog`** (static) or `Log`/`ModLogger` — not `Logger` — because `using UnityEngine;`
> already brings in `UnityEngine.Logger`.

## 4. Global events (`GameEvents`)

| Event | When |
| --- | --- |
| `SceneLoaded` | After a scene finishes loading. |
| `ApplicationQuitting` | Before the process exits. |
| `AllModsLoaded` | First frame after every mod is loaded. |

Always unsubscribe in `OnUnload`.

## 5. Game API cheat-sheet

Types are in the **global namespace**. Signatures below were recovered from
`Assembly-CSharp.dll`.

| Member | Notes |
| --- | --- |
| `GameManager.instance` | Singleton; may be null in menus. |
| `GameManager.instance.InGame` | Is a match running. |
| `GameManager.instance.currentPlayerScript` | `Player` whose turn it is. |
| `GameManager.instance.Players`, `.MyPlayer`, `.firstPlayer` | Player access. |
| `GameManager.instance.cellsInMonth`, `.boardCells` | Board/time model. |
| `Player.money` | `long`, get-only — read the balance. |
| `Player.moneyOp(long value, bool showEffect = true)` | Change money (handles UI/effects). |
| `Player.income`, `Player.salary`, `Player.cashflow` | Economy fields. |
| `Player.Age`, `Player.ChangeYearsOld(int)` | Ageing. |
| `TimeView.ShowTime(bool editMode, bool fromLoan, Card callCard = null, bool mayClose = false)` | Time/study UI. |
| `SettingsManager` | Difficulty / settings. `SettingsManager.PreciseMoves`, crypto scam levels. |
| `Localization` | Text lookup. |
| `SoundManager`, `UIManager` | Audio and UI managers. |
| `SteamManager` | Steamworks wrapper. |
| `Achievements.AchievementsManager` | Achievements. |
| `SaveSystem.*`, `SaveSystem.Data.*` | Save/load (`GameData`, `PlayerData`, ...). |

Other notable classes seen in the assembly: `GameStarter`, `BoardCell(s)`, `BoardsManager`,
`Card`, `CharacterCard`, `PlayerInfoView`, `EventView`, `GameOverView`, `UIManager`,
`CharacterEditor*`.

To enumerate more, dump the assembly (see `AGENTS.md`):

```powershell
dotnet ilspycmd -l c "..\Timeflow\Timeflow_Data\Managed\Assembly-CSharp.dll"
```

## 6. Debugging

- Logs: `TimeflowModLoader\Logs\ModLoader.log` and `output_log.txt` (next to `Timeflow.exe`).
- In-game console overlay: press **F10**.
- Managed debugger: set `debug_enabled=true` (and optionally `debug_suspend=true`) in
  `doorstop_config.ini`, then attach (dnSpy: *Debug → Unity*).
