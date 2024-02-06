using HarmonyLib;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GeneralImprovements.Patches
{
    internal static class DoorLockPatch
    {
        [HarmonyPatch(typeof(DoorLock), nameof(Update))]
        [HarmonyPostfix]
        private static void Update(DoorLock __instance, InteractTrigger ___doorTrigger)
        {
            if (Plugin.UnlockDoorsFromInventory.Value)
            {
                var localPlayer = StartOfRound.Instance?.localPlayerController;

                if (localPlayer != null && __instance.isLocked)
                {
                    // If the player is looking at the locked door with a key in their inventory, override the
                    var mask = LayerMask.GetMask("Room", "InteractableObject", "Colliders");
                    if (Physics.Raycast(new Ray(localPlayer.gameplayCamera.transform.position, localPlayer.gameplayCamera.transform.forward), out var hit, 3f, mask)
                        && localPlayer.ItemSlots.FirstOrDefault(i => i is KeyItem) is KeyItem key)
                    {
                        ___doorTrigger.disabledHoverTip = "Use key: [ LMB ]";

                        // If the player clicks the left mouse button, activate that key
                        if (Mouse.current.leftButton.wasPressedThisFrame)
                        {
                            key.ItemActivate(true);
                        }
                    }
                }
            }
        }
    }
}