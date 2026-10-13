using UnityEngine;

namespace Timeflow.ModLoader.Modding
{
    /// <summary>
    /// Contract implemented by every mod. All callbacks are invoked on Unity's main thread.
    /// Exceptions thrown by a single mod are logged and do not stop the other mods.
    /// Consider deriving from <see cref="ModBase"/> instead of implementing this directly.
    /// </summary>
    public interface IMod
    {
        /// <summary>Called once, after the mod instance is created, before the first Update.</summary>
        void OnLoad();

        /// <summary>Called every frame.</summary>
        void OnUpdate();

        /// <summary>Called every fixed update step.</summary>
        void OnFixedUpdate();

        /// <summary>Called every frame, after Update.</summary>
        void OnLateUpdate();

        /// <summary>Called during the IMGUI pass (only when the overlay is drawn).</summary>
        void OnGUI();

        /// <summary>Called when the game quits or the mod is unloaded.</summary>
        void OnUnload();
    }
}
