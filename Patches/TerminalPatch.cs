using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace GeneralImprovements.Patches
{
    internal static class TerminalPatch
    {
        private static List<string> _commandHistory = new List<string>();
        private static int _historyCount;
        private static int _curHistoryIndex = 0;

        [HarmonyPatch(typeof(Terminal), nameof(Start))]
        [HarmonyPostfix]
        private static void Start()
        {
            _historyCount = Math.Clamp(Plugin.TerminalHistoryItemCount.Value, 0, 100);
        }

        [HarmonyPatch(typeof(Terminal), nameof(BeginUsingTerminal))]
        [HarmonyPostfix]
        private static void BeginUsingTerminal(Terminal __instance)
        {
            __instance.terminalUIScreen.gameObject.SetActive(true);
            __instance.screenText.Select();
            __instance.screenText.ActivateInputField();
        }

        [HarmonyPatch(typeof(Terminal), nameof(OnSubmit))]
        [HarmonyPrefix]
        private static void OnSubmit(Terminal __instance)
        {
            if (_historyCount <= 0 || __instance.textAdded <= 0)
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
                    _commandHistory.RemoveAt(_commandHistory.Count - 1);
                }
            }

            _curHistoryIndex = _commandHistory.Count;
        }

        [HarmonyPatch(typeof(Terminal), nameof(TextPostProcess))]
        [HarmonyPostfix]
        private static void TextPostProcess(string modifiedDisplayText, ref string __result)
        {
            __result = __result.Replace("\nn\n", "\n\n\n");
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
                else if ((leftPressed || rightPressed) && __instance.displayingPersistentImage?.name == "mapTexture")
                {
                    // Cycle through cameras
                    int nextIndex = StartOfRound.Instance.mapScreen.targetTransformIndex + (leftPressed ? -1 : 1);
                    if (nextIndex <= 0) nextIndex = StartOfRound.Instance.mapScreen.radarTargets.Count - 1;
                    else if (nextIndex >= StartOfRound.Instance.mapScreen.radarTargets.Count) nextIndex = 0;

                    StartOfRound.Instance.mapScreen.SwitchRadarTargetAndSync(nextIndex);
                    __instance.LoadNewNode(__instance.terminalNodes.specialNodes[20]);
                }
            }
        }
    }
}