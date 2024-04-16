using GeneralImprovements.Patches;
using Unity.Netcode;

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
                var monitorAssignments = new string[14];
                monitorAssignments[0] = monitor1;
                monitorAssignments[1] = monitor2;
                monitorAssignments[2] = monitor3;
                monitorAssignments[3] = monitor4;
                monitorAssignments[4] = monitor5;
                monitorAssignments[5] = monitor6;
                monitorAssignments[6] = monitor7;
                monitorAssignments[7] = monitor8;
                monitorAssignments[8] = monitor9;
                monitorAssignments[9] = monitor10;
                monitorAssignments[10] = monitor11;
                monitorAssignments[11] = monitor12;
                monitorAssignments[12] = monitor13;
                monitorAssignments[13] = monitor14;

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
    }
}