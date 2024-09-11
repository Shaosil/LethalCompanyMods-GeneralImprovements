using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GameNetcodeStuff;
using GeneralImprovements.Utilities;
using HarmonyLib;
using TMPro;
using Unity.Netcode;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using static GeneralImprovements.Enums;

namespace GeneralImprovements.Patches
{
    internal static class PlayerControllerBPatch
    {
        private static readonly ProfilerMarker _pm_PlayerHovertip = new ProfilerMarker(ProfilerCategory.Scripts, "GeneralImprovements.PlayerControllerB.SetHoverTipAndCurrentInteractTrigger");
        private static readonly ProfilerMarker _pm_PlayerUpdate = new ProfilerMarker(ProfilerCategory.Scripts, "GeneralImprovements.PlayerControllerB.Update");
        private static readonly ProfilerMarker _pm_PlayerLateUpdate = new ProfilerMarker(ProfilerCategory.Scripts, "GeneralImprovements.PlayerControllerB.LateUpdate");

        public static Dictionary<PlayerControllerB, int> PlayerMaxHealthValues = new Dictionary<PlayerControllerB, int>();

        private static Func<bool> _flashlightTogglePressed;
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
        private static void ConnectClientToPlayerObjectPost(PlayerControllerB __instance)
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

            MonitorsHelper.UpdatePlayerHealthMonitors();
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

            MonitorsHelper.UpdatePlayerHealthMonitors();
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

        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer))]
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayer))]
        [HarmonyPatch(typeof(PlayerControllerB), "KillPlayerClientRpc")]
        [HarmonyPostfix]
        private static void AfterDamage()
        {
            MonitorsHelper.UpdatePlayerHealthMonitors();
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
            if (__instance.timeSinceSwitchingSlots < 0.1f)
            {
                __instance.timeSinceSwitchingSlots = 0.3f - desiredDelay;
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
            Label? outsideEqualityLabel = null;

            // Do not set grab hovertip if grabbable item is not grabbable
            if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
            {
                i => i.Branches(out _),
                i => i.IsLdloc(),
                i => i.Is(OpCodes.Ldstr, "InteractTrigger"),
                i => i.Calls(typeof(string).GetMethod("op_Equality")),
                i => i.Branches(out _),
                i => i.Branches(out outsideEqualityLabel),

                i => i.IsLdarg(0), // Index 6
                i => i.Calls(typeof(PlayerControllerB).GetMethod("FirstEmptyItemSlot", BindingFlags.Instance | BindingFlags.NonPublic)),
                i => i.LoadsConstant(-1),
                i => i.Branches(out _), // Index 9
                i => i.IsLdarg(0),
                i => i.LoadsField(typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.cursorTip))),
                i => i.Is(OpCodes.Ldstr, "Inventory full!"),
                i => i.Calls(typeof(TMP_Text).GetMethod("set_text")),
                i => i.Branches(out _), // Index 14

                i => i.IsLdarg(0), // Index 15
                i => i.LoadsField(typeof(PlayerControllerB).GetField("hit", BindingFlags.Instance | BindingFlags.NonPublic), true),
                i => i.Calls(typeof(RaycastHit).GetMethod("get_collider")),
                i => i.Calls(typeof(Component).GetMethod("get_gameObject")),
                i => i.Calls(typeof(GameObject).GetMethod(nameof(GameObject.GetComponent), 1, Type.EmptyTypes).MakeGenericMethod(typeof(GrabbableObject))),
                i => i.opcode == OpCodes.Stloc_2, // Index 20

                // 20 lines to a vehicle linecast we don't care about
                null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,

                i => i.Calls(typeof(GameNetworkManager).GetMethod("get_Instance")) // Index 41
            }, out var grabbableCode))
            {
                // Create a label on the get_Instance call, and branch to that instead
                var newLabel = generator.DefineLabel();
                codeList[grabbableCode.Last().Index].labels.Add(newLabel);
                grabbableCode[9].Instruction.operand = newLabel;

                // Create a label on the GetComponent variable, and make sure it is branched to
                var componentLabel = generator.DefineLabel();
                grabbableCode[15].Instruction.labels.Add(componentLabel);
                grabbableCode[0].Instruction.operand = componentLabel;

                // Create a label on the FirstEmptyItemSlot call so we can still jump to that if needed
                var firstEmptySlotLabel = generator.DefineLabel();
                grabbableCode[6].Instruction.labels.Add(firstEmptySlotLabel);

                // Move the GetComponent variable to be above the FirstEmptyItemSlot if statement
                codeList.RemoveRange(grabbableCode[15].Index, 6);
                codeList.InsertRange(grabbableCode[6].Index, grabbableCode.Skip(15).Take(6).Select(f => f.Instruction));

                // Add an if statement to break out of this entire section if the component is not grabbable and has no custom hover tip
                codeList.InsertRange(grabbableCode[6].Index + 6, new[]
                {
                    // If it's grabbable, skip over the next if statement
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldfld, typeof(GrabbableObject).GetField(nameof(GrabbableObject.grabbable))),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Beq_S, firstEmptySlotLabel),

                    // Otherwise if it has no custom hovertip either, skip this whole section
                    new CodeInstruction(OpCodes.Ldloc_2),
                    new CodeInstruction(OpCodes.Ldfld, typeof(GrabbableObject).GetField(nameof(GrabbableObject.customGrabTooltip))),
                    new CodeInstruction(OpCodes.Call, typeof(string).GetMethod(nameof(string.IsNullOrEmpty))),
                    new CodeInstruction(OpCodes.Brtrue_S, outsideEqualityLabel)
                });

                Plugin.MLS.LogDebug("Patching PlayerControllerB.SetHoverTipAndCurrentInteractionTrigger to remove grab notification when not needed.");
            }
            else
            {
                Plugin.MLS.LogWarning("Unexpected IL code - Could not patch PlayerControllerB.SetHoverTipAndCurrentInteractionTrigger to remove the grab notification!");
            }

            // Check for masked entity raycast hit when doing players in order to show their billboards if needed
            if (Plugin.MaskedEntitiesShowPlayerNames.Value)
            {
                Label? outsideRaycastLabel = null;
                if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(PlayerControllerB).GetField("interactRay", BindingFlags.NonPublic | BindingFlags.Instance)),
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(PlayerControllerB).GetField("hit", BindingFlags.NonPublic | BindingFlags.Instance), true),

                    i => i.LoadsConstant(5f),
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(PlayerControllerB).GetField("playerMask", BindingFlags.Instance | BindingFlags.NonPublic)),
                    i => i.Calls(typeof(Physics).GetMethod(nameof(Physics.Raycast), new[] { typeof(Ray), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int) }))
                }, out var raycastCode)

                    && codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.Branches(out outsideRaycastLabel),
                    i => i.IsLdloc(),
                    i => i.Calls(typeof(PlayerControllerB).GetMethod(nameof(PlayerControllerB.ShowNameBillboard)))
                }, out var billboardCode))
                {
                    // Include enemy layer in our raycast
                    codeList[raycastCode[5].Index] = new CodeInstruction(OpCodes.Nop);
                    codeList[raycastCode[6].Index] = new CodeInstruction(OpCodes.Ldc_I4, LayerMask.GetMask("Player", "Enemies"));

                    // Nop out the loading of the interact ray param
                    codeList[raycastCode[0].Index] = new CodeInstruction(OpCodes.Nop);
                    codeList[raycastCode[1].Index] = new CodeInstruction(OpCodes.Nop);

                    // Replace the interactRay parameter with a new Ray with a starting position 0.5 units forward
                    codeList.InsertRange(raycastCode[2].Index, new[]
                    {
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.gameplayCamera))),
                        new CodeInstruction(OpCodes.Callvirt, typeof(Component).GetMethod("get_transform")),
                        new CodeInstruction(OpCodes.Callvirt, typeof(Transform).GetMethod("get_position")),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.gameplayCamera))),
                        new CodeInstruction(OpCodes.Callvirt, typeof(Component).GetMethod("get_transform")),
                        new CodeInstruction(OpCodes.Callvirt, typeof(Transform).GetMethod("get_forward")),
                        new CodeInstruction(OpCodes.Ldc_R4, 0.5f),
                        new CodeInstruction(OpCodes.Call, typeof(Vector3).GetMethod("op_Multiply", new[] { typeof(Vector3), typeof(float) })),
                        new CodeInstruction(OpCodes.Call, typeof(Vector3).GetMethod("op_Addition", new[] { typeof(Vector3), typeof(Vector3) })),
                        new CodeInstruction(OpCodes.Ldarg_0),
                        new CodeInstruction(OpCodes.Ldfld, typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.gameplayCamera))),
                        new CodeInstruction(OpCodes.Callvirt, typeof(Component).GetMethod("get_transform")),
                        new CodeInstruction(OpCodes.Callvirt, typeof(Transform).GetMethod("get_forward")),
                        new CodeInstruction(OpCodes.Newobj, typeof(Ray).GetConstructor(new Type[] { typeof(Vector3), typeof(Vector3) }))
                    });

                    // Update the raycast call to collide with triggers so it can detect masked entities' colliders (they only have triggers)
                    codeList.Insert(raycastCode.Last().Index + 16, new CodeInstruction(OpCodes.Ldc_I4, (int)QueryTriggerInteraction.Collide));
                    raycastCode.Last().Instruction.operand = typeof(Physics).GetMethod(nameof(Physics.Raycast), new[] { typeof(Ray), typeof(RaycastHit).MakeByRefType(), typeof(float), typeof(int), typeof(QueryTriggerInteraction) });

                    // Create a new label and emit a delegate to check if we can get a MaskedPlayerEnemy component
                    var newIfLabel = generator.DefineLabel();
                    var newDelegate = Transpilers.EmitDelegate<Action<GameObject>>(d =>
                    {
                        var mask = d?.transform.parent?.GetComponent<MaskedPlayerEnemy>();
                        if (mask != null)
                        {
                            MaskedPlayerEnemyPatch.ShowNameBillboard(mask);
                        }
                    });

                    // Branch to our new if statement from the first check, instead of outside the entire block
                    billboardCode[0].Instruction.operand = newIfLabel;

                    // Insert new block
                    codeList.InsertRange(billboardCode.Last().Index + 18, new[]
                    {
                        new CodeInstruction(OpCodes.Br, outsideRaycastLabel), // To make this into an else, jump completely out after the original ShowNameBillboard
                        new CodeInstruction(OpCodes.Ldarg_0).WithLabels(newIfLabel),
                        new CodeInstruction(OpCodes.Ldflda, typeof(PlayerControllerB).GetField("hit", BindingFlags.Instance | BindingFlags.NonPublic)),
                        new CodeInstruction(OpCodes.Call, typeof(RaycastHit).GetMethod("get_collider")),
                        new CodeInstruction(OpCodes.Callvirt, typeof(Component).GetMethod("get_gameObject")),
                        newDelegate
                    });

                    Plugin.MLS.LogDebug("Patching PlayerControllerB.SetHoverTipAndCurrentInteractionTrigger to add mask name billboards.");
                }
                else
                {
                    Plugin.MLS.LogWarning("Unexpected IL code - Could not patch PlayerControllerB.SetHoverTipAndCurrentInteractionTrigger to add mask name billboards!");
                }
            }

            return codeList;
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(SetHoverTipAndCurrentInteractTrigger))]
        [HarmonyPostfix]
        private static void SetHoverTipAndCurrentInteractTrigger(PlayerControllerB __instance, Ray ___interactRay, RaycastHit ___hit)
        {
            ProfilerHelper.BeginProfilingSafe(_pm_PlayerHovertip);

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

            ProfilerHelper.EndProfilingSafe(_pm_PlayerHovertip);
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(ShowNameBillboard))]
        [HarmonyPrefix]
        private static bool ShowNameBillboard()
        {
            // Do not show player names if we are hiding them, unless we are orbiting
            return !(Plugin.HidePlayerNames.Value && !(StartOfRound.Instance?.inShipPhase ?? true));
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
            ProfilerHelper.BeginProfilingSafe(_pm_PlayerUpdate);

            // Keep max health values up to date
            if (!PlayerMaxHealthValues.ContainsKey(__instance) || PlayerMaxHealthValues[__instance] < __instance.health)
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

            ProfilerHelper.EndProfilingSafe(_pm_PlayerUpdate);
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(LateUpdate))]
        [HarmonyPostfix]
        private static void LateUpdate(PlayerControllerB __instance)
        {
            ProfilerHelper.BeginProfilingSafe(_pm_PlayerLateUpdate);

            // If we are invulnerable, never let the sprint meter drain
            if (StartOfRound.Instance != null && !StartOfRound.Instance.allowLocalPlayerDeath && __instance != null && __instance.sprintMeter < 1)
            {
                __instance.sprintMeter = 1;
            }

            ProfilerHelper.EndProfilingSafe(_pm_PlayerLateUpdate);
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