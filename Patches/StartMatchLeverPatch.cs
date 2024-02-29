using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class StartMatchLeverPatch
    {
        [HarmonyPatch(typeof(StartMatchLever), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(StartMatchLever __instance)
        {
            if (Plugin.AllowPreGameLeverPullAsClient.Value)
            {
                __instance.triggerScript.hoverTip = "Start game : [LMB]";
                __instance.triggerScript.interactable = true;
            }
        }
    }
}