using System;
using System.IO;
using HarmonyLib;
using Timeflow.ModLoader;
using Timeflow.ModLoader.Modding;
using UnityEngine;

namespace Timeflow.Examples
{
    /// <summary>
    /// Difficulty: ★★★ (medium).
    ///
    /// A "spy" mod that records what the game does into its own file
    /// (<c>TimeflowModLoader\Logs\example-events.log</c>): every dice move, every turn button
    /// press and every game log message. Extremely useful while reverse-engineering — run it,
    /// play for a minute, then read the file to learn *when* the game calls which method.
    ///
    /// Demonstrates: patching several methods at once (public + private), mirroring the game's
    /// own log, writing to a file, clean shutdown in OnUnload.
    /// </summary>
    [Mod(
        "example.event-log",
        Name = "Event Logger",
        Version = "1.0.0",
        Author = "Timeflow examples",
        Description = "Writes moves, turns and game log messages to a dedicated log file.")]
    public sealed class EventLoggerMod : ModBase
    {
        private static readonly object Gate = new object();
        private static StreamWriter _writer;

        public override void OnLoad()
        {
            try
            {
                Directory.CreateDirectory(Paths.LogsDir);
                _writer = new StreamWriter(Path.Combine(Paths.LogsDir, "example-events.log"), append: false)
                {
                    AutoFlush = true
                };
            }
            catch (Exception ex)
            {
                Log.Error("Could not open the event log: " + ex.Message);
            }

            Harmony.Patch(
                AccessTools.Method(typeof(Player), "MoveEnd"),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(EventLoggerMod), nameof(OnMoveEnd))));

            Harmony.Patch(
                AccessTools.Method(typeof(GameManager), nameof(GameManager.OnTurnButton)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(EventLoggerMod), nameof(OnTurnButton))));

            Harmony.Patch(
                AccessTools.Method(typeof(LogManager), nameof(LogManager.Message)),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(EventLoggerMod), nameof(OnGameLog))));

            Write("Event logger started.");
            Log.Info("Event Logger loaded; see Logs\\example-events.log.");
        }

        public override void OnUnload()
        {
            Write("Event logger stopped.");
            lock (Gate)
            {
                _writer?.Dispose();
                _writer = null;
            }
            Log.Info("Event Logger unloaded.");
        }

        // ----------------------------------------------------------------- patches

        private static void OnMoveEnd(Player __instance)
        {
            if (__instance == null)
                return;
            Write($"[move] {__instance.title} -> cell {__instance.cellIndex} (passed {__instance.cellsPassed})");
        }

        private static void OnTurnButton()
        {
            Write("[turn] Turn button pressed.");
        }

        private static void OnGameLog(string message)
        {
            if (!string.IsNullOrEmpty(message))
                Write("[game] " + message.Replace("\n", " "));
        }

        // ----------------------------------------------------------------- io

        private static void Write(string text)
        {
            lock (Gate)
            {
                try { _writer?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {text}"); }
                catch { /* ignore */ }
            }
        }
    }
}
