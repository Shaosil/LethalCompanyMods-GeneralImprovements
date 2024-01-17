using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GeneralImprovements.OtherMods;
using GeneralImprovements.Patches;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System;
using System.Linq;
using UnityEngine.InputSystem;

namespace GeneralImprovements
{
    [BepInPlugin(Metadata.GUID, Metadata.PLUGIN_NAME, Metadata.VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource MLS { get; private set; }

        private const string FixesSection = "Fixes";
        public static ConfigEntry<bool> FixInternalFireExits { get; private set; }
        public static ConfigEntry<bool> FixItemsFallingThrough { get; private set; }
        public static ConfigEntry<bool> ShowShipTotalBelowDeadline { get; private set; }
        public static ConfigEntry<bool> FixPersonalScanner { get; private set; }

        private const string GeneralSection = "General";
        public static ConfigEntry<bool> SkipStartupScreen { get; private set; }
        public static ConfigEntry<string> AutoSelectLaunchMode { get; private set; }
        public static ConfigEntry<bool> PickupInOrder { get; private set; }
        public static ConfigEntry<bool> RearrangeOnDrop { get; private set; }
        public static ConfigEntry<bool> TwoHandedInSlotOne { get; private set; }
        public static ConfigEntry<float> ScrollDelay { get; private set; }
        public static ConfigEntry<int> TerminalHistoryItemCount { get; private set; }
        public static ConfigEntry<bool> HideClipboardAndStickyNote { get; private set; }
        public static ConfigEntry<bool> ShowShipTimeMonitor { get; private set; }
        public static ConfigEntry<bool> ShowShipWeatherMonitor { get; private set; }
        public static ConfigEntry<bool> FancyWeatherMonitor { get; private set; }
        public static ConfigEntry<bool> ShowShipSalesMonitor { get; private set; }
        public static ConfigEntry<bool> SyncLittleScreensPower { get; private set; }
        public static ConfigEntry<bool> AddTargetReticle { get; private set; }

        private const string TweaksSection = "Tweaks";
        public static ConfigEntry<int> StartingMoneyPerPlayer { get; private set; }
        public static int StartingMoneyPerPlayerVal => Math.Clamp(StartingMoneyPerPlayer.Value, -1, 1000);
        public static ConfigEntry<int> MinimumStartingMoney { get; private set; }
        public static int MinimumStartingMoneyVal => Math.Clamp(MinimumStartingMoney.Value, StartingMoneyPerPlayerVal, 1000);
        public static ConfigEntry<int> SnapObjectsByDegrees { get; private set; }
        public static ConfigEntry<string> FreeRotateKey { get; private set; }
        public static ConfigEntry<string> CounterClockwiseKey { get; private set; }
        public static ConfigEntry<bool> ShipMapCamDueNorth { get; private set; }
        public static ConfigEntry<bool> ToolsDoNotAttractLightning { get; private set; }
        public static ConfigEntry<int> RegularTeleporterCooldown { get; private set; }
        public static ConfigEntry<int> InverseTeleporterCooldown { get; private set; }
        public static ConfigEntry<bool> AllowQuotaRollover { get; private set; }
        public static ConfigEntry<bool> AllowHealthRecharge { get; private set; }

        private void Awake()
        {
            string validKeys = string.Join(", ", Enum.GetValues(typeof(Key)).Cast<int>().Where(e => e < (int)Key.OEM1).Select(e => Enum.GetName(typeof(Key), e))
                .Concat(Enum.GetValues(typeof(ShipBuildModeManagerPatch.MouseButton)).Cast<int>().Select(e => Enum.GetName(typeof(ShipBuildModeManagerPatch.MouseButton), e))));

            MLS = Logger;

            // Fixes
            FixInternalFireExits = Config.Bind(FixesSection, nameof(FixInternalFireExits), true, "If set to true, the player will face the interior of the facility when entering through a fire entrance.");
            FixItemsFallingThrough = Config.Bind(FixesSection, nameof(FixItemsFallingThrough), true, "Fixes items falling through furniture on the ship when loading the game.");
            FixPersonalScanner = Config.Bind(FixesSection, nameof(FixPersonalScanner), false, "If set to true, will tweak the behavior of the scan action and more reliably ping items closer to you, and the ship/main entrance.");

            // General
            SkipStartupScreen = Config.Bind(GeneralSection, nameof(SkipStartupScreen), true, "Skips the main menu loading screen bootup animation.");
            AutoSelectLaunchMode = Config.Bind(GeneralSection, nameof(AutoSelectLaunchMode), string.Empty, "If set to 'ONLINE' or 'LAN', will automatically launch the correct mode, saving you from having to click the menu option when the game loads.");
            PickupInOrder = Config.Bind(GeneralSection, nameof(PickupInOrder), true, "When picking up items, will always put them in left - right order.");
            RearrangeOnDrop = Config.Bind(GeneralSection, nameof(RearrangeOnDrop), true, "When dropping items, will rearrange other inventory items to ensure slots are filled left - right.");
            TwoHandedInSlotOne = Config.Bind(GeneralSection, nameof(TwoHandedInSlotOne), true, $"When picking up a two handed item, it will always place it in slot 1 and shift things to the right if needed. Makes selling quicker when paired with {nameof(RearrangeOnDrop)}.");
            ScrollDelay = Config.Bind(GeneralSection, nameof(ScrollDelay), 0.1f, "The minimum time you must wait to scroll to another item in your inventory. Ignores values outside of 0.05 - 0.3. Vanilla: 0.3.");
            TerminalHistoryItemCount = Config.Bind(GeneralSection, nameof(TerminalHistoryItemCount), 20, "How many items to keep in your terminal's command history. Ignores values outside of 0 - 100. Previous terminal commands may be navigated by using the up/down arrow keys.");
            HideClipboardAndStickyNote = Config.Bind(GeneralSection, nameof(HideClipboardAndStickyNote), false, "If set to true, the game will not show the clipboard or sticky note when the game loads.");
            ShowShipTotalBelowDeadline = Config.Bind(GeneralSection, nameof(ShowShipTotalBelowDeadline), false, "Constantly displays the sum of all scrap value on the ship underneath the deadline text.");
            ShowShipTimeMonitor = Config.Bind(GeneralSection, nameof(ShowShipTimeMonitor), false, "If set to true, The ship will show the time on one of the unused monitors above the landing lever.");
            ShowShipWeatherMonitor = Config.Bind(GeneralSection, nameof(ShowShipWeatherMonitor), false, "If set to true, The ship will show the weather on one of the unused monitors above the landing lever.");
            FancyWeatherMonitor = Config.Bind(GeneralSection, nameof(FancyWeatherMonitor), true, "If set to true and paired with ShowShipWeatherMonitor, the weather monitor will display ASCII art instead of text descriptions.");
            ShowShipSalesMonitor = Config.Bind(GeneralSection, nameof(ShowShipSalesMonitor), false, "If set to true, The ship will show basic sales info on one of the unused monitors above the landing lever.");
            SyncLittleScreensPower = Config.Bind(GeneralSection, nameof(SyncLittleScreensPower), true, "If set to true, The smaller monitors above the map screen will turn off and on when the map screen power is toggled.");
            AddTargetReticle = Config.Bind(GeneralSection, nameof(AddTargetReticle), false, "If set to true, the HUD will display a small dot so you can see exactly where you are pointing at all times.");

            // Tweaks
            StartingMoneyPerPlayer = Config.Bind(TweaksSection, nameof(StartingMoneyPerPlayer), -1, "How much starting money the group gets per player. Set to -1 to disable. Ignores values outside of -1 - 1000. Adjusts money as players join and leave, until the game starts.");
            MinimumStartingMoney = Config.Bind(TweaksSection, nameof(MinimumStartingMoney), 30, "When paired with StartingMoneyPerPlayer, will ensure a group always starts with at least this much money. Ignores values outside of StartingMoneyPerPlayer - 1000.");
            SnapObjectsByDegrees = Config.Bind(TweaksSection, nameof(SnapObjectsByDegrees), 45, "Build mode will switch to snap turning (press instead of hold) by this many degrees at a time. Setting it to 0 uses vanilla behavior. Must be an interval of 15 and go evenly into 360.");
            FreeRotateKey = Config.Bind(TweaksSection, nameof(FreeRotateKey), Key.LeftAlt.ToString(), $"If SnapObjectsByDegrees > 0, configures which modifer key activates free rotation. Valid values: {validKeys}");
            CounterClockwiseKey = Config.Bind(TweaksSection, nameof(CounterClockwiseKey), Key.LeftShift.ToString(), $"If SnapObjectsByDegrees > 0, configures which modifier key spins it CCW. Valid values: {validKeys}");
            ShipMapCamDueNorth = Config.Bind(TweaksSection, nameof(ShipMapCamDueNorth), false, "If set to true, the ship's map camera will rotate so that it faces north evenly, instead of showing everything at an angle.");
            ToolsDoNotAttractLightning = Config.Bind(TweaksSection, nameof(ToolsDoNotAttractLightning), false, "If set to true, all useful tools (jetpacks, keys, radar boosters, shovels & signs, tzp inhalant, and zap guns) will no longer attract lighning.");
            RegularTeleporterCooldown = Config.Bind(TweaksSection, nameof(RegularTeleporterCooldown), 10, "How many seconds to wait in between button presses for the REGULAR teleporter. Vanilla = 10. Ignores values outside of 0 - 300.");
            InverseTeleporterCooldown = Config.Bind(TweaksSection, nameof(InverseTeleporterCooldown), 10, "How many seconds to wait in between button presses for the INVERSE teleporter. Vanilla = 210. Ignores values outside of 0 - 300.");
            AllowQuotaRollover = Config.Bind(TweaksSection, nameof(AllowQuotaRollover), false, "If set to true, will keep the surplus money remaining after selling things to the company, and roll it over to the next quota.");
            AllowHealthRecharge = Config.Bind(TweaksSection, nameof(AllowHealthRecharge), false, "If set to true, a medical charging station will be above the ship's battery charger, and can be used to heal to full.");

            MLS.LogDebug("Configuration Initialized.");

            Harmony.CreateAndPatchAll(GetType().Assembly);

            Harmony.CreateAndPatchAll(typeof(DepositItemsDeskPatch));
            MLS.LogDebug("DepositItemsDesk patched.");

            Harmony.CreateAndPatchAll(typeof(EntranceTeleportPatch));
            MLS.LogDebug("EntranceTeleport patched.");

            Harmony.CreateAndPatchAll(typeof(GameNetworkManagerPatch));
            MLS.LogDebug("GameNetworkManager patched.");

            Harmony.CreateAndPatchAll(typeof(GrabbableObjectsPatch));
            MLS.LogDebug("GrabbableObjects patched.");

            Harmony.CreateAndPatchAll(typeof(HUDManagerPatch));
            MLS.LogDebug("HUDManager patched.");

            Harmony.CreateAndPatchAll(typeof(ManualCameraRendererPatch));
            MLS.LogDebug("ManualCameraRenderer patched.");

            Harmony.CreateAndPatchAll(typeof(MenuPatches));
            MLS.LogDebug("Menus patched.");

            Harmony.CreateAndPatchAll(typeof(PlayerControllerBPatch));
            MLS.LogDebug("PlayerControllerB patched.");

            Harmony.CreateAndPatchAll(typeof(RoundManagerPatch));
            MLS.LogDebug("RoundManager patched.");

            Harmony.CreateAndPatchAll(typeof(ShipBuildModeManagerPatch));
            MLS.LogDebug("ShipBuildModeManager patched.");

            Harmony.CreateAndPatchAll(typeof(ShipTeleporterPatch));
            MLS.LogDebug("ShipTeleporter patched.");

            Harmony.CreateAndPatchAll(typeof(StartOfRoundPatch));
            MLS.LogDebug("StartOfRound patched.");

            Harmony.CreateAndPatchAll(typeof(TerminalAccessibleObjectPatch));
            MLS.LogDebug("TerminalAccessibleObject patched.");

            Harmony.CreateAndPatchAll(typeof(TerminalPatch));
            MLS.LogDebug("Terminal patched.");

            Harmony.CreateAndPatchAll(typeof(TimeOfDayPatch));
            MLS.LogDebug("TimeOfDay patched.");

            // Load info about any external mods
            ReservedItemSlotCoreHelper.Initialize();
            AdvancedCompanyHelper.Initialize();
            AssetBundleHelper.Initialize();

            MLS.LogInfo($"{Metadata.PLUGIN_NAME} v{Metadata.VERSION} fully loaded.");
        }
    }
}