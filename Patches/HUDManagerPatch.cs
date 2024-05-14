using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

            // Create lightning overlays on each inventory slot
            if (Plugin.ShowLightningWarnings.Value)
            {
                _lightningOverlays = new List<SpriteRenderer>();
                for (int i = 0; i < __instance.itemSlotIconFrames.Length; i++)
                {
                    var overlay = Object.Instantiate(AssetBundleHelper.LightningOverlay, __instance.itemSlotIconFrames[i].transform);
                    overlay.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    overlay.transform.localScale = Vector3.one;

                    var sprite = overlay.GetComponent<SpriteRenderer>();
                    sprite.enabled = false;
                    _lightningOverlays.Add(sprite);
                }
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

        [HarmonyPatch(typeof(HUDManager), nameof(AssignNodeToUIElement))]
        [HarmonyPrefix]
        private static void AssignNodeToUIElement(ScanNodeProperties node)
        {
            // If we have scanned a player or a masked entity, make sure their health subtext is up to date
            PlayerControllerB player = null;
            MaskedPlayerEnemy masked = null;
            if (Plugin.ScanPlayers.Value && ((node.transform.parent?.TryGetComponent(out player) ?? false) || (node.transform.parent?.TryGetComponent(out masked) ?? false)))
            {
                int curHealth, maxHealth;
                if (player != null)
                {
                    curHealth = player.health;
                    maxHealth = PlayerControllerBPatch.PlayerMaxHealthValues.GetValueOrDefault(player);
                }
                else
                {
                    curHealth = masked.enemyHP;
                    maxHealth = MaskedPlayerEnemyPatch.MaxHealth;
                }
                node.subText = ObjectHelper.GetEntityHealthDescription(curHealth, maxHealth);
                node.nodeType = curHealth <= 0 && (masked == null || masked.enemyHP <= 0) ? 1 : 0; // Red or blue depending on live status
            }
        }

        [HarmonyPatch(typeof(HUDManager), nameof(SetClock))]
        [HarmonyPrefix]
        private static bool SetClock_Pre(float timeNormalized, float numberOfHours, TextMeshProUGUI ___clockNumber, ref string __result)
        {
            if (Plugin.TwentyFourHourClock.Value)
            {
                int totalMinutes = (int)(timeNormalized * (60f * numberOfHours)) + 360;
                int hours = (int)Mathf.Floor(totalMinutes / 60f);
                int minutes = totalMinutes % 60;

                string text = $"{hours}:{$"{minutes}".PadLeft(2, '0')}";
                ___clockNumber.text = text;
                __result = text;

                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(HUDManager), nameof(SetClock))]
        [HarmonyPostfix]
        private static void SetClock()
        {
            MonitorsHelper.UpdateTimeMonitors();
        }

        [HarmonyPatch(typeof(HUDManager), nameof(SetClockVisible))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SetClockVisible(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.AlwaysShowClock.Value)
            {
                Label? elseLabel = null;
                var getInstanceMethod = typeof(StartOfRound).GetMethod("get_Instance");
                var currentLevelField = typeof(StartOfRound).GetField(nameof(StartOfRound.currentLevel));

                if (codeList.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                    i => i.IsLdarg(1),
                    i => i.Branches(out elseLabel),
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(HUDManager).GetField(nameof(HUDManager.Clock)))
                }, out var found))
                {
                    Plugin.MLS.LogDebug("Patching SetClockVisible to always show clock.");

                    // Remove the original visible check
                    codeList.RemoveRange(found.First().Index, 2);

                    // Inject code that looks for spawnEnemiesAndScrap and planetHasTime instead
                    codeList.InsertRange(found.First().Index, new[]
                    {
                        // StartOfRound.Instance != null
                        new CodeInstruction(OpCodes.Call, getInstanceMethod),
                        new CodeInstruction(OpCodes.Ldnull),
                        new CodeInstruction(OpCodes.Beq_S, elseLabel),

                        // && StartOfRound.Instance.currentLevel.spawnEnemiesAndScrap
                        new CodeInstruction(OpCodes.Call, getInstanceMethod),
                        new CodeInstruction(OpCodes.Ldfld, currentLevelField),
                        new CodeInstruction(OpCodes.Ldfld, typeof(SelectableLevel).GetField(nameof(SelectableLevel.spawnEnemiesAndScrap))),
                        new CodeInstruction(OpCodes.Brfalse_S, elseLabel),

                        // && StartOfRound.Instance.currentLevel.planetHasTime
                        new CodeInstruction(OpCodes.Call, getInstanceMethod),
                        new CodeInstruction(OpCodes.Ldfld, currentLevelField),
                        new CodeInstruction(OpCodes.Ldfld, typeof(SelectableLevel).GetField(nameof(SelectableLevel.planetHasTime))),
                        new CodeInstruction(OpCodes.Brfalse_S, elseLabel)
                    });
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected code found - could not transpile HUDManager.SetClockVisible to always show clock!");
                }
            }

            return codeList;
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
                if (_lightningOverlays.Count > 0 && HUDManager.Instance != null && StartOfRound.Instance.localPlayerController != null)
                {
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

        [HarmonyPatch(typeof(HUDManager), nameof(Update))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> UpdateTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.DisplayKgInsteadOfLb.Value)
            {
                if (codeList.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                    i => i.Calls(typeof(Mathf).GetMethod(nameof(Mathf.Clamp), new[] { typeof(float), typeof(float), typeof(float) })),
                    i => i.LoadsConstant(105f),
                    i => i.opcode == OpCodes.Mul,
                    i => i.Calls(typeof(Mathf).GetMethod(nameof(Mathf.RoundToInt))),
                    i => i.opcode == OpCodes.Conv_R4,
                    i => i.IsStloc(),
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(HUDManager).GetField(nameof(HUDManager.weightCounter))),
                    i => i.opcode == OpCodes.Ldstr,
                    i => i.IsLdloc()
                }, out var found))
                {
                    codeList[found[8].Index].operand = "{0} kg";

                    // Convert UI number to a single decimal point kg
                    codeList.InsertRange(found.Last().Index + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldc_R4, 2.205f),
                        new CodeInstruction(OpCodes.Div),
                        new CodeInstruction(OpCodes.Conv_R8),
                        new CodeInstruction(OpCodes.Ldc_I4_1),
                        new CodeInstruction(OpCodes.Call, typeof(System.Math).GetMethod(nameof(System.Math.Round), new[] { typeof(double), typeof(int) }))
                    });

                    Plugin.MLS.LogDebug("Patching HUDManager.Update to use kg instead of lb.");


                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL code found! Could not transpile HUDManager.Update to use kg instead of lb.");
                }
            }

            return codeList;
        }
    }
}