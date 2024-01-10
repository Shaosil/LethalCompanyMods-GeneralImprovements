using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class TimeOfDayPatch
    {

        [HarmonyPatch(typeof(TimeOfDay), nameof(UpdateProfitQuotaCurrentTime))]
        [HarmonyPostfix]
        private static void UpdateProfitQuotaCurrentTime()
        {
            StartOfRoundPatch.UpdateQuotaScreenText();
        }
    }
}