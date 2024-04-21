using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

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
                // Ensure the code is as expected (checking for itemID == 14)
                if (codeList[23].operand is FieldInfo fi && fi == typeof(Item).GetField(nameof(Item.itemId)) && int.TryParse(codeList[24].operand.ToString(), out var id) && id == 14)
                {
                    // Declare a delegate to avoid writing this in IL
                    var injectedFunction = Transpilers.EmitDelegate<Func<bool>>(() =>
                    {
                        // Return true if the player has a key in their inventory
                        for (int i = 0; i < (StartOfRound.Instance?.localPlayerController?.ItemSlots.Length ?? 0); i++)
                        {
                            if (StartOfRound.Instance.localPlayerController.ItemSlots[i] is KeyItem)
                            {
                                return true;
                            }
                        }

                        return false;
                    });

                    // Define new labels for the OR and existing (true) code block
                    var orLabel = generator.DefineLabel();
                    var blockLabel = generator.DefineLabel();
                    var elseLabel = codeList[39].labels.First();

                    // Change existing if statement to be ((<existing checks>) || Delegate)
                    codeList[18].operand = orLabel;                                 // Jump to || instead of past the entire if
                    codeList[25] = new CodeInstruction(OpCodes.Beq_S, blockLabel);  // Jump to the code block
                    codeList.InsertRange(26, new[]
                    {
                        new CodeInstruction(OpCodes.Ldc_I4_1) { labels = new List<Label>(new[] { orLabel }) },      // Load 1 onto the stack (for true) and label it
                        injectedFunction,                                                                           // Load our delegate onto the stack (for comparison)
                        new CodeInstruction(OpCodes.Bne_Un_S, elseLabel)                                            // If not equal, jump past block
                    });
                    codeList[29].labels.Add(blockLabel); // Add a new label to the true block

                    Plugin.MLS.LogDebug("Patching DoorLock Update() to update hovertip when a key is in inventory.");
                }
                else
                {
                    Plugin.MLS.LogError("Unexpected code found, could not patch DoorLock Update()!");
                }
            }

            return codeList.AsEnumerable();
        }

        //[HarmonyPatch(typeof(DoorLock), nameof(Update))]
        //[HarmonyPostfix]
        //private static void Update2(DoorLock __instance, InteractTrigger ___doorTrigger)
        //{
        //    if (Plugin.UnlockDoorsFromInventory.Value)
        //    {
        //        var localPlayer = StartOfRound.Instance?.localPlayerController;

        //        if (localPlayer != null && __instance.isLocked)
        //        {
        //            // If the player is looking at the locked door with a key in their inventory, override the
        //            var mask = LayerMask.GetMask("Room", "InteractableObject", "Colliders");
        //            if (Physics.Raycast(new Ray(localPlayer.gameplayCamera.transform.position, localPlayer.gameplayCamera.transform.forward), out var hit, 3f, mask)
        //                && localPlayer.ItemSlots.FirstOrDefault(i => i is KeyItem) is KeyItem key)
        //            {
        //                ___doorTrigger.disabledHoverTip = "Use key: [ LMB ]";

        //                // If the player clicks the left mouse button, activate that key
        //                if (Mouse.current.leftButton.wasPressedThisFrame)
        //                {
        //                    key.ItemActivate(true);
        //                }
        //            }
        //        }
        //    }
        //}
    }
}