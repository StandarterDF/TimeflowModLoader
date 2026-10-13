# 06 — Config & save data

Three separate storage layers. Pick the right one:

| Layer | Persists across... | Use for |
|-------|--------------------|---------|
| `ModConfig` (`Config\&lt;id&gt;.cfg`) | launches, all saves | Mod settings (speed, hotkey choices, flags). |
| `mod_states.cfg` | launches | Whether the mod is enabled. Managed by the Mods menu. |
| Game save (`PlayerData.Variables`) | save/load/continue | Per-player progress your mod must keep with the save. |
| Your own file (`Paths.ConfigDir`/`LogsDir`) | launches | Logs, exports, anything large/custom. |

---

## 1. `ModConfig` — per-mod settings

File: `<Timeflow>\TimeflowModLoader\Config\<sanitised mod id>.cfg`. INI-like, human-editable.

```csharp
public override void OnLoad()
{
    _speed = Config.GetFloat("speed", 2f);
    _enabled = Config.GetBool("auto-enable", false);

    Config.Set("speed", _speed);   // write in memory
    Config.Save();                 // flush to disk
}
```

| Member | Behaviour |
|--------|-----------|
| `GetString(key, default)` | Returns `default` if missing. |
| `GetInt(key, default)` | Invariant culture. |
| `GetFloat(key, default)` | Invariant culture (so `1.5` parses on `ru` locales too). |
| `GetBool(key, default)` | Accepts `true/1/yes/on`, `false/0/no/off`. |
| `Set(key, object)` | Stores in memory; `Save()` writes the file. |
| `HasKey(key)` | Existence check. |
| `FilePath` | Absolute path of the file. |

Comments start with `#` or `;`. Keys are case-insensitive. `Save()` creates the directory.

Number formatting is `InvariantCulture` on purpose — never print a comma decimal separator.

---

## 2. On/off state

`<Timeflow>\TimeflowModLoader\Config\mod_states.cfg`:

```
# Timeflow ModLoader - per-mod enabled state
# Format: <mod id>=enabled|disabled
me.mymod=enabled
```

**Default is disabled.** The Mods menu calls `ModManager.SetEnabled(id, bool)` which loads/unloads
the mod *and* rewrites this file. You don't touch it directly. You can inspect your state at
runtime via `entry.Loaded` (see `ModManager.All`).

---

## 3. Useful paths (`Timeflow.ModLoader.Paths`)

```csharp
using Timeflow.ModLoader;

Paths.GameRoot     // <Timeflow>\
Paths.ModsDir      // <Timeflow>\Mods
Paths.DataRoot     // <Timeflow>\TimeflowModLoader
Paths.CoreDir      // <Timeflow>\TimeflowModLoader\Core
Paths.ConfigDir    // <Timeflow>\TimeflowModLoader\Config
Paths.LogsDir      // <Timeflow>\TimeflowModLoader\Logs
Paths.LoaderDir, Paths.ManagedDir
```

**Verified:** `example.event-log` writes `<Timeflow>\TimeflowModLoader\Logs\example-events.log`
using `Paths.LogsDir`.

Writing your own file:

```csharp
var path = Path.Combine(Paths.ConfigDir, "mymod-data.json");
File.WriteAllText(path, json);
```

Always wrap file IO in try/catch and **flush/close** in `OnUnload` (the process may be killed).

---

## 4. Persisting inside the game save

The player's `variables` dictionary is serialised into `PlayerData.Variables` and restored on
load. It is the cleanest "my mod remembers this playthrough" storage.

```csharp
// Namespace your keys to avoid collisions with the game and other mods.
private const string ScoreKey = "mymod.score";

// write
player.AddValueToVariableForKey(+1, ScoreKey);        // increment by 1
player.SetValueToVariableForKey(42, ScoreKey);        // set to 42

// read
int score = player.VariableValueForKey(ScoreKey);
```

Caveats:

* These variables are also used by cards/events. **Prefix every key** (`mymod.…`) to avoid
  clobbering game logic.
* The engine keeps tuning constants `SettingsManager.variableValueMin` (0) and `variableValueMax`
  (100); treat the dictionary as *small integers used by gameplay*. For large text/structs, save
  your own JSON file in `ConfigDir` and only keep an id/counter in `variables`.
* `player.variables` is a plain `Dictionary<string,int>`; you can read it directly.

### When to load per-save data

Your mod runs from boot, before any save is loaded. React to scene changes and check for a player:

```csharp
public override void OnLoad()
{
    GameEvents.SceneLoaded += OnScene;
}

private void OnScene(Scene s, LoadSceneMode m)
{
    var gm = GameManager.instance;
    if (gm == null || !gm.InGame || gm.firstPlayer == null) return;
    _score = gm.firstPlayer.VariableValueForKey(ScoreKey);
    // ... restore mod state for this save
}
```

Because `Player`s are recreated when a game starts, re-read on every `GameScene` load.

### Hooking the save itself (advanced)

If you must inject data at the moment of saving, patch the synchronous entry points:

```csharp
// GameManager.SaveParty(bool auto) and SavesProvider.SaveParty(...) are async UniTask.
// A Prefix runs before serialisation starts.
Harmony.Patch(AccessTools.Method(typeof(GameManager), "SaveParty"),
    prefix: new HarmonyMethod(AccessTools.Method(typeof(MyMod), nameof(BeforeSave))));
```

Prefer `variables`; only patch the save flow if the data can't be expressed there.

---

## 5. A complete example

```csharp
[Mod("me.scorekeeper", Name = "Score Keeper")]
public sealed class ScoreKeeper : ModBase
{
    private const string Key = "scorekeeper.points";

    private bool _announce;
    private int _points;

    public override void OnLoad()
    {
        _announce = Config.GetBool("announce", true);
        GameEvents.SceneLoaded += OnScene;
        Harmony.Patch(AccessTools.Method(typeof(Player), "MoveEnd"),
            postfix: new HarmonyMethod(AccessTools.Method(typeof(ScoreKeeper), nameof(OnMove))));
    }

    private void OnScene(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m)
    {
        var p = GameManager.instance != null ? GameManager.instance.firstPlayer : null;
        _points = p != null ? p.VariableValueForKey(Key) : 0;
    }

    private static void OnMove(Player __instance) { /* static hook */ }

    public override void OnUnload()
    {
        GameEvents.SceneLoaded -= OnScene;
        Config.Save();
    }
}
```

---

## Rules of thumb

1. Settings → `Config`. Progress → game save (`variables`). Logs/exports → `Paths.LogsDir`.
2. Prefix every `variables` key with your mod id.
3. Flush/close files in `OnUnload`; the game process can be killed without notice.
4. Never store absolute machine-specific paths in the save.
5. Re-read per-save state on `SceneLoaded`, not once at boot.

Next: [07 — Recipes](07-recipes.md).
