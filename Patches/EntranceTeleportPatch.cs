using GameNetcodeStuff;
using GeneralImprovements.Utilities;
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

        [HarmonyPatch(typeof(EntranceTeleport), nameof(TeleportPlayer))]
        [HarmonyPostfix]
        private static void TeleportPlayer(EntranceTeleport __instance)
        {
            // If we are going through an external fire exit, rotate ourselves 180 degrees after going through
            if (Plugin.FixInternalFireExits.Value && __instance.entranceId != 0 && __instance.isEntranceToBuilding)
            {
                FlipPlayer(__instance, GameNetworkManager.Instance.localPlayerController);
            }
        }

        [HarmonyPatch(typeof(EntranceTeleport), nameof(TeleportPlayerClientRpc))]
        [HarmonyPostfix]
        private static void TeleportPlayerClientRpc(EntranceTeleport __instance, int playerObj)
        {
            // Flip any clients (not ourselves) that have teleported through the door
            if (Plugin.FixInternalFireExits.Value && __instance.entranceId != 0 && __instance.isEntranceToBuilding)
            {
                var player = __instance.playersManager.allPlayerScripts[playerObj];
                if (!player.IsOwner)
                {
                    FlipPlayer(__instance, player);
                }
            }
        }

        private static void FlipPlayer(EntranceTeleport instance, PlayerControllerB player)
        {
            var targetAngles = ((Transform)ExitPoint.GetValue(instance)).eulerAngles;
            player.transform.rotation = Quaternion.Euler(targetAngles.x, targetAngles.y + 180, targetAngles.z);
        }
    }
}