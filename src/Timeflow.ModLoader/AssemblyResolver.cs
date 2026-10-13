using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Timeflow.ModLoader.Logging;

namespace Timeflow.ModLoader
{
    /// <summary>
    /// Resolves assemblies that the default probing paths cannot find - most importantly
    /// <c>0Harmony.dll</c> (our patching backend) and any libraries shipped alongside a mod.
    /// </summary>
    internal static class AssemblyResolver
    {
        private static readonly HashSet<string> ProbeCache = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool _installed;

        public static void Install()
        {
            if (_installed)
                return;
            _installed = true;
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            var requested = new AssemblyName(args.Name);
            var simpleName = requested.Name + ".dll";

            foreach (var dir in EnumerateProbeDirectories(args))
            {
                var candidate = Path.Combine(dir, simpleName);
                if (!File.Exists(candidate))
                    continue;

                try
                {
                    var assembly = Assembly.LoadFrom(candidate);
                    if (!ProbeCache.Contains(candidate))
                    {
                        ProbeCache.Add(candidate);
                        ModLog.Debug($"Resolved '{requested.Name}' -> {candidate}");
                    }
                    return assembly;
                }
                catch (Exception ex)
                {
                    ModLog.Warn($"Found '{candidate}' but failed to load it: {ex.Message}");
                }
            }

            return null;
        }

        private static IEnumerable<string> EnumerateProbeDirectories(ResolveEventArgs args)
        {
            // Directory of the assembly that requested the dependency (mod-local libs).
            if (args.RequestingAssembly != null)
            {
                string requestingDir = null;
                try { requestingDir = Path.GetDirectoryName(args.RequestingAssembly.Location); }
                catch { }
                if (!string.IsNullOrEmpty(requestingDir))
                    yield return requestingDir;
            }

            yield return Paths.LoaderDir;
            yield return Paths.CoreDir;
            yield return Paths.DataRoot;
            yield return Paths.ModsDir;
            yield return Paths.GameRoot;
            yield return Paths.ManagedDir;
        }
    }
}
