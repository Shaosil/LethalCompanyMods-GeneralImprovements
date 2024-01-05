using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class GrabbableObjectPatch
    {
        [HarmonyPatch(typeof(GrabbableObject), nameof(Start))]
        [HarmonyPrefix]
        private static void Start(GrabbableObject __instance)
        {
            // Ensure no non-scrap items have scrap value. This will update its value and description
            if (!__instance.itemProperties.isScrap)
            {
                if (__instance.GetComponentInChildren<ScanNodeProperties>() is ScanNodeProperties scanNode)
                {
                    // If the previous description had something other than "Value...", restore it afterwards
                    string oldDesc = scanNode.subText;
                    __instance.SetScrapValue(0);

                    if (oldDesc != null && !oldDesc.ToLower().StartsWith("value"))
                    {
                        scanNode.subText = oldDesc;
                    }
                }
                else
                {
                    __instance.scrapValue = 0;
                }
            }

            // Allow all items to be grabbed before game start
            if (!__instance.itemProperties.canBeGrabbedBeforeGameStart)
            {
                __instance.itemProperties.canBeGrabbedBeforeGameStart = true;
            }
        }
    }
}