using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class MaskedPlayerEnemyPatch
    {
        private static Dictionary<MaskedPlayerEnemy, ExtraMaskData> _maskData = new Dictionary<MaskedPlayerEnemy, ExtraMaskData>();

        public static int NumSpawnedThisLevel = 0;
        public static int MaxHealth { get; private set; }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.Start))]
        [HarmonyPostfix]
        private static void Start(MaskedPlayerEnemy __instance)
        {
            // If any mask setting is something other than default, check further logic
            if (!Plugin.MaskedEntitiesWearMasks.Value || Plugin.MaskedEntitiesShowPlayerNames.Value || Plugin.MaskedEntitiesCopyPlayerLooks.Value > Enums.eMaskedEntityCopyLook.None
                 || !Plugin.MaskedEntitiesSpinOnRadar.Value || !Plugin.MaskedEntitiesReachTowardsPlayer.Value || Plugin.MaskedEntitiesShowScrapIconChance.Value != 0)
            {
                // Init extra mask data
                _maskData[__instance] = new ExtraMaskData();

                // If the masked spawned from a player, use their name and appearance. Otherwise, use any random player's name and appearance.
                var rand = new System.Random(StartOfRound.Instance.randomMapSeed + NumSpawnedThisLevel);
                var allPlayers = StartOfRound.Instance.allPlayerScripts.Where(p => p.isPlayerDead || p.isPlayerControlled).ToList();
                PlayerControllerB playerToTarget = __instance.mimickingPlayer ?? allPlayers[rand.Next(allPlayers.Count)];

                if (Plugin.MaskedEntitiesShowPlayerNames.Value)
                {
                    if (Plugin.ScanPlayers.Value)
                    {
                        // Create scan node
                        var node = ObjectHelper.CreateScanNodeOnObject(__instance.gameObject, 0, 1, 10, playerToTarget.playerUsername);
                        node.transform.localPosition += new Vector3(0, 2.25f, 0);
                    }

                    // Set username billboard text and add it to our tracked items
                    if (!Plugin.HidePlayerNames.Value)
                    {
                        var tmp = __instance.transform.Find("PlayerUsernameCanvas")?.GetComponentInChildren<TextMeshProUGUI>();
                        if (tmp != null)
                        {
                            tmp.text = playerToTarget.usernameBillboardText.text;
                            _maskData[__instance].Canvas = tmp.transform.parent.GetComponent<Canvas>();
                            _maskData[__instance].CanvasGroup = tmp.GetComponent<CanvasGroup>();
                        }
                    }
                }

                if (!Plugin.MaskedEntitiesWearMasks.Value)
                {
                    // Hide the mesh renderer of their masks and set the suit to the targeted player's ID
                    var masks1 = __instance.transform.Find("ScavengerModel/metarig/spine/spine.001/spine.002/spine.003/spine.004/HeadMaskComedy")?.GetComponentsInChildren<MeshRenderer>();
                    var masks2 = __instance.transform.Find("ScavengerModel/metarig/spine/spine.001/spine.002/spine.003/spine.004/HeadMaskTragedy")?.GetComponentsInChildren<MeshRenderer>();
                    foreach (var mask in (masks1 ?? new MeshRenderer[0]).Concat(masks2 ?? new MeshRenderer[0]))
                    {
                        mask.enabled = false;
                    }
                }

                if (Plugin.MaskedEntitiesCopyPlayerLooks.Value > Enums.eMaskedEntityCopyLook.None)
                {
                    // Copy whatever suit the target player is wearing
                    __instance.SetSuit(playerToTarget.currentSuitID);
                }

                if (Plugin.MaskedEntitiesCopyPlayerLooks.Value == Enums.eMaskedEntityCopyLook.SuitAndCosmetics)
                {
                    // Set isMimicking and SetOutside to trigger MoreCompany cosmetics if they exist
                    __instance.mimickingPlayer = playerToTarget;
                    __instance.SetEnemyOutside(__instance.isOutside);
                }

                if (!Plugin.MaskedEntitiesSpinOnRadar.Value)
                {
                    // Remove map dot animation if needed
                    var animator = __instance.transform.Find("Misc/MapDot")?.GetComponent<Animator>();
                    if (animator != null)
                    {
                        animator.enabled = false;
                    }
                }

                // Spawn a radar map scrap icon if needed
                if (Plugin.MaskedEntitiesShowScrapIconChance.Value > 0 && rand.Next(100) < Mathf.Clamp(Plugin.MaskedEntitiesShowScrapIconChance.Value, 1, 100))
                {
                    _maskData[__instance].RadarIcon = Object.Instantiate(StartOfRound.Instance.itemRadarIconPrefab, __instance.transform);
                    _maskData[__instance].RadarIcon.transform.SetLocalPositionAndRotation(new Vector3(0, 2, 1.5f), Quaternion.identity);
                }

                MaxHealth = __instance.enemyHP;
                NumSpawnedThisLevel++;
            }
        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.LateUpdate))]
        [HarmonyPostfix]
        private static void LateUpdate(MaskedPlayerEnemy __instance)
        {
            // Decrease username billboard alpha if needed
            if (Plugin.MaskedEntitiesShowPlayerNames.Value && _maskData.ContainsKey(__instance) && _maskData[__instance].CanvasGroup?.alpha > 0 && StartOfRound.Instance.localPlayerController != null)
            {
                _maskData[__instance].CanvasGroup.alpha -= Time.deltaTime;
                _maskData[__instance].Canvas.transform.LookAt(StartOfRound.Instance.localPlayerController.localVisorTargetPoint);
            }
        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.SetHandsOutClientRpc))]
        [HarmonyPrefix]
        private static void SetHandsOutClientRpc(ref bool setOut)
        {
            // Override setting the hands to reach out if needed
            if (!Plugin.MaskedEntitiesReachTowardsPlayer.Value && setOut)
            {
                setOut = false;
            }
        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.KillEnemy))]
        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.OnDestroy))]
        [HarmonyPostfix]
        private static void EndOfMasked(MaskedPlayerEnemy __instance)
        {
            // Disable the username canvas if it's enabled and stop keeping track of it
            if (Plugin.MaskedEntitiesShowPlayerNames.Value && _maskData.ContainsKey(__instance))
            {
                if (_maskData[__instance].Canvas != null)
                {
                    _maskData[__instance].Canvas.gameObject.SetActive(false);
                }
                if (_maskData[__instance].RadarIcon != null)
                {
                    Object.Destroy(_maskData[__instance].RadarIcon);
                }
                _maskData.Remove(__instance);
            }
        }

        internal static void ShowNameBillboard(MaskedPlayerEnemy mask)
        {
            if (_maskData.ContainsKey(mask) && _maskData[mask].CanvasGroup != null)
            {
                _maskData[mask].CanvasGroup.alpha = 1;
                _maskData[mask].Canvas.gameObject.SetActive(true);
            }
        }

        private class ExtraMaskData
        {
            public Canvas Canvas;
            public CanvasGroup CanvasGroup;
            public GameObject RadarIcon;
        }
    }
}