using HarmonyLib;
using Timeflow.ModLoader.Logging;
using Timeflow.ModLoader.Modding;
using UnityEngine;

namespace Timeflow.Trainer
{
    /// <summary>
    /// Test mod / trainer. Demonstrates the parts of the API a real mod uses:
    ///   * a per-mod config file,
    ///   * hotkeys handled in <c>OnUpdate</c>,
    ///   * an IMGUI HUD in <c>OnGUI</c>,
    ///   * a Harmony patch applied automatically while the mod is enabled and removed when it is
    ///     disabled from the in-game Mods menu.
    ///
    /// Hotkeys: F5 toggle HUD, F6 add money, F7 set income, F8 reset income.
    /// </summary>
    [Mod(
        "timeflow.trainer",
        Name = "Trainer",
        Version = "1.0.0",
        Author = "you",
        Description = "Test trainer. F5 HUD, F6 +money, F7 set income, F8 reset income.")]
    public sealed class TrainerMod : ModBase
    {
        private long _moneyAmount;
        private long _incomeAmount;
        private bool _hudVisible = true;

        public override void OnLoad()
        {
            _moneyAmount = Config.GetInt("moneyAmount", 100000);
            _incomeAmount = Config.GetInt("incomeAmount", 100000);
            Config.Set("moneyAmount", _moneyAmount);
            Config.Set("incomeAmount", _incomeAmount);
            Config.Save();

            Log.Info("Trainer loaded. F5 HUD | F6 +money | F7 set income | F8 reset income.");
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F5))
                _hudVisible = !_hudVisible;
            if (Input.GetKeyDown(KeyCode.F6))
                AddMoney();
            if (Input.GetKeyDown(KeyCode.F7))
                SetIncome(_incomeAmount);
            if (Input.GetKeyDown(KeyCode.F8))
                SetIncome(0);
        }

        public override void OnGUI()
        {
            if (!_hudVisible)
                return;

            GUILayout.BeginArea(new Rect(10, 10, 320, 130), GUI.skin.box);
            GUILayout.Label("<b>Timeflow Trainer</b>  (F5 to hide)");
            GUILayout.Label($"F6  +{_moneyAmount} money");
            GUILayout.Label($"F7  income = {_incomeAmount}");
            GUILayout.Label("F8  income = 0");

            var player = CurrentPlayer();
            GUILayout.Label(player != null
                ? $"money: {player.money}    income: {player.income}"
                : "no active player");
            GUILayout.EndArea();
        }

        public override void OnUnload()
        {
            Log.Info("Trainer disabled; its Harmony patches were removed.");
        }

        private void AddMoney()
        {
            var player = CurrentPlayer();
            if (player == null)
            {
                Log.Warn("No current player yet.");
                return;
            }

            player.moneyOp(_moneyAmount);
            Log.Info($"Added {_moneyAmount} money -> balance {player.money}.");
        }

        private void SetIncome(long value)
        {
            var player = CurrentPlayer();
            if (player == null)
            {
                Log.Warn("No current player yet.");
                return;
            }

            player.income = value;
            Log.Info($"Income set to {value}.");
        }

        private static Player CurrentPlayer()
        {
            var manager = GameManager.instance;
            return manager != null ? manager.currentPlayerScript : null;
        }
    }

    /// <summary>
    /// Logs every money change. While the trainer is disabled the loader calls
    /// <c>Harmony.UnpatchSelf()</c>, so this stops firing - proof that toggling works.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.moneyOp))]
    internal static class MoneyOpLogPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Player __instance, long value)
        {
            ModLog.Debug($"[trainer] moneyOp({value:+#;-#;0}) -> balance {__instance.money}");
        }
    }
}
