using GeneralImprovements.Items;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace GeneralImprovements.Utilities
{
    internal static class ItemHelper
    {
        private static IReadOnlyList<NetworkPrefab> AllPrefabs => NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs;

        public static GameObject MedStation;
        public static int MaxHealth;

        public static void CreateMedStation()
        {
            if (Plugin.AddHealthRechargeStation.Value && AssetBundleHelper.MedStationPrefab != null)
            {
                MedStationItem medStationItem = null;

                if (StartOfRound.Instance.IsHost)
                {
                    // If we are the host, spawn it as a network object
                    Plugin.MLS.LogInfo("Adding medical station to ship.");
                    MedStation = Object.Instantiate(AssetBundleHelper.MedStationPrefab, new Vector3(2.75f, 3.4f, -16.561f), Quaternion.Euler(-90, 0, 0));
                    medStationItem = MedStation.GetComponent<MedStationItem>();
                    medStationItem.NetworkObject.Spawn();
                    medStationItem.NetworkObject.TrySetParent(StartOfRound.Instance.elevatorTransform);
                }
                else
                {
                    // If we are a client, try to find it in the scene first
                    Plugin.MLS.LogInfo("Finding medical station in ship");
                    medStationItem = Object.FindObjectOfType<MedStationItem>();
                    MedStation = medStationItem?.gameObject;

                    if (MedStation == null)
                    {
                        Plugin.MLS.LogWarning($"Could not find existing med station! Ensure the host has {nameof(Plugin.AddHealthRechargeStation)} set to true.");
                        return;
                    }
                }

                var chargeStation = Object.FindObjectOfType<ItemCharger>().GetComponent<InteractTrigger>();
                MedStation.GetComponent<AudioSource>().outputAudioMixerGroup = chargeStation.GetComponent<AudioSource>().outputAudioMixerGroup;

                // Add interaction trigger
                var chargeTriggerCollider = chargeStation.GetComponent<BoxCollider>();
                chargeTriggerCollider.center = Vector3.zero;
                chargeTriggerCollider.size = new Vector3(1, 0.8f, 0.8f);
                var medTrigger = MedStation.transform.Find("Trigger");
                medTrigger.tag = chargeStation.tag;
                medTrigger.gameObject.layer = chargeStation.gameObject.layer;
                var interactScript = medTrigger.gameObject.AddComponent<InteractTrigger>();
                interactScript.hoverTip = "Heal";
                interactScript.disabledHoverTip = "(Health Full)";
                interactScript.hoverIcon = chargeStation.hoverIcon;
                interactScript.specialCharacterAnimation = true;
                interactScript.animationString = chargeStation.animationString;
                interactScript.lockPlayerPosition = true;
                interactScript.playerPositionNode = chargeStation.playerPositionNode;
                interactScript.onInteract = new InteractEvent();
                interactScript.onCancelAnimation = new InteractEvent();
                interactScript.onInteractEarly = new InteractEvent();
                interactScript.onInteractEarly.AddListener(_ => medStationItem.HealLocalPlayer());

                // Add scan node
                var scanNode = MedStation.transform.Find("ScanNode").gameObject.AddComponent<ScanNodeProperties>();
                scanNode.gameObject.layer = LayerMask.NameToLayer("ScanNode");
                scanNode.minRange = 0;
                scanNode.maxRange = 6;
                scanNode.nodeType = 0;
                scanNode.headerText = "Med Station";
                scanNode.subText = "Fully heal yourself";
            }
        }

        public static void AlterFancyLampPrefab()
        {
            // Add fancy lamp toggle to existing fancy lamp network prefab
            var fancyLampPrefab = AllPrefabs.FirstOrDefault(p => p.Prefab.name == "FancyLamp");
            if (fancyLampPrefab != null)
            {
                Plugin.MLS.LogInfo("Adding ToggleableFancyLamp behavior to FancyLamp prefab.");
                var grabbable = fancyLampPrefab.Prefab.GetComponent<PhysicsProp>();
                var toggleable = fancyLampPrefab.Prefab.AddComponent<ToggleableFancyLamp>();
                CopyGrabbablePrefab(grabbable, toggleable);
                toggleable.itemProperties.toolTips = new[] { "[LMB] : Toggle Light" };
                toggleable.itemProperties.syncUseFunction = true;
                Object.Destroy(grabbable);

                // Create audio source from flashlight clips
                var flashlight = AllPrefabs.FirstOrDefault(p => p.Prefab.name == "FlashlightItem")?.Prefab.GetComponent<FlashlightItem>();
                if (flashlight != null && flashlight.flashlightAudio != null && flashlight.flashlightClips != null && flashlight.flashlightClips.Length > 0)
                {
                    var newAudioSource = fancyLampPrefab.Prefab.AddComponent<AudioSource>();
                    CopyAudioSource(flashlight.flashlightAudio, newAudioSource);
                    toggleable.AudioClips = flashlight.flashlightClips;
                }
            }
        }

        public static void CopyGrabbablePrefab<T>(GrabbableObject baseObj, T newObj) where T : GrabbableObject
        {
            newObj.customGrabTooltip = baseObj.customGrabTooltip;
            newObj.floorYRot = baseObj.floorYRot;
            newObj.grabbable = baseObj.grabbable;
            newObj.grabbableToEnemies = baseObj.grabbableToEnemies;
            newObj.isInElevator = baseObj.isInElevator;
            newObj.isInFactory = baseObj.isInFactory;
            newObj.isInShipRoom = baseObj.isInShipRoom;
            newObj.itemProperties = baseObj.itemProperties;
            newObj.propBody = baseObj.propBody;
            newObj.propColliders = baseObj.propColliders;
            newObj.radarIcon = baseObj.radarIcon;
            newObj.useCooldown = baseObj.useCooldown;
        }

        public static void CopyAudioSource(AudioSource original, AudioSource dest)
        {
            dest.clip = original.clip;
            dest.dopplerLevel = original.dopplerLevel;
            dest.ignoreListenerPause = original.ignoreListenerPause;
            dest.ignoreListenerVolume = original.ignoreListenerVolume;
            dest.loop = original.loop;
            dest.maxDistance = original.maxDistance;
            dest.minDistance = original.minDistance;
            dest.mute = original.mute;
            dest.outputAudioMixerGroup = original.outputAudioMixerGroup;
            dest.panStereo = original.panStereo;
            dest.pitch = original.pitch;
            dest.playOnAwake = original.playOnAwake;
            dest.priority = original.priority;
            dest.reverbZoneMix = original.reverbZoneMix;
            dest.rolloffMode = original.rolloffMode;
            dest.spatialBlend = original.spatialBlend;
            dest.spatialize = original.spatialize;
            dest.spatializePostEffects = original.spatializePostEffects;
            dest.spread = original.spread;
            dest.time = original.time;
            dest.timeSamples = original.timeSamples;
            dest.velocityUpdateMode = original.velocityUpdateMode;
            dest.volume = original.volume;
        }
    }
}