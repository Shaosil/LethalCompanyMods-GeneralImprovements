using GeneralImprovements.Utilities;
using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class RoundManagerPatch
    {
        private static bool _gotShipNode = false;
        public static ScanNodeProperties CurShipNode { get; private set; }

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

            CurShipNode = Object.FindObjectsOfType<ScanNodeProperties>().FirstOrDefault(s => s.headerText == "Ship");
            if (CurShipNode != null)
            {
                // Disable the node for now (it will enable again once the ship is landed)
                Plugin.MLS.LogInfo("Disabling ship scan node until landed.");
                CurShipNode.gameObject.SetActive(false);

                _gotShipNode = true;
            }
        }

        [HarmonyPatch(typeof(RoundManager), nameof(DespawnPropsAtEndOfRound))]
        [HarmonyPostfix]
        private static void DespawnPropsAtEndOfRound()
        {
            MonitorsHelper.UpdateTimeMonitor();
        }

        public static void EnableAndAttachShipScanNode()
        {
            if (CurShipNode != null)
            {
                // Enable the ship node again
                Plugin.MLS.LogInfo("Enabling ship scan node again and parenting it to the terminal.");
                CurShipNode.gameObject.SetActive(true);
                CurShipNode.transform.parent = TerminalPatch.Instance.transform;
                _gotShipNode = false;
            }
        }
    }
}