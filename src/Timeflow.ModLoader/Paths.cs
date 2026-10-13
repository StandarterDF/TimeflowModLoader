using System;
using System.IO;
using System.Reflection;

namespace Timeflow.ModLoader
{
    /// <summary>
    /// Resolves every important directory used by the loader. Values are derived from the
    /// environment variables that UnityDoorstop sets, with sensible fallbacks.
    ///
    /// Layout (relative to the folder that contains Timeflow.exe):
    ///   winhttp.dll                      Doorstop injector
    ///   doorstop_config.ini              Doorstop configuration
    ///   TimeflowModLoader\Core\          loader + 0Harmony.dll
    ///   TimeflowModLoader\Config\        per-mod configuration files
    ///   TimeflowModLoader\Logs\          log files
    ///   Mods\                            drop mod .dll files (or per-mod subfolders) here
    /// </summary>
    public static class Paths
    {
        public static string GameRoot { get; private set; }
        public static string ManagedDir { get; private set; }
        public static string LoaderDir { get; private set; }
        public static string DataRoot { get; private set; }
        public static string CoreDir { get; private set; }
        public static string ModsDir { get; private set; }
        public static string ConfigDir { get; private set; }
        public static string LogsDir { get; private set; }

        internal static void Initialize()
        {
            var processPath = Environment.GetEnvironmentVariable("DOORSTOP_PROCESS_PATH");
            GameRoot = !string.IsNullOrEmpty(processPath)
                ? Path.GetDirectoryName(Path.GetFullPath(processPath))
                : AppContext.BaseDirectory;

            var managed = Environment.GetEnvironmentVariable("DOORSTOP_MANAGED_FOLDER_DIR");
            ManagedDir = !string.IsNullOrEmpty(managed)
                ? Path.GetFullPath(managed)
                : Path.Combine(GameRoot, "Timeflow_Data", "Managed");

            var invokePath = Environment.GetEnvironmentVariable("DOORSTOP_INVOKE_DLL_PATH");
            if (!string.IsNullOrEmpty(invokePath))
                LoaderDir = Path.GetDirectoryName(Path.GetFullPath(invokePath));
            if (string.IsNullOrEmpty(LoaderDir))
            {
                try { LoaderDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); }
                catch { /* single-file / dynamic assembly */ }
            }
            if (string.IsNullOrEmpty(LoaderDir))
                LoaderDir = Path.Combine(GameRoot, "TimeflowModLoader", "Core");

            DataRoot = Path.Combine(GameRoot, "TimeflowModLoader");
            CoreDir = Path.Combine(DataRoot, "Core");
            ModsDir = Path.Combine(GameRoot, "Mods");
            ConfigDir = Path.Combine(DataRoot, "Config");
            LogsDir = Path.Combine(DataRoot, "Logs");
        }

        internal static void EnsureDirectories()
        {
            foreach (var dir in new[] { DataRoot, CoreDir, ModsDir, ConfigDir, LogsDir })
            {
                try { Directory.CreateDirectory(dir); }
                catch { /* best effort */ }
            }
        }
    }
}
