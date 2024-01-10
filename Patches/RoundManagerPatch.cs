using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class RoundManagerPatch
    {

        [HarmonyPatch(typeof(RoundManager), nameof(SyncScrapValuesClientRpc))]
        [HarmonyPostfix]
        private static void SyncScrapValuesClientRpc()
        {
            // Update and override the total scrap in level
            var valuables = Object.FindObjectsOfType<GrabbableObject>().Where(o => !o.isInShipRoom && !o.isInElevator && o.itemProperties.minValue > 0).ToList();
            RoundManager.Instance.totalScrapValueInLevel = valuables.Sum(i => i.scrapValue);
        }
    }
}