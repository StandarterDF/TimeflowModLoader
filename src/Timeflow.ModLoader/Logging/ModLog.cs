using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Timeflow.ModLoader.Logging
{
    /// <summary>
    /// Central log sink for the loader and for mods. Writes to a rolling in-memory buffer
    /// (used by the in-game console overlay), to a log file, and - once Unity is up - to the
    /// Unity console.
    ///
    /// Named <c>ModLog</c> (not <c>ModLog</c>) on purpose: code that does
    /// <c>using UnityEngine;</c> already has a <c>UnityEngine.ModLog</c>, so this avoids a
    /// name collision for mod authors.
    /// </summary>
    public static class ModLog
    {
        private const int RecentCapacity = 1000;
        private static readonly object Gate = new object();
        private static readonly List<string> RecentBuffer = new List<string>(RecentCapacity);

        private static StreamWriter _writer;
        private static string _logFilePath;

        /// <summary>Messages at or above this level are recorded. Defaults to Debug.</summary>
        public static LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

        /// <summary>Set to true by the bootstrap once the Unity runtime can accept Debug log calls.</summary>
        internal static bool UnityAvailable { get; set; }

        /// <summary>Path of the current log file, or null before initialization.</summary>
        public static string LogFilePath => _logFilePath;

        /// <summary>Snapshot (oldest first) of the most recent log lines, for the console overlay.</summary>
        public static IReadOnlyList<string> Recent
        {
            get { lock (Gate) { return RecentBuffer.ToArray(); } }
        }

        internal static void Initialize(string logDirectory)
        {
            lock (Gate)
            {
                try
                {
                    if (!string.IsNullOrEmpty(logDirectory))
                    {
                        Directory.CreateDirectory(logDirectory);
                        _logFilePath = Path.Combine(logDirectory, "ModLoader.log");
                        var stream = new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                    }
                }
                catch (Exception ex)
                {
                    // Logging must never take the game down; fall back to memory only.
                    _writer = null;
                    WriteLine(LogLevel.Error, "Loader", "Could not open log file: " + ex.Message);
                }
            }
        }

        public static void Trace(object message) => Log(LogLevel.Trace, message);
        public static void Debug(object message) => Log(LogLevel.Debug, message);
        public static void Info(object message) => Log(LogLevel.Info, message);
        public static void Warn(object message) => Log(LogLevel.Warning, message);
        public static void Error(object message) => Log(LogLevel.Error, message);
        public static void Fatal(object message) => Log(LogLevel.Fatal, message);

        public static void Log(LogLevel level, object message)
            => WriteLine(level, "Loader", message?.ToString() ?? "null");

        internal static void WriteLine(LogLevel level, string source, string message)
        {
            if (level < MinimumLevel && level != LogLevel.Fatal)
                return;

            var line = string.Format(
                "[{0:HH:mm:ss.fff}] [{1}] [{2}] {3}",
                DateTime.Now, level.Tag(), source, message);

            lock (Gate)
            {
                RecentBuffer.Add(line);
                if (RecentBuffer.Count > RecentCapacity)
                    RecentBuffer.RemoveRange(0, RecentBuffer.Count - RecentCapacity);

                if (_writer != null)
                {
                    try { _writer.WriteLine(line); }
                    catch { /* ignore disk errors */ }
                }
            }

            WriteToUnity(level, line);
        }

        private static void WriteToUnity(LogLevel level, string line)
        {
            if (!UnityAvailable)
                return;

            try
            {
                switch (level)
                {
                    case LogLevel.Warning:
                        UnityEngine.Debug.LogWarning(line);
                        break;
                    case LogLevel.Error:
                    case LogLevel.Fatal:
                        UnityEngine.Debug.LogError(line);
                        break;
                    default:
                        UnityEngine.Debug.Log(line);
                        break;
                }
            }
            catch
            {
                // Unity is not ready yet, or is shutting down.
            }
        }
    }
}
