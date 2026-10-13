using System;
using HarmonyLib;
using Timeflow.ModLoader.Config;
using Timeflow.ModLoader.Logging;

namespace Timeflow.ModLoader.Modding
{
    /// <summary>
    /// Convenience base class for mods. Provides access to the mod's metadata, a scoped logger,
    /// a dedicated Harmony instance and a per-mod configuration file.
    /// </summary>
    public abstract class ModBase : IMod
    {
        /// <summary>Metadata of this mod. Assigned by the loader before <see cref="OnLoad"/>.</summary>
        public ModInfo Info { get; internal set; }

        /// <summary>Scoped logger. Assigned by the loader before <see cref="OnLoad"/>.</summary>
        public ModLogger Log { get; private set; } = new ModLogger("Mod");

        /// <summary>Harmony instance bound to this mod. Create patches with <c>Harmony.PatchAll()</c> etc.</summary>
        public Harmony Harmony { get; private set; }

        /// <summary>Persistent per-mod config file.</summary>
        public ModConfig Config { get; private set; }

        internal void Bind(ModInfo info, Harmony harmony)
        {
            Info = info;
            Harmony = harmony;
            Log = new ModLogger(info.Id);
            Config = new ModConfig(info.Id);
        }

        public virtual void OnLoad() { }
        public virtual void OnUpdate() { }
        public virtual void OnFixedUpdate() { }
        public virtual void OnLateUpdate() { }
        public virtual void OnGUI() { }
        public virtual void OnUnload() { }
    }
}
