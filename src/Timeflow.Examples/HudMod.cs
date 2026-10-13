using System.Reflection;
using HarmonyLib;
using Timeflow.ModLoader.Modding;
using UnityEngine;

namespace Timeflow.Examples
{
    /// <summary>
    /// Difficulty: ★★☆ (easy/medium).
    ///
    /// Press <b>F4</b> to toggle an on-screen HUD that shows the current player's money, income,
    /// cash-flow, time, mood, age and success. It also reads a *private* game field
    /// (<c>GameManager._gameState</c>) through reflection, which is the usual way to inspect
    /// internals without recompiling the game.
    ///
    /// Demonstrates: OnGUI (IMGUI), reading public game fields/properties, reflection into
    /// private members, a per-frame HUD.
    /// </summary>
    [Mod(
        "example.hud",
        Name = "Stats HUD",
        Version = "1.0.0",
        Author = "Timeflow examples",
        Description = "F4 toggles an on-screen stats overlay.")]
    public sealed class HudMod : ModBase
    {
        private static FieldInfo _gameStateField;
        private bool _visible = true;

        public override void OnLoad()
        {
            _visible = Config.GetBool("visible", true);
            _gameStateField = AccessTools.Field(typeof(GameManager), "_gameState");
            Log.Info("Stats HUD loaded. Press F4 to toggle.");
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F4))
            {
                _visible = !_visible;
                Config.Set("visible", _visible);
                Config.Save();
            }
        }

        public override void OnGUI()
        {
            if (!_visible)
                return;

            var manager = GameManager.instance;
            var player = manager != null ? manager.currentPlayerScript : null;

            GUILayout.BeginArea(new Rect(10, 10, 340, 190), GUI.skin.box);
            GUILayout.Label("<b>Timeflow — Stats HUD</b>  (F4 to hide)");

            if (player == null)
            {
                GUILayout.Label("No active player (not in a game yet).");
            }
            else
            {
                GUILayout.Label($"Money    : {player.money:N0}");
                GUILayout.Label($"Income   : {player.income:N0}");
                GUILayout.Label($"Cash-flow: {player.cashflow:N0}");
                GUILayout.Label($"Time     : {player.time}");
                GUILayout.Label($"Mood     : {player.mood}");
                GUILayout.Label($"Age      : {player.Age}");
                GUILayout.Label($"Success  : {player.success}%");
            }

            if (manager != null && _gameStateField != null)
            {
                var state = _gameStateField.GetValue(manager);
                GUILayout.Label($"Game state (private) : {state}");
            }

            GUILayout.EndArea();
        }

        public override void OnUnload()
        {
            Log.Info("Stats HUD unloaded.");
        }
    }
}
