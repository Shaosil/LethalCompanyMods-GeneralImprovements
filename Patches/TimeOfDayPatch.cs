using GeneralImprovements.Utilities;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;

namespace GeneralImprovements.Patches
{
    internal static class TimeOfDayPatch
    {
        private static int _leftoverFunds = 0;

        [HarmonyPatch(typeof(TimeOfDay), nameof(TimeOfDay.SetNewProfitQuota))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static bool SetNewProfitQuota(TimeOfDay __instance)
        {
            if (Plugin.AllowQuotaRollover.Value)
            {
                // This will get called by vanilla code every day it detects you are over the target quota.
                // If we are over our deadline, or we have sold items today, that's fine. Otherwise, prevent the call.
                bool shouldSetNewQuota = __instance.timeUntilDeadline <= 0 || DepositItemsDeskPatch.NumItemsSoldToday > 0;
                if (!shouldSetNewQuota)
                {
                    Plugin.MLS.LogInfo($"Skipping SetNewProfitQuota as we are not past the deadline ({__instance.timeUntilDeadline} remaining) and we did not sell any items today.");
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
            if (instructions.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
            {
                i => i.IsLdloc(),
                i => i.IsLdarg(),
                i => i.LoadsField(typeof(TimeOfDay).GetField(nameof(TimeOfDay.timesFulfilledQuota))),
                i => i.Calls(typeof(TimeOfDay).GetMethod(nameof(TimeOfDay.SyncNewProfitQuotaClientRpc)))
            }, out var found))
            {
                // Replace the overtime bonus parameter with a simple 0
                Plugin.MLS.LogDebug("Patching new profit quota method to remove overtime bonus.");
                found.First().Instruction.opcode = OpCodes.Ldc_I4_0;
            }
            else
            {
                Plugin.MLS.LogError("Unexpected IL code found in TimeOfDay.SetNewProfitQuota! Could not patch out overtime bonus.");
            }

            return instructions;
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
            // Always make sure the two quota numbers are on separate lines if nothing else modified them
            var match = new Regex(@"PROFIT QUOTA:\n(.+\d+) / (.+\d+)").Match(StartOfRound.Instance?.profitQuotaMonitorText?.text ?? string.Empty);
            if (match.Success)
            {
                // Keep it as vanilla as possible and just use whatever we found
                StartOfRound.Instance.profitQuotaMonitorText.text = $"PROFIT\nQUOTA:\n{match.Groups[1]} /\n{match.Groups[2]}";
            }

            MonitorsHelper.CopyProfitQuotaAndDeadlineTexts();
        }
    }
}