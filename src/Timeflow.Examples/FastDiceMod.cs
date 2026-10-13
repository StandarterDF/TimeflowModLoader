using System.Reflection;
using HarmonyLib;
using Timeflow.ModLoader.Modding;
using UnityEngine;

namespace Timeflow.Examples
{
    /// <summary>
    /// Difficulty: ★★☆ (easy/medium).
    ///
    /// Press <b>F2</b> to make the current player always roll a fixed dice value. This shows how
    /// to patch a game method <i>manually</i> (instead of the [HarmonyPatch] attribute) so that
    /// several mods can live in the same assembly and still be toggled independently.
    ///
    /// The patch runs as a <c>Postfix</c>, i.e. the game's own logic executes first (the dice
    /// cache stays consistent) and only the returned value is overridden.
    ///
    /// Demonstrates: manual Harmony patching, a Postfix that rewrites the result with
    /// <c>ref __result</c>, config values, hotkey toggling.
    /// </summary>
    [Mod(
        "example.fast-dice",
        Name = "Lucky Dice",
        Version = "1.0.0",
        Author = "Timeflow examples",
        Description = "F2 toggles a fixed dice roll value.")]
    public sealed class FastDiceMod : ModBase
    {
        private static FastDiceMod _current;
        private static bool _active;
        private static int _value = 4;

        public override void OnLoad()
        {
            _current = this;
            _value = Mathf.Clamp(Config.GetInt("diceValue", 4), 1, 6);
            Config.Set("diceValue", _value);
            Config.Save();

            // Manual patch: Player.GetDice() returns the dice roll (byte).
            var target = AccessTools.Method(typeof(Player), nameof(Player.GetDice));
            var postfix = new HarmonyMethod(AccessTools.Method(typeof(FastDiceMod), nameof(GetDicePostfix)));
            Harmony.Patch(target, postfix: postfix);

            Log.Info($"Lucky Dice loaded. Press F2 to toggle (value = {_value}).");
        }

        public override void OnUpdate()
        {
            if (!Input.GetKeyDown(KeyCode.F2))
                return;

            _active = !_active;
            Log.Info($"Lucky Dice {(_active ? "ON" : "OFF")} (value = {_value}).");
        }

        public override void OnUnload()
        {
            // The loader calls Harmony.UnpatchSelf() for the disabled mod, which removes the
            // patch above automatically. We only reset our own state.
            _active = false;
            _current = null;
            Log.Info("Lucky Dice unloaded.");
        }

        private static void GetDicePostfix(ref byte __result)
        {
            if (_active)
                __result = (byte)_value;
        }
    }
}
