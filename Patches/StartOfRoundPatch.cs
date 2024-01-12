using GameNetcodeStuff;
using HarmonyLib;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class StartOfRoundPatch
    {
        private static MethodInfo _updatePlayerPositionClientRpcMethod;
        private static MethodInfo UpdatePlayerPositionClientRpcMethod
        {
            get
            {
                // Lazy load and cache reflection info
                if (_updatePlayerPositionClientRpcMethod == null)
                {
                    _updatePlayerPositionClientRpcMethod = typeof(PlayerControllerB).GetMethod("UpdatePlayerPositionClientRpc", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return _updatePlayerPositionClientRpcMethod;
            }
        }

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

            // Rotate ship camera if specified
            if (Plugin.ShipMapCamDueNorth.Value)
            {
                Plugin.MLS.LogInfo("Rotating ship map camera to face north");
                Vector3 curAngles = __instance.mapScreen.mapCamera.transform.eulerAngles;
                __instance.mapScreen.mapCamera.transform.rotation = Quaternion.Euler(curAngles.x, 90, curAngles.z);
            }

            // Resize the two little monitors to about 95% of their existing width and font size
            var deadlineSize = __instance.deadlineMonitorText.rectTransform.sizeDelta;
            var profitQuotaSize = __instance.profitQuotaMonitorText.rectTransform.sizeDelta;
            __instance.deadlineMonitorText.rectTransform.sizeDelta = new Vector2(deadlineSize.x * 0.95f, deadlineSize.y);
            __instance.deadlineMonitorText.fontSize = __instance.deadlineMonitorText.fontSize * 0.95f;
            __instance.profitQuotaMonitorText.rectTransform.sizeDelta = new Vector2(profitQuotaSize.x * 0.95f, profitQuotaSize.y);
            __instance.profitQuotaMonitorText.fontSize = __instance.profitQuotaMonitorText.fontSize * 0.95f;
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(OnShipLandedMiscEvents))]
        [HarmonyPostfix]
        private static void OnShipLandedMiscEvents()
        {
            RoundManagerPatch.EnableAndAttachShipScanNode();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(ShipLeave))]
        [HarmonyPrefix]
        private static void ShipLeave()
        {
            // Easter egg
            RoundManagerPatch.CurShipNode.subText = "BYE LOL";
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(ShipHasLeft))]
        [HarmonyPrefix]
        private static void ShipHasLeft()
        {
            // Manually destroy the node here since it won't be after attaching it to the ship
            Object.Destroy(RoundManagerPatch.CurShipNode.gameObject);
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
            if (__instance.IsServer)
            {
                // Add to the terminal credits before this function gets called so it is relayed to the connecting client
                TerminalPatch.AdjustGroupCredits(true);

                // Send positional, rotational, and emotional (heh) data to all when new people connect
                foreach (var connectedPlayer in __instance.allPlayerScripts.Where(p => p.isPlayerControlled))
                {
                    UpdatePlayerPositionClientRpcMethod.Invoke(connectedPlayer, new object[] { connectedPlayer.thisPlayerBody.localPosition, connectedPlayer.isInElevator,
                        connectedPlayer.isInHangarShipRoom, connectedPlayer.isExhausted, connectedPlayer.thisController.isGrounded });

                    if (connectedPlayer.performingEmote)
                    {
                        connectedPlayer.StartPerformingEmoteClientRpc();
                    }
                }
            }
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(OnClientDisconnect))]
        [HarmonyPostfix]
        private static void OnClientDisconnect()
        {
            TerminalPatch.AdjustGroupCredits(false);
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(ResetShip))]
        [HarmonyPostfix]
        private static void ResetShip()
        {
            TerminalPatch.SetStartingMoneyPerPlayer(true);
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(LoadShipGrabbableItems))]
        [HarmonyPostfix]
        private static void LoadShipGrabbableItems()
        {
            UpdateDeadlineMonitorText();
        }

        public static void UpdateDeadlineMonitorText()
        {
            if (!Plugin.ShowShipTotalBelowDeadline.Value)
            {
                return;
            }

            var instance = StartOfRound.Instance;

            if (instance.isChallengeFile)
            {
                // TODO: Put it on a different monitor?
            }
            else
            {
                int shipLoot = Object.FindObjectsOfType<GrabbableObject>().Where(o => o.itemProperties.isScrap && o.isInShipRoom && o.isInElevator).Sum(o => o.scrapValue);
                int days = TimeOfDay.Instance.daysUntilDeadline;
                string deadline = days >= 0 ? $"{days} DAY{(days == 1 ? string.Empty : "S")}" : "NOW";
                instance.deadlineMonitorText.text = $"DEADLINE:\n{deadline}\nIN SHIP:\n${shipLoot}";
            }
        }
    }
}