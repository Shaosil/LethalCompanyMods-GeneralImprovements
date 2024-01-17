using HarmonyLib;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class TerminalAccessibleObjectPatch
    {
        // Lazy load and cache reflection info
        private static FieldInfo _mapRadarTextField;
        private static FieldInfo MapRadarTextField => _mapRadarTextField ?? (_mapRadarTextField = typeof(TerminalAccessibleObject).GetField("mapRadarText", BindingFlags.Instance | BindingFlags.NonPublic));

        [HarmonyPatch(typeof(TerminalAccessibleObject), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(TerminalAccessibleObject __instance)
        {
            if (Plugin.ShipMapCamDueNorth.Value)
            {
                // Make sure the labels are facing the same way as the map camera
                var mapRadarText = (TextMeshProUGUI)MapRadarTextField.GetValue(__instance);
                mapRadarText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var curAngles = mapRadarText.rectTransform.eulerAngles;
                mapRadarText.rectTransform.rotation = Quaternion.Euler(curAngles.x, curAngles.y, 225);
                mapRadarText.transform.position += new Vector3(1, 0, -1);
            }
        }
    }
}