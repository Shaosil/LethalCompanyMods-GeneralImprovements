using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace GeneralImprovements.Patches
{
    internal static class DepositItemsDeskPatch
    {
        [HarmonyPatch(typeof(DepositItemsDesk), nameof(PlaceItemOnCounter))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PlaceItemOnCounter(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();
            for (int i = 2; i < codeList.Count; i++)
            {
                if (codeList[i].Is(OpCodes.Ldc_I4_S, 12) && codeList[i - 2].opcode == OpCodes.Ldlen)
                {
                    Plugin.MLS.LogDebug($"Updating sell counter item limit to {Plugin.SellCounterItemLimit.Value}.");
                    codeList[i].operand = Plugin.SellCounterItemLimit.Value;
                    break;
                }
            }

            return codeList.AsEnumerable();
        }

        [HarmonyPatch(typeof(DepositItemsDesk), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(DepositItemsDesk __instance)
        {
            __instance.GetComponentInChildren<InteractTrigger>().cooldownTime = 0.1f;
        }
    }
}