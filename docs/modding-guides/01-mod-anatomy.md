# 01 — Mod anatomy

Everything a mod is made of: the `[Mod]` attribute, the `IMod`/`ModBase` contract, the lifecycle,
ordering, logging and configuration.

---

## The `[Mod]` attribute

```csharp
[Mod("author.modname",
     Name = "Pretty Name",
     Version = "1.0.0",
     Author = "author",
     Description = "What it does.",
     Dependencies = new[] { "other.mod.id" },
     LoadPriority = 10)]
public sealed class MyMod : ModBase { ... }
```

| Property | Required | Meaning |
|----------|----------|---------|
| `Id` (constructor argument) | ✅ | Stable unique id, e.g. `author.modname`. Used for the config file name, the saved on/off state, and dependency resolution. Keep it stable across versions. |
| `Name` | — | Human-readable name shown in logs and in the Mods menu. |
| `Version` | — | `"1.0.0"`; shown in logs. |
| `Author` | — | Free text. |
| `Description` | — | Free text (not shown in the native menu yet, available in `Info.Description`). |
| `Dependencies` | — | Ids that must be enabled before this one. If a dependency is missing/disabled, the mod is skipped at load time. |
| `LoadPriority` | — | Higher loads earlier among mods with no dependency relation. Default 0. |

The class must:

* implement `IMod` (deriving from `ModBase` is the easy way), and
* have a **public parameterless constructor**.

## Discovery rules

On startup the loader scans `<Timeflow>\Mods\` (recursively) for `.dll` files, loads each
assembly and looks for public types annotated with `[Mod]`. Notes:

* `Timeflow.ModLoader.dll` and `0Harmony.dll` are ignored.
* One assembly may contain **several** `[Mod]` types — all are discovered (see the
  `Timeflow.Examples` project). But see the patching caveat below.
* A failing assembly is logged and skipped; it does not stop the loader.
* Dependencies and `LoadPriority` determine enable order (`ModSorter`).

## The `IMod` / `ModBase` contract

`ModBase` provides everything below plus the lifecycle hooks. Derived mods override only the
hooks they need.

| Member | Type | Purpose |
|--------|------|---------|
| `Info` | `ModInfo` | `Id`, `Name`, `Version`, `Author`, `Description`, `Assembly`, `AssemblyPath`, `ModType`. Assigned before `OnLoad`. |
| `Log` | `ModLogger` | Scoped logger; every line is tagged with the mod id. `Log.Info/Warn/Error/Debug`. |
| `Harmony` | `Harmony` | A Harmony instance whose id is unique to this mod. Required for manual patching and for `UnpatchSelf` on disable. |
| `Config` | `ModConfig` | Persistent per-mod settings file (see below). |
| `OnLoad()` | lifecycle | Once, right after the instance is created and patches applied. |
| `OnUpdate()` | lifecycle | Every frame, main thread. |
| `OnFixedUpdate()` | lifecycle | Every fixed step. |
| `OnLateUpdate()` | lifecycle | Every frame, after Update. |
| `OnGUI()` | lifecycle | During the IMGUI pass — draw HUDs here. |
| `OnUnload()` | lifecycle | When the mod is disabled at runtime, or the game quits. |

> Every callback is wrapped in a try/catch by the loader: **an exception in one mod is logged and
> never breaks the game or the other mods**. Still, keep hot paths (`OnUpdate`) cheap.

### Lifecycle order

```
Discovery/ordering
  └─ per enabled mod, in order:
       1. Harmony instance created
       2. instance created (parameterless ctor)
       3. ModBase.Bind(Info, Harmony)   → Info/Log/Harmony/Config ready
       4. Harmony.PatchAll(assembly)    → attribute patches applied
       5. OnLoad()                      → your manual patches / subscriptions
   ...
   GameEvents.RaiseAllModsLoaded()       → after ALL mods are loaded
   every frame: OnUpdate → OnLateUpdate, plus OnFixedUpdate and OnGUI
```

On disable (runtime toggle, or quit): `OnUnload()` is called, then `Harmony.UnpatchSelf()`
removes every patch applied through this mod's Harmony instance.

## Logging

Two ways, both fine:

```csharp
// Inside a ModBase: instance logger, tagged with the mod id.
Log.Info("loaded; speed=" + speed);
Log.Warn("something odd");
Log.Error("failed: " + ex.Message);
Log.Debug("verbose");

// Anywhere (including static patch methods): the shared loader logger.
using Timeflow.ModLoader.Logging;
ModLog.Info("[mymod] from a static postfix");
```

Output goes to the in-memory console buffer (F10) and to
`<Timeflow>\TimeflowModLoader\Logs\ModLoader.log`. The global minimum level is configurable via
`ModLog.MinLevel`.

> **Never use `Logger`** — `using UnityEngine;` brings in `UnityEngine.Logger`. Use `ModLog`.

## Configuration

`ModConfig` is a tiny INI-like file at
`<Timeflow>\TimeflowModLoader\Config\<sanitised mod id>.cfg`.

```csharp
public override void OnLoad()
{
    _multiplier = Config.GetFloat("multiplier", 2f);   // read with default
    Config.Set("multiplier", _multiplier);             // write
    Config.Save();                                     // flush to disk
}

public override void OnUpdate()
{
    if (Input.GetKeyDown(KeyCode.F3))
    {
        _multiplier += 0.5f;
        Config.Set("multiplier", _multiplier);
        Config.Save();
    }
}
```

| Method | Notes |
|--------|-------|
| `GetString/GetInt/GetFloat/GetBool(key, default)` | Missing/!parseable keys return the default. Bools accept `true/1/yes/on` and `false/0/no/off`. |
| `Set(key, value)` | Stores in memory only until `Save()`. |
| `Save()` | Writes the file (creates directories). |
| `HasKey(key)`, `FilePath` | Introspection. |

Numbers are written/parsed with `InvariantCulture`, so `1.5` works on every locale.

## The on/off state (default = disabled)

`ModStateStore` keeps `<Timeflow>\TimeflowModLoader\Config\mod_states.cfg`:

```
timeflow.trainer=enabled
example.fast-dice=disabled
```

**A mod is disabled unless the file says otherwise.** The Mods menu writes this file when the
player toggles a row. You do not need to do anything.

## Manual vs attribute patching (important for multi-mod assemblies)

The loader applies `Harmony.PatchAll(modAssembly)` for every enabled mod. If one assembly
contains two mods that both use `[HarmonyPatch]` classes, enabling either one patches **all**
`[HarmonyPatch]` classes in that assembly — the per-mod toggle then leaks across mods.

Two safe patterns:

* **One mod per assembly** — use attributes freely (like `Timeflow.ModTemplate` and
  `Timeflow.Trainer`).
* **Several mods per assembly** — avoid `[HarmonyPatch]` and register patches manually in
  `OnLoad` using the mod's own `Harmony` instance (like `Timeflow.Examples`). Then
  `UnpatchSelf` removes only that mod's patches.

See [03-harmony-patching.md](03-harmony-patching.md) for both styles.

Next: [02 — Game internals](02-game-internals.md).
