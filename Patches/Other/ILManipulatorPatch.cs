using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using GeneralImprovements.Utilities;
using HarmonyLib;

namespace GeneralImprovements.Patches.Other
{
    internal static class ILManipulatorPatch
    {
        [HarmonyPatch("HarmonyLib.Internal.Patching.ILManipulator", "WriteTo")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> WriteToTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            // Fix a transpiler error in certain functions by removing the check for Leave opcodes and empty exception blocks before Emitting instructions (https://github.com/BepInEx/HarmonyX/issues/65)
            if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
            {
                // Is Leave?
                i => i.IsLdloc(),
                i => i.LoadsField(typeof(CodeInstruction).GetField(nameof(CodeInstruction.opcode))),
                i => i.LoadsField(typeof(OpCodes).GetField(nameof(OpCodes.Leave))),
                i => i.Calls(typeof(OpCode).GetMethod("op_Equality")),
                i => i.Branches(out _),

                // Is Leave_S?
                i => i.IsLdloc(),
                i => i.LoadsField(typeof(CodeInstruction).GetField(nameof(CodeInstruction.opcode))),
                i => i.LoadsField(typeof(OpCodes).GetField(nameof(OpCodes.Leave_S))),
                i => i.Calls(typeof(OpCode).GetMethod("op_Equality")),
                i => i.Branches(out _),

                // No exception blocks?
                i => i.IsLdloc(),
                i => i.LoadsField(typeof(CodeInstruction).GetField(nameof(CodeInstruction.blocks))),
                i => i.Calls(typeof(List<ExceptionBlock>).GetMethod("get_Count")),
                i => i.LoadsConstant(0),
                i => i.Branches(out _),
                i => i.IsLdloc(),
                i => i.Branches(out _),
                i => i.IsLdloc(),
                i => i.LoadsField(typeof(CodeInstruction).GetField(nameof(CodeInstruction.blocks))),
                i => i.Calls((typeof(List<ExceptionBlock>).GetMethod("get_Count"))),
                i => i.LoadsConstant(0),
                i => i.Branches(out _)

            }, out var leaveCheckCode))
            {
                Plugin.MLS.LogDebug("Patching ILManipulator.WriteTo to fix unexpected transpiler behavior.");

                // Remove every check that we searched for by simply NOPing the instructions
                for (int i = 0; i < leaveCheckCode.Length; i++)
                {
                    codeList[leaveCheckCode[i].Index].opcode = OpCodes.Nop;
                    codeList[leaveCheckCode[i].Index].operand = null;
                }
            }
            else
            {
                Plugin.MLS.LogError("Could not patch ILManipulator.WriteTo to fix unexpected transpiler behavior! Some game behavior may break.");
            }

            return codeList;
        }
    }
}