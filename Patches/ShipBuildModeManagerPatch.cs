using HarmonyLib;
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
        private static int _curObjectDegrees;

        [HarmonyPatch(typeof(ShipBuildModeManager), nameof(Awake))]
        [HarmonyPostfix]
        private static void Awake()
        {
            // Find all intervals of 15 that go into 360
            var validNumbers = new List<int> { 0 };
            for (int i = 15; i < 360; i += 15)
            {
                if (360 % i == 0)
                {
                    validNumbers.Add(i);
                }
            }

            // Find closest valid number to what was specified
            var absoluteResults = new List<KeyValuePair<int, int>>();
            var specifiedDegrees = Plugin.SnapObjectsByDegrees.Value;
            for (int i = 0; i < validNumbers.Count; i++)
            {
                absoluteResults.Add(new KeyValuePair<int, int>(i, Math.Abs(validNumbers[i] - specifiedDegrees)));
            }
            absoluteResults = absoluteResults.OrderBy(r => r.Value).ToList();

            _snapObjectsByDegrees = validNumbers[absoluteResults[0].Key];
            Plugin.MLS.LogInfo($"Using {_snapObjectsByDegrees} for build mode snapping");
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
            if (StartOfRound.Instance.localPlayerUsingController)
            {
                HUDManager.Instance.buildModeControlTip.text = "Confirm: [Y]   |   Rotate: [L-shoulder]   |   Store: [B]";
            }
            else
            {
                HUDManager.Instance.buildModeControlTip.text = "Confirm: [B]   |   Rotate CW: [R] (Hold Shift for CCW)   |   Store: [X]";
            }

            // Set the initial degrees
            var existingAngles = __instance.ghostObject.eulerAngles;
            _curObjectDegrees = (int)Math.Round(existingAngles.y / _snapObjectsByDegrees) * _snapObjectsByDegrees;
            __instance.ghostObject.rotation = Quaternion.Euler(existingAngles.x, _curObjectDegrees, existingAngles.z);
            __instance.selectionOutlineMesh.transform.eulerAngles = __instance.ghostObject.eulerAngles;
        }

        [HarmonyPatch(typeof(ShipBuildModeManager), nameof(Update))]
        [HarmonyPostfix]
        private static void Update(ShipBuildModeManager __instance)
        {
            if (!__instance.InBuildMode || _snapObjectsByDegrees == 0)
            {
                return;
            }


            var rotateActionKeyboard = IngamePlayerSettings.Instance.playerInput.actions.FindAction("ReloadBatteries", false);
            var rotateActionController = __instance.playerActions.Movement.InspectItem;
            if (rotateActionKeyboard.IsPressed() || (StartOfRound.Instance.localPlayerUsingController && rotateActionController.IsPressed()))
            {
                // If the rotate button was pressed this frame, increment the degree storage variable and assign it to the object
                if (rotateActionKeyboard.WasPressedThisFrame() || (StartOfRound.Instance.localPlayerUsingController && rotateActionController.WasPressedThisFrame()))
                {
                    bool holdingShift = Keyboard.current[Key.LeftShift].IsPressed();
                    _curObjectDegrees += _snapObjectsByDegrees * (holdingShift ? -1 : 1);
                }

                // Now make sure we overwrite whatever the game set the rotation to
                __instance.ghostObject.rotation = Quaternion.Euler(__instance.ghostObject.eulerAngles.x, _curObjectDegrees, __instance.ghostObject.eulerAngles.z);
            }
        }
    }
}