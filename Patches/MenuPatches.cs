using HarmonyLib;
using UnityEngine.SceneManagement;

namespace GeneralImprovements.Patches
{
    internal static class MenuPatches
    {
        [HarmonyPatch(typeof(InitializeGame), "Start")]
        [HarmonyPrefix]
        private static void Start_Initialize(InitializeGame __instance)
        {
            if (Plugin.SkipStartupScreen.Value)
            {
                __instance.runBootUpScreen = false;
            }
        }

        [HarmonyPatch(typeof(PreInitSceneScript), "Start")]
        [HarmonyPostfix]
        private static void Start_PreInit()
        {
            string autoVal = Plugin.AutoSelectLaunchMode.Value?.ToUpper();
            bool autoSpecified = !string.IsNullOrWhiteSpace(autoVal) && (autoVal.Contains("ON") || autoVal.Contains("LAN"));
            bool isOnline = autoSpecified && autoVal.Contains("ON");

            if (autoSpecified)
            {
                Plugin.MLS.LogInfo($"Automatically launching {(isOnline ? "ONLINE" : "LAN")} mode.");
                SceneManager.LoadScene(isOnline ? "InitScene" : "InitSceneLANMode");
            }
        }
    }
}