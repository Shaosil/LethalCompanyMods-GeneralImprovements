using GeneralImprovements.Utilities;
using HarmonyLib;

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