# 03 — Harmony patching

Timeflow is a Mono/.NET game, so **HarmonyX** (shipped as `0Harmony.dll`) can rewrite any managed
method at runtime. This guide covers the two styles this loader supports, the injectable
parameters, and the traps that are specific to Unity + Timeflow.

---

## The two styles

### A. Attribute patches (automatic)

The loader calls `Harmony.PatchAll(modAssembly)` **before** `OnLoad` for every enabled mod. So
if your assembly has exactly one mod, this just works:

```csharp
using HarmonyLib;

[HarmonyPatch(typeof(Player), nameof(Player.moneyOp))]
internal static class MoneyOpPatch
{
    [HarmonyPostfix]
    static void Postfix(Player __instance, long value)
        => ModLoader.Logging.ModLog.Debug($"[{__instance.title}] {value:+#;-#;0} -> {__instance.money}");
}
```

You do **not** call `PatchAll` yourself.

### B. Manual patches (full control)

Register in `OnLoad` with the mod's own `Harmony` instance. Required when several mods share one
assembly (otherwise enabling one would apply everyone's attribute patches):

```csharp
using System.Reflection;
using HarmonyLib;

public override void OnLoad()
{
    var target = AccessTools.Method(typeof(Player), nameof(Player.GetDice));
    var postfix = new HarmonyMethod(AccessTools.Method(typeof(MyMod), nameof(GetDicePostfix)));
    Harmony.Patch(target, postfix: postfix);
}

private static void GetDicePostfix(ref byte __result) { __result = 6; }
```

On disable, `ModManager` calls `Harmony.UnpatchSelf()`, which removes precisely the patches this
mod applied. **Verified** in `example.fast-dice` / `example.game-speed` / `example.mood-guard`.

> Rule of thumb: **one mod per assembly → attributes; many mods per assembly → manual.** The
> `Timeflow.Examples` project uses manual for exactly this reason.

---

## Finding the method (`AccessTools`)

```csharp
AccessTools.Method(typeof(Player), "MoveEnd");                       // private, no args
AccessTools.Method(typeof(Player), nameof(Player.moneyOp));          // public
AccessTools.Method(typeof(Player), "moneyOp", new[] { typeof(long), typeof(bool) }); // overload disambiguation
AccessTools.Method(typeof(SomeType), "SomeName", new[] { typeof(int) }, new[] { typeof(string) }); // generics
AccessTools.PropertyGetter(typeof(Player), "mood");                  // property getter
AccessTools.PropertySetter(typeof(Player), "mood");                  // property setter
AccessTools.Field(typeof(GameManager), "_gameState");                // private field
```

Patching a **property** means patching its getter/setter method (`get_mood` / `set_mood`). That is
the way to intercept reads/writes of `Player.mood`, `Player.money`, `Player.success`, etc.

---

## Injection parameters

Harmony injects these by **name**:

| Name | Type | Use |
|------|------|-----|
| `__instance` | the declaring type | The object the method runs on. |
| `__result` | the return type | Read/rewrite the result. Use `ref` in Prefix/Postfix to change it. |
| `__state` | any | Pass data from a Prefix to its Postfix. |
| `__0`, `__1`, ... | arg types | Raw positional arguments. |
| *(actual parameter name)* | matching type | The real argument, by name. |
| `__runOriginal` | `bool` | (Postfix/Transpiler) whether the original ran. |
| `___fieldName` | field type | Access a private field of `__instance` (`___` triple underscore). |

### Prefix

Return `bool`: `true` → run original, `false` → **skip it**.

```csharp
private static bool Prefix(int value, Player __instance)
{
    if (_active && value < 0) return false;   // veto a negative mood change
    return true;
}
```

You can also set `ref __result` in a Prefix and return `false` to short-circuit with a value.

### Postfix

Runs after the original. Rewrite the result with `ref`:

```csharp
private static void GetCellDelayPostfix(ref float __result)
{
    if (_multiplier > 1f) __result /= _multiplier;
}
```

### Transpiler / Finalizer

Transpilers rewrite the IL (advanced; use only when no Prefix/Postfix can express it). Finalizers
run even if the original throws, for cleanup:

```csharp
static Exception Finalizer(Exception __exception) { /* log/repair */ return __exception; }
```

---

## Value types vs reference types on `__result`

* Value type (`byte`, `int`, `float`, `long`, `struct`): use **`ref __result`** to change it.
* Reference type (`Card`, `List<...>`): you may assign directly, but `ref` is always safe.

## Patching static vs instance methods

* Static methods: no `__instance`; patch signature is just the parameters (+ `ref __result`).
  Example: `SettingsManager.GetCellDelay(int)`.
* Instance methods: include `Player __instance` if you need the object.

---

## Async methods (`UniTask`)

Many game methods are `async UniTask` (`Player.Move`, `BoardCell.ActivateCellEvent`,
`EventAbstract.ActivateEvent`, `GameManager.MakeTurn`-style flows). Harmony patches the **stub**
method that *starts* the state machine:

* A Prefix/Postfix runs **when the method is called**, not when it completes.
* You cannot easily observe the awaited result from a Postfix.

For "did X happen?" logging this is fine — a Postfix on `BoardCell.ActivateCellEvent` fires when
the cell is activated. For "after X finished" logic, patch the **synchronous** continuation
instead (e.g. `Player.MoveEnd` is a plain `void` and fires when the pawn stops). **Verified:**
`example.event-log` uses `Player.MoveEnd` (sync) rather than the async `Move`.

---

## Hard rules (learned the hard way)

1. **Never patch methods that contain native internal calls.**
   `GameObject..ctor`, `GameObject.AddComponent`, `MonoBehaviour..ctor`, and most `extern`
   Unity members are implemented partly in native code. Replacing the body breaks the internal
   call (e.g. `Internal_CreateGameObject`) and **GameObject creation fails game-wide** with
   `MissingMethodException`. This is why the loader only hooks pure-managed targets
   (`Application::.cctor`, `Debug::Log`).

2. **Use Postfix to change a result when the original has side-effects.**
   `Player.GetDice()` also maintains `DicesCache`. If you Prefix-and-skip it, the cache desyncs.
   A Postfix lets the game do its bookkeeping and only overrides the returned value.

3. **`Player.money` is get-only.** Don't try to patch/assign it; call `Player.moneyOp(long)` or
   patch `moneyOp` itself.

4. **Mind the enemy of performance.** Prefix/Postfix on a method called every frame (Update,
   GetCellDelay, etc.) must be cheap and must not allocate in the common case.

5. **Always keep a way to disable the patch.** Attribute patches on a single-mod assembly are
   removed by `UnpatchSelf()` automatically; manual patches likewise. Do not apply patches
   outside your mod's Harmony instance.

---

## Unpatching

`ModManager` calls `UnpatchSelf()` when a mod is turned off or the game quits, so you rarely need
to. To do it yourself:

```csharp
Harmony.UnpatchSelf();                                   // everything from this mod
Harmony.Unpatch(target, HarmonyPatchType.Postfix, Harmony.Id); // one method/type
```

---

## Verify that a patch applied

Manual patches can silently fail if the target is missing. Make it loud:

```csharp
var target = AccessTools.Method(typeof(Player), "MoveEnd");
if (target == null) { Log.Error("MoveEnd not found — game version changed?"); return; }
Harmony.Patch(target, postfix: new HarmonyMethod(...));
Log.Info($"patched {target.DeclaringType?.Name}.{target.Name}");
```

For attribute patches the loader already logs
`Applied N Harmony patch method(s) from '<id>'.` when `N > 0`. If a PatchAll throws,
the message `Failed to apply Harmony patches from '<id>': ...` appears in `ModLoader.log`.

---

## Where to see what you patched

* `<Timeflow>\TimeflowModLoader\Logs\ModLoader.log`
* In-game console: **F10**
* `Harmony.GetPatchedMethods()` during debugging.

Next: [04 — Events and cards](04-events-and-cards.md).
