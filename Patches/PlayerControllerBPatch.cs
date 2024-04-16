﻿using GameNetcodeStuff;
using GeneralImprovements.OtherMods;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Netcode;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class PlayerControllerBPatch
    {
        private static FieldInfo _timeSinceSwitchingSlotsField = null;
        private static FieldInfo TimeSinceSwitchingSlotsField
        {
            get
            {
                // Lazy load and cache the reflection info
                if (_timeSinceSwitchingSlotsField == null)
                {
                    _timeSinceSwitchingSlotsField = typeof(PlayerControllerB).GetField("timeSinceSwitchingSlots", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return _timeSinceSwitchingSlotsField;
            }
        }
        private static float _originalCursorScale = 0;

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        private static void ConnectClientToPlayerObjectPre(PlayerControllerB __instance)
        {
            _originalCursorScale = __instance.cursorIcon.transform.localScale.x;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPriority(Priority.Low)]
        [HarmonyFinalizer] // Need a finalizer because CorporateRestructure sometimes has an exception before this, stopping our hide code
        private static void ConnectClientToPlayerObjectPost(PlayerControllerB __instance)
        {
            // If using the new monitors, hide the old text objects HERE in order to let other mods utilize them first
            if (Plugin.UseBetterMonitors.Value)
            {
                MonitorsHelper.HideOldMonitors();
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(FirstEmptyItemSlot))]
        [HarmonyPrefix]
        private static bool FirstEmptyItemSlot(PlayerControllerB __instance, ref int __result)
        {
            // If not configured to pickup in order, OR the reserved item slot or advanced company mods exist, pass over this patch
            if (!Plugin.PickupInOrder.Value || ReservedItemSlotCoreHelper.Assembly != null || AdvancedCompanyHelper.IsActive)
            {
                return true;
            }

            // Otherwise, rewrite the method to always return the first empty slot
            __result = -1;

            for (int i = 0; i < __instance.ItemSlots.Length; i++)
            {
                if (__instance.ItemSlots[i] == null)
                {
                    __result = i;
                    break;
                }
            }

            return false;
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(GrabObjectClientRpc))]
        [HarmonyPrefix]
        private static void GrabObjectClientRpc(PlayerControllerB __instance, bool grabValidated, NetworkObjectReference grabbedObject)
        {
            // If the player is about to pick up a two handed item and we are configured to do this, make sure it lands in slot 1
            if (!Plugin.TwoHandedInSlotOne.Value || !grabValidated || !grabbedObject.TryGet(out var networkObject))
            {
                return;
            }

            // Make sure this is a two handed object and we aren't currently processing it
            var grabbableObject = networkObject.gameObject.GetComponentInChildren<GrabbableObject>();
            if (!grabbableObject?.itemProperties.twoHanded ?? false)
            {
                return;
            }

            Plugin.MLS.LogDebug($"Two handed item being grabbed!");

            // Move things over
            ShiftRightFromSlot(__instance, 0);
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(GrabObjectClientRpc))]
        [HarmonyPostfix]
        private static void GrabObjectClientRpc(PlayerControllerB __instance, NetworkObjectReference grabbedObject)
        {
            if (__instance.currentlyHeldObjectServer?.isInShipRoom ?? false)
            {
                MonitorsHelper.UpdateShipScrapMonitors();
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "PlaceObjectClientRpc")]
        [HarmonyPatch(typeof(PlayerControllerB), "ThrowObjectClientRpc")]
        [HarmonyPatch(typeof(PlayerControllerB), "DespawnHeldObjectClientRpc")]
        [HarmonyPatch(typeof(PlayerControllerB), "DestroyItemInSlot")]
        [HarmonyPostfix]
        private static void ItemLeftSlot(PlayerControllerB __instance)
        {
            // If we are not configured to rearrange, or AdvancedCompany is active, skip this patch
            if (!Plugin.RearrangeOnDrop.Value || AdvancedCompanyHelper.IsActive)
            {
                return;
            }

            for (int i = 0; i < __instance.ItemSlots.Length - 1; i++)
            {
                // Each time we find an empty slot, move the first found item after this slot to this one
                if (__instance.ItemSlots[i] == null)
                {
                    for (int j = i + 1; j < __instance.ItemSlots.Length; j++)
                    {
                        // If we found a reserved core slot, skip it
                        if (ReservedItemSlotCoreHelper.IsReservedItemSlot(__instance, j))
                        {
                            continue;
                        }

                        if (__instance.ItemSlots[j] != null)
                        {
                            ShiftSlots(__instance, i, j);
                            break;
                        }
                    }
                }
            }

            // Refresh the current item slot if the player is holding something new
            var newHeldItem = __instance.ItemSlots[__instance.currentItemSlot];
            if (__instance.currentlyHeldObjectServer != newHeldItem)
            {
                if (newHeldItem != null)
                {
                    newHeldItem.EquipItem();
                }
                __instance.twoHanded = false;
                __instance.twoHandedAnimation = false;
                __instance.playerBodyAnimator.ResetTrigger("Throw");
                __instance.playerBodyAnimator.SetBool("Grab", true);
                if (!string.IsNullOrEmpty(newHeldItem?.itemProperties.grabAnim))
                {
                    __instance.playerBodyAnimator.SetBool(newHeldItem.itemProperties.grabAnim, true);
                }
                if (__instance.twoHandedAnimation != newHeldItem?.itemProperties.twoHandedAnimation)
                {
                    __instance.playerBodyAnimator.ResetTrigger("SwitchHoldAnimationTwoHanded");
                    __instance.playerBodyAnimator.SetTrigger("SwitchHoldAnimationTwoHanded");
                }
                __instance.playerBodyAnimator.ResetTrigger("SwitchHoldAnimation");
                __instance.playerBodyAnimator.SetTrigger("SwitchHoldAnimation");
                __instance.playerBodyAnimator.SetBool("GrabValidated", true);
                __instance.playerBodyAnimator.SetBool("cancelHolding", false);
                __instance.twoHandedAnimation = newHeldItem?.itemProperties.twoHandedAnimation ?? false;
                __instance.isHoldingObject = newHeldItem != null;
                __instance.currentlyHeldObjectServer = newHeldItem;
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(ScrollMouse_performed))]
        [HarmonyPostfix]
        private static void ScrollMouse_performed(PlayerControllerB __instance)
        {
            // If the code has just reset the timer to 0 (or very close to it), push it forward to decrease the time needed for the next check
            float desiredDelay = Math.Clamp(Plugin.ScrollDelay.Value, 0.05f, 0.3f);
            if ((float)TimeSinceSwitchingSlotsField.GetValue(__instance) < 0.1f)
            {
                TimeSinceSwitchingSlotsField.SetValue(__instance, 0.3f - desiredDelay);
            }
        }

        [HarmonyPatch(typeof(QuickMenuManager), nameof(OpenQuickMenu))]
        [HarmonyPrefix]
        private static bool OpenQuickMenu(QuickMenuManager __instance)
        {
            if (ShipBuildModeManager.Instance?.InBuildMode ?? false)
            {
                // Cancel out of build mode instead
                Plugin.MLS.LogInfo("Cancelling build mode and returning false!");
                ShipBuildModeManager.Instance.CancelBuildMode();
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(SetHoverTipAndCurrentInteractTrigger))]
        [HarmonyPostfix]
        private static void SetHoverTipAndCurrentInteractTrigger(PlayerControllerB __instance)
        {
            if (Plugin.ShowUIReticle.Value && AssetBundleHelper.Reticle != null && __instance.isPlayerControlled && !__instance.inTerminalMenu)
            {
                // Use our reticle and resize
                if (!__instance.cursorIcon.enabled)
                {
                    __instance.cursorIcon.sprite = AssetBundleHelper.Reticle;
                    __instance.cursorIcon.color = new Color(1, 1, 1, 0.1f);
                    __instance.cursorIcon.enabled = true;
                    __instance.cursorIcon.transform.localScale = Vector3.one * 0.05f;
                }
                else if (__instance.cursorIcon.transform.localScale.x < _originalCursorScale)
                {
                    // Make sure we reset/turn back off when needed
                    __instance.cursorIcon.transform.localScale = Vector3.one * _originalCursorScale;
                    __instance.cursorIcon.color = Color.white;
                    __instance.cursorIcon.sprite = __instance.hoveringOverTrigger?.hoverIcon ?? __instance.cursorIcon.sprite;

                    if (__instance.cursorIcon.sprite == AssetBundleHelper.Reticle)
                    {
                        __instance.cursorIcon.enabled = false;
                    }
                }
            }

            if (Plugin.AddHealthRechargeStation.Value && ItemHelper.MedStation != null && __instance.hoveringOverTrigger?.transform.parent == ItemHelper.MedStation.transform)
            {
                __instance.hoveringOverTrigger.interactable = __instance.health < ItemHelper.MedStation.MaxLocalPlayerHealth;
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(ShowNameBillboard))]
        [HarmonyPrefix]
        private static bool ShowNameBillboard()
        {
            // Do not show player names if we are hiding them, unless we are orbiting
            return !(Plugin.HidePlayerNames.Value && !(StartOfRound.Instance?.inShipPhase ?? true));
        }

        [HarmonyPatch(typeof(PlayerControllerB), "CalculateSmoothLookingInput")]
        [HarmonyPatch(typeof(PlayerControllerB), "CalculateNormalLookingInput")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PatchLookDownClamper(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            if (!Plugin.AllowLookDownMore.Value)
            {
                return instructions;
            }

            var codeList = new List<CodeInstruction>(instructions);
            for (int i = 0; i < codeList.Count; i++)
            {

                if (codeList[i].opcode == OpCodes.Ldc_R4 && int.TryParse(codeList[i].operand?.ToString(), out var operand) && operand == 60 // If we are putting the value of 60 on the stack,
                    && i + 1 < codeList.Count && codeList[i + 1].opcode == OpCodes.Call && (codeList[i + 1].operand as MethodInfo)?.Name == "Clamp") // and the next operation is Math.Clamp
                {
                    Plugin.MLS.LogDebug($"Updating look down angle to 85 in {original.Name}.");
                    codeList[i].operand = 85f;
                    break;
                }
            }

            return codeList.AsEnumerable();
        }

        private static void ShiftRightFromSlot(PlayerControllerB player, int slot)
        {
            // Double check the object is not two handed to prevent multiple calls to this from happening
            if (player.ItemSlots[slot] != null)
            {
                // If the slot to the right has an item, recursively move that to the right as well
                if (player.ItemSlots[slot + 1] != null)
                {
                    ShiftRightFromSlot(player, slot + 1);
                }

                ShiftSlots(player, slot + 1, slot);
            }
        }

        private static void ShiftSlots(PlayerControllerB player, int newSlot, int oldSlot)
        {
            Plugin.MLS.LogDebug($"Shifting slot {oldSlot} to {newSlot}");

            // Update the owner's UI
            if (player.IsOwner)
            {
                HUDManager.Instance.itemSlotIcons[newSlot].sprite = player.ItemSlots[oldSlot].itemProperties.itemIcon;
                HUDManager.Instance.itemSlotIcons[newSlot].enabled = true;
                HUDManager.Instance.itemSlotIcons[oldSlot].enabled = false;
            }

            // Move item
            player.ItemSlots[newSlot] = player.ItemSlots[oldSlot];
            player.ItemSlots[oldSlot] = null;
        }

        private static void SelectNewSlotLocal(PlayerControllerB player, int newSlot)
        {
            // This should only be used when an item has shifted, so we don't need any animations or grab code
            for (int i = 0; i < HUDManager.Instance.itemSlotIconFrames.Length; i++)
            {
                HUDManager.Instance.itemSlotIconFrames[i].GetComponent<Animator>().SetBool("selectedSlot", false);
            }
            HUDManager.Instance.itemSlotIconFrames[newSlot].GetComponent<Animator>().SetBool("selectedSlot", true);
            player.currentItemSlot = newSlot;
        }

        public static void DropAllItemsExceptHeld(PlayerControllerB player, bool onlyDropScrap)
        {
            int oldHeldSlot = -1;

            // Had to copy this from DropAllHeldItems() since vanilla code doesn't have a way to discard only a single item from a specific slot
            for (int i = 0; i < player.ItemSlots.Length; i++)
            {
                // If the checked item is held or not scrap (if we only drop scrap), skip this one
                bool isHeldItem = player.currentlyHeldObjectServer != null && player.ItemSlots[i] == player.currentlyHeldObjectServer && !onlyDropScrap;
                if (isHeldItem || (player.ItemSlots[i] != null && !player.ItemSlots[i].itemProperties.isScrap && onlyDropScrap))
                {
                    if (isHeldItem)
                    {
                        oldHeldSlot = i;
                    }
                    continue;
                }

                var grabbableObject = player.ItemSlots[i];
                if (grabbableObject != null)
                {
                    grabbableObject.parentObject = null;
                    grabbableObject.heldByPlayerOnServer = false;
                    if (player.isInElevator)
                    {
                        grabbableObject.transform.SetParent(player.playersManager.elevatorTransform, true);
                    }
                    else
                    {
                        grabbableObject.transform.SetParent(player.playersManager.propsContainer, true);
                    }
                    player.SetItemInElevator(player.isInHangarShipRoom, player.isInElevator, grabbableObject);
                    grabbableObject.EnablePhysics(true);
                    grabbableObject.EnableItemMeshes(true);
                    grabbableObject.transform.localScale = grabbableObject.originalScale;
                    grabbableObject.isHeld = false;
                    grabbableObject.isPocketed = false;
                    grabbableObject.startFallingPosition = grabbableObject.transform.parent.InverseTransformPoint(grabbableObject.transform.position);
                    grabbableObject.FallToGround(true);
                    grabbableObject.fallTime = UnityEngine.Random.Range(-0.3f, 0.05f);
                    if (player.IsOwner)
                    {
                        grabbableObject.DiscardItemOnClient();
                    }
                    else if (!grabbableObject.itemProperties.syncDiscardFunction)
                    {
                        grabbableObject.playerHeldBy = null;
                    }

                    player.ItemSlots[i] = null;
                    HUDManager.Instance.itemSlotIcons[i].enabled = false;
                }
            }

            // Shift whatever necessary and select the new slot
            ItemLeftSlot(player);
            for (int i = 0; i < player.ItemSlots.Length; i++)
            {
                if (oldHeldSlot > -1 && i < oldHeldSlot && player.ItemSlots[i] == player.currentlyHeldObjectServer)
                {
                    SelectNewSlotLocal(player, i);
                    break;
                }
            }

            if (player.IsOwner)
            {
                HUDManager.Instance.holdingTwoHandedItem.enabled = player.twoHanded;
            }
            player.carryWeight = 1 + player.ItemSlots.Sum(i => (i?.itemProperties.weight ?? 1) - 1);
        }
    }
}