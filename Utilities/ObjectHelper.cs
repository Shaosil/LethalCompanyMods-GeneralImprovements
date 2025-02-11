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
        public static Dictionary<PlaceableShipObject, InteractTrigger> PlaceablesToTriggers = new Dictionary<PlaceableShipObject, InteractTrigger>();
        public static float OriginalChargeYHeight;
        public static int MedStationUnlockableID = -1;
        public static int ChargeStationUnlockableID = -1;

        public static ScanNodeProperties CreateScanNodeOnObject(GameObject obj, int nodeType, int minRange, int maxRange, string headerText, string subText = "", int size = 1)
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
                    var position = new Vector3(2.75f, 3.4f, -16.561f);
                    var rotation = new Vector3(-90, 0, 0);
                    if (MedStationUnlockableID >= 0)
                    {
                        var unlockable = StartOfRound.Instance.unlockablesList.unlockables[MedStationUnlockableID];
                        bool hasMoved = ES3.KeyExists($"ShipUnlockedMoved_{unlockable.unlockableName}", GameNetworkManager.Instance.currentSaveFileName);
                        position = ES3.Load($"ShipUnlockPos_{unlockable.unlockableName}", GameNetworkManager.Instance.currentSaveFileName, position);
                        rotation = ES3.Load($"ShipUnlockRot_{unlockable.unlockableName}", GameNetworkManager.Instance.currentSaveFileName, rotation);
                    }
                    var medStationObj = Object.Instantiate(AssetBundleHelper.MedStationPrefab, position, Quaternion.Euler(rotation));
                    MedStation = medStationObj.GetComponent<MedStationItem>();
                    MedStation.NetworkObject.Spawn();
                    MedStation.NetworkObject.TrySetParent(StartOfRound.Instance.elevatorTransform);
                }
                else
                {
                    // If we are a client, try to find it in the scene first
                    Plugin.MLS.LogInfo("Finding medical station in ship");
                    MedStation = Object.FindObjectOfType<MedStationItem>();

                    if (MedStation == null)
                    {
                        Plugin.MLS.LogWarning($"Could not find existing med station! Ensure the host has {nameof(Plugin.AddHealthRechargeStation)} set to true.");
                        return;
                    }
                }

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
                interactScript.playerPositionNode = medTrigger.transform.GetChild(0);
                interactScript.onInteract = new InteractEvent();
                interactScript.onCancelAnimation = new InteractEvent();
                interactScript.onInteractEarly = new InteractEvent();
                interactScript.onInteractEarly.AddListener(_ => MedStation.HealLocalPlayer());

                // Depend on the light switch for copying audio
                var lightSwitchPlaceable = StartOfRound.Instance.elevatorTransform.Find("LightSwitchContainer")?.GetComponentInChildren<PlaceableShipObject>();
                var placeable = AddPlaceableComponentToGameObject(MedStation.gameObject, MedStationUnlockableID, 0.05f, lightSwitchPlaceable);
                PlaceablesToTriggers.Add(placeable, interactScript);

                // Add scan node
                CreateScanNodeOnObject(MedStation.gameObject, 0, 0, 6, "Med Station", "Fully heal yourself");
            }
        }

        public static void MakeChargeStationPlaceable(InteractTrigger chargeStation)
        {
            if (Plugin.AllowChargerPlacement && chargeStation?.transform.parent?.parent is Transform charger)
            {
                Plugin.MLS.LogInfo("Allowing item charger to be placeable.");

                // Add a new placeable gameobject with a box collider
                var placementObject = new GameObject("PlacementCollider");
                placementObject.transform.SetParent(charger.transform);
                placementObject.transform.SetLocalPositionAndRotation(new Vector3(0.35f, 0, 0), Quaternion.identity);
                placementObject.layer = LayerMask.NameToLayer("PlaceableShipObjects");
                var collider = placementObject.AddComponent<BoxCollider>();
                collider.size = new Vector3(1, 2, 2);
                placementObject.AddComponent<AudioSource>();

                // Add the actual placeable components
                var terminalPlaceable = TerminalPatch.Instance.transform.parent.parent.GetComponentInChildren<PlaceableShipObject>();
                var placeable = AddPlaceableComponentToGameObject(charger.gameObject, ChargeStationUnlockableID, 0.75f, terminalPlaceable);
                PlaceablesToTriggers.Add(placeable, chargeStation);
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

        public static int AddUnlockable(string name)
        {
            int existingUnlockable = StartOfRound.Instance.unlockablesList.unlockables.FindIndex(u => u.unlockableName == name);
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
            StartOfRound.Instance.unlockablesList.unlockables.Add(unlockable);

            return StartOfRound.Instance.unlockablesList.unlockables.Count - 1;
        }

        public static PlaceableShipObject AddPlaceableComponentToGameObject(GameObject gameObject, int unlockableID, float wallOffset, PlaceableShipObject copyAudioFrom)
        {
            if (copyAudioFrom != null)
            {
                // Add PlaceableShipObject and AutoParentToShip
                var placeable = gameObject.transform.Find("PlacementCollider").gameObject.AddComponent<PlaceableShipObject>();
                placeable.gameObject.tag = "PlaceableObject";
                placeable.unlockableID = unlockableID;
                placeable.parentObject = gameObject.AddComponent<AutoParentToShip>();
                placeable.placeObjectCollider = placeable.GetComponent<BoxCollider>();
                placeable.AllowPlacementOnWalls = true;
                placeable.mainMesh = gameObject.GetComponentInChildren<MeshFilter>();
                placeable.mainTransform = placeable.mainMesh.transform;
                placeable.overrideWallOffset = true;
                placeable.wallOffset = wallOffset;
                CopyAudioSource(copyAudioFrom.GetComponent<AudioSource>(), placeable.GetComponent<AudioSource>());
                placeable.placeObjectSFX = copyAudioFrom.placeObjectSFX;

                return placeable;
            }
            else
            {
                Plugin.MLS.LogError("Error - Could not create a ship placeable. The copyAudioFrom parameter was null.");
            }

            return null;
        }
    }
}