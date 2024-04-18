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

            ItemHelper.AlterFancyLampPrefab();

            // Attach our own network helper to this gameobject
            __instance.gameObject.AddComponent<NetworkHelper>();
            __instance.gameObject.AddComponent<NetworkObject>();
        }

        [HarmonyPatch(typeof(GameNetworkManager), nameof(Disconnect))]
        [HarmonyPrefix]
        private static void Disconnect(GameNetworkManager __instance)
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

            // Make sure a few lines of the IL code is what we expect first
            if (codeList[32].opcode == OpCodes.Newobj && codeList[34].opcode == OpCodes.Newobj && codeList[36].opcode == OpCodes.Newobj && codeList[107].opcode == OpCodes.Callvirt)
            {
                Plugin.MLS.LogDebug("Patching SaveItemsInShip to include item rotations.");

                // Ensure we have a new variable slot to store our list
                generator.DeclareLocal(newListType);

                // Inject code that deletes the new key when needed
                codeList.InsertRange(0, new[]
                {
                    new CodeInstruction(OpCodes.Ldstr, "shipGrabbableItemRot"),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.currentSaveFileName))),
                    new CodeInstruction(OpCodes.Call, typeof(ES3).GetMethod(nameof(ES3.DeleteKey), new[] { typeof(string), typeof(string) }))
                });

                // Inject a new list variable after the other declarations
                codeList.InsertRange(42, new[]
                {
                    new CodeInstruction(OpCodes.Newobj, newListType.GetConstructor(Type.EmptyTypes)),
                    new CodeInstruction(OpCodes.Stloc_S, 8)
                });

                // Inject code to add the euler angles to our new list
                codeList.InsertRange(114, new[]
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
                codeList.InsertRange(205, new[]
                {
                    new CodeInstruction(OpCodes.Ldstr, "shipGrabbableItemRot"),
                    new CodeInstruction(OpCodes.Ldloc_S, 8), // Our list
                    new CodeInstruction(OpCodes.Callvirt, typeof(List<Vector3>).GetMethod("ToArray")),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.currentSaveFileName))),
                    new CodeInstruction(OpCodes.Call, typeof(ES3).GetMethod(nameof(ES3.Save), new[] { typeof(string), typeof(Vector3[]), typeof(string) }))
                });
            }
            else
            {
                Plugin.MLS.LogError("Could not transpile SaveItemsInShip! Unexpected IL code found.");
            }

            return codeList.AsEnumerable();
        }

        [HarmonyPatch(typeof(GameNetworkManager), nameof(SaveItemsInShip))]
        [HarmonyPostfix]
        private static void SaveItemsInShip(GameNetworkManager __instance)
        {
            // Save spray can colors
            var sprayCanItems = SprayPaintItemPatch.GetAllOrderedSprayPaintItemsInShip().Select(s => SprayPaintItemPatch.GetColorIndex(s)).ToArray();
            if (sprayCanItems.Any())
            {
                ES3.Save("sprayPaintItemColors", sprayCanItems, __instance.currentSaveFileName);
            }
        }
    }
}