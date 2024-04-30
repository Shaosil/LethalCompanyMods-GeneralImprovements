using HarmonyLib;
using static GeneralImprovements.Plugin.Enums;

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
            if (Plugin.AutoSelectLaunchMode.Value != eAutoLaunchOptions.NONE)
            {
                Plugin.MLS.LogInfo($"Automatically launching {Plugin.AutoSelectLaunchMode.Value} mode.");
                __instance.ChooseLaunchOption(Plugin.AutoSelectLaunchMode.Value == eAutoLaunchOptions.ONLINE);
                __instance.launchSettingsPanelsContainer.SetActive(false);

                return false;
            }

            return true;
        }
    }
}