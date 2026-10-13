using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Timeflow.ModLoader.Logging;
using Timeflow.ModLoader.Patching;

namespace Timeflow.ModLoader
{
    /// <summary>
    /// First code that runs inside the game process (invoked by UnityDoorstop). It must stay
    /// dependency-free with respect to Unity: at this point the engine is not initialized yet.
    /// Its only job is to register services and install a hook that calls <see cref="Startup"/>
    /// once Unity is far enough along.
    /// </summary>
    internal static class Bootstrap
    {
        /// <summary>Entry point called from <c>Doorstop.Entrypoint.Start()</c>.</summary>
        internal static void Run()
        {
            // Phase 1 - only System types here, so nothing forces an early Unity load.
            try
            {
                Paths.Initialize();
                Paths.EnsureDirectories();
                ModLog.Initialize(Paths.LogsDir);
                AssemblyResolver.Install();

                ModLog.Info("======================================================");
                ModLog.Info("Timeflow ModLoader starting (Doorstop bootstrap).");
                ModLog.Info($"Game root : {Paths.GameRoot}");
                ModLog.Info($"Managed   : {Paths.ManagedDir}");
                ModLog.Info($"Loader    : {Paths.LoaderDir}");
                ModLog.Info($"Mods      : {Paths.ModsDir}");
            }
            catch (Exception ex)
            {
                // Without paths/logging there is very little we can do; write a last-resort file.
                TryWriteCrash("bootstrap_init", ex);
                return;
            }

            // Phase 2 - touches HarmonyLib; the resolver above is already installed.
            try
            {
                InstallUnityHook();
            }
            catch (Exception ex)
            {
                ModLog.Fatal("Failed to install the Unity bootstrap hook: " + ex);
                TryWriteCrash("bootstrap_hook", ex);
            }
        }

        /// <summary>
        /// Installs the main-thread Unity callback that runs <see cref="Startup"/> once the engine
        /// is initialized. Every candidate that can be patched is patched; whichever fires first
        /// wins (Startup is idempotent). Patching a static constructor with a runtime detour is
        /// not always effective under Mono, so the GameObject constructor is the dependable one.
        /// </summary>
        private static void InstallUnityHook()
        {
            var harmony = HarmonyProvider.Root;
            var prefix = typeof(Startup).GetMethod(
                nameof(Startup.OnUnityReady),
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (prefix == null)
            {
                ModLog.Fatal("Internal error: Startup.OnUnityReady was not found.");
                return;
            }

            var core = LoadUnityEngineCore();
            if (core == null)
            {
                ModLog.Fatal("Could not locate UnityEngine.CoreModule.dll; the loader cannot hook Unity.");
                return;
            }

            var installed = 0;

            // Only patch *plain managed* methods here. Methods that wrap native internal calls
            // (GameObject ctors, AddComponent, MonoBehaviour ctor, ...) must NOT be detoured:
            // Harmony replaces the body and the internal call (e.g. Internal_CreateGameObject)
            // then fails to resolve, breaking GameObject creation for the whole game.

            // Candidate 1: UnityEngine.Application static constructor (pure managed, earliest).
            var application = core.GetType("UnityEngine.Application");
            var cctor = application?.GetConstructor(
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null, Type.EmptyTypes, null);
            installed += TryPatch(harmony, cctor, prefix, "UnityEngine.Application::.cctor");

            // Candidate 2/3: Debug logging methods - managed, called very early and often, which
            // makes the readiness retry loop effective.
            var debugType = core.GetType("UnityEngine.Debug");
            installed += TryPatch(harmony, FindMethod(debugType, "Log", typeof(object)),
                prefix, "UnityEngine.Debug::Log(object)");
            installed += TryPatch(harmony, FindMethod(debugType, "LogError", typeof(object)),
                prefix, "UnityEngine.Debug::LogError(object)");

            if (installed == 0)
                ModLog.Fatal("No usable Unity bootstrap hook was found; mods will not be loaded.");
        }

        private static MethodInfo FindMethod(Type type, string name, params Type[] parameters)
            => type?.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);

        private static int TryPatch(Harmony harmony, MethodBase target, MethodInfo prefix, string label)
        {
            if (target == null)
            {
                ModLog.Warn($"Bootstrap hook candidate not found: {label}");
                return 0;
            }

            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                ModLog.Info($"Bootstrap hook installed: {label}");
                return 1;
            }
            catch (Exception ex)
            {
                ModLog.Warn($"Could not patch {label}: {ex.Message}");
                return 0;
            }
        }

        private static Assembly LoadUnityEngineCore()
        {
            const string name = "UnityEngine.CoreModule";

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(assembly.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
                    return assembly;
            }

            try { return Assembly.Load(new AssemblyName(name)); }
            catch { /* fall through to explicit path */ }

            try
            {
                var path = Path.Combine(Paths.ManagedDir, name + ".dll");
                if (File.Exists(path))
                    return Assembly.LoadFrom(path);
            }
            catch { /* ignored */ }

            return null;
        }

        private static void TryWriteCrash(string tag, Exception ex)
        {
            try
            {
                var dir = Environment.GetEnvironmentVariable("DOORSTOP_PROCESS_PATH");
                var baseDir = !string.IsNullOrEmpty(dir)
                    ? Path.GetDirectoryName(dir)
                    : AppContext.BaseDirectory;
                var file = Path.Combine(baseDir, $"ModLoader_crash_{tag}.log");
                File.WriteAllText(file, ex.ToString());
            }
            catch { /* give up silently */ }
        }
    }
}
