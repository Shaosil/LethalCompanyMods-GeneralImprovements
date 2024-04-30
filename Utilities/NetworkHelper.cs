using GeneralImprovements.Patches;
using System;
using Unity.Netcode;
using static GeneralImprovements.Plugin.Enums;

namespace GeneralImprovements.Utilities
{
    internal class NetworkHelper : NetworkBehaviour
    {
        public static NetworkHelper Instance { get; private set; }

        private void Start()
        {
            Instance = this;
        }

        [ClientRpc]
        public void SyncMonitorsFromHostClientRpc(bool usingBetterMonitors, string monitor1, string monitor2, string monitor3, string monitor4, string monitor5, string monitor6, string monitor7,
            string monitor8, string monitor9, string monitor10, string monitor11, string monitor12, string monitor13, string monitor14, ClientRpcParams clientParams)
        {
            if (Plugin.SyncMonitorsFromOtherHost.Value)
            {
                if (usingBetterMonitors != Plugin.UseBetterMonitors.Value)
                {
                    Plugin.MLS.LogError($"Received monitor settings from host but could not apply, since the host IS{(usingBetterMonitors ? "" : " NOT")} using better monitors but you ARE{(Plugin.UseBetterMonitors.Value ? "" : " NOT")}.");
                    return;
                }

                // Reinitialize the monitors with whatever the host sent over
                var monitorAssignments = new eMonitorNames[14];
                monitorAssignments[0] = (eMonitorNames)Enum.Parse(typeof(eMonitorNames), monitor1);
                monitorAssignments[1] = (eMonitorNames)Enum.Parse(typeof(eMonitorNames), monitor2);
                monitorAssignments[2] = (eMonitorNames)Enum.Parse(typeof(eMonitorNames), monitor3);
                monitorAssignments[3] = (eMonitorNames)Enum.Parse(typeof(eMonitorNames), monitor4);
                monitorAssignments[4] = (eMonitorNames)Enum.Parse(typeof(eMonitorNames), monitor5);
                monitorAssignments[5] = (eMonitorNames)Enum.Parse(typeof(eMonitorNames), monitor6);
                monitorAssignments[6] = (eMonitorNames)Enum.Parse(typeof(eMonitorNames), monitor7);
                monitorAssignments[7] = (eMonitorNames)Enum.Parse(typeof(eMonitorNames), monitor8);
                monitorAssignments[8] = (eMonitorNames)Enum.Parse(typeof(eMonitorNames), monitor9);
                monitorAssignments[9] = (eMonitorNames)Enum.Parse(typeof(eMonitorNames), monitor10);
                monitorAssignments[10] = (eMonitorNames)Enum.Parse(typeof(eMonitorNames), monitor11);
                monitorAssignments[11] = (eMonitorNames)Enum.Parse(typeof(eMonitorNames), monitor12);
                monitorAssignments[12] = (eMonitorNames)Enum.Parse(typeof(eMonitorNames), monitor13);
                monitorAssignments[13] = (eMonitorNames)Enum.Parse(typeof(eMonitorNames), monitor14);

                Plugin.MLS.LogInfo("Received monitor settings from host - syncing.");
                MonitorsHelper.InitializeMonitors(monitorAssignments);
            }
        }

        [ClientRpc]
        public void SyncExtraDataOnConnectClientRpc(int quotaNum, int totalDays, int totalDeaths, int daysSinceLastDeath, ClientRpcParams clientParams)
        {
            if (!IsServer && StartOfRound.Instance != null)
            {
                Plugin.MLS.LogInfo("Client received extra data sync RPC.");
                TimeOfDay.Instance.timesFulfilledQuota = quotaNum;
                StartOfRound.Instance.gameStats.daysSpent = totalDays;
                StartOfRound.Instance.gameStats.deaths = totalDeaths;
                StartOfRoundPatch.DaysSinceLastDeath = daysSinceLastDeath;

                MonitorsHelper.UpdateTotalQuotasMonitors();
                MonitorsHelper.UpdateTotalDaysMonitors();
                MonitorsHelper.UpdateDeathMonitors();
            }
        }

        [ClientRpc]
        public void SyncSprayPaintItemColorsClientRpc(int[] matIndexes)
        {
            // After receiving information about which spray cans use which materials from the host, update each of them
            if (!IsServer)
            {
                // Assume the number of cans match and were loaded in the same order, but make sure we don't go past either array's length
                Plugin.MLS.LogInfo("Received spray can colors from host - syncing.");
                var sprayPaintItems = SprayPaintItemPatch.GetAllOrderedSprayPaintItemsInShip();
                for (int i = 0; i < sprayPaintItems.Length && i < matIndexes.Length; i++)
                {
                    SprayPaintItemPatch.UpdateColor(sprayPaintItems[i], matIndexes[i]);
                }
            }
        }
    }
}