using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
using TMPro;
using Unity.Profiling;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class HUDManagerPatch
    {
        private static readonly ProfilerMarker _pm_HUDAssignNewNodes = new ProfilerMarker(ProfilerCategory.Scripts, "GeneralImprovements.HUDManager.AssignNewNodes");
        private static readonly ProfilerMarker _pm_HUDAssignNodeToUI = new ProfilerMarker(ProfilerCategory.Scripts, "GeneralImprovements.HUDManager.AssignNodeToUIElement");
        private static readonly ProfilerMarker _pm_HUDUpdate = new ProfilerMarker(ProfilerCategory.Scripts, "GeneralImprovements.HUDManager.Update");

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

            if (Plugin.CenterSignalTranslatorText.Value)
            {
                __instance.signalTranslatorText.horizontalAlignment = HorizontalAlignmentOptions.Center;
                __instance.signalTranslatorText.enableAutoSizing = false;
                var rect = __instance.signalTranslatorText.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(-5, -230);
                rect.offsetMax = new Vector2(-5, -230);
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
            if (!Plugin.FixPersonalScanner.Value || !playerScript || !playerScript.gameplayCamera)
            {
                return true;
            }

            ProfilerHelper.BeginProfilingSafe(_pm_HUDAssignNewNodes);

            ___nodesOnScreen.Clear();
            ___scannedScrapNum = 0;

            // Get the planes of the active camera view
            var camPlanes = GeometryUtility.CalculateFrustumPlanes(playerScript.gameplayCamera);

            // Cast a giant sphere 100f around ourself to get scan nodes we collided with, ordered by distance
            var hitScanNodes = new Collider[100];
            int hits = Physics.OverlapSphereNonAlloc(playerScript.gameplayCamera.transform.position, 100f, hitScanNodes, 0x400000);

            var nearbyScanNodes = new List<KeyValuePair<float, ScanNodeProperties>>();
            for (int i = 0; i < hits; i++)
            {
                if (hitScanNodes[i].transform.TryGetComponent<ScanNodeProperties>(out var snp))
                {
                    float dist = Vector3.Distance(hitScanNodes[i].transform.position, playerScript.transform.position);
                    if (dist >= snp.minRange && dist <= snp.maxRange && GeometryUtility.TestPlanesAABB(camPlanes, new Bounds(snp.transform.position, Vector3.one)))
                    {
                        // In range and in camera view
                        nearbyScanNodes.Add(new KeyValuePair<float, ScanNodeProperties>(dist, snp));
                    }
                }
            }
            nearbyScanNodes = nearbyScanNodes.OrderBy(n => n.Key).ToList();

            // Now attempt to scan each of them, stopping when we fill the number of UI elements
            foreach (var scannable in nearbyScanNodes.Select(s => s.Value))
            {
                __instance.AttemptScanNode(scannable, 0, playerScript);
                if (___nodesOnScreen.Count >= __instance.scanElements.Length)
                {
                    break;
                }
            }

            ProfilerHelper.EndProfilingSafe(_pm_HUDAssignNewNodes);

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
                    i => i.LoadsConstant(0x08000100),
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
                        return Physics.Linecast(start, end, out var hitInfo, mask, interaction) && hitInfo.transform && hitInfo.transform.gameObject.layer == 8;
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
        private static IEnumerable<CodeInstruction> UpdateScanNodesTranspiler(IEnumerable<CodeInstruction> instructions)
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
            ProfilerHelper.BeginProfilingSafe(_pm_HUDAssignNodeToUI);

            // If we have scanned a player or a masked entity, make sure their health subtext is up to date
            MaskedPlayerEnemy masked = null;
            if (Plugin.ScanPlayers.Value && node.transform.parent && (node.transform.parent.TryGetComponent(out PlayerControllerB player) || node.transform.parent.TryGetComponent(out masked)))
            {
                int curHealth, maxHealth;
                if (player != null)
                {
                    curHealth = player.health;
                    maxHealth = 100; // For UI purposes, we don't care if the target is over 100.
                }
                else
                {
                    curHealth = masked.enemyHP;
                    maxHealth = MaskedPlayerEnemyPatch.MaxHealth;
                }
                node.subText = ObjectHelper.GetEntityHealthDescription(curHealth, maxHealth);
                node.nodeType = curHealth <= 0 && (masked == null || masked.enemyHP <= 0) ? 1 : 0; // Red or blue depending on live status
            }

            ProfilerHelper.EndProfilingSafe(_pm_HUDAssignNodeToUI);
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

        public static bool ModifyChatAndHasPingCode(IEnumerable<CodeInstruction> instructions, MethodBase method, out TranspilerHelper.FoundInstruction[] pingCode)
        {
            pingCode = new TranspilerHelper.FoundInstruction[] { };

            if (Plugin.ChatFadeDelay.Value != (float)Plugin.ChatFadeDelay.DefaultValue || Plugin.ChatOpacity.Value != (float)Plugin.ChatOpacity.DefaultValue)
            {
                if (instructions.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                    null,
                    null,
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
                    Plugin.MLS.LogError($"Unexpected IL Code - Could not patch {method.DeclaringType.Name}.{method.Name}!");
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
                Plugin.MLS.LogDebug($"Patching HUDManager.{method.Name} to remove HUD ping call.");
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

        [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.UseSignalTranslatorClientRpc))]
        [HarmonyPrefix]
        private static void UseSignalTranslatorClientRpc(ref string signalMessage)
        {
            if (Plugin.CenterSignalTranslatorText.Value)
            {
                signalMessage = signalMessage.Trim();
            }
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
            __result = __result && (!ShipBuildModeManager.Instance || !ShipBuildModeManager.Instance.InBuildMode);
        }

        [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.DisplayCreditsEarning))]
        [HarmonyPostfix]
        private static void DisplayCreditsEarning()
        {
            MonitorsHelper.UpdateCalculatedScrapMonitors();
        }

        [HarmonyPatch(typeof(HUDManager), nameof(Update))]
        [HarmonyPostfix]
        private static void Update()
        {
            ProfilerHelper.BeginProfilingSafe(_pm_HUDUpdate);

            if (_hpText != null && StartOfRound.Instance && StartOfRound.Instance.localPlayerController)
            {
                _hpText.text = $"{StartOfRound.Instance.localPlayerController.health} HP";
            }

            if (StormyWeatherPatch.Instance && StormyWeatherPatch.Instance.isActiveAndEnabled && Plugin.ShowLightningWarnings.Value)
            {
                // Toggle lightning overlays on item slots when needed
                if (_lightningOverlays.Count > 0 && HUDManager.Instance != null && StartOfRound.Instance.localPlayerController != null)
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

            ProfilerHelper.EndProfilingSafe(_pm_HUDUpdate);
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

                    // Convert UI number to either 1 or 0 decimal points in kg
                    codeList.InsertRange(found.Last().Index + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldc_R4, 2.205f),
                        new CodeInstruction(OpCodes.Div),
                        new CodeInstruction(OpCodes.Conv_R8),
                        new CodeInstruction(Plugin.DisplayRoundedKg.Value ? OpCodes.Ldc_I4_0 : OpCodes.Ldc_I4_1),
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

        [HarmonyPatch(typeof(HUDManager), nameof(HUDManager.FillEndGameStats))]
        [HarmonyPostfix]
        private static void FillEndGameStats(HUDManager __instance)
        {
            if (__instance.IsServer && Plugin.AllowQuotaRollover.Value && Plugin.QuotaRolloverSquadWipePenalty.Value > 0 && StartOfRound.Instance.allPlayersDead && TimeOfDay.Instance.quotaFulfilled > 0)
            {
                // Subtract any leftover funds by a specified percentage if everyone died
                var pct = Plugin.QuotaRolloverSquadWipePenalty.Value / 100f;
                var valToRemove = (int)Mathf.Round(TimeOfDay.Instance.quotaFulfilled * pct);

                Plugin.MLS.LogMessage($"Subtracting ${valToRemove} ({Plugin.QuotaRolloverSquadWipePenalty.Value}%) from surplus quota due to squad wipe.");
                TimeOfDay.Instance.quotaFulfilled -= valToRemove;

                // Send the new quota surplus over to clients
                NetworkHelper.Instance.SyncProfitQuotaClientRpc(TimeOfDay.Instance.quotaFulfilled);
            }
        }
    }
}