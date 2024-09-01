using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using GeneralImprovements.Utilities;
using HarmonyLib;
using Unity.Netcode;

namespace GeneralImprovements.Patches
{
    internal static class DepositItemsDeskPatch
    {
        public static int NumItemsSoldToday = 0;

        [HarmonyPatch(typeof(DepositItemsDesk), nameof(DepositItemsDesk.PlaceItemOnCounter))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PlaceItemOnCounter_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            if (instructions.TryFindInstructions(new Func<CodeInstruction, bool>[]
            {
                i => i.opcode == OpCodes.Ldlen,
                i => i.opcode == OpCodes.Conv_I4,
                i => i.LoadsConstant(12)
            }, out var foundInstructions))
            {
                Plugin.MLS.LogDebug($"Updating sell counter item limit to {Plugin.SellCounterItemLimit.Value}.");
                foundInstructions[2].Instruction.opcode = OpCodes.Ldc_I4;
                foundInstructions[2].Instruction.operand = Plugin.SellCounterItemLimit.Value;
            }
            else
            {
                Plugin.MLS.LogError("Undexpected IL code - Could not transpile PlaceItemOnCounter!");
            }

            return instructions;
        }

        [HarmonyPatch(typeof(DepositItemsDesk), nameof(DepositItemsDesk.AddObjectToDeskClientRpc))]
        [HarmonyPostfix]
        private static void AddObjectToDeskClientRpc(NetworkObjectReference grabbableObjectNetObject)
        {
            // Update layer so we do not detect them anymore.
            if (grabbableObjectNetObject.TryGet(out var netObj) && netObj.GetComponentInChildren<GrabbableObject>() is GrabbableObject obj)
            {
                obj.gameObject.layer = 0;
            }
        }

        [HarmonyPatch(typeof(DepositItemsDesk), nameof(SellAndDisplayItemProfits))]
        [HarmonyPostfix]
        private static void SellAndDisplayItemProfits(DepositItemsDesk __instance)
        {
            var items = __instance.deskObjectsContainer.GetComponentsInChildren<GrabbableObject>();
            NumItemsSoldToday += items.Length;

            MonitorsHelper.UpdateCompanyBuyRateMonitors();
        }

        [HarmonyPatch(typeof(DepositItemsDesk), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(DepositItemsDesk __instance)
        {
            __instance.GetComponentInChildren<InteractTrigger>().cooldownTime = 0.1f;
        }
    }
}