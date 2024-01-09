using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class GrabbableObjectsPatch
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

            // Pin the clipboard to the wall when loading in
            if (__instance is ClipboardItem clipboard)
            {
                clipboard.transform.SetPositionAndRotation(new Vector3(11.02f, 2.45f, -13.4f), Quaternion.Euler(0, 180, 90));
            }

            // Fix conductivity of certain objects
            if (__instance.itemProperties != null)
            {
                var nonConductiveItems = new string[] { "Flask", "Whoopie Cushion" };
                var tools = new string[] { "Jetpack", "Key", "Radar-booster", "Shovel", "Stop sign", "TZP-Inhalant", "Yield sign", "Zap gun" };

                if (nonConductiveItems.Any(n => __instance.itemProperties.itemName.Equals(n, StringComparison.OrdinalIgnoreCase))
                    || (Plugin.ToolsDoNotAttractLightning.Value && tools.Any(t => __instance.itemProperties.itemName.Equals(t, StringComparison.OrdinalIgnoreCase))))
                {
                    Plugin.MLS.LogInfo($"Item {__instance.itemProperties.itemName} being set to NON conductive.");
                    __instance.itemProperties.isConductiveMetal = false;
                }
            }
        }
    }
}