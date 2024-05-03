using HarmonyLib;
using System.Collections.Generic;
using System.Linq;

namespace GeneralImprovements.Patches
{
    internal static class UnlockableSuitPatch
    {
        [HarmonyPatch(typeof(UnlockableSuit), nameof(UnlockableSuit.SwitchSuitServerRpc))]
        [HarmonyPostfix]
        private static void SwitchSuitServerRpc(UnlockableSuit __instance, int playerID)
        {
            var player = StartOfRound.Instance?.allPlayerScripts.ElementAtOrDefault(playerID);

            // As the host, keep track of each player's suit ID as they manually put one on
            if (Plugin.SavePlayerSuits.Value && __instance.IsHost && player != null && player.playerSteamId != default && StartOfRoundPatch.SteamIDsToSuits.GetValueOrDefault(player.playerSteamId) != __instance.suitID)
            {
                StartOfRoundPatch.SteamIDsToSuits[player.playerSteamId] = __instance.suitID;
                Plugin.MLS.LogDebug($"Player {player.playerUsername} switched to suit ID {__instance.suitID}.");
            }
        }
    }
}