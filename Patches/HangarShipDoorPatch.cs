using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class HangarShipDoorPatch
    {
        public static HangarShipDoor Instance;

        [HarmonyPatch(typeof(HangarShipDoor), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(HangarShipDoor __instance)
        {
            Instance = __instance;
        }
    }
}