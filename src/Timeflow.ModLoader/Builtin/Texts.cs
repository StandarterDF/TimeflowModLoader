namespace Timeflow.ModLoader.Builtin
{
    /// <summary>Tiny bilingual string table (the game's own localisation is only ru/en).</summary>
    internal static class Texts
    {
        private static bool IsRussian
        {
            get
            {
                try { return LocalizationBridge.CurrentLanguage == "ru"; }
                catch { return false; }
            }
        }

        public static string Mods => IsRussian ? "Моды" : "Mods";
        public static string Back => IsRussian ? "Назад" : "Back";
        public static string On => IsRussian ? "  —  вкл." : "  —  on";
        public static string Off => IsRussian ? "  —  выкл." : "  —  off";
        public static string ErrorSuffix => IsRussian ? "  [ошибка]" : "  [error]";
        public static string NoMods => IsRussian
            ? "Моды не найдены"
            : "No mods found";
    }
}
