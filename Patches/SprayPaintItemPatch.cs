using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class SprayPaintItemPatch
    {
        [HarmonyPatch(typeof(SprayPaintItem), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(int ___sprayCanMatsIndex)
        {
            Plugin.MLS.LogError($"MAP SEED: {StartOfRound.Instance.randomMapSeed}. SPRAY PAINT INDEX: {___sprayCanMatsIndex}");
        }
    }
}