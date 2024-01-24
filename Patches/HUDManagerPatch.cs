using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class HUDManagerPatch
    {
        // Lazy load and cache reflection info
        private static MethodInfo _attemptScanNodeMethod;
        private static MethodInfo AttemptScanNodeMethod => _attemptScanNodeMethod ?? (_attemptScanNodeMethod = typeof(HUDManager).GetMethod("AttemptScanNode", BindingFlags.NonPublic | BindingFlags.Instance));

        [HarmonyPatch(typeof(HUDManager), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(ref float ___playerPingingScan)
        {
            ___playerPingingScan = -1f;
        }

        [HarmonyPatch(typeof(HUDManager), nameof(AssignNewNodes))]
        [HarmonyPrefix]
        private static bool AssignNewNodes(HUDManager __instance, PlayerControllerB playerScript, ref int ___scannedScrapNum, List<ScanNodeProperties> ___nodesOnScreen)
        {
            if (!Plugin.FixPersonalScanner.Value)
            {
                return true;
            }

            ___nodesOnScreen.Clear();
            ___scannedScrapNum = 0;

            // Get all the in-range scannables in the player's camera viewbox and sort them by distance away from the player
            var camPlanes = GeometryUtility.CalculateFrustumPlanes(playerScript.gameplayCamera);
            var allScannables = Object.FindObjectsOfType<ScanNodeProperties>()
                .Select(s => new KeyValuePair<float, ScanNodeProperties>(Vector3.Distance(s.transform.position, playerScript.transform.position), s))
                .Where(s => ((s.Value.GetComponent<Collider>()?.enabled ?? false) // Active and enabled...
                        || Plugin.ScanHeldPlayerItems.Value && s.Value.GetComponentInParent<GrabbableObject>() is GrabbableObject g
                        && !g.isPocketed && g.playerHeldBy != null && g.playerHeldBy != playerScript) // ... or held by someone else
                    && s.Key >= s.Value.minRange && s.Key <= s.Value.maxRange // In range
                    && GeometryUtility.TestPlanesAABB(camPlanes, new Bounds(s.Value.transform.position, Vector3.one))) // In camera view
                .OrderBy(s => s.Key);

            // Now attempt to scan each of them, stopping when we fill the number of UI elements
            foreach (var scannable in allScannables)
            {
                AttemptScanNodeMethod.Invoke(__instance, new object[] { scannable.Value, 0, playerScript });
                if (___nodesOnScreen.Count >= __instance.scanElements.Length)
                {
                    break;
                }
            }

            // Skip the original method
            return false;
        }

        [HarmonyPatch(typeof(HUDManager), nameof(UpdateScanNodes))]
        [HarmonyPostfix]
        private static void UpdateScanNodes(RectTransform[] ___scanElements, Dictionary<RectTransform, ScanNodeProperties> ___scanNodes)
        {
            // Disable subtext if desired and it has no text or scrap value
            if (Plugin.HideEmptySubtextOfScanNodes.Value && ___scanElements != null)
            {
                foreach (var scanElement in ___scanElements.Where(s => s.gameObject.activeSelf))
                {
                    var subText = scanElement.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(t => t.name.ToUpper() == "SUBTEXT");
                    var subTextBox = subText?.transform.parent.Find("SubTextBox");

                    if (subTextBox != null && subText != null && ___scanNodes.ContainsKey(scanElement) && ___scanNodes[scanElement] != null)
                    {
                        bool shouldHide = string.IsNullOrWhiteSpace(subText.text) || subText.text.ToUpper().Contains("VALUE: $0");
                        subTextBox.gameObject.SetActive(!shouldHide);
                        subText.enabled = !shouldHide;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(HUDManager), nameof(SetClock))]
        [HarmonyPostfix]
        private static void SetClock(TextMeshProUGUI ___clockNumber)
        {
            MonitorsHelper.UpdateTimeMonitor();
        }

        [HarmonyPatch(typeof(HUDManager), nameof(CanPlayerScan))]
        [HarmonyPostfix]
        private static void CanPlayerScan(ref bool __result)
        {
            __result = __result && !(ShipBuildModeManager.Instance?.InBuildMode ?? false);
        }
    }
}