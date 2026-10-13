using System;
using System.Collections.Generic;
using System.Linq;
using Timeflow.ModLoader.Logging;
using Timeflow.ModLoader.Modding;

namespace Timeflow.ModLoader
{
    /// <summary>
    /// Produces a loading order for discovered mods: dependencies first, then by descending
    /// <see cref="ModAttribute.LoadPriority"/>, then alphabetically for determinism.
    /// Missing dependencies cause the dependent mod to be skipped (with a clear log line).
    /// </summary>
    internal static class ModSorter
    {
        public static List<ModInfo> Sort(IReadOnlyList<ModInfo> mods)
        {
            var result = new List<ModInfo>();
            var byId = new Dictionary<string, ModInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in mods)
                byId[mod.Id] = mod;

            var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // 0 = visiting, 1 = done

            // Deterministic starting order.
            var ordered = mods
                .OrderByDescending(m => m.LoadPriority)
                .ThenBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var mod in ordered)
                Visit(mod, byId, state, result, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            return result;
        }

        private static void Visit(
            ModInfo mod,
            Dictionary<string, ModInfo> byId,
            Dictionary<string, int> state,
            List<ModInfo> result,
            HashSet<string> path)
        {
            if (state.TryGetValue(mod.Id, out var s))
            {
                if (s == 1)
                    return;
                if (s == 0)
                {
                    ModLog.Error($"Dependency cycle detected involving '{mod.Id}'; breaking the cycle.");
                    return;
                }
            }

            state[mod.Id] = 0;
            path.Add(mod.Id);

            foreach (var depId in mod.Dependencies)
            {
                if (string.IsNullOrWhiteSpace(depId))
                    continue;

                if (!byId.TryGetValue(depId, out var dep))
                {
                    ModLog.Error($"Mod '{mod.Id}' depends on '{depId}', which was not found. The mod will not be loaded.");
                    path.Remove(mod.Id);
                    state[mod.Id] = 1; // mark done so we do not load it
                    return;
                }

                if (path.Contains(dep.Id))
                {
                    ModLog.Error($"Dependency cycle detected between '{mod.Id}' and '{dep.Id}'; ignoring the edge.");
                    continue;
                }

                Visit(dep, byId, state, result, path);
            }

            path.Remove(mod.Id);
            state[mod.Id] = 1;

            // Skip mods whose dependency was itself skipped due to a missing dependency.
            if (result.Contains(mod))
                return;

            result.Add(mod);
        }
    }
}
