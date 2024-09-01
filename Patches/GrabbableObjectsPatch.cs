using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
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
                // Otherwise, pin the clipboard to the wall when loading in if needed
                else if (__instance is ClipboardItem clipboardItem && !clipboardItem.truckManual && Plugin.MoveShipClipboardToWall.Value)
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

            // Prevent ship items from falling through objects when they spawn (prefix)
            if (Plugin.FixItemsFallingThrough.Value && __instance.isInShipRoom && __instance.isInElevator && __instance.scrapPersistedThroughRounds)
            {
                Plugin.MLS.LogDebug($"KEEPING {__instance.name} IN PLACE");
                _itemsToKeepInPlace.Add(__instance);
            }

            // Add scan nodes to tools if requested
            if (Plugin.ScannableToolVals.Any(t => __instance.GetType() == t) && __instance.GetComponentInChildren<ScanNodeProperties>() == null)
            {
                ObjectHelper.CreateScanNodeOnObject(__instance.gameObject, 0, 1, 13, __instance.itemProperties.itemName);
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
        private static IEnumerable<CodeInstruction> PatchKeyActivate(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            return ApplyGenericKeyActivateTranspiler(instructions.ToList(), method);
        }

        [HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.GetItemFloorPosition))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> GetItemFloorPosition_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (!Plugin.ShipPlaceablesCollide.Value)
            {
                // If ship placeables no longer collide, their layers will have changed, so include InteractableObject layers in item drop raycasts for floor target collisions
                if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.LoadsConstant(0x10000901),
                    i => i.LoadsConstant(1),
                    i => i.Calls(typeof(Physics).GetMethod(nameof(Physics.Raycast), new Type[] { typeof(Vector3), typeof(Vector3), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int), typeof(QueryTriggerInteraction) }))
                }, out var raycastCode))
                {
                    Plugin.MLS.LogDebug("Patching GrabbableObject.GetItemFloorPosition to include the InteractableObject layer.");
                    raycastCode[0].Instruction.operand = 0x10000B01;
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL Code - Could not patch GrabbableObject.GetItemFloorPosition to include the InteractableObject layer!");
                }
            }

            return codeList;
        }

        public static IEnumerable<CodeInstruction> ApplyGenericKeyActivateTranspiler(List<CodeInstruction> codeList, MethodBase method)
        {
            // Make sure we only transpile if needed
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
                        Plugin.MLS.LogDebug($"Patching key activate (in {method.DeclaringType.Name}.{method.Name}) to no longer call DespawnHeldObject.");

                        // Simply remove the call (3 lines)
                        for (int i = 0; i < found.Length; i++)
                        {
                            found[i].Instruction.opcode = OpCodes.Nop;
                        }
                    }
                    else
                    {
                        Plugin.MLS.LogDebug($"Patching key activate (in {method.DeclaringType.Name}.{method.Name}) to call DestroyItemInSlot instead of DespawnHeldObject.");

                        // Remove the previous line that loads playerHeldBy onto the stack
                        found[1].Instruction.opcode = OpCodes.Nop;

                        // Insert a new instruction delegate at the current new index
                        Action<KeyItem> callDestroy = k =>
                        {
                            for (int i = 0; i < StartOfRound.Instance.localPlayerController.ItemSlots.Length; i++)
                            {
                                if (StartOfRound.Instance.localPlayerController.ItemSlots[i] == k)
                                {
                                    ObjectHelper.DestroyLocalItemAndSync(i);
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

        public static List<GrabbableObject> GetAllScrap(bool inShip)
        {
            return UnityEngine.Object.FindObjectsOfType<GrabbableObject>().Where(o => o.itemProperties.isScrap && o.isInShipRoom == inShip && o.isInElevator == inShip && o.itemProperties.minValue > 0
                && !(o is RagdollGrabbableObject) && (!(o is StunGrenadeItem grenade) || !grenade.hasExploded || !grenade.DestroyGrenade)).ToList();
        }

        public static KeyValuePair<int, int> GetScrapAmountAndValue(bool approximate)
        {
            // Get every non-ragdoll and unexploded grenade/grabbable outside of the ship that has a minimum value
            var fixedRandom = new System.Random(StartOfRound.Instance.randomMapSeed + 91); // Why 91? Shrug. It's the offset in vanilla code and I kept it.
            var valuables = GetAllScrap(false);
            float multiplier = RoundManager.Instance.scrapValueMultiplier;
            int sum = approximate ? (int)Math.Round(valuables.Sum(i => fixedRandom.Next(Mathf.Clamp(i.itemProperties.minValue, 0, i.itemProperties.maxValue), i.itemProperties.maxValue) * multiplier))
                : valuables.Sum(i => i.scrapValue);

            return new KeyValuePair<int, int>(valuables.Count, sum);
        }
    }
}