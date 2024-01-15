using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class TimeOfDayPatch
    {
        private static int _leftoverFunds = 0;

        [HarmonyPatch(typeof(TimeOfDay), nameof(UpdateProfitQuotaCurrentTime))]
        [HarmonyPostfix]
        private static void UpdateProfitQuotaCurrentTime()
        {
            StartOfRoundPatch.UpdateDeadlineMonitorText();
        }

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

                _leftoverFunds = __instance.quotaFulfilled - __instance.profitQuota;
                Plugin.MLS.LogInfo($"Storing surplus quota: ${_leftoverFunds}");
            }

            return true;
        }

        [HarmonyPatch(typeof(TimeOfDay), nameof(SyncNewProfitQuotaClientRpc))]
        [HarmonyPostfix]
        private static void SyncNewProfitQuotaClientRpc(TimeOfDay __instance)
        {
            if (Plugin.AllowQuotaRollover.Value)
            {
                Plugin.MLS.LogInfo($"Applying surplus quota to fulfilled: ${_leftoverFunds}");
                __instance.quotaFulfilled = _leftoverFunds;
            }
        }
    }
}