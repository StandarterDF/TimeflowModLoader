using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Timeflow.ModLoader.Logging;
using Timeflow.ModLoader.Modding;

namespace Timeflow.ModLoader
{
    /// <summary>Scans the Mods folder for assemblies containing <see cref="ModAttribute"/> types.</summary>
    internal static class ModDiscovery
    {
        // Assemblies that are part of the loader itself and must never be treated as mods.
        private static readonly HashSet<string> ReservedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Timeflow.ModLoader.dll",
            "0Harmony.dll"
        };

        public static List<ModInfo> Discover(string modsDirectory)
        {
            var result = new List<ModInfo>();
            if (!Directory.Exists(modsDirectory))
            {
                ModLog.Warn($"Mods directory does not exist: {modsDirectory}");
                return result;
            }

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dll in Directory.EnumerateFiles(modsDirectory, "*.dll", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileName(dll);
                if (ReservedAssemblies.Contains(fileName))
                    continue;

                Assembly assembly;
                try
                {
                    assembly = Assembly.LoadFrom(dll);
                }
                catch (Exception ex)
                {
                    ModLog.Warn($"Could not load assembly '{dll}': {ex.Message}");
                    continue;
                }

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                    ModLog.Warn($"'{fileName}' has types that failed to load; scanning the rest.");
                }
                catch (Exception ex)
                {
                    ModLog.Warn($"Could not enumerate types of '{fileName}': {ex.Message}");
                    continue;
                }

                foreach (var type in types)
                {
                    ModAttribute attribute;
                    try { attribute = type.GetCustomAttribute<ModAttribute>(false); }
                    catch { continue; }

                    if (attribute == null)
                        continue;

                    if (type.IsAbstract || type.IsInterface)
                    {
                        ModLog.Warn($"Skipping [{type.FullName}]: mods must be concrete classes.");
                        continue;
                    }

                    if (!typeof(IMod).IsAssignableFrom(type))
                    {
                        ModLog.Warn($"Skipping [{type.FullName}]: it is marked [Mod] but does not implement IMod.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(attribute.Id))
                    {
                        ModLog.Warn($"Skipping [{type.FullName}]: [Mod] id is empty.");
                        continue;
                    }

                    if (!seenIds.Add(attribute.Id))
                    {
                        ModLog.Warn($"Skipping [{type.FullName}]: mod id '{attribute.Id}' is already provided by another assembly.");
                        continue;
                    }

                    var info = new ModInfo
                    {
                        Id = attribute.Id,
                        Name = string.IsNullOrWhiteSpace(attribute.Name) ? attribute.Id : attribute.Name,
                        Version = attribute.Version ?? "0.0.0",
                        Author = attribute.Author ?? string.Empty,
                        Description = attribute.Description ?? string.Empty,
                        Dependencies = attribute.Dependencies ?? Array.Empty<string>(),
                        LoadPriority = attribute.LoadPriority,
                        AssemblyPath = dll,
                        Assembly = assembly,
                        ModType = type
                    };

                    ModLog.Info($"Discovered mod {info}");
                    result.Add(info);
                }
            }

            return result;
        }
    }
}
