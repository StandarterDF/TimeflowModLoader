using HarmonyLib;

namespace Timeflow.ModLoader.Patching
{
    /// <summary>Creates and hands out Harmony instances.</summary>
    internal static class HarmonyProvider
    {
        private static Harmony _root;

        /// <summary>Loader-wide Harmony instance, used for the Unity bootstrap hook.</summary>
        public static Harmony Root => _root ?? (_root = new Harmony("timeflow.modloader"));

        /// <summary>Creates an isolated Harmony instance for a mod.</summary>
        public static Harmony Create(string modId) => new Harmony("timeflow.modloader." + modId);
    }
}
