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
            var outsideScrap = GrabbableObjectsPatch.GetOutsideScrap(false);
            RoundManager.Instance.totalScrapValueInLevel = outsideScrap.Value;
        }

        [HarmonyPatch(typeof(RoundManager), nameof(FinishGeneratingNewLevelClientRpc))]
        [HarmonyPostfix]
        private static void FinishGeneratingNewLevelClientRpc(RoundManager __instance)
        {
            if (!_gotShipNode)
            {
                CurShipNode = Object.FindObjectsOfType<ScanNodeProperties>().FirstOrDefault(s => s.headerText == "Ship");
                if (CurShipNode != null)
                {
                    // Disable the node for now (it will enable again once the ship is landed)
                    Plugin.MLS.LogInfo("Disabling ship scan node until landed.");
                    CurShipNode.gameObject.SetActive(false);

                    _gotShipNode = true;
                }
            }

            // If mimics are active and we want scan nodes on fire exits, create them here
            if (Plugin.ShowDoorsOnScanner.Value && OtherModHelper.MimicsActive)
            {
                var mimics = Object.FindObjectsOfType<InteractTrigger>().Where(g => g.transform.parent?.name.StartsWith("MimicDoor") ?? false).ToList();
                foreach (var mimic in mimics)
                {
                    // Manually move the scan node's position, or it will be in a noticeably different spot than real exits
                    var mimicScanNode = ObjectHelper.CreateScanNodeOnObject(mimic.transform.parent.gameObject, 0, 1, 20, "Fire Exit?");
                    mimicScanNode.transform.localPosition += new Vector3(0, 1.5f, 0);
                }
            }

            MonitorsHelper.UpdateScrapLeftMonitors();
        }

        [HarmonyPatch(typeof(RoundManager), nameof(DespawnPropsAtEndOfRound))]
        [HarmonyPostfix]
        private static void DespawnPropsAtEndOfRound()
        {
            MonitorsHelper.UpdateTimeMonitors();
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