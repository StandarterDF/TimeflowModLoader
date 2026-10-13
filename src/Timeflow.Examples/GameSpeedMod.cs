using System.Reflection;
using HarmonyLib;
using Timeflow.ModLoader.Modding;
using UnityEngine;

namespace Timeflow.Examples
{
    /// <summary>
    /// Difficulty: ★★☆ (easy/medium).
    ///
    /// Press <b>F3</b> to cycle the board-walk speed (x1 → x2 → x5 → x10). The game computes the
    /// delay between two board cells in the static method <c>SettingsManager.GetCellDelay</c>;
    /// a Postfix divides the returned value, so the pawn moves faster.
    ///
    /// Demonstrates: patching a static method, dividing a result, cycling through states.
    /// </summary>
    [Mod(
        "example.game-speed",
        Name = "Game Speed",
        Version = "1.0.0",
        Author = "Timeflow examples",
        Description = "F3 cycles board movement speed x1/x2/x5/x10.")]
    public sealed class GameSpeedMod : ModBase
    {
        private static readonly float[] Steps = { 1f, 2f, 5f, 10f };
        private static int _stepIndex;
        private static float _multiplier = 1f;

        public override void OnLoad()
        {
            _stepIndex = Mathf.Clamp(Config.GetInt("speedStep", 0), 0, Steps.Length - 1);
            _multiplier = Steps[_stepIndex];

            var target = AccessTools.Method(typeof(SettingsManager), "GetCellDelay");
            var postfix = new HarmonyMethod(AccessTools.Method(typeof(GameSpeedMod), nameof(GetCellDelayPostfix)));
            Harmony.Patch(target, postfix: postfix);

            Log.Info($"Game Speed loaded. Current x{_multiplier:0}. Press F3 to cycle.");
        }

        public override void OnUpdate()
        {
            if (!Input.GetKeyDown(KeyCode.F3))
                return;

            _stepIndex = (_stepIndex + 1) % Steps.Length;
            _multiplier = Steps[_stepIndex];
            Config.Set("speedStep", _stepIndex);
            Config.Save();
            Log.Info($"Game Speed set to x{_multiplier:0}.");
        }

        private static void GetCellDelayPostfix(ref float __result)
        {
            // __result is the delay in milliseconds; smaller == faster movement.
            if (_multiplier > 1f && __result > 0f)
                __result /= _multiplier;
        }
    }
}
