using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class UnlockableSuitPatch
    {
        [HarmonyPatch(typeof(UnlockableSuit), nameof(UnlockableSuit.SwitchSuitServerRpc))]
        [HarmonyPostfix]
        private static void SwitchSuitServerRpc(UnlockableSuit __instance, int playerID)
        {
            var player = StartOfRound.Instance && StartOfRound.Instance.allPlayerScripts != null ? StartOfRound.Instance.allPlayerScripts.ElementAtOrDefault(playerID) : null;

            // As the host, keep track of each player's suit ID as they manually put one on
            if (Plugin.SavePlayerSuits.Value && __instance.IsHost && player && player.playerSteamId != default && StartOfRoundPatch.SteamIDsToSuits.GetValueOrDefault(player.playerSteamId) != __instance.suitID)
            {
                StartOfRoundPatch.SteamIDsToSuits[player.playerSteamId] = __instance.suitID;
                Plugin.MLS.LogDebug($"Player {player.playerUsername} switched to suit ID {__instance.suitID}.");
            }
        }

        [HarmonyPatch(typeof(UnlockableSuit), nameof(UnlockableSuit.SwitchSuitClientRpc))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SwitchSuitClientRpc(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.SavePlayerSuits.Value)
            {
                if (instructions.TryFindInstructions(new System.Func<CodeInstruction, bool>[]
                {
                    // Checking if local client == client arg
                    i => i.Calls(typeof(GameNetworkManager).GetMethod("get_Instance")),
                    i => i.LoadsField(typeof(GameNetworkManager).GetField(nameof(GameNetworkManager.localPlayerController))),
                    i => i.LoadsField(typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.playerClientId))),
                    i => i.opcode == OpCodes.Conv_I4,
                    i => i.IsLdarg(1),

                    // Skipping the return if not
                    i => i.Branches(out _),
                    i => i.opcode == OpCodes.Ret,

                    // Loading args for SwitchSuitForPlayer
                    null, null, null, null, null, null,
                    i => i.LoadsConstant(1)
                }, out var found))
                {
                    Plugin.MLS.LogDebug("Patching UnlockableSuit.SwitchSuitClientRpc to allow hosts to send suit info on join.");

                    // Nop out the skip
                    codeList[found[5].Index] = new CodeInstruction(OpCodes.Nop);
                    codeList[found[6].Index] = new CodeInstruction(OpCodes.Nop);

                    // Move the first check to be passed as a bool for whether to play the audio instead
                    codeList.RemoveAt(found.Last().Index);
                    var args = found.Take(5).Select(f => f.Instruction).Concat(new[]
                    {
                        // Use a double ceq to pass != for the bool
                        new CodeInstruction(OpCodes.Ceq),
                        new CodeInstruction(OpCodes.Ldc_I4_0),
                        new CodeInstruction(OpCodes.Ceq),
                    });
                    codeList.InsertRange(found.Last().Index, args);
                    codeList.RemoveRange(found[0].Index, 5);
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected IL code - Could not patch UnlockableSuit.SwitchSuitClientRpc to allow hosts to send suit info on join!");
                }
            }

            return codeList;
        }
    }
}