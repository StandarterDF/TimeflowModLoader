# AGENTS.md — Timeflow ModLoader

Instructions for automated agents and humans working in **this** repository.

## What this is

A from-scratch mod loader for the Unity game **Timeflow** (by Makhmutov, Steam release).

- Engine: **Unity 2022.3.60f1**, **Mono** scripting backend (not IL2CPP), x86_64.
- Target runtime: Unity's bundled Mono, API compatibility level **.NET Standard 2.1**.
- Game build id: `153cf38d89af45a39184d47cb15a3263`.

Two moving parts:

1. **UnityDoorstop** (third-party, LGPLv2.1) — a `winhttp.dll` proxy that runs our managed
   assembly *before* the game's code. We do not reimplement native injection.
2. **Timeflow.ModLoader** (our code) — discovers mods, exposes the modding API, boots after
   Unity is ready, and applies Harmony patches.

## Repository layout

```
Directory.Build.props            shared MSBuild props (game paths, language version)
TimeflowModLoader.sln            solution (loader + template mod)
doorstop_config.ini              Doorstop config deployed next to Timeflow.exe
src/Timeflow.ModLoader/          the loader (Doorstop entrypoint lives here)
src/Timeflow.ModTemplate/        copy this to start a new mod (Example.GiveMoney)
docs/                            architecture, modding guide, troubleshooting
tools/setup.ps1                  downloads Doorstop + Harmony (via BepInEx) into vendor/
tools/build.ps1                  setup + build + deploy (dev workflow)
tools/deploy.ps1                 copies artifacts into the game folder; -Uninstall to remove
vendor/                          downloaded deps (gitignored) — run tools/setup.ps1
reference/                       decompilation dumps (gitignored, regenerated on demand)
```

## Build & run

```powershell
# First time / after cleaning:
powershell -ExecutionPolicy Bypass -File tools\setup.ps1   # fetch Doorstop + HarmonyX
dotnet build TimeflowModLoader.sln -c Release

# Deploy into the game (defaults to ..\Timeflow; override with -GameRoot):
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 -GameRoot "D:\Steam\...\Timeflow"

# Or everything at once:
powershell -ExecutionPolicy Bypass -File tools\build.ps1 -GameRoot "D:\...\Timeflow"
```

The game is elsewhere? Set the path once for all builds:

```powershell
dotnet build -p:GameRoot="D:\Steam\steamapps\common\Timeflow"
```

There is **no venv here** — this is .NET. Only the local dotnet SDK 8/9/10 is used, plus the
local `ilspycmd` tool (see below). Never install global tools.

## How the loader boots (important)

1. Doorstop loads `TimeflowModLoader\Core\Timeflow.ModLoader.dll` and calls
   `Doorstop.Entrypoint.Start()`.
2. `Bootstrap.Run()` (System-only, no Unity types) sets up `Paths`, `ModLog`, and an
   `AppDomain.AssemblyResolve` handler, then patches a Unity callback with Harmony.
3. The hook is **`UnityEngine.Application::.cctor`** (pure managed, confirmed present in this
   build); `UnityEngine.Debug::Log/LogError` are patched as fallbacks. Each calls
   `Startup.OnUnityReady()`, which is idempotent and, if the engine is not ready yet, retries on
   the next invocation (throttled to 100 ms) until it can create a GameObject.
4. `Startup` creates a hidden `DontDestroyOnLoad` GameObject with `ModLoaderBehaviour`, then
   waits for `Assembly-CSharp` to load and calls `ModManager.LoadAll()`.

**Do not patch methods that contain native internal calls** (`GameObject..ctor`/`AddComponent`,
`MonoBehaviour..ctor`, most `extern` Unity members, ...). A Harmony detour replaces the method
body and the internal call (e.g. `Internal_CreateGameObject`) then fails to resolve with
`MissingMethodException`, which breaks GameObject creation for the entire game. This was
observed and is why only pure-managed hook targets are used.

Keep `Doorstop.Entrypoint.Start()` and `Bootstrap.Run()` free of direct Unity/Harmony type
references — the resolver must be installed before those assemblies are JIT-resolved.

## Conventions

- Namespace root: `Timeflow.ModLoader`. Public mod-facing API lives in `Modding`, `Config`,
  `Logging`. Internal plumbing is `internal`.
- The public logger is **`ModLog`** (not `Logger`) on purpose — `using UnityEngine;` brings in
  `UnityEngine.Logger`, and a clash inside mod code is painful.
- Language version C# 9, `netstandard2.1`. Do not use records (no `IsExternalInit`) or other
  modern runtime features Unity's Mono may lack.
- Reference game assemblies with `<Private>false</Private>` — never ship copies of
  `UnityEngine.*` or `Assembly-CSharp`.
- Never commit `vendor/`, `reference/`, `bin/`, `obj/`.

## Reverse engineering tooling

- **REA MCP tools** are the first choice for artifact work: `inspect_managed_artifact`,
  `inspect_managed_native_boundaries`, `inspect_native_data_type`, etc. They give hashes,
  runtime family (`unity-mono`), references and boundaries without executing anything.
- **Caveat:** `inspect_managed_members` on `Assembly-CSharp.dll` fails with
  `resource-limit` (metadata representation > 64 MiB budget). For member/type/signature dumps
  of the big game assemblies, use the local `ilspycmd`:

  ```powershell
  dotnet ilspycmd -l c  "..\Timeflow\Timeflow_Data\Managed\Assembly-CSharp.dll"   # list types
  dotnet ilspycmd -t GameManager "..\Timeflow\Timeflow_Data\Managed\Assembly-CSharp.dll"
  ```

  Dumps are written to `reference/` and are gitignored.

## Game facts worth knowing (from decompilation)

- Game script types are in the **global namespace** (no `namespace`): `GameManager`,
  `Player`, `UIManager`, `TimeView`, `SettingsManager`, `SoundManager`, `SteamManager`,
  `Localization`, `BoardsManager`, `Card`, `Achievements.AchievementsManager`,
  `SaveSystem.Data.*`, namespaced others (`SaveSystem`, `Achievements`, `GameSettings`).
- `GameManager.instance` — singleton (may be null during menus).
- `GameManager.instance.currentPlayerScript`, `.Players`, `.MyPlayer`, `.InGame`.
- `Player.money` (`long`, get-only), `Player.moneyOp(long value, bool showEffect = true)`,
  `Player.income`, `Player.salary`, `Player.Age`.
- Steamworks via `com.rlabrecque.steamworks.net` (`SteamManager`); networking via
  `Unity.Netcode.Runtime`; localisation via custom `Localization` + CSV.

## Testing the loader

Launch `Timeflow.exe` (Steam may need to be running). Evidence of success:

- `TimeflowModLoader\Logs\ModLoader.log` shows the banner and `Loaded N mod(s).`
- `output_log.txt` (Doorstop `redirect_output_log=true`) contains the same lines.
- Press **F10** in-game for the loader console overlay.

If nothing is written, see `docs/TROUBLESHOOTING.md`.
