using GeneralImprovements.Utilities;
using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class ItemDropshipPatch
    {
        [HarmonyPatch(typeof(ItemDropship), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(ItemDropship __instance)
        {
            if (Plugin.ShowDropshipOnScanner.Value)
            {
                ObjectHelper.CreateScanNodeOnObject(__instance.gameObject, 0, 5, 50, "Dropship", size: 4);
            }
        }
    }
}