using GeneralImprovements.Items;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System.Linq;
using System.Reflection;
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
                var allHeldItems = Object.FindObjectsOfType<GrabbableObject>().Where(g => g.isHeld).ToList();
                foreach (var heldItem in allHeldItems)
                {
                    Plugin.MLS.LogInfo($"Server disconnecting - dropping {heldItem.name}");
                    heldItem.transform.position = heldItem.GetItemFloorPosition();
                }
            }
        }

        [HarmonyPatch(typeof(GameNetworkManager), nameof(SaveItemsInShip))]
        [HarmonyPostfix]
        private static void SaveItemsInShip()
        {
            // Save extra things we care about
            var sprayCanItems = SprayPaintItemPatch.GetAllOrderedSprayPaintItemsInShip().Select(s => SprayPaintItemPatch.GetColorIndex(s)).ToArray();
            if (sprayCanItems.Any())
            {
                ES3.Save("sprayPaintItemColors", sprayCanItems);
            }
        }
    }
}