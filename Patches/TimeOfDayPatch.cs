using GeneralImprovements.Utilities;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace GeneralImprovements.Patches
{
    internal static class TimeOfDayPatch
    {
        private static int _leftoverFunds = 0;

        [HarmonyPatch(typeof(TimeOfDay), nameof(SetNewProfitQuota))]
        [HarmonyPrefix]
        private static bool SetNewProfitQuota(TimeOfDay __instance)
        {
            if (Plugin.AllowQuotaRollover.Value)
            {
                // If we are not yet over our deadline, make sure to cancel out of the original method here
                if (__instance.timeUntilDeadline > 0)
                {
                    return false;
                }

                // Store the leftover funds on the server before they are overwritten
                _leftoverFunds = __instance.quotaFulfilled - __instance.profitQuota;
                Plugin.MLS.LogInfo($"Storing surplus quota on server: ${_leftoverFunds}");
            }

            return true;
        }

        [HarmonyPatch(typeof(TimeOfDay), nameof(SetNewProfitQuota))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SetNewProfitQuota_RemoveOvertimeBonus(IEnumerable<CodeInstruction> instructions)
        {
            if (Plugin.AllowOvertimeBonus.Value)
            {
                return instructions;
            }

            // Patch out the overtime bonus if needed
            var codeList = instructions.ToList();
            var callSync = codeList[codeList.Count - 2];
            var loadOvertime = codeList[codeList.Count - 5];
            if (callSync.opcode != OpCodes.Call || (callSync.operand as MethodInfo)?.Name != nameof(TimeOfDay.SyncNewProfitQuotaClientRpc) || loadOvertime.opcode != OpCodes.Ldloc_1)
            {
                Plugin.MLS.LogError("Unexpected IL code found in TimeOfDay.SetNewProfitQuota! Could not patch out overtime bonus.");
                return instructions;
            }

            // Replace the overtime bonus parameter with a simple 0
            Plugin.MLS.LogDebug("Patching new profit quota method to remove overtime bonus.");
            loadOvertime.opcode = OpCodes.Ldc_I4_0;
            return codeList.AsEnumerable();
        }


        [HarmonyPatch(typeof(TimeOfDay), "SyncNewProfitQuotaClientRpc")]
        [HarmonyPrefix]
        private static void SyncNewProfitQuotaClientRpc_Pre(TimeOfDay __instance)
        {
            // On clients only, store the leftover funds before they are overwritten
            if (Plugin.AllowQuotaRollover.Value && !__instance.IsServer)
            {
                _leftoverFunds = __instance.quotaFulfilled - __instance.profitQuota;
                Plugin.MLS.LogInfo($"Storing surplus quota on client: ${_leftoverFunds}");
            }
        }

        [HarmonyPatch(typeof(TimeOfDay), "SyncNewProfitQuotaClientRpc")]
        [HarmonyPostfix]
        private static void SyncNewProfitQuotaClientRpc_Post(TimeOfDay __instance)
        {
            if (Plugin.AllowQuotaRollover.Value)
            {
                // At this point, we will have the surplus set whether we are server or client
                Plugin.MLS.LogInfo($"Applying surplus quota to fulfilled: ${_leftoverFunds}");
                __instance.quotaFulfilled = _leftoverFunds;
            }

            MonitorsHelper.UpdateTotalQuotasMonitors();
        }

        [HarmonyPatch(typeof(TimeOfDay), nameof(UpdateProfitQuotaCurrentTime))]
        [HarmonyPostfix]
        private static void UpdateProfitQuotaCurrentTime()
        {
            MonitorsHelper.CopyProfitQuotaAndDeadlineTexts();
        }
    }
}