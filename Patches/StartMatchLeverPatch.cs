using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class StartMatchLeverPatch
    {
        public static StartMatchLever Instance { get; private set; }

        [HarmonyPatch(typeof(StartMatchLever), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(StartMatchLever __instance)
        {
            Instance = __instance;

            if (Plugin.AllowPreGameLeverPullAsClient.Value && !__instance.IsHost)
            {
                __instance.triggerScript.hoverTip = "Start game : [LMB]";
                __instance.triggerScript.interactable = true;
            }
        }
    }
}