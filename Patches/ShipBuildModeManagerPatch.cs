using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GeneralImprovements.Patches
{
    internal static class ShipBuildModeManagerPatch
    {
        private static int _snapObjectsByDegrees;
        private static float _curObjectDegrees;

        [HarmonyPatch(typeof(ShipBuildModeManager), nameof(Awake))]
        [HarmonyPostfix]
        private static void Awake()
        {
            // Find all intervals of 15 that go into 360
            var validNumbers = Enumerable.Range(0, 360 / 15).Select(n => n * 15).Where(n => n == 0 || 360 % n == 0).ToList();

            // Use the closest valid number to what was specified
            _snapObjectsByDegrees = validNumbers.OrderBy(n => Math.Abs(n - Plugin.SnapObjectsByDegrees.Value)).First();
            Plugin.MLS.LogInfo($"Using {_snapObjectsByDegrees} degrees for build mode snapping");
        }

        [HarmonyPatch(typeof(ShipBuildModeManager), nameof(CreateGhostObjectAndHighlight))]
        [HarmonyPostfix]
        private static void CreateGhostObjectAndHighlight(ShipBuildModeManager __instance)
        {
            if (!__instance.InBuildMode || _snapObjectsByDegrees == 0)
            {
                return;
            }

            // Update the text tips
            if (!StartOfRound.Instance.localPlayerUsingController)
            {
                HUDManager.Instance.buildModeControlTip.text = "Confirm: [B]   |   Rotate: [R]   |   Store: [X]\nFree Rotate: Hold [LAlt]   |   CCW: Hold [LShift]";
            }

            // Set the initial degrees (and snap immediately if they are already holding shift)
            var existingAngles = __instance.ghostObject.eulerAngles;
            if (Keyboard.current[Key.LeftShift].isPressed)
            {
                _curObjectDegrees = (float)Math.Round(existingAngles.y / _snapObjectsByDegrees) * _snapObjectsByDegrees;
                __instance.ghostObject.rotation = Quaternion.Euler(existingAngles.x, _curObjectDegrees, existingAngles.z);
                __instance.selectionOutlineMesh.transform.eulerAngles = __instance.ghostObject.eulerAngles;
            }
            else
            {
                _curObjectDegrees = existingAngles.y;
            }
        }

        [HarmonyPatch(typeof(ShipBuildModeManager), nameof(Update))]
        [HarmonyPostfix]
        private static void Update(ShipBuildModeManager __instance)
        {
            if (!__instance.InBuildMode || _snapObjectsByDegrees == 0)
            {
                return;
            }

            var rotateAction = StartOfRound.Instance.localPlayerUsingController
                ? __instance.playerActions.Movement.InspectItem
                : IngamePlayerSettings.Instance.playerInput.actions.FindAction("ReloadBatteries", false);

            if (rotateAction.IsPressed())
            {
                bool holdingAlt = Keyboard.current[Key.LeftAlt].isPressed;
                bool holdingShift = Keyboard.current[Key.LeftShift].isPressed;

                // If hold free rotate, use vanilla rotation and simply store the current degrees
                if (holdingAlt)
                {
                    // If we want counter clockwise movement, apply vanilla rotation backwards
                    if (holdingShift)
                    {
                        _curObjectDegrees -= Time.deltaTime * 155f;
                    }
                    else
                    {
                        _curObjectDegrees = __instance.ghostObject.eulerAngles.y;
                    }
                }
                else if (rotateAction.WasPressedThisFrame())
                {
                    // First make sure the current degrees are snapped, then add or subtract to them
                    _curObjectDegrees = (float)Math.Round(_curObjectDegrees / _snapObjectsByDegrees) * _snapObjectsByDegrees;
                    _curObjectDegrees += _snapObjectsByDegrees * (holdingShift ? -1 : 1);
                }

                // Now make sure we overwrite whatever the game set the rotation to
                __instance.ghostObject.rotation = Quaternion.Euler(__instance.ghostObject.eulerAngles.x, _curObjectDegrees, __instance.ghostObject.eulerAngles.z);
            }
        }
    }
}