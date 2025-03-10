using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using GeneralImprovements.Utilities;
using HarmonyLib;

namespace GeneralImprovements.Patches
{
    internal static class DoorLockPatch
    {
        [HarmonyPatch(typeof(DoorLock), nameof(Update))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Update(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codeList = instructions.ToList();

            if (Plugin.UnlockDoorsFromInventory.Value)
            {
                Label? elseLabel = null;

                // Ensure the code is as expected (checking for itemID == 14)
                if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.Branches(out elseLabel),
                    null,
                    null,
                    null,
                    null,
                    i => i.LoadsField(typeof(Item).GetField(nameof(Item.itemId))),
                    i => i.LoadsConstant(14),
                    i => i.Branches(out _)
                }, out var foundInstructions))
                {
                    // Declare a delegate to avoid writing this in IL
                    var injectedFunction = Transpilers.EmitDelegate<Func<bool>>(() =>
                    {
                        // Return true if the player has a key in their inventory
                        if (StartOfRound.Instance && StartOfRound.Instance.localPlayerController && StartOfRound.Instance.localPlayerController.ItemSlots != null)
                        {
                            for (int i = 0; i < StartOfRound.Instance.localPlayerController.ItemSlots.Length; i++)
                            {
                                if (StartOfRound.Instance.localPlayerController.ItemSlots[i] is KeyItem)
                                {
                                    return true;
                                }
                            }
                        }

                        return false;
                    });

                    // Define new labels for the OR and existing (true) code block
                    var orLabel = generator.DefineLabel();
                    var blockLabel = generator.DefineLabel();

                    // Change existing if statement to be ((<existing checks>) || Delegate)
                    codeList[foundInstructions[0].Index].operand = orLabel;                                 // Jump to || instead of past the entire if
                    codeList[foundInstructions[7].Index] = new CodeInstruction(OpCodes.Beq_S, blockLabel);  // Jump to the code block
                    codeList.InsertRange(foundInstructions.Last().Index + 1, new[]
                    {
                        new CodeInstruction(OpCodes.Ldc_I4_1).WithLabels(orLabel),          // Load 1 onto the stack (for true) and label it
                        injectedFunction,                                                   // Load our delegate onto the stack (for comparison)
                        new CodeInstruction(OpCodes.Bne_Un_S, elseLabel.Value)              // If not equal, jump past block
                    });
                    codeList[foundInstructions.Last().Index + 4].labels.Add(blockLabel);    // Add a new label to the true block

                    Plugin.MLS.LogDebug("Patching DoorLock Update() to update hovertip when a key is in inventory.");
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected code found, could not patch DoorLock Update()!");
                }
            }

            return codeList;
        }
    }
}