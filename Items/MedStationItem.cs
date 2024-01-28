using GeneralImprovements.Utilities;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GeneralImprovements.Items
{
    public class MedStationItem : NetworkBehaviour
    {
        public int MaxLocalPlayerHealth;

        private void Update()
        {
            if (StartOfRound.Instance?.localPlayerController == null)
            {
                return;
            }

            if (StartOfRound.Instance.localPlayerController.health < MaxLocalPlayerHealth)
            {
                Plugin.MLS.LogInfo($"Setting max local player health to {StartOfRound.Instance.localPlayerController.health}");
                MaxLocalPlayerHealth = StartOfRound.Instance.localPlayerController.health;
            }
        }

        public void HealLocalPlayer()
        {
            if (StartOfRound.Instance.localPlayerController.health < MaxLocalPlayerHealth)
            {
                StartOfRound.Instance.localPlayerController.StartCoroutine(HealLocalPlayerCoroutine());
            }
        }

        private IEnumerator HealLocalPlayerCoroutine()
        {
            PlayHealSoundServerRpc();
            yield return new WaitForSeconds(0.75f);

            HUDManager.Instance.UpdateHealthUI(MaxLocalPlayerHealth, false);
            HealPlayerServerRpc(StartOfRound.Instance.localPlayerController.playerClientId, MaxLocalPlayerHealth);
        }

        [ServerRpc(RequireOwnership = false)]
        private void PlayHealSoundServerRpc()
        {
            PlayHealSoundClientRpc();
        }

        [ClientRpc]
        private void PlayHealSoundClientRpc()
        {
            if (ItemHelper.MedStation != null)
            {
                ItemHelper.MedStation.GetComponentInChildren<AudioSource>().Play();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void HealPlayerServerRpc(ulong playerID, int targetHealth)
        {
            HealPlayerClientRpc(playerID, targetHealth);
        }

        [ClientRpc]
        private void HealPlayerClientRpc(ulong playerID, int targetHealth)
        {
            var player = StartOfRound.Instance.allPlayerScripts[playerID];

            Plugin.MLS.LogInfo($"Healing {player.playerUsername} back to {targetHealth}...");
            player.health = targetHealth;
            player.criticallyInjured = false;
            player.bleedingHeavily = false;
            player.playerBodyAnimator.SetBool("Limp", false);
        }
    }
}