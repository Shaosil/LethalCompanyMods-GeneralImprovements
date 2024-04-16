using BepInEx.Bootstrap;

namespace GeneralImprovements.OtherMods
{
    internal static class MimicsHelper
    {
        public const string GUID = "x753.Mimics";
        public static bool IsActive { get; private set; }

        public static void Initialize()
        {
            IsActive = Chainloader.PluginInfos.ContainsKey(GUID);
        }
    }
}