using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class MaskedPlayerEnemyPatch
    {
        private static Dictionary<MaskedPlayerEnemy, KeyValuePair<Canvas, CanvasGroup>> _maskUsernames = new Dictionary<MaskedPlayerEnemy, KeyValuePair<Canvas, CanvasGroup>>();

        public static int NumSpawnedThisLevel = 0;
        public static int MaxHealth { get; private set; }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.Start))]
        [HarmonyPostfix]
        private static void Start(MaskedPlayerEnemy __instance)
        {
            // If any mask setting is something other than default, check further logic
            if (!Plugin.MaskedEntitiesWearMasks.Value || Plugin.MaskedEntitiesShowPlayerNames.Value || Plugin.MaskedEntitiesCopyPlayerLooks.Value > Enums.eMaskedEntityCopyLook.None
                 || !Plugin.MaskedEntitiesSpinOnRadar.Value || !Plugin.MaskedEntitiesReachTowardsPlayer.Value)
            {
                // If the masked spawned from a player, use their name and appearance. Otherwise, use any random player's name and appearance.
                var rand = new System.Random(StartOfRound.Instance.randomMapSeed + NumSpawnedThisLevel);
                var allPlayers = StartOfRound.Instance.allPlayerScripts.Where(p => p.isPlayerDead || p.isPlayerControlled).ToList();
                PlayerControllerB playerToTarget = __instance.mimickingPlayer ?? allPlayers[rand.Next(allPlayers.Count)];

                if (Plugin.MaskedEntitiesShowPlayerNames.Value)
                {
                    // Create scan node
                    var node = ObjectHelper.CreateScanNodeOnObject(__instance.gameObject, 0, 1, 10, playerToTarget.playerUsername);
                    node.transform.localPosition += new Vector3(0, 2.25f, 0);

                    // Set username billboard text and add it to our tracked items
                    if (!Plugin.HidePlayerNames.Value)
                    {
                        var tmp = __instance.transform.Find("PlayerUsernameCanvas")?.GetComponentInChildren<TextMeshProUGUI>();
                        if (tmp != null)
                        {
                            tmp.text = playerToTarget.usernameBillboardText.text;
                            _maskUsernames[__instance] = new KeyValuePair<Canvas, CanvasGroup>(tmp.transform.parent.GetComponent<Canvas>(), tmp.GetComponent<CanvasGroup>());
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

                MaxHealth = __instance.enemyHP;
                NumSpawnedThisLevel++;
            }
        }

        [HarmonyPatch(typeof(MaskedPlayerEnemy), nameof(MaskedPlayerEnemy.LateUpdate))]
        [HarmonyPostfix]
        private static void LateUpdate(MaskedPlayerEnemy __instance)
        {
            // Decrease username billboard alpha if needed
            if (Plugin.MaskedEntitiesShowPlayerNames.Value && _maskUsernames.ContainsKey(__instance) && _maskUsernames[__instance].Value.alpha > 0 && StartOfRound.Instance.localPlayerController != null)
            {
                _maskUsernames[__instance].Value.alpha -= Time.deltaTime;
                _maskUsernames[__instance].Key.transform.LookAt(StartOfRound.Instance.localPlayerController.localVisorTargetPoint);
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
            if (Plugin.MaskedEntitiesShowPlayerNames.Value && _maskUsernames.ContainsKey(__instance))
            {
                _maskUsernames[__instance].Key.gameObject.SetActive(false);
                _maskUsernames.Remove(__instance);
            }
        }

        internal static void ShowNameBillboard(MaskedPlayerEnemy mask)
        {
            Plugin.MLS.LogError("IN BILLBOARD CALL");
            if (_maskUsernames.ContainsKey(mask))
            {
                Plugin.MLS.LogError($"SHOWING MASK BILLBOARD TEXT: {_maskUsernames[mask].Value.GetComponent<TextMeshProUGUI>().text}");
                _maskUsernames[mask].Value.alpha = 1;
                _maskUsernames[mask].Key.gameObject.SetActive(true);
            }
        }
    }
}