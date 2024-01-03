using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class DepositItemsDeskPatch
    {
        [HarmonyPatch(typeof(DepositItemsDesk), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(DepositItemsDesk __instance)
        {
            __instance.GetComponentInChildren<InteractTrigger>().cooldownTime = 0.1f;
        }
    }
}