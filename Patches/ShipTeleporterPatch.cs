using GameNetcodeStuff;
using HarmonyLib;
using System;

namespace GeneralImprovements.Patches
{
    internal static class ShipTeleporterPatch
    {
        private static PlayerControllerB _lastPlayerTeleported;

        [HarmonyPatch(typeof(ShipTeleporter), "Awake")]
        [HarmonyPrefix]
        private static void Awake_Pre(ShipTeleporter __instance)
        {
            // Overwrite the cooldown values
            int regularCooldown = Math.Clamp(Plugin.RegularTeleporterCooldown.Value, 0, 300);
            int inverseCooldown = Math.Clamp(Plugin.InverseTeleporterCooldown.Value, 0, 300);
            __instance.cooldownAmount = __instance.isInverseTeleporter ? inverseCooldown : regularCooldown;
        }

        [HarmonyPatch(typeof(ShipTeleporter), "Awake")]
        [HarmonyPostfix]
        private static void Awake_Post(ShipTeleporter __instance)
        {
            if (__instance.isInverseTeleporter)
            {
                __instance.GetComponentInChildren<InteractTrigger>().hoverTip = "Beam out : [E]";
            }
        }

        [HarmonyPatch(typeof(ShipTeleporter), nameof(beamUpPlayer), MethodType.Enumerator)]
        [HarmonyPostfix]
        private static void beamUpPlayer(bool __result)
        {
            // When the result of the MoveNext method is false, we are done
            if (__result)
            {
                _lastPlayerTeleported = StartOfRound.Instance.mapScreen.targetedPlayer;
            }
            else if (_lastPlayerTeleported?.deadBody?.grabBodyObject != null)
            {
                // "Drop" the body in the elevator so it marks it as collected scrap for everyone to see
                _lastPlayerTeleported.SetItemInElevator(true, true, _lastPlayerTeleported.deadBody.grabBodyObject);
            }
        }
    }
}