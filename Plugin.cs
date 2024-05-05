using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GeneralImprovements.Patches;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using static GeneralImprovements.Plugin.Enums;
using static GeneralImprovements.Utilities.MonitorsHelper;

namespace GeneralImprovements
{
    [BepInPlugin(Metadata.GUID, Metadata.PLUGIN_NAME, Metadata.VERSION)]
    [BepInDependency(OtherModHelper.TwoRadarCamsGUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(OtherModHelper.MimicsGUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(OtherModHelper.WeatherTweaksGUID, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public class Enums
        {
            public enum eAutoLaunchOptions { NONE, ONLINE, LAN }

            public enum eShowHiddenMoons { Never, AfterDiscovery, Always }

            public enum eMonitorNames
            {
                None,
                ProfitQuota,
                Deadline,
                ShipScrap,
                ScrapLeft,
                Time,
                Weather,
                FancyWeather,
                Sales,
                Credits,
                DoorPower,
                TotalDays,
                TotalQuotas,
                TotalDeaths,
                DaysSinceDeath,
                InternalCam,
                ExternalCam
            }

            // Shortcut key helpers
            public enum eValidKeys
            {
                None, Space, Enter, Tab, Backquote, Quote, Semicolon, Comma, Period, Slash, Backslash, LeftBracket, RightBracket, Minus, Equals,
                A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
                Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9, Digit0,
                LeftShift, RightShift, LeftAlt, AltGr, LeftCtrl, RightCtrl, LeftWindows, RightCommand,
                ContextMenu, Escape, LeftArrow, RightArrow, UpArrow, DownArrow, Backspace,
                PageDown, PageUp, Home, End, Insert, Delete, CapsLock, NumLock, PrintScreen, ScrollLock, Pause,
                NumpadEnter, NumpadDivide, NumpadMultiply, NumpadPlus, NumpadMinus, NumpadPeriod, NumpadEquals, Numpad0, Numpad1, Numpad2, Numpad3, Numpad4, Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,
                F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
                MouseLeft, MouseRight, MouseMiddle, MouseBackButton, MouseForwardButton
            };
            public static ButtonControl GetMouseButtonMapping(eValidKeys mouseButton)
            {
                return mouseButton switch
                {
                    eValidKeys.MouseLeft => Mouse.current.leftButton,
                    eValidKeys.MouseRight => Mouse.current.rightButton,
                    eValidKeys.MouseMiddle => Mouse.current.middleButton,
                    eValidKeys.MouseBackButton => Mouse.current.backButton,
                    eValidKeys.MouseForwardButton => Mouse.current.forwardButton,
                    _ => throw new NotImplementedException()
                };
            }

            public enum eItemsToKeep { None, Held, NonScrap, All };
        }

        public static ManualLogSource MLS { get; private set; }

        private const string ExtraMonitorsSection = "ExtraMonitors";
        public static ConfigEntry<bool> UseBetterMonitors { get; private set; }
        public static ConfigEntry<bool> SyncMonitorsFromOtherHost { get; private set; }
        public static ConfigEntry<bool> ShowBlueMonitorBackground { get; private set; }
        public static ConfigEntry<string> MonitorBackgroundColor { get; private set; }
        public static Color MonitorBackgroundColorVal { get; private set; }
        public static ConfigEntry<string> MonitorTextColor { get; private set; }
        public static Color MonitorTextColorVal { get; private set; }
        public static ConfigEntry<bool> ShowBackgroundOnAllScreens { get; private set; }
        public static ConfigEntry<eMonitorNames>[] ShipMonitorAssignments { get; private set; }
        public static ConfigEntry<bool> SyncExtraMonitorsPower { get; private set; }
        public static ConfigEntry<bool> CenterAlignMonitorText { get; private set; }
        public static ConfigEntry<int> ShipInternalCamSizeMultiplier { get; private set; }
        public static ConfigEntry<int> ShipInternalCamFPS { get; private set; }
        public static ConfigEntry<int> ShipExternalCamSizeMultiplier { get; private set; }
        public static ConfigEntry<int> ShipExternalCamFPS { get; private set; }
        public static ConfigEntry<bool> AlwaysRenderMonitors { get; private set; }

        private const string FixesSection = "Fixes";
        public static ConfigEntry<bool> FixInternalFireExits { get; private set; }
        public static ConfigEntry<bool> FixItemsFallingThrough { get; private set; }
        public static ConfigEntry<bool> FixItemsLoadingSameRotation { get; private set; }
        public static ConfigEntry<bool> AllowLookDownMore { get; private set; }
        public static ConfigEntry<int> DropShipItemLimit { get; private set; }
        public static ConfigEntry<int> SellCounterItemLimit { get; private set; }

        private const string GameLaunchSection = "GameLaunch";
        public static ConfigEntry<bool> SkipStartupScreen { get; private set; }
        public static ConfigEntry<eAutoLaunchOptions> AutoSelectLaunchMode { get; private set; }
        public static ConfigEntry<bool> AlwaysShowNews { get; private set; }
        public static ConfigEntry<bool> AllowPreGameLeverPullAsClient { get; private set; }
        public static ConfigEntry<int> MenuMusicVolume { get; private set; }

        private const string InventorySection = "Inventory";
        public static ConfigEntry<bool> PickupInOrder { get; private set; }
        public static ConfigEntry<bool> RearrangeOnDrop { get; private set; }
        public static ConfigEntry<bool> TwoHandedInSlotOne { get; private set; }
        public static ConfigEntry<float> ScrollDelay { get; private set; }

        private const string MechanicsSection = "Mechanics";
        public static ConfigEntry<int> StartingMoneyPerPlayer { get; private set; }
        public static int StartingMoneyPerPlayerVal => Math.Clamp(StartingMoneyPerPlayer.Value, -1, 10000);
        public static ConfigEntry<int> MinimumStartingMoney { get; private set; }
        public static int MinimumStartingMoneyVal => Math.Clamp(MinimumStartingMoney.Value, StartingMoneyPerPlayerVal, 10000);
        public static ConfigEntry<bool> AllowQuotaRollover { get; private set; }
        public static ConfigEntry<bool> AllowOvertimeBonus { get; private set; }
        public static ConfigEntry<bool> AddHealthRechargeStation { get; private set; }
        public static ConfigEntry<bool> ScanCommandUsesExactAmount { get; private set; }
        public static ConfigEntry<bool> UnlockDoorsFromInventory { get; private set; }
        public static ConfigEntry<bool> KeysHaveInfiniteUses { get; private set; }
        public static ConfigEntry<bool> DestroyKeysAfterOrbiting { get; private set; }
        public static ConfigEntry<bool> SavePlayerSuits { get; private set; }
        public static ConfigEntry<bool> MaskedLookLikePlayers { get; private set; }

        private const string ScannerSection = "Scanner";
        public static ConfigEntry<bool> FixPersonalScanner { get; private set; }
        public static ConfigEntry<bool> ScanPlayers { get; private set; }
        public static ConfigEntry<bool> ScanHeldPlayerItems { get; private set; }
        public static ConfigEntry<bool> ShowDropshipOnScanner { get; private set; }
        public static ConfigEntry<bool> ShowDoorsOnScanner { get; private set; }

        private const string ShipSection = "Ship";
        public static ConfigEntry<bool> HideClipboardAndStickyNote { get; private set; }
        public static ConfigEntry<bool> HideShipCabinetDoors { get; private set; }
        public static ConfigEntry<int> SnapObjectsByDegrees { get; private set; }
        public static ConfigEntry<eValidKeys> FreeRotateKey { get; private set; }
        public static ConfigEntry<eValidKeys> CounterClockwiseKey { get; private set; }
        public static ConfigEntry<bool> ShipPlaceablesCollide { get; private set; }
        public static ConfigEntry<bool> SaveFurnitureState { get; private set; }
        public static ConfigEntry<bool> ShipMapCamDueNorth { get; private set; }
        public static ConfigEntry<bool> SpeakerPlaysIntroVoice { get; private set; }
        public static ConfigEntry<bool> LightSwitchScanNode { get; private set; }
        public static ConfigEntry<bool> DisableShipCamPostProcessing { get; private set; }

        private const string TeleportersSection = "Teleporters";
        public static ConfigEntry<int> RegularTeleporterCooldown { get; private set; }
        public static ConfigEntry<int> InverseTeleporterCooldown { get; private set; }
        public static ConfigEntry<eItemsToKeep> KeepItemsDuringTeleport { get; private set; }
        public static ConfigEntry<eItemsToKeep> KeepItemsDuringInverse { get; private set; }

        private const string TerminalSection = "Terminal";
        public static ConfigEntry<int> TerminalHistoryItemCount { get; private set; }
        public static ConfigEntry<bool> TerminalFastCamSwitch { get; private set; }
        public static ConfigEntry<bool> LockCameraAtTerminal { get; private set; }
        public static ConfigEntry<bool> ShowMoonPricesInTerminal { get; private set; }
        public static ConfigEntry<bool> ShowBlanksDuringViewMonitor { get; private set; }
        public static ConfigEntry<eShowHiddenMoons> ShowHiddenMoonsInCatalog { get; private set; }

        private const string ToolsSection = "Tools";
        public static ConfigEntry<bool> OnlyAllowOneActiveFlashlight { get; private set; }
        public static ConfigEntry<bool> TreatLasersAsFlashlights { get; private set; }
        public static ConfigEntry<eValidKeys> FlashlightToggleShortcut { get; private set; }
        public static ConfigEntry<string> ScannableTools { get; private set; }
        public static List<Type> ScannableToolVals { get; private set; } = new List<Type>();
        public static ConfigEntry<bool> ToolsDoNotAttractLightning { get; private set; }
        public static ConfigEntry<bool> AutoChargeOnOrbit { get; private set; }

        private const string UISection = "UI";
        public static ConfigEntry<bool> ShowUIReticle { get; private set; }
        public static ConfigEntry<bool> HideEmptySubtextOfScanNodes { get; private set; }
        public static ConfigEntry<bool> ShowHitPoints { get; private set; }
        public static ConfigEntry<bool> ShowLightningWarnings { get; private set; }
        public static ConfigEntry<bool> HidePlayerNames { get; private set; }
        public static ConfigEntry<bool> TwentyFourHourClock { get; private set; }
        public static ConfigEntry<bool> AlwaysShowClock { get; private set; }
        public static ConfigEntry<bool> DisplayKgInsteadOfLb { get; private set; }

        private void Awake()
        {
            MLS = Logger;

            // Load info about any external mods first
            OtherModHelper.Initialize();
            AssetBundleHelper.Initialize();

            BindConfigs();
            MigrateOldConfigValues();
            MLS.LogDebug("Configuration Initialized.");

            Harmony.CreateAndPatchAll(typeof(AutoParentToShipPatch));
            MLS.LogDebug("AutoParentToShip patched.");

            Harmony.CreateAndPatchAll(typeof(DepositItemsDeskPatch));
            MLS.LogDebug("DepositItemsDesk patched.");

            Harmony.CreateAndPatchAll(typeof(DoorLockPatch));
            MLS.LogDebug("DoorLock patched.");

            Harmony.CreateAndPatchAll(typeof(EntranceTeleportPatch));
            MLS.LogDebug("EntranceTeleport patched.");

            if (!OtherModHelper.FlashlightFixActive)
            {
                Harmony.CreateAndPatchAll(typeof(FlashlightItemPatch));
                MLS.LogDebug("FlashlightItem patched.");
            }
            else
            {
                MLS.LogWarning("Outdated version of FlashlightFix detected - please update your mods.");
            }

            Harmony.CreateAndPatchAll(typeof(GameNetworkManagerPatch));
            MLS.LogDebug("GameNetworkManager patched.");

            Harmony.CreateAndPatchAll(typeof(GrabbableObjectsPatch));
            MLS.LogDebug("GrabbableObjects patched.");

            Harmony.CreateAndPatchAll(typeof(HangarShipDoorPatch));
            MLS.LogDebug("HangarShipDoor patched.");

            Harmony.CreateAndPatchAll(typeof(HUDManagerPatch));
            MLS.LogDebug("HUDManager patched.");

            Harmony.CreateAndPatchAll(typeof(ItemDropshipPatch));
            MLS.LogDebug("ItemDropship patched.");

            Harmony.CreateAndPatchAll(typeof(LandminePatch));
            MLS.LogDebug("Landmine patched.");

            Harmony.CreateAndPatchAll(typeof(ManualCameraRendererPatch));
            MLS.LogDebug("ManualCameraRenderer patched.");

            Harmony.CreateAndPatchAll(typeof(MaskedPlayerEnemyPatch));
            MLS.LogDebug("MaskedPlayerEnemy patched.");

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

            Harmony.CreateAndPatchAll(typeof(SprayPaintItemPatch));
            MLS.LogDebug("SprayPaintItem patched.");

            Harmony.CreateAndPatchAll(typeof(StartMatchLeverPatch));
            MLS.LogDebug("StartMatchLever patched.");

            Harmony.CreateAndPatchAll(typeof(StartOfRoundPatch));
            MLS.LogDebug("StartOfRound patched.");

            Harmony.CreateAndPatchAll(typeof(StormyWeatherPatch));
            MLS.LogDebug("StormyWeather patched.");

            Harmony.CreateAndPatchAll(typeof(TerminalAccessibleObjectPatch));
            MLS.LogDebug("TerminalAccessibleObject patched.");

            Harmony.CreateAndPatchAll(typeof(TerminalPatch));
            MLS.LogDebug("Terminal patched.");

            Harmony.CreateAndPatchAll(typeof(TimeOfDayPatch));
            MLS.LogDebug("TimeOfDay patched.");

            Harmony.CreateAndPatchAll(typeof(UnlockableSuitPatch));
            MLS.LogDebug("UnlockableSuit patched.");

            GameNetworkManagerPatch.PatchNetcode();

            MLS.LogInfo($"{Metadata.PLUGIN_NAME} v{Metadata.VERSION} fully loaded.");
        }

        public void BindConfigs()
        {
            var validSnapRotations = Enumerable.Range(0, 360 / 15).Select(n => n * 15).Where(n => n == 0 || 360 % n == 0).ToArray();

            var validToolTypes = new List<Type> { typeof(BoomboxItem), typeof(ExtensionLadderItem), typeof(FlashlightItem), typeof(JetpackItem), typeof(LockPicker), typeof(RadarBoosterItem), typeof(KnifeItem),
                                                typeof(Shovel), typeof(SprayPaintItem), typeof(StunGrenadeItem), typeof(TetraChemicalItem), typeof(WalkieTalkie), typeof(PatcherTool) };
            var validToolStrings = string.Join(", ", new[] { "All" }.Concat(validToolTypes.Select(t => t.Name)));

            // Extra monitors
            UseBetterMonitors = Config.Bind(ExtraMonitorsSection, nameof(UseBetterMonitors), false, "If set to true, uses 12 fully customizable and integrated monitors instead of the 8 vanilla ones with overlays. If true, 1-6 are top, 7-12 are bottom, and 13-14 are the big ones beside the terminal. Otherwise, 1-4 are the top, and 5-8 are on the bottom.");
            SyncMonitorsFromOtherHost = Config.Bind(ExtraMonitorsSection, nameof(SyncMonitorsFromOtherHost), false, "If set to true, all monitor placements will be synced from the host when joining a game, if the host is also using this mod. Settings such as color, FPS, etc will not be synced.");
            ShowBlueMonitorBackground = Config.Bind(ExtraMonitorsSection, nameof(ShowBlueMonitorBackground), true, "If set to true and NOT using UseBetterMonitors, keeps the vanilla blue backgrounds on the extra monitors. Set to false to hide.");
            MonitorBackgroundColor = Config.Bind(ExtraMonitorsSection, nameof(MonitorBackgroundColor), "160959", "The hex color code of what the backgrounds of the monitors should be. A recommended value close to black is 050505.");
            MonitorTextColor = Config.Bind(ExtraMonitorsSection, nameof(MonitorTextColor), "00FF2C", "The hex color code of what the text on the monitors should be.");
            ShowBackgroundOnAllScreens = Config.Bind(ExtraMonitorsSection, nameof(ShowBackgroundOnAllScreens), false, "If set to true, will show the MonitorBackgroundColor on ALL monitors when they are on, not just used ones.");
            ShipMonitorAssignments = new ConfigEntry<eMonitorNames>[MonitorCount];
            for (int i = 0; i < ShipMonitorAssignments.Length; i++)
            {
                eMonitorNames defaultVal = i switch { 4 => eMonitorNames.ProfitQuota, 5 => eMonitorNames.Deadline, 11 => eMonitorNames.InternalCam, 14 => eMonitorNames.ExternalCam, _ => eMonitorNames.None };
                ShipMonitorAssignments[i] = Config.Bind(ExtraMonitorsSection, $"ShipMonitor{i + 1}", defaultVal, $"What to display on the ship monitor at position {i + 1}, if anything.");
            }
            SyncExtraMonitorsPower = Config.Bind(ExtraMonitorsSection, nameof(SyncExtraMonitorsPower), true, "If set to true, The smaller monitors above the map screen will turn off and on when the map screen power is toggled.");
            CenterAlignMonitorText = Config.Bind(ExtraMonitorsSection, nameof(CenterAlignMonitorText), true, "If set to true, all small monitors in the ship will have their text center aligned, instead of left.");
            ShipInternalCamSizeMultiplier = Config.Bind(ExtraMonitorsSection, nameof(ShipInternalCamSizeMultiplier), 1, new ConfigDescription($"How many times to double the internal ship cam's resolution.", new AcceptableValueRange<int>(1, 5)));
            ShipInternalCamFPS = Config.Bind(ExtraMonitorsSection, nameof(ShipInternalCamFPS), 0, new ConfigDescription($"Limits the FPS of the internal ship cam for performance. 0 = Unrestricted.", new AcceptableValueRange<int>(0, 30)));
            ShipExternalCamSizeMultiplier = Config.Bind(ExtraMonitorsSection, nameof(ShipExternalCamSizeMultiplier), 1, new ConfigDescription($"How many times to double the external ship cam's resolution.", new AcceptableValueRange<int>(1, 5)));
            ShipExternalCamFPS = Config.Bind(ExtraMonitorsSection, nameof(ShipExternalCamFPS), 0, new ConfigDescription($"Limits the FPS of the external ship cam for performance. 0 = Unrestricted.", new AcceptableValueRange<int>(0, 30)));
            AlwaysRenderMonitors = Config.Bind(ExtraMonitorsSection, nameof(AlwaysRenderMonitors), false, $"If using better monitors and set to true, text-based monitors will render updates even when you are not in the ship. May slightly affect performance.");

            // Fixes
            FixInternalFireExits = Config.Bind(FixesSection, nameof(FixInternalFireExits), true, "If set to true, the player will face the interior of the facility when entering through a fire entrance.");
            FixItemsFallingThrough = Config.Bind(FixesSection, nameof(FixItemsFallingThrough), true, "Fixes items falling through furniture on the ship when loading the game.");
            FixItemsLoadingSameRotation = Config.Bind(FixesSection, nameof(FixItemsLoadingSameRotation), true, "Fixes items all facing the same way when loading a save file. Now they will store their rotations as well.");
            AllowLookDownMore = Config.Bind(FixesSection, nameof(AllowLookDownMore), true, "If set to true, you will be able to look down at a steeper angle than vanilla.");
            DropShipItemLimit = Config.Bind(FixesSection, nameof(DropShipItemLimit), 24, new ConfigDescription("Sets the max amount of items a single dropship delivery will allow. Vanilla = 12.", new AcceptableValueRange<int>(12, 100)));
            SellCounterItemLimit = Config.Bind(FixesSection, nameof(SellCounterItemLimit), 24, new ConfigDescription("Sets the max amount of items the company selling counter will hold at one time. Vanilla = 12.", new AcceptableValueRange<int>(12, 100)));

            // Game Launch
            SkipStartupScreen = Config.Bind(GameLaunchSection, nameof(SkipStartupScreen), true, "Skips the main menu loading screen bootup animation.");
            AutoSelectLaunchMode = Config.Bind(GameLaunchSection, nameof(AutoSelectLaunchMode), eAutoLaunchOptions.NONE, "If set to 'ONLINE' or 'LAN', will automatically launch the correct mode, saving you from having to click the menu option when the game loads.");
            AlwaysShowNews = Config.Bind(GameLaunchSection, nameof(AlwaysShowNews), false, "If set to true, will always display the news popup when starting the game.");
            AllowPreGameLeverPullAsClient = Config.Bind(GameLaunchSection, nameof(AllowPreGameLeverPullAsClient), true, "If set to true, you will be able to pull the ship lever to start the game as a connected player.");
            MenuMusicVolume = Config.Bind(GameLaunchSection, nameof(MenuMusicVolume), 100, new ConfigDescription("Controls the volume of the menu music, from 0-100.", new AcceptableValueRange<int>(0, 100)));

            // Inventory
            PickupInOrder = Config.Bind(InventorySection, nameof(PickupInOrder), false, "When picking up items, will always put them in left - right order.");
            RearrangeOnDrop = Config.Bind(InventorySection, nameof(RearrangeOnDrop), false, "When dropping items, will rearrange other inventory items to ensure slots are filled left - right.");
            TwoHandedInSlotOne = Config.Bind(InventorySection, nameof(TwoHandedInSlotOne), false, $"When picking up a two handed item, it will always place it in slot 1 and shift things to the right if needed. Makes selling quicker when paired with RearrangeOnDrop.");
            ScrollDelay = Config.Bind(InventorySection, nameof(ScrollDelay), 0.1f, new ConfigDescription("The minimum time you must wait to scroll to another item in your inventory. Vanilla: 0.3.", new AcceptableValueRange<float>(0.05f, 0.3f)));

            // Mechanics
            StartingMoneyPerPlayer = Config.Bind(MechanicsSection, nameof(StartingMoneyPerPlayer), -1, "[Host Only] How much starting money the group gets per player. Set to -1 to disable. Adjusts money as players join and leave, until the game starts. Internally capped at 10k.");
            MinimumStartingMoney = Config.Bind(MechanicsSection, nameof(MinimumStartingMoney), 30, "[Host Only] When paired with StartingMoneyPerPlayer, will ensure a group always starts with at least this much money. Must be at least the value of StartingMoneyPerPlayer. Internally capped at 10k.");
            AllowQuotaRollover = Config.Bind(MechanicsSection, nameof(AllowQuotaRollover), false, "[Host Required] If set to true, will keep the surplus money remaining after selling things to the company, and roll it over to the next quota. If clients do not set this, they will see visual desyncs.");
            AllowOvertimeBonus = Config.Bind(MechanicsSection, nameof(AllowOvertimeBonus), true, "[Host Only] If set to false, will prevent the vanilla overtime bonus from being applied after the end of a quota.");
            AddHealthRechargeStation = Config.Bind(MechanicsSection, nameof(AddHealthRechargeStation), false, "[Host Only] If set to true, a medical charging station will be above the ship's battery charger, and can be used to heal to full. **WARNING:** THIS WILL PREVENT YOU FROM CONNECTING TO ANY OTHER PLAYERS THAT DO NOT ALSO HAVE IT ENABLED!");
            ScanCommandUsesExactAmount = Config.Bind(MechanicsSection, nameof(ScanCommandUsesExactAmount), false, "If set to true, the terminal's scan command (and ScrapLeft monitor) will use display the exact scrap value remaining instead of approximate.");
            UnlockDoorsFromInventory = Config.Bind(MechanicsSection, nameof(UnlockDoorsFromInventory), false, "If set to true, keys in your inventory do not have to be held when unlocking facility doors.");
            KeysHaveInfiniteUses = Config.Bind(MechanicsSection, nameof(KeysHaveInfiniteUses), false, "If set to true, keys will not despawn when they are used.");
            DestroyKeysAfterOrbiting = Config.Bind(MechanicsSection, nameof(DestroyKeysAfterOrbiting), false, "If set to true, all keys in YOUR inventory (and IF HOSTING, the ship) will be destroyed after orbiting. Works well to nerf KeysHaveInfiniteUses. Players who do not have this enabled will keep keys currently in their inventory.");
            SavePlayerSuits = Config.Bind(MechanicsSection, nameof(SavePlayerSuits), true, "If set to true, the host will keep track of every player's last used suit, and will persist between loads and ship resets for each save file. Only works in Online mode.");
            MaskedLookLikePlayers = Config.Bind(MechanicsSection, nameof(MaskedLookLikePlayers), false, "If set to true, masked entities will NOT be wearing masks, and spawned masks will look identical to a random real player of their choice. Works with MoreCompany cosmetics.");

            // Scanner
            FixPersonalScanner = Config.Bind(ScannerSection, nameof(FixPersonalScanner), false, "If set to true, will tweak the behavior of the scan action and more reliably ping items closer to you, and the ship/main entrance.");
            ScanPlayers = Config.Bind(ScannerSection, nameof(ScanPlayers), false, "If set to true, players (and sneaky masked entities) will be scannable.");
            ScanHeldPlayerItems = Config.Bind(ScannerSection, nameof(ScanHeldPlayerItems), false, "If this and FixPersonalScanner are set to true, the scanner will also ping items in other players' hands.");
            ShowDropshipOnScanner = Config.Bind(ScannerSection, nameof(ShowDropshipOnScanner), false, "If set to true, the item drop ship will be scannable.");
            ShowDoorsOnScanner = Config.Bind(ScannerSection, nameof(ShowDoorsOnScanner), false, "If set to true, all fire entrances and facility exits will be scannable. Compatible with mimics mod (they show up as an exit as well).");

            // Ship
            HideClipboardAndStickyNote = Config.Bind(ShipSection, nameof(HideClipboardAndStickyNote), false, "If set to true, the game will not show the clipboard or sticky note when the game loads.");
            HideShipCabinetDoors = Config.Bind(ShipSection, nameof(HideShipCabinetDoors), false, "If set to true, the storage shelves in the ship will not have doors.");
            SnapObjectsByDegrees = Config.Bind(ShipSection, nameof(SnapObjectsByDegrees), 45, new ConfigDescription("Build mode will switch to snap turning (press instead of hold) by this many degrees at a time. Setting it to 0 uses vanilla behavior.", new AcceptableValueList<int>(validSnapRotations)));
            FreeRotateKey = Config.Bind(ShipSection, nameof(FreeRotateKey), eValidKeys.LeftAlt, "If SnapObjectsByDegrees > 0, configures which modifer key activates free rotation.");
            CounterClockwiseKey = Config.Bind(ShipSection, nameof(CounterClockwiseKey), eValidKeys.LeftShift, "If SnapObjectsByDegrees > 0, configures which modifier key spins it CCW.");
            ShipPlaceablesCollide = Config.Bind(ShipSection, nameof(ShipPlaceablesCollide), true, "If set to true, placeable ship objects will check for collisions with each other during placement.");
            SaveFurnitureState = Config.Bind(ShipSection, nameof(SaveFurnitureState), true, "If set to true, all default ship furniture positions and storage states will not be reset after being fired.");
            ShipMapCamDueNorth = Config.Bind(ShipSection, nameof(ShipMapCamDueNorth), false, "If set to true, the ship's map camera will rotate so that it faces north evenly, instead of showing everything at an angle.");
            SpeakerPlaysIntroVoice = Config.Bind(ShipSection, nameof(SpeakerPlaysIntroVoice), true, "If set to true, the ship's speaker will play the introductory welcome audio on the first day.");
            LightSwitchScanNode = Config.Bind(ShipSection, nameof(LightSwitchScanNode), true, "If set to true, the light switch will have a scan node attached.");
            DisableShipCamPostProcessing = Config.Bind(ShipSection, nameof(DisableShipCamPostProcessing), false, "If set to true, the internal and external ship cameras will no longer use post processing. This may improve performance with higher resolution camera settings.");

            // Teleporters
            RegularTeleporterCooldown = Config.Bind(TeleportersSection, nameof(RegularTeleporterCooldown), 10, new ConfigDescription("How many seconds to wait in between button presses for the REGULAR teleporter. Vanilla = 10. If using the vanilla value, the teleporter code will not be modified.", new AcceptableValueRange<int>(1, 300)));
            InverseTeleporterCooldown = Config.Bind(TeleportersSection, nameof(InverseTeleporterCooldown), 210, new ConfigDescription("How many seconds to wait in between button presses for the INVERSE teleporter. Vanilla = 210. If using the vanilla value, the teleporter code will not be modified.", new AcceptableValueRange<int>(1, 300)));
            KeepItemsDuringTeleport = Config.Bind(TeleportersSection, nameof(KeepItemsDuringTeleport), eItemsToKeep.None, "Whether to keep Held, Non Scrap, or All items in inventory when using the regular teleporter. *WARNING:* THIS WILL CAUSE INVENTORY DESYNCS IF OTHER PLAYERS DO NOT SHARE YOUR SETTING!");
            KeepItemsDuringInverse = Config.Bind(TeleportersSection, nameof(KeepItemsDuringInverse), eItemsToKeep.None, "Whether to keep Held, Non Scrap, or All items in inventory when using the inverse teleporter. *WARNING:* THIS WILL CAUSE INVENTORY DESYNCS IF OTHER PLAYERS DO NOT SHARE YOUR SETTING!");

            // Terminal
            TerminalHistoryItemCount = Config.Bind(TerminalSection, nameof(TerminalHistoryItemCount), 20, new ConfigDescription("How many items to keep in your terminal's command history. Previous terminal commands may be navigated by using the up/down arrow keys.", new AcceptableValueRange<int>(0, 100)));
            TerminalFastCamSwitch = Config.Bind(TerminalSection, nameof(TerminalFastCamSwitch), true, "If set to true, will allow use of the left/right arrow keys to quickly cycle through radar cameras while using the terminal.");
            LockCameraAtTerminal = Config.Bind(TerminalSection, nameof(LockCameraAtTerminal), true, "If set to true, the camera will no longer move around when moving your mouse/controller while at the terminal.");
            ShowMoonPricesInTerminal = Config.Bind(TerminalSection, nameof(ShowMoonPricesInTerminal), false, "If set to true, the moons will also display the cost to fly to them next to their name and weather.");
            ShowBlanksDuringViewMonitor = Config.Bind(TerminalSection, nameof(ShowBlanksDuringViewMonitor), true, "If set to true, typing commands while View Monitor is active requires you to scroll down to see the result.");
            ShowHiddenMoonsInCatalog = Config.Bind(TerminalSection, nameof(ShowHiddenMoonsInCatalog), eShowHiddenMoons.AfterDiscovery, "When to show any hidden moons in the terminal's moon catalog. AfterDiscovery is per save file.");

            // Tools
            OnlyAllowOneActiveFlashlight = Config.Bind(ToolsSection, nameof(OnlyAllowOneActiveFlashlight), true, "When turning on any flashlight, will turn off any others in your inventory that are still active.");
            TreatLasersAsFlashlights = Config.Bind(ToolsSection, nameof(TreatLasersAsFlashlights), false, "If set to true, laser pointers will be like flashlights and automatically toggle off and on when switching to them, etc.");
            FlashlightToggleShortcut = Config.Bind(ToolsSection, nameof(FlashlightToggleShortcut), eValidKeys.None, $"A shortcut key to allow toggling a flashlight at any time.");

            ScannableTools = Config.Bind(ToolsSection, nameof(ScannableTools), string.Empty, $"A comma separated list of which tools, if any, should be scannable. Accepted values: {validToolStrings}");
            ToolsDoNotAttractLightning = Config.Bind(ToolsSection, nameof(ToolsDoNotAttractLightning), false, "[Host Only] If set to true, all useful tools (ladders, jetpacks, keys, radar boosters, shovels & signs, tzp inhalant, knives, and zap guns) will no longer attract lighning.");
            AutoChargeOnOrbit = Config.Bind(ToolsSection, nameof(AutoChargeOnOrbit), false, "If set to true, all owned* battery-using items will be automatically charged every time the ship goes into orbit. *You are considered to 'own' an item if you are the last person to have held it.");

            // UI
            HideEmptySubtextOfScanNodes = Config.Bind(UISection, nameof(HideEmptySubtextOfScanNodes), true, "If set to true, will hide the subtext section of scannables that do not have subtext or scrap value.");
            ShowUIReticle = Config.Bind(UISection, nameof(ShowUIReticle), false, "If set to true, the HUD will display a small dot so you can see exactly where you are pointing at all times.");
            ShowHitPoints = Config.Bind(UISection, nameof(ShowHitPoints), true, "If set to true, the HUD will display your current remaining hitpoints.");
            ShowLightningWarnings = Config.Bind(UISection, nameof(ShowLightningWarnings), true, "If set to true, the inventory slots will flash electrically when an item in the slot is being targeted by lightning.");
            HidePlayerNames = Config.Bind(UISection, nameof(HidePlayerNames), false, "If set to true, player names will no longer show above players.");
            TwentyFourHourClock = Config.Bind(UISection, nameof(TwentyFourHourClock), false, "If set to true, the clock will be 24 hours instead of 12.");
            AlwaysShowClock = Config.Bind(UISection, nameof(AlwaysShowClock), false, "If set to true, the clock will always be displayed on the HUD when landed on a moon.");
            DisplayKgInsteadOfLb = Config.Bind(UISection, nameof(DisplayKgInsteadOfLb), false, "If set to true, your carry weight will be converted from lb to kg.");

            // Sanitize where needed
            string backgroundHex = Regex.Match(MonitorBackgroundColor.Value, "([a-fA-F0-9]{6})").Groups[1].Value.ToUpper();
            string textHex = Regex.Match(MonitorTextColor.Value, "([a-fA-F0-9]{6})").Groups[1].Value.ToUpper();
            if (backgroundHex.Length != 6) MLS.LogWarning("Invalid hex code used for monitor background color! Reverting to default.");
            if (textHex.Length != 6) MLS.LogWarning("Invalid hex code used for monitor text color! Reverting to default.");
            MonitorBackgroundColor.Value = backgroundHex.Length == 6 ? backgroundHex : MonitorBackgroundColor.DefaultValue.ToString();
            MonitorBackgroundColorVal = HexToColor(MonitorBackgroundColor.Value);
            MonitorTextColor.Value = textHex.Length == 6 ? textHex : MonitorTextColor.DefaultValue.ToString();
            MonitorTextColorVal = HexToColor(MonitorTextColor.Value);

            if (MinimumStartingMoney.Value < StartingMoneyPerPlayer.Value)
            {
                MinimumStartingMoney.Value = StartingMoneyPerPlayer.Value;
            }

            var validGrabbables = new List<string>();
            string[] specifiedScannables = ScannableTools.Value.Replace(" ", "").Split(',');
            if (specifiedScannables.Any(s => s.ToUpper() == "ALL"))
            {
                ScannableToolVals = validToolTypes;
            }
            else
            {
                foreach (string scannableTool in ScannableTools.Value.Replace(" ", "").Split(','))
                {
                    var grabbable = validToolTypes.FirstOrDefault(g => g.Name.ToUpper().Contains(scannableTool.ToUpper()));
                    if (grabbable == null)
                    {
                        MLS.LogWarning($"Could not find item type {scannableTool} when trying to add a scan node! Check your spelling and the acceptable values.");
                        continue;
                    }
                    else
                    {
                        ScannableToolVals.Add(grabbable);
                        validGrabbables.Add(scannableTool);
                    }
                }

                ScannableTools.Value = string.Join(',', validGrabbables.ToArray());
            }
        }

        private static Color HexToColor(string hex)
        {
            float r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber) / 255f;
            float g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber) / 255f;
            float b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber) / 255f;

            return new Color(r, g, b);
        }

        public static Dictionary<string, List<InternalConfigDef>> GetConfigSectionsAndItems(string filePath)
        {
            string[] configLines = File.ReadAllLines(filePath);
            var ourEntries = new Dictionary<string, List<InternalConfigDef>> { { string.Empty, new List<InternalConfigDef>() } };
            string curSection = string.Empty;
            string curDescription = string.Empty;
            string curDefaultValue = string.Empty;

            foreach (string line in configLines.Select(l => l.Trim()))
            {
                if (line.StartsWith('#'))
                {
                    if (line.StartsWith("##")) curDescription = line.Substring(2);
                    else
                    {
                        var match = Regex.Match(line, "# Default value: (.+)");
                        if (match.Groups[1].Success)
                        {
                            curDefaultValue = match.Groups[1].Value;
                        }
                    }
                    continue;
                }

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    curSection = line.Substring(1, line.Length - 2);
                    ourEntries.TryAdd(curSection, new List<InternalConfigDef>());
                    continue;
                }

                string[] entry = line.Split('=');
                if (entry.Length == 2)
                {
                    ourEntries[curSection].Add(new InternalConfigDef(curSection, curDescription, entry[0].Trim(), entry[1].Trim(), curDefaultValue));

                    curDescription = string.Empty;
                    curDefaultValue = string.Empty;
                }
            }

            return ourEntries;
        }

