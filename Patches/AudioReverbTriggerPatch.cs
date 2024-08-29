using GeneralImprovements.Utilities;
using HarmonyLib;
using System;
using System.Collections.Generic;

namespace GeneralImprovements.Patches
{
    internal static class AudioReverbTriggerPatch
    {
        public static AudioReverbPresets CurrentAudioReverbPresets = null;

        [HarmonyPatch(typeof(AudioReverbTrigger), nameof(AudioReverbTrigger.ChangeAudioReverbForPlayer))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> ChangeAudioReverbForPlayer(IEnumerable<CodeInstruction> instructions)
        {
            // Replace the FindObjectOfType lookup every frame with our cached result
            if (instructions.TryFindInstructions(new Func<CodeInstruction, bool>[]
            {
                i => i.LoadsConstant(-1),
                i => i.Branches(out _),
                i => i.Calls(typeof(UnityEngine.Object).GetMethod(nameof(UnityEngine.Object.FindObjectOfType), Type.EmptyTypes).MakeGenericMethod(typeof(AudioReverbPresets))),
                i => i.IsStloc()
            }, out var findObjectInstructions))
            {
                Plugin.MLS.LogDebug("Patching AudioReverbTrigger.ChangeAudioReverbForPlayer to optimize code.");
                findObjectInstructions[2].Instruction.operand = Transpilers.EmitDelegate<Func<AudioReverbPresets>>(() => CurrentAudioReverbPresets).operand;
            }
            else
            {
                Plugin.MLS.LogWarning("Unexpected IL Code - Could not patch AudioReverbTrigger.ChangeAudioReverbForPlayer to optimize code!");
            }

            return instructions;
        }
    }
}