using BepInEx.Bootstrap;

namespace GeneralImprovements.OtherMods
{
    internal static class TwoRadarCamsHelper
    {
        public static bool IsActive { get; private set; }

        public static void Initialize()
        {
            IsActive = Chainloader.PluginInfos.ContainsKey("Zaggy1024.TwoRadarMaps");
        }
    }
}