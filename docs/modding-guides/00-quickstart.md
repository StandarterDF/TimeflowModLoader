# 00 — Quickstart: your first mod in ~10 minutes

This walks you from an empty checkout to a working mod running in the real game.

> **Prerequisites:** .NET SDK (8/9/10) on `PATH`, and Timeflow installed. The loader itself must
> already be deployed to the game (see the repository `README`/`AGENTS.md`; normally you run
> `tools\build.ps1` once).

---

## 1. Get the loader building

```powershell
# from the repository root (Timeflow_MODLOADER\)
powershell -ExecutionPolicy Bypass -File tools\setup.ps1     # downloads Doorstop + Harmony (once)
dotnet build TimeflowModLoader.sln -c Release
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1    # copies the loader into ..\Timeflow
```

`deploy.ps1` defaults to `..\Timeflow`. Point elsewhere with `-GameRoot "D:\...\Timeflow"`.

## 2. Copy the template

The fastest start is the template mod:

```
src\Timeflow.ModTemplate\        →  copy to  src\Timeflow.MyMod\
```

Rename the folder and edit `Timeflow.ModTemplate.csproj`:

```xml
<AssemblyName>Timeflow.MyMod</AssemblyName>
<RootNamespace>Timeflow.MyMod</RootNamespace>
```

The important line that auto-deploys your DLL after every build is:

```xml
<PropertyGroup>
  <ModDeployDir>$(GameRoot)\Mods</ModDeployDir>
</PropertyGroup>
...
<Target Name="DeployMod" AfterTargets="Build">
  <MakeDir Directories="$(ModDeployDir)" />
  <Copy SourceFiles="$(TargetPath)" DestinationFolder="$(ModDeployDir)" />
</Target>
```

Add the project to the solution so `dotnet build` picks it up:

```powershell
dotnet sln TimeflowModLoader.sln add src\Timeflow.MyMod\Timeflow.MyMod.csproj
```

## 3. Write the mod

Replace the template's class with this minimal mod:

```csharp
using Timeflow.ModLoader;
using Timeflow.ModLoader.Modding;
using UnityEngine;

[Mod("me.hello", Name = "Hello Mod", Version = "1.0.0", Author = "me",
     Description = "Logs a line and watches scene loads.")]
public sealed class HelloMod : ModBase
{
    public override void OnLoad()
    {
        Log.Info("Hello from my first mod!");
        GameEvents.SceneLoaded += OnScene;
    }

    public override void OnUpdate()
    {
        // Runs every frame while the mod is enabled.
        if (Input.GetKeyDown(KeyCode.F9))
            Log.Info("F9 was pressed.");
    }

    public override void OnUnload()
    {
        GameEvents.SceneLoaded -= OnScene;   // always undo subscriptions
        Log.Info("Goodbye.");
    }

    private void OnScene(UnityEngine.SceneManagement.Scene s,
                         UnityEngine.SceneManagement.LoadSceneMode m)
        => Log.Info($"Scene: {s.name}");
}
```

## 4. Build & deploy

```powershell
dotnet build TimeflowModLoader.sln -c Release
```

The `DeployMod` target copies `Timeflow.MyMod.dll` into `<Timeflow>\Mods\`. That's it — no manual
file copying.

## 5. Enable it in the game

**Mods are disabled by default.** Launch the game and on the main menu click **Моды / Mods**
(it sits under **Настройки / Settings**). The menu panel becomes the mod list. Click your mod's
row to turn it on; the title shows the new state immediately ("— вкл." / "— выкл."). Click
**Назад / Back**.

Verify:

* `<Timeflow>\TimeflowModLoader\Logs\ModLoader.log` contains
  `Mod enabled: Hello Mod (me.hello) v1.0.0`.
* In-game, press **F10** to read the loader console.

## 6. Iterate

Because the mod is toggled at runtime, you can:

1. Toggle it **off** in the Mods menu (this calls `OnUnload` and removes its Harmony patches).
2. Rebuild + redeploy.
3. Toggle it **on** again — it re-reads the fresh DLL.

Note that a *loaded* assembly cannot be overwritten on Windows. If you rebuild while the mod is
enabled, exit the game first (or at least disable it and restart).

---

## Cheat-sheet

| I want to... | Where |
|--------------|-------|
| Build everything | `dotnet build TimeflowModLoader.sln -c Release` |
| Deploy the loader | `powershell -File tools\deploy.ps1` |
| Build + deploy in one go | `powershell -File tools\build.ps1` |
| Drop a mod DLL | `<Timeflow>\Mods\` (automatic via the `DeployMod` target) |
| Read logs | `<Timeflow>\TimeflowModLoader\Logs\ModLoader.log` |
| Open in-game console | **F10** |
| Enable/disable a mod | main menu → **Моды** → click the row |
| Change hotkeys | edit each mod's `.cs` |
| Per-mod settings | `<Timeflow>\TimeflowModLoader\Config\<mod id>.cfg` |

Next: [01 — Mod anatomy](01-mod-anatomy.md).
