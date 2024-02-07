using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace GeneralImprovements.Patches
{
    internal static class ShipTeleporterPatch
    {
        private static PlayerControllerB _lastPlayerTeleported;

        [HarmonyPatch(typeof(ShipTeleporter), "Awake")]
        [HarmonyPrefix]
        private static void Awake_Pre(ShipTeleporter __instance)
        {
            // Overwrite the cooldown values
            int regularCooldown = Math.Clamp(Plugin.RegularTeleporterCooldown.Value, 0, 300);
            int inverseCooldown = Math.Clamp(Plugin.InverseTeleporterCooldown.Value, 0, 300);
            __instance.cooldownAmount = __instance.isInverseTeleporter ? inverseCooldown : regularCooldown;
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

            for (int i = 5; i < codeList.Count; i++)
            {
                if (codeList[i].opcode == OpCodes.Callvirt && (codeList[i].operand as MethodInfo)?.Name == nameof(PlayerControllerB.DropAllHeldItems))
                {
                    // Patch the dead body dropping
                    if (codeList[i - 5].opcode == OpCodes.Ldfld && (codeList[i - 5].operand as FieldInfo)?.Name == nameof(PlayerControllerB.deadBody))
                    {
                        var collectBodyDelegate = Transpilers.EmitDelegate<Action>(() =>
                        {
                            var deadBodyObj = StartOfRound.Instance?.mapScreen?.targetedPlayer?.deadBody?.grabBodyObject;
                            if (deadBodyObj != null)
                            {
                                StartOfRound.Instance.mapScreen.targetedPlayer.SetItemInElevator(true, true, deadBodyObj);
                            }
                        });

                        codeList.Insert(i + 1, collectBodyDelegate);
                    }
                    else if (itemsToKeep != "none")
                    {
                        if (itemsToKeep == "held")
                        {
                            var dropAllExceptHeldDelegate = Transpilers.EmitDelegate<Action<PlayerControllerB>>((player) =>
                            {
                                PlayerControllerBPatch.DropAllItemsExceptHeld(player);
                            });

                            // Replace the drop function with our own
                            codeList[i] = dropAllExceptHeldDelegate;

                            // Remove the two bools (no longer needed) from the stack load code
                            codeList.RemoveRange(i - 2, 2);
                        }
                        else
                        {
                            // Remove the 4 lines of code that call this function
                            codeList.RemoveRange(i - 4, 5);
                        }
                    }
                }
            }

            Plugin.MLS.LogDebug($"Patched beamUpPlayer to drop dead bodies properly{(itemsToKeep != "none" ? $" and keep {itemsToKeep} items" : string.Empty)}.");

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
                        if (itemsToKeep == "held")
                        {
                            var dropAllExceptHeldDelegate = Transpilers.EmitDelegate<Action<PlayerControllerB>>((player) =>
                            {
                                PlayerControllerBPatch.DropAllItemsExceptHeld(player);
                            });

                            // Replace the function call with our own
                            codeList[i] = dropAllExceptHeldDelegate;

                            // Remove the two bools (no longer needed) from the stack load code
                            codeList.RemoveRange(i - 2, 2);
                        }
                        else
                        {
                            // Remove the 3 lines of code that call this function
                            codeList.RemoveRange(i - 3, 4);
                        }
                    }
                }
            }

            Plugin.MLS.LogDebug($"Patched TeleportPlayerOutWithInverseTeleporter to keep {itemsToKeep} items.");

            return codeList.AsEnumerable();
        }
    }
}