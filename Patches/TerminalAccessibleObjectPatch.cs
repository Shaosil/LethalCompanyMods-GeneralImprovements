using HarmonyLib;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class TerminalAccessibleObjectPatch
    {
        [HarmonyPatch(typeof(TerminalAccessibleObject), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(TerminalAccessibleObject __instance)
        {
            if (Plugin.ShipMapCamRotation.Value != Enums.eShipCamRotation.None && __instance.mapRadarText != null)
            {
                // Make sure the labels are facing the same way as the map camera
                __instance.mapRadarText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var curAngles = __instance.mapRadarText.transform.eulerAngles;
                //__instance.mapRadarText.rectTransform.rotation = Quaternion.Euler(curAngles.x, curAngles.y, 225);
                float rotationAngle = 90 * (int)Plugin.ShipMapCamRotation.Value;
                __instance.mapRadarText.transform.rotation = Quaternion.Euler(curAngles.x, rotationAngle, curAngles.z);
                //__instance.mapRadarText.transform.position += new Vector3(1, 0, -1);
            }
        }
    }
}