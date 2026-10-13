using Timeflow.ModLoader.Logging;
using Timeflow.ModLoader.UI;
using UnityEngine;
using ModLog = Timeflow.ModLoader.Logging.ModLog;

namespace Timeflow.ModLoader
{
    /// <summary>
    /// Persistent component the loader attaches to a hidden GameObject. It forwards Unity
    /// lifecycle callbacks to every loaded mod and draws the optional console overlay.
    /// </summary>
    internal sealed class ModLoaderBehaviour : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            ModManager.Update();
            ConsoleOverlay.Instance.Update();
        }

        private void FixedUpdate() => ModManager.FixedUpdate();

        private void LateUpdate() => ModManager.LateUpdate();

        private void OnGUI()
        {
            ModManager.DrawGUI();
            ConsoleOverlay.Instance.OnGUI();
        }

        private void OnApplicationQuit()
        {
            ModLog.Info("Application quitting; unloading mods.");
            ModManager.UnloadAll();
        }
    }
}
