using BepInEx;
using BepInEx.Bootstrap;
using System.Linq;

namespace GeneralImprovements.OtherMods
{
    internal static class FlashlightFixHelper
    {
        public const string GUID = "ShaosilGaming.FlashlightFix";
        public static bool IsActive { get; private set; }

        public static void Initialize()
        {
            // Find 
            var plugin = TypeLoader.FindPluginTypes(Paths.PluginPath, Chainloader.ToPluginInfo).FirstOrDefault(p => p.Value.FirstOrDefault()?.Metadata.GUID == GUID).Value?.FirstOrDefault();

            // Only detect as active if they are not on the latest version that removes all code
            IsActive = plugin != null && plugin.Metadata.Version.Minor < 2;
        }
    }
}