# Timeflow modding guides

A complete, practical handbook for modding **Timeflow** with this loader. Written for two
audiences at once:

* **Humans** — modders who want to add features, tweak the economy, hook events, or build UI.
* **AI agents / LLMs** — the guides are deliberately explicit, structured and machine-friendly
  so an assistant can read one file and immediately know what to do. See
  [`08-ai-agents.md`](08-ai-agents.md) for the condensed, instruction-like version.

Everything here was verified against the shipping build:
`Unity 2022.3.60f1`, Mono, `.NET Standard 2.1`, build id `153cf38d89af45a39184d47cb15a3263`.
Game types live in the **global namespace** (`Player`, `GameManager`, `SettingsManager`, ...).

---

## Read in this order

| # | Guide | What you learn | Difficulty |
|---|-------|----------------|------------|
| 0 | [00-quickstart.md](00-quickstart.md) | Build, deploy and toggle your first mod in ~10 minutes | ★ |
| 1 | [01-mod-anatomy.md](01-mod-anatomy.md) | `[Mod]`, `IMod`/`ModBase`, lifecycle, ordering, logging, config | ★ |
| 2 | [02-game-internals.md](02-game-internals.md) | The game's own API: singletons, `Player`, `GameManager`, scenes, board cells, cards, saves | ★★ |
| 3 | [03-harmony-patching.md](03-harmony-patching.md) | Prefix/Postfix/Transpiler, `__result`, `__instance`, manual vs attribute patches, pitfalls | ★★ |
| 4 | [04-events-and-cards.md](04-events-and-cards.md) | How board events and cards work; observe & modify them; add your own | ★★★ |
| 5 | [05-ui-and-hud.md](05-ui-and-hud.md) | IMGUI HUDs, opening the game's own windows, and cloning native uGUI (the Mods menu trick) | ★★★ |
| 6 | [06-config-and-save-data.md](06-config-and-save-data.md) | Per-mod config, per-mod on/off state, persisting data in the game's save | ★★ |
| 7 | [07-recipes.md](07-recipes.md) | Copy-paste cookbook: money, speed, mood, dice, income, custom cards, cheats, achievements | ★★ |
| 8 | [08-ai-agents.md](08-ai-agents.md) | Packaged for automation: repo map, exact commands, tool usage, hard rules | — |
| 9 | [09-cards-and-business-internals.md](09-cards-and-business-internals.md) | Deep card/business/economy model: JSON schema, effect language, money signs, deck injection | ★★★ |
| 10 | [10-api-reference.md](10-api-reference.md) | The full API index + subsystem map + "find anything" workflow | ★★ |

Also see the top-level docs: [`../MODDING_GUIDE.md`](../MODDING_GUIDE.md) (short version),
[`../ARCHITECTURE.md`](../ARCHITECTURE.md) (how the loader itself works),
[`../TROUBLESHOOTING.md`](../TROUBLESHOOTING.md).

---

## The one-paragraph summary

A mod is a `netstandard2.1` class library dropped into `<Timeflow>\Mods\`. Mark a class with
`[Mod("author.name", ...)]` and derive from `ModBase`. Override `OnLoad`/`OnUpdate`/`OnUnload`.
Patch the game with Harmony — either attribute patches (applied automatically per assembly) or
manual `Harmony.Patch(...)` calls from `OnLoad`. Mods are **disabled by default**; the player
enables them in the in-game **Моды / Mods** menu, and the choice is persisted. Logs go to
`TimeflowModLoader\Logs\ModLoader.log`; the in-game console is **F10**.

---

## Example mods you can read and run

`src/Timeflow.Examples/` is one assembly containing several mods of increasing difficulty. They
are intentionally written *without* `[HarmonyPatch]` attributes and instead register patches
manually in `OnLoad`, so each of them can be toggled independently (see
[03-harmony-patching.md](03-harmony-patching.md#manual-vs-attribute-patching)).

| Mod id | File | Difficulty | Demonstrates |
|--------|------|-----------|--------------|
| `example.hello` | `HelloMod.cs` | ★ | `[Mod]`, `ModBase`, `GameEvents.SceneLoaded`, logging |
| `example.give-money` | `../ModTemplate/ExampleMod.cs` | ★ | attribute patch, config, hotkey |
| `example.fast-dice` | `FastDiceMod.cs` | ★★ | manual Postfix, `ref __result`, hotkey toggle |
| `example.game-speed` | `GameSpeedMod.cs` | ★★ | patching a static method, cycling states |
| `example.hud` | `HudMod.cs` | ★★ | IMGUI overlay, reading game fields, reflection into privates |
| `example.event-log` | `EventLoggerMod.cs` | ★★★ | patching public + private methods, mirroring the game log to a file |
| `example.mood-guard` | `MoodGuardMod.cs` | ★★★ | Prefix that *cancels* the original method (a "veto") |
| `example.custom-business` | `CustomBusinessMod.cs` | ★★★ | adding a new **business** to the master deck, `Player.TakeCard`, money/income signs |
| `example.custom-event` | `CustomEventMod.cs` | ★★★ | adding a new **happening** with an `ActionCard` reward, Prefix on an async event |
| `timeflow.trainer` | `../Trainer/TrainerMod.cs` | ★★★ | combined trainer: HUD, hotkeys, money patch |

All of them are tested in the real game — see the "verified" notes in each guide.

---

## Conventions used in these guides

* Paths are relative to the repository root (`Timeflow_MODLOADER\`) unless they start with
  `<Timeflow>`, which is the game install folder.
* Code blocks marked **`C#`** are full, compiling snippets unless noted "illustration".
* `★` = relative difficulty.
* "Verified" means it was actually run inside the shipping game, not just compiled.
