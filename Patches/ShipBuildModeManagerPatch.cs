using System;
using System.Collections.Generic;
using System.Linq;
using GeneralImprovements.Utilities;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using static GeneralImprovements.Enums;

namespace GeneralImprovements.Patches
{
    internal static class ShipBuildModeManagerPatch
    {
        private static int _snapObjectsByDegrees;
        private static float _curObjectDegrees;

        private static string _rotateKeyDesc;
        private static InputAction _rotateAction;
        private static Func<bool> _freeRotateHeld;
        private static Func<bool> _ccwHeld;

        [HarmonyPatch(typeof(ShipBuildModeManager), nameof(Awake))]
        [HarmonyPostfix]
        private static void Awake(ShipBuildModeManager __instance, ref int ___placementMask, ref int ___placementMaskAndBlockers)
        {
            // Set the keybinds - default to LAlt and LShift for Free Rotate Modifier and CCW Modifier, respectively
            UpdateRotateAction(__instance);
            bool hasFreeRotateModifier = Plugin.FreeRotateKey.Value != eValidKeys.None;
            bool hasCCWModifier = Plugin.CounterClockwiseKey.Value != eValidKeys.None;
            if (Plugin.FreeRotateKey.Value >= eValidKeys.MouseLeft)
            {
                _freeRotateHeld = () => GetMouseButtonMapping(Plugin.FreeRotateKey.Value).isPressed;
            }
            else
            {
                var control = hasFreeRotateModifier ? Keyboard.current[Enum.TryParse<Key>(Plugin.FreeRotateKey.Value.ToString(), out var freeRotateKey) ? freeRotateKey : Key.LeftAlt] : null;
                _freeRotateHeld = () => hasFreeRotateModifier && control.isPressed;
            }
            if (Plugin.CounterClockwiseKey.Value >= eValidKeys.MouseLeft)
            {
                _ccwHeld = () => GetMouseButtonMapping(Plugin.CounterClockwiseKey.Value).isPressed;
            }
            else
            {
                var control = hasCCWModifier ? Keyboard.current[Enum.TryParse<Key>(Plugin.CounterClockwiseKey.Value.ToString(), out var ccwKey) ? ccwKey : Key.LeftShift] : null;
                _ccwHeld = () => hasCCWModifier && control.isPressed;
            }
            Plugin.MLS.LogInfo($"Snap keys initialized. Rotate: {_rotateKeyDesc}. Free rotate modifier: {Plugin.FreeRotateKey.Value}. CCW modifier: {Plugin.CounterClockwiseKey.Value}");

            // Find all intervals of 15 that go into 360
            var validNumbers = Enumerable.Range(0, 360 / 15).Select(n => n * 15).Where(n => n == 0 || 360 % n == 0).ToList();

            // Use the closest valid number to what was specified
            _snapObjectsByDegrees = validNumbers.OrderBy(n => Math.Abs(n - Plugin.SnapObjectsByDegrees.Value)).First();
            Plugin.MLS.LogInfo($"Using {_snapObjectsByDegrees} degrees for build mode snapping");

            // Override placeable collision mask if specified
            if (!Plugin.ShipPlaceablesCollide.Value)
            {
                ___placementMask = LayerMask.GetMask("Room", "Colliders");
                ___placementMaskAndBlockers = LayerMask.GetMask("Room");
            }
        }

        [HarmonyPatch(typeof(ShipBuildModeManager), nameof(CreateGhostObjectAndHighlight))]
        [HarmonyPostfix]
        private static void CreateGhostObjectAndHighlight(ShipBuildModeManager __instance, PlaceableShipObject ___placingObject)
        {
            if (!__instance.InBuildMode || _snapObjectsByDegrees == 0 || ___placingObject?.parentObject == null)
            {
                return;
            }

            UpdateRotateAction(__instance);

            // Update the text tips
            if (!StartOfRound.Instance.localPlayerUsingController)
            {
                string vanillaDesc = $"Confirm: [B]   |   Rotate: [{_rotateKeyDesc}]   |   Store: [X]";
                var combinedNewDescs = new List<string>();
                if (Plugin.FreeRotateKey.Value != eValidKeys.None) combinedNewDescs.Add($"Free Rotate: Hold [{Plugin.FreeRotateKey.Value}]");
                if (Plugin.CounterClockwiseKey.Value != eValidKeys.None) combinedNewDescs.Add($"CCW: Hold [{Plugin.CounterClockwiseKey.Value}]");
                if (combinedNewDescs.Count > 0) vanillaDesc += '\n';

                HUDManager.Instance.buildModeControlTip.text = $"{vanillaDesc}{string.Join("  |  ", combinedNewDescs.ToArray())}";
            }

            // Set the initial degrees (and snap immediately unless they are already holding the free rotate modifier)
            var existingAngles = __instance.ghostObject.transform.eulerAngles;
            if (_freeRotateHeld())
            {
                _curObjectDegrees = existingAngles.y;
            }
            else
            {
                float existingOffset = AutoParentToShipPatch.Offsets.GetValueOrDefault(___placingObject.parentObject, 0f);
                _curObjectDegrees = ((float)Math.Round(existingAngles.y / _snapObjectsByDegrees) * _snapObjectsByDegrees) + existingOffset;
                __instance.ghostObject.rotation = Quaternion.Euler(existingAngles.x, _curObjectDegrees, existingAngles.z);
            }
        }

