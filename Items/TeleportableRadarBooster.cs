using Unity.Netcode;
using UnityEngine;

namespace GeneralImprovements.Items
{
    internal class TeleportableRadarBooster : NetworkBehaviour
    {
        private RadarBoosterItem RadarInstance;

        private void Start()
        {
            RadarInstance = GetComponent<RadarBoosterItem>();
        }

        [ClientRpc]
        public void PlayBeamEffectsClientRpc(bool isRegular)
        {
            Plugin.MLS.LogDebug("Received radar booster beam effects RPC");
            var particles = transform.Find(isRegular ? "BeamUpEffects" : "BeamOutBuildupEffects")?.GetComponent<ParticleSystem>();
            if (particles != null) particles.Play();
        }

        [ClientRpc]
        public void TeleportRadarBoosterClientRpc(NetworkObjectReference teleporterNetRef, Vector3 position, bool onShip)
        {
            Plugin.MLS.LogDebug("Received radar booster regular teleport RPC");

            if (teleporterNetRef.TryGet(out var teleporterNetObj) && teleporterNetObj.TryGetComponent<ShipTeleporter>(out var teleporter))
            {
                TeleportRadarBooster(teleporter, position, onShip);
            }
        }

        [ClientRpc]
        public void BeamOutClientRpc(NetworkObjectReference teleporterNetRef, int randSeed)
        {
            Plugin.MLS.LogDebug("Received radar booster inverse teleport RPC");

            if (teleporterNetRef.TryGet(out var teleporterNetObj) && teleporterNetObj.TryGetComponent<ShipTeleporter>(out var teleporter)
                && RoundManager.Instance.insideAINodes.Length > 0)
            {
                int rndIndex = new System.Random(randSeed).Next(0, RoundManager.Instance.insideAINodes.Length);
                var dest = RoundManager.Instance.insideAINodes[rndIndex].transform.position;

                // Play final effects and teleport inside
                var particles = transform.Find("BeamOutEffects")?.GetComponent<ParticleSystem>();
                if (particles != null) particles.Play();
                TeleportRadarBooster(teleporter, dest, false);
            }
        }

        private void TeleportRadarBooster(ShipTeleporter teleporter, Vector3 position, bool onShip)
        {
            RadarInstance.transform.SetParent(onShip ? StartOfRound.Instance.elevatorTransform : StartOfRound.Instance.propsContainer);

            RadarInstance.transform.position = position + (Vector3.up * 1f);
            RadarInstance.startFallingPosition = RadarInstance.transform.parent.InverseTransformPoint(RadarInstance.transform.position);

            RadarInstance.isInElevator = onShip;
            RadarInstance.isInShipRoom = onShip;
            RadarInstance.isInFactory = !onShip;
            RadarInstance.hasHitGround = false;
            RadarInstance.FallToGround();

            // Activate it on the owner's machine if it was inversed in and not already active
            if (!onShip && !RadarInstance.isBeingUsed && RadarInstance.IsOwner)
                RadarInstance.UseItemOnClient();

            // Play the teleporter's beam sound on the radar audio source
            if (teleporter.teleporterBeamUpSFX != null && RadarInstance.radarBoosterAudio != null)
            {
                RadarInstance.radarBoosterAudio.PlayOneShot(teleporter.teleporterBeamUpSFX);
            }
        }
    }
}