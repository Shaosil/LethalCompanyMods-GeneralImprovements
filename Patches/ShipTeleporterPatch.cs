using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using static GeneralImprovements.Plugin.Enums;

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

            // Fix wording of glass lid
            var buttonGlass = __instance.transform.Find("ButtonContainer/ButtonAnimContainer/ButtonGlass")?.GetComponent<InteractTrigger>();
            if (buttonGlass != null && __instance.buttonAnimator != null)
            {
                buttonGlass.onInteract.AddListener(p =>
                {
                    bool isOpen = __instance.buttonAnimator.GetBool("GlassOpen");
                    buttonGlass.hoverTip = $"{(isOpen ? "Shut" : "Lift")} glass : [LMB]";
                });
            }
        }

        [HarmonyPatch(typeof(ShipTeleporter), "beamUpPlayer", MethodType.Enumerator)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PatchRegularTeleporter(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            // Find the code that teleports dead bodies
            if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
            {
                i => i.LoadsField(typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.deadBody))),
                i => i.Calls(typeof(Component).GetMethod("get_transform")),
                i => i.Calls(typeof(StartOfRound).GetMethod("get_Instance")),
                i => i.LoadsField(typeof(StartOfRound).GetField(nameof(StartOfRound.elevatorTransform))),
                i => i.LoadsConstant(1),
                i => i.Calls(typeof(Transform).GetMethod(nameof(Transform.SetParent), new[] { typeof(Transform), typeof(bool) }))
            }, out var deadBody))
            {
                var collectBodyDelegate = Transpilers.EmitDelegate<Action>(() =>
                {
                    var deadBodyObj = StartOfRound.Instance?.mapScreen?.targetedPlayer?.deadBody?.grabBodyObject;
                    if (deadBodyObj != null)
                    {
                        StartOfRound.Instance.mapScreen.targetedPlayer.SetItemInElevator(true, true, deadBodyObj);
                    }
                });

                codeList.Insert(deadBody.Last().Index + 1, collectBodyDelegate);
                Plugin.MLS.LogDebug("Patched beamUpPlayer to drop dead bodies properly.");
            }
            else
            {
                Plugin.MLS.LogError("Unexpected code - Could not transpile ShipTeleporter.beamUpPlayer to fix dead body collection!");
            }

            // Find the code that drops held items otherwise (if needed)
            if (Plugin.KeepItemsDuringTeleport.Value != eItemsToKeep.None)
            {
                if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo fi && fi.Name.Contains("playerToBeamUp"),
                    i => i.LoadsConstant(1),
                    i => i.LoadsConstant(0),
                    i => i.Calls(typeof(PlayerControllerB).GetMethod(nameof(PlayerControllerB.DropAllHeldItems)))
                }, out var dropItems))
                {
                    if (Plugin.KeepItemsDuringTeleport.Value == eItemsToKeep.Held || Plugin.KeepItemsDuringTeleport.Value == eItemsToKeep.NonScrap)
                    {
                        var dropAllExceptHeldDelegate = Transpilers.EmitDelegate<Action<PlayerControllerB>>((player) =>
                        {
                            PlayerControllerBPatch.DropAllItemsExceptHeld(player, Plugin.KeepItemsDuringTeleport.Value == eItemsToKeep.NonScrap);
                        });

                        // Replace the drop function with our own
                        codeList[dropItems.Last().Index] = dropAllExceptHeldDelegate;

                        // Remove the two bools (no longer needed) from the stack load code
                        codeList[dropItems[1].Index].opcode = OpCodes.Nop;
                        codeList[dropItems[2].Index].opcode = OpCodes.Nop;
                    }
                    else
                    {
                        // Remove the 5 lines of code that call the drop function
                        for (int i = 0; i < 5; i++)
                        {
                            codeList[(dropItems.First().Index - 1) + i].opcode = OpCodes.Nop;
                        }
                    }

                    Plugin.MLS.LogDebug($"Patched beamUpPlayer to keep {Plugin.KeepItemsDuringTeleport.Value} items.");
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected code - Could not transpile ShipTeleporter.beamUpPlayer to keep items!");
                }
            }

            return codeList;
        }

        [HarmonyPatch(typeof(ShipTeleporter), "TeleportPlayerOutWithInverseTeleporter")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PatchInverseTeleporter(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.KeepItemsDuringTeleport.Value != eItemsToKeep.None)
            {
                if (codeList.TryFindInstruction(i => i.Calls(typeof(PlayerControllerB).GetMethod(nameof(PlayerControllerB.DropAllHeldItems))), out var found))
                {
                    if (Plugin.KeepItemsDuringTeleport.Value == eItemsToKeep.Held || Plugin.KeepItemsDuringTeleport.Value == eItemsToKeep.NonScrap)
                    {
                        var dropAllExceptHeldDelegate = Transpilers.EmitDelegate<Action<PlayerControllerB>>((player) =>
                        {
                            PlayerControllerBPatch.DropAllItemsExceptHeld(player, Plugin.KeepItemsDuringTeleport.Value == eItemsToKeep.NonScrap);
                        });

                        // Replace the function call with our own
                        codeList[found.Index] = dropAllExceptHeldDelegate;

                        // Remove the two bools (no longer needed) from the stack load code
                        codeList[found.Index - 2].opcode = OpCodes.Nop;
                        codeList[found.Index - 1].opcode = OpCodes.Nop;
                    }
                    else
                    {
                        // Remove the 4 lines of code that call the drop function
                        for (int i = 0; i < 4; i++)
                        {
                            codeList[(found.Index - 3) + i].opcode = OpCodes.Nop;
                        }
                    }
                }

                Plugin.MLS.LogDebug($"Patched TeleportPlayerOutWithInverseTeleporter to keep {Plugin.KeepItemsDuringTeleport.Value} items.");
            }

            return codeList;
        }
    }
}