        private void MigrateOldConfigValues()
        {
            try
            {
                // Manually read sections and entries since the config classes don't provide a way to see unused values
                var ourEntries = GetConfigSectionsAndItems(Config.ConfigFilePath);

                // Find definitions that are not part of our current keys and remove migrate if possible
                bool foundOrphans = false;
                foreach (string section in ourEntries.Keys)
                {
                    foreach (var entry in ourEntries[section])
                    {
                        if (!Config.Any(k => k.Key.Section == section && entry.Name == k.Value.Definition.Key))
                        {
                            MigrateSpecificValue(entry);
                            foundOrphans = true;
                        }
                    }
                }

                // Manually clear the private orphans
                if (foundOrphans)
                {
                    var orphans = (Dictionary<ConfigDefinition, string>)Config.GetType().GetProperty("OrphanedEntries", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(Config);
                    if (orphans != null)
                    {
                        orphans.Clear();
                        Config.Save();
                    }
                    else
                    {
                        MLS.LogWarning("Could not clear orphaned config values when migrating old config values.");
                    }
                }
            }
            catch (Exception ex)
            {
                MLS.LogError($"Error encountered while migrating old config values! This will not affect gameplay, but please verify your config file to ensure the settings are as you expect.\n\n{ex}");
            }
        }

        private void MigrateSpecificValue(InternalConfigDef entry)
        {
            MLS.LogMessage($"Found unused config value: {entry.Name}. Migrating and removing if possible...");

            Action<eMonitorNames> convertMonitor = s =>
            {
                if (int.TryParse(entry.Value, out var num) && num >= 1 && num <= ShipMonitorAssignments.Length)
                {
                    MLS.LogInfo($"Migrating {s} to monitor position {num}.");
                    ShipMonitorAssignments[num - 1].Value = s;
                }
            };

            switch (entry.Name)
            {
                // Ship monitors
                case "ShipProfitQuotaMonitorNum": convertMonitor(eMonitorNames.ProfitQuota); break;
                case "ShipDeadlineMonitorNum": convertMonitor(eMonitorNames.Deadline); break;
                case "ShipTotalMonitorNum": convertMonitor(eMonitorNames.ShipScrap); break;
                case "ShipTimeMonitorNum": convertMonitor(eMonitorNames.Time); break;
                case "ShipWeatherMonitorNum": convertMonitor(eMonitorNames.Weather); break;
                case "FancyWeatherMonitor":
                    var existingWeatherMonitor = ShipMonitorAssignments.FirstOrDefault(a => a.Value == eMonitorNames.Weather);
                    if (entry.Value.ToUpper() == "TRUE" && existingWeatherMonitor != null)
                    {
                        // Unless manually modified, the new weather monitor would have been migrated at this point so it's safe to overwrite it here
                        MLS.LogInfo($"Migrating fancy weather to override weather monitor.");
                        existingWeatherMonitor.Value = eMonitorNames.FancyWeather;
                    }
                    break;
                case "ShipSalesMonitorNum": convertMonitor(eMonitorNames.Sales); break;
                case "ShipInternalCamMonitorNum": convertMonitor(eMonitorNames.InternalCam); break;
                case "ShipExternalCamMonitorNum": convertMonitor(eMonitorNames.ExternalCam); break;

                // A couple things under fixes section moving to scanner section
                case "FixPersonalScanner": FixPersonalScanner.Value = entry.Value.ToUpper() == "TRUE"; break;
                case "ScanHeldPlayerItems": ScanHeldPlayerItems.Value = entry.Value.ToUpper() == "TRUE"; break;

                default:
                    MLS.LogDebug("No matching migration");
                    break;
            }
        }

        public class InternalConfigDef
        {
            public readonly string Section;
            public readonly string Description;
            public readonly string Name;
            public readonly string Value;
            public readonly string DefaultValue;

            public InternalConfigDef(string section, string description, string name, string value, string defaultValue)
            {
                Section = section;
                Description = description;
                Name = name;
                Value = value;
                DefaultValue = defaultValue;
            }
        }
    }
}