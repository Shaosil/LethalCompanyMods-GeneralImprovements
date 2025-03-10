using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GeneralImprovements.Items;
using GeneralImprovements.Utilities;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class GameNetworkManagerPatch
    {
        public static void PatchNetcode()
        {
            var methods = Assembly.GetExecutingAssembly().GetTypes().SelectMany(t => t.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                .Where(m => m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>(false) != null).ToList();

            methods.ForEach(m => m.Invoke(null, null));
        }

        [HarmonyPatch(typeof(GameNetworkManager), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(GameNetworkManager __instance)
        {
            if (Plugin.AddHealthRechargeStation.Value && AssetBundleHelper.MedStationPrefab != null)
            {
                AssetBundleHelper.MedStationPrefab.AddComponent<MedStationItem>();
                NetworkManager.Singleton.AddNetworkPrefab(AssetBundleHelper.MedStationPrefab);
            }

            if (Plugin.AllowChargerPlacement.Value && AssetBundleHelper.ChargeStationPrefab != null)
            {
                AssetBundleHelper.ChargeStationPrefab.AddComponent<CustomChargeStation>();
                NetworkManager.Singleton.AddNetworkPrefab(AssetBundleHelper.ChargeStationPrefab);
            }

            if (Plugin.RadarBoostersCanBeTeleported.Value != Enums.eRadarBoosterTeleport.Disabled)
            {
                var boosterPrefab = NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs.FirstOrDefault(p => p.Prefab.TryGetComponent<RadarBoosterItem>(out _));
                if (boosterPrefab != null)
                {
                    boosterPrefab.Prefab.AddComponent<TeleportableRadarBooster>();
                }
            }

            if (Plugin.AllowFancyLampToBeToggled.Value)
            {
                ObjectHelper.AlterFancyLampPrefab();
            }

            // Attach our own network helper to this gameobject
            __instance.gameObject.AddComponent<NetworkHelper>();
            __instance.gameObject.AddComponent<NetworkObject>();

            var allItems = NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs
                .Where(p => p.Prefab.TryGetComponent<GrabbableObject>(out var go) && go.itemProperties != null)
                .Select(p => p.Prefab.GetComponent<GrabbableObject>().itemProperties).ToList();
            var nonConductiveItems = new string[] { "Control pad", "Soccer ball", "Toilet paper", "Zed Dog" };
            var tools = new string[] { "Extension ladder", "Jetpack", "Key", "Radar-booster", "Shovel", "Stop sign", "TZP-Inhalant", "Yield sign", "Kitchen knife", "Zap gun" };
            foreach (var item in allItems)
            {
                // Allow all items to be grabbed before game start
                if (!item.canBeGrabbedBeforeGameStart && Plugin.AllowPickupOfAllItemsPreStart.Value)
                {
                    item.canBeGrabbedBeforeGameStart = true;
                }

                // Fix conductivity of certain objects
                if (nonConductiveItems.Any(n => item.itemName.Equals(n, StringComparison.OrdinalIgnoreCase))
                    || (Plugin.ToolsDoNotAttractLightning.Value && tools.Any(t => item.itemName.Equals(t, StringComparison.OrdinalIgnoreCase))))
                {
                    Plugin.MLS.LogInfo($"Item {item.itemName} being set to NON conductive.");
                    item.isConductiveMetal = false;
                }

                // Fix any min and max values being reversed
                if (item.minValue > item.maxValue)
                {
                    int oldMin = item.minValue;
                    item.minValue = item.maxValue;
                    item.maxValue = oldMin;
                }

                // Fix gold bar rotation
                if (item.itemName == "Gold bar")
                {
                    item.restingRotation = new Vector3(-90, 0, 0);
                }
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), nameof(Disconnect))]
        [HarmonyPrefix]
        private static void Disconnect()
        {
            // If we are about to disconnect as a host, first "drop" all held items so they don't save in mid air
            if (StartOfRound.Instance && StartOfRound.Instance.IsHost)
            {
                var allHeldItems = UnityEngine.Object.FindObjectsOfType<GrabbableObject>().Where(g => g.isHeld).ToList();
                foreach (var heldItem in allHeldItems)
                {
                    Plugin.MLS.LogInfo($"Server disconnecting - dropping {heldItem.name}");
                    heldItem.transform.position = heldItem.GetItemFloorPosition();
                }
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), nameof(SaveItemsInShip))]
        [HarmonyTranspiler]
        [HarmonyBefore(OtherModHelper.MattyFixesGUID)]
        private static IEnumerable<CodeInstruction> SaveItemsInShipTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            if (!Plugin.FixItemsLoadingSameRotation.Value)
            {
                return instructions;
            }

            // This transpiler will add a new list of Vector3, fill it with rotation values of all saved items, and persist it to the current save file
            var codeList = instructions.ToList();
            var newListType = typeof(List<Vector3>);
            var saveMethod = typeof(ES3).GetMethods().First(m => m.Name == nameof(ES3.Save) && m.ContainsGenericParameters && m.GetParameters().Length == 3).MakeGenericMethod(typeof(Vector3[]));

            bool foundDelete = codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
            {
                i => i.opcode == OpCodes.Ldstr && i.operand.ToString() == "shipGrabbableItemIDs",
                i => i.IsLdarg(),
                i => i.LoadsField(typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.currentSaveFileName))),
                i => i.Calls(typeof(ES3).GetMethod(nameof(ES3.DeleteKey), new[] { typeof(string), typeof(string) }))
            }, out var deleteInstructions);

            bool foundNewLists = codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.Is(OpCodes.Newobj, typeof(List<int>).GetConstructor(Type.EmptyTypes)),
                    i => i.IsStloc(),
                    i => i.Is(OpCodes.Newobj, typeof(List<Vector3>).GetConstructor(Type.EmptyTypes)),
                    i => i.IsStloc(),
                    i => i.Is(OpCodes.Newobj, typeof(List<int>).GetConstructor(Type.EmptyTypes)),
                    i => i.IsStloc(),
                    i => i.Is(OpCodes.Newobj, typeof(List<int>).GetConstructor(Type.EmptyTypes)),
                    i => i.IsStloc()
                }, out var newLists);

            bool foundAddPos = codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.opcode == OpCodes.Ldelem_Ref,
                    i => i.Calls(typeof(Component).GetMethod("get_transform")),
                    i => i.Calls(typeof(Transform).GetMethod("get_position")),
                    i => i.Calls(typeof(List<Vector3>).GetMethod(nameof(List<Vector3>.Add)))
                }, out var addPosition);

            bool foundSavePos = codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.Is(OpCodes.Ldstr, "shipGrabbableItemPos"),
                    i => i.IsLdloc(),
                    i => i.Calls(typeof(List<Vector3>).GetMethod(nameof(List<Vector3>.ToArray))),
                    i => i.IsLdarg(),
                    i => i.LoadsField(typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.currentSaveFileName))),
                    i => i.Calls(saveMethod)
                }, out var savePos);

            // Make sure we can find the IL code we expect first
            if (foundDelete && foundNewLists && foundAddPos && foundSavePos)
            {
                Plugin.MLS.LogDebug("Patching SaveItemsInShip to include item rotations.");

                // Ensure we have a new variable slot to store our list
                generator.DeclareLocal(newListType);

                // Inject code that deletes the new key when needed
                codeList.InsertRange(deleteInstructions[0].Index, new[]
                {
                    new CodeInstruction(OpCodes.Ldstr, "shipGrabbableItemRot"),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.currentSaveFileName))),
                    new CodeInstruction(OpCodes.Call, typeof(ES3).GetMethod(nameof(ES3.DeleteKey), new[] { typeof(string), typeof(string) }))
                });

                // Inject a new list variable after the other declarations
                codeList.InsertRange(newLists.Last().Index + 5, new[]
                {
                    new CodeInstruction(OpCodes.Newobj, newListType.GetConstructor(Type.EmptyTypes)),
                    new CodeInstruction(OpCodes.Stloc_S, 8)
                });

                // Inject code to add the euler angles to our new list
                codeList.InsertRange(addPosition.Last().Index + 7, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_S, 8), // Our list
                    new CodeInstruction(OpCodes.Ldloc_0),
                    new CodeInstruction(OpCodes.Ldloc_S, 6), // The item array index
                    new CodeInstruction(OpCodes.Ldelem_Ref),
                    new CodeInstruction(OpCodes.Callvirt, typeof(Component).GetMethod("get_transform")),
                    new CodeInstruction(OpCodes.Callvirt, typeof(Transform).GetMethod("get_eulerAngles")),
                    new CodeInstruction(OpCodes.Callvirt, typeof(List<Vector3>).GetMethod("Add"))
                });

                // Inject code to save our new list to the current save file
                codeList.InsertRange(savePos.Last().Index + 14, new[]
                {
                    new CodeInstruction(OpCodes.Ldstr, "shipGrabbableItemRot"),
                    new CodeInstruction(OpCodes.Ldloc_S, 8), // Our list
                    new CodeInstruction(OpCodes.Callvirt, typeof(List<Vector3>).GetMethod("ToArray")),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.currentSaveFileName))),
                    new CodeInstruction(OpCodes.Call, saveMethod)
                });
            }
            else
            {
                Plugin.MLS.LogError("Could not transpile SaveItemsInShip! Unexpected IL code found.");
            }

            return codeList;
        }

        [HarmonyPatch(typeof(GameNetworkManager), nameof(SaveItemsInShip))]
        [HarmonyPostfix]
        private static void SaveItemsInShip(GameNetworkManager __instance)
        {
            // Save extra game stats
            ES3.Save("Stats_DaysSinceLastDeath", StartOfRoundPatch.DaysSinceLastDeath, GameNetworkManager.Instance.currentSaveFileName);
            ES3.Save("Stats_AverageDailyScrap", StartOfRoundPatch.DailyScrapCollected, GameNetworkManager.Instance.currentSaveFileName);
            if (Plugin.ShowHiddenMoonsInCatalog.Value == Enums.eShowHiddenMoons.AfterDiscovery)
            {
                ES3.Save("DiscoveredMoons", string.Join(',', StartOfRoundPatch.FlownToHiddenMoons), GameNetworkManager.Instance.currentSaveFileName);
            }

            // Save spray can colors
            var sprayCanItems = SprayPaintItemPatch.GetAllOrderedSprayPaintItemsInShip().Select(s => s.sprayCanMatsIndex).ToArray();
            ES3.Save("sprayPaintItemColors", sprayCanItems, __instance.currentSaveFileName);

            // Save suit data
            if (Plugin.SavePlayerSuits.Value)
            {
                ES3.Save("SteamIDsToSuitIDs", StartOfRoundPatch.SteamIDsToSuits, __instance.currentSaveFileName);
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), nameof(ResetSavedGameValues))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ResetSavedGameValues(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.SaveShipFurniturePlaces.Value != Enums.eSaveFurniturePlacement.None)
            {
                Label? forLabelEnd = null;
                Label? innerFor = null;
                if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.Is(OpCodes.Ldstr, "UnlockedShipObjects"),
                    i => i.Calls(typeof(GameNetworkManager).GetMethod("get_Instance")),
                    i => i.LoadsField(typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.currentSaveFileName))),
                    i => i.Calls(typeof(ES3).GetMethod(nameof(ES3.DeleteKey), new Type[] { typeof(string), typeof(string) }))
                }, out var unlockedCode)

                && codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.LoadsConstant(0),
                    i => i.IsStloc(),
                    i => i.Branches(out forLabelEnd),
                    i => i.IsLdloc(),
                    i => i.LoadsField(typeof(StartOfRound).GetField(nameof(StartOfRound.unlockablesList))),
                    i => i.LoadsField(typeof(UnlockablesList).GetField(nameof(UnlockablesList.unlockables))),
                    i => i.IsLdloc(),
                    null, null, null,
                    i => i.Branches(out innerFor)
                }, out var forBlockStart)

                && codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.labels.Contains(forLabelEnd.Value),
                    null, null, null, null, // Don't care what these are
                    i => i.Branches(out _)
                }, out var forBlockEnd))
                {
                    Plugin.MLS.LogDebug("Patching GameNetworkManager.ResetSavedGameValues to keep ship unlockable positions.");

                    // Replace the UnlockedShipObjects key deletion with a custom method that strips out non starting furniture items
                    for (int i = unlockedCode.First().Index; i < unlockedCode.Last().Index; i++)
                    {
                        codeList[i] = new CodeInstruction(OpCodes.Nop);
                    }
                    codeList[unlockedCode.Last().Index] = Transpilers.EmitDelegate<Action>(() =>
                    {
                        List<int> movedStartingFurnitures = new List<int>();

                        if (StartOfRound.Instance && StartOfRound.Instance.unlockablesList && StartOfRound.Instance.unlockablesList.unlockables != null)
                        {
                            for (int i = 0; i < StartOfRound.Instance.unlockablesList.unlockables.Count; i++)
                            {
                                var unlockable = StartOfRound.Instance.unlockablesList.unlockables[i];
                                if ((unlockable.hasBeenMoved || unlockable.inStorage) && !unlockable.spawnPrefab)
                                {
                                    movedStartingFurnitures.Add(i);
                                }
                            }
                        }

                        ES3.Save("UnlockedShipObjects", movedStartingFurnitures.ToArray(), GameNetworkManager.Instance.currentSaveFileName);
                    });

                    // If keeping everything, remove the loop that deletes the saved positions and rotations
                    if (Plugin.SaveShipFurniturePlaces.Value == Enums.eSaveFurniturePlacement.All)
                    {
                        for (int i = forBlockStart.First().Index; i <= forBlockEnd.Last().Index; i++)
                        {
                            codeList[i] = new CodeInstruction(OpCodes.Nop);
                        }
                    }
                    else
                    {
                        // Otherwise, add an extra check to skip the starting furniture
                        codeList.InsertRange(forBlockStart.Last().Index + 1, new CodeInstruction[]
                        {
                            new CodeInstruction(OpCodes.Ldloc_1),
                            CodeInstruction.LoadField(typeof(StartOfRound), nameof(StartOfRound.unlockablesList)),
                            CodeInstruction.LoadField(typeof(UnlockablesList), nameof(UnlockablesList.unlockables)),
                            new CodeInstruction(OpCodes.Ldloc_3),
                            CodeInstruction.Call(typeof(List<UnlockableItem>), "get_Item"),
                            CodeInstruction.LoadField(typeof(UnlockableItem), nameof(UnlockableItem.spawnPrefab)),
                            new CodeInstruction(OpCodes.Brfalse_S, innerFor)
                        });
                    }
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL code - Could not patch GameNetworkManager.ResetSavedGameValues to keep ship unlockable positions!");
                }
            }

            return codeList;
        }

        [HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.ResetUnlockablesListValues))]
        [HarmonyPrefix]
        private static bool ResetUnlockablesListValues(ref bool onlyResetPrefabItems)
        {
            if (Plugin.SaveShipFurniturePlaces.Value == Enums.eSaveFurniturePlacement.StartingFurniture && !onlyResetPrefabItems)
            {
                // Make sure not to reset default furniture if specified
                onlyResetPrefabItems = true;
            }
            else if (Plugin.SaveShipFurniturePlaces.Value == Enums.eSaveFurniturePlacement.All && StartOfRound.Instance && StartOfRound.Instance.unlockablesList && StartOfRound.Instance.unlockablesList.unlockables != null)
            {
                // If we want to save everything, just manually reset their unlocked state but keep everything else
                StartOfRound.Instance.unlockablesList.unlockables.ForEach(u => u.hasBeenUnlockedByPlayer = false);
                return false;
            }

            return true;
        }
    }
}