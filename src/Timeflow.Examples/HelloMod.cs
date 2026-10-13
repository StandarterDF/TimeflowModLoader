using Timeflow.ModLoader;
using Timeflow.ModLoader.Modding;
using UnityEngine.SceneManagement;

namespace Timeflow.Examples
{
    /// <summary>
    /// Difficulty: ★☆☆ (beginner).
    ///
    /// The smallest useful mod: it logs when it is loaded/unloaded and reacts to engine
    /// scene changes through <see cref="GameEvents"/>. Use it as a template to verify that
    /// the loader sees your DLL at all.
    ///
    /// Demonstrates: the [Mod] attribute, ModBase, the scoped <c>Log</c>, GameEvents.
    /// </summary>
    [Mod(
        "example.hello",
        Name = "Hello World",
        Version = "1.0.0",
        Author = "Timeflow examples",
        Description = "Minimal example: logs load/unload and scene changes.",
        LoadPriority = 100)]
    public sealed class HelloMod : ModBase
    {
        public override void OnLoad()
        {
            Log.Info("Hello, Timeflow! (example.hello)");
            GameEvents.SceneLoaded += OnSceneLoaded;
        }

        public override void OnUnload()
        {
            // Always undo what OnLoad did: subscriptions, patches, UI, timers...
            GameEvents.SceneLoaded -= OnSceneLoaded;
            Log.Info("Goodbye from example.hello.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Log.Info($"Scene loaded: '{scene.name}' ({mode}).");
        }
    }
}
