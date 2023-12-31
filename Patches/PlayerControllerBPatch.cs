using GameNetcodeStuff;
using HarmonyLib;
using System.Reflection;

namespace GeneralImprovements.Patches
{
    internal static class PlayerControllerBPatch
    {
        private static MethodInfo _switchToSlotMethod = null;
        private static MethodInfo SwitchToSlotMethod
        {
            get
            {
                // Lazy load and cache the reflection info
                if (_switchToSlotMethod == null)
                {
                    _switchToSlotMethod = typeof(PlayerControllerB).GetMethod("SwitchToItemSlot", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return _switchToSlotMethod;
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

        [HarmonyPatch(typeof(PlayerControllerB), "PlaceObjectClientRpc")]
        [HarmonyPatch(typeof(PlayerControllerB), "ThrowObjectClientRpc")]
        [HarmonyPatch(typeof(PlayerControllerB), "DespawnHeldObjectOnClient")]
        [HarmonyPatch(typeof(PlayerControllerB), "DestroyItemInSlotClientRpc")]
        [HarmonyPostfix]
        private static void RearrangeItems(PlayerControllerB __instance)
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
                            // Update the owner's UI
                            if (__instance.IsOwner)
                            {
                                HUDManager.Instance.itemSlotIcons[i].sprite = __instance.ItemSlots[j].itemProperties.itemIcon;
                                HUDManager.Instance.itemSlotIcons[i].enabled = true;
                                HUDManager.Instance.itemSlotIcons[j].enabled = false;
                            }

                            // Move item and continue
                            __instance.ItemSlots[i] = __instance.ItemSlots[j];
                            __instance.ItemSlots[j] = null;

                            break;
                        }
                    }
                }
            }

            // Refresh the current item slot if the player is holding something new
            if (__instance.ItemSlots[__instance.currentItemSlot] != null)
            {
                SwitchToSlotMethod.Invoke(__instance, new object[] { __instance.currentItemSlot, null });
            }
        }
    }
}