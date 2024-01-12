using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class GrabbableObjectsPatch
    {
        private static HashSet<GrabbableObject> _itemsToKeepInPlace = new HashSet<GrabbableObject>();

        [HarmonyPatch(typeof(GrabbableObject), "Start")]
        [HarmonyPrefix]
        private static void Start_Pre(GrabbableObject __instance)
        {
            if (__instance is ClipboardItem || (__instance is PhysicsProp && __instance.itemProperties.itemName == "Sticky note"))
            {
                // If this is the clipboard or sticky note, and we want to hide them, do so
                if (Plugin.HideClipboardAndStickyNote.Value)
                {
                    __instance.gameObject.SetActive(false);
                }
                // Otherwise, pin the clipboard to the wall when loading in
                else if (__instance is ClipboardItem)
                {
                    __instance.transform.SetPositionAndRotation(new Vector3(11.02f, 2.45f, -13.4f), Quaternion.Euler(0, 180, 90));
                }
            }

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

            // Prevent ship items from falling through objects when they spawn (prefix)
            if (Plugin.FixItemsFallingThrough.Value && __instance.isInShipRoom && __instance.isInElevator && __instance.targetFloorPosition == Vector3.zero)
            {
                _itemsToKeepInPlace.Add(__instance);
            }
        }

        [HarmonyPatch(typeof(GrabbableObject), "Start")]
        [HarmonyPostfix]
        private static void Start_Post(GrabbableObject __instance)
        {
            // Prevent ship items from falling through objects when they spawn (postfix)
            if (_itemsToKeepInPlace.Contains(__instance))
            {
                __instance.fallTime = 1;
                __instance.reachedFloorTarget = false;
                __instance.targetFloorPosition = __instance.startFallingPosition;
                _itemsToKeepInPlace.Remove(__instance);
            }
        }
    }
}