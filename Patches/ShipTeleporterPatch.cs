using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameNetcodeStuff;
using GeneralImprovements.Items;
using GeneralImprovements.Utilities;
using HarmonyLib;
using UnityEngine;
using static GeneralImprovements.Enums;

namespace GeneralImprovements.Patches
{
    internal static class ShipTeleporterPatch
    {
        private static Dictionary<ShipTeleporter, InteractTrigger> _buttonGlasses = new Dictionary<ShipTeleporter, InteractTrigger>();

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

            var buttonGlass = __instance.transform.Find("ButtonContainer/ButtonAnimContainer/ButtonGlass")?.GetComponent<InteractTrigger>();
            if (buttonGlass != null)
            {
                _buttonGlasses[__instance] = buttonGlass;
            }
        }

        [HarmonyPatch(typeof(ShipTeleporter), "beamUpPlayer", MethodType.Enumerator)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> beamUpPlayer_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            // Insert a call to check radar boosters if needed
            if (new[] { eRadarBoosterTeleport.OnlyRegular, eRadarBoosterTeleport.RegularAndInverse }.Contains(Plugin.RadarBoostersCanBeTeleported.Value))
            {
                if (codeList.TryFindInstruction(i => i.opcode == OpCodes.Stloc_1, out var found))
                {
                    Plugin.MLS.LogDebug("Patching ShipTeleporter.beamUpPlayer to include radar boosters.");
                    codeList.InsertRange(found.Index + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_1),
                        Transpilers.EmitDelegate<Action<ShipTeleporter>>(t =>
                        {
                            // Only the server should broadcast the check for each teleporter
                            if (StartOfRound.Instance.IsServer) t.StartCoroutine(CheckCanBeamUpRadarBooster(t));
                        })
                    });
                }
                else
                {
                    Plugin.MLS.LogWarning("Could not find a SINGLE STLOC.1 instruction!? Could not patch ShipTeleporter.beamUpPlayer to include radar boosters.");
                }
            }

            // Find the code that teleports dead bodies
            if (Plugin.AutomaticallyCollectTeleportedCorpses.Value)
            {
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
                        if (deadBodyObj != null && !deadBodyObj.isInShipRoom)
                        {
                            StartOfRound.Instance.mapScreen.targetedPlayer.SetItemInElevator(true, true, deadBodyObj);
                        }
                    });

                    Plugin.MLS.LogDebug("Patching ShipTeleporter.beamUpPlayer to drop dead bodies properly.");
                    codeList.Insert(deadBody.Last().Index + 1, collectBodyDelegate);
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected code - Could not transpile ShipTeleporter.beamUpPlayer to fix dead body collection!");
                }
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
                    Plugin.MLS.LogDebug($"Patching ShipTeleporter.beamUpPlayer to keep {Plugin.KeepItemsDuringTeleport.Value} items.");

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
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected code - Could not transpile ShipTeleporter.beamUpPlayer to keep items!");
                }
            }

            return codeList;
        }

        [HarmonyPatch(typeof(ShipTeleporter), "beamOutPlayer", MethodType.Enumerator)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> beamOutPlayer_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            // Insert a call to check radar boosters if needed
            if (new[] { eRadarBoosterTeleport.OnlyInverse, eRadarBoosterTeleport.RegularAndInverse }.Contains(Plugin.RadarBoostersCanBeTeleported.Value))
            {
                if (codeList.TryFindInstruction(i => i.opcode == OpCodes.Stloc_1, out var found))
                {
                    Plugin.MLS.LogDebug("Patching ShipTeleporter.beamOutPlayer to include radar boosters.");
                    codeList.InsertRange(found.Index + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldloc_1),
                        Transpilers.EmitDelegate<Action<ShipTeleporter>>(t =>
                        {
                            // Only the server should broadcast the check for each teleporter
                            if (StartOfRound.Instance.IsServer) t.StartCoroutine(CheckCanBeamOutRadarBoosters(t));
                        })
                    });
                }
                else
                {
                    Plugin.MLS.LogWarning("Could not find a SINGLE STLOC.1 instruction!? Could not patch ShipTeleporter.beamOutPlayer to include radar boosters.");
                }
            }

            return codeList;
        }

        [HarmonyPatch(typeof(ShipTeleporter), "TeleportPlayerOutWithInverseTeleporter")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PatchInverseTeleporter(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.KeepItemsDuringInverse.Value != eItemsToKeep.None)
            {
                if (codeList.TryFindInstruction(i => i.Calls(typeof(PlayerControllerB).GetMethod(nameof(PlayerControllerB.DropAllHeldItems))), out var found))
                {
                    Plugin.MLS.LogDebug($"Patching ShipTeleporter.TeleportPlayerOutWithInverseTeleporter to keep {Plugin.KeepItemsDuringTeleport.Value} items.");

                    if (Plugin.KeepItemsDuringInverse.Value == eItemsToKeep.Held || Plugin.KeepItemsDuringInverse.Value == eItemsToKeep.NonScrap)
                    {
                        var dropAllExceptHeldDelegate = Transpilers.EmitDelegate<Action<PlayerControllerB>>((player) =>
                        {
                            PlayerControllerBPatch.DropAllItemsExceptHeld(player, Plugin.KeepItemsDuringInverse.Value == eItemsToKeep.NonScrap);
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
            }

            return codeList;
        }

        [HarmonyPatch(typeof(ShipTeleporter), nameof(Update))]
        [HarmonyPostfix]
        private static void Update(ShipTeleporter __instance)
        {
            // Fix wording of glass lid
            if (_buttonGlasses.ContainsKey(__instance) && __instance.buttonAnimator != null)
            {
                var buttonGlass = _buttonGlasses[__instance];
                bool tipSaysLift = buttonGlass.hoverTip.Contains("Lift");
                bool isOpen = __instance.buttonAnimator.GetBool("GlassOpen");
                if (tipSaysLift == isOpen)
                {
                    buttonGlass.hoverTip = $"{(isOpen ? "Shut" : "Lift")} glass : [LMB]";
                }
            }
        }

        private static IEnumerator CheckCanBeamUpRadarBooster(ShipTeleporter teleporter)
        {
            TeleportableRadarBooster helper = null;
            Func<bool> validRadarOnMapScreen = () =>
            {
                var map = StartOfRound.Instance.mapScreen;
                var mapTarget = map.radarTargets.ElementAtOrDefault(map.targetTransformIndex);
                if ((mapTarget?.isNonPlayer ?? false) && mapTarget.transform.TryGetComponent<RadarBoosterItem>(out var radar) && radar.playerHeldBy == null)
                {
                    radar.TryGetComponent(out helper);
                }

                return helper != null;
            };

            // If the current map target is an unheld radar booster, play its beam up particle system if found
            if (validRadarOnMapScreen())
            {
                Plugin.MLS.LogDebug("Server sending radar booster beam effects RPC");
                helper.PlayBeamEffectsClientRpc(true);
            }
            else
            {
                yield break;
            }

            // Wait 3 seconds to copy vanilla teleporter delays
            yield return new WaitForSeconds(3);

            // If the current map target is STILL an unheld radar booster, beam it into the ship the same way a player would be
            if (validRadarOnMapScreen())
            {
                Plugin.MLS.LogDebug("Server sending radar booster regular teleport RPC");
                helper.TeleportRadarBoosterClientRpc(teleporter.NetworkObject, teleporter.transform.position, true);
            }
        }

        private static IEnumerator CheckCanBeamOutRadarBoosters(ShipTeleporter teleporter)
        {
            List<TeleportableRadarBooster> helpers = null;
            Func<bool> radarsNearby = () =>
            {
                helpers = new List<TeleportableRadarBooster>();
                var hitProps = Physics.OverlapSphere(teleporter.transform.position, 2, 64);
                foreach (var collider in hitProps)
                {
                    if (collider.TryGetComponent<RadarBoosterItem>(out var radar) && radar.playerHeldBy == null && radar.TryGetComponent<TeleportableRadarBooster>(out var helper))
                    {
                        helpers.Add(helper);
                    }
                }

                return helpers.Count > 0;
            };

            // Find all nearby radar boosters not held by players and play beam effets on them
            if (radarsNearby())
            {
                foreach (var helper in helpers)
                {
                    Plugin.MLS.LogDebug("Server sending radar booster beam effects RPC");
                    helper.PlayBeamEffectsClientRpc(false);
                }
            }

            // Wait 5 seconds to copy vanilla teleporter delays
            yield return new WaitForSeconds(5);

            // If we can still use the inverse teleporter, find all nearby radar boosters and teleport them all in
            if (radarsNearby())
            {
                foreach (var helper in helpers)
                {
                    Plugin.MLS.LogDebug("Server sending radar booster inverse teleport RPC");
                    helper.BeamOutClientRpc(teleporter.NetworkObject, UnityEngine.Random.Range(0, int.MaxValue)); // Server sets the random seed
                }
            }
        }
    }
}