using GameNetcodeStuff;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class HUDManagerPatch
    {
        private static RaycastHit[] _scanHits;

        // Lazy load and cache reflection info
        private static FieldInfo _scannedScrapNumField;
        private static FieldInfo ScannedScrapNumField => _scannedScrapNumField ?? (_scannedScrapNumField = typeof(HUDManager).GetField("scannedScrapNum", BindingFlags.NonPublic | BindingFlags.Instance));
        private static FieldInfo _nodesOnScreenField;
        private static FieldInfo NodesOnScreenField => _nodesOnScreenField ?? (_nodesOnScreenField = typeof(HUDManager).GetField("nodesOnScreen", BindingFlags.NonPublic | BindingFlags.Instance));
        private static MethodInfo _attemptScanNodeMethod;
        private static MethodInfo AttemptScanNodeMethod => _attemptScanNodeMethod ?? (_attemptScanNodeMethod = typeof(HUDManager).GetMethod("AttemptScanNode", BindingFlags.NonPublic | BindingFlags.Instance));

        [HarmonyPatch(typeof(HUDManager), nameof(AssignNewNodes))]
        [HarmonyPrefix]
        private static bool AssignNewNodes(HUDManager __instance, PlayerControllerB playerScript)
        {
            if (!Plugin.FixPersonalScanner.Value)
            {
                return true;
            }

            // Increase the allowance for raycast scan targets
            _scanHits = new RaycastHit[64];
            var ray = new Ray(playerScript.gameplayCamera.transform.position + playerScript.gameplayCamera.transform.forward * 20f, playerScript.gameplayCamera.transform.forward);
            int numHits = Physics.SphereCastNonAlloc(ray, 20f, _scanHits, 100f, LayerMask.GetMask("ScanNode"));

            // Sort the hits by distance and take only up to the number of UI elements to distribute
            _scanHits = _scanHits.Where(h => h.collider != null).OrderBy(h => h.distance).Take(__instance.scanElements.Length).ToArray();

            NodesOnScreenField.SetValue(__instance, new List<ScanNodeProperties>());
            ScannedScrapNumField.SetValue(__instance, 0);

            for (int i = 0; i < __instance.scanElements.Length && i < _scanHits.Length; i++)
            {
                var component = _scanHits[i].transform.GetComponent<ScanNodeProperties>();
                AttemptScanNodeMethod.Invoke(__instance, new object[] { component, i, playerScript });
            }

            // Skip the original method
            return false;
        }
    }
}