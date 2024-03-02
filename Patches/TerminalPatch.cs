using GameNetcodeStuff;
using GeneralImprovements.OtherMods;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace GeneralImprovements.Patches
{
    internal static class TerminalPatch
    {
        private static int _currentCredits = 0;
        private static List<string> _commandHistory = new List<string>();
        private static int _historyCount;
        private static int _curHistoryIndex = 0;

        private static Terminal _instance;
        public static Terminal Instance => _instance ?? (_instance = UnityEngine.Object.FindObjectOfType<Terminal>());

        [HarmonyPatch(typeof(Terminal), nameof(Start))]
        [HarmonyPrefix]
        [HarmonyAfter(TwoRadarCamsHelper.GUID)]
        private static void Start(Terminal __instance)
        {
            _instance = __instance;
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

            TwoRadarCamsHelper.TerminalStarted(__instance);
        }

        [HarmonyPatch(typeof(Terminal), nameof(Start))]
        [HarmonyPostfix]
        private static void Start()
        {
            MonitorsHelper.UpdateSalesMonitors();
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

        [HarmonyPatch(typeof(Terminal), nameof(OnSubmit))]
        [HarmonyPrefix]
        [HarmonyBefore("AdvancedCompany")]
        private static void OnSubmit(Terminal __instance)
        {
            if (_historyCount <= 0 || __instance.textAdded < 3)
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

        [HarmonyPatch(typeof(Terminal), "LoadNewNodeIfAffordable")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PatchDropshipItemLimit(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = new List<CodeInstruction>(instructions);
            for (int i = 3; i < codeList.Count; i++)
            {
                // If the current instruction is to compare the numberOfItemsInDropship to 12, update it to our defined number
                if (codeList[i].Is(OpCodes.Ldc_R4, 12f) && codeList[i - 3].opcode == OpCodes.Ldfld && (codeList[i - 3].operand as FieldInfo)?.Name == nameof(Terminal.numberOfItemsInDropship))
                {
                    Plugin.MLS.LogDebug($"Updating dropship item limit to {Plugin.DropShipItemLimit.Value}.");
                    codeList[i].operand = (float)Plugin.DropShipItemLimit.Value;
                    break;
                }
            }

            return codeList.AsEnumerable();
        }

        [HarmonyPatch(typeof(Terminal), "TextPostProcess")]
        [HarmonyPrefix]
        private static void TextPostProcess_Pre(ref string modifiedDisplayText, TerminalNode node)
        {
            // Improve the scanning
            if (modifiedDisplayText.Contains("[scanForItems]"))
            {
                var scannedItems = GrabbableObjectsPatch.GetOutsideScrap(!Plugin.ScanCommandUsesExactAmount.Value);
                string desc = Plugin.ScanCommandUsesExactAmount.Value ? "exact" : "approximate";
                modifiedDisplayText = modifiedDisplayText.Replace("[scanForItems]", $"There are {scannedItems.Key} objects outside the ship, totalling at an {desc} value of ${scannedItems.Value}.");
            }
        }

        [HarmonyPatch(typeof(Terminal), "TextPostProcess")]
        [HarmonyPostfix]
        private static void TextPostProcess_Post(string modifiedDisplayText, ref string __result)
        {
            __result = __result.Replace("\nn\n", "\n\n\n");
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

                if ((upPressed || downPressed) && _commandHistory.Any())
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
                    ManualCameraRenderer mapRenderer = TwoRadarCamsHelper.MapRenderer ?? StartOfRound.Instance.mapScreen;
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

        public static void SetStartingMoneyPerPlayer()
        {
            // Grab initial credits value if this is the server
            if (Instance.IsServer && Plugin.StartingMoneyPerPlayerVal >= 0)
            {
                // Set initial credits value if this is a new game
                if (StartOfRound.Instance.gameStats.daysSpent == 0)
                {
                    _currentCredits = Math.Clamp(Plugin.StartingMoneyPerPlayerVal * (StartOfRound.Instance.connectedPlayersAmount + 1), Plugin.MinimumStartingMoneyVal, int.MaxValue);
                    Plugin.MLS.LogInfo($"Setting starting money to {_currentCredits} ({Plugin.StartingMoneyPerPlayerVal} per player x {StartOfRound.Instance.connectedPlayersAmount + 1} current players), with a minimium of {Plugin.MinimumStartingMoneyVal}.");
                    TimeOfDay.Instance.quotaVariables.startingCredits = _currentCredits;
                    Instance.groupCredits = Math.Clamp(_currentCredits, 0, _currentCredits);
                    ES3.Save("GroupCredits", _currentCredits, GameNetworkManager.Instance.currentSaveFileName);
                    Instance.SyncGroupCreditsServerRpc(Instance.groupCredits, Instance.numberOfItemsInDropship);
                }
                else
                {
                    _currentCredits = ES3.Load("GroupCredits", GameNetworkManager.Instance.currentSaveFileName, Plugin.StartingMoneyPerPlayerVal);
                }
            }
        }

        public static void AdjustGroupCredits(bool adding)
        {
            if (Instance.IsServer && Plugin.StartingMoneyPerPlayerVal >= 0 && StartOfRound.Instance.inShipPhase && StartOfRound.Instance.gameStats.daysSpent == 0)
            {
                // Do nothing if the number of current players does not match the num required to go past the minimum starting credits
                int actualNumPlayers = StartOfRound.Instance.connectedPlayersAmount + (adding ? 2 : 1);
                int minPlayersToAdjust = (int)MathF.Floor((float)Plugin.MinimumStartingMoneyVal / Plugin.StartingMoneyPerPlayerVal) + (adding ? 1 : 0);
                if (actualNumPlayers < minPlayersToAdjust)
                {
                    return;
                }

                _currentCredits += Plugin.StartingMoneyPerPlayerVal * (adding ? 1 : -1);
                Plugin.MLS.LogInfo($"{(adding ? "Adding" : "Subtracting")} {Plugin.StartingMoneyPerPlayerVal} {(adding ? "to" : "from")} group credits.");
                Instance.groupCredits = Math.Clamp(_currentCredits, 0, _currentCredits);

                // If this is a disconnect, credits will not be synced automatically, so do that here
                if (!adding)
                {
                    Instance.SyncGroupCreditsServerRpc(Instance.groupCredits, Instance.numberOfItemsInDropship);
                }
            }
        }
    }
}