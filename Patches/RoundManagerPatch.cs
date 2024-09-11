using System.Collections.Generic;
using System.Linq;
using GeneralImprovements.Utilities;
using HarmonyLib;
using UnityEngine;
using static GeneralImprovements.Enums;

namespace GeneralImprovements.Patches
{
    internal static class RoundManagerPatch
    {
        private static bool _gotShipNode = false;
        public static ScanNodeProperties CurShipNode { get; private set; }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.CollectNewScrapForThisRound))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> CollectNewScrapForThisRound_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.ShipMonitorAssignments.Any(a => a.Value == eMonitorNames.DailyProfit))
            {
                if (codeList.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(RoundManager).GetField(nameof(RoundManager.scrapCollectedThisRound))),
                    i => i.IsLdarg(1),
                    i => i.Calls(typeof(List<GrabbableObject>).GetMethod("Add"))
                }, out var addScrapCode))
                {
                    Plugin.MLS.LogDebug("Patching RoundManager.CollectNewScrapForThisRound to update certain monitor(s).");
                    codeList.Insert(addScrapCode.Last().Index + 1, Transpilers.EmitDelegate<System.Action>(() =>
                    {
                        MonitorsHelper.UpdateDailyProfitMonitors();
                        MonitorsHelper.UpdateCalculatedScrapMonitors();
                    }));
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL Code - Could not patch RoundManager.CollectNewScrapForThisRound to update certain monitor(s)!");
                }
            }

            return codeList;
        }

        [HarmonyPatch(typeof(RoundManager), nameof(SyncScrapValuesClientRpc))]
        [HarmonyPostfix]
        private static void SyncScrapValuesClientRpc()
        {
            // Update and override the total scrap in level
            var outsideScrap = GrabbableObjectsPatch.GetScrapAmountAndValue(false);
            RoundManager.Instance.totalScrapValueInLevel = outsideScrap.Value;
        }

        [HarmonyPatch(typeof(RoundManager), nameof(FinishGeneratingNewLevelClientRpc))]
        [HarmonyPostfix]
        private static void FinishGeneratingNewLevelClientRpc(RoundManager __instance)
        {
            if (!_gotShipNode)
            {
                CurShipNode = Object.FindObjectsOfType<ScanNodeProperties>().FirstOrDefault(s => s.headerText == "Ship");
                if (CurShipNode != null)
                {
                    // Disable the node for now (it will enable again once the ship is landed)
                    Plugin.MLS.LogInfo("Disabling ship scan node until landed.");
                    CurShipNode.gameObject.SetActive(false);

                    _gotShipNode = true;
                }
            }

            // If mimics are active and we want scan nodes on fire exits, create them here
            if (Plugin.ShowDoorsOnScanner.Value && OtherModHelper.MimicsActive)
            {
                var mimics = Object.FindObjectsOfType<InteractTrigger>().Where(g => g.transform.parent?.name.StartsWith("MimicDoor") ?? false).ToList();
                foreach (var mimic in mimics)
                {
                    // Manually move the scan node's position, or it will be in a noticeably different spot than real exits
                    var mimicScanNode = ObjectHelper.CreateScanNodeOnObject(mimic.transform.parent.gameObject, 0, 1, 20, "Fire Exit?");
                    mimicScanNode.transform.localPosition += new Vector3(0, 1.5f, 0);
                }
            }

            // Refresh the current audio presets
            AudioReverbTriggerPatch.CurrentAudioReverbPresets = Object.FindAnyObjectByType<AudioReverbPresets>();

            // Update round specific monitors
            MonitorsHelper.UpdateDailyProfitMonitors();
            MonitorsHelper.UpdateCalculatedScrapMonitors(true);
        }

        [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SpawnScrapInLevel))]
        [HarmonyPrefix]
        private static void SpawnScrapInLevel(RoundManager __instance)
        {
            // Multiply generated scrap value by defined weather multiplier
            var modifiedScrapValue = Plugin.SanitizedScrapValueWeatherMultipliers
                .FirstOrDefault(s => s.Key.Equals(__instance.currentLevel.currentWeather.ToString(), System.StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(modifiedScrapValue.Key))
            {
                Plugin.MLS.LogInfo($"Applying defined scrap value weather multiplier for {__instance.currentLevel.currentWeather} ({modifiedScrapValue.Value}x).");
                __instance.scrapValueMultiplier = modifiedScrapValue.Value;
            }

            // Multiply generated scrap amount by defined weather multiplier
            var modifiedScrapAmount = Plugin.SanitizedScrapAmountWeatherMultipliers
                .FirstOrDefault(s => s.Key.Equals(__instance.currentLevel.currentWeather.ToString(), System.StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(modifiedScrapAmount.Key))
            {
                Plugin.MLS.LogInfo($"Applying defined scrap amount weather multiplier for {__instance.currentLevel.currentWeather} ({modifiedScrapAmount.Value}x).");
                __instance.scrapAmountMultiplier = modifiedScrapAmount.Value;
            }
        }

        [HarmonyPatch(typeof(RoundManager), nameof(DespawnPropsAtEndOfRound))]
        [HarmonyPostfix]
        private static void DespawnPropsAtEndOfRound()
        {
            MonitorsHelper.UpdateTimeMonitors();
        }

        public static void EnableAndAttachShipScanNode()
        {
            if (CurShipNode != null)
            {
                // Enable the ship node again
                Plugin.MLS.LogInfo("Enabling ship scan node again and parenting it to the terminal.");
                CurShipNode.gameObject.SetActive(true);
                CurShipNode.transform.parent = TerminalPatch.Instance.transform;
                _gotShipNode = false;
            }
        }
    }
}