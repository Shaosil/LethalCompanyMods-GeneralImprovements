using GeneralImprovements.Utilities;
using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class ManualCameraRendererPatch
    {
        [HarmonyPatch(typeof(ManualCameraRenderer), nameof(updateMapTarget), MethodType.Enumerator)]
        [HarmonyPostfix]
        private static void updateMapTarget(int ___setRadarTargetIndex, bool ___calledFromRPC, bool __result)
        {
            // Wait until the enumerator is complete (result == false)
            var instance = StartOfRound.Instance.mapScreen;
            bool inTerminal = GameNetworkManager.Instance.localPlayerController.inTerminalMenu;
            bool curNodeIsSwitchCam = TerminalPatch.Instance.currentNode?.name == "SwitchedCam";
            bool validTarget = instance.radarTargets != null && instance.radarTargets[___setRadarTargetIndex] != null;

            if (!__result && !___calledFromRPC && inTerminal && curNodeIsSwitchCam && validTarget)
            {
                Plugin.MLS.LogInfo("Updating terminal node text to player name");
                string targetName = instance.radarTargets[___setRadarTargetIndex].name;
                TerminalPatch.Instance.screenText.text += $"Switched radar to {targetName}.\n\n";
                TerminalPatch.Instance.currentText = TerminalPatch.Instance.screenText.text;
                TerminalPatch.Instance.textAdded = 0;
                TerminalPatch.Instance.screenText.ActivateInputField();
                TerminalPatch.Instance.screenText.Select();
                TerminalPatch.Instance.scrollBarVertical.value = 0;
            }
        }

        [HarmonyPatch(typeof(ManualCameraRenderer), nameof(SwitchScreenOn))]
        [HarmonyPrefix]
        private static bool SwitchScreenOn(bool on, ManualCameraRenderer __instance, ref bool ___isScreenOn)
        {
            if (Plugin.SyncExtraMonitorsPower.Value)
            {
                MonitorsHelper.ToggleExtraMonitorPower(on);
            }

            // Manually handle this if we are using our own monitors
            if (Plugin.UseBetterMonitors.Value)
            {
                MonitorsHelper.UpdateMapMaterial(on ? __instance.onScreenMat : __instance.offScreenMat);
                ___isScreenOn = on;
                __instance.currentCameraDisabled = !on;
                if (on)
                {
                    __instance.mapCameraAnimator.SetTrigger("Transition");
                }
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(ManualCameraRenderer), nameof(MeetsCameraEnabledConditions))]
        [HarmonyPostfix]
        private static void MeetsCameraEnabledConditions(ManualCameraRenderer __instance, ref bool __result)
        {
            // View monitor will break in some cases, perhaps related to culling, if the internal ship security cam is manually rendering
            if (!__result && __instance.mesh != null && !__instance.mesh.isVisible)
            {
                __result = true;
            }
        }
    }
}