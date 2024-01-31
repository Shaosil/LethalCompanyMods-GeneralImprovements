using HarmonyLib;
using Unity.Netcode;

namespace GeneralImprovements.Patches
{
    internal static class StormyWeatherPatch
    {
        [HarmonyPatch(typeof(StormyWeather), nameof(SetStaticElectricityWarning))]
        [HarmonyPostfix]
        private static void SetStaticElectricityWarning(NetworkObject warningObject)
        {
            // Store the targeted grabbable object when clients receive the warning
            warningObject.TryGetComponent(out HUDManagerPatch.CurrentLightningTarget);
        }

        [HarmonyPatch(typeof(StormyWeather), nameof(LightningStrike))]
        [HarmonyPostfix]
        private static void LightningStrike(bool useTargetedObject)
        {
            // Null out the lightning target after a targeted strike
            if (useTargetedObject)
            {
                HUDManagerPatch.CurrentLightningTarget = null;
            }
        }
    }
}