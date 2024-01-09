using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class EntranceTeleportPatch
    {
        private static FieldInfo _exitPoint;
        private static FieldInfo ExitPoint
        {
            get
            {
                // Lazy load and cache reflection info
                if (_exitPoint == null)
                {
                    _exitPoint = typeof(EntranceTeleport).GetField("exitPoint", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return _exitPoint;
            }
        }

        [HarmonyPatch(typeof(EntranceTeleport), nameof(TeleportPlayerClientRpc))]
        [HarmonyPostfix]
        private static void TeleportPlayerClientRpc(EntranceTeleport __instance, int playerObj)
        {
            // If we are going through an external fire exit, rotate player 180 degrees after going through
            if (__instance.entranceId != 0 && __instance.isEntranceToBuilding)
            {
                var player = __instance.playersManager.allPlayerScripts[playerObj];
                var targetAngles = ((Transform)ExitPoint.GetValue(__instance)).eulerAngles;
                player.transform.rotation = Quaternion.Euler(targetAngles.x, targetAngles.y + 180, targetAngles.z);
            }
        }
    }
}