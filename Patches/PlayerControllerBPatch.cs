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
using UnityEngine.Windows;
using static GeneralImprovements.Enums;

namespace GeneralImprovements.Patches
{
    internal static class PlayerControllerBPatch
    {
        private static readonly ProfilerMarker _pm_PlayerHovertip = new ProfilerMarker(ProfilerCategory.Scripts, "GeneralImprovements.PlayerControllerB.SetHoverTipAndCurrentInteractTrigger");
        private static readonly ProfilerMarker _pm_PlayerUpdate = new ProfilerMarker(ProfilerCategory.Scripts, "GeneralImprovements.PlayerControllerB.Update");
        private static readonly ProfilerMarker _pm_PlayerLateUpdate = new ProfilerMarker(ProfilerCategory.Scripts, "GeneralImprovements.PlayerControllerB.LateUpdate");

        public static int CurrentMaxHealth = 100;

        private static Func<bool> _flashlightTogglePressed;
        private static float _originalCursorScale = 0;
        private static float _originalClimbSpeed;

        public static KeyValuePair<int, bool> LastSyncedLifeStatus = new KeyValuePair<int, bool>(100, false); // Health, isDead
        private static readonly float _healthCheckInterval = 1f;
        private static float _healthCheckTimer = 0;

        [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.High)]
        private static void ConnectClientToPlayerObjectPre(PlayerControllerB __instance)
        {
            if (!__instance.IsOwner) return;

            _originalCursorScale = __instance.cursorIcon.transform.localScale.x;
            _originalClimbSpeed = __instance.climbSpeed;

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

            if (Plugin.NumberKeysSwitchItemSlots.Value)
            {
                // Rebind 1 and 2 to use the function keys if they exist
                var emote1 = InputSystem.actions.FindAction("Emote1");
                var emote2 = InputSystem.actions.FindAction("Emote2");
                bool emote1NeedsRebind = emote1?.bindings.FirstOrDefault().path == "<Keyboard>/1";
                bool emote2NeedsRebind = emote2?.bindings.FirstOrDefault().path == "<Keyboard>/2";

                if (emote1NeedsRebind || emote2NeedsRebind)
                {
                    Plugin.MLS.LogMessage("Rebinding emotes to F1/F2.");
                    if (emote1NeedsRebind) emote1.ApplyBindingOverride("<Keyboard>/F1");
                    if (emote2NeedsRebind) emote2.ApplyBindingOverride("<Keyboard>/F2");
                    IngamePlayerSettings.Instance.settings.keyBindings = InputSystem.actions.SaveBindingOverridesAsJson();
			        ES3.Save("Bindings", IngamePlayerSettings.Instance.settings.keyBindings, "LCGeneralSaveData");
                }
            }
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
                        scanNode.headerText = player.playerUsername;
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
                if (GetAllItemSlots(__instance).Values.OfType<KeyItem>().FirstOrDefault() is KeyItem key)
                {
                    key.ItemActivate(true);
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
        [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.DamagePlayerClientRpc))]
        [HarmonyPatch(typeof(PlayerControllerB), "KillPlayerClientRpc")]
        [HarmonyPostfix]
        private static void AfterDamage(PlayerControllerB __instance)
        {
            MonitorsHelper.UpdatePlayerHealthMonitors();
            MonitorsHelper.UpdatePlayersAliveMonitors();

            // If it was the local player taking damage or dying, send a health sync to guarantee values are known
            if (__instance && __instance.IsOwner)
            {
                NetworkHelper.Instance.SyncPlayerLifeStatusServerRpc((int)__instance.playerClientId, __instance.health, __instance.isPlayerDead);
            }
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(ItemTertiaryUse_performed))]
        [HarmonyPrefix]
        private static bool ItemTertiaryUse_performed(PlayerControllerB __instance)
        {
            // Flashlights do not have tertiary uses, so cancel out when this is called in that case
            return !(__instance.currentlyHeldObjectServer is FlashlightItem);
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(SwitchToItemSlot))]
        [HarmonyPostfix]
        private static void SwitchToItemSlot(PlayerControllerB __instance, int slot)
        {
            if ((slot == 50 ? __instance.ItemOnlySlot : __instance.ItemSlots[slot]) is FlashlightItem slotFlashlight)
            {
                // If the player already has an active flashlight (helmet lamp will be on) when picking up a new INACTIVE one, switch to the new one
                if (__instance.IsOwner && !slotFlashlight.isBeingUsed && __instance.helmetLight.enabled && !slotFlashlight.CheckForLaser() && Plugin.OnlyAllowOneActiveFlashlight.Value)
                {
                    var otherFlashlights = GetAllItemSlots(__instance).Values.OfType<FlashlightItem>();

                    foreach (var otherFlashlight in otherFlashlights)
                    {
                        // Find the first active flashlights in our inventory that still has battery, and turn it on
                        if (otherFlashlight != slotFlashlight && otherFlashlight.usingPlayerHelmetLight
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
            var activeLight = GetAllItemSlots(player).Values.OfType<FlashlightItem>()
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
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> FirstEmptyItemSlot(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            // If not configured to pickup in order, or the reserved item slot or advanced company mods exist, skip this patch
            if (!Plugin.PickupInOrder.Value || OtherModHelper.ReservedItemSlotCoreActive || OtherModHelper.AdvancedCompanyActive)
            {
                return codeList;
            }

            FieldInfo curItemSlotField = typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.currentItemSlot));
            if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
            {
                // if (currentItemSlot != 50...
                i => i.IsLdarg(0),
                i => i.LoadsField(curItemSlotField),
                i => i.LoadsConstant(50), i => i.Branches(out _),

                // && ItemSlots[currentItemSlot] == null)
                i => i.IsLdarg(0),
                i => i.LoadsField(typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.ItemSlots))),
                i => i.IsLdarg(0),
                i => i.LoadsField(curItemSlotField),
                i => i.opcode == OpCodes.Ldelem_Ref,
                i => i.opcode == OpCodes.Ldnull,
                i => i.opcode == OpCodes.Call,
                i => i.Branches(out _),

                // num = currentItemSlot
                i => i.IsLdarg(0),
                i => i.LoadsField(curItemSlotField),
                i => i.IsStloc(),
                i => i.Branches(out _)
            }, out var found))
            {
                Plugin.MLS.LogDebug("Patching PlayerControllerB.FirstEmptyItemSlot to help manage inventory.");

                // Remove the code that sets current item slot
                foreach (var code in found)
                {
                    codeList[code.Index].opcode = OpCodes.Nop;
                }
            }
            else
            {
                Plugin.MLS.LogWarning("Unexpected IL Code - Could not patch PlayerControllerB.FirstEmptyItemSlot to help manage inventory!");
            }

            return codeList;
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(GrabObjectClientRpc))]
        [HarmonyPrefix]
        private static void GrabObjectClientRpc(PlayerControllerB __instance, bool grabValidated, NetworkObjectReference grabbedObject)
        {
            // If the local player is about to pick up a two handed item and we are configured to do this, make sure it lands in slot 1
            if (!Plugin.TwoHandedInSlotOne.Value || !grabValidated || !grabbedObject.TryGet(out var networkObject))
            {
                return;
            }

            // Make sure this is a two handed object and we aren't currently processing it
            var grabbableObject = networkObject.gameObject.GetComponentInChildren<GrabbableObject>();
            if (!grabbableObject || !grabbableObject.itemProperties || !grabbableObject.itemProperties.twoHanded)
            {
                return;
            }

            if (__instance.IsOwner)
            {
                Plugin.MLS.LogDebug($"Two handed item being grabbed!");
            }

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

            // Refresh currently held item/animations
            __instance.SwitchToItemSlot(__instance.currentItemSlot, null);
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

            // Update the UI
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
        private static bool OpenQuickMenu()
        {
            if (ShipBuildModeManager.Instance && ShipBuildModeManager.Instance.InBuildMode)
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

            // Do not set grab hovertip if grabbable item is not grabbable
            var getTransformMethod = typeof(Component).GetMethod("get_transform");
            var getPositionMethod = typeof(Transform).GetMethod("get_position");
            if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
            {
                // if (grabbableObject == null) { grabbableObject = hit.collider.gameObject.GetComponent<GrabbableObject>(); }
                i => i.Branches(out _),
                i => i.IsLdarg(0),
                i => i.LoadsField(typeof(PlayerControllerB).GetField("hit", BindingFlags.Instance | BindingFlags.NonPublic), true),
                i => i.Calls(typeof(RaycastHit).GetMethod("get_collider")),
                i => i.Calls(typeof(Component).GetMethod("get_gameObject")),
                null,
                i => i.IsStloc(),

                // if (Physics.Linecast(gameplayCamera.transform.position, grabbableObject.transform.position...))
                i => i.IsLdarg(0),
                i => i.LoadsField(typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.gameplayCamera))),
                i => i.Calls(getTransformMethod),
                i => i.Calls(getPositionMethod),
                i => i.IsLdloc(),
                i => i.Calls(getTransformMethod),
                i => i.Calls(getPositionMethod),
                i => i.LoadsConstant(),
                i => i.LoadsConstant(),
                i => i.Calls(typeof(Physics).GetMethod(nameof(Physics.Linecast), new Type[] { typeof(Vector3), typeof(Vector3), typeof(int), typeof(QueryTriggerInteraction) })),
                i => i.Branches(out _),
                i => i.IsLdarg(0)
            }, out var grabbableCode))
            {
                Plugin.MLS.LogDebug("Patching PlayerControllerB.SetHoverTipAndCurrentInteractionTrigger to remove grab notification when not needed.");

                // First check if item is not grabbable, and if so, branch to newly added no-tip label
                Label newCheckLabel = generator.DefineLabel();
                Label noTipLabel = generator.DefineLabel();
                grabbableCode.Last().Instruction.labels.Add(noTipLabel);
                codeList.InsertRange(grabbableCode[7].Index, new CodeInstruction[]
                {
                    new CodeInstruction(grabbableCode[11].Instruction.opcode).WithLabels(newCheckLabel), // LdLoc grabbableObject
                    new CodeInstruction(OpCodes.Ldfld, typeof(GrabbableObject).GetField(nameof(GrabbableObject.grabbable))),
                    new CodeInstruction(OpCodes.Brfalse_S, noTipLabel)
                });

                // Make sure previous code goes to our check and not past it
                grabbableCode.First().Instruction.operand = newCheckLabel;
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
                        if (d && d.transform.parent && d.transform.parent.GetComponent<MaskedPlayerEnemy>() is MaskedPlayerEnemy mask)
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
        private static void SetHoverTipAndCurrentInteractTrigger(PlayerControllerB __instance)
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
                    __instance.cursorIcon.sprite = __instance.hoveringOverTrigger ? __instance.hoveringOverTrigger.hoverIcon : __instance.cursorIcon.sprite;

                    if (__instance.cursorIcon.sprite == AssetBundleHelper.Reticle)
                    {
                        __instance.cursorIcon.enabled = false;
                    }
                }
            }

            if (Plugin.AddHealthRechargeStation.Value && ObjectHelper.MedStation && __instance.hoveringOverTrigger && __instance.hoveringOverTrigger.transform.parent == ObjectHelper.MedStation.transform)
            {
                bool shouldBeInteractable = __instance.health < CurrentMaxHealth;
                if (__instance.hoveringOverTrigger.interactable != shouldBeInteractable)
                {
                    __instance.hoveringOverTrigger.interactable = shouldBeInteractable;
                }
            }

            ProfilerHelper.EndProfilingSafe(_pm_PlayerHovertip);
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(ShowNameBillboard))]
        [HarmonyPrefix]
        private static bool ShowNameBillboard()
        {
            // Do not show player names if we are hiding them, unless we are orbiting
            return !(Plugin.HidePlayerNames.Value && !(StartOfRound.Instance && StartOfRound.Instance.inShipPhase));
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
            for (int i = 0; i < player.ItemSlots.Length; i++)
            {
                // If the checked item is held or not scrap (if we only drop scrap), skip this one
                // Only drop non held items, or held scrap if we only drop scrap
                bool isHeldItem = player.currentlyHeldObjectServer != null && player.ItemSlots[i] == player.currentlyHeldObjectServer;
                if (!isHeldItem || (player.ItemSlots[i] != null && player.ItemSlots[i].itemProperties.isScrap && onlyDropScrap))
                {
                    player.DropHeldItem(player.ItemSlots[i], true, false);
                }
            }

            var allItems = GetAllItemSlots(player).Values.ToArray();
            player.carryWeight = 1 + allItems.Sum(i => (i && i.itemProperties ? i.itemProperties.weight : 1) - 1);
        }

        public static Dictionary<int, GrabbableObject> GetAllItemSlots(PlayerControllerB player)
        {
            var allSlots = new Dictionary<int, GrabbableObject>();

            if (player != null)
            {
                for (int i = 0; i < player.ItemSlots.Length; i++)
                {
                    allSlots[i] = player.ItemSlots[i];
                }

                allSlots[50] = player.ItemOnlySlot;
            }

            return allSlots;
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(Update))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Update_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.SprintOnLadders.Value == eLadderSprintOption.Allow)
            {
                // Look for the player body position affected by climbing
                if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.thisPlayerBody))),
                    i => i.Calls(typeof(Component).GetMethod("get_transform")),
                    i => i.opcode == OpCodes.Dup,
                    i => i.Calls(typeof(Transform).GetMethod("get_position")),
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.thisPlayerBody))),
                    i => i.Calls(typeof(Transform).GetMethod("get_up")),
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.moveInputVector)), true),
                    i => i.LoadsField(typeof(Vector2).GetField(nameof(Vector2.y))),
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.climbSpeed))),
                    i => i.opcode == OpCodes.Mul,
                    i => i.Calls(typeof(Time).GetMethod("get_deltaTime")),
                    i => i.opcode == OpCodes.Mul,
                    i => i.Calls(typeof(Vector3).GetMethod("op_Multiply", new[] { typeof(Vector3), typeof(float) })),
                    i => i.Calls(typeof(Vector3).GetMethod("op_Addition", new[] { typeof(Vector3), typeof(Vector3) })),
                    i => i.Calls(typeof(Transform).GetMethod("set_position"))
                }, out var found))
                {
                    Plugin.MLS.LogDebug("Patching PlayerControllerB.Update to utilize sprint speed while climbing ladders.");
                    codeList.InsertRange(found.First().Index,
                        new CodeInstruction[] {
                            new CodeInstruction(OpCodes.Ldarg_0), // Load player onto stack
                            Transpilers.EmitDelegate<Action<PlayerControllerB>>(player =>
                            {
                                // Update the player's sprint speed
                                if (player.isSprinting) player.climbSpeed = Mathf.Lerp(player.climbSpeed, _originalClimbSpeed * 2.25f, Time.deltaTime * 1f);
                                else player.climbSpeed = Mathf.Lerp(player.climbSpeed, _originalClimbSpeed, Time.deltaTime * 10f);
                            })
                        }
                    );
                }
                else
                {
                    Plugin.MLS.LogWarning("Could not find IL code necessary to patch ladder sprinting in PlayerControllerB.Update!");
                }
            }

            return codeList;
        }

        [HarmonyPatch(typeof(PlayerControllerB), nameof(Update))]
        [HarmonyPostfix]
        private static void Update(PlayerControllerB __instance)
        {
            ProfilerHelper.BeginProfilingSafe(_pm_PlayerUpdate);

            if (__instance && __instance.IsOwner)
            {
                // Keep max health value up to date
                if (CurrentMaxHealth < __instance.health)
                {
                    Plugin.MLS.LogInfo($"Storing local player's max health as {__instance.health}");
                    CurrentMaxHealth = __instance.health;
                }

                // Periodically check if our last synced health value or life status is different from our current, and send it across if so
                _healthCheckTimer += Time.deltaTime;
                if (_healthCheckTimer >= _healthCheckInterval)
                {
                    _healthCheckTimer = 0;

                    if (LastSyncedLifeStatus.Key != __instance.health || LastSyncedLifeStatus.Value != __instance.isPlayerDead)
                    {
                        NetworkHelper.Instance.SyncPlayerLifeStatusServerRpc((int)__instance.playerClientId, __instance.health, __instance.isPlayerDead);
                    }
                }

                if (Plugin.FlashlightToggleShortcut.Value != eValidKeys.None && !__instance.inTerminalMenu && !__instance.isTypingChat && __instance.isPlayerControlled)
                {
                    if (_flashlightTogglePressed())
                    {
                        // Get the nearest flashlight with charge, whether it's held or in the inventory
                        var allItems = GetAllItemSlots(__instance);
                        var targetFlashlight = allItems.OfType<FlashlightItem>().Where(f => !f.insertedBattery.empty) // All charged flashlight items
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

                if (Plugin.NumberKeysSwitchItemSlots.Value)
                {
                    // If we CAN switch, allow switching
                    bool disableSwitch = __instance.inTerminalMenu || !__instance.isPlayerControlled || __instance.timeSinceSwitchingSlots < 0.3f || __instance.isGrabbingObjectAnimation
                        || __instance.quickMenuManager.isMenuOpen || __instance.inSpecialInteractAnimation || __instance.throwingObject || __instance.isTypingChat || __instance.twoHanded
                        || __instance.activatingItem || ((__instance.jetpackControls || __instance.disablingJetpackControls) && __instance.currentlyHeldObjectServer?.itemProperties.itemId  == 13);
                    
                    if (!disableSwitch)
                    {
                        for (int i = (int)Key.Digit1; i <= (int)Key.Digit0; i++)
                        {
                            if (Keyboard.current?[(Key)i]?.wasPressedThisFrame == true)
                            {
                                int index = i - (int)Key.Digit1;
                                if (index < __instance.ItemSlots.Length)
                                {
                                    __instance.SwitchToItemSlot(index);
                                    __instance.SwitchToSlotServerRpc(index);
                                }
                            }
                        }
                    }
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

        [HarmonyPatch(typeof(PlayerControllerB), nameof(LateUpdate))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> LateUpdate_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeList = instructions.ToList();

            if (Plugin.SprintOnLadders.Value == eLadderSprintOption.NoDrain)
            {
                Label? elseLabel = null;

                // Look for the player body position affected by climbing
                if (codeList.TryFindInstructions(new Func<CodeInstruction, bool>[]
                {
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.isSprinting))),
                    i => i.Branches(out elseLabel),
                    i => i.IsLdarg(0),
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.sprintMeter))),
                    i => i.Calls(typeof(Time).GetMethod("get_deltaTime")),
                    i => i.IsLdarg(0),
                    i => i.LoadsField(typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.sprintTime)))
                }, out var found))
                {
                    Plugin.MLS.LogDebug("Patching PlayerControllerB.LateUpdate to prevent sprint drain while climbing ladders.");
                    codeList.InsertRange(found.ElementAt(3).Index,
                        new CodeInstruction[] {
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Ldfld, typeof(PlayerControllerB).GetField(nameof(PlayerControllerB.isClimbingLadder))),
                            new CodeInstruction(OpCodes.Brtrue_S, elseLabel)
                        }
                    );
                }
                else
                {
                    Plugin.MLS.LogWarning("Could not find IL code necessary to patch ladder sprint drain in PlayerControllerB.LateUpdate!");
                }
            }

            return codeList;
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