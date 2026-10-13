using System;
using System.Collections.Generic;
using System.IO;

namespace Timeflow.ModLoader.Config
{
    /// <summary>
    /// Remembers which mods the user enabled or disabled, so the choice survives restarts.
    /// Stored as a simple "modId=enabled|disabled" list in
    /// <c>TimeflowModLoader\Config\mod_states.cfg</c>.
    /// </summary>
    internal static class ModStateStore
    {
        private static readonly Dictionary<string, bool> States =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded;

        private static string FilePath => Path.Combine(Paths.ConfigDir, "mod_states.cfg");

        public static bool IsEnabled(string modId, bool defaultValue = false)
        {
            EnsureLoaded();
            return States.TryGetValue(modId, out var enabled) ? enabled : defaultValue;
        }

        public static void Set(string modId, bool enabled)
        {
            EnsureLoaded();
            States[modId] = enabled;
            Save();
        }

        private static void EnsureLoaded()
        {
            if (_loaded)
                return;
            _loaded = true;

            try
            {
                if (!File.Exists(FilePath))
                    return;

                foreach (var raw in File.ReadAllLines(FilePath))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                        continue;

                    var idx = line.IndexOf('=');
                    if (idx <= 0)
                        continue;

                    var id = line.Substring(0, idx).Trim();
                    var value = line.Substring(idx + 1).Trim();
                    States[id] = value.Equals("disabled", StringComparison.OrdinalIgnoreCase)
                        ? false
                        : !value.Equals("false", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Logging.ModLog.Error($"Failed to read mod states: {ex.Message}");
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Paths.ConfigDir);
                using (var writer = new StreamWriter(FilePath, false))
                {
                    writer.WriteLine("# Timeflow ModLoader - per-mod enabled state");
                    writer.WriteLine("# Format: <mod id>=enabled|disabled");
                    foreach (var pair in States)
                        writer.WriteLine($"{pair.Key}={(pair.Value ? "enabled" : "disabled")}");
                }
            }
            catch (Exception ex)
            {
                Logging.ModLog.Error($"Failed to save mod states: {ex.Message}");
            }
        }
    }
}
