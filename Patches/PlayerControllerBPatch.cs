using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Reflection;
using Unity.Netcode;

namespace GeneralImprovements.Patches
{
    internal static class PlayerControllerBPatch
    {
        private static FieldInfo _timeSinceSwitchingSlotsField = null;
        internal static FieldInfo TimeSinceSwitchingSlotsField
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


        [HarmonyPatch(typeof(PlayerControllerB), nameof(FirstEmptyItemSlot))]
        [HarmonyPrefix]
        private static bool FirstEmptyItemSlot(PlayerControllerB __instance, ref int __result)
        {
            if (!Plugin.PickupInOrder.Value)
            {
                // If not configured to pickup in order, call the original method instead
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
            if (!grabbableObject.itemProperties.twoHanded)
            {
                return;
            }

            Plugin.MLS.LogDebug($"Two handed item being grabbed!");

            // Move things over
            ShiftRightFromSlot(__instance, 0);
        }

        [HarmonyPatch(typeof(PlayerControllerB), "PlaceObjectClientRpc")]
        [HarmonyPatch(typeof(PlayerControllerB), "ThrowObjectClientRpc")]
        [HarmonyPatch(typeof(PlayerControllerB), "DespawnHeldObjectOnClient")]
        [HarmonyPatch(typeof(PlayerControllerB), "DestroyItemInSlotClientRpc")]
        [HarmonyPostfix]
        private static void ItemLeftSlot(PlayerControllerB __instance)
        {
            if (!Plugin.RearrangeOnDrop.Value)
            {
                return;
            }

            for (int i = __instance.currentItemSlot; i < __instance.ItemSlots.Length - 1; i++)
            {
                // Each time we find an empty slot, move the first found item after this slot to this one
                if (__instance.ItemSlots[i] == null)
                {
                    for (int j = i + 1; j < __instance.ItemSlots.Length; j++)
                    {
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
            if (newHeldItem != null)
            {
                newHeldItem.EquipItem();
                __instance.twoHanded = false;
                __instance.twoHandedAnimation = false;
                __instance.playerBodyAnimator.ResetTrigger("Throw");
                __instance.playerBodyAnimator.SetBool("Grab", true);
                if (!string.IsNullOrEmpty(newHeldItem.itemProperties.grabAnim))
                {
                    __instance.playerBodyAnimator.SetBool(newHeldItem.itemProperties.grabAnim, true);
                }
                if (__instance.twoHandedAnimation != newHeldItem.itemProperties.twoHandedAnimation)
                {
                    __instance.playerBodyAnimator.ResetTrigger("SwitchHoldAnimationTwoHanded");
                    __instance.playerBodyAnimator.SetTrigger("SwitchHoldAnimationTwoHanded");
                }
                __instance.playerBodyAnimator.ResetTrigger("SwitchHoldAnimation");
                __instance.playerBodyAnimator.SetTrigger("SwitchHoldAnimation");
                __instance.playerBodyAnimator.SetBool("GrabValidated", true);
                __instance.playerBodyAnimator.SetBool("cancelHolding", false);
                __instance.twoHandedAnimation = newHeldItem.itemProperties.twoHandedAnimation;
                __instance.isHoldingObject = true;
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
    }
}