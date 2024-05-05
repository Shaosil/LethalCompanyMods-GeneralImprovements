using BepInEx.Configuration;
using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Netcode;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class StartOfRoundPatch
    {
        private static bool _playerInElevatorLastFrame = true;

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

        public static int DaysSinceLastDeath = -1;
        public static Dictionary<ulong, int> SteamIDsToSuits = new Dictionary<ulong, int>();
        public static HashSet<string> FlownToHiddenMoons = new HashSet<string>();

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
            if (Plugin.ShipMapCamDueNorth.Value)
            {
                Plugin.MLS.LogInfo("Rotating ship map camera to face north");
                Vector3 curAngles = __instance.mapScreen.mapCamera.transform.eulerAngles;
                __instance.mapScreen.mapCamera.transform.rotation = Quaternion.Euler(curAngles.x, 90, curAngles.z);
            }

            // Create monitors if necessary and update the texts needed
            MonitorsHelper.InitializeMonitors(Plugin.ShipMonitorAssignments.Select(a => a.Value).ToArray());
            MonitorsHelper.UpdateTotalDaysMonitors();
            MonitorsHelper.UpdateTotalQuotasMonitors();
            MonitorsHelper.UpdateDeathMonitors();

            // Add medical charging station
            ObjectHelper.CreateMedStation();

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
            if (Plugin.ShowHiddenMoonsInCatalog.Value == Plugin.Enums.eShowHiddenMoons.AfterDiscovery && !TerminalPatch.Instance.moonsCatalogueList.Contains(__instance.currentLevel))
            {
                FlownToHiddenMoons.Add(__instance.currentLevel.PlanetName);
            }
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

                // Send any custom network signals to the client if they also use this mod
                if (NetworkHelper.Instance != null)
                {
                    var clientParams = new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } };

                    // Send over the extra info about this quota to this client
                    if (__instance?.gameStats != null)
                    {
                        var stats = __instance.gameStats;
                        string foundMoons = string.Join(',', FlownToHiddenMoons);
                        Plugin.MLS.LogInfo("Server sending extra data sync RPC.");
                        NetworkHelper.Instance.SyncExtraDataOnConnectClientRpc(TimeOfDay.Instance.timesFulfilledQuota, stats.daysSpent, stats.deaths, DaysSinceLastDeath, foundMoons, clientParams);
                    }

                    // Send over our monitor information in case the client wants to sync from the host
                    Plugin.MLS.LogInfo("Server sending monitor information RPC.");
                    NetworkHelper.Instance.SyncMonitorsFromHostClientRpc(Plugin.UseBetterMonitors.Value, Plugin.ShipMonitorAssignments[0].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[1].Value.ToString() ?? null,
                        Plugin.ShipMonitorAssignments[2].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[3].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[4].Value.ToString() ?? null,
                        Plugin.ShipMonitorAssignments[5].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[6].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[7].Value.ToString() ?? null,
                        Plugin.ShipMonitorAssignments[8].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[9].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[10].Value.ToString() ?? null,
                        Plugin.ShipMonitorAssignments[11].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[12].Value.ToString() ?? null, Plugin.ShipMonitorAssignments[13].Value.ToString() ?? null, clientParams);

                    // Send over color information about existing spray cans
                    var sprayCanMatIndexes = SprayPaintItemPatch.GetAllOrderedSprayPaintItemsInShip().Select(s => SprayPaintItemPatch.GetColorIndex(s)).ToArray();
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
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(OnPlayerDC))]
        [HarmonyPostfix]
        private static void OnPlayerDC()
        {
            TerminalPatch.AdjustGroupCredits(false);
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(SyncShipUnlockablesClientRpc))]
        [HarmonyPostfix]
        private static void SyncShipUnlockablesClientRpc()
        {
            MonitorsHelper.UpdateShipScrapMonitors();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(ResetShipFurniture))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ResetShipFurniture(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.SaveFurnitureState.Value)
            {
                Label? elseBlock = null;
                if (instructions.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                    i => i.LoadsConstant(0),
                    i => i.StoresField(typeof(ShipTeleporter).GetField(nameof(ShipTeleporter.hasBeenSpawnedThisSession))),
                    i => i.LoadsConstant(0),
                    i => i.StoresField(typeof(ShipTeleporter).GetField(nameof(ShipTeleporter.hasBeenSpawnedThisSessionInverse))),
                    i => i.IsLdarg(1),
                    i => i.Branches(out elseBlock)
                }, out var found))
                {
                    Plugin.MLS.LogDebug("Patching StartOfRound.ResetShipFurniture to save furniture state.");

                    codeList.InsertRange(found.Last().Index + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Call, typeof(Plugin).GetMethod($"get_{nameof(Plugin.SaveFurnitureState)}")),
                        new CodeInstruction(OpCodes.Callvirt, typeof(ConfigEntry<bool>).GetMethod("get_Value")),
                        new CodeInstruction(OpCodes.Brtrue_S, elseBlock)
                    });
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL code - Could not transpile StartOfRound.ResetShipFurniture to save furniture state!");
                }
            }

            return codeList;
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.EndPlayersFiredSequenceClientRpc))]
        [HarmonyPostfix]
        private static void EndPlayersFiredSequenceClientRpc(StartOfRound __instance)
        {
            // Reset money and health
            TerminalPatch.SetStartingMoneyPerPlayer();
            PlayerControllerBPatch.PlayerMaxHealthValues = new Dictionary<PlayerControllerB, int>();
            foreach (var player in __instance.allPlayerScripts.Where(p => p.isPlayerControlled))
            {
                Plugin.MLS.LogInfo($"Resetting player {player.playerUsername}'s health to {player.health}.");
                PlayerControllerBPatch.PlayerMaxHealthValues[player] = player.health;
            }

            // Update monitors that may need it
            MonitorsHelper.UpdateScrapLeftMonitors();
            MonitorsHelper.UpdateTotalDaysMonitors();
            MonitorsHelper.UpdateTotalQuotasMonitors();
            DaysSinceLastDeath = -1;
            MonitorsHelper.UpdateDeathMonitors(false);

            // If we are the host, update everyone's suits since they have been reset
            if (Plugin.SavePlayerSuits.Value && StartOfRound.Instance.IsHost)
            {
                foreach (var player in StartOfRound.Instance.allPlayerScripts)
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

            // Reset some monitors
            MonitorsHelper.UpdateShipScrapMonitors();
            MonitorsHelper.UpdateScrapLeftMonitors();
            MonitorsHelper.UpdateTotalDaysMonitors();
            MonitorsHelper.UpdateTotalQuotasMonitors();
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
            MonitorsHelper.UpdateShipScrapMonitors();

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
            if (Plugin.ShowHiddenMoonsInCatalog.Value == Plugin.Enums.eShowHiddenMoons.AfterDiscovery)
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
            MonitorsHelper.AnimateSpecialMonitors();
            MonitorsHelper.UpdateCreditsMonitors();
            MonitorsHelper.UpdateDoorPowerMonitors();
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(LateUpdate))]
        [HarmonyPostfix]
        private static void LateUpdate(StartOfRound __instance)
        {
            // Keep track of "in elevator" changes and refresh monitors when needed
            if (__instance.localPlayerController != null && _playerInElevatorLastFrame != __instance.localPlayerController.isInElevator)
            {
                _playerInElevatorLastFrame = __instance.localPlayerController.isInElevator;
                if (_playerInElevatorLastFrame)
                {
                    MonitorsHelper.RefreshQueuedMonitorChanges();
                }
            }
        }
    }
}