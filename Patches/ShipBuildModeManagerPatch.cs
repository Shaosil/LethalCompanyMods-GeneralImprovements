﻿using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GeneralImprovements.Patches
{
    internal static class ShipBuildModeManagerPatch
    {
        private static int _snapObjectsByDegrees;
        private static float _curObjectDegrees;

        private static string _rotateKeyDesc, _freeRotateModifierKey, _ccwModifierKey;
        private static InputAction _rotateAction;
        private static Func<bool> _freeRotateHeld;
        private static Func<bool> _ccwHeld;

        [HarmonyPatch(typeof(ShipBuildModeManager), nameof(Awake))]
        [HarmonyPostfix]
        private static void Awake(ShipBuildModeManager __instance)
        {
            // Set the keybinds - default to LAlt and LShift for Free Rotate Modifier and CCW Modifier, respectively
            UpdateRotateAction(__instance);
            bool hasFreeRotateModifier = Plugin.FreeRotateKey.Value != Key.None.ToString();
            bool hasCCWModifier = Plugin.CounterClockwiseKey.Value != Key.None.ToString();
            if (Enum.TryParse<Plugin.MouseButton>(Plugin.FreeRotateKey.Value, out var freeRotateMouseButton))
            {
                _freeRotateModifierKey = freeRotateMouseButton.ToString();
                _freeRotateHeld = () => Plugin.GetMouseButtonMapping(freeRotateMouseButton).isPressed;
            }
            else
            {
                var control = hasFreeRotateModifier ? Keyboard.current[Enum.TryParse<Key>(Plugin.FreeRotateKey.Value, out var freeRotateKey) ? freeRotateKey : Key.LeftAlt] : null;
                _freeRotateModifierKey = control?.keyCode.ToString();
                _freeRotateHeld = () => hasFreeRotateModifier && control.isPressed;
            }
            if (Enum.TryParse<Plugin.MouseButton>(Plugin.CounterClockwiseKey.Value, out var ccwMouseButton))
            {
                _ccwModifierKey = ccwMouseButton.ToString();
                _ccwHeld = () => Plugin.GetMouseButtonMapping(ccwMouseButton).isPressed;
            }
            else
            {
                var control = hasCCWModifier ? Keyboard.current[Enum.TryParse<Key>(Plugin.CounterClockwiseKey.Value, out var ccwKey) ? ccwKey : Key.LeftShift] : null;
                _ccwModifierKey = control?.keyCode.ToString();
                _ccwHeld = () => hasCCWModifier && control.isPressed;
            }
            Plugin.MLS.LogInfo($"Snap keys initialized. Rotate: {_rotateKeyDesc}. Free rotate modifier: {_freeRotateModifierKey}. CCW modifier: {_ccwModifierKey}");

            // Find all intervals of 15 that go into 360
            var validNumbers = Enumerable.Range(0, 360 / 15).Select(n => n * 15).Where(n => n == 0 || 360 % n == 0).ToList();

            // Use the closest valid number to what was specified
            _snapObjectsByDegrees = validNumbers.OrderBy(n => Math.Abs(n - Plugin.SnapObjectsByDegrees.Value)).First();
            Plugin.MLS.LogInfo($"Using {_snapObjectsByDegrees} degrees for build mode snapping");
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
                if (_freeRotateModifierKey != null) combinedNewDescs.Add($"Free Rotate: Hold [{_freeRotateModifierKey}]");
                if (_ccwModifierKey != null) combinedNewDescs.Add($"CCW: Hold [{_ccwModifierKey}]");
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