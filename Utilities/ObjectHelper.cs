using System.Collections.Generic;
using System.Linq;
using GeneralImprovements.Items;
using GeneralImprovements.Patches;
using Unity.Netcode;
using UnityEngine;

namespace GeneralImprovements.Utilities
{
    internal static class ObjectHelper
    {
        private static IReadOnlyList<NetworkPrefab> AllPrefabs => NetworkManager.Singleton.NetworkConfig.Prefabs.Prefabs;
        public static MedStationItem MedStation = null;
        public static CustomChargeStation ChargeStation = null;
        public static float OriginalChargeYHeight;
        public static int MedStationUnlockableID = -1;
        public static int ChargeStationUnlockableID = -1;

        public static ScanNodeProperties CreateScanNodeOnObject(GameObject obj, int nodeType, int minRange, int maxRange, string headerText, string subText = "", float size = 0.5f)
        {
            var scanNodeObj = new GameObject("ScanNode", typeof(ScanNodeProperties), typeof(BoxCollider), typeof(Rigidbody));
            scanNodeObj.GetComponent<Rigidbody>().isKinematic = true;
            scanNodeObj.transform.localScale = Vector3.one * size;
            scanNodeObj.transform.SetParent(obj.transform, false);
            scanNodeObj.layer = LayerMask.NameToLayer("ScanNode");

            var newScanNode = scanNodeObj.GetComponent<ScanNodeProperties>();
            newScanNode.nodeType = nodeType;
            newScanNode.minRange = minRange;
            newScanNode.maxRange = maxRange;
            newScanNode.headerText = headerText;
            newScanNode.subText = subText;

            return newScanNode;
        }

        public static void CreateMedStation(InteractTrigger chargeStation)
        {
            if (Plugin.AddHealthRechargeStation.Value && AssetBundleHelper.MedStationPrefab != null)
            {
                if (StartOfRound.Instance.IsHost)
                {
                    // If we are the host, spawn it as a network object
                    Plugin.MLS.LogInfo("Adding medical station to ship.");
                    var unlockable = StartOfRound.Instance.unlockablesList.unlockables[MedStationUnlockableID];
                    var position = ES3.Load($"ShipUnlockPos_{unlockable.unlockableName}", GameNetworkManager.Instance.currentSaveFileName, new Vector3(2.75f, 3.4f, -16.561f));
                    var rotation = ES3.Load($"ShipUnlockRot_{unlockable.unlockableName}", GameNetworkManager.Instance.currentSaveFileName, Vector3.zero);

                    var medStationObj = Object.Instantiate(AssetBundleHelper.MedStationPrefab, position, Quaternion.Euler(rotation));
                    MedStation = medStationObj.GetComponent<MedStationItem>();
                    MedStation.NetworkObject.Spawn();
                    MedStation.NetworkObject.TrySetParent(StartOfRound.Instance.elevatorTransform);
                }
                else
                {
                    // If we are a client, try to find it in the scene first
                    Plugin.MLS.LogInfo("Finding networked medical station in ship");
                    MedStation = Object.FindObjectOfType<MedStationItem>();

                    if (MedStation == null)
                    {
                        Plugin.MLS.LogWarning($"Could not find existing med station! Ensure the host has {nameof(Plugin.AddHealthRechargeStation)} set to true.");
                        return;
                    }

                    // AutoParentToShip awakes too early - clients need to overwrite this here
                    MedStation.GetComponent<AutoParentToShip>().positionOffset = new Vector3(1.4785f, 3.1216f, -9.061f);
                }

                MedStation.GetComponent<AudioSource>().outputAudioMixerGroup = chargeStation.GetComponent<AudioSource>().outputAudioMixerGroup;

                // Add icon and event to interaction trigger, and lock position node height to the ship floor
                var medTrigger = MedStation.GetComponentInChildren<InteractTrigger>();
                medTrigger.hoverIcon = chargeStation.hoverIcon;
                medTrigger.onInteractEarly.AddListener(_ => MedStation.HealLocalPlayer());
                var posNode = medTrigger.transform.GetChild(0);
                posNode.position = new Vector3(posNode.position.x, OriginalChargeYHeight, posNode.position.z);

                // Depend on the light switch for copying audio
                var lightSwitchPlaceable = StartOfRound.Instance.elevatorTransform.Find("LightSwitchContainer")?.GetComponentInChildren<PlaceableShipObject>();
                CopyPlaceableAudio(MedStation.gameObject, lightSwitchPlaceable);
            }
        }

