using System;
using System.Reflection;
using Timeflow.ModLoader.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using ModLog = Timeflow.ModLoader.Logging.ModLog;

namespace Timeflow.ModLoader
{
    /// <summary>
    /// Runs on Unity's main thread when the bootstrap hook fires. The hook can fire very early
    /// (before the engine registers its native internal calls), so initialization is retried on
    /// subsequent hook invocations until the engine is ready to create a GameObject.
    /// </summary>
    internal static class Startup
    {
        private const string GameAssemblyName = "Assembly-CSharp";
        private const int MaxReadinessRetries = 600;

        private static bool _initialized;
        private static GameObject _loaderObject;
        private static int _readinessRetries;
        private static long _lastAttemptTicks;

        /// <summary>Harmony prefix target. Must stay parameterless and avoid touching Unity statics.</summary>
        internal static void OnUnityReady()
        {
            if (_initialized)
                return;

            // Throttle: during early startup the hooks fire in bursts; do not attempt (and throw)
            // more than once every 100 ms until the engine is ready.
            var now = DateTime.UtcNow.Ticks;
            if (now - _lastAttemptTicks < TimeSpan.TicksPerMillisecond * 100)
                return;
            _lastAttemptTicks = now;

            try
            {
                Initialize();
            }
            catch (Exception ex)
            {
                // Expected while the engine is still bringing up its native internal calls.
                _readinessRetries++;
                if (_readinessRetries <= 3)
                {
                    ModLog.Debug($"Unity not ready yet (attempt {_readinessRetries}): {ex.GetType().Name}: {ex.Message}");
                }
                else if (_readinessRetries > MaxReadinessRetries)
                {
                    ModLog.Fatal($"Giving up waiting for Unity after {_readinessRetries} attempts: {ex}");
                    _initialized = true; // stop retrying
                }
            }
        }

        private static void Initialize()
        {
            // Creating the object first also serves as the readiness probe: it throws until
            // UnityEngine has registered Internal_CreateGameObject.
            if (_loaderObject == null)
            {
                var go = new GameObject("Timeflow_ModLoader");
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.HideAndDontSave;
                go.AddComponent<ModLoaderBehaviour>();
                _loaderObject = go;
            }

            if (_initialized)
                return;
            _initialized = true;

            ModLog.UnityAvailable = true;
            ModLog.Info($"Unity runtime ready after {_readinessRetries} readiness attempt(s); initializing mods.");

            HookSceneEvents();

            if (IsGameAssemblyLoaded())
            {
                OnGameAssemblyReady();
            }
            else
            {
                ModLog.Info($"Waiting for '{GameAssemblyName}' to be loaded before scanning mods...");
                AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
            }
        }

        private static void HookSceneEvents()
        {
            try
            {
                SceneManager.sceneLoaded += OnSceneLoaded;
            }
            catch (Exception ex)
            {
                ModLog.Warn("Could not subscribe to sceneLoaded: " + ex.Message);
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                GameEvents.RaiseSceneLoaded(scene, mode);
                ModManager.OnSceneLoaded(scene, mode);
            }
            catch (Exception ex)
            {
                ModLog.Error("Scene-loaded handling failed: " + ex);
            }
        }

        private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            if (!string.Equals(args.LoadedAssembly.GetName().Name, GameAssemblyName, StringComparison.OrdinalIgnoreCase))
                return;

            AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
            ModLog.Info($"'{GameAssemblyName}' loaded; scanning mods.");
            try { OnGameAssemblyReady(); }
            catch (Exception ex) { ModLog.Fatal("Mod loading failed: " + ex); }
        }

        /// <summary>Runs once the game's own assembly is available: install built-ins, then mods.</summary>
        private static void OnGameAssemblyReady()
        {
            try { Builtin.ModMenuButton.Install(); }
            catch (Exception ex) { ModLog.Error("Failed to install the Mods menu button: " + ex); }

            ModManager.LoadAll();
        }

        private static bool IsGameAssemblyLoaded()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(assembly.GetName().Name, GameAssemblyName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
