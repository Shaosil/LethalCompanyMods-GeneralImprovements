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