using HarmonyLib;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class EntranceTeleportPatch
    {
        [HarmonyPatch(typeof(EntranceTeleport), nameof(TeleportPlayer))]
        [HarmonyPostfix]
        private static void Awake(EntranceTeleport __instance)
        {
            // If this is an external fire exit, rotate player 180 degrees after going through
            if (__instance.isEntranceToBuilding && __instance.entranceId > 0)
			{
				Transform thisPlayerBody = GameNetworkManager.Instance.localPlayerController.thisPlayerBody;
				thisPlayerBody.RotateAround(thisPlayerBody.transform.position, Vector3.up, 180f);
			}
        }
    }
}
