using System;
using UnityEngine.SceneManagement;

namespace Timeflow.ModLoader
{
    /// <summary>
    /// Global gameplay events that mods can subscribe to. Subscribe from <c>OnLoad</c> and
    /// always unsubscribe in <c>OnUnload</c> to avoid leaks on hot reload / shutdown.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Raised after a scene has finished loading.</summary>
        public static event Action<Scene, LoadSceneMode> SceneLoaded;

        /// <summary>Raised right before the process quits.</summary>
        public static event Action ApplicationQuitting;

        /// <summary>Raised on the first frame after all mods have been loaded.</summary>
        public static event Action AllModsLoaded;

        internal static void RaiseSceneLoaded(Scene scene, LoadSceneMode mode)
            => SafeInvoke(SceneLoaded, scene, mode);

        internal static void RaiseApplicationQuitting()
            => SafeInvoke(ApplicationQuitting);

        internal static void RaiseAllModsLoaded()
            => SafeInvoke(AllModsLoaded);

        private static void SafeInvoke(Action action)
        {
            if (action == null) return;
            foreach (var d in action.GetInvocationList())
            {
                try { ((Action)d)(); }
                catch (Exception ex) { Logging.ModLog.Error("GameEvents handler threw: " + ex); }
            }
        }

        private static void SafeInvoke<T1, T2>(Action<T1, T2> action, T1 a1, T2 a2)
        {
            if (action == null) return;
            foreach (var d in action.GetInvocationList())
            {
                try { ((Action<T1, T2>)d)(a1, a2); }
                catch (Exception ex) { Logging.ModLog.Error("GameEvents handler threw: " + ex); }
            }
        }
    }
}
