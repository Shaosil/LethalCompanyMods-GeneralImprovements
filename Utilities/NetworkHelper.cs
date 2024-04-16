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

                // Overwrite our config settings with whatever the host sent over, and re-initialize the monitors
                Plugin.ShipMonitorAssignments[0].Value = monitor1;
                Plugin.ShipMonitorAssignments[1].Value = monitor2;
                Plugin.ShipMonitorAssignments[2].Value = monitor3;
                Plugin.ShipMonitorAssignments[3].Value = monitor4;
                Plugin.ShipMonitorAssignments[4].Value = monitor5;
                Plugin.ShipMonitorAssignments[5].Value = monitor6;
                Plugin.ShipMonitorAssignments[6].Value = monitor7;
                Plugin.ShipMonitorAssignments[7].Value = monitor8;
                Plugin.ShipMonitorAssignments[8].Value = monitor9;
                Plugin.ShipMonitorAssignments[9].Value = monitor10;
                Plugin.ShipMonitorAssignments[10].Value = monitor11;
                Plugin.ShipMonitorAssignments[11].Value = monitor12;
                Plugin.ShipMonitorAssignments[12].Value = monitor13;
                Plugin.ShipMonitorAssignments[13].Value = monitor14;

                Plugin.MLS.LogInfo("Received monitor settings from host - syncing.");
                MonitorsHelper.InitializeMonitors();
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