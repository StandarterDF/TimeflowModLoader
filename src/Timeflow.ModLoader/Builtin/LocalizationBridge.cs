using System.Reflection;
using HarmonyLib;

namespace Timeflow.ModLoader.Builtin
{
    /// <summary>
    /// Reads the game's current language without a compile-time dependency on Assembly-CSharp.
    /// Mirrors <c>Localization.currentLanguage</c> (= <c>SettingsManager.instance.currentLanguage</c>).
    /// </summary>
    internal static class LocalizationBridge
    {
        private static bool _resolved;
        private static PropertyInfo _instanceProperty;
        private static FieldInfo _languageField;
        private static PropertyInfo _languageProperty;

        /// <summary>"ru", "en", ... or null when the game is not ready yet.</summary>
        public static string CurrentLanguage
        {
            get
            {
                try
                {
                    Resolve();
                    if (_instanceProperty == null)
                        return null;

                    var instance = _instanceProperty.GetValue(null);
                    if (instance == null)
                        return null;

                    if (_languageProperty != null)
                        return _languageProperty.GetValue(instance) as string;
                    if (_languageField != null)
                        return _languageField.GetValue(instance) as string;
                }
                catch
                {
                    // The game assemblies may not be loaded yet.
                }

                return null;
            }
        }

        private static void Resolve()
        {
            if (_resolved)
                return;
            _resolved = true;

            var type = AccessTools.TypeByName("SettingsManager");
            if (type == null)
                return;

            _instanceProperty = type.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            _languageField = type.GetField("currentLanguage", BindingFlags.Public | BindingFlags.Instance);
            if (_languageField == null)
                _languageProperty = type.GetProperty("currentLanguage", BindingFlags.Public | BindingFlags.Instance);
        }
    }
}
