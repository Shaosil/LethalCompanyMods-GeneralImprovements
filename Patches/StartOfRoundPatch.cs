using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using GeneralImprovements.Utilities;
using HarmonyLib;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using static GeneralImprovements.Enums;

namespace GeneralImprovements.Patches
{
    internal static class StartOfRoundPatch
    {
        private static readonly ProfilerMarker _pm_StartUpdate = new ProfilerMarker(ProfilerCategory.Scripts, "GeneralImprovements.StartOfRound.Update");
        private static readonly ProfilerMarker _pm_StartLateUpdate = new ProfilerMarker(ProfilerCategory.Scripts, "GeneralImprovements.StartOfRound.LateUpdate");
        private static bool _playerInElevatorLastFrame = true;

        public static int DaysSinceLastDeath = -1;
        public static Dictionary<ulong, int> SteamIDsToSuits = new Dictionary<ulong, int>();
        public static HashSet<string> FlownToHiddenMoons = new HashSet<string>();
        public static Dictionary<int, int> DailyScrapCollected = new Dictionary<int, int>(); // Each day's scrap collected

        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Awake_Pre(StartOfRound __instance)
        {
            // Reset med station reference and other helpers
            TerminalPatch._instance = null;
            ObjectHelper.MedStation = null;
            ObjectHelper.ChargeStation = null;
            ObjectHelper.MedStationUnlockableID = -1;
            ObjectHelper.ChargeStationUnlockableID = -1;

            // If we will be creating the med station, make sure it is registered as an unlockable
            if (Plugin.AddHealthRechargeStation.Value)
            {
                ObjectHelper.MedStationUnlockableID = ObjectHelper.AddUnlockable(__instance, "Med Station");
                AssetBundleHelper.MedStationPrefab.GetComponentInChildren<PlaceableShipObject>().unlockableID = ObjectHelper.MedStationUnlockableID;
            }

            // If we allow the item charger to be a placeable, make sure it is registered as an unlockable
            if (Plugin.AllowChargerPlacement.Value)
            {
                ObjectHelper.ChargeStationUnlockableID = ObjectHelper.AddUnlockable(__instance, "Item Charger");
                AssetBundleHelper.ChargeStationPrefab.GetComponentInChildren<PlaceableShipObject>().unlockableID = ObjectHelper.ChargeStationUnlockableID;
            }
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(Start))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.High)]
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

            // Remove cabinet doors if specified
            if (Plugin.HideShipCabinetDoors.Value)
            {
                Plugin.MLS.LogInfo("Removing ship cabinet doors.");
                var cabinet = GameObject.Find("StorageCloset");
                if (cabinet?.transform.GetChild(0)?.GetChild(0)?.GetComponent<InteractTrigger>() != null
                    && cabinet?.transform.GetChild(1)?.GetChild(0)?.GetComponent<InteractTrigger>() != null)
                {
                    Object.Destroy(cabinet.transform.GetChild(0).gameObject);
                    Object.Destroy(cabinet.transform.GetChild(1).gameObject);
                }
                else
                {
                    Plugin.MLS.LogError("Could not find storage closet doors!");
                }
            }

            // Rotate ship camera if specified
            if (Plugin.ShipMapCamRotation.Value != Enums.eShipCamRotation.None)
            {
                Plugin.MLS.LogInfo($"Rotating ship map camera to face {Plugin.ShipMapCamRotation.Value}.");
                Vector3 curAngles = __instance.mapScreen.mapCamera.transform.eulerAngles;
                float rotationAngle = 90 * (int)Plugin.ShipMapCamRotation.Value;
                __instance.mapScreen.mapCamera.transform.rotation = Quaternion.Euler(curAngles.x, rotationAngle, curAngles.z);
            }

            // Create monitors if necessary and update the texts needed
            MonitorsHelper.InitializeMonitors(Plugin.ShipMonitorAssignments.Select(a => a.Value).ToArray(), true);
            MonitorsHelper.UpdateTotalDaysMonitors();
            MonitorsHelper.UpdateTotalQuotasMonitors();
            MonitorsHelper.UpdateDeathMonitors();

            // Load item charger and store its original position node Y value
            var chargeStation = Object.FindObjectOfType<ItemCharger>().GetComponent<InteractTrigger>();
            ObjectHelper.OriginalChargeYHeight = chargeStation.playerPositionNode.position.y;

            // Adjust interact trigger box for charger
            var chargeTriggerCollider = chargeStation.GetComponent<BoxCollider>();
            chargeTriggerCollider.center = Vector3.zero;
            chargeTriggerCollider.size = new Vector3(0.5f, 0.7f, 0.8f);

            // Add medical station
            ObjectHelper.CreateMedStation(chargeStation);

            // Allow charging station to be placeable
            ObjectHelper.MakeChargeStationPlaceable(chargeStation);

            // Fix max items allowed to be stored
            __instance.maxShipItemCapacity = 999;

            // Remove the intro sound effects if needed
            if (!Plugin.SpeakerPlaysIntroVoice.Value)
            {
                Plugin.MLS.LogInfo("Nulling out intro audio SFX");
                StartOfRound.Instance.shipIntroSpeechSFX = null;
            }

            // Create the light switch scan node
            if (Plugin.LightSwitchScanNode.Value && Object.FindObjectsOfType<InteractTrigger>().FirstOrDefault(t => t.gameObject.name == "LightSwitch") is InteractTrigger light)
            {
                ObjectHelper.CreateScanNodeOnObject(light.gameObject, 0, 0, 20, "Light Switch");
            }
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(SetTimeAndPlanetToSavedSettings))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SetTimeAndPlanetToSavedSettings(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();
            var loadMethod = typeof(ES3).GetMethods().First(m => m.Name == nameof(ES3.Load) && m.ContainsGenericParameters && m.GetParameters().Length == 3 && m.GetParameters()[2].ParameterType.IsGenericParameter);

            if (codeList.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
            {
                i => i.IsLdarg(0),
                i => i.Is(OpCodes.Ldstr, "RandomSeed"),
                i => i.IsLdloc(),
                i => i.LoadsConstant(0),
                i => i.Calls(loadMethod.MakeGenericMethod(typeof(int))),
                i => i.StoresField(typeof(StartOfRound).GetField(nameof(StartOfRound.randomMapSeed)))
            }, out var found))
            {
                // Instead of using 0 for a default map seed, use Random.Range(1, 100000000)
                codeList[found[3].Index] = new CodeInstruction(OpCodes.Ldc_I4_1);
                codeList.InsertRange(found[4].Index, new[]
                {
                    new CodeInstruction(OpCodes.Ldc_I4, 100000000),
                    new CodeInstruction(OpCodes.Call, typeof(Random).GetMethod(nameof(Random.Range), new[] { typeof(int), typeof(int) }))
                });

                Plugin.MLS.LogDebug("Patched SetTimeAndPlanetToSavedSettings to default the first map seed to a random value.");
            }
            else
            {
                Plugin.MLS.LogError("Could not find expected code in SetTimeAndPlanetToSavedSettings - Unable to patch randomMapSeed!");
            }

            return codeList;
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(OnShipLandedMiscEvents))]
        [HarmonyPostfix]
        private static void OnShipLandedMiscEvents()
        {
            RoundManagerPatch.EnableAndAttachShipScanNode();
            MonitorsHelper.UpdateCalculatedScrapMonitors();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(ShipLeave))]
        [HarmonyPrefix]
        private static void ShipLeave()
        {
            // Easter egg
            if (RoundManagerPatch.CurShipNode != null)
            {
                RoundManagerPatch.CurShipNode.subText = "BYE LOL";
            }
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(ShipHasLeft))]
        [HarmonyPostfix]
        private static void ShipHasLeft()
        {
            // Manually destroy the node here since it won't be after attaching it to the ship
            if (RoundManagerPatch.CurShipNode != null)
            {
                Object.Destroy(RoundManagerPatch.CurShipNode.gameObject);
            }

            // Auto charge owned batteries
            if (Plugin.AutoChargeOnOrbit.Value)
            {
                var itemsToCharge = Object.FindObjectsOfType<GrabbableObject>().Where(o => o.IsOwner && (o.itemProperties?.requiresBattery ?? false) && o.insertedBattery.charge < 1).ToList();
                foreach (var batteryItem in itemsToCharge)
                {
                    batteryItem.insertedBattery = new Battery(false, 1);
                    batteryItem.SyncBatteryServerRpc(100);
                }
                if (itemsToCharge.Any() && StartOfRound.Instance.localPlayerController != null)
                {
                    Plugin.MLS.LogInfo($"Auto charged {itemsToCharge.Count} owned item{(itemsToCharge.Count == 1 ? string.Empty : "s")}.");
                    var zapAudio = Object.FindObjectOfType<ItemCharger>()?.zapAudio?.clip;
                    if (zapAudio != null)
                    {
                        StartOfRound.Instance.localPlayerController.itemAudio.PlayOneShot(zapAudio);
                    }
                }
            }

            // Destroy keys if specified
            if (Plugin.DestroyKeysAfterOrbiting.Value)
            {
                // If we are the host, despawn all keys in the ship
                if (StartOfRound.Instance.IsHost)
                {
                    var allKeys = Object.FindObjectsOfType<KeyItem>().Where(k => !k.isHeld && k.isInShipRoom).ToList();
                    Plugin.MLS.LogInfo($"Destroying {allKeys.Count} keys in ship after orbiting.");
                    for (int i = 0; i < allKeys.Count; i++)
                    {
                        allKeys[i].NetworkObject.Despawn();
                    }
                }

                // Destroy keys in our own inventory
                for (int i = 0; i < StartOfRound.Instance.localPlayerController.ItemSlots.Length; i++)
                {
                    if (StartOfRound.Instance.localPlayerController.ItemSlots[i] is KeyItem key)
                    {
                        Plugin.MLS.LogInfo($"Destroying held key in slot {i} after orbiting.");
                        ObjectHelper.DestroyLocalItemAndSync(i);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ChangeLevel))]
        [HarmonyPostfix]
        private static void ChangeLevel(StartOfRound __instance)
        {
            // If we're flying to a hidden moon (not in the moonsCatalogueList, keep track of it. The host will also save that value in their save file.
            if (Plugin.ShowHiddenMoonsInCatalog.Value == Enums.eShowHiddenMoons.AfterDiscovery && !TerminalPatch.Instance.moonsCatalogueList.Contains(__instance.currentLevel))
            {
                FlownToHiddenMoons.Add(__instance.currentLevel.PlanetName);
            }
        }

        [HarmonyPatch(typeof(StartOfRound), "TravelToLevelEffects", MethodType.Enumerator)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TravelToLevelEffects_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.AllowPreGameLeverPullAsClient.Value)
            {
                if (codeList.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                    i => i.Calls(AccessTools.Method(typeof(StartOfRound), nameof(StartOfRound.ArriveAtLevel))),

                    i => i.IsLdloc(),
                    i => i.Calls(AccessTools.Method(typeof(NetworkBehaviour), "get_IsServer")),
                    i => i.Branches(out _),
                    i => i.Calls(AccessTools.Method(typeof(GameNetworkManager), "get_Instance")),
                    i => i.LoadsField(AccessTools.Field(typeof(GameNetworkManager), nameof(GameNetworkManager.gameHasStarted))),
                    i => i.Branches(out _)
                }, out var gameStartedCode))
                {
                    Plugin.MLS.LogDebug("Patching StartOfRound.TravelToLevelEffects to allow AllowPreGameLeverPullAsClient to work properly.");

                    // Remove the if statement entirely
                    codeList.RemoveRange(gameStartedCode[1].Index, gameStartedCode.Length - 1);
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL Code - Could not patch StartOfRound.TravelToLevelEffects to allow AllowPreGameLeverPullAsClient to work properly!");
                }
            }

            return codeList;
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(SetMapScreenInfoToCurrentLevel))]
        [HarmonyPostfix]
        private static void SetMapScreenInfoToCurrentLevel()
        {
            // Update weather monitor text
            MonitorsHelper.UpdateWeatherMonitors();
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
        private static void OnClientConnect(StartOfRound __instance, ulong clientId)
        {
            if (__instance.IsServer)
            {
                // Add to the terminal credits before this function gets called so it is relayed to the connecting client
                if (Plugin.StartingMoneyFunction.Value == eStartingMoneyFunction.PerPlayer || Plugin.StartingMoneyFunction.Value == eStartingMoneyFunction.PerPlayerWithMinimum)
                {
                    TerminalPatch.AdjustGroupCredits(true);
                }

                // Send positional, rotational, emotional (heh), light, and monitor power data to all when new people connect
                foreach (var connectedPlayer in __instance.allPlayerScripts.Where(p => p.isPlayerControlled))
                {
                    connectedPlayer.UpdatePlayerPositionClientRpc(connectedPlayer.thisPlayerBody.localPosition, connectedPlayer.isInElevator,
                        connectedPlayer.isInHangarShipRoom, connectedPlayer.isExhausted, connectedPlayer.thisController.isGrounded);

                    connectedPlayer.UpdatePlayerRotationClientRpc((short)connectedPlayer.cameraUp, (short)connectedPlayer.thisPlayerBody.localEulerAngles.y);

                    if (connectedPlayer.performingEmote)
                    {
                        connectedPlayer.StartPerformingEmoteClientRpc();
                    }

                    __instance.shipRoomLights.SetShipLightsClientRpc(__instance.shipRoomLights.areLightsOn);

                    __instance.mapScreen.SwitchScreenOnClientRpc(__instance.mapScreen.isScreenOn);
                }

                // Send any custom network signals to the client if they also use this mod
                if (NetworkHelper.Instance != null)
                {
                    var clientParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } };

                    // Send over our monitor information in case the client wants to sync from the host
                    Plugin.MLS.LogInfo("Server sending monitor information RPC.");
                    NetworkHelper.Instance.SyncMonitorsFromHostClientRpc(Plugin.UseBetterMonitors.Value, Plugin.ShipMonitorAssignments[0].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[1].Value.ToString() ?? null,
                        Plugin.ShipMonitorAssignments[2].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[3].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[4].Value.ToString() ?? null,
                        Plugin.ShipMonitorAssignments[5].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[6].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[7].Value.ToString() ?? null,
                        Plugin.ShipMonitorAssignments[8].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[9].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[10].Value.ToString() ?? null,
                        Plugin.ShipMonitorAssignments[11].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[12].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[13].Value.ToString() ?? null, clientParams);

                    // Send over the extra info about this quota to this client
                    if (__instance?.gameStats != null)
                    {
                        var stats = __instance.gameStats;
                        string foundMoons = string.Join(',', FlownToHiddenMoons);
                        Plugin.MLS.LogInfo("Server sending extra data sync RPC.");
                        var convertedDailyScrap = new List<int[]> { DailyScrapCollected.Keys.ToArray(), DailyScrapCollected.Values.ToArray() };
                        NetworkHelper.Instance.SyncExtraDataOnConnectClientRpc(TimeOfDay.Instance.timesFulfilledQuota, stats.daysSpent, stats.deaths, DaysSinceLastDeath, foundMoons, convertedDailyScrap[0], convertedDailyScrap[1], clientParams);
                    }

                    // Send over color information about existing spray cans
                    var sprayCanMatIndexes = SprayPaintItemPatch.GetAllOrderedSprayPaintItemsInShip().Select(s => s.sprayCanMatsIndex).ToArray();
                    if (sprayCanMatIndexes.Any())
                    {
                        Plugin.MLS.LogInfo($"Server sending {sprayCanMatIndexes.Length} spray can colors RPC.");
                        NetworkHelper.Instance.SyncSprayPaintItemColorsClientRpc(sprayCanMatIndexes);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(OnPlayerConnectedClientRpc))]
        [HarmonyPostfix]
        private static void OnPlayerConnectedClientRpc(StartOfRound __instance, ulong clientId)
        {
            // When we receive this as our own initial join to a host, make sure we add everyone who is already here into the fullyLoadedPlayers collection
            if (Plugin.AllowPreGameLeverPullAsClient.Value && !__instance.IsHost && NetworkManager.Singleton.LocalClientId == clientId)
            {
                foreach (var player in __instance.allPlayerScripts.Where(p => !p.IsOwner && p.isPlayerControlled))
                {
                    __instance.fullyLoadedPlayers.Add(player.actualClientId);
                }
            }

            if (GameNetworkManager.Instance.disableSteam)
            {
                // Need a specific call to health monitors for LAN situations
                MonitorsHelper.UpdatePlayerHealthMonitors();
            }

            MonitorsHelper.UpdatePlayersAliveMonitors();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnClientDisconnect))]
        [HarmonyPostfix]
        private static void OnClientDisconnect()
        {
            if (Plugin.StartingMoneyFunction.Value == eStartingMoneyFunction.PerPlayer || Plugin.StartingMoneyFunction.Value == eStartingMoneyFunction.PerPlayerWithMinimum)
            {
                TerminalPatch.AdjustGroupCredits(false);
            }
            MonitorsHelper.UpdatePlayerHealthMonitors();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.OnClientDisconnectClientRpc))]
        [HarmonyPostfix]
        private static void OnClientDisconnectClientRpc()
        {
            MonitorsHelper.UpdatePlayersAliveMonitors();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(SyncShipUnlockablesClientRpc))]
        [HarmonyPostfix]
        private static void SyncShipUnlockablesClientRpc()
        {
            MonitorsHelper.UpdateCalculatedScrapMonitors();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(ResetShipFurniture))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ResetShipFurniture(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.SaveShipFurniturePlaces.Value != Enums.eSaveFurniturePlacement.None)
            {
                Label? elseLabel = null;
                if (codeList.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                    i => i.LoadsConstant(0),
                    i => i.StoresField(typeof(ShipTeleporter).GetField(nameof(ShipTeleporter.hasBeenSpawnedThisSession))),
                    i => i.LoadsConstant(0),
                    i => i.StoresField(typeof(ShipTeleporter).GetField(nameof(ShipTeleporter.hasBeenSpawnedThisSessionInverse))),
                    i => i.IsLdarg(1),
                    i => i.Branches(out elseLabel)
                }, out var ifBlock)
                && codeList.TryFindInstruction(new System.Func<CodeInstruction, bool>(i => i.labels.Contains(elseLabel.Value)), out var elseBlock))
                {
                    Plugin.MLS.LogDebug("Patching StartOfRound.ResetShipFurniture to save furniture state.");

                    // NOP the entire if statement
                    for (int i = ifBlock[4].Index; i < elseBlock.Index; i++)
                    {
                        codeList[i] = new CodeInstruction(OpCodes.Nop);
                    }
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL code - Could not transpile StartOfRound.ResetShipFurniture to save furniture state!");
                }
            }

            return codeList;
        }

        [HarmonyPatch(typeof(StartOfRound), "EndOfGame", MethodType.Enumerator)]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> EndOfGame_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            // Prevent the call to set a new profit quota every day if we are over
            if (Plugin.AllowQuotaRollover.Value)
            {
                Label? outsideBlock = null;

                if (codeList.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                    i => i.opcode == OpCodes.Ldloc_1,
                    i => i.LoadsField(typeof(StartOfRound).GetField(nameof(StartOfRound.isChallengeFile))),
                    i => i.Branches(out outsideBlock),
                    i => i.Calls(typeof(TimeOfDay).GetMethod("get_Instance")),
                    i => i.Calls(typeof(TimeOfDay).GetMethod(nameof(TimeOfDay.SetNewProfitQuota)))
                }, out var found))
                {
                    Plugin.MLS.LogDebug("Patching StartOfRound.EndOfGame to prevent a new quota from being set every day if there is rollover profit.");
                    codeList.InsertRange(found[3].Index, new CodeInstruction[]
                    {
                        new CodeInstruction(OpCodes.Call, typeof(TimeOfDay).GetMethod("get_Instance")),
                        // Return true (should call) if we are either over our deadline or have sold items today
                        Transpilers.EmitDelegate<System.Func<TimeOfDay, bool>>(tod => tod.timeUntilDeadline <= 0 || DepositItemsDeskPatch.NumItemsSoldToday > 0),
                        new CodeInstruction(OpCodes.Brfalse_S, outsideBlock)
                    });
                }
                else
                {
                    Plugin.MLS.LogWarning("Unexpected IL Code - Could not patch StartOfRound.EndOfGame to support quota rollover!");
                }
            }

            return codeList;
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(PassTimeToNextDay))]
        [HarmonyPrefix]
        private static void PassTimeToNextDay(StartOfRound __instance)
        {
            if (__instance.isChallengeFile)
            {
                FullQuotaReset(__instance);
            }
            else if (__instance.currentLevel.planetHasTime && TimeOfDay.Instance.timeUntilDeadline > 0)
            {
                DailyScrapCollected[__instance.gameStats.daysSpent] = __instance.scrapCollectedLastRound;
                MonitorsHelper.UpdateAverageDailyScrapMonitors();
            }
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.EndPlayersFiredSequenceClientRpc))]
        [HarmonyPostfix]
        private static void EndPlayersFiredSequenceClientRpc(StartOfRound __instance)
        {
            FullQuotaReset(__instance);
        }

        private static void FullQuotaReset(StartOfRound instance)
        {
            // Reset money and max health
            TerminalPatch.SetStartingMoneyPerPlayer();
            PlayerControllerBPatch.CurrentMaxHealth = instance.localPlayerController.health;

            // Update monitors that may need it
            DaysSinceLastDeath = -1;
            DailyScrapCollected = new Dictionary<int, int>();
            MonitorsHelper.UpdateAverageDailyScrapMonitors();
            MonitorsHelper.UpdateDailyProfitMonitors();
            MonitorsHelper.UpdateDeathMonitors(false);
            MonitorsHelper.UpdateCalculatedScrapMonitors();
            MonitorsHelper.UpdateTotalDaysMonitors();
            MonitorsHelper.UpdateTotalQuotasMonitors();

            // If we are the host, update everyone's suits since they have been reset
            if (Plugin.SavePlayerSuits.Value && instance.IsHost)
            {
                foreach (var player in instance.allPlayerScripts)
                {
                    PlayerControllerBPatch.UpdatePlayerSuitToSavedValue(player);
                }
            }
        }

        [HarmonyPatch(typeof(StartOfRound), "StartGame")]
        [HarmonyPatch(typeof(StartOfRound), nameof(ResetPlayersLoadedValueClientRpc))]
        [HarmonyPostfix]
        private static void ResetPlayersLoadedValueClientRpc()
        {
            StartMatchLeverPatch.Instance.triggerScript.disabledHoverTip = "[Ship in motion]";
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(ReviveDeadPlayers))]
        [HarmonyPrefix]
        private static void ReviveDeadPlayers(StartOfRound __instance)
        {
            bool playersDied = __instance.allPlayerScripts.Any(p => p.isPlayerDead);
            MonitorsHelper.UpdateDeathMonitors(playersDied);
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(AllPlayersHaveRevivedClientRpc))]
        [HarmonyPostfix]
        private static void AllPlayersHaveRevivedClientRpc()
        {
            // Reset any necessary after-orbit variables
            DepositItemsDeskPatch.NumItemsSoldToday = 0;
            MaskedPlayerEnemyPatch.NumSpawnedThisLevel = 0;
            EnemyAIPatch.CurTotalPowerLevel = 0;

            // Reset some monitors
            MonitorsHelper.UpdateCalculatedScrapMonitors();
            MonitorsHelper.UpdateTotalDaysMonitors();
            MonitorsHelper.UpdateTotalQuotasMonitors();
            MonitorsHelper.UpdatePlayerHealthMonitors();
            MonitorsHelper.UpdateDangerLevelMonitors();
            MonitorsHelper.UpdatePlayersAliveMonitors();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.SyncCompanyBuyingRateClientRpc))]
        [HarmonyPostfix]
        private static void SyncCompanyBuyingRateClientRpc()
        {
            MonitorsHelper.UpdateCompanyBuyRateMonitors();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(LoadShipGrabbableItems))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> LoadShipGrabbableItemsTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            if (!Plugin.FixItemsLoadingSameRotation.Value)
            {
                return instructions;
            }

            // This transpiler will add a new array of Vector3, fill it with rotation values from save file, and apply it to the game objects that are spawned
            var codeList = instructions.ToList();
            var newArrayType = typeof(Vector3[]);
            var instantiateMethod = typeof(Object).GetMethods().First(m => m.Name == nameof(Object.Instantiate) && m.ContainsGenericParameters && m.GetParameters().Length == 4).MakeGenericMethod(typeof(GameObject));
            var genLoadMethods = typeof(ES3).GetMethods().Where(m => m.Name == nameof(ES3.Load) && m.IsGenericMethod).ToList();
            var theirLoad = genLoadMethods.First(m => m.GetParameters().Length == 2 && m.GetParameters().All(p => p.ParameterType == typeof(string))).MakeGenericMethod(typeof(Vector3[]));
            var ourLoad = genLoadMethods.First(m => m.GetParameters().Length == 3 && m.GetParameters()[2].ParameterType.IsGenericParameter).MakeGenericMethod(typeof(Vector3[]));

            bool foundLoad = codeList.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
            {
                i => i.Is(OpCodes.Ldstr, "shipGrabbableItemPos"),
                i => i.Calls(typeof(GameNetworkManager).GetMethod("get_Instance")),
                i => i.LoadsField(typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.currentSaveFileName))),
                i => i.Calls(theirLoad),
                i => i.IsStloc()
            }, out var loadPositions);

            bool foundRotate = codeList.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                    i => i.Calls(typeof(Quaternion).GetMethod("get_identity")),
                    i => i.IsLdarg(),
                    i => i.LoadsField(typeof(StartOfRound).GetField(nameof(StartOfRound.elevatorTransform))),
                    i => i.Calls(instantiateMethod)
                }, out var rotateCode);

            // Make sure a few lines of the IL code is what we expect first
            if (foundLoad && foundRotate)
            {
                Plugin.MLS.LogDebug("Patching LoadShipGrabbableItems to include item rotations.");

                // Ensure we have a new variable slot to store our array
                generator.DeclareLocal(newArrayType);

                // Inject a new array variable loaded with our save file's rotations
                codeList.InsertRange(loadPositions.Last().Index + 1, new[]
                {
                    new CodeInstruction(OpCodes.Ldstr, "shipGrabbableItemRot"),
                    new CodeInstruction(OpCodes.Call, typeof(GameNetworkManager).GetMethod("get_Instance")),
                    new CodeInstruction(OpCodes.Ldfld, typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.currentSaveFileName))),
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldlen),
                    new CodeInstruction(OpCodes.Newarr, typeof(Vector3)),
                    new CodeInstruction(OpCodes.Call, ourLoad),
                    new CodeInstruction(OpCodes.Stloc_S, 11)
                });

                // Inject code in our instantiate call to use Quaternion.Euler instead of Quaternion.Identity
                codeList.InsertRange(rotateCode.First().Index + 8, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, 11),   // Our new array
                    new CodeInstruction(OpCodes.Ldloc_S, 9),    // i
                    new CodeInstruction(OpCodes.Ldelem, typeof(Vector3))  // Get the Vector3 value
                });
                rotateCode.First().Instruction.operand = typeof(Quaternion).GetMethod(nameof(Quaternion.Euler), new[] { typeof(Vector3) });
            }
            else
            {
                Plugin.MLS.LogError("Could not transpile LoadShipGrabbableItems! Unexpected IL code found.");
            }

            return codeList;
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(LoadShipGrabbableItems))]
        [HarmonyPostfix]
        private static void LoadShipGrabbableItems()
        {
            MonitorsHelper.UpdateCalculatedScrapMonitors();

            // Also load any extra item info we've saved
            if (ES3.KeyExists("sprayPaintItemColors", GameNetworkManager.Instance.currentSaveFileName))
            {
                var sprayCans = SprayPaintItemPatch.GetAllOrderedSprayPaintItemsInShip();
                var orderedColors = ES3.Load<int[]>("sprayPaintItemColors", GameNetworkManager.Instance.currentSaveFileName);

                for (int i = 0; i < sprayCans.Length && i < orderedColors.Length; i++)
                {
                    SprayPaintItemPatch.UpdateColor(sprayCans[i], orderedColors[i]);
                }
            }

            // Load custom stats
            var anyDeaths = StartOfRound.Instance.gameStats.deaths > 0;
            DaysSinceLastDeath = ES3.Load("Stats_DaysSinceLastDeath", GameNetworkManager.Instance.currentSaveFileName, anyDeaths ? 0 : -1);
            DailyScrapCollected = ES3.Load("Stats_AverageDailyScrap", GameNetworkManager.Instance.currentSaveFileName, new Dictionary<int, int>());
            if (Plugin.ShowHiddenMoonsInCatalog.Value == Enums.eShowHiddenMoons.AfterDiscovery)
            {
                FlownToHiddenMoons = new HashSet<string>();
                string foundMoons = ES3.Load("DiscoveredMoons", GameNetworkManager.Instance.currentSaveFileName, string.Empty);
                foreach (string foundMoon in foundMoons.Split(',', System.StringSplitOptions.RemoveEmptyEntries))
                {
                    FlownToHiddenMoons.Add(foundMoon);
                }
            }

            // Load player suits (assign to host after player Steam ID is set)
            if (Plugin.SavePlayerSuits.Value)
            {
                SteamIDsToSuits = ES3.Load("SteamIDsToSuitIDs", GameNetworkManager.Instance.currentSaveFileName, new Dictionary<ulong, int>());
            }
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(Update))]
        [HarmonyPostfix]
        private static void Update()
        {
            ProfilerHelper.BeginProfilingSafe(_pm_StartUpdate);

            MonitorsHelper.AnimateSpecialMonitors();
            MonitorsHelper.UpdateCreditsMonitors();
            MonitorsHelper.UpdateDoorPowerMonitors();

            ProfilerHelper.EndProfilingSafe(_pm_StartUpdate);
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(LateUpdate))]
        [HarmonyPostfix]
        private static void LateUpdate(StartOfRound __instance)
        {
            ProfilerHelper.BeginProfilingSafe(_pm_StartLateUpdate);

            // Keep track of "in elevator" changes and refresh monitors when needed
            if (__instance.localPlayerController != null && _playerInElevatorLastFrame != __instance.localPlayerController.isInElevator)
            {
                _playerInElevatorLastFrame = __instance.localPlayerController.isInElevator;
                if (_playerInElevatorLastFrame)
                {
                    MonitorsHelper.RefreshQueuedMonitorChanges();
                }
            }

            ProfilerHelper.EndProfilingSafe(_pm_StartLateUpdate);
        }
    }
}