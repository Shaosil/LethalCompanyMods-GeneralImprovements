using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System.Linq;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class MaskedPlayerEnemyPatch
    {
        public static int NumSpawnedThisLevel = 0;
        public static int MaxHealth { get; private set; }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.Start))]
        [HarmonyPostfix]
        private static void Start(MaskedPlayerEnemy __instance)
        {
            if (Plugin.MaskedLookLikePlayers.Value || Plugin.ScanPlayers.Value)
            {
                // If the masked spawned from a player, use their name and appearance. Otherwise, use any random player's name and appearance.
                var rand = new System.Random(StartOfRound.Instance.randomMapSeed + NumSpawnedThisLevel);
                var allPlayers = StartOfRound.Instance.allPlayerScripts.Where(p => p.isPlayerDead || p.isPlayerControlled).ToList();
                PlayerControllerB playerToTarget = __instance.mimickingPlayer ?? allPlayers[rand.Next(allPlayers.Count)];

                if (Plugin.ScanPlayers.Value)
                {
                    var node = ObjectHelper.CreateScanNodeOnObject(__instance.gameObject, 0, 1, 10, playerToTarget.playerUsername);
                    node.transform.localPosition += new Vector3(0, 2.25f, 0);
                }

                if (Plugin.MaskedLookLikePlayers.Value)
                {
                    // Hide the mesh renderer of their masks and set the suit to the targeted player's ID
                    var masks1 = __instance.transform.Find("ScavengerModel/metarig/spine/spine.001/spine.002/spine.003/spine.004/HeadMaskComedy")?.GetComponentsInChildren<MeshRenderer>();
                    var masks2 = __instance.transform.Find("ScavengerModel/metarig/spine/spine.001/spine.002/spine.003/spine.004/HeadMaskTragedy")?.GetComponentsInChildren<MeshRenderer>();
                    foreach (var mask in (masks1 ?? new MeshRenderer[0]).Concat(masks2 ?? new MeshRenderer[0]))
                    {
                        mask.enabled = false;
                    }
                    __instance.SetSuit(playerToTarget.currentSuitID);
                }

                MaxHealth = __instance.enemyHP;
                NumSpawnedThisLevel++;
            }
        }
    }
}