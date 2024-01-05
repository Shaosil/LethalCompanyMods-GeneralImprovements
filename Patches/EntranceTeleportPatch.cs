using HarmonyLib;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class EntranceTeleportPatch
    {
        [HarmonyPatch(typeof(EntranceTeleport), nameof(Awake))]
        [HarmonyPostfix]
        private static void Awake(EntranceTeleport __instance)
        {
            // If this is an internal fire exit, add 180 to our Y direction
            if (__instance.entranceId != 0 && !__instance.isEntranceToBuilding)
            {
                var curRot = __instance.entrancePoint.eulerAngles;
                __instance.entrancePoint.eulerAngles = new Vector3(curRot.x, curRot.y + 180, curRot.z);
            }
        }
    }
}