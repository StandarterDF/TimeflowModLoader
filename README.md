# Timeflow ModLoader

A mod loader for the Unity game **Timeflow**. Drop mods into a folder and they load
automatically; patches are applied with Harmony via [HarmonyX](https://github.com/BepInEx/HarmonyX).

- Game: Timeflow (Steam), Unity 2022.3.60f1, Mono, x86_64
- Injection: [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop) (`winhttp.dll`)
- Mod API: `[Mod]` attribute + `IMod`/`ModBase` + Harmony

## Install

1. Download / build the release (see *Building* below).
2. Copy into the folder that contains `Timeflow.exe`:
   - `winhttp.dll`
   - `doorstop_config.ini`
   - `TimeflowModLoader\Core\Timeflow.ModLoader.dll`
   - `TimeflowModLoader\Core\0Harmony.dll`
3. Launch the game. A `TimeflowModLoader\Logs\ModLoader.log` file appears.

The easy way is the deploy script:

```powershell
powershell -ExecutionPolicy Bypass -File tools\build.ps1 -GameRoot "C:\Path\To\Timeflow"
```

To remove it again:

```powershell
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 -GameRoot "C:\Path\To\Timeflow" -Uninstall
```

## Using mods

Put mod `.dll` files (or one subfolder per mod) into:

```
<Timeflow>\Mods\
```

Each mod must contain a class marked `[Mod(...)]` that implements `IMod` (usually by deriving
from `ModBase`). See **[docs/MODDING_GUIDE.md](docs/MODDING_GUIDE.md)** and the ready-to-copy
`src/Timeflow.ModTemplate` project.

**Mods are disabled by default.** Turn them on in the main menu via the native **Моды / Mods**
button (under *Settings*): the menu panel becomes a list of mods, click a row to enable/disable it
live (patches are applied/removed instantly), then **Назад / Back**. The choice is stored in
`TimeflowModLoader\Config\mod_states.cfg`.

Press **F10** in game to open the loader console (log tail + paths + mods folder).

### Example mods

`src/Timeflow.Examples/` contains eight runnable mods of increasing difficulty (hello-world, forced
dice, game speed, stats HUD, event logger, mood guard, a **custom business** and a **custom event**)
plus the `Trainer`. Read them, or run them to see each technique in action — the two content mods
are the reference implementations for [chapter 09](docs/modding-guides/09-cards-and-business-internals.md).

## Building from source

```powershell
powershell -ExecutionPolicy Bypass -File tools\setup.ps1   # fetch Doorstop + HarmonyX
dotnet build TimeflowModLoader.sln -c Release
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1  # default path: ..\Timeflow
```

`-p:GameRoot=<path>` points the build at a different game installation.

## Documentation

- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — how the loader works internally
- [docs/MODDING_GUIDE.md](docs/MODDING_GUIDE.md) — writing mods (short version)
- [docs/modding-guides/](docs/modding-guides/README.md) — **the full handbook**: quickstart, game
  internals/API, Harmony patching, events & cards, UI/HUD, config & save data, recipes, a deep
  card/business/economy reference, a full generated API index, and an AI-agent guide
- [docs/modding-guides/10-api-reference.md](docs/modding-guides/10-api-reference.md) — the API
  index (regenerate with `dotnet run --project tools\ApiIndexer -- <Assembly-CSharp.dll> reference\API`)
- [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) — when something does not load
