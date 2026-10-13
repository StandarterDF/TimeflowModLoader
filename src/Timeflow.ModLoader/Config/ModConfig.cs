using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Timeflow.ModLoader.Config
{
    /// <summary>
    /// Minimal, dependency-free INI-like configuration stored in
    /// <c>TimeflowModLoader\Config\&lt;modId&gt;.cfg</c>.
    /// Values are read into memory on construction; call <see cref="Save"/> to flush changes.
    /// </summary>
    public sealed class ModConfig
    {
        private readonly string _path;
        private readonly Dictionary<string, string> _values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _order = new List<string>();

        public string FilePath => _path;

        public ModConfig(string modId)
        {
            var safeName = Sanitize(modId);
            _path = Path.Combine(Paths.ConfigDir, safeName + ".cfg");
            Load();
        }

        public bool HasKey(string key) => _values.ContainsKey(key);

        public string GetString(string key, string defaultValue = "")
            => _values.TryGetValue(key, out var v) ? v : defaultValue;

        public int GetInt(string key, int defaultValue = 0)
            => _values.TryGetValue(key, out var v)
               && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
                ? r : defaultValue;

        public float GetFloat(string key, float defaultValue = 0f)
            => _values.TryGetValue(key, out var v)
               && float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var r)
                ? r : defaultValue;

        public bool GetBool(string key, bool defaultValue = false)
            => _values.TryGetValue(key, out var v)
                ? ParseBool(v, defaultValue)
                : defaultValue;

        public void Set(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
                return;

            var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            if (!_values.ContainsKey(key))
                _order.Add(key);
            _values[key] = text;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Paths.ConfigDir);
                var sb = new StringBuilder();
                sb.AppendLine("# Timeflow mod configuration");
                sb.AppendLine("# Generated automatically. Edit freely; values are re-read on the next launch.");
                sb.AppendLine();

                var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var key in _order)
                {
                    if (written.Add(key))
                        sb.Append(key).Append('=').AppendLine(_values[key]);
                }
                foreach (var pair in _values)
                {
                    if (written.Add(pair.Key))
                        sb.Append(pair.Key).Append('=').AppendLine(pair.Value);
                }

                File.WriteAllText(_path, sb.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Logging.ModLog.Error($"Failed to save config '{_path}': {ex.Message}");
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path))
                    return;

                foreach (var raw in File.ReadAllLines(_path))
                {
                    var line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                        continue;

                    var idx = line.IndexOf('=');
                    if (idx <= 0)
                        continue;

                    var key = line.Substring(0, idx).Trim();
                    var value = line.Substring(idx + 1).Trim();
                    if (!_values.ContainsKey(key))
                        _order.Add(key);
                    _values[key] = value;
                }
            }
            catch (Exception ex)
            {
                Logging.ModLog.Error($"Failed to read config '{_path}': {ex.Message}");
            }
        }

        private static bool ParseBool(string value, bool fallback)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "true":
                case "1":
                case "yes":
                case "on":
                    return true;
                case "false":
                case "0":
                case "no":
                case "off":
                    return false;
                default:
                    return fallback;
            }
        }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "mod";

            var sb = new StringBuilder(name.Length);
            foreach (var c in name)
                sb.Append(char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-' ? c : '_');
            return sb.ToString();
        }
    }
}
