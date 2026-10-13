using HarmonyLib;
using Timeflow.ModLoader.Logging;
using Timeflow.ModLoader.Modding;
using UnityEngine;

namespace Timeflow.Examples
{
    /// <summary>
    /// Difficulty: ★★★ (medium/hard).
    ///
    /// Press <b>F1</b> to stop the current player's mood from ever dropping. The game funnels all
    /// mood changes through <c>Player.ChangeMood(int)</c>; a <c>Prefix</c> returns <c>false</c>
    /// for negative deltas, which skips the original method entirely. This is how you "veto" a
    /// game action instead of merely changing its result.
    ///
    /// Demonstrates: a Prefix that cancels the original method, a persisted boolean toggle.
    /// </summary>
    [Mod(
        "example.mood-guard",
        Name = "Mood Guard",
        Version = "1.0.0",
        Author = "Timeflow examples",
        Description = "F1 prevents mood from dropping.")]
    public sealed class MoodGuardMod : ModBase
    {
        private static bool _active;

        public override void OnLoad()
        {
            _active = Config.GetBool("active", false);

            Harmony.Patch(
                AccessTools.Method(typeof(Player), nameof(Player.ChangeMood)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(MoodGuardMod), nameof(ChangeMoodPrefix))));

            Log.Info($"Mood Guard loaded (currently {(_active ? "ON" : "OFF")}). Press F1 to toggle.");
        }

        public override void OnUpdate()
        {
            if (!Input.GetKeyDown(KeyCode.F1))
                return;

            _active = !_active;
            Config.Set("active", _active);
            Config.Save();
            Log.Info($"Mood Guard {(_active ? "ON" : "OFF")}.");
        }

        /// <summary>Returning false skips (vetoes) the original <c>ChangeMood</c>.</summary>
        private static bool ChangeMoodPrefix(int value, Player __instance)
        {
            if (_active && value < 0)
            {
                ModLog.Debug($"[mood-guard] blocked mood change {value} for {__instance?.title}.");
                return false;
            }
            return true;
        }

        public override void OnUnload()
        {
            Log.Info("Mood Guard unloaded.");
        }
    }
}
