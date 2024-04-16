using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class MenuPatches
    {
        [HarmonyPatch(typeof(MenuManager), "Start")]
        [HarmonyPrefix]
        private static void MenuManager_Start()
        {
            if (!Plugin.AlwaysShowNews.Value && GameNetworkManager.Instance != null)
            {
                GameNetworkManager.Instance.firstTimeInMenu = false;
            }
        }

        [HarmonyPatch(typeof(InitializeGame), "Start")]
        [HarmonyPrefix]
        private static void Start_Initialize(InitializeGame __instance)
        {
            if (Plugin.SkipStartupScreen.Value)
            {
                __instance.runBootUpScreen = false;
            }
        }

        [HarmonyPatch(typeof(PreInitSceneScript), nameof(SkipToFinalSetting))]
        [HarmonyPrefix]
        private static bool SkipToFinalSetting(PreInitSceneScript __instance)
        {
            string autoVal = Plugin.AutoSelectLaunchMode.Value?.ToUpper();
            bool autoSpecified = !string.IsNullOrWhiteSpace(autoVal) && (autoVal.Contains("ON") || autoVal.Contains("LAN"));
            bool isOnline = autoSpecified && autoVal.Contains("ON");

            if (autoSpecified)
            {
                Plugin.MLS.LogInfo($"Automatically launching {(isOnline ? "ONLINE" : "LAN")} mode.");
                __instance.ChooseLaunchOption(isOnline);
                __instance.launchSettingsPanelsContainer.SetActive(false);

                return false;
            }

            return true;
        }
    }
}