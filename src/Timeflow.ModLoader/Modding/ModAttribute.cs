using System;

namespace Timeflow.ModLoader.Modding
{
    /// <summary>
    /// Marks a class as a Timeflow mod. The class must implement <see cref="IMod"/>
    /// (usually by deriving from <see cref="ModBase"/>) and have a public parameterless constructor.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ModAttribute : Attribute
    {
        /// <summary>Stable, unique identifier (recommended: like "author.modname").</summary>
        public string Id { get; }

        /// <summary>Human readable name shown in logs.</summary>
        public string Name { get; set; }

        /// <summary>Mod version, e.g. "1.0.0".</summary>
        public string Version { get; set; }

        public string Author { get; set; }

        public string Description { get; set; }

        /// <summary>Ids of mods that must be loaded before this one. Defaults to none.</summary>
        public string[] Dependencies { get; set; }

        /// <summary>Higher values load earlier among mods that have no ordering constraints.</summary>
        public int LoadPriority { get; set; }

        public ModAttribute(string id)
        {
            Id = id;
        }
    }
}
