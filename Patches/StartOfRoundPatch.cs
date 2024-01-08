using HarmonyLib;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class StartOfRoundPatch
    {
        private static int currentCredits = 0;

        [HarmonyPatch(typeof(StartOfRound), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(StartOfRound __instance)
        {
            // Set all grabbable objects as in ship if the game hasn't started (it never should be unless someone is using a join mid-game mod or something)
            if (!__instance.IsHost && __instance.inShipPhase)
            {
                var allGrabbables = Object.FindObjectsOfType<GrabbableObject>();
                foreach (var grabbable in allGrabbables)
                {
                    grabbable.isInElevator = true;
                    grabbable.isInShipRoom = true;
                    grabbable.scrapPersistedThroughRounds = true;
                }
            }

            // Grab initial credits value if this is the server
            if (__instance.IsServer && Plugin.StartingMoneyPerPlayerVal >= 0 && __instance.inShipPhase && __instance.gameStats.daysSpent == 0)
            {
                currentCredits = Plugin.StartingMoneyPerPlayerVal;
            }
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(SwitchMapMonitorPurpose))]
        [HarmonyPostfix]
        private static void SwitchMapMonitorPurpose(StartOfRound __instance, bool displayInfo)
        {
            var map = __instance.mapScreen;

            if (!displayInfo && !string.IsNullOrWhiteSpace(map?.radarTargets[map.targetTransformIndex]?.name))
            {
                __instance.mapScreenPlayerName.text = $"MONITORING: {__instance.mapScreen.radarTargets[__instance.mapScreen.targetTransformIndex].name}";
            }
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(OnClientConnect))]
        [HarmonyPrefix]
        private static void OnClientConnect(StartOfRound __instance)
        {
            if (__instance.IsServer && Plugin.StartingMoneyPerPlayerVal >= 0 && __instance.inShipPhase && __instance.gameStats.daysSpent == 0)
            {
                // Add to the terminal credits before this function gets called so it is relayed to the connecting client
                Plugin.MLS.LogInfo($"Player connected on day 0, adding {Plugin.StartingMoneyPerPlayerVal} to group credits");
                currentCredits += Plugin.StartingMoneyPerPlayerVal;
                Object.FindObjectOfType<Terminal>().groupCredits = currentCredits;
            }
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(OnClientDisconnect))]
        [HarmonyPostfix]
        private static void OnClientDisconnect(StartOfRound __instance)
        {
            if (__instance.IsServer && Plugin.StartingMoneyPerPlayerVal >= 0 && __instance.inShipPhase && __instance.gameStats.daysSpent == 0)
            {
                // Subtract from the terminal credits, then sync it to the clients
                Plugin.MLS.LogInfo($"Player disconnected on day 0, subtracting {Plugin.StartingMoneyPerPlayerVal} from group credits");
                currentCredits -= Plugin.StartingMoneyPerPlayerVal;

                // Keep track of negatives to prevent exploits, but do not go below zero on the actual terminal
                var terminal = Object.FindObjectOfType<Terminal>();
                terminal.groupCredits = Mathf.Clamp(currentCredits, 0, int.MaxValue);
                terminal.SyncGroupCreditsServerRpc(terminal.groupCredits, terminal.numberOfItemsInDropship);
            }
        }
    }
}