        [HarmonyPatch(typeof(ShipBuildModeManager), nameof(Update))]
        [HarmonyPostfix]
        private static void Update(ShipBuildModeManager __instance, PlaceableShipObject ___placingObject)
        {
            if (__instance.InBuildMode && ___placingObject?.parentObject != null)
            {
                // Handle rotation hotkeys
                if (_snapObjectsByDegrees != 0 && _rotateAction.IsPressed())
                {
                    // If hold free rotate, use vanilla rotation and simply store the current degrees
                    if (_freeRotateHeld())
                    {
                        // If we want counter clockwise movement, apply vanilla rotation backwards
                        if (_ccwHeld())
                        {
                            _curObjectDegrees -= Time.deltaTime * 155f;
                        }
                        else
                        {
                            _curObjectDegrees = __instance.ghostObject.eulerAngles.y;
                        }
                    }
                    else if (_rotateAction.WasPressedThisFrame())
                    {
                        // Just add or subtract to the current degrees. If they want it lined up to the world grid they will need to cancel and rebuild
                        _curObjectDegrees += _snapObjectsByDegrees * (_ccwHeld() ? -1 : 1);
                    }

                    // Now make sure we overwrite whatever the game set the rotation to
                    __instance.ghostObject.rotation = Quaternion.Euler(__instance.ghostObject.eulerAngles.x, _curObjectDegrees, __instance.ghostObject.eulerAngles.z);
                }

                // If this is the medkit, clamp it to a certain height
                if (ObjectHelper.MedStation != null && ___placingObject.parentObject.gameObject == ObjectHelper.MedStation.gameObject
                    && (__instance.ghostObject.position.y < 1.75f || __instance.ghostObject.position.y > 3.5f))
                {
                    float clampedY = Mathf.Clamp(__instance.ghostObject.position.y, 1.75f, 3.5f);
                    __instance.ghostObject.position = new Vector3(__instance.ghostObject.position.x, clampedY, __instance.ghostObject.position.z);
                }
            }
        }

        [HarmonyPatch(typeof(ShipBuildModeManager), nameof(ShipBuildModeManager.PlaceShipObject))]
        [HarmonyPrefix]
        private static void PlaceShipObjectPre(PlaceableShipObject placeableObject, ref float __state)
        {
            // If this was the med station or charging station, reset the player position node(s) Y
            if (ObjectHelper.PlaceablesToTriggers.ContainsKey(placeableObject))
            {
                // Store the current position offset Y value here to be picked up by the postfix
                __state = placeableObject.parentObject.positionOffset.y;
            }
        }

        [HarmonyPatch(typeof(ShipBuildModeManager), nameof(ShipBuildModeManager.PlaceShipObject))]
        [HarmonyPostfix]
        private static void PlaceShipObjectPost(PlaceableShipObject placeableObject, ref float __state)
        {
            if (__state != default)
            {
                float yDiff = placeableObject.parentObject.positionOffset.y - __state;

                var trigger = ObjectHelper.PlaceablesToTriggers[placeableObject];
                List<Transform> allNodes = new List<Transform>();
                for (int i = 0; i < trigger.transform.childCount; i++)
                {
                    var child = trigger.transform.GetChild(i);
                    float newY = child.transform.position.y - yDiff;
                    child.transform.SetPositionAndRotation(new Vector3(child.transform.position.x, newY, child.transform.position.z), child.transform.rotation);
                    allNodes.Add(child);
                }

                // In case there are multiple target nodes, pick the one that is closest to the ship bounds center
                if (allNodes.Count > 0)
                {
                    // Pre-position and rotate the object here so that we can accurately calculate where the closest node should be
                    placeableObject.parentObject.transform.position = StartOfRound.Instance.elevatorTransform.position;
                    placeableObject.parentObject.transform.rotation = StartOfRound.Instance.elevatorTransform.rotation;
                    placeableObject.parentObject.transform.Rotate(placeableObject.parentObject.rotationOffset);
                    placeableObject.parentObject.transform.position += (StartOfRound.Instance.elevatorTransform.rotation * placeableObject.parentObject.positionOffset);

                    trigger.playerPositionNode = allNodes.OrderBy(n => Vector3.Distance(StartOfRound.Instance.shipInnerRoomBounds.transform.position, n.transform.position)).First();
                }
            }
        }

        private static void UpdateRotateAction(ShipBuildModeManager instance)
        {
            _rotateAction = StartOfRound.Instance.localPlayerUsingController
                ? instance.playerActions.Movement.InspectItem
                : IngamePlayerSettings.Instance.playerInput.actions.FindAction("ReloadBatteries", false);

            _rotateKeyDesc = _rotateAction.GetBindingDisplayString();
        }
    }
}