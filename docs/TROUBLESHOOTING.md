# Troubleshooting

## Nothing happens / no log file

`TimeflowModLoader\Logs\ModLoader.log` is not created at all ⇒ the loader never ran.

1. **Is Doorstop deployed?** `winhttp.dll` and `doorstop_config.ini` must sit **next to
   `Timeflow.exe`** (same folder as `UnityPlayer.dll`).
2. **Is `winhttp.dll` the Doorstop one?** Check the size (~26 KB for 4.5.0). If the game or
   Steam replaced it, redeploy.
3. **Is the game 64-bit?** Yes — use the 64-bit `winhttp.dll` (the release build we ship).
4. **Check `doorstop_config.ini`**: `enabled=true` and
   `target_assembly=TimeflowModLoader\Core\Timeflow.ModLoader.dll`.
5. **Anti-virus / Windows Defender** sometimes blocks a DLL proxy named `winhttp.dll`. Add an
   exclusion.
6. Look for `ModLoader_crash_*.log` next to `Timeflow.exe` (written if early setup throws).
7. Launch `Timeflow.exe` **directly** (from Steam is fine too). `DOORSTOP_DISABLE=1` in the
   environment disables Doorstop — make sure it is not set.

## Loader runs (log exists) but "Loaded 0 mod(s)"

- Confirm the mod DLL is under `<Timeflow>\Mods\` (subfolders are scanned too).
- The mod class must be **public-ish concrete**, implement `IMod`, and carry `[Mod("id")]`.
  The log lists every rejected type and why.
- Mod assemblies are only scanned if they load. A hard `MissingMethod`/`FileLoadException`
  means a dependency is missing — check the log line `Could not load assembly ...`.
- `ReservedAssemblies` (`Timeflow.ModLoader.dll`, `0Harmony.dll`) are never treated as mods.

## A mod loads but its patch does nothing

- Verify the target method name/signature. Decompile the game with `ilspycmd` (see
  `AGENTS.md`) — the game's types are in the **global namespace**.
- `Harmony.PatchAll` failures are logged; look for `Failed to apply Harmony patches from ...`.
- A `prefix` returning `false` skips the original method; a postfix runs after it.
- Harmony cannot patch generic/virtual edge cases the same way; prefer the concrete method.

## `Logger` is ambiguous / does not compile

You did `using UnityEngine;` and `using Timeflow.ModLoader.Logging;`. Use the loader's
`ModLog` (not `Logger`), or `ModBase.Log`. This is why the class is named `ModLog`.

## Game crashes at startup right after installing the loader

- Check `output_log.txt` and `ModLoader_crash_*.log`.
- Temporarily empty the `Mods\` folder. If the game starts, bisect the mods.
- Set `ModLog.MinimumLevel = LogLevel.Trace` in a mod to get more detail.
- Restore the game files: run `tools\deploy.ps1 -Uninstall` and delete any files named
  `winhttp.dll.modloader_backup`.

## "Metadata representation exceeds budget" (REA tooling)

This is a known limitation of the REA `inspect_managed_members` tool on the very large
`Assembly-CSharp.dll` (>64 MiB of metadata representation). Use `ilspycmd` for type/member
dumps; keep REA for artifact classification and boundary analysis.

## Enabling the managed debugger

In `doorstop_config.ini`:

```ini
[UnityMono]
debug_enabled=true
debug_address=127.0.0.1:10000
debug_suspend=false
```

Then attach dnSpy (Debug → Start Debugging → Debug engine → Unity) or your debugger to
`127.0.0.1:10000`. Set `debug_suspend=true` if you want the game to wait for the debugger.
