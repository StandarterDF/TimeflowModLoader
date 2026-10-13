using System;
using System.Reflection;

namespace Timeflow.ModLoader.Modding
{
    /// <summary>Immutable metadata describing a discovered mod.</summary>
    public sealed class ModInfo
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public string Version { get; internal set; }
        public string Author { get; internal set; }
        public string Description { get; internal set; }
        public string[] Dependencies { get; internal set; } = Array.Empty<string>();
        public int LoadPriority { get; internal set; }
        public string AssemblyPath { get; internal set; }
        public Assembly Assembly { get; internal set; }
        public Type ModType { get; internal set; }

        public override string ToString()
            => $"{Name ?? Id} ({Id}) v{Version ?? "?"}";
    }
}
