using GeneralImprovements.Utilities;
using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class DepositItemsDeskPatch
    {
        public static int NumItemsSoldToday = 0;
        public static int ProfitThisQuota = 0;

        [HarmonyPatch(typeof(DepositItemsDesk), nameof(SellAndDisplayItemProfits))]
        [HarmonyPostfix]
        private static void SellAndDisplayItemProfits(DepositItemsDesk __instance, int profit)
        {
            var items = __instance.deskObjectsContainer.GetComponentsInChildren<GrabbableObject>();
            NumItemsSoldToday += items.Length;
            ProfitThisQuota += profit;

            MonitorsHelper.UpdateCompanyBuyRateMonitors();
            MonitorsHelper.UpdateSoldScrapMonitors();
        }

        [HarmonyPatch(typeof(DepositItemsDesk), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(DepositItemsDesk __instance)
        {
            __instance.GetComponentInChildren<InteractTrigger>().cooldownTime = 0.1f;
        }
    }
}