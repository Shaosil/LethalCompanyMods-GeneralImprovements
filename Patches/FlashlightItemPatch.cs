using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace GeneralImprovements.Patches
{
    internal static class FlashlightItemPatch
    {
        public const int LaserPointerTypeID = 2; // See BBFlashlight.prefab, FlashlightItem.prefab, and LaserPointer.prefab

        [HarmonyPatch(typeof(FlashlightItem), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(FlashlightItem __instance)
        {
            // Make sure the correct material is displayed for the lamp part
            if (__instance.changeMaterial)
            {
                var materials = __instance.flashlightMesh.sharedMaterials;
                materials[1] = __instance.isBeingUsed ? __instance.bulbLight : __instance.bulbDark;
                __instance.flashlightMesh.sharedMaterials = materials;
            }
        }

        [HarmonyPatch(typeof(FlashlightItem), "EquipItem")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PatchEquipFlashlight(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (instructions.TryFindInstruction(i => i.Calls(typeof(PlayerControllerB).GetMethod(nameof(PlayerControllerB.ChangeHelmetLight))), out var helmet)
                && instructions.TryFindInstruction(i => i.Calls(typeof(FlashlightItem).GetMethod(nameof(FlashlightItem.SwitchFlashlight))), out var switchLight))
            {
                // Remove both lines that affect the player's helmet light
                for (int i = 0; i < 11; i++)
                {
                    codeList[(helmet.Index - 5) + i] = new CodeInstruction(OpCodes.Nop);
                }

                // Remove the call to switch flashlight (moving it to the postfix)
                for (int i = 0; i < 6; i++)
                {
                    codeList[(switchLight.Index - 5) + i] = new CodeInstruction(OpCodes.Nop);
                }

                Plugin.MLS.LogDebug("Patched FlashlightItem.EquipItem");
            }
            else
            {
                Plugin.MLS.LogError("Unexpected code found - Could not patch FlashlightItem.EquipItem!");
            }

            return codeList;
        }

        [HarmonyPatch(typeof(FlashlightItem), nameof(EquipItem))]
        [HarmonyPostfix]
        private static void EquipItem(FlashlightItem __instance)
        {
            // Move the switch to POST equip so we are aware of the pocketed field being set correctly
            if (__instance.isBeingUsed)
            {
                __instance.SwitchFlashlight(true);
            }
        }

        [HarmonyPatch(typeof(FlashlightItem), nameof(DiscardItem))]
        [HarmonyPostfix]
        private static void DiscardItem(FlashlightItem __instance, PlayerControllerB ___previousPlayerHeldBy)
        {
            if (!Plugin.OnlyAllowOneActiveFlashlight.Value)
            {
                return;
            }

            // If there is an active flashlight in the players inventory (prioritize non lasers), turn on its helemet light. This can happen if a helmet light was on last frame.
            var otherFlashlights = ___previousPlayerHeldBy.ItemSlots.OfType<FlashlightItem>().Where(f => f != __instance);
            var activeFlashlight = otherFlashlights.Where(f => f.isBeingUsed).OrderBy(f => f.CheckForLaser()).FirstOrDefault();

            if (activeFlashlight != null)
            {
                Plugin.MLS.LogDebug("Turning active helmet light back on after a flashlight was discarded");
                activeFlashlight.playerHeldBy.ChangeHelmetLight(activeFlashlight.flashlightTypeID, true);
            }
            else if (__instance.isBeingUsed && ___previousPlayerHeldBy.IsOwner)
            {
                // Otherwise, if we have ANY charged flashlights (not lasers) in our inventory, turn one on
                var otherFlashlight = otherFlashlights.FirstOrDefault(f => !f.CheckForLaser() && !f.insertedBattery.empty);
                if (otherFlashlight != null)
                {
                    Plugin.MLS.LogDebug("Turning another flashlight on after an active one was discarded");
                    otherFlashlight.UseItemOnClient();
                }
            }
        }

        [HarmonyPatch(typeof(FlashlightItem), nameof(SwitchFlashlight))]
        [HarmonyPrefix]
        private static bool SwitchFlashlight(FlashlightItem __instance, bool on)
        {
            __instance.isBeingUsed = on;
            __instance.flashlightBulb.enabled = on && !__instance.isPocketed;
            __instance.flashlightBulbGlow.enabled = on && !__instance.isPocketed;

            // Copied and optimized from vanilla method
            if (__instance.changeMaterial)
            {
                __instance.flashlightMesh.materials[1] = on ? __instance.bulbLight : __instance.bulbDark;
            }

            if (__instance.playerHeldBy != null)
            {
                if (on)
                {
                    // Make sure no other flashlights in our inventory are on
                    if (__instance.IsOwner && Plugin.OnlyAllowOneActiveFlashlight.Value && !__instance.CheckForLaser())
                    {
                        for (int i = 0; i < __instance.playerHeldBy.ItemSlots.Length; i++)
                        {
                            if (__instance.playerHeldBy.ItemSlots[i] is FlashlightItem otherFlashlight && otherFlashlight != __instance && otherFlashlight.isBeingUsed && !otherFlashlight.CheckForLaser())
                            {
                                Plugin.MLS.LogDebug($"Flashlight in pocket slot {i} TURNING OFF after turning on a held flashlight");
                                otherFlashlight.UseItemOnClient();
                            }
                        }
                    }
                }

                // Always ensure helmet light is up to date
                PlayerControllerBPatch.UpdateHelmetLight(__instance.playerHeldBy);
            }

            // Never call the original method
            return false;
        }

        public static bool CheckForLaser(this FlashlightItem flashlight)
        {
            // Return true if this is a laser pointer IF we are not treating them as flashlights
            return flashlight.flashlightTypeID == LaserPointerTypeID && !Plugin.TreatLasersAsFlashlights.Value;
        }
    }
}