using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using static GeneralImprovements.Plugin.Enums;

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
            if (!__instance.InBuildMode || _snapObjectsByDegrees == 0 || ___placingObject?.parentObject == null)
            {
                return;
            }

            if (_rotateAction.IsPressed())
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