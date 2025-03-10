using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using GeneralImprovements.Utilities;
using HarmonyLib;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class TimeOfDayPatch
    {
        private static int _leftoverFunds = 0;

        [HarmonyPatch(typeof(TimeOfDay), nameof(TimeOfDay.SetNewProfitQuota))]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.Low)]
        private static void SetNewProfitQuota(TimeOfDay __instance)
        {
            if (Plugin.AllowQuotaRollover.Value)
            {
                // Store the leftover funds on the server before they are overwritten
                _leftoverFunds = __instance.quotaFulfilled - __instance.profitQuota;
                Plugin.MLS.LogInfo($"Storing surplus quota on server: ${_leftoverFunds}");
            }
        }

        [HarmonyPatch(typeof(TimeOfDay), nameof(SetNewProfitQuota))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SetNewProfitQuota_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.OvertimeBonusType.Value != Enums.eOvertimeBonusType.Vanilla)
            {
                // Patch out the overtime bonus if needed
                if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {

                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(TimeOfDay).GetField(nameof(TimeOfDay.quotaFulfilled))),
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(TimeOfDay).GetField(nameof(TimeOfDay.profitQuota))),
                    i => i.opcode == OpCodes.Sub
                }, out var found))
                {
                    switch (Plugin.OvertimeBonusType.Value)
                    {
                        case Enums.eOvertimeBonusType.Disabled:
                        case Enums.eOvertimeBonusType.SoldScrapOnly:
                            if (Plugin.OvertimeBonusType.Value == Enums.eOvertimeBonusType.Disabled)
                            {
                                // Replace the subtraction with a simple zero
                                Plugin.MLS.LogDebug("Patching TimeOfDay.SetNewProfitQuota to remove overtime bonus calculation.");
                                found.First().Instruction.opcode = OpCodes.Ldc_I4_0;
                            }
                            else
                            {
                                // Replace the overtime bonus parameter with the amount that we've sold
                                Plugin.MLS.LogDebug("Patching TimeOfDay.SetNewProfitQuota to use sold scrap only for overtime bonus.");
                                codeList[found.First().Index] = Transpilers.EmitDelegate<Func<int>>(() => Math.Max(DepositItemsDeskPatch.ProfitThisQuota - TimeOfDay.Instance.profitQuota, 0));
                            }

                            for (int i = 1; i < found.Length; i++) found[i].Instruction.opcode = OpCodes.Nop;

                            break;

                        default:
                            Plugin.MLS.LogWarning($"Unknown overtime bonus type specified ({Plugin.OvertimeBonusType.Value})! No action taken.");
                            break;
                    }
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL code found in TimeOfDay.SetNewProfitQuota! Could not patch out overtime bonus.");
                }
            }

            // Add the monitor update at the end of the function
            codeList.Insert(codeList.Count - 1, Transpilers.EmitDelegate<Action>(() => MonitorsHelper.UpdateCalculatedScrapMonitors()));

            return codeList;
        }

        [HarmonyPatch(typeof(TimeOfDay), nameof(TimeOfDay.SetBuyingRateForDay))]
        [HarmonyPostfix]
        private static void SetBuyingRateForDay()
        {
            MonitorsHelper.UpdateCompanyBuyRateMonitors();

            // Also set a 5 second delay to do it again to support the delay that BuyRateSettings uses
            TimeOfDay.Instance.StartCoroutine(UpdateCompanyBuyRateDelayed());
        }

        private static System.Collections.IEnumerator UpdateCompanyBuyRateDelayed()
        {
            yield return new WaitForSeconds(5);

            MonitorsHelper.UpdateCompanyBuyRateMonitors();
        }

        [HarmonyPatch(typeof(TimeOfDay), nameof(SyncNewProfitQuotaClientRpc))]
        [HarmonyPostfix]
        private static void SyncNewProfitQuotaClientRpc(TimeOfDay __instance)
        {
            if (__instance.IsServer && Plugin.AllowQuotaRollover.Value)
            {
                // At this point, we will have the surplus set
                Plugin.MLS.LogInfo($"Applying surplus quota to fulfilled: ${_leftoverFunds}");
                __instance.quotaFulfilled = _leftoverFunds;

                // Send the new quota surplus over to clients
                NetworkHelper.Instance.SyncProfitQuotaClientRpc(__instance.quotaFulfilled);
            }

            // Reset per quota variables
            DepositItemsDeskPatch.ProfitThisQuota = 0;

            MonitorsHelper.UpdateTotalQuotasMonitors();
            MonitorsHelper.UpdateCompanyBuyRateMonitors();
            MonitorsHelper.UpdateSoldScrapMonitors();
        }

        [HarmonyPatch(typeof(TimeOfDay), nameof(UpdateProfitQuotaCurrentTime))]
        [HarmonyPostfix]
        private static void UpdateProfitQuotaCurrentTime()
        {
            // Always make sure the two quota numbers are on separate lines if nothing else modified them
            string text = StartOfRound.Instance && StartOfRound.Instance.profitQuotaMonitorText ? StartOfRound.Instance.profitQuotaMonitorText.text : string.Empty;
            var match = new Regex(@"PROFIT QUOTA:\n(.+\d+) / (.+\d+)").Match(text);
            if (match.Success)
            {
                // Keep it as vanilla as possible and just use whatever we found
                StartOfRound.Instance.profitQuotaMonitorText.text = $"PROFIT\nQUOTA:\n{match.Groups[1]} /\n{match.Groups[2]}";
            }

            MonitorsHelper.CopyProfitQuotaAndDeadlineTexts();
        }
    }
}