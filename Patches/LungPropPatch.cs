using System.Linq;
using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class LungPropPatch
    {
        [HarmonyPatch(typeof(LungProp), nameof(LungProp.Start))]
        [HarmonyPostfix]
        private static void Start(LungProp __instance)
        {
            // Multiply its scrap value by defined weather multiplier
            var modifiedScrapValue = Plugin.SanitizedScrapValueWeatherMultipliers
                .FirstOrDefault(s => s.Key.Equals(RoundManager.Instance.currentLevel.currentWeather.ToString(), System.StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(modifiedScrapValue.Key))
            {
                Plugin.MLS.LogDebug($"Applying defined scrap value weather multiplier for {RoundManager.Instance.currentLevel.currentWeather} ({modifiedScrapValue.Value}x) to apparatus.");
                __instance.SetScrapValue((int)(__instance.scrapValue * (modifiedScrapValue.Value + 1)));
            }
        }
    }
}