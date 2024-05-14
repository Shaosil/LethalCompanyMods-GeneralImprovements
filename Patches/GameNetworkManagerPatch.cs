using GeneralImprovements.Items;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Unity.Netcode;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class GameNetworkManagerPatch
    {
        public static void PatchNetcode()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
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

            ObjectHelper.AlterFancyLampPrefab();

            // Attach our own network helper to this gameobject
            __instance.gameObject.AddComponent<NetworkHelper>();
            __instance.gameObject.AddComponent<NetworkObject>();

            var allItems = NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs
                .Where(p => p.Prefab.TryGetComponent<GrabbableObject>(out var go) && go.itemProperties != null)
                .Select(p => p.Prefab.GetComponent<GrabbableObject>().itemProperties).ToList();
            var nonConductiveItems = new string[] { "Flask", "Whoopie Cushion" };
            var tools = new string[] { "Extension ladder", "Jetpack", "Key", "Radar-booster", "Shovel", "Stop sign", "TZP-Inhalant", "Yield sign", "Kitchen knife", "Zap gun" };
            foreach (var item in allItems)
            {
                // Allow all items to be grabbed before game start
                if (!item.canBeGrabbedBeforeGameStart)
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
            if (StartOfRound.Instance?.IsHost ?? false)
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
            if (Plugin.ShowHiddenMoonsInCatalog.Value == Enums.eShowHiddenMoons.AfterDiscovery)
            {
                ES3.Save("DiscoveredMoons", string.Join(',', StartOfRoundPatch.FlownToHiddenMoons), GameNetworkManager.Instance.currentSaveFileName);
            }

            // Save spray can colors
            var sprayCanItems = SprayPaintItemPatch.GetAllOrderedSprayPaintItemsInShip().Select(s => SprayPaintItemPatch.GetColorIndex(s)).ToArray();
            ES3.Save("sprayPaintItemColors", sprayCanItems, __instance.currentSaveFileName);

            // Save suit data
            if (Plugin.SavePlayerSuits.Value)
            {
                ES3.Save("SteamIDsToSuitIDs", StartOfRoundPatch.SteamIDsToSuits, __instance.currentSaveFileName);
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), nameof(SaveGameValues))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SaveGameValues(IEnumerable<CodeInstruction> instructions)
        {
            if (Plugin.SaveShipFurniturePlaces.Value == Enums.eSaveFurniturePlacement.All)
            {
                Label? outsideBlockLabel = null;
                if (instructions.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.LoadsField(typeof(UnlockableItem).GetField(nameof(UnlockableItem.hasBeenUnlockedByPlayer))),
                    i => i.Branches(out _),
                    null, null, null, null, null, // Unlockables array stuff
                    i => i.LoadsField(typeof(UnlockableItem).GetField(nameof(UnlockableItem.hasBeenMoved))),
                    i => i.Branches(out _),
                    null, null, null, null, null, // Unlockables array stuff
                    i => i.LoadsField(typeof(UnlockableItem).GetField(nameof(UnlockableItem.inStorage))),
                    i => i.Branches(out outsideBlockLabel)
                }, out var found))
                {
                    // Update the 2nd check to be alreadyUnlocked
                    found[7].Instruction.operand = typeof(UnlockableItem).GetField(nameof(UnlockableItem.alreadyUnlocked));

                    // NOP out the 3rd check
                    for (int i = 9; i < found.Length; i++)
                    {
                        found[i].Instruction.opcode = OpCodes.Nop;
                        found[i].Instruction.operand = null;
                    }

                    // Update the second branch
                    found[8].Instruction.opcode = OpCodes.Brfalse_S;
                    found[8].Instruction.operand = outsideBlockLabel;

                    Plugin.MLS.LogDebug("Patching GameNetworkManager.SaveGameValues to remove bought ship items after being fired.");
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL code - Could not patch GameNetworkManager.SaveGameValues to fully remove bought ship items after being fired!");
                }
            }

            return instructions;
        }

        [HarmonyPatch(typeof(GameNetworkManager), nameof(ResetSavedGameValues))]
        [HarmonyTranspiler]
        [Harmony]
        private static IEnumerable<CodeInstruction> ResetSavedGameValues(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.SaveShipFurniturePlaces.Value == Enums.eSaveFurniturePlacement.All)
            {
                Label? forLabelEnd = null;
                if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.Is(OpCodes.Ldstr, "UnlockedShipObjects"),
                    i => i.Calls(typeof(GameNetworkManager).GetMethod("get_Instance")),
                    i => i.LoadsField(typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.currentSaveFileName))),
                    i => i.Calls(typeof(ES3).GetMethod(nameof(ES3.DeleteKey), new[] { typeof(string), typeof(string) })),
                    i => i.LoadsConstant(0),
                    i => i.IsStloc(),
                    i => i.Branches(out forLabelEnd)
                }, out var forBlockStart)
                && codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.labels.Contains(forLabelEnd.Value),
                    null, null, null, null, // Don't care what these are
                    i => i.Branches(out _)
                }, out var forBlockEnd))
                {
                    Plugin.MLS.LogDebug("Patching GameNetworkManager.ResetSavedGameValues to keep bought ship decor positions.");

                    // NOP out entire for loop
                    for (int i = forBlockStart[4].Index; i <= forBlockEnd.Last().Index; i++)
                    {
                        codeList[i] = new CodeInstruction(OpCodes.Nop);
                    }
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL code - Could not patch GameNetworkManager.ResetSavedGameValues to keep bought ship decor positions!");
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
            else if (Plugin.SaveShipFurniturePlaces.Value == Enums.eSaveFurniturePlacement.All && StartOfRound.Instance?.unlockablesList?.unlockables != null)
            {
                // If we want to save everything, just manually reset their unlocked state but keep everything else
                StartOfRound.Instance.unlockablesList.unlockables.ForEach(u => u.hasBeenUnlockedByPlayer = false);
                return false;
            }

            return true;
        }
    }
}