using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class GameNetworkManagerPatch
    {
        [HarmonyPatch(typeof(GameNetworkManager), nameof(Disconnect))]
        [HarmonyPrefix]
        private static void Disconnect(GameNetworkManager __instance)
        {
            Plugin.MLS.LogInfo("IN DISCONNECT");

            // If we are about to disconnect as a host, first "drop" all held items so they don't save in mid air
            if (StartOfRound.Instance?.IsHost ?? false)
            {
                Plugin.MLS.LogInfo("IN DISCONNECT AS SERVER");

                var allHeldItems = Object.FindObjectsOfType<GrabbableObject>().Where(g => g.isHeld).ToList();
                foreach (var heldItem in allHeldItems)
                {
                    Plugin.MLS.LogInfo($"HELD ITEM {heldItem.name} CUR POSITION: {heldItem.transform.position}");
                    heldItem.transform.position = heldItem.GetItemFloorPosition();
                    Plugin.MLS.LogInfo($"HELD ITEM {heldItem.name} NEW POSITION: {heldItem.transform.position}");
                }
            }
        }
    }
}