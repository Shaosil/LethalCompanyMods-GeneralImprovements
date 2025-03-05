﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;
using static GeneralImprovements.Enums;

namespace GeneralImprovements.Patches
{
    internal static class TerminalPatch
    {
        private static int _currentCredits = 0;
        private static List<string> _commandHistory = new List<string>();
        private static int _historyCount;
        private static int _curHistoryIndex = 0;

        public static Terminal _instance; // Should be erased each time StartOfRound begins, since it usually starts before Terminal
        public static Terminal Instance => _instance ?? (_instance = UnityEngine.Object.FindObjectOfType<Terminal>());

        [HarmonyPatch(typeof(Terminal), "Start")]
        [HarmonyPrefix]
        [HarmonyAfter(OtherModHelper.TwoRadarCamsGUID)]
        private static void StartPre(Terminal __instance)
        {
            _historyCount = Math.Clamp(Plugin.TerminalHistoryItemCount.Value, 0, 100);

            if (!StartOfRound.Instance.isChallengeFile)
            {
                SetStartingMoneyPerPlayer();
            }

            // Clear out the plain "Switched to player" text from the switch node
            var switchNodes = __instance.terminalNodes?.specialNodes?.Where(n => n.displayText.Contains("Switched radar to player.") || n.terminalEvent == "switchCamera");
            if (switchNodes != null)
            {
                foreach (var switchNode in switchNodes)
                {
                    switchNode.displayText = string.Empty;
                }
            }

            // Update the "too many items" node display text
            var tooManyItemsNode = __instance.terminalNodes.specialNodes[4];
            tooManyItemsNode.displayText = tooManyItemsNode.displayText.Replace("12 items", $"{Plugin.DropShipItemLimit.Value} items");

            if (OtherModHelper.TwoRadarCamsActive)
            {
                OtherModHelper.TwoRadarCamsMapRenderer = __instance.GetComponent<ManualCameraRenderer>();
            }
        }

        [HarmonyPatch(typeof(Terminal), "Start")]
        [HarmonyPostfix]
        private static void StartPost(Terminal __instance)
        {
            MonitorsHelper.UpdateSalesMonitors();

            if (Plugin.FitCreditsInBackgroundImage.Value)
            {
                // If the last sibling has an Image component, resize it, change the text parent to that, stretch to its bounds, and enable autosizing
                int siblingIndex = (__instance.topRightText?.rectTransform?.GetSiblingIndex() ?? 0) - 1;
                if (siblingIndex >= 0 && __instance.topRightText.rectTransform.parent.GetChild(siblingIndex).GetComponent<Image>() is Image background)
                {
                    background.rectTransform.anchoredPosition = new Vector2(-170, background.rectTransform.anchoredPosition.y);
                    background.rectTransform.sizeDelta = new Vector2(95, background.rectTransform.sizeDelta.y);
                    __instance.topRightText.rectTransform.SetParent(background.rectTransform);

                    __instance.topRightText.rectTransform.anchoredPosition = Vector2.zero;
                    __instance.topRightText.rectTransform.anchorMin = Vector2.zero;
                    __instance.topRightText.rectTransform.anchorMax = Vector2.one;
                    __instance.topRightText.rectTransform.sizeDelta = Vector2.zero;
                    __instance.topRightText.margin = new Vector4(0, 0, 5, 0);
                    __instance.topRightText.enableAutoSizing = true;
                }
            }
        }

        [HarmonyPatch(typeof(Terminal), nameof(BeginUsingTerminal))]
        [HarmonyPostfix]
        private static void BeginUsingTerminal(Terminal __instance)
        {
            __instance.terminalUIScreen.gameObject.SetActive(true);
            __instance.screenText.Select();
            __instance.screenText.ActivateInputField();
            _curHistoryIndex = _commandHistory.Count;

            if (Plugin.LockCameraAtTerminal.Value && StartOfRound.Instance.localPlayerController != null)
            {
                StartOfRound.Instance.localPlayerController.disableLookInput = true;
            }
        }

        [HarmonyPatch(typeof(Terminal), nameof(QuitTerminal))]
        [HarmonyPrefix]
        private static void QuitTerminal()
        {
            if (StartOfRound.Instance.localPlayerController != null)
            {
                StartOfRound.Instance.localPlayerController.disableLookInput = false;
            }
        }

