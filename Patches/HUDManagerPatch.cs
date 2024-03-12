﻿using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class HUDManagerPatch
    {
        // Lazy load and cache reflection info
        private static MethodInfo _attemptScanNodeMethod;
        private static MethodInfo AttemptScanNodeMethod => _attemptScanNodeMethod ?? (_attemptScanNodeMethod = typeof(HUDManager).GetMethod("AttemptScanNode", BindingFlags.NonPublic | BindingFlags.Instance));

        private static TextMeshProUGUI _hpText;
        public static GrabbableObject CurrentLightningTarget;
        private static List<SpriteRenderer> _lightningOverlays;

        [HarmonyPatch(typeof(HUDManager), nameof(Start))]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Low)]
        private static void Start(HUDManager __instance)
        {
            if (Plugin.ShowHitPoints.Value && __instance.weightCounterAnimator != null)
            {
                // Copy weight UI object, move it, and remove its animator
                var hpUI = Object.Instantiate(__instance.weightCounterAnimator.gameObject, __instance.weightCounterAnimator.transform.parent);
                hpUI.transform.localPosition += new Vector3(-260, 50, 0);
                hpUI.name = "HPUI";
                Object.Destroy(hpUI.GetComponent<Animator>());

                // Store the text object and change the alignment
                _hpText = hpUI.transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                _hpText.alignment = TextAlignmentOptions.TopRight;
                _hpText.name = "HP";
            }

            CreateLightningOverlays(__instance.itemSlotIconFrames);
        }

        public static void CreateLightningOverlays(UnityEngine.UI.Image[] itemSlotIconFrames)
        {
            _lightningOverlays = new List<SpriteRenderer>();
            for (int i = 0; i < itemSlotIconFrames.Length; i++)
            {
                var overlay = Object.Instantiate(AssetBundleHelper.LightningOverlay, itemSlotIconFrames[i].transform);
                overlay.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                overlay.transform.localScale = Vector3.one;

                var sprite = overlay.GetComponent<SpriteRenderer>();
                sprite.enabled = false;
                _lightningOverlays.Add(sprite);
            }
        }

        [HarmonyPatch(typeof(HUDManager), nameof(AssignNewNodes))]
        [HarmonyPrefix]
        private static bool AssignNewNodes(HUDManager __instance, PlayerControllerB playerScript, ref int ___scannedScrapNum, List<ScanNodeProperties> ___nodesOnScreen)
        {
            if (!Plugin.FixPersonalScanner.Value)
            {
                return true;
            }

            ___nodesOnScreen.Clear();
            ___scannedScrapNum = 0;

            // Get all the in-range scannables in the player's camera viewbox and sort them by distance away from the player
            var camPlanes = GeometryUtility.CalculateFrustumPlanes(playerScript.gameplayCamera);
            var allScannables = Object.FindObjectsOfType<ScanNodeProperties>()
                .Select(s => new KeyValuePair<float, ScanNodeProperties>(Vector3.Distance(s.transform.position, playerScript.transform.position), s))
                .Where(s => ((s.Value.GetComponent<Collider>()?.enabled ?? false) // Active and enabled...
                        || (Plugin.ScanHeldPlayerItems.Value && s.Value.GetComponentInParent<GrabbableObject>() is GrabbableObject g
                            && !g.isPocketed && g.playerHeldBy != null && g.playerHeldBy != playerScript)) // ... or held by someone else
                        && s.Key >= s.Value.minRange && s.Key <= s.Value.maxRange // In range
                        && GeometryUtility.TestPlanesAABB(camPlanes, new Bounds(s.Value.transform.position, Vector3.one))) // In camera view
                .OrderBy(s => s.Key);

            // Now attempt to scan each of them, stopping when we fill the number of UI elements
            foreach (var scannable in allScannables)
            {
                AttemptScanNodeMethod.Invoke(__instance, new object[] { scannable.Value, 0, playerScript });
                if (___nodesOnScreen.Count >= __instance.scanElements.Length)
                {
                    break;
                }
            }

            // Skip the original method
            return false;
        }

        [HarmonyPatch(typeof(HUDManager), nameof(MeetsScanNodeRequirements))]
        [HarmonyPrefix]
        private static bool MeetsScanNodeRequirements(ScanNodeProperties node, PlayerControllerB playerScript, ref bool __result)
        {
            if (node == null)
            {
                __result = false;
            }
            else
            {
                float dist = Vector3.Distance(playerScript.transform.position, node.transform.position);
                bool inRange = dist <= node.maxRange && dist >= node.minRange;
                bool collidesWithRoom = false;

                // If we are in range, include both scan node and room layers when doing the line cast
                if (inRange)
                {
                    var mask = LayerMask.GetMask("ScanNode", "Room");
                    Physics.Linecast(playerScript.gameplayCamera.transform.position, node.transform.position, out var hitInfo, mask, QueryTriggerInteraction.Ignore);
                    collidesWithRoom = hitInfo.transform?.gameObject.layer == LayerMask.NameToLayer("Room");
                }

                __result = inRange && (!node.requiresLineOfSight || !collidesWithRoom);
            }

            // Do not call the original method
            return false;
        }

        [HarmonyPatch(typeof(HUDManager), nameof(UpdateScanNodes))]
        [HarmonyPostfix]
        private static void UpdateScanNodes(RectTransform[] ___scanElements, Dictionary<RectTransform, ScanNodeProperties> ___scanNodes)
        {
            // Disable subtext if desired and it has no text or scrap value
            if (Plugin.HideEmptySubtextOfScanNodes.Value && ___scanElements != null)
            {
                foreach (var scanElement in ___scanElements.Where(s => s.gameObject.activeSelf))
                {
                    var subText = scanElement.GetComponentsInChildren<TextMeshProUGUI>().FirstOrDefault(t => t.name.ToUpper() == "SUBTEXT");
                    var subTextBox = subText?.transform.parent.Find("SubTextBox");

                    if (subTextBox != null && subText != null && ___scanNodes.ContainsKey(scanElement) && ___scanNodes[scanElement] != null)
                    {
                        bool shouldHide = string.IsNullOrWhiteSpace(subText.text) || subText.text.ToUpper().Contains("VALUE: $0");
                        subTextBox.gameObject.SetActive(!shouldHide);
                        subText.enabled = !shouldHide;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(HUDManager), nameof(SetClock))]
        [HarmonyPostfix]
        private static void SetClock(TextMeshProUGUI ___clockNumber)
        {
            MonitorsHelper.UpdateTimeMonitors();
        }

        [HarmonyPatch(typeof(HUDManager), nameof(CanPlayerScan))]
        [HarmonyPostfix]
        private static void CanPlayerScan(ref bool __result)
        {
            __result = __result && !(ShipBuildModeManager.Instance?.InBuildMode ?? false);
        }

        [HarmonyPatch(typeof(HUDManager), nameof(Update))]
        [HarmonyPostfix]
        private static void Update(HUDManager __instance)
        {
            if (_hpText != null && StartOfRound.Instance?.localPlayerController != null)
            {
                _hpText.text = $"{StartOfRound.Instance.localPlayerController.health} HP";
            }

            if ((StormyWeatherPatch.Instance?.isActiveAndEnabled ?? false) && Plugin.ShowLightningWarnings.Value)
            {
                // Toggle lightning overlays on item slots when needed
                if (_lightningOverlays.Any() && HUDManager.Instance != null && StartOfRound.Instance.localPlayerController != null)
                {
                    if (HUDManager.Instance.itemSlotIconFrames.Length > _lightningOverlays.Count)
                    {
                        CreateLightningOverlays(HUDManager.Instance.itemSlotIconFrames);
                    }

                    for (int i = 0; i < Mathf.Min(HUDManager.Instance.itemSlotIconFrames.Length, _lightningOverlays.Count, StartOfRound.Instance.localPlayerController.ItemSlots.Length); i++)
                    {
                        bool shouldBeEnabled = CurrentLightningTarget != null && StartOfRound.Instance.localPlayerController.ItemSlots[i] == CurrentLightningTarget;
                        if (_lightningOverlays[i].enabled != shouldBeEnabled)
                        {
                            _lightningOverlays[i].enabled = shouldBeEnabled;
                        }
                    }
                }
            }
        }
    }
}