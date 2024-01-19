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
                .Where(s => s.Value.GetComponentInParent<GrabbableObject>()?.playerHeldBy != playerScript && s.Key >= s.Value.minRange && s.Key <= s.Value.maxRange
                    && GeometryUtility.TestPlanesAABB(camPlanes, new Bounds(s.Value.transform.position, Vector3.one)))
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

        [HarmonyPatch(typeof(HUDManager), nameof(SetClock))]
        [HarmonyPostfix]
        private static void SetClock(TextMeshProUGUI ___clockNumber)
        {
            SceneHelper.UpdateTimeMonitor();
        }

        [HarmonyPatch(typeof(HUDManager), nameof(CanPlayerScan))]
        [HarmonyPostfix]
        private static void CanPlayerScan(ref bool __result)
        {
            __result = __result && !(ShipBuildModeManager.Instance?.InBuildMode ?? false);
        }
    }
}