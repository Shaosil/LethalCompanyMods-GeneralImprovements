using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class KeyItemPatch
    {
        [HarmonyPatch(typeof(GrabbableObject), nameof(Start))]
        [HarmonyPrefix]
        private static void Start(GrabbableObject __instance)
        {
            if (__instance is KeyItem)
            {
                __instance.SetScrapValue(0);
            }
        }
    }
}