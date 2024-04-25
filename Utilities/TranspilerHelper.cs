using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeneralImprovements.Utilities
{
    internal static class TranspilerHelper
    {
        public class FoundInstruction
        {
            public CodeInstruction Instruction { get; private set; }
            public int Index { get; private set; }

            public FoundInstruction(CodeInstruction instruction, int index)
            {
                Instruction = instruction;
                Index = index;
            }
        }

        /// <summary>
        /// Searches through code instructions and tries to find one that matches the logic supplied by the function parameter.
        /// </summary>
        public static bool TryFindInstruction(this IEnumerable<CodeInstruction> instructions, Func<CodeInstruction, bool> testFunc, out FoundInstruction foundInstruction)
        {
            foundInstruction = null;

            int curIndex = 0;
            foreach (var instruction in instructions)
            {
                if (testFunc(instruction))
                {
                    foundInstruction = new FoundInstruction(instruction, curIndex);
                    return true;
                }

                curIndex++;
            }

            return false;
        }

        /// <summary>
        /// Searches through code instructions and tries to find a series that match the logic supplied by the function parameters.
        /// </summary>
        /// <param name="testFuncs">An array of consecutive functions to test. If some entries are null, they will match any instruction. Otherwise, each function must be true for the match to be successful.</param>
        public static bool TryFindInstructions(this IEnumerable<CodeInstruction> instructions, Func<CodeInstruction, bool>[] testFuncs, out FoundInstruction[] foundInstructions)
        {
            foundInstructions = new FoundInstruction[0];

            if (testFuncs == null || testFuncs.Length < 1 || testFuncs.All(f => f == null))
            {
                return false;
            }

            var codeList = instructions.ToList();

            for (int i = 0; i < codeList.Count - (testFuncs.Length - 1); i++)
            {
                // Test for each supplied function.
                for (int f = 0; f < testFuncs.Length; f++)
                {
                    if (testFuncs[f] != null && !testFuncs[f](codeList[i + f]))
                    {
                        // If any of them are specified and fail the check, do not continue checking the rest
                        break;
                    }
                    else
                    {
                        if (f == testFuncs.Length - 1)
                        {
                            // If we reached the end of the test funcs and they have all been successful, we have found the specified matching lines
                            foundInstructions = codeList.Skip(i).Take(testFuncs.Length).Select((c, idx) => new FoundInstruction(c, i + idx)).ToArray();
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}