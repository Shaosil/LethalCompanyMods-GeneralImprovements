using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace GeneralImprovements.Patches.Other
{
    internal static class CodeRebirthPatch
    {
        internal static IEnumerable<CodeInstruction> CustomPickableObjectsTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase method)
        {
            return GrabbableObjectsPatch.ApplyGenericKeyActivateTranspiler(instructions.ToList(), method);
        }
    }
}