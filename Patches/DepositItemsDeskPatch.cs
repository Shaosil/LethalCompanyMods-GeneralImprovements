using GeneralImprovements.Utilities;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GeneralImprovements.Patches
{
    internal static class DepositItemsDeskPatch
    {
        [HarmonyPatch(typeof(DepositItemsDesk), nameof(PlaceItemOnCounter))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PlaceItemOnCounter(IEnumerable<CodeInstruction> instructions)
        {
            if (instructions.TryFindInstructions(new Func<CodeInstruction, bool>[]
            {
                i => i.opcode == OpCodes.Ldlen,
                i => i.opcode == OpCodes.Conv_I4,
                i => i.LoadsConstant(12)
            }, out var foundInstructions))
            {
                Plugin.MLS.LogDebug($"Updating sell counter item limit to {Plugin.SellCounterItemLimit.Value}.");
                foundInstructions[2].Instruction.operand = Plugin.SellCounterItemLimit.Value;
            }
            else
            {
                Plugin.MLS.LogError("Undexpected IL code - Could not transpile PlaceItemOnCounter!");
            }

            return instructions;
        }

        [HarmonyPatch(typeof(DepositItemsDesk), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(DepositItemsDesk __instance)
        {
            __instance.GetComponentInChildren<InteractTrigger>().cooldownTime = 0.1f;
        }
    }
}