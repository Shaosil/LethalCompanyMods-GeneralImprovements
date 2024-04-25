using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
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
                    __instance.transform.SetLocalPositionAndRotation(new Vector3(2, 2.25f, -9.125f), Quaternion.Euler(0, -90, 90));
                }

                // Fix this being set elsewhere
                __instance.scrapPersistedThroughRounds = false;
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
                var tools = new string[] { "Extension ladder", "Jetpack", "Key", "Radar-booster", "Shovel", "Stop sign", "TZP-Inhalant", "Yield sign", "Kitchen knife", "Zap gun" };

                if (nonConductiveItems.Any(n => __instance.itemProperties.itemName.Equals(n, StringComparison.OrdinalIgnoreCase))
                    || (Plugin.ToolsDoNotAttractLightning.Value && tools.Any(t => __instance.itemProperties.itemName.Equals(t, StringComparison.OrdinalIgnoreCase))))
                {
                    Plugin.MLS.LogInfo($"Item {__instance.itemProperties.itemName} being set to NON conductive.");
                    __instance.itemProperties.isConductiveMetal = false;
                }
            }

            // Prevent ship items from falling through objects when they spawn (prefix)
            if (Plugin.FixItemsFallingThrough.Value && __instance.isInShipRoom && __instance.isInElevator && __instance.scrapPersistedThroughRounds)
            {
                Plugin.MLS.LogDebug($"KEEPING {__instance.name} IN PLACE");
                _itemsToKeepInPlace.Add(__instance);
            }

            // Fix any min and max values being reversed
            if (__instance.itemProperties.minValue > __instance.itemProperties.maxValue)
            {
                int oldMin = __instance.itemProperties.minValue;
                __instance.itemProperties.minValue = __instance.itemProperties.maxValue;
                __instance.itemProperties.maxValue = oldMin;
            }

            // Add scan nodes to tools if requested
            if (Plugin.ScannableToolVals.Any(t => __instance.GetType() == t) && __instance.GetComponentInChildren<ScanNodeProperties>() == null)
            {
                ItemHelper.CreateScanNodeOnObject(__instance.gameObject, 0, 1, 13, __instance.itemProperties.itemName);
            }
        }

        [HarmonyPatch(typeof(GrabbableObject), "Start")]
        [HarmonyPostfix]
        private static void Start_Post(GrabbableObject __instance)
        {
            if (Plugin.FixItemsLoadingSameRotation.Value)
            {
                __instance.floorYRot = -1; // If not initialized to -1, all items will rotate to 0 Y when "hitting the floor" on spawn
            }

            // Prevent ship items from falling through objects when they spawn (postfix)
            if (_itemsToKeepInPlace.Contains(__instance))
            {
                __instance.fallTime = 1;
                __instance.reachedFloorTarget = false;
                __instance.targetFloorPosition = __instance.transform.localPosition;
                _itemsToKeepInPlace.Remove(__instance);
            }
        }

        [HarmonyPatch(typeof(GrabbableObject), "OnHitGround")]
        [HarmonyPatch(typeof(StunGrenadeItem), "ExplodeStunGrenade")]
        [HarmonyPostfix]
        private static void OnHitGroundOrExplode(GrabbableObject __instance)
        {
            if (__instance.isInShipRoom)
            {
                MonitorsHelper.UpdateShipScrapMonitors();
                MonitorsHelper.UpdateScrapLeftMonitors();
            }
        }

        [HarmonyPatch(typeof(KeyItem), "ItemActivate")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PatchKeyActivate(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.UnlockDoorsFromInventory.Value || Plugin.KeysHaveInfiniteUses.Value)
            {
                // Call DestroyItemInSlot instead of DespawnHeldObject
                if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.IsLdarg(),
                    i => i.LoadsField(typeof(GrabbableObject).GetField(nameof(GrabbableObject.playerHeldBy))),
                    i => i.Calls(typeof(PlayerControllerB).GetMethod(nameof(PlayerControllerB.DespawnHeldObject)))
                }, out var found))
                {
                    if (Plugin.KeysHaveInfiniteUses.Value)
                    {
                        Plugin.MLS.LogDebug("Patching key activate to no longer call DespawnHeldObject.");

                        // Simply remove the call (3 lines)
                        for (int i = 0; i < found.Length; i++)
                        {
                            found[i].Instruction.opcode = OpCodes.Nop;
                        }
                    }
                    else
                    {
                        Plugin.MLS.LogDebug("Patching key activate to call DestroyItemInSlot instead of DespawnHeldObject.");

                        // Remove the previous line that loads playerHeldBy onto the stack
                        found[1].Instruction.opcode = OpCodes.Nop;

                        // Insert a new instruction delegate at the current new index
                        Action<KeyItem> callDestroy = k =>
                        {
                            for (int i = 0; i < StartOfRound.Instance.localPlayerController.ItemSlots.Length; i++)
                            {
                                if (StartOfRound.Instance.localPlayerController.ItemSlots[i] == k)
                                {
                                    ItemHelper.DestroyLocalItemAndSync(i);
                                    return;
                                }
                            }
                        };
                        codeList[found.Last().Index] = Transpilers.EmitDelegate(callDestroy);
                    }
                }
            }

            return codeList;
        }

        public static KeyValuePair<int, int> GetOutsideScrap(bool approximate)
        {
            // Get every non-ragdoll and unexploded grenade/grabbable outside of the ship that has a minimum value
            var fixedRandom = new System.Random(StartOfRound.Instance.randomMapSeed + 91); // Why 91? Shrug. It's the offset in vanilla code and I kept it.
            var valuables = UnityEngine.Object.FindObjectsOfType<GrabbableObject>().Where(o => !o.isInShipRoom && !o.isInElevator && o.itemProperties.minValue > 0
                && !(o is RagdollGrabbableObject) && (!(o is StunGrenadeItem grenade) || !grenade.hasExploded || !grenade.DestroyGrenade)).ToList();

            float multiplier = RoundManager.Instance.scrapValueMultiplier;
            int sum = approximate ? (int)Math.Round(valuables.Sum(i => fixedRandom.Next(Mathf.Clamp(i.itemProperties.minValue, 0, i.itemProperties.maxValue), i.itemProperties.maxValue) * multiplier))
                : valuables.Sum(i => i.scrapValue);

            return new KeyValuePair<int, int>(valuables.Count, sum);
        }
    }
}