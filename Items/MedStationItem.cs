using GeneralImprovements.Utilities;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace GeneralImprovements.Items
{
    public class MedStationItem : NetworkBehaviour
    {
        public void HealLocalPlayer()
        {
            if (StartOfRound.Instance.localPlayerController.health <= SceneHelper.MaxHealth)
            {
                StartOfRound.Instance.localPlayerController.StartCoroutine(HealLocalPlayerCoroutine());
            }
        }

        private IEnumerator HealLocalPlayerCoroutine()
        {
            PlayHealSoundServerRpc();
            yield return new WaitForSeconds(0.75f);

            Plugin.MLS.LogInfo($"Healing back to {SceneHelper.MaxHealth}...");
            StartOfRound.Instance.localPlayerController.DamagePlayer(-(SceneHelper.MaxHealth - StartOfRound.Instance.localPlayerController.health), false, true);
            StartOfRound.Instance.localPlayerController.MakeCriticallyInjured(false);

            yield break;
        }

        [ServerRpc(RequireOwnership = false)]
        private void PlayHealSoundServerRpc()
        {
            PlayHealSoundClientRpc();
        }

        [ClientRpc]
        private void PlayHealSoundClientRpc()
        {
            if (SceneHelper.MedStation != null)
            {
                SceneHelper.MedStation.GetComponentInChildren<AudioSource>().Play();
            }
        }
    }
}