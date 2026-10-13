// UnityDoorstop entry point. Doorstop looks for exactly `Doorstop.Entrypoint.Start()`.
// Keep this tiny: the real work lives in Timeflow.ModLoader.Bootstrap.
namespace Doorstop
{
    internal static class Entrypoint
    {
        public static void Start()
        {
            global::Timeflow.ModLoader.Bootstrap.Run();
        }
    }
}
