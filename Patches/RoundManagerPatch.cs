using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class RoundManagerPatch
    {
        private static bool _gotShipNode = false;
        private static ScanNodeProperties _curShipNode;

        [HarmonyPatch(typeof(RoundManager), nameof(SyncScrapValuesClientRpc))]
        [HarmonyPostfix]
        private static void SyncScrapValuesClientRpc()
        {
            // Update and override the total scrap in level
            var valuables = Object.FindObjectsOfType<GrabbableObject>().Where(o => !o.isInShipRoom && !o.isInElevator && o.itemProperties.minValue > 0).ToList();
            RoundManager.Instance.totalScrapValueInLevel = valuables.Sum(i => i.scrapValue);
        }

        [HarmonyPatch(typeof(RoundManager), nameof(FinishGeneratingNewLevelClientRpc))]
        [HarmonyPostfix]
        private static void FinishGeneratingNewLevelClientRpc(RoundManager __instance)
        {
            if (_gotShipNode)
            {
                return;
            }

            _curShipNode = Object.FindObjectsOfType<ScanNodeProperties>().FirstOrDefault(s => s.headerText == "Ship");
            if (_curShipNode != null)
            {
                // Fix Rend's ship scan node
                if (__instance.currentLevel.sceneName == "Level5Rend")
                {
                    _curShipNode.transform.localScale *= 2;
                }

                // Disable the node for now (it will enable again once the ship is landed)
                Plugin.MLS.LogInfo("Disabling ship scan node until landed.");
                _curShipNode.gameObject.SetActive(false);

                _gotShipNode = true;
            }
        }

        public static void EnableShipScanNode()
        {
            if (_curShipNode != null)
            {
                // Enable the ship node again
                Plugin.MLS.LogInfo("Enabling ship scan node again.");
                _curShipNode.gameObject.SetActive(true);
                _gotShipNode = false;
            }
        }
    }
}