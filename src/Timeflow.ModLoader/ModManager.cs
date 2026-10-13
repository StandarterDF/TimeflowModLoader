using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Timeflow.ModLoader.Config;
using Timeflow.ModLoader.Logging;
using Timeflow.ModLoader.Modding;
using Timeflow.ModLoader.Patching;
using UnityEngine.SceneManagement;

namespace Timeflow.ModLoader
{
    /// <summary>Owns discovery and the runtime lifecycle of every mod.</summary>
    internal static class ModManager
    {
        /// <summary>Runtime state for one discovered mod.</summary>
        internal sealed class ModEntry
        {
            public ModInfo Info;
            public Harmony Harmony;
            public IMod Instance;
            public bool Loaded;
            public string Error;

            public override string ToString() => Info.ToString();
        }

        private static readonly List<ModEntry> Entries = new List<ModEntry>();
        private static bool _discovered;

        /// <summary>All discovered mods (enabled and disabled), in load order.</summary>
        public static IReadOnlyList<ModEntry> All => Entries;

        public static int LoadedCount => Entries.Count(e => e.Loaded);

        // ---------------------------------------------------------------- discovery

        public static void LoadAll()
        {
            if (!_discovered)
            {
                _discovered = true;
                Discover();
                foreach (var entry in Entries)
                {
                    if (ModStateStore.IsEnabled(entry.Info.Id))
                        Enable(entry);
                    else
                        ModLog.Info($"Mod disabled in config: {entry.Info.Id}");
                }
                ModLog.Info($"Loaded {LoadedCount}/{Entries.Count} mod(s).");
                GameEvents.RaiseAllModsLoaded();
            }
        }

        private static void Discover()
        {
            Paths.EnsureDirectories();
            ModLog.Info($"Scanning for mods in: {Paths.ModsDir}");

            var discovered = ModDiscovery.Discover(Paths.ModsDir);
            var ordered = ModSorter.Sort(discovered);
            foreach (var info in ordered)
                Entries.Add(new ModEntry { Info = info });

            ModLog.Info($"Discovered {Entries.Count} mod(s).");
        }

        // ---------------------------------------------------------------- runtime toggle

        /// <summary>Enables or disables a mod at runtime (and persists the choice).</summary>
        public static void SetEnabled(string modId, bool enabled)
        {
            var entry = Find(modId);
            if (entry == null)
                return;

            if (enabled)
                Enable(entry);
            else
                Disable(entry);

            ModStateStore.Set(modId, enabled);
        }

        public static bool Toggle(string modId)
        {
            var entry = Find(modId);
            if (entry == null)
                return false;

            SetEnabled(modId, !entry.Loaded);
            return entry.Loaded;
        }

        private static ModEntry Find(string modId)
            => Entries.FirstOrDefault(e => string.Equals(e.Info.Id, modId, StringComparison.OrdinalIgnoreCase));

        private static void Enable(ModEntry entry)
        {
            if (entry.Loaded)
                return;

            try
            {
                var harmony = HarmonyProvider.Create(entry.Info.Id);
                var instance = (IMod)Activator.CreateInstance(entry.Info.ModType);
                if (instance is ModBase modBase)
                    modBase.Bind(entry.Info, harmony);

                ApplyHarmonyPatches(entry.Info, harmony);

                entry.Harmony = harmony;
                entry.Instance = instance;
                entry.Error = null;
                entry.Loaded = true;

                instance.OnLoad();
                ModLog.Info($"Mod enabled: {entry.Info}");
            }
            catch (Exception ex)
            {
                entry.Loaded = false;
                entry.Error = ex.Message;
                ModLog.Error($"Failed to enable mod '{entry.Info.Id}': {ex}");
            }
        }

        private static void Disable(ModEntry entry)
        {
            if (!entry.Loaded)
                return;

            try { entry.Instance?.OnUnload(); }
            catch (Exception ex) { ModLog.Error($"Mod '{entry.Info.Id}' threw during OnUnload: {ex}"); }

            try { entry.Harmony?.UnpatchSelf(); }
            catch (Exception ex) { ModLog.Error($"Failed to unpatch mod '{entry.Info.Id}': {ex}"); }

            entry.Instance = null;
            entry.Harmony = null;
            entry.Loaded = false;
            ModLog.Info($"Mod disabled: {entry.Info.Id}");
        }

        private static void ApplyHarmonyPatches(ModInfo info, Harmony harmony)
        {
            try
            {
                harmony.PatchAll(info.Assembly);
                var patched = harmony.GetPatchedMethods().ToList();
                if (patched.Count > 0)
                    ModLog.Info($"Applied {patched.Count} Harmony patch method(s) from '{info.Id}'.");
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to apply Harmony patches from '{info.Id}': {ex}");
            }
        }

        // ---------------------------------------------------------------- lifecycle fan-out

        public static void Update() => ForEachLoaded(m => m.Instance.OnUpdate(), nameof(IMod.OnUpdate));
        public static void FixedUpdate() => ForEachLoaded(m => m.Instance.OnFixedUpdate(), nameof(IMod.OnFixedUpdate));
        public static void LateUpdate() => ForEachLoaded(m => m.Instance.OnLateUpdate(), nameof(IMod.OnLateUpdate));
        public static void DrawGUI() => ForEachLoaded(m => m.Instance.OnGUI(), nameof(IMod.OnGUI));

        public static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { }

        public static void UnloadAll()
        {
            GameEvents.RaiseApplicationQuitting();
            for (var i = Entries.Count - 1; i >= 0; i--)
            {
                var entry = Entries[i];
                if (!entry.Loaded)
                    continue;
                try { entry.Instance.OnUnload(); }
                catch (Exception ex) { ModLog.Error($"Mod '{entry.Info.Id}' threw during OnUnload: {ex}"); }
                try { entry.Harmony?.UnpatchSelf(); }
                catch { /* ignore */ }
                entry.Loaded = false;
                entry.Instance = null;
            }
        }

        private static void ForEachLoaded(Action<ModEntry> action, string phase)
        {
            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (!entry.Loaded)
                    continue;
                try { action(entry); }
                catch (Exception ex)
                {
                    ModLog.Error($"Mod '{entry.Info.Id}' threw in {phase}: {ex}");
                }
            }
        }
    }
}
