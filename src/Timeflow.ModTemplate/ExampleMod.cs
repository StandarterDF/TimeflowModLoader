using HarmonyLib;
using Timeflow.ModLoader.Logging;
using Timeflow.ModLoader.Modding;
using UnityEngine;

namespace Example.GiveMoney
{
    /// <summary>
    /// Example mod: press F9 to add money to the current player and log every money change
    /// coming through <c>Player.moneyOp</c>.
    ///
    /// It demonstrates the four things a mod usually needs:
    ///   1. the [Mod] attribute + ModBase base class,
    ///   2. a per-mod config value,
    ///   3. a Harmony patch on a game method (applied automatically by the loader),
    ///   4. per-frame input handling in OnUpdate.
    /// </summary>
    [Mod(
        "example.give-money",
        Name = "Give Money",
        Version = "1.0.0",
        Author = "you",
        Description = "Press F9 to add money to the current player.")]
    public sealed class GiveMoneyMod : ModBase
    {
        private const KeyCode Hotkey = KeyCode.F9;
        private long _amount;

        public override void OnLoad()
        {
            _amount = Config.GetInt("moneyAmount", 1000);
            Config.Set("moneyAmount", _amount);
            Config.Save();

            Log.Info($"Loaded. Press {Hotkey} to add {_amount}. Config: {Config.FilePath}");
        }

        public override void OnUpdate()
        {
            if (!Input.GetKeyDown(Hotkey))
                return;

            var manager = GameManager.instance;
            var player = manager != null ? manager.currentPlayerScript : null;
            if (player == null)
            {
                Log.Warn("No current player yet.");
                return;
            }

            player.moneyOp(_amount);
            Log.Info($"Gave {_amount} to player. New balance: {player.money}");
        }
    }

    /// <summary>
    /// Harmony patches declared with attributes are found and applied automatically by the
    /// loader when the mod assembly is loaded - no manual PatchAll needed.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.moneyOp))]
    internal static class MoneyOpPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, long value)
        {
            ModLog.Debug($"[money] {value:+#;-#;0} -> balance {__instance.money}");
        }
    }
}
