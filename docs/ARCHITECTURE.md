# Architecture

## Overview

```
Timeflow.exe
 └─ loads winhttp.dll (UnityDoorstop)
     └─ mono_jit_init → loads TimeflowModLoader\Core\Timeflow.ModLoader.dll
         └─ Doorstop.Entrypoint.Start()
             └─ Bootstrap.Run()
                 ├─ Paths / ModLog / AssemblyResolver  (System only)
                 └─ Harmony patch: UnityEngine.Application::.cctor
                     └─ Startup.OnUnityReady()          (Unity is up now)
                         ├─ create hidden GameObject + ModLoaderBehaviour
                         ├─ wait for Assembly-CSharp
                         └─ ModManager.LoadAll()
                             ├─ ModDiscovery  (scan Mods\*.dll)
                             ├─ ModSorter     (dependencies / priority)
                             └─ per mod: instantiate, PatchAll, OnLoad()
```

## Why Doorstop

Managed code cannot run before `Assembly-CSharp` is loaded unless something native injects
earlier. UnityDoorstop is the de-facto standard for Unity **Mono** games: a `winhttp.dll`
proxy that starts our assembly in the game's own Mono runtime. It is a separate, proven
component — we only ship its release binary.

We deliberately do **not** use BepInEx/MelonLoader so the loader stays small and the mod API
is ours.

## Bootstrap timing

The hard part is "run early, but after the engine is usable".

- At `Doorstop.Entrypoint.Start()` the runtime exists but Unity is not initialized. We only
  touch `System.*` there.
- We Harmony-patch `UnityEngine.Application`'s **static constructor** (confirmed pure managed
  in this build), plus `UnityEngine.Debug::Log/LogError` as frequently-called fallbacks. Each
  prefix calls `Startup.OnUnityReady()`.
- `OnUnityReady` is idempotent and, if the engine cannot create a GameObject yet, retries on the
  next hook invocation (throttled to 100 ms) until it succeeds.

**Never patch methods with native internal calls** (`GameObject..ctor`, `AddComponent`,
`MonoBehaviour..ctor`, most `extern` members). Detouring those replaces the body and the
internal call (e.g. `Internal_CreateGameObject`) stops resolving, raising
`MissingMethodException` and breaking GameObject creation *for the whole game*. Confirmed
experimentally.

At the hook we still avoid touching `Application` statics (the cctor has not finished);
we create a GameObject and defer mod loading until `Assembly-CSharp` is loaded, which
guarantees game types are available and no duplicate game assembly is loaded.

## Assembly resolution

`Bootstrap.Run()` installs an `AppDomain.AssemblyResolve` handler before any Harmony type is
JIT-resolved. It probes, in order: the requesting assembly's directory, the loader core
directory, the loader data root, `Mods\`, the game root, and the game `Managed` folder. This
is how `0Harmony.dll` and any mod-local dependencies are found.

## Components

| File | Responsibility |
| --- | --- |
| `Doorstop/Entrypoint.cs` | Doorstop entry — calls `Bootstrap.Run()` |
| `Bootstrap.cs` | Early setup + Unity hook installation |
| `Startup.cs` | Runs when Unity is ready; creates behaviour; loads mods |
| `Paths.cs` | Directory layout, derived from Doorstop env vars |
| `AssemblyResolver.cs` | Dependency probing |
| `Patching/HarmonyProvider.cs` | Harmony instances (root + per mod) |
| `ModDiscovery.cs` | Scans `Mods\` for `[Mod]` types |
| `ModSorter.cs` | Dependency / priority ordering |
| `ModManager.cs` | Lifecycle: instantiate, patch, OnLoad, Update, Unload |
| `ModLoaderBehaviour.cs` | Unity MonoBehaviour forwarding to `ModManager` |
| `GameEvents.cs` | Public `SceneLoaded`, `ApplicationQuitting`, `AllModsLoaded` |
| `Config/ModConfig.cs` | Per-mod INI-ish config |
| `Config/ModStateStore.cs` | Persisted per-mod enabled/disabled state (`mod_states.cfg`) |
| `Builtin/ModMenuButton.cs` | Adds the "Моды/Mods" entry to the main menu |
| `Builtin/ModsPanel.cs` | Native mod list drawn inside the menu panel (toggle on/off) |
| `Builtin/LocalizationBridge.cs` | Reads the game's current language for our labels |
| `UI/ConsoleOverlay.cs` | IMGUI console (F10) |
| `Logging/*` | `ModLog` (central) and `ModLogger` (per mod) |

## Mod lifecycle

1. `ModDiscovery` loads each `.dll` and finds concrete classes with `[Mod]` that implement
   `IMod`. Duplicate ids are rejected.
2. `ModSorter` topologically orders by `Dependencies`; missing dependency ⇒ mod skipped with a
   clear error; cycles are broken and logged.
3. For each mod: a Harmony instance is created, `Harmony.PatchAll(modAssembly)` runs (so
   attribute patches apply automatically), then `OnLoad()`.
4. `Update`/`FixedUpdate`/`LateUpdate`/`OnGUI` are forwarded every frame, each mod isolated in
   a try/catch so one failure cannot stall the others.
5. `OnApplicationQuit` raises `ApplicationQuitting` and calls `OnUnload()` in reverse order.

## Deployment layout inside the game

```
Timeflow.exe
winhttp.dll                         Doorstop
doorstop_config.ini                 target_assembly=TimeflowModLoader\Core\Timeflow.ModLoader.dll
TimeflowModLoader\Core\             Timeflow.ModLoader.dll, 0Harmony.dll (, .pdb)
TimeflowModLoader\Config\           <modId>.cfg
TimeflowModLoader\Logs\             ModLoader.log
Mods\                               *.dll (multiple per folder, or one subfolder each)
output_log.txt                      Unity player log (redirect_output_log=true)
```

## Documentation map

| Doc | Audience |
| --- | --- |
| `README.md` | Users — install, run, toggle mods. |
| `docs/MODDING_GUIDE.md` | Mod authors — short version. |
| `docs/modding-guides/` | Mod authors + AI agents — the full handbook (API, patching, events, UI, save data, recipes). |
| `docs/ARCHITECTURE.md` | Loader internals (this file). |
| `docs/TROUBLESHOOTING.md` | When the loader itself fails. |
| `AGENTS.md` | Build/RE conventions for automated contributors. |
