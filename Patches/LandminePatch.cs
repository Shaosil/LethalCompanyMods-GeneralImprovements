using HarmonyLib;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class LandminePatch
    {
        [HarmonyPatch(typeof(Landmine), nameof(Detonate))]
        [HarmonyPostfix]
        private static void Detonate(Landmine __instance)
        {
            // Remove the terminal accessible object script so it stops showing up on the map screen
            Object.Destroy(__instance.GetComponent<TerminalAccessibleObject>());
        }
    }
}