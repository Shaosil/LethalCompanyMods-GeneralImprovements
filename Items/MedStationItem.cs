using System.Collections;
using GeneralImprovements.Patches;
using GeneralImprovements.Utilities;
using Unity.Netcode;
using UnityEngine;

namespace GeneralImprovements.Items
{
    public class MedStationItem : NetworkBehaviour
    {
        public void HealLocalPlayer()
        {
            if (StartOfRound.Instance.localPlayerController.health < PlayerControllerBPatch.CurrentMaxHealth)
            {
                StartOfRound.Instance.localPlayerController.StartCoroutine(HealLocalPlayerCoroutine());
            }
        }

        private IEnumerator HealLocalPlayerCoroutine()
        {
            PlayHealSoundServerRpc();
            yield return new WaitForSeconds(0.75f);

            HUDManager.Instance.UpdateHealthUI(PlayerControllerBPatch.CurrentMaxHealth, false);
            HealPlayerServerRpc(StartOfRound.Instance.localPlayerController.playerClientId, PlayerControllerBPatch.CurrentMaxHealth);
        }

        [ServerRpc(RequireOwnership = false)]
        private void PlayHealSoundServerRpc()
        {
            PlayHealSoundClientRpc();
        }

        [ClientRpc]
        private void PlayHealSoundClientRpc()
        {
            if (ObjectHelper.MedStation != null)
            {
                ObjectHelper.MedStation.GetComponentInChildren<AudioSource>().Play();
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
            MonitorsHelper.UpdatePlayerHealthMonitors();
        }
    }
}