        [HarmonyPatch(typeof(Terminal), nameof(BeginUsingTerminal))]
        [HarmonyPatch(typeof(Terminal), nameof(QuitTerminal))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Terminal_ChatPings_Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            var codeList = instructions.ToList();

            if (HUDManagerPatch.ModifyChatAndHasPingCode(instructions, method, out var pingCode))
            {
                // If we found the code, just rip out the entire call
                Plugin.MLS.LogDebug($"Patching Terminal.{method.Name} to remove HUD ping call.");
                codeList.RemoveRange(pingCode[0].Index, pingCode.Length);
            }

            return codeList;
        }

        [HarmonyPatch(typeof(Terminal), nameof(OnSubmit))]
        [HarmonyPrefix]
        [HarmonyBefore("AdvancedCompany")]
        private static void OnSubmit(Terminal __instance)
        {
            if (_historyCount <= 0 || __instance.textAdded < 2 || __instance.screenText.text.Length < __instance.textAdded)
            {
                return;
            }

            // If this command isn't the same as the last one, queue it in the history and trim if needed
            string command = __instance.screenText.text.Substring(__instance.screenText.text.Length - __instance.textAdded).Trim();

            if (!_commandHistory.Any() || _commandHistory.Last().ToUpper() != command.ToUpper())
            {
                _commandHistory.Add(command);

                if (_commandHistory.Count > _historyCount)
                {
                    _commandHistory.RemoveAt(0);
                }
            }

            _curHistoryIndex = _commandHistory.Count;
        }

        [HarmonyPatch(typeof(Terminal), "ParsePlayerSentence")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ParsePlayerSentence_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // If the current instruction is to compare the numberOfItemsInDropship to 12, update it to our defined number
            if (instructions.TryFindInstructions(new Func<CodeInstruction, bool>[]
            {
                i => i.Calls(typeof(int).GetMethod(nameof(int.Parse), new[] { typeof(string) })),
                i => i.LoadsConstant(0),
                i => i.LoadsConstant(10),
                i => i.Calls(typeof(Mathf).GetMethod(nameof(Mathf.Clamp), new[] { typeof(int), typeof(int), typeof(int) })),
                i => i.StoresField(typeof(Terminal).GetField(nameof(Terminal.playerDefinedAmount)))
            }, out var found))
            {
                Plugin.MLS.LogDebug($"Patching Terminal.ParsePlayerSentence to set dropship item limit to {Plugin.DropShipItemLimit.Value}.");
                found[2].Instruction.operand = Plugin.DropShipItemLimit.Value;
            }

            return instructions;
        }

        [HarmonyPatch(typeof(Terminal), "LoadNewNodeIfAffordable")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> LoadNewNodeIfAffordable_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // If the current instruction is to compare the numberOfItemsInDropship to 12, update it to our defined number
            if (instructions.TryFindInstructions(new Func<CodeInstruction, bool>[]
            {
                i => i.LoadsConstant(12),
                i => i.Branches(out _),
                i => i.IsLdloc(),
                i => i.IsLdarg(0),
                i => i.LoadsField(typeof(Terminal).GetField(nameof(Terminal.numberOfItemsInDropship))),
                i => i.opcode == OpCodes.Conv_R4,
                i => i.opcode == OpCodes.Add,
                i => i.LoadsConstant(12f)
            }, out var found))
            {
                Plugin.MLS.LogDebug($"Patching Terminal.LoadNewNodeIfAffordable to set dropship item limit to {Plugin.DropShipItemLimit.Value}.");
                found.First().Instruction.operand = Plugin.DropShipItemLimit.Value;
                found.Last().Instruction.operand = (float)Plugin.DropShipItemLimit.Value;
            }

            return instructions;
        }

        [HarmonyPatch(typeof(Terminal), nameof(TextPostProcess))]
        [HarmonyPrefix]
        private static void TextPostProcess(ref string modifiedDisplayText, TerminalNode node)
        {
            // Improve the scanning
            if (modifiedDisplayText.Contains("[scanForItems]"))
            {
                var scannedItems = GrabbableObjectsPatch.GetScrapAmountAndValue(!Plugin.ScanCommandUsesExactAmount.Value);
                string desc = Plugin.ScanCommandUsesExactAmount.Value ? "exact" : "approximate";
                modifiedDisplayText = modifiedDisplayText.Replace("[scanForItems]", $"There are {scannedItems.Key} objects outside the ship, totalling at an {desc} value of ${scannedItems.Value}.");
            }

            // Show extra moons and prices if specified
            if (node.name == "MoonsCatalogue" && (Plugin.ShowMoonPricesInTerminal.Value || Plugin.ShowHiddenMoonsInCatalog.Value != Enums.eShowHiddenMoons.Never))
            {
                // Get prices by finding "route" node and matching on names. A bit risky if things change, but better than hardcoding
                var routeNode = Instance.terminalNodes.allKeywords.FirstOrDefault(k => k.word == "route");
                if (routeNode != null)
                {
                    // Get all moons we can actually route to, making sure to filter out hidden moons where necessary
                    var allmoons = StartOfRound.Instance.levels.Where(l =>
                        routeNode.compatibleNouns.Any(n => l.PlanetName.ToLower().Contains(n.noun.word.ToLower())) // Find the name in routeable moons
                        && (Instance.moonsCatalogueList.Contains(l) || Plugin.ShowHiddenMoonsInCatalog.Value == Enums.eShowHiddenMoons.Always // And it's either shown in vanilla, or we always show hidden...
                            || (Plugin.ShowHiddenMoonsInCatalog.Value == Enums.eShowHiddenMoons.AfterDiscovery && StartOfRoundPatch.FlownToHiddenMoons.Contains(l.PlanetName)))) // ... or we've discovered it
                        .ToList();

                    // If extra moons exist that are not in our usual catalogue, add them to the display text as needed
                    if (allmoons.Count > Instance.moonsCatalogueList.Length)
                    {
                        foreach (var extraMoon in allmoons.Where(m => !Instance.moonsCatalogueList.Contains(m)))
                        {
                            modifiedDisplayText += $"* {Regex.Match(extraMoon.PlanetName, ".* (.+)").Groups[1].Value}\n";
                        }
                        modifiedDisplayText += '\n';
                    }

                    // Now handle weather and optional price manually
                    for (int i = 0; i < allmoons.Count; i++)
                    {
                        string weather = OtherModHelper.WeatherRegistryActive ? $"({OtherModHelper.GetWeatherRegistryWeatherName(allmoons[i])})" : allmoons[i].currentWeather == LevelWeatherType.None ? string.Empty : $"({allmoons[i].currentWeather}) ";
                        var matchingMoonNode = routeNode.compatibleNouns.FirstOrDefault(n => allmoons[i].PlanetName.Contains(n.noun.word, StringComparison.OrdinalIgnoreCase));
                        if (matchingMoonNode != null)
                        {
                            string cost = !Plugin.ShowMoonPricesInTerminal.Value || matchingMoonNode.result.itemCost <= 0 ? string.Empty : $"- ${matchingMoonNode.result.itemCost}";
                            modifiedDisplayText = Regex.Replace(modifiedDisplayText, @$"({matchingMoonNode.noun.word}).*", $"$1 {weather}{cost}", RegexOptions.IgnoreCase);
                        }
                        else
                        {
                            Plugin.MLS.LogError($"Could not find moon node for {allmoons[i].PlanetName}! Unable to display its info.");
                        }
                    }
                }
                else
                {
                    Plugin.MLS.LogError("Could not find terminal route node! Unable to display custom moon info.");
                }
            }
        }