        public static void MakeChargeStationPlaceable(InteractTrigger existingChargeTrigger)
        {
            if (Plugin.AllowChargerPlacement.Value && AssetBundleHelper.ChargeStationPrefab != null && existingChargeTrigger?.transform.parent?.parent is Transform charger)
            {
                Plugin.MLS.LogInfo("Allowing item charger to be placeable.");

                if (StartOfRound.Instance.IsHost)
                {
                    // If we are the host, spawn it as a network object
                    Plugin.MLS.LogInfo("Adding networked item charger to ship.");
                    var unlockable = StartOfRound.Instance.unlockablesList.unlockables[ChargeStationUnlockableID];
                    var position = ES3.Load($"ShipUnlockPos_{unlockable.unlockableName}", GameNetworkManager.Instance.currentSaveFileName, charger.position);
                    var rotation = ES3.Load($"ShipUnlockRot_{unlockable.unlockableName}", GameNetworkManager.Instance.currentSaveFileName, Vector3.zero);

                    var chargeStationObject = Object.Instantiate(AssetBundleHelper.ChargeStationPrefab, position, Quaternion.Euler(rotation));
                    ChargeStation = chargeStationObject.GetComponent<CustomChargeStation>();
                    ChargeStation.NetworkObject.Spawn();
                    ChargeStation.NetworkObject.TrySetParent(StartOfRound.Instance.elevatorTransform);
                }
                else
                {
                    // If we are a client, try to find it in the scene first
                    Plugin.MLS.LogInfo("Finding networked item charger in ship");
                    ChargeStation = Object.FindObjectOfType<CustomChargeStation>();

                    if (ChargeStation == null)
                    {
                        Plugin.MLS.LogWarning($"Could not find existing networked item charger! Ensure the host has {nameof(Plugin.AllowChargerPlacement)} set to true.");
                        return;
                    }

                    // AutoParentToShip awakes too early - clients need to overwrite this here
                    ChargeStation.GetComponent<AutoParentToShip>().positionOffset = new Vector3(1.4798f, 2.2167f, -8.918f);
                }

                // Store some variables
                var ourTrigger = ChargeStation.GetComponentInChildren<InteractTrigger>();
                var posNode = ourTrigger.transform.GetChild(0);
                var chargeComponent = existingChargeTrigger.GetComponent<ItemCharger>();
                var terminalPlaceable = TerminalPatch.Instance.transform.parent.parent.GetComponentInChildren<PlaceableShipObject>();
                var zapLight = charger.Find("ZapLight");
                var zapParticle = charger.Find("ZapParticle");

                // Copy over the things from the real charger that we couldn't set in our prefab, and lock position node height to the ship floor
                ourTrigger.hoverIcon = existingChargeTrigger.hoverIcon;
                ChargeStation.transform.Find("Model").GetComponent<MeshRenderer>().sharedMaterials = charger.GetComponent<MeshRenderer>().sharedMaterials   // Base 7 materials
                    .Concat(charger.Find("ChargeInductiveCoil").GetComponent<MeshRenderer>().sharedMaterials)                                               // Coil material
                    .Concat(charger.Find("ChargeProng1").GetComponent<MeshRenderer>().sharedMaterials).ToArray();                                           // Prongs material
                chargeComponent.triggerScript = ourTrigger;
                chargeComponent.zapAudio = CopyAudioSource(existingChargeTrigger.GetComponent<AudioSource>(), ourTrigger.GetComponent<AudioSource>());
                ourTrigger.onInteractEarly.AddListener(_ => chargeComponent.ChargeItem());
                posNode.position = new Vector3(posNode.position.x, OriginalChargeYHeight, posNode.position.z);

                // Move the zap effects to the new one
                var placeable = CopyPlaceableAudio(ChargeStation.gameObject, terminalPlaceable);
                zapLight.SetParent(ChargeStation.transform);
                zapLight.position = placeable.transform.position;
                zapParticle.SetParent(ChargeStation.transform);
                zapParticle.position = placeable.transform.position;

                // Disable/hide the original charger
                charger.GetComponentsInChildren<MeshRenderer>().ToList().ForEach(r => r.enabled = false);
                existingChargeTrigger.GetComponent<BoxCollider>().enabled = false;
                charger.GetComponentInChildren<ScanNodeProperties>().GetComponent<BoxCollider>().enabled = false;
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
                toggleable.itemProperties.toolTips = new[] { "Toggle Light : [LMB]" };
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

        public static AudioSource CopyAudioSource(AudioSource original, AudioSource dest)
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

            return dest;
        }

        public static void DestroyLocalItemAndSync(int slot)
        {
            if (StartOfRound.Instance.localPlayerController != null)
            {
                // Manually disable the HUD icon since vanilla doesn't do it for this function
                StartOfRound.Instance.localPlayerController.DestroyItemInSlotAndSync(slot);

                if (StartOfRound.Instance.localPlayerController.ItemSlots[slot] == null && HUDManager.Instance != null)
                {
                    HUDManager.Instance.itemSlotIcons[slot].enabled = false;
                }
            }
        }

        public static string GetEntityHealthDescription(int curHP, int maxHP)
        {
            float pct = (float)curHP / maxHP;

            return pct > 1 ? "Radiant" : pct >= 0.75f ? "Healthy" : pct >= 0.5f ? "Injured" : pct >= 0.25f ? "Badly Injured" : pct > 0 ? "Near Death" : "Deceased";
        }

        public static int AddUnlockable(StartOfRound startOfRound, string name)
        {
            int existingUnlockable = startOfRound.unlockablesList.unlockables.FindIndex(u => u.unlockableName == name);
            if (existingUnlockable >= 0) return existingUnlockable;

            var unlockable = new UnlockableItem
            {
                unlockableName = name,
                unlockableType = 1,
                IsPlaceable = true,
                canBeStored = false,
                maxNumber = 1,
                alreadyUnlocked = true,
                spawnPrefab = false
            };
            startOfRound.unlockablesList.unlockables.Add(unlockable);

            return startOfRound.unlockablesList.unlockables.Count - 1;
        }

        public static PlaceableShipObject CopyPlaceableAudio(GameObject gameObject, PlaceableShipObject copyAudioFrom)
        {
            var placeable = gameObject.GetComponentInChildren<PlaceableShipObject>();

            if (copyAudioFrom != null)
            {
                CopyAudioSource(copyAudioFrom.GetComponent<AudioSource>(), placeable.GetComponent<AudioSource>());
                placeable.placeObjectSFX = copyAudioFrom.placeObjectSFX;
            }
            else
            {
                Plugin.MLS.LogWarning("Could not copy audio between PlaceableShipObjects. The copyAudioFrom parameter was null.");
            }

            return placeable;
        }
    }
}