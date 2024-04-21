using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class ShipTeleporterPatch
    {
        private static PlayerControllerB _lastPlayerTeleported;

        [HarmonyPatch(typeof(ShipTeleporter), "Awake")]
        [HarmonyPrefix]
        private static void Awake_Pre(ShipTeleporter __instance)
        {
            // Overwrite the cooldown values IF a non-vanilla value was specified
            if ((__instance.isInverseTeleporter && Plugin.InverseTeleporterCooldown.Value != (int)Plugin.InverseTeleporterCooldown.DefaultValue)
                || (!__instance.isInverseTeleporter && Plugin.RegularTeleporterCooldown.Value != (int)Plugin.RegularTeleporterCooldown.DefaultValue))
            {
                int regularCooldown = Math.Clamp(Plugin.RegularTeleporterCooldown.Value, 1, 300);
                int inverseCooldown = Math.Clamp(Plugin.InverseTeleporterCooldown.Value, 1, 300);
                __instance.cooldownAmount = __instance.isInverseTeleporter ? inverseCooldown : regularCooldown;
            }
        }

        [HarmonyPatch(typeof(ShipTeleporter), "Awake")]
        [HarmonyPostfix]
        private static void Awake_Post(ShipTeleporter __instance)
        {
            if (__instance.isInverseTeleporter)
            {
                __instance.GetComponentInChildren<InteractTrigger>().hoverTip = "Beam out : [E]";
            }
        }

        [HarmonyPatch(typeof(ShipTeleporter), "beamUpPlayer", MethodType.Enumerator)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PatchRegularTeleporter(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();
            string itemsToKeep = Plugin.KeepItemsDuringTeleport.Value.ToLower();

            // Find the code that teleports dead bodies
            if (codeList[203].opcode == OpCodes.Ldfld && codeList[203].operand as FieldInfo == typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.deadBody))
                && codeList[208].opcode == OpCodes.Callvirt && codeList[208].operand as MethodInfo == typeof(Transform).GetMethod(nameof(Transform.SetParent), new[] { typeof(Transform), typeof(bool) }))
            {
                var collectBodyDelegate = Transpilers.EmitDelegate<Action>(() =>
                {
                    var deadBodyObj = StartOfRound.Instance?.mapScreen?.targetedPlayer?.deadBody?.grabBodyObject;
                    if (deadBodyObj != null)
                    {
                        StartOfRound.Instance.mapScreen.targetedPlayer.SetItemInElevator(true, true, deadBodyObj);
                    }
                });

                codeList.Insert(209, collectBodyDelegate);
                Plugin.MLS.LogDebug("Patched beamUpPlayer to drop dead bodies properly.");
            }
            else
            {
                Plugin.MLS.LogError("Unexpected code - Could not transpile ShipTeleporter.beamUpPlayer to fix dead body collection!");
            }

            // Find the code that drops held items otherwise (if needed)
            if (itemsToKeep != "none")
            {
                if (codeList[246].opcode == OpCodes.Callvirt && codeList[246].operand as MethodInfo == typeof(PlayerControllerB).GetMethod(nameof(PlayerControllerB.DropAllHeldItems)))
                {
                    if (itemsToKeep == "held" || itemsToKeep == "nonscrap")
                    {
                        var dropAllExceptHeldDelegate = Transpilers.EmitDelegate<Action<PlayerControllerB>>((player) =>
                        {
                            PlayerControllerBPatch.DropAllItemsExceptHeld(player, itemsToKeep == "nonscrap");
                        });

                        // Replace the drop function with our own
                        codeList[246] = dropAllExceptHeldDelegate;

                        // Remove the two bools (no longer needed) from the stack load code
                        codeList.RemoveRange(244, 2);
                    }
                    else
                    {
                        // Remove the 5 lines of code that call the drop function
                        codeList.RemoveRange(242, 5);
                    }

                    Plugin.MLS.LogDebug($"Patched beamUpPlayer to keep {itemsToKeep} items.");
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected code - Could not transpile ShipTeleporter.beamUpPlayer to keep items!");
                }
            }

            return codeList.AsEnumerable();
        }

        [HarmonyPatch(typeof(ShipTeleporter), "TeleportPlayerOutWithInverseTeleporter")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PatchInverseTeleporter(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();
            string itemsToKeep = Plugin.KeepItemsDuringInverse.Value.ToLower();

            if (itemsToKeep != "none")
            {
                for (int i = 5; i < codeList.Count; i++)
                {
                    if (codeList[i].opcode == OpCodes.Callvirt && (codeList[i].operand as MethodInfo)?.Name == nameof(PlayerControllerB.DropAllHeldItems))
                    {
                        if (itemsToKeep == "held" || itemsToKeep == "nonscrap")
                        {
                            var dropAllExceptHeldDelegate = Transpilers.EmitDelegate<Action<PlayerControllerB>>((player) =>
                            {
                                PlayerControllerBPatch.DropAllItemsExceptHeld(player, itemsToKeep == "nonscrap");
                            });

                            // Replace the function call with our own
                            codeList[i] = dropAllExceptHeldDelegate;

                            // Remove the two bools (no longer needed) from the stack load code
                            codeList.RemoveRange(i - 2, 2);
                        }
                        else
                        {
                            // Remove the 3 lines of code that call the drop function
                            codeList.RemoveRange(i - 3, 4);
                        }
                    }
                }

                Plugin.MLS.LogDebug($"Patched TeleportPlayerOutWithInverseTeleporter to keep {itemsToKeep} items.");
            }

            return codeList.AsEnumerable();
        }
    }
}