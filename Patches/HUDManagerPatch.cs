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

        [HarmonyPatch(typeof(HUDManager), "PingScan_performed")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PingScan_performedTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codeList = instructions.ToList();

            if (Plugin.FixPersonalScanner.Value)
            {
                if (codeList.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                    i => i.IsLdarg(0),
                    i => i.opcode == OpCodes.Ldc_R4,
                    i => i.StoresField(typeof(HUDManager).GetField("playerPingingScan", BindingFlags.Instance | BindingFlags.NonPublic))
                }, out var pingCode))
                {
                    Plugin.MLS.LogDebug("Patching HUDManager.PingScan_performed to assign nodes on demand.");

                    // Define and assign a label to jump to if we don't need to call AssignNewNodes
                    var nextLabel = generator.DefineLabel();
                    codeList[pingCode.Last().Index + 1].labels.Add(nextLabel);

                    codeList.InsertRange(pingCode.Last().Index + 1, new CodeInstruction[]
                    {
                        // If updateScanInterval was recently called (< 0.25s ago), skip past the manual call
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, typeof(HUDManager).GetField("updateScanInterval", BindingFlags.Instance | BindingFlags.NonPublic)),
                        new CodeInstruction(OpCodes.Ldc_R4, 0.25f),
                        new CodeInstruction(OpCodes.Blt, nextLabel),

                        // Load required parameter onto the stack
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Call, typeof(GameNetworkManager).GetMethod("get_Instance")),
                        new CodeInstruction(OpCodes.Ldfld, typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.localPlayerController))),
                        
                        // Call AssignNewNodes on demand
                        new CodeInstruction(OpCodes.Call, typeof(HUDManager).GetMethod("AssignNewNodes", BindingFlags.Instance | BindingFlags.NonPublic))
                    });
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL Code - Could not patch HUDManager.PingScan_performed to assign nodes on demand!");
                }
            }

            return codeList;
        }

        [HarmonyPatch(typeof(HUDManager), nameof(AssignNewNodes))]
        [HarmonyPrefix]
        private static bool AssignNewNodes(HUDManager __instance, PlayerControllerB playerScript, ref int ___scannedScrapNum, List<ScanNodeProperties> ___nodesOnScreen)
        {
            if (!Plugin.FixPersonalScanner.Value || playerScript?.gameplayCamera == null)
            {
                return true;
            }

            ___nodesOnScreen.Clear();
            ___scannedScrapNum = 0;

            // Get the planes of the active camera view
            var camPlanes = GeometryUtility.CalculateFrustumPlanes(playerScript.gameplayCamera);

            // Cast a giant sphere 100f around ourself to get scan nodes we collided with, ordered by distance
            var nearbyScanNodes = Physics.OverlapSphere(playerScript.gameplayCamera.transform.position, 100f, 0x400000)
                .Select(n => new KeyValuePair<float, ScanNodeProperties>(Vector3.Distance(n.transform.position, playerScript.transform.position), n.transform.GetComponent<ScanNodeProperties>()))
                .Where(s => s.Value != null && s.Key >= s.Value.minRange && s.Key <= s.Value.maxRange                   // In range
                    && GeometryUtility.TestPlanesAABB(camPlanes, new Bounds(s.Value.transform.position, Vector3.one)))  // In camera view
                .OrderBy(n => n.Key);

            // Now attempt to scan each of them, stopping when we fill the number of UI elements
            foreach (var scannable in nearbyScanNodes.Select(s => s.Value))
            {
                __instance.AttemptScanNode(scannable, 0, playerScript);
                if (___nodesOnScreen.Count >= __instance.scanElements.Length)
                {
                    break;
                }
            }

            // Skip the original method
            return false;
        }

        [HarmonyPatch(typeof(HUDManager), "MeetsScanNodeRequirements")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> MeetsScanNodeRequirementsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.FixPersonalScanner.Value)
            {
                if (codeList.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                    i => i.LoadsConstant(0x100),
                    i => i.LoadsConstant(1),
                    i => i.Calls(typeof(Physics).GetMethod(nameof(Physics.Linecast), new System.Type[] { typeof(Vector3), typeof(Vector3), typeof(int), typeof(QueryTriggerInteraction) }))
                }, out var scanCode))
                {
                    Plugin.MLS.LogDebug("Patching HUDManager.MeetsScanNodeRequiredments to include scan nodes in the linecast check.");

                    // Change the layer mask to be scan node (22) and room (8)
                    codeList[scanCode[0].Index].operand = 0x400100;

                    // Change the simple linecast bool call to only return true if the collision is with a room
                    codeList[scanCode.Last().Index] = Transpilers.EmitDelegate<System.Func<Vector3, Vector3, int, QueryTriggerInteraction, bool>>((start, end, mask, interaction) =>
                    {
                        return Physics.Linecast(start, end, out var hitInfo, mask, interaction) && hitInfo.transform?.gameObject.layer == 8;
                    });
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL Code - Could not transpile HUDManager.MeetsScanNodeRequiredments!");
                }
            }

            return codeList;
        }

        [HarmonyPatch(typeof(HUDManager), "UpdateScanNodes")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> UpdateScanNodesTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codeList = instructions.ToList();

            // Increase scan interval if FixPersonalScanner is active since it is more performance heavy
            if (Plugin.FixPersonalScanner.Value)
            {
                if (codeList.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                    i => i.IsLdarg(0),
                    i => i.LoadsConstant(0.25f),
                    i => i.StoresField(typeof(HUDManager).GetField("updateScanInterval", BindingFlags.Instance | BindingFlags.NonPublic))
                }, out var scanIntervalCode))
                {
                    Plugin.MLS.LogDebug("Patching HUDManager.UpdateScanNodes to increase scan interval.");

                    scanIntervalCode[1].Instruction.operand = 1f;
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL Code - Could not patch HUDManager.UpdateScanNodes to increase scan interval!");
                }
            }

            // Disable subtext if desired and it has no text or scrap value
            if (Plugin.HideEmptySubtextOfScanNodes.Value)
            {
                var scanElementTextField = typeof(HUDManager).GetField("scanElementText", BindingFlags.NonPublic | BindingFlags.Instance);

                if (codeList.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                        // Store text components from children
                        i => i.StoresField(scanElementTextField),

                        // scanElementText.Length < 1
                        i => i.IsLdarg(0), // Index 1
                        i => i.LoadsField(scanElementTextField),
                        i => i.opcode == OpCodes.Ldlen,
                        i => i.opcode == OpCodes.Conv_I4,
                        i => i.LoadsConstant(1),
                        i => i.Branches(out _),

                        // Set header text [0]
                        i => i.IsLdarg(0), // Index 7
                        i => i.LoadsField(scanElementTextField),
                        i => i.LoadsConstant(0),
                        i => i.opcode == OpCodes.Ldelem_Ref,
                        i => i.IsLdloc(),
                        i => i.LoadsField(typeof(ScanNodeProperties).GetField(nameof(ScanNodeProperties.headerText))),
                        i => i.Calls(typeof(TMP_Text).GetMethod("set_text")),

                        // Set sub text [1]
                        i => i.IsLdarg(0), // Index 14
                        i => i.LoadsField(scanElementTextField),
                        i => i.LoadsConstant(1),
                        i => i.opcode == OpCodes.Ldelem_Ref,
                        i => i.IsLdloc(),
                        i => i.LoadsField(typeof(ScanNodeProperties).GetField(nameof(ScanNodeProperties.subText))),
                        i => i.Calls(typeof(TMP_Text).GetMethod("set_text")),

                        // The first line of the rest of the code
                        i => i.IsLdloc(),
                }, out var scanCode))
                {
                    Plugin.MLS.LogDebug("Patching HudManager.UpdateScanNodes to hide subtext when needed.");

                    // Insert if/else for hiding/showing the subtext stuff
                    codeList.InsertRange(scanCode.Last().Index, new CodeInstruction[]
                    {
                        // Load the text and scan node onto the stack for the following delegate
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, scanElementTextField),
                        new CodeInstruction(OpCodes.Ldc_I4_1),
                        new CodeInstruction(OpCodes.Ldelem_Ref), // scanElementText[1]
                        new CodeInstruction(OpCodes.Ldloc_2), // scanNodeProperties out var

                        Transpilers.EmitDelegate<System.Action<TextMeshProUGUI, ScanNodeProperties>>((text, scanNodeProperties) =>
                        {
                            bool shouldHide = scanNodeProperties.subText == "Value: $0" || string.IsNullOrWhiteSpace(scanNodeProperties.subText);
                            if (shouldHide) text.text = string.Empty;
                            text.transform.parent.Find("SubTextBox").gameObject.SetActive(!shouldHide);
                        }),
                    });
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL Code - Could not patch HudManager.UpdateScanNodes to hide subtext!");
                }
            }

            return codeList;
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

        private static bool ModifyChatAndHasPingCode(IEnumerable<CodeInstruction> instructions, MethodBase method, out TranspilerHelper.FoundInstruction[] pingCode)
        {
            pingCode = new TranspilerHelper.FoundInstruction[] { };

            if (Plugin.ChatFadeDelay.Value != (float)Plugin.ChatFadeDelay.DefaultValue || Plugin.ChatOpacity.Value != (float)Plugin.ChatOpacity.DefaultValue)
            {
                if (instructions.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                    i => i.IsLdarg(0),
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(HUDManager).GetField(nameof(HUDManager.Chat))),
                    i => i.opcode == OpCodes.Ldc_R4,
                    i => i.opcode == OpCodes.Ldc_R4,
                    i => i.opcode == OpCodes.Ldc_R4,
                    i => i.Calls(typeof(HUDManager).GetMethod(nameof(HUDManager.PingHUDElement)))
                }, out pingCode))
                {
                    return true;
                }
                else
                {
                    Plugin.MLS.LogError($"Unexpected IL Code - Could not patch HUDManager.{method.Name}!");
                }
            }

            return false;
        }

        [HarmonyPatch(typeof(HUDManager), "AddChatMessage")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> AddChatMessage_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            var codeList = instructions.ToList();

            if (ModifyChatAndHasPingCode(instructions, method, out var pingCode))
            {
                // Overwrite the delay and ending opacity
                Plugin.MLS.LogDebug("Patching HUDManager.AddChatMessage to use modified delay and opacity.");
                codeList[pingCode.Last().Index - 3].operand = Plugin.ChatFadeDelay.Value;
                codeList[pingCode.Last().Index - 1].operand = Plugin.ChatOpacity.Value;
            }

            return codeList;
        }

        [HarmonyPatch(typeof(HUDManager), "SubmitChat_performed")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SubmitChat_performed_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            var codeList = instructions.ToList();

            if (ModifyChatAndHasPingCode(instructions, method, out var pingCode))
            {
                // If we found the code, just rip out the entire call since further methods will call it again
                Plugin.MLS.LogDebug("Patching HUDManager.SubmitChat_performed to remove HUD ping call.");
                codeList.RemoveRange(pingCode[0].Index, pingCode.Length);
            }

            return codeList;
        }

        [HarmonyPatch(typeof(HUDManager), "OpenMenu_performed")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> OpenMenu_performed_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            var codeList = instructions.ToList();

            if (ModifyChatAndHasPingCode(instructions, method, out var pingCode))
            {
                // Overwrite the ending opacity
                Plugin.MLS.LogDebug("Patching HUDManager.OpenMenu_performed to use modified opacity.");
                codeList[pingCode.Last().Index - 1].operand = Plugin.ChatOpacity.Value;
            }

            return codeList;
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