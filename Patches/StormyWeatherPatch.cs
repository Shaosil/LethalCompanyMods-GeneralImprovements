using HarmonyLib;
using Unity.Netcode;

namespace GeneralImprovements.Patches
{
    internal static class StormyWeatherPatch
    {
        public static StormyWeather Instance { get; private set; }

        [HarmonyPatch(typeof(StormyWeather), nameof(OnEnable))]
        [HarmonyPostfix]
        private static void OnEnable(StormyWeather __instance)
        {
            if (Instance != __instance)
            {
                Instance = __instance;
            }
        }

        [HarmonyPatch(typeof(StormyWeather), nameof(OnDisable))]
        [HarmonyPostfix]
        private static void OnDisable()
        {
            Instance = null;
        }

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