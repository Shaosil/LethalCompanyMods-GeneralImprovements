using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using static GeneralImprovements.Enums;

namespace GeneralImprovements.Patches
{
    internal static class PlayerControllerBPatch
    {
        public static Dictionary<PlayerControllerB, int> PlayerMaxHealthValues = new Dictionary<PlayerControllerB, int>();

        private static Func<bool> _flashlightTogglePressed;
        private static FieldInfo _timeSinceSwitchingSlotsField = null;
        private static FieldInfo TimeSinceSwitchingSlotsField
        {
            get
            {
                // Lazy load and cache the reflection info
                if (_timeSinceSwitchingSlotsField == null)
                {
                    _timeSinceSwitchingSlotsField = typeof(PlayerControllerB).GetField("timeSinceSwitchingSlots", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                return _timeSinceSwitchingSlotsField;
            }
        }
        private static float _originalCursorScale = 0;

        [HarmonyPatch(typeof(PlayerControllerB), nameof(Awake))]
        [HarmonyPostfix]
        private static void Awake()
        {
            bool hasToggleShortcut = Plugin.FlashlightToggleShortcut.Value != eValidKeys.None;
            if (Plugin.FlashlightToggleShortcut.Value >= eValidKeys.MouseLeft)
            {
                _flashlightTogglePressed = () => GetMouseButtonMapping(Plugin.FlashlightToggleShortcut.Value).wasPressedThisFrame;
            }
            else
            {
                var control = hasToggleShortcut && Enum.TryParse<Key>(Plugin.FlashlightToggleShortcut.Value.ToString(), out var flashlightToggleKey) ? Keyboard.current[flashlightToggleKey] : null;
                _flashlightTogglePressed = () => control?.wasPressedThisFrame ?? false;
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.High)]
        private static void ConnectClientToPlayerObjectPre(PlayerControllerB __instance)
        {
            _originalCursorScale = __instance.cursorIcon.transform.localScale.x;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPriority(Priority.Low)]
        [HarmonyFinalizer] // Need a finalizer because CorporateRestructure sometimes has an exception before this, stopping our hide code
        private static void ConnectClientToPlayerObjectPost()
        {
            // If using the new monitors, hide the old text objects HERE in order to let other mods utilize them first
            if (Plugin.UseBetterMonitors.Value)
            {
                MonitorsHelper.HideOldMonitors();
            }

            // Create the initial other players scan nodes
            if (Plugin.ScanPlayers.Value)
            {
                foreach (var player in StartOfRound.Instance.allPlayerScripts.Where(p => StartOfRound.Instance.localPlayerController != p))
                {
                    var node = ObjectHelper.CreateScanNodeOnObject(player.gameObject, 0, 1, 10, player.playerUsername, ObjectHelper.GetEntityHealthDescription(1, 1));
                    node.transform.localPosition += new Vector3(0, 2.25f, 0);
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(SendNewPlayerValuesServerRpc))]
        [HarmonyPostfix]
        private static void SendNewPlayerValuesServerRpc(PlayerControllerB __instance)
        {
            // When the host receives the steam ID, update the caller's suit if we can
            UpdatePlayerSuitToSavedValue(__instance);
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(SendNewPlayerValuesClientRpc))]
        [HarmonyPostfix]
        private static void SendNewPlayerValuesClientRpc()
        {
            // Update scan nodes now that we have the Steam names
            if (Plugin.ScanPlayers.Value)
            {
                foreach (var player in StartOfRound.Instance.allPlayerScripts)
                {
                    var scanNode = player.GetComponentInChildren<ScanNodeProperties>();
                    if (scanNode != null)
                    {
                        int curHealth = player.health;
                        int maxHealth = PlayerMaxHealthValues.ContainsKey(player) ? PlayerMaxHealthValues[player] : 100;

                        scanNode.headerText = player.playerUsername;
                        scanNode.subText = ObjectHelper.GetEntityHealthDescription(curHealth, maxHealth);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(ActivateItem_performed))]
        [HarmonyPostfix]
        private static void ActivateItem_performed(PlayerControllerB __instance)
        {
            // If specified, attempt to use the first key found in our inventory
            if (Plugin.UnlockDoorsFromInventory.Value && CanUseAnyItem(__instance))
            {
                for (int i = 0; i < __instance.ItemSlots.Length; i++)
                {
                    if (__instance.ItemSlots[i] is KeyItem key)
                    {
                        key.ItemActivate(true);
                        break;
                    }
                }
            }
        }

        private static bool CanUseAnyItem(PlayerControllerB instance)
        {
            // Copied from CanUseItem(), but removed checks for currently held item
            return ((instance.IsOwner && instance.isPlayerControlled && (!instance.IsServer || instance.isHostPlayerObject)) || instance.isTestingPlayer)
                && !instance.quickMenuManager.isMenuOpen && !instance.isPlayerDead
                && (!instance.isGrabbingObjectAnimation && !instance.inTerminalMenu && !instance.isTypingChat && (!instance.inSpecialInteractAnimation || instance.inShockingMinigame));
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(ItemTertiaryUse_performed))]
        [HarmonyPrefix]
        private static bool ItemTertiaryUse_performed(PlayerControllerB __instance)
        {
            // Flashlights do not have tertiary uses, so cancel out when this is called in that case
            return OtherModHelper.FlashlightFixActive || !(__instance.currentlyHeldObjectServer is FlashlightItem);
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(SwitchToItemSlot))]
        [HarmonyPostfix]
        private static void SwitchToItemSlot(PlayerControllerB __instance, int slot)
        {
            if (!OtherModHelper.FlashlightFixActive && __instance.ItemSlots[slot] is FlashlightItem slotFlashlight)
            {
                // If the player already has an active flashlight (helmet lamp will be on) when picking up a new INACTIVE one, switch to the new one
                if (__instance.IsOwner && !slotFlashlight.isBeingUsed && __instance.helmetLight.enabled && !slotFlashlight.CheckForLaser() && Plugin.OnlyAllowOneActiveFlashlight.Value)
                {
                    for (int i = 0; i < __instance.ItemSlots.Length; i++)
                    {
                        // Find the first active flashlights in our inventory that still has battery, and turn it on
                        if (i != slot && __instance.ItemSlots[i] is FlashlightItem otherFlashlight && otherFlashlight.usingPlayerHelmetLight
                            && !otherFlashlight.CheckForLaser() && !slotFlashlight.insertedBattery.empty && !slotFlashlight.CheckForLaser())
                        {
                            Plugin.MLS.LogDebug($"Flashlight in slot {slot} turning ON after switching to it");
                            slotFlashlight.UseItemOnClient();
                            break;
                        }
                    }
                }

                // Ensure we are using the proper helmet light each time we switch to a flashlight
                UpdateHelmetLight(__instance);
            }
        }

        public static void UpdateHelmetLight(PlayerControllerB player)
        {
            // The helmet light should always be the first sorted flashlight that is on (lasers are sorted last, then pocketed flashlights are prioritized)
            var activeLight = player.ItemSlots.OfType<FlashlightItem>()
                .Where(f => f.isBeingUsed)
                .OrderBy(f => f.CheckForLaser())
                .ThenByDescending(f => f.isPocketed)
                .FirstOrDefault();

            // Update it if the current helmet light is something else
            if (activeLight != null && player.helmetLight != player.allHelmetLights[activeLight.flashlightTypeID])
            {
                Plugin.MLS.LogDebug($"Updating helmet light to type {activeLight.flashlightTypeID} ({(activeLight.isPocketed ? "ON" : "OFF")}).");
                player.ChangeHelmetLight(activeLight.flashlightTypeID, activeLight.isPocketed);
            }

            // Always make sure the helmet light state is correct
            bool helmetLightShouldBeOn = activeLight != null && activeLight.isBeingUsed && activeLight.isPocketed;
            if (player.helmetLight != null && player.helmetLight.enabled != helmetLightShouldBeOn)
            {
                // Toggle helmet light here if needed
                Plugin.MLS.LogDebug($"Toggling helmet light {(helmetLightShouldBeOn ? "on" : "off")}.");
                player.helmetLight.enabled = helmetLightShouldBeOn;

                // Update pocket flashlight if needed
                if (helmetLightShouldBeOn && player.pocketedFlashlight != activeLight)
                {
                    Plugin.MLS.LogDebug("Updating pocketed flashlight");
                    player.pocketedFlashlight = activeLight;
                }
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(FirstEmptyItemSlot))]
        [HarmonyPrefix]
        private static bool FirstEmptyItemSlot(PlayerControllerB __instance, ref int __result)
        {
            // If not configured to pickup in order, OR the reserved item slot or advanced company mods exist, pass over this patch
            if (!Plugin.PickupInOrder.Value || OtherModHelper.ReservedItemSlotCoreActive || OtherModHelper.AdvancedCompanyActive)
            {
                return true;
            }

            // Otherwise, rewrite the method to always return the first empty slot
            __result = -1;

            for (int i = 0; i < __instance.ItemSlots.Length; i++)
            {
                if (__instance.ItemSlots[i] == null)
                {
                    __result = i;
                    break;
                }
            }

            return false;
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(GrabObjectClientRpc))]
        [HarmonyPrefix]
        private static void GrabObjectClientRpc(PlayerControllerB __instance, bool grabValidated, NetworkObjectReference grabbedObject)
        {
            // If the player is about to pick up a two handed item and we are configured to do this, make sure it lands in slot 1
            if (!Plugin.TwoHandedInSlotOne.Value || !grabValidated || !grabbedObject.TryGet(out var networkObject))
            {
                return;
            }

            // Make sure this is a two handed object and we aren't currently processing it
            var grabbableObject = networkObject.gameObject.GetComponentInChildren<GrabbableObject>();
            if (!grabbableObject?.itemProperties.twoHanded ?? false)
            {
                return;
            }

            Plugin.MLS.LogDebug($"Two handed item being grabbed!");

            // Move things over
            ShiftRightFromSlot(__instance, 0);
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(GrabObjectClientRpc))]
        [HarmonyPostfix]
        private static void GrabObjectClientRpc(PlayerControllerB __instance, NetworkObjectReference grabbedObject)
        {
            if (__instance.currentlyHeldObjectServer?.isInShipRoom ?? false)
            {
                MonitorsHelper.UpdateShipScrapMonitors();
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), "PlaceObjectClientRpc")]
        [HarmonyPatch(typeof(PlayerControllerB), "ThrowObjectClientRpc")]
        [HarmonyPatch(typeof(PlayerControllerB), "DespawnHeldObjectClientRpc")]
        [HarmonyPatch(typeof(PlayerControllerB), "DestroyItemInSlot")]
        [HarmonyPostfix]
        private static void ItemLeftSlot(PlayerControllerB __instance)
        {
            // If we are not configured to rearrange, or AdvancedCompany is active, skip this patch
            if (!Plugin.RearrangeOnDrop.Value || OtherModHelper.AdvancedCompanyActive)
            {
                return;
            }

            for (int i = 0; i < __instance.ItemSlots.Length - 1; i++)
            {
                // Each time we find an empty slot, move the first found item after this slot to this one
                if (__instance.ItemSlots[i] == null)
                {
                    for (int j = i + 1; j < __instance.ItemSlots.Length; j++)
                    {
                        // If we found a reserved core slot, skip it
                        if (OtherModHelper.IsReservedItemSlot(__instance, j))
                        {
                            continue;
                        }

                        if (__instance.ItemSlots[j] != null)
                        {
                            ShiftSlots(__instance, i, j);
                            break;
                        }
                    }
                }
            }

            // Refresh the current item slot if the player is holding something new
            var newHeldItem = __instance.ItemSlots[__instance.currentItemSlot];
            if (__instance.currentlyHeldObjectServer != newHeldItem)
            {
                if (newHeldItem != null)
                {
                    newHeldItem.EquipItem();
                }
                __instance.twoHanded = false;
                __instance.twoHandedAnimation = false;
                __instance.playerBodyAnimator.ResetTrigger("Throw");
                __instance.playerBodyAnimator.SetBool("Grab", true);
                if (!string.IsNullOrEmpty(newHeldItem?.itemProperties.grabAnim))
                {
                    __instance.playerBodyAnimator.SetBool(newHeldItem.itemProperties.grabAnim, true);
                }
                if (__instance.twoHandedAnimation != newHeldItem?.itemProperties.twoHandedAnimation)
                {
                    __instance.playerBodyAnimator.ResetTrigger("SwitchHoldAnimationTwoHanded");
                    __instance.playerBodyAnimator.SetTrigger("SwitchHoldAnimationTwoHanded");
                }
                __instance.playerBodyAnimator.ResetTrigger("SwitchHoldAnimation");
                __instance.playerBodyAnimator.SetTrigger("SwitchHoldAnimation");
                __instance.playerBodyAnimator.SetBool("GrabValidated", true);
                __instance.playerBodyAnimator.SetBool("cancelHolding", false);
                __instance.twoHandedAnimation = newHeldItem?.itemProperties.twoHandedAnimation ?? false;
                __instance.isHoldingObject = newHeldItem != null;
                __instance.currentlyHeldObjectServer = newHeldItem;
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(ScrollMouse_performed))]
        [HarmonyPostfix]
        private static void ScrollMouse_performed(PlayerControllerB __instance)
        {
            // If the code has just reset the timer to 0 (or very close to it), push it forward to decrease the time needed for the next check
            float desiredDelay = Math.Clamp(Plugin.ScrollDelay.Value, 0.05f, 0.3f);
            if ((float)TimeSinceSwitchingSlotsField.GetValue(__instance) < 0.1f)
            {
                TimeSinceSwitchingSlotsField.SetValue(__instance, 0.3f - desiredDelay);
            }
        }

        [HarmonyPatch(typeof(QuickMenuManager), nameof(OpenQuickMenu))]
        [HarmonyPrefix]
        private static bool OpenQuickMenu(QuickMenuManager __instance)
        {
            if (ShipBuildModeManager.Instance?.InBuildMode ?? false)
            {
                // Cancel out of build mode instead
                Plugin.MLS.LogInfo("Cancelling build mode and returning false!");
                ShipBuildModeManager.Instance.CancelBuildMode();
                return false;
            }

            return true;
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(SetHoverTipAndCurrentInteractTrigger))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SetHoverTipAndCurrentInteractTrigger_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codeList = instructions.ToList();
            Label? outsideLabel = null;

            // Do not set grab hovertip if grabbable item is not grabbable
            if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
            {
                i => i.Branches(out _),
                i => i.IsLdloc(),
                i => i.Is(OpCodes.Ldstr, "InteractTrigger"),
                i => i.Calls(typeof(String).GetMethod("op_Equality")),
                i => i.Branches(out _),
                i => i.Branches(out outsideLabel),

                i => i.IsLdarg(0), // Index 6
                i => i.Calls(typeof(PlayerControllerB).GetMethod("FirstEmptyItemSlot", BindingFlags.Instance | BindingFlags.NonPublic)),
                i => i.LoadsConstant(-1),
                i => i.Branches(out _), // Index 9
                i => i.IsLdarg(0),
                i => i.LoadsField(typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.cursorTip))),
                i => i.Is(OpCodes.Ldstr, "Inventory full!"),
                i => i.Calls(typeof(TMP_Text).GetMethod("set_text")),
                i => i.Branches(out _),

                i => i.IsLdarg(0), // Index 15
                i => i.LoadsField(typeof(PlayerControllerB).GetField("hit", BindingFlags.Instance | BindingFlags.NonPublic), true),
                i => i.Calls(typeof(RaycastHit).GetMethod("get_collider")),
                i => i.Calls(typeof(Component).GetMethod("get_gameObject")),
                i => i.Calls(typeof(GameObject).GetMethod(nameof(GameObject.GetComponent), 1, Type.EmptyTypes).MakeGenericMethod(typeof(GrabbableObject))),
                i => i.opcode == OpCodes.Stloc_2,

                i => i.Calls(typeof(GameNetworkManager).GetMethod("get_Instance"))
            }, out var found))
            {
                // Create a label on the line below our GetComponent call, and branch to that instead
                var newLabel = generator.DefineLabel();
                codeList[found.Last().Index].labels.Add(newLabel);
                found[9].Instruction.operand = newLabel;

                // Create a label on the GetComponent variable, and make sure it is branched to
                var componentLabel = generator.DefineLabel();
                found[15].Instruction.labels.Add(componentLabel);
                found[0].Instruction.operand = componentLabel;

                // Move the GetComponent variable to be above the FirstEmptyItemSlot if statement
                codeList.RemoveRange(found[15].Index, 6);
                codeList.InsertRange(found[6].Index, found.Skip(15).Take(6).Select(f => f.Instruction));

                // Add an if statement to break out of this entire section if the component is not grabbable
                codeList.InsertRange(found[6].Index + 6, new[]
                {
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldfld, typeof(GrabbableObject).GetField(nameof(GrabbableObject.grabbable))),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Bne_Un_S, outsideLabel)
                });

                Plugin.MLS.LogDebug("Patching PlayerControllerB.SetHoverTipAndCurrentInteractionTrigger to remove grab notification when not needed.");
            }
            else
            {
                Plugin.MLS.LogWarning("Unexpected IL code - Could not patch PlayerControllerB.SetHoverTipAndCurrentInteractionTrigger to remove the grab notification!");
            }

            return codeList;
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(SetHoverTipAndCurrentInteractTrigger))]
        [HarmonyPostfix]
        private static void SetHoverTipAndCurrentInteractTrigger(PlayerControllerB __instance)
        {
            if (Plugin.ShowUIReticle.Value && AssetBundleHelper.Reticle != null && __instance.isPlayerControlled && !__instance.inTerminalMenu)
            {
                // Use our reticle and resize
                if (!__instance.cursorIcon.enabled)
                {
                    __instance.cursorIcon.sprite = AssetBundleHelper.Reticle;
                    __instance.cursorIcon.color = new Color(1, 1, 1, 0.1f);
                    __instance.cursorIcon.enabled = true;
                    __instance.cursorIcon.transform.localScale = Vector3.one * 0.05f;
                }
                else if (__instance.cursorIcon.transform.localScale.x < _originalCursorScale)
                {
                    // Make sure we reset/turn back off when needed
                    __instance.cursorIcon.transform.localScale = Vector3.one * _originalCursorScale;
                    __instance.cursorIcon.color = Color.white;
                    __instance.cursorIcon.sprite = __instance.hoveringOverTrigger?.hoverIcon ?? __instance.cursorIcon.sprite;

                    if (__instance.cursorIcon.sprite == AssetBundleHelper.Reticle)
                    {
                        __instance.cursorIcon.enabled = false;
                    }
                }
            }

            if (Plugin.AddHealthRechargeStation.Value && ObjectHelper.MedStation != null && __instance.hoveringOverTrigger?.transform.parent == ObjectHelper.MedStation.transform && PlayerMaxHealthValues.ContainsKey(__instance))
            {
                __instance.hoveringOverTrigger.interactable = __instance.health < PlayerMaxHealthValues[__instance];
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(ShowNameBillboard))]
        [HarmonyPrefix]
        private static bool ShowNameBillboard()
        {
            // Do not show player names if we are hiding them, unless we are orbiting
            return !(Plugin.HidePlayerNames.Value && !(StartOfRound.Instance?.inShipPhase ?? true));
        }

        [HarmonyPatch(typeof(PlayerControllerB), "CalculateSmoothLookingInput")]
        [HarmonyPatch(typeof(PlayerControllerB), "CalculateNormalLookingInput")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> PatchLookDownClamper(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            if (!Plugin.AllowLookDownMore.Value)
            {
                return instructions;
            }

            if (instructions.TryFindInstructions(new Func<CodeInstruction, bool>[]
            {
                i => i.LoadsField(typeof(PlayerControllerB).GetField("cameraUp", BindingFlags.NonPublic | BindingFlags.Instance)),
                i => i.LoadsConstant(-80f),
                i => i.LoadsConstant(60f),
                i => i.Calls(typeof(Mathf).GetMethod(nameof(Mathf.Clamp), new[] { typeof(float), typeof(float), typeof(float) }))
            }, out var found))
            {
                Plugin.MLS.LogDebug($"Updating look down angle to 85 in {original.Name}.");
                found[2].Instruction.operand = 85f;
            }
            else
            {
                Plugin.MLS.LogError($"Unexpected IL code found - Could not patch look down angle in {original.Name}!");
            }

            return instructions;
        }

        private static void ShiftRightFromSlot(PlayerControllerB player, int slot)
        {
            // Double check the object is not two handed to prevent multiple calls to this from happening
            if (player.ItemSlots[slot] != null)
            {
                // If the slot to the right has an item, recursively move that to the right as well
                if (player.ItemSlots[slot + 1] != null)
                {
                    ShiftRightFromSlot(player, slot + 1);
                }

                ShiftSlots(player, slot + 1, slot);
            }
        }

        private static void ShiftSlots(PlayerControllerB player, int newSlot, int oldSlot)
        {
            Plugin.MLS.LogDebug($"Shifting slot {oldSlot} to {newSlot}");

            // Update the owner's UI
            if (player.IsOwner)
            {
                HUDManager.Instance.itemSlotIcons[newSlot].sprite = player.ItemSlots[oldSlot].itemProperties.itemIcon;
                HUDManager.Instance.itemSlotIcons[newSlot].enabled = true;
                HUDManager.Instance.itemSlotIcons[oldSlot].enabled = false;
            }

            // Move item
            player.ItemSlots[newSlot] = player.ItemSlots[oldSlot];
            player.ItemSlots[oldSlot] = null;
        }

        private static void SelectNewSlotLocal(PlayerControllerB player, int newSlot)
        {
            // This should only be used when an item has shifted, so we don't need any animations or grab code
            for (int i = 0; i < HUDManager.Instance.itemSlotIconFrames.Length; i++)
            {
                HUDManager.Instance.itemSlotIconFrames[i].GetComponent<Animator>().SetBool("selectedSlot", false);
            }
            HUDManager.Instance.itemSlotIconFrames[newSlot].GetComponent<Animator>().SetBool("selectedSlot", true);
            player.currentItemSlot = newSlot;
        }

        public static void DropAllItemsExceptHeld(PlayerControllerB player, bool onlyDropScrap)
        {
            int oldHeldSlot = -1;

            // Had to copy this from DropAllHeldItems() since vanilla code doesn't have a way to discard only a single item from a specific slot
            for (int i = 0; i < player.ItemSlots.Length; i++)
            {
                // If the checked item is held or not scrap (if we only drop scrap), skip this one
                bool isHeldItem = player.currentlyHeldObjectServer != null && player.ItemSlots[i] == player.currentlyHeldObjectServer && !onlyDropScrap;
                if (isHeldItem || (player.ItemSlots[i] != null && !player.ItemSlots[i].itemProperties.isScrap && onlyDropScrap))
                {
                    if (isHeldItem)
                    {
                        oldHeldSlot = i;
                    }
                    continue;
                }

                var grabbableObject = player.ItemSlots[i];
                if (grabbableObject != null)
                {
                    grabbableObject.parentObject = null;
                    grabbableObject.heldByPlayerOnServer = false;
                    if (player.isInElevator)
                    {
                        grabbableObject.transform.SetParent(player.playersManager.elevatorTransform, true);
                    }
                    else
                    {
                        grabbableObject.transform.SetParent(player.playersManager.propsContainer, true);
                    }
                    player.SetItemInElevator(player.isInHangarShipRoom, player.isInElevator, grabbableObject);
                    grabbableObject.EnablePhysics(true);
                    grabbableObject.EnableItemMeshes(true);
                    grabbableObject.transform.localScale = grabbableObject.originalScale;
                    grabbableObject.isHeld = false;
                    grabbableObject.isPocketed = false;
                    grabbableObject.startFallingPosition = grabbableObject.transform.parent.InverseTransformPoint(grabbableObject.transform.position);
                    grabbableObject.FallToGround(true);
                    grabbableObject.fallTime = UnityEngine.Random.Range(-0.3f, 0.05f);
                    if (player.IsOwner)
                    {
                        grabbableObject.DiscardItemOnClient();
                    }
                    else if (!grabbableObject.itemProperties.syncDiscardFunction)
                    {
                        grabbableObject.playerHeldBy = null;
                    }

                    player.ItemSlots[i] = null;
                    HUDManager.Instance.itemSlotIcons[i].enabled = false;
                }
            }

            // Shift whatever necessary and select the new slot
            ItemLeftSlot(player);
            for (int i = 0; i < player.ItemSlots.Length; i++)
            {
                if (oldHeldSlot > -1 && i < oldHeldSlot && player.ItemSlots[i] == player.currentlyHeldObjectServer)
                {
                    SelectNewSlotLocal(player, i);
                    break;
                }
            }

            player.carryWeight = 1 + player.ItemSlots.Sum(i => (i?.itemProperties.weight ?? 1) - 1);
            player.twoHanded = oldHeldSlot > -1 && (player.ItemSlots[oldHeldSlot]?.itemProperties.twoHanded ?? false);
            if (player.IsOwner)
            {
                HUDManager.Instance.holdingTwoHandedItem.enabled = player.twoHanded;
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(Update))]
        [HarmonyPostfix]
        private static void Update(PlayerControllerB __instance)
        {
            // Keep max health values up to date
            if (PlayerMaxHealthValues.GetValueOrDefault(__instance) < __instance.health)
            {
                Plugin.MLS.LogInfo($"Storing player {__instance.playerUsername}'s max health as {__instance.health}");
                PlayerMaxHealthValues[__instance] = __instance.health;
            }

            if (!__instance.IsOwner || Plugin.FlashlightToggleShortcut.Value == eValidKeys.None || __instance.inTerminalMenu || __instance.isTypingChat || !__instance.isPlayerControlled)
            {
                return;
            }

            if (!OtherModHelper.FlashlightFixActive && _flashlightTogglePressed())
            {
                // Get the nearest flashlight with charge, whether it's held or in the inventory
                var targetFlashlight = __instance.ItemSlots.OfType<FlashlightItem>().Where(f => !f.insertedBattery.empty) // All charged flashlight items
                    .OrderBy(f => f.CheckForLaser()) // Sort by non-lasers first
                    .ThenByDescending(f => __instance.currentlyHeldObjectServer == f) // ... then by held items
                    .ThenByDescending(f => f.isBeingUsed) // ... then by active status
                    .ThenBy(f => f.flashlightTypeID) // ... then by pro, regular, laser
                    .FirstOrDefault();

                // Active lasers in hand are toggling OFF first
                if (targetFlashlight != null)
                {
                    targetFlashlight.UseItemOnClient();
                }
            }
        }

        public static void UpdatePlayerSuitToSavedValue(PlayerControllerB player)
        {
            // Only do this if we are configured to and hosting
            if (Plugin.SavePlayerSuits.Value && StartOfRound.Instance.IsHost && player.playerSteamId != default && StartOfRoundPatch.SteamIDsToSuits.ContainsKey(player.playerSteamId))
            {
                // First, make sure we can find the suit object that matches the saved ID
                int suitID = StartOfRoundPatch.SteamIDsToSuits[player.playerSteamId];
                var matchingSuit = UnityEngine.Object.FindObjectsOfType<UnlockableSuit>().FirstOrDefault(s => s.syncedSuitID?.Value == suitID);
                var unlockable = StartOfRound.Instance.unlockablesList.unlockables.ElementAtOrDefault(suitID);

                // If the suit was found and valid, send a client RPC to everyone who has this suit
                if (matchingSuit != null && unlockable != null && unlockable.suitMaterial != null)
                {
                    player.StartCoroutine(SwitchSuitAfterInitialized(player, matchingSuit, unlockable));
                }
                else
                {
                    Plugin.MLS.LogWarning($"Found a saved suit ID for {player.playerUsername} but it was either not found or not yet unlocked. Suit changing will be skipped.");
                }
            }
        }

        private static IEnumerator SwitchSuitAfterInitialized(PlayerControllerB player, UnlockableSuit suit, UnlockableItem unlockable)
        {
            yield return new WaitUntil(() => suit.suitID == suit.syncedSuitID?.Value);

            if (player.currentSuitID != suit.suitID)
            {
                Plugin.MLS.LogInfo($"Found saved suit ID for player {player.playerUsername} - changing to {unlockable.unlockableName}.");
                suit.SwitchSuitToThis(player);
            }
        }
    }
}