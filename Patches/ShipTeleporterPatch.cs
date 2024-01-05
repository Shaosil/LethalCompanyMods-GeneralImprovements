using GameNetcodeStuff;
using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class ShipTeleporterPatch
    {
        private static PlayerControllerB _lastPlayerTeleported;

        [HarmonyPatch(typeof(ShipTeleporter), nameof(beamUpPlayer), MethodType.Enumerator)]
        [HarmonyPostfix]
        private static void beamUpPlayer(ShipTeleporter __instance, bool __result)
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