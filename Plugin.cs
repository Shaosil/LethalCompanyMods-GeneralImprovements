using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GeneralImprovements.Patches;
using GeneralImprovements.Patches.Other;
using GeneralImprovements.Utilities;
using HarmonyLib;
using UnityEngine;
using static GeneralImprovements.Enums;

namespace GeneralImprovements
{
    [BepInPlugin(Metadata.GUID, Metadata.PLUGIN_NAME, Metadata.VERSION)]
    [BepInDependency(OtherModHelper.BuyRateSettingsGUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(OtherModHelper.CodeRebirthGUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(OtherModHelper.MimicsGUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(OtherModHelper.TwoRadarCamsGUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(OtherModHelper.WeatherRegistryGUID, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource MLS { get; private set; }

        private const string EnemiesSection = "Enemies";
        public static ConfigEntry<bool> MaskedPlayersAppearAliveOnMonitors { get; private set; }
        public static ConfigEntry<eMaskedEntityCopyLook> MaskedEntitiesCopyPlayerLooks { get; private set; }
        public static ConfigEntry<bool> MaskedEntitiesReachTowardsPlayer { get; private set; }
        public static ConfigEntry<bool> MaskedEntitiesShowPlayerNames { get; private set; }
        public static ConfigEntry<int> MaskedEntitiesShowScrapIconChance { get; private set; }
        public static ConfigEntry<bool> MaskedEntitiesSpinOnRadar { get; private set; }
        public static ConfigEntry<bool> MaskedEntitiesWearMasks { get; private set; }

        private const string ExtraMonitorsSection = "ExtraMonitors";
        public static ConfigEntry<bool> AddMoreBetterMonitors { get; private set; }
        public static ConfigEntry<bool> AlwaysRenderMonitors { get; private set; }
        public static ConfigEntry<bool> CenterAlignMonitorText { get; private set; }
        public static ConfigEntry<string> CustomTextMonitorValue { get; private set; }
        public static ConfigEntry<string> MonitorBackgroundColor { get; private set; }
        public static Color MonitorBackgroundColorVal { get; private set; }
        public static ConfigEntry<string> MonitorTextColor { get; private set; }
        public static Color MonitorTextColorVal { get; private set; }
        public static ConfigEntry<int> ShipExternalCamFPS { get; private set; }
        public static ConfigEntry<int> ShipExternalCamSizeMultiplier { get; private set; }
        public static ConfigEntry<int> ShipInternalCamFPS { get; private set; }
        public static ConfigEntry<int> ShipInternalCamSizeMultiplier { get; private set; }
        public static ConfigEntry<eMonitorNames>[] ShipMonitorAssignments { get; private set; }
        public static ConfigEntry<bool> ShowBackgroundOnAllScreens { get; private set; }
        public static ConfigEntry<bool> ShowBlueMonitorBackground { get; private set; }
        public static ConfigEntry<bool> SyncExtraMonitorsPower { get; private set; }
        public static ConfigEntry<bool> SyncMonitorsFromOtherHost { get; private set; }
        public static ConfigEntry<bool> UseBetterMonitors { get; private set; }
        public static ConfigEntry<bool> UseMoreMonitorTextColors { get; private set; }

        private const string FixesSection = "Fixes";
        public static ConfigEntry<bool> AutomaticallyCollectTeleportedCorpses { get; private set; }
        public static ConfigEntry<int> DropShipItemLimit { get; private set; }
        public static ConfigEntry<bool> FixInternalFireExits { get; private set; }
        public static ConfigEntry<bool> FixItemsFallingThrough { get; private set; }
        public static ConfigEntry<bool> FixItemsLoadingSameRotation { get; private set; }

        private const string GameLaunchSection = "GameLaunch";
        public static ConfigEntry<bool> AllowPreGameLeverPullAsClient { get; private set; }
        public static ConfigEntry<bool> AlwaysShowNews { get; private set; }
        public static ConfigEntry<eAutoLaunchOption> AutoSelectLaunchMode { get; private set; }
        public static ConfigEntry<int> MenuMusicVolume { get; private set; }
        public static ConfigEntry<bool> SkipStartupScreen { get; private set; }

        private const string InventorySection = "Inventory";
        public static ConfigEntry<bool> PickupInOrder { get; private set; }
        public static ConfigEntry<bool> RearrangeOnDrop { get; private set; }
        public static ConfigEntry<float> ScrollDelay { get; private set; }
        public static ConfigEntry<bool> TwoHandedInSlotOne { get; private set; }

        private const string MechanicsSection = "Mechanics";
        public static ConfigEntry<bool> AddHealthRechargeStation { get; private set; }
        public static ConfigEntry<bool> AllowPickupOfAllItemsPreStart { get; private set; }
        public static ConfigEntry<bool> AllowQuotaRollover { get; private set; }
        public static ConfigEntry<bool> DestroyKeysAfterOrbiting { get; private set; }
        public static ConfigEntry<bool> KeysHaveInfiniteUses { get; private set; }
        public static ConfigEntry<int> MinimumStartingMoney { get; private set; }
        public static int MinimumStartingMoneyVal => Math.Clamp(MinimumStartingMoney.Value, StartingMoneyVal, 10000);
        public static ConfigEntry<eOvertimeBonusType> OvertimeBonusType { get; private set; }
        public static ConfigEntry<int> QuotaRolloverSquadWipePenalty { get; private set; }
        public static ConfigEntry<bool> SavePlayerSuits { get; private set; }
        public static ConfigEntry<bool> ScanCommandUsesExactAmount { get; private set; }
        public static ConfigEntry<string> ScrapValueWeatherMultipliers { get; private set; }
        public static Dictionary<string, float> SanitizedScrapValueWeatherMultipliers { get; private set; }
        public static ConfigEntry<string> ScrapAmountWeatherMultipliers { get; private set; }
        public static Dictionary<string, float> SanitizedScrapAmountWeatherMultipliers { get; private set; }
        public static ConfigEntry<eLadderSprintOption> SprintOnLadders { get; private set; }
        public static ConfigEntry<int> StartingMoney { get; private set; }
        public static int StartingMoneyVal => Math.Clamp(StartingMoney.Value, 0, 10000);
        public static ConfigEntry<eStartingMoneyFunction> StartingMoneyFunction { get; private set; }
        public static ConfigEntry<bool> UnlockDoorsFromInventory { get; private set; }

        private const string ScannerSection = "Scanner";
        public static ConfigEntry<bool> FixPersonalScanner { get; private set; }
        public static ConfigEntry<bool> ScanPlayers { get; private set; }
        public static ConfigEntry<bool> ShowDoorsOnScanner { get; private set; }
        public static ConfigEntry<bool> ShowDropshipOnScanner { get; private set; }

        public const string ScrapSection = "Scrap";
        public static ConfigEntry<bool> AllowFancyLampToBeToggled { get; private set; }

        private const string ShipSection = "Ship";
        public static ConfigEntry<bool> AllowChargerPlacement { get; private set; }
        public static ConfigEntry<eValidKeys> CounterClockwiseKey { get; private set; }
        public static ConfigEntry<bool> DisableInternalShipCamPostProcessing { get; private set; }
        public static ConfigEntry<bool> DisableExternalShipCamPostProcessing { get; private set; }
        public static ConfigEntry<eValidKeys> FreeRotateKey { get; private set; }
        public static ConfigEntry<bool> HideClipboardAndStickyNote { get; private set; }
        public static ConfigEntry<bool> HideShipCabinetDoors { get; private set; }
        public static ConfigEntry<bool> LightSwitchScanNode { get; private set; }
        public static ConfigEntry<bool> MoveShipClipboardToWall { get; private set; }
        public static ConfigEntry<eSaveFurniturePlacement> SaveShipFurniturePlaces { get; private set; }
        public static ConfigEntry<eShipCamRotation> ShipMapCamRotation { get; private set; }
        public static ConfigEntry<bool> ShipPlaceablesCollide { get; private set; }
        public static ConfigEntry<int> SnapObjectsByDegrees { get; private set; }
        public static ConfigEntry<bool> SpeakerPlaysIntroVoice { get; private set; }

        private const string TeleportersSection = "Teleporters";
        public static ConfigEntry<int> InverseTeleporterCooldown { get; private set; }
        public static ConfigEntry<eItemsToKeep> KeepItemsDuringInverse { get; private set; }
        public static ConfigEntry<eItemsToKeep> KeepItemsDuringTeleport { get; private set; }
        public static ConfigEntry<eRadarBoosterTeleport> RadarBoostersCanBeTeleported { get; private set; }
        public static ConfigEntry<int> RegularTeleporterCooldown { get; private set; }

        private const string TerminalSection = "Terminal";
        public static ConfigEntry<bool> FitCreditsInBackgroundImage { get; private set; }
        public static ConfigEntry<bool> LockCameraAtTerminal { get; private set; }
        public static ConfigEntry<bool> ShowBlanksDuringViewMonitor { get; private set; }
        public static ConfigEntry<eShowHiddenMoons> ShowHiddenMoonsInCatalog { get; private set; }
        public static ConfigEntry<bool> ShowMoonPricesInTerminal { get; private set; }
        public static ConfigEntry<int> TerminalHistoryItemCount { get; private set; }
        public static ConfigEntry<bool> TerminalFastCamSwitch { get; private set; }

        private const string ToolsSection = "Tools";
        public static ConfigEntry<bool> AutoChargeOnOrbit { get; private set; }
        public static ConfigEntry<eValidKeys> FlashlightToggleShortcut { get; private set; }
        public static ConfigEntry<bool> OnlyAllowOneActiveFlashlight { get; private set; }
        public static ConfigEntry<string> ScannableTools { get; private set; }
        public static List<Type> ScannableToolVals { get; private set; } = new List<Type>();
        public static ConfigEntry<bool> ToolsDoNotAttractLightning { get; private set; }
        public static ConfigEntry<bool> TreatLasersAsFlashlights { get; private set; }

        private const string UISection = "UI";
        public static ConfigEntry<bool> AlwaysShowClock { get; private set; }
        public static ConfigEntry<bool> CenterSignalTranslatorText { get; private set; }
        public static ConfigEntry<float> ChatFadeDelay { get; private set; }
        public static ConfigEntry<float> ChatOpacity { get; private set; }
        public static ConfigEntry<bool> DisplayKgInsteadOfLb { get; private set; }
        public static ConfigEntry<bool> DisplayRoundedKg { get; private set; }
        public static ConfigEntry<bool> HideEmptySubtextOfScanNodes { get; private set; }
        public static ConfigEntry<bool> HidePlayerNames { get; private set; }
        public static ConfigEntry<bool> ShowHitPoints { get; private set; }
        public static ConfigEntry<bool> ShowLightningWarnings { get; private set; }
        public static ConfigEntry<bool> ShowUIReticle { get; private set; }
        public static ConfigEntry<bool> TwentyFourHourClock { get; private set; }

        private void Awake()
        {
            MLS = Logger;

            // Load info about any external mods first
            OtherModHelper.Initialize();
            AssetBundleHelper.Initialize();

            BindConfigs();
            MigrateOldConfigValues();
            MLS.LogInfo("Configuration Initialized.");

            var harmony = new Harmony(Metadata.GUID);

            harmony.PatchAll(typeof(ILManipulatorPatch));
            MLS.LogInfo("ILManipulator patched (fixes rare cases where transpilers do not emit the expected IL code.");

            harmony.PatchAll(typeof(AudioReverbTriggerPatch));
            MLS.LogInfo("AudioReverbTrigger patched.");

            harmony.PatchAll(typeof(AutoParentToShipPatch));
            MLS.LogInfo("AutoParentToShip patched.");

            harmony.PatchAll(typeof(DepositItemsDeskPatch));
            MLS.LogInfo("DepositItemsDesk patched.");

            harmony.PatchAll(typeof(DoorLockPatch));
            MLS.LogInfo("DoorLock patched.");

            harmony.PatchAll(typeof(EnemyAIPatch));
            MLS.LogInfo("EnemyAI patched.");

            harmony.PatchAll(typeof(EntranceTeleportPatch));
            MLS.LogInfo("EntranceTeleport patched.");

            if (!OtherModHelper.FlashlightFixActive)
            {
                harmony.PatchAll(typeof(FlashlightItemPatch));
                MLS.LogInfo("FlashlightItem patched.");
            }
            else
            {
                MLS.LogWarning("Outdated version of FlashlightFix detected - please update your mods.");
            }

            harmony.PatchAll(typeof(GameNetworkManagerPatch));
            MLS.LogInfo("GameNetworkManager patched.");

            harmony.PatchAll(typeof(GrabbableObjectsPatch));
            MLS.LogInfo("GrabbableObjects patched.");

            harmony.PatchAll(typeof(HangarShipDoorPatch));
            MLS.LogInfo("HangarShipDoor patched.");

            harmony.PatchAll(typeof(HUDManagerPatch));
            MLS.LogInfo("HUDManager patched.");

            harmony.PatchAll(typeof(ItemDropshipPatch));
            MLS.LogInfo("ItemDropship patched.");

            harmony.PatchAll(typeof(LandminePatch));
            MLS.LogInfo("Landmine patched.");

            harmony.PatchAll(typeof(ManualCameraRendererPatch));
            MLS.LogInfo("ManualCameraRenderer patched.");

            harmony.PatchAll(typeof(MaskedPlayerEnemyPatch));
            MLS.LogInfo("MaskedPlayerEnemy patched.");

            harmony.PatchAll(typeof(MenuPatches));
            MLS.LogInfo("Menus patched.");

            harmony.PatchAll(typeof(PlayerControllerBPatch));
            MLS.LogInfo("PlayerControllerB patched.");

            harmony.PatchAll(typeof(RadarBoosterItemPatch));
            MLS.LogInfo("RadarBoosterItem patched.");

            harmony.PatchAll(typeof(RoundManagerPatch));
            MLS.LogInfo("RoundManager patched.");

            harmony.PatchAll(typeof(ShipBuildModeManagerPatch));
            MLS.LogInfo("ShipBuildModeManager patched.");

            harmony.PatchAll(typeof(ShipTeleporterPatch));
            MLS.LogInfo("ShipTeleporter patched.");

            harmony.PatchAll(typeof(SprayPaintItemPatch));
            MLS.LogInfo("SprayPaintItem patched.");

            harmony.PatchAll(typeof(StartMatchLeverPatch));
            MLS.LogInfo("StartMatchLever patched.");

            harmony.PatchAll(typeof(StartOfRoundPatch));
            MLS.LogInfo("StartOfRound patched.");

            harmony.PatchAll(typeof(StormyWeatherPatch));
            MLS.LogInfo("StormyWeather patched.");

            harmony.PatchAll(typeof(TerminalAccessibleObjectPatch));
            MLS.LogInfo("TerminalAccessibleObject patched.");

            harmony.PatchAll(typeof(TerminalPatch));
            MLS.LogInfo("Terminal patched.");

            harmony.PatchAll(typeof(TimeOfDayPatch));
            MLS.LogInfo("TimeOfDay patched.");

            harmony.PatchAll(typeof(UnlockableSuitPatch));
            MLS.LogInfo("UnlockableSuit patched.");

            GameNetworkManagerPatch.PatchNetcode();

            // Custom patches for other things
            OtherModHelper.PatchBuyRateSettingsIfNeeded(harmony);
            OtherModHelper.PatchCodeRebirthIfNeeded(harmony);

            MLS.LogInfo($"{Metadata.PLUGIN_NAME} v{Metadata.VERSION} fully loaded.");
        }

        public void BindConfigs()
        {
            string incompatWarning = "**WARNING:** THIS WILL PREVENT YOU FROM CONNECTING TO ANY OTHER PLAYERS THAT DO NOT ALSO HAVE IT ENABLED!";
            string defaultNoChange = "Leaving this on its default value will ensure no vanilla code is changed.";

            var validSnapRotations = Enumerable.Range(0, 360 / 15).Select(n => n * 15).Where(n => n == 0 || 360 % n == 0).ToArray();

            var validToolTypes = new List<Type> { typeof(BeltBagItem), typeof(BoomboxItem), typeof(ExtensionLadderItem), typeof(FlashlightItem), typeof(JetpackItem), typeof(LockPicker), typeof(RadarBoosterItem),
                                                  typeof(KnifeItem), typeof(Shovel), typeof(SprayPaintItem), typeof(StunGrenadeItem), typeof(TetraChemicalItem), typeof(WalkieTalkie), typeof(PatcherTool) };
            var validToolStrings = string.Join(", ", new[] { "All" }.Concat(validToolTypes.Select(t => t.Name)));

            // Enemies
            MaskedPlayersAppearAliveOnMonitors = Config.Bind(EnemiesSection, nameof(MaskedPlayersAppearAliveOnMonitors), false, "If set to true, when a masked entity kills a player, the player's health and alive status will display false (misleading) info on life status monitors.");
            MaskedEntitiesCopyPlayerLooks = Config.Bind(EnemiesSection, nameof(MaskedEntitiesCopyPlayerLooks), eMaskedEntityCopyLook.None, "How much masked entities should look like a targeted player.");
            MaskedEntitiesReachTowardsPlayer = Config.Bind(EnemiesSection, nameof(MaskedEntitiesReachTowardsPlayer), true, "If set to true, masked entities will reach towards the player they are chasing in a zombie-like way.");
            MaskedEntitiesShowPlayerNames = Config.Bind(EnemiesSection, nameof(MaskedEntitiesShowPlayerNames), false, "If set to true, masked entities will display their targeted player's name above their head, as well as be scannable if ScanPlayers = True.");
            MaskedEntitiesShowScrapIconChance = Config.Bind(EnemiesSection, nameof(MaskedEntitiesShowScrapIconChance), 0, new ConfigDescription("The percentage (0-100) a masked entity will spawn with a radar map scrap icon attached.", new AcceptableValueRange<int>(0, 100)));
            MaskedEntitiesSpinOnRadar = Config.Bind(EnemiesSection, nameof(MaskedEntitiesSpinOnRadar), true, "If set to true, masked entities' radar dots will spin randomly, slightly giving away their identity.");
            MaskedEntitiesWearMasks = Config.Bind(EnemiesSection, nameof(MaskedEntitiesWearMasks), true, "If set to true, masked entities will wear their default masks.");

            // Extra monitors
            AddMoreBetterMonitors = Config.Bind(ExtraMonitorsSection, nameof(AddMoreBetterMonitors), true, "If set to true and paired with UseBetterMonitors (required), adds 4 more small and 1 large monitor to the left of the main ship monitor group.");
            AlwaysRenderMonitors = Config.Bind(ExtraMonitorsSection, nameof(AlwaysRenderMonitors), false, $"If using better monitors and set to true, text-based monitors will render updates even when you are not in the ship. May slightly affect performance.");
            CenterAlignMonitorText = Config.Bind(ExtraMonitorsSection, nameof(CenterAlignMonitorText), true, "If set to true, all small monitors in the ship will have their text center aligned, instead of left.");
            CustomTextMonitorValue = Config.Bind(ExtraMonitorsSection, nameof(CustomTextMonitorValue), "SAMPLE TEXT 1|SAMPLE TEXT 2", $"If using any custom text monitors, will display this custom text on them. Pipes (|) can be used to set multiple monitor's values, in display order. Each value is limited to 200 characters.");
            MonitorBackgroundColor = Config.Bind(ExtraMonitorsSection, nameof(MonitorBackgroundColor), "160959", "The hex color code of what the backgrounds of the monitors should be. A recommended value close to black is 050505.");
            MonitorTextColor = Config.Bind(ExtraMonitorsSection, nameof(MonitorTextColor), "00FF2C", "The hex color code of what the text on the monitors should be.");
            ShipExternalCamFPS = Config.Bind(ExtraMonitorsSection, nameof(ShipExternalCamFPS), 0, new ConfigDescription($"Limits the FPS of the external ship cam for performance. 0 = Unrestricted.", new AcceptableValueRange<int>(0, 30)));
            ShipExternalCamSizeMultiplier = Config.Bind(ExtraMonitorsSection, nameof(ShipExternalCamSizeMultiplier), 1, new ConfigDescription($"How many times to double the external ship cam's resolution.", new AcceptableValueRange<int>(1, 5)));
            ShipInternalCamFPS = Config.Bind(ExtraMonitorsSection, nameof(ShipInternalCamFPS), 0, new ConfigDescription($"Limits the FPS of the internal ship cam for performance. 0 = Unrestricted.", new AcceptableValueRange<int>(0, 30)));
            ShipInternalCamSizeMultiplier = Config.Bind(ExtraMonitorsSection, nameof(ShipInternalCamSizeMultiplier), 1, new ConfigDescription($"How many times to double the internal ship cam's resolution.", new AcceptableValueRange<int>(1, 5)));
            ShipMonitorAssignments = new ConfigEntry<eMonitorNames>[14];
            for (int i = 0; i < ShipMonitorAssignments.Length; i++)
            {
                eMonitorNames defaultVal = i switch { 4 => eMonitorNames.ProfitQuota, 5 => eMonitorNames.Deadline, 10 => eMonitorNames.InternalCam, 13 => eMonitorNames.ExternalCam, _ => eMonitorNames.None };
                ShipMonitorAssignments[i] = Config.Bind(ExtraMonitorsSection, $"ShipMonitor{i + 1}", defaultVal, $"What to display on the ship monitor at position {i + 1}, if anything.");
            }
            ShowBackgroundOnAllScreens = Config.Bind(ExtraMonitorsSection, nameof(ShowBackgroundOnAllScreens), false, "If set to true, will show the MonitorBackgroundColor on ALL monitors when they are on, not just used ones.");
            ShowBlueMonitorBackground = Config.Bind(ExtraMonitorsSection, nameof(ShowBlueMonitorBackground), true, "If set to true and NOT using UseBetterMonitors, keeps the vanilla blue backgrounds on the extra monitors. Set to false to hide.");
            SyncExtraMonitorsPower = Config.Bind(ExtraMonitorsSection, nameof(SyncExtraMonitorsPower), true, "If set to true, The smaller monitors above the map screen will turn off and on when the map screen power is toggled.");
            SyncMonitorsFromOtherHost = Config.Bind(ExtraMonitorsSection, nameof(SyncMonitorsFromOtherHost), false, "If set to true, all monitor placements will be synced from the host when joining a game, if the host is also using this mod. Settings such as color, FPS, etc will not be synced.");
            UseBetterMonitors = Config.Bind(ExtraMonitorsSection, nameof(UseBetterMonitors), false, "If set to true, upgrades the vanilla monitors with integrated and more customizable overlays.");
            UseMoreMonitorTextColors = Config.Bind(ExtraMonitorsSection, nameof(UseMoreMonitorTextColors), true, "If set to true, many monitors' texts will have multiple context sensitive colors to try making them more pleasant to read.");

            // Fixes
            AutomaticallyCollectTeleportedCorpses = Config.Bind(FixesSection, nameof(AutomaticallyCollectTeleportedCorpses), true, "If set to true, dead bodies will be automatically collected as scrap when being teleported to the ship.");
            DropShipItemLimit = Config.Bind(FixesSection, nameof(DropShipItemLimit), 24, new ConfigDescription("Sets the max amount of items a single dropship delivery will allow. Vanilla = 12.", new AcceptableValueRange<int>(12, 100)));
            FixInternalFireExits = Config.Bind(FixesSection, nameof(FixInternalFireExits), true, "If set to true, the player will face the interior of the facility when entering through a fire entrance.");
            FixItemsFallingThrough = Config.Bind(FixesSection, nameof(FixItemsFallingThrough), true, "Fixes items falling through furniture on the ship when loading the game.");
            FixItemsLoadingSameRotation = Config.Bind(FixesSection, nameof(FixItemsLoadingSameRotation), true, "Fixes items all facing the same way when loading a save file. Now they will store their rotations as well.");

            // Game Launch
            AllowPreGameLeverPullAsClient = Config.Bind(GameLaunchSection, nameof(AllowPreGameLeverPullAsClient), true, "If set to true, you will be able to pull the ship lever to start the game as a connected player.");
            AlwaysShowNews = Config.Bind(GameLaunchSection, nameof(AlwaysShowNews), false, "If set to true, will always display the news popup when starting the game.");
            AutoSelectLaunchMode = Config.Bind(GameLaunchSection, nameof(AutoSelectLaunchMode), eAutoLaunchOption.NONE, "If set to 'ONLINE' or 'LAN', will automatically launch the correct mode, saving you from having to click the menu option when the game loads. **WARNING** THIS MAY PREVENT CERTAIN OTHER MODS FROM BEING ABLE TO PROPERLY INITIALIZE.");
            MenuMusicVolume = Config.Bind(GameLaunchSection, nameof(MenuMusicVolume), 100, new ConfigDescription("Controls the volume of the menu music, from 0-100.", new AcceptableValueRange<int>(0, 100)));
            SkipStartupScreen = Config.Bind(GameLaunchSection, nameof(SkipStartupScreen), true, "Skips the main menu loading screen bootup animation.");

            // Inventory
            PickupInOrder = Config.Bind(InventorySection, nameof(PickupInOrder), false, "When picking up items, will always put them in left - right order.");
            RearrangeOnDrop = Config.Bind(InventorySection, nameof(RearrangeOnDrop), false, "When dropping items, will rearrange other inventory items to ensure slots are filled left - right.");
            ScrollDelay = Config.Bind(InventorySection, nameof(ScrollDelay), 0.1f, new ConfigDescription("The minimum time you must wait to scroll to another item in your inventory. Vanilla: 0.3.", new AcceptableValueRange<float>(0.05f, 0.3f)));
            TwoHandedInSlotOne = Config.Bind(InventorySection, nameof(TwoHandedInSlotOne), false, $"When picking up a two handed item, it will always place it in slot 1 and shift things to the right if needed. Makes selling quicker when paired with RearrangeOnDrop.");

            // Mechanics
            AddHealthRechargeStation = Config.Bind(MechanicsSection, nameof(AddHealthRechargeStation), false, $"[ALL USERS] If set to true, a medical charging station will be above the ship's battery charger, and can be used to heal to full. {incompatWarning}");
            AllowPickupOfAllItemsPreStart = Config.Bind(MechanicsSection, nameof(AllowPickupOfAllItemsPreStart), true, "Allows you to pick up all grabbable items before the game is started.");
            AllowQuotaRollover = Config.Bind(MechanicsSection, nameof(AllowQuotaRollover), false, "[Host Only] If set to true, will keep the surplus money remaining after selling things to the company, and roll it over to the next quota. If clients do not set this, they will see visual desyncs.");
            DestroyKeysAfterOrbiting = Config.Bind(MechanicsSection, nameof(DestroyKeysAfterOrbiting), false, "If set to true, all keys in YOUR inventory (and IF HOSTING, the ship) will be destroyed after orbiting. Works well to nerf KeysHaveInfiniteUses. Players who do not have this enabled will keep keys currently in their inventory.");
            KeysHaveInfiniteUses = Config.Bind(MechanicsSection, nameof(KeysHaveInfiniteUses), false, "If set to true, keys will not despawn when they are used.");
            MinimumStartingMoney = Config.Bind(MechanicsSection, nameof(MinimumStartingMoney), 60, $"[Host Only] If set to a value higher than {nameof(StartingMoney)}'s and using {eStartingMoneyFunction.PerPlayerWithMinimum} for {nameof(StartingMoneyFunction)}, will ensure the group always starts with at least this much money. Internally capped at 10k. {defaultNoChange}");
            OvertimeBonusType = Config.Bind(MechanicsSection, nameof(OvertimeBonusType), eOvertimeBonusType.Vanilla, $"[Host Only] {eOvertimeBonusType.Vanilla} will not alter vanilla overtime bonuses. {eOvertimeBonusType.SoldScrapOnly} will calculate bonuses based on what was sold at the company, ignoring any extra rollover. {eOvertimeBonusType.Disabled} disabled overtime bonuses completely.");
            QuotaRolloverSquadWipePenalty = Config.Bind(MechanicsSection, nameof(QuotaRolloverSquadWipePenalty), 0, new ConfigDescription($"[Host Only] When using {nameof(AllowQuotaRollover)}, this number will be the percentage of rolled-over quota funds that will be immediately deducted if losing every player while on a moon.", new AcceptableValueRange<int>(0, 100)));
            SavePlayerSuits = Config.Bind(MechanicsSection, nameof(SavePlayerSuits), true, "If set to true, the host will keep track of every player's last used suit, and will persist between loads and ship resets for each save file. Only works in Online mode.");
            ScanCommandUsesExactAmount = Config.Bind(MechanicsSection, nameof(ScanCommandUsesExactAmount), false, "If set to true, the terminal's scan command (and ScrapLeft monitor) will use display the exact scrap value remaining instead of approximate.");
            ScrapValueWeatherMultipliers = Config.Bind(MechanicsSection, nameof(ScrapValueWeatherMultipliers), string.Empty, "[Host Only] You may specify comma separated weather:multiplier (0.1 - 2.0) for all weather types, including modded weather. Default vanilla scrap value multiplier is 0.4, which will default for any unspecified weather type. A recommended value would be 'None:0.4, DustClouds:0.5, Foggy:0.5, Rainy:0.55, Flooded:0.6, Stormy:0.7, Eclipsed:0.8'.");
            ScrapAmountWeatherMultipliers = Config.Bind(MechanicsSection, nameof(ScrapAmountWeatherMultipliers), string.Empty, "[Host Only] You may specify comma separated weather:multiplier (1.0 - 5.0) for all weather types, including modded weather. Default vanilla scrap amount multiplier is 1.0, which will default for any unspecified weather type. A recommended value would be 'None:1.0, DustClouds:1.2, Foggy:1.2, Rainy:1.3, Flooded:1.4, Stormy:1.5, Eclipsed:1.6'.");
            SprintOnLadders = Config.Bind(MechanicsSection, nameof(SprintOnLadders), eLadderSprintOption.None, $"If set to {nameof(eLadderSprintOption.NoDrain)}, will prevent sprint meter from draining while using ladders. If set to {eLadderSprintOption.Allow}, allows faster climbing while sprinting.");
            StartingMoney = Config.Bind(MechanicsSection, nameof(StartingMoney), 60, $"[Host Only] How much starting money the group gets when starting a new game. Internally clamped between 0 and 10k. {defaultNoChange}");
            StartingMoneyFunction = Config.Bind(MechanicsSection, nameof(StartingMoneyFunction), eStartingMoneyFunction.Disabled, $"[Host Only] Controls how {nameof(StartingMoney)} behaves. {eStartingMoneyFunction.Total} will set the credits to a single flat amount. {eStartingMoneyFunction.PerPlayer} will adjust the credits by {nameof(StartingMoney)} as players join and leave. {eStartingMoneyFunction.PerPlayerWithMinimum} does the same, with a minimum set by {nameof(MinimumStartingMoney)}. {defaultNoChange}");
            UnlockDoorsFromInventory = Config.Bind(MechanicsSection, nameof(UnlockDoorsFromInventory), false, "If set to true, keys in your inventory do not have to be held when unlocking facility doors.");

            // Scanner
            FixPersonalScanner = Config.Bind(ScannerSection, nameof(FixPersonalScanner), false, "If set to true, will tweak the behavior of the scan action and more reliably ping items closer to you, and the ship/main entrance.");
            ScanPlayers = Config.Bind(ScannerSection, nameof(ScanPlayers), false, "If set to true, players (and sneaky masked entities) will be scannable.");
            ShowDoorsOnScanner = Config.Bind(ScannerSection, nameof(ShowDoorsOnScanner), false, "If set to true, all fire entrances and facility exits will be scannable. Compatible with mimics mod (they show up as an exit as well).");
            ShowDropshipOnScanner = Config.Bind(ScannerSection, nameof(ShowDropshipOnScanner), false, "If set to true, the item drop ship will be scannable.");

            // Scrap
            AllowFancyLampToBeToggled = Config.Bind(ScrapSection, nameof(AllowFancyLampToBeToggled), true, "If set to true, will enable the fancy lamp scrap's light to be turned on and off while being held. Be careful, sound sensitive enemies can hear its click!");

            // Ship
            AllowChargerPlacement = Config.Bind(ShipSection, nameof(AllowChargerPlacement), false, $"[ALL USERS] If set to true, the battery charger may be placed via the ship's build mode. {incompatWarning}");
            CounterClockwiseKey = Config.Bind(ShipSection, nameof(CounterClockwiseKey), eValidKeys.LeftShift, "If SnapObjectsByDegrees > 0, configures which modifier key spins it CCW.");
            DisableInternalShipCamPostProcessing = Config.Bind(ShipSection, nameof(DisableInternalShipCamPostProcessing), false, "If set to true, the internal ship camera will no longer use post processing. This may improve performance with higher resolution camera settings.");
            DisableExternalShipCamPostProcessing = Config.Bind(ShipSection, nameof(DisableExternalShipCamPostProcessing), false, "If set to true, the external ship camera will no longer use post processing. This may improve performance with higher resolution camera settings.");
            FreeRotateKey = Config.Bind(ShipSection, nameof(FreeRotateKey), eValidKeys.LeftAlt, "If SnapObjectsByDegrees > 0, configures which modifer key activates free rotation.");
            HideClipboardAndStickyNote = Config.Bind(ShipSection, nameof(HideClipboardAndStickyNote), false, "If set to true, the game will not show the clipboard or sticky note when the game loads.");
            HideShipCabinetDoors = Config.Bind(ShipSection, nameof(HideShipCabinetDoors), false, "If set to true, the storage shelves in the ship will not have doors.");
            LightSwitchScanNode = Config.Bind(ShipSection, nameof(LightSwitchScanNode), true, "If set to true, the light switch will have a scan node attached.");
            MoveShipClipboardToWall = Config.Bind(ShipSection, nameof(MoveShipClipboardToWall), true, "If set to true, the ship's clipboard will not start on the table but instead on the wall in front of the player.");
            SaveShipFurniturePlaces = Config.Bind(ShipSection, nameof(SaveShipFurniturePlaces), eSaveFurniturePlacement.StartingFurniture, "Determines what ship furniture positions and storage states will not be reset after being fired.");
            ShipMapCamRotation = Config.Bind(ShipSection, nameof(ShipMapCamRotation), eShipCamRotation.None, "If specified, makes the ship cam face a specific direction instead of a 45 degree SW angle.");
            ShipPlaceablesCollide = Config.Bind(ShipSection, nameof(ShipPlaceablesCollide), true, "If set to true, placeable ship objects will check for collisions with each other during placement.");
            SnapObjectsByDegrees = Config.Bind(ShipSection, nameof(SnapObjectsByDegrees), 45, new ConfigDescription("Build mode will switch to snap turning (press instead of hold) by this many degrees at a time. Setting it to 0 uses vanilla behavior.", new AcceptableValueList<int>(validSnapRotations)));
            SpeakerPlaysIntroVoice = Config.Bind(ShipSection, nameof(SpeakerPlaysIntroVoice), true, "If set to true, the ship's speaker will play the introductory welcome audio on the first day.");

            // Teleporters
            InverseTeleporterCooldown = Config.Bind(TeleportersSection, nameof(InverseTeleporterCooldown), 210, new ConfigDescription("How many seconds to wait in between button presses for the INVERSE teleporter. Vanilla = 210. If using the vanilla value, the teleporter code will not be modified.", new AcceptableValueRange<int>(1, 300)));
            KeepItemsDuringInverse = Config.Bind(TeleportersSection, nameof(KeepItemsDuringInverse), eItemsToKeep.None, "Whether to keep Held, Non Scrap, or All items in inventory when using the inverse teleporter. *WARNING:* THIS WILL CAUSE INVENTORY DESYNCS IF OTHER PLAYERS DO NOT SHARE YOUR SETTING!");
            KeepItemsDuringTeleport = Config.Bind(TeleportersSection, nameof(KeepItemsDuringTeleport), eItemsToKeep.None, "Whether to keep Held, Non Scrap, or All items in inventory when using the regular teleporter. *WARNING:* THIS WILL CAUSE INVENTORY DESYNCS IF OTHER PLAYERS DO NOT SHARE YOUR SETTING!");
            RadarBoostersCanBeTeleported = Config.Bind(TeleportersSection, nameof(RadarBoostersCanBeTeleported), eRadarBoosterTeleport.Disabled, "[Host Only] If enabled, radar boosters can be affected by the specified type of teleporters. If the host has this setting enabled, unmodded clients may experience desyncs with radar boosters and teleporters.");
            RegularTeleporterCooldown = Config.Bind(TeleportersSection, nameof(RegularTeleporterCooldown), 10, new ConfigDescription("How many seconds to wait in between button presses for the REGULAR teleporter. Vanilla = 10. If using the vanilla value, the teleporter code will not be modified.", new AcceptableValueRange<int>(1, 300)));

            // Terminal
            FitCreditsInBackgroundImage = Config.Bind(TerminalSection, nameof(FitCreditsInBackgroundImage), true, "If set to true, the credits displayed in the terminal will always fit nicely inside its dark green background.");
            LockCameraAtTerminal = Config.Bind(TerminalSection, nameof(LockCameraAtTerminal), true, "If set to true, the camera will no longer move around when moving your mouse/controller while at the terminal.");
            ShowBlanksDuringViewMonitor = Config.Bind(TerminalSection, nameof(ShowBlanksDuringViewMonitor), true, "If set to true, typing commands while View Monitor is active requires you to scroll down to see the result.");
            ShowHiddenMoonsInCatalog = Config.Bind(TerminalSection, nameof(ShowHiddenMoonsInCatalog), eShowHiddenMoons.AfterDiscovery, "When to show any hidden moons in the terminal's moon catalog. AfterDiscovery is per save file.");
            ShowMoonPricesInTerminal = Config.Bind(TerminalSection, nameof(ShowMoonPricesInTerminal), false, "If set to true, the moons will also display the cost to fly to them next to their name and weather.");
            TerminalHistoryItemCount = Config.Bind(TerminalSection, nameof(TerminalHistoryItemCount), 20, new ConfigDescription("How many items to keep in your terminal's command history. Previous terminal commands may be navigated by using the up/down arrow keys.", new AcceptableValueRange<int>(0, 100)));
            TerminalFastCamSwitch = Config.Bind(TerminalSection, nameof(TerminalFastCamSwitch), true, "If set to true, will allow use of the left/right arrow keys to quickly cycle through radar cameras while using the terminal.");

            // Tools
            AutoChargeOnOrbit = Config.Bind(ToolsSection, nameof(AutoChargeOnOrbit), false, "If set to true, all owned* battery-using items will be automatically charged every time the ship goes into orbit. *You are considered to 'own' an item if you are the last person to have held it.");
            FlashlightToggleShortcut = Config.Bind(ToolsSection, nameof(FlashlightToggleShortcut), eValidKeys.None, $"A shortcut key to allow toggling a flashlight at any time.");
            OnlyAllowOneActiveFlashlight = Config.Bind(ToolsSection, nameof(OnlyAllowOneActiveFlashlight), true, "When turning on any flashlight, will turn off any others in your inventory that are still active.");
            ScannableTools = Config.Bind(ToolsSection, nameof(ScannableTools), string.Empty, $"A comma separated list of which tools, if any, should be scannable. Accepted values: {validToolStrings}");
            ToolsDoNotAttractLightning = Config.Bind(ToolsSection, nameof(ToolsDoNotAttractLightning), false, "[Host Only] If set to true, all useful tools (ladders, jetpacks, keys, radar boosters, shovels & signs, tzp inhalant, knives, and zap guns) will no longer attract lighning.");
            TreatLasersAsFlashlights = Config.Bind(ToolsSection, nameof(TreatLasersAsFlashlights), false, "If set to true, laser pointers will be like flashlights and automatically toggle off and on when switching to them, etc.");

            // UI
            AlwaysShowClock = Config.Bind(UISection, nameof(AlwaysShowClock), false, "If set to true, the clock will always be displayed on the HUD when landed on a moon.");
            CenterSignalTranslatorText = Config.Bind(UISection, nameof(CenterSignalTranslatorText), true, "If set to true, the signal translator text will always be perfectly horizontally centered.");
            ChatFadeDelay = Config.Bind(UISection, nameof(ChatFadeDelay), 4f, new ConfigDescription("How long to wait before fading chat after a new chat message appears.", new AcceptableValueRange<float>(0f, 10f)));
            ChatOpacity = Config.Bind(UISection, nameof(ChatOpacity), 0.2f, new ConfigDescription("How faded the chat should be after the fade delay. 0 = fully transparent, 1 = solid.", new AcceptableValueRange<float>(0f, 1f)));
            DisplayKgInsteadOfLb = Config.Bind(UISection, nameof(DisplayKgInsteadOfLb), false, "If set to true, your carry weight will be converted from lb to kg.");
            DisplayRoundedKg = Config.Bind(UISection, nameof(DisplayRoundedKg), false, $"If set to true and using {nameof(DisplayKgInsteadOfLb)}, numeric values will be rounded to the nearest integer.");
            HideEmptySubtextOfScanNodes = Config.Bind(UISection, nameof(HideEmptySubtextOfScanNodes), true, "If set to true, will hide the subtext section of scannables that do not have subtext or scrap value.");
            HidePlayerNames = Config.Bind(UISection, nameof(HidePlayerNames), false, "If set to true, player names will no longer show above players.");
            ShowHitPoints = Config.Bind(UISection, nameof(ShowHitPoints), true, "If set to true, the HUD will display your current remaining hitpoints.");
            ShowLightningWarnings = Config.Bind(UISection, nameof(ShowLightningWarnings), true, "If set to true, the inventory slots will flash electrically when an item in the slot is being targeted by lightning.");
            ShowUIReticle = Config.Bind(UISection, nameof(ShowUIReticle), false, "If set to true, the HUD will display a small dot so you can see exactly where you are pointing at all times.");
            TwentyFourHourClock = Config.Bind(UISection, nameof(TwentyFourHourClock), false, "If set to true, the clock will be 24 hours instead of 12.");

            // Sanitize where needed
            string backgroundHex = Regex.Match(MonitorBackgroundColor.Value, "([a-fA-F0-9]{6})").Groups[1].Value.ToUpper();
            string textHex = Regex.Match(MonitorTextColor.Value, "([a-fA-F0-9]{6})").Groups[1].Value.ToUpper();
            if (backgroundHex.Length != 6) MLS.LogWarning("Invalid hex code used for monitor background color! Reverting to default.");
            if (textHex.Length != 6) MLS.LogWarning("Invalid hex code used for monitor text color! Reverting to default.");
            MonitorBackgroundColor.Value = backgroundHex.Length == 6 ? backgroundHex : MonitorBackgroundColor.DefaultValue.ToString();
            MonitorBackgroundColorVal = HexToColor(MonitorBackgroundColor.Value);
            MonitorTextColor.Value = textHex.Length == 6 ? textHex : MonitorTextColor.DefaultValue.ToString();
            MonitorTextColorVal = HexToColor(MonitorTextColor.Value);

            // Handle custom scannable tools parsing
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

            // Sanitize weather multipliers by removing invalid entries and clamping the multipliers
            var scrapValueWeatherMatches = Regex.Matches(ScrapValueWeatherMultipliers.Value, @"[a-z]+:\d*([\. ]\d+)?", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
            var scrapAmountWeatherMatches = Regex.Matches(ScrapAmountWeatherMultipliers.Value, @"[a-z]+:\d([\. ]\d+)?", RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);
            List<string[]> sanitizedScrapValues = new List<string[]>();
            List<string[]> sanitizedScrapAmounts = new List<string[]>();
            foreach (Match valueMatch in scrapValueWeatherMatches)
            {
                string[] curSplit = valueMatch.Value.Split(":");
                if (float.TryParse(curSplit[1], out var multiplier))
                {
                    sanitizedScrapValues.Add(new[] { curSplit[0], Mathf.Clamp(multiplier, 0.1f, 2f).ToString() });
                }
            }
            foreach (Match amountMatch in scrapAmountWeatherMatches)
            {
                string[] curSplit = amountMatch.Value.Split(":");
                if (float.TryParse(curSplit[1], out var multiplier))
                {
                    sanitizedScrapAmounts.Add(new[] { curSplit[0], Mathf.Clamp(multiplier, 1f, 5f).ToString() });
                }
            }
            ScrapValueWeatherMultipliers.Value = string.Join(", ", sanitizedScrapValues.Select(s => $"{s[0]}:{s[1]}"));
            SanitizedScrapValueWeatherMultipliers = sanitizedScrapValues.ToDictionary(k => k[0], v => float.Parse(v[1]));
            ScrapAmountWeatherMultipliers.Value = string.Join(", ", sanitizedScrapAmounts.Select(s => $"{s[0]}:{s[1]}"));
            SanitizedScrapAmountWeatherMultipliers = sanitizedScrapAmounts.ToDictionary(k => k[0], v => float.Parse(v[1]));
        }

        private static Color HexToColor(string hex)
        {
            float r = int.Parse(hex[..2], NumberStyles.HexNumber) / 255f;
            float g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber) / 255f;
            float b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber) / 255f;

            return new Color(r, g, b);
        }

        private void MigrateOldConfigValues()
        {
            try
            {
                // Migrate and clear any orphans
                if (Config?.OrphanedEntries?.Any() ?? false)
                {
                    foreach (var orphan in Config.OrphanedEntries)
                    {
                        MigrateSpecificValue(orphan);
                    }

                    Config.OrphanedEntries.Clear();
                    Config.Save();
                }
            }
            catch (Exception ex)
            {
                MLS.LogError($"Error encountered while migrating old config values! This will not affect gameplay, but please verify your config file to ensure the settings are as you expect.\n\n{ex}");
            }
        }

        private void MigrateSpecificValue(KeyValuePair<ConfigDefinition, string> entry)
        {
            MLS.LogMessage($"Found unused config value: {entry.Key.Key}. Migrating and removing if possible...");

            void convertMonitor(eMonitorNames s)
            {
                if (int.TryParse(entry.Value, out var num) && num >= 1 && num <= ShipMonitorAssignments.Length)
                {
                    MLS.LogInfo($"Migrating {s} to monitor position {num}.");
                    ShipMonitorAssignments[num - 1].Value = s;
                }
            }

            switch (entry.Key.Key)
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

                // Things under fixes section moving to scanner section
                case "FixPersonalScanner": FixPersonalScanner.Value = entry.Value.ToUpper() == "TRUE"; break;

                // Indecisive masked entity renaming and reorganizing
                case "MaskedLookLikePlayers":
                    bool.TryParse(entry.Value, out var maskedLookLikePlayers);
                    MaskedEntitiesWearMasks.Value = !maskedLookLikePlayers;
                    MaskedEntitiesShowPlayerNames.Value = maskedLookLikePlayers;
                    MaskedEntitiesCopyPlayerLooks.Value = maskedLookLikePlayers ? eMaskedEntityCopyLook.SuitAndCosmetics : eMaskedEntityCopyLook.None;
                    MaskedEntitiesSpinOnRadar.Value = !maskedLookLikePlayers;
                    MaskedEntitiesReachTowardsPlayer.Value = !maskedLookLikePlayers;
                    break;
                case "MaskedEntityBlendLevel":
                    string maskBlendLevel = entry.Value.ToUpper();
                    MaskedEntitiesWearMasks.Value = new[] { "NONE", "JUSTCOPYSUIT", "JUSTCOPYSUITANDCOSMETICS" }.Contains(maskBlendLevel);
                    MaskedEntitiesShowPlayerNames.Value = maskBlendLevel == "FULL";
                    switch (maskBlendLevel)
                    {
                        case "JUSTCOPYSUIT": case "NOMASKANDCOPYSUIT": MaskedEntitiesCopyPlayerLooks.Value = eMaskedEntityCopyLook.Suit; break;
                        case "JUSTCOPYSUITANDCOSMETICS": case "FULL": MaskedEntitiesCopyPlayerLooks.Value = eMaskedEntityCopyLook.SuitAndCosmetics; break;
                        default: MaskedEntitiesCopyPlayerLooks.Value = eMaskedEntityCopyLook.None; break;
                    }
                    MaskedEntitiesSpinOnRadar.Value = maskBlendLevel != "FULL";
                    MaskedEntitiesReachTowardsPlayer.Value = maskBlendLevel != "FULL";
                    break;

                // Settings that were converted from bools to enums
                case "SaveShipFurniturePlaces": SaveShipFurniturePlaces.Value = bool.TryParse(entry.Value, out _) ? eSaveFurniturePlacement.All : eSaveFurniturePlacement.None; break;
                case "ShipMapCamDueNorth": ShipMapCamRotation.Value = bool.TryParse(entry.Value, out _) ? eShipCamRotation.North : eShipCamRotation.None; break;

                // Misc
                case "AllowOvertimeBonus": OvertimeBonusType.Value = bool.Parse(entry.Value) ? eOvertimeBonusType.Vanilla : eOvertimeBonusType.Disabled; break;

                case "StartingMoneyPerPlayer":
                    var startingMoneyPerPlayerVal = int.Parse(entry.Value);
                    if (startingMoneyPerPlayerVal >= 0) StartingMoney.Value = startingMoneyPerPlayerVal;

                    // If the minimum value was something other than the old default, they will either be using per player or total
                    StartingMoneyFunction.Value = MinimumStartingMoney.Value != 30 ?
                        (startingMoneyPerPlayerVal >= 0 ? eStartingMoneyFunction.PerPlayerWithMinimum : eStartingMoneyFunction.Total)
                        : startingMoneyPerPlayerVal >= 0 ? eStartingMoneyFunction.PerPlayer
                        : eStartingMoneyFunction.Disabled;
                    break;

                case "DisableShipCamPostProcessing":
                    bool.TryParse(entry.Value, out var oldShipCamPostProcessing);
                    DisableInternalShipCamPostProcessing.Value = oldShipCamPostProcessing;
                    DisableExternalShipCamPostProcessing.Value = oldShipCamPostProcessing;
                    break;

                default:
                    MLS.LogDebug("No matching migration");
                    break;
            }
        }
    }
}