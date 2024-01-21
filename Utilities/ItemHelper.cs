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