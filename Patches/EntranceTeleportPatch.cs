using GeneralImprovements.Utilities;
using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class EntranceTeleportPatch
    {
        [HarmonyPatch(typeof(EntranceTeleport), nameof(Awake))]
        [HarmonyPostfix]
        private static void Awake(EntranceTeleport __instance)
        {
            // If configured, add a scan node to all exits and fire entrances
            if (Plugin.ShowDoorsOnScanner.Value && !(__instance.entranceId == 0 && __instance.isEntranceToBuilding))
            {
                string text = __instance.isEntranceToBuilding ? "Fire Entrance" : __instance.entranceId == 0 ? "Main Exit" : $"Fire Exit{(OtherModHelper.MimicsActive ? "?" : "")}";
                ObjectHelper.CreateScanNodeOnObject(__instance.gameObject, 0, 1, __instance.isEntranceToBuilding ? 50 : 20, text);
            }
        }
    }
}