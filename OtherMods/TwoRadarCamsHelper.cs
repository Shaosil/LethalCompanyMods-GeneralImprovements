using BepInEx.Bootstrap;

namespace GeneralImprovements.OtherMods
{
    internal static class TwoRadarCamsHelper
    {
        public const string GUID = "Zaggy1024.TwoRadarMaps";
        public static bool IsActive { get; private set; }
        public static ManualCameraRenderer MapRenderer { get; private set; }

        public static void Initialize()
        {
            IsActive = Chainloader.PluginInfos.ContainsKey(GUID);
        }

        public static void TerminalStarted(Terminal terminal)
        {
            if (IsActive)
                MapRenderer = terminal.GetComponent<ManualCameraRenderer>();
        }
    }
}