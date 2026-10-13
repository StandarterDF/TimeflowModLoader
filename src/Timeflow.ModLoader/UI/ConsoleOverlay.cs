using System.Collections.Generic;
using Timeflow.ModLoader.Logging;
using UnityEngine;
using ModLog = Timeflow.ModLoader.Logging.ModLog;

namespace Timeflow.ModLoader.UI
{
    /// <summary>
    /// Lightweight in-game IMGUI console. Toggle with F10. Shows the tail of the loader log
    /// and the list of loaded mods - handy when there is no console window attached.
    /// </summary>
    internal sealed class ConsoleOverlay
    {
        public static ConsoleOverlay Instance { get; } = new ConsoleOverlay();

        public bool Visible { get; set; }

        private Rect _window = new Rect(20, 20, 720, 420);
        private Vector2 _scroll;
        private const int MaxLines = 200;

        public void Update()
        {
            // Input is polled in OnGUI via Event.current to keep everything on the main thread.
        }

        public void OnGUI()
        {
            var e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.F10)
            {
                Visible = !Visible;
                e.Use();
            }

            if (!Visible)
                return;

            _window = GUILayout.Window(0x7F10, _window, DrawWindow, "Timeflow ModLoader - console (F10)");
        }

        private void DrawWindow(int id)
        {
            GUILayout.BeginVertical();

            var recent = ModLog.Recent;
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            var start = recent.Count > MaxLines ? recent.Count - MaxLines : 0;
            for (var i = start; i < recent.Count; i++)
                GUILayout.Label(recent[i]);

            GUILayout.EndScrollView();

            GUILayout.Space(4);
            GUILayout.Label("Mods folder: " + Paths.ModsDir);
            GUILayout.Label("Log file:    " + (ModLog.LogFilePath ?? "<memory only>"));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear"))
                ModLog.Log(LogLevel.Info, "Console cleared.");
            if (GUILayout.Button("Open mods folder"))
                Application.OpenURL("file://" + Paths.ModsDir.Replace('\\', '/'));
            if (GUILayout.Button("Close"))
                Visible = false;
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }
    }
}