        [HarmonyPatch(typeof(Terminal), nameof(TextPostProcess))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> TextPostProcess_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            // If they don't want to show blanks, remove the entire check that adds \n's. Otherwise, just fix the \nn issue.
            if (!Plugin.ShowBlanksDuringViewMonitor.Value)
            {
                if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(Terminal).GetField(nameof(Terminal.displayingPersistentImage))),
                    null,
                    i => i.Branches(out _),
                    i => i.opcode == OpCodes.Ldstr && i.operand.ToString().Contains("\n\n\n\n\n\n\n"),
                    i => i.IsLdarg(),
                    i => i.Calls(typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(string), typeof(string) })),
                    i => i.IsStarg()
                }, out var found))
                {
                    Plugin.MLS.LogDebug("Patching Terminal.TextPostProcess to remove blank screens during View Monitor.");
                    codeList.RemoveRange(found.First().Index, found.Length);
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL code found - Could not patch Terminal.TextPostProcess to remove blank screens!");
                }
            }
            else if (codeList.TryFindInstruction(i => i.opcode == OpCodes.Ldstr && i.operand.ToString().Contains("\n\n\n\n\n\n\n"), out var found))
            {
                Plugin.MLS.LogDebug("Patching Terminal.TextPostProcess to remove \\nn typo.");
                found.Instruction.operand = new string('\n', 20);
            }

            return codeList;
        }

        [HarmonyPatch(typeof(Terminal), nameof(SetItemSales))]
        [HarmonyPostfix]
        private static void SetItemSales(Terminal __instance)
        {
            MonitorsHelper.UpdateSalesMonitors();
        }

        [HarmonyPatch(typeof(Terminal), nameof(InitializeItemSalesPercentages))]
        [HarmonyPrefix]
        private static bool InitializeItemSalesPercentages(Terminal __instance)
        {
            // Do nothing if we've already initialized these
            return __instance.itemSalesPercentages == null || __instance.itemSalesPercentages.Length == 0;
        }

        [HarmonyPatch(typeof(Terminal), nameof(Update))]
        [HarmonyPostfix]
        private static void Update(Terminal __instance)
        {
            if (GameNetworkManager.Instance?.localPlayerController?.inTerminalMenu ?? false)
            {
                bool upPressed = Keyboard.current[Key.UpArrow].wasPressedThisFrame;
                bool rightPressed = Keyboard.current[Key.RightArrow].wasPressedThisFrame;
                bool downPressed = Keyboard.current[Key.DownArrow].wasPressedThisFrame;
                bool leftPressed = Keyboard.current[Key.LeftArrow].wasPressedThisFrame;

                if ((upPressed || downPressed) && _commandHistory.Count > 0)
                {
                    // Navigate through history
                    _curHistoryIndex += (upPressed ? -1 : 1);
                    if (_curHistoryIndex < 0) _curHistoryIndex = 0;
                    else if (_curHistoryIndex >= _commandHistory.Count) _curHistoryIndex = _commandHistory.Count - 1;

                    string curCommand = _commandHistory.ElementAt(_curHistoryIndex);

                    __instance.screenText.text = $"{__instance.screenText.text.Substring(0, __instance.screenText.text.Length - __instance.textAdded)}{curCommand}";
                    __instance.screenText.caretPosition = __instance.screenText.text.Length;
                    __instance.textAdded = curCommand.Length;
                }
                else if (Plugin.TerminalFastCamSwitch.Value && (leftPressed || rightPressed))
                {
                    // Cycle through cameras
                    ManualCameraRenderer mapRenderer = OtherModHelper.TwoRadarCamsMapRenderer ?? StartOfRound.Instance.mapScreen;
                    int originalIndex = mapRenderer.targetTransformIndex;
                    int nextIndex = originalIndex;
                    bool isInactivePlayer;
                    do
                    {
                        // Find the next target, if there is one
                        nextIndex += (leftPressed ? -1 : 1);

                        if (nextIndex < 0) nextIndex = mapRenderer.radarTargets.Count - 1;
                        else if (nextIndex >= mapRenderer.radarTargets.Count) nextIndex = 0;

                        var player = mapRenderer.radarTargets[nextIndex].transform.gameObject.GetComponent<PlayerControllerB>();
                        isInactivePlayer = player != null && (!player.isPlayerControlled && !player.isPlayerDead && player.redirectToEnemy == null);

                    } while (isInactivePlayer && nextIndex != originalIndex);

                    if (nextIndex != originalIndex)
                    {
                        mapRenderer.SwitchRadarTargetAndSync(nextIndex);
                        __instance.LoadNewNode(__instance.terminalNodes.specialNodes[20]);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.SyncGroupCreditsClientRpc))]
        [HarmonyPostfix]
        private static void SyncGroupCreditsClientRpc(int newGroupCredits)
        {
            // Adjust our current credits tracker by the difference to ensure it is always accurate
            _currentCredits += (newGroupCredits - Math.Max(_currentCredits, 0));
        }

        [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.BuyShipUnlockableClientRpc))]
        [HarmonyPostfix]
        private static void BuyShipUnlockableClientRpc(int newGroupCreditsAmount)
        {
            // This is a StartOfRound patch but belongs here with the rest of the credits functionality
            _currentCredits += (newGroupCreditsAmount - Math.Max(_currentCredits, 0));
        }

        public static void SetStartingMoneyPerPlayer()
        {
            // Grab initial credits value if this is the server and we specified a value
            if (Instance.IsServer && Plugin.StartingMoneyFunction.Value != eStartingMoneyFunction.Disabled)
            {
                // Calculate default money based on settings
                _currentCredits = Plugin.StartingMoneyVal;
                if (Plugin.StartingMoneyFunction.Value != eStartingMoneyFunction.Total) _currentCredits *= (StartOfRound.Instance.connectedPlayersAmount + 1);
                if (Plugin.StartingMoneyFunction.Value == eStartingMoneyFunction.PerPlayerWithMinimum) _currentCredits = Math.Max(_currentCredits, Plugin.MinimumStartingMoneyVal);

                if (StartOfRound.Instance.gameStats.daysSpent == 0)
                {
                    // Set initial credits value if this is a new game
                    Plugin.MLS.LogInfo($"Setting starting money to {_currentCredits} based on current settings. ({nameof(Plugin.StartingMoney)}: {Plugin.StartingMoneyVal}. {nameof(Plugin.StartingMoneyFunction)}: {Plugin.StartingMoneyFunction.Value}. {nameof(Plugin.MinimumStartingMoney)}: {Plugin.MinimumStartingMoneyVal})");
                    TimeOfDay.Instance.quotaVariables.startingCredits = _currentCredits;
                    ES3.Save("GroupCredits", _currentCredits, GameNetworkManager.Instance.currentSaveFileName);
                }
                else
                {
                    // Otherwise just load it from the save file, making sure to use our calculated amount as the backup default
                    _currentCredits = ES3.Load("GroupCredits", GameNetworkManager.Instance.currentSaveFileName, _currentCredits);
                }

                Instance.groupCredits = Math.Clamp(_currentCredits, 0, _currentCredits);
                Instance.SyncGroupCreditsServerRpc(Instance.groupCredits, Instance.numberOfItemsInDropship);
            }
        }

        public static void AdjustGroupCredits(bool adding)
        {
            if (Instance.IsServer && Plugin.StartingMoneyVal > 0 && StartOfRound.Instance.inShipPhase && StartOfRound.Instance.gameStats.daysSpent == 0)
            {
                // If there is a minimum, only adjust if the number of current players matches the required amount
                if (Plugin.StartingMoneyFunction.Value == eStartingMoneyFunction.PerPlayerWithMinimum)
                {
                    int actualNumPlayers = StartOfRound.Instance.connectedPlayersAmount + (adding ? 2 : 1);
                    int minPlayersToAdjust = (int)MathF.Floor((float)Plugin.MinimumStartingMoneyVal / Plugin.StartingMoneyVal) + (adding ? 1 : 0);
                    if (actualNumPlayers < minPlayersToAdjust)
                    {
                        Plugin.MLS.LogInfo($"Player {(adding ? "joined" : "left")} but current number of players ({actualNumPlayers}) is less than minimum required players ({minPlayersToAdjust}) and no credits were adjusted. Minimum starting money: {Plugin.MinimumStartingMoneyVal}");
                        return;
                    }
                }
                else if (Plugin.StartingMoneyFunction.Value != eStartingMoneyFunction.PerPlayer)
                {
                    return;
                }

                _currentCredits += Plugin.StartingMoneyVal * (adding ? 1 : -1);
                Plugin.MLS.LogInfo($"{(adding ? "Adding" : "Subtracting")} {Plugin.StartingMoneyVal} {(adding ? "to" : "from")} group credits (tracked: ${_currentCredits})");
                Instance.groupCredits = Math.Max(_currentCredits, 0);

                // If this is a disconnect, credits will not be synced automatically, so do that here
                if (!adding)
                {
                    Instance.SyncGroupCreditsServerRpc(Instance.groupCredits, Instance.numberOfItemsInDropship);
                }
            }
        }
    }
}