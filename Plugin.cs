using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GeneralImprovements.OtherMods;
using GeneralImprovements.Patches;
using GeneralImprovements.Utilities;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;

namespace GeneralImprovements
{
    [BepInPlugin(Metadata.GUID, Metadata.PLUGIN_NAME, Metadata.VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource MLS { get; private set; }

        private const string ExtraMonitorsSection = "ExtraMonitors";
        public static ConfigEntry<bool> ShowBlueMonitorBackground { get; private set; }
        public static ConfigEntry<int> ShipTotalMonitorNum { get; private set; }
        public static ConfigEntry<int> ShipTimeMonitorNum { get; private set; }
        public static ConfigEntry<int> ShipWeatherMonitorNum { get; private set; }
        public static ConfigEntry<bool> FancyWeatherMonitor { get; private set; }
        public static ConfigEntry<int> ShipSalesMonitorNum { get; private set; }
        public static ConfigEntry<bool> SyncExtraMonitorsPower { get; private set; }

        private const string FixesSection = "Fixes";
        public static ConfigEntry<bool> FixInternalFireExits { get; private set; }
        public static ConfigEntry<bool> FixItemsFallingThrough { get; private set; }
        public static ConfigEntry<bool> FixPersonalScanner { get; private set; }

        private const string GameLaunchSection = "GameLaunch";
        public static ConfigEntry<bool> SkipStartupScreen { get; private set; }
        public static ConfigEntry<string> AutoSelectLaunchMode { get; private set; }

        private const string InventorySection = "Inventory";
        public static ConfigEntry<bool> PickupInOrder { get; private set; }
        public static ConfigEntry<bool> RearrangeOnDrop { get; private set; }
        public static ConfigEntry<bool> TwoHandedInSlotOne { get; private set; }
        public static ConfigEntry<float> ScrollDelay { get; private set; }

        private const string MechanicsSection = "Mechanics";
        public static ConfigEntry<int> StartingMoneyPerPlayer { get; private set; }
        public static int StartingMoneyPerPlayerVal => Math.Clamp(StartingMoneyPerPlayer.Value, -1, 1000);
        public static ConfigEntry<int> MinimumStartingMoney { get; private set; }
        public static int MinimumStartingMoneyVal => Math.Clamp(MinimumStartingMoney.Value, StartingMoneyPerPlayerVal, 1000);
        public static ConfigEntry<bool> AllowQuotaRollover { get; private set; }
        public static ConfigEntry<bool> AddHealthRechargeStation { get; private set; }

        private const string ShipSection = "Ship";
        public static ConfigEntry<bool> HideClipboardAndStickyNote { get; private set; }
        public static ConfigEntry<int> SnapObjectsByDegrees { get; private set; }
        public static ConfigEntry<string> FreeRotateKey { get; private set; }
        public static ConfigEntry<string> CounterClockwiseKey { get; private set; }
        public static ConfigEntry<bool> ShipMapCamDueNorth { get; private set; }

        private const string TeleportersSection = "Teleporters";
        public static ConfigEntry<int> RegularTeleporterCooldown { get; private set; }
        public static ConfigEntry<int> InverseTeleporterCooldown { get; private set; }

        private const string TerminalSection = "Terminal";
        public static ConfigEntry<int> TerminalHistoryItemCount { get; private set; }
        public static ConfigEntry<bool> TerminalFastCamSwitch { get; private set; }

        private const string ToolsSection = "Tools";
        public static ConfigEntry<string> ScannableTools { get; private set; }
        public static List<Type> ScannableToolVals { get; private set; } = new List<Type>();
        public static ConfigEntry<bool> ToolsDoNotAttractLightning { get; private set; }

        private const string UISection = "UI";
        public static ConfigEntry<bool> ShowUIReticle { get; private set; }
        public static ConfigEntry<bool> HideEmptySubtextOfScanNodes { get; private set; }

        private void Awake()
        {
            var validKeys = Enum.GetValues(typeof(Key)).Cast<int>().Where(e => e < (int)Key.OEM1).Select(e => Enum.GetName(typeof(Key), e))
                .Concat(Enum.GetValues(typeof(ShipBuildModeManagerPatch.MouseButton)).Cast<int>().Select(e => Enum.GetName(typeof(ShipBuildModeManagerPatch.MouseButton), e))).ToArray();

            var validSnapRotations = Enumerable.Range(0, 360 / 15).Select(n => n * 15).Where(n => n == 0 || 360 % n == 0).ToArray();

            var validToolTypes = new List<Type> { typeof(BoomboxItem), typeof(ExtensionLadderItem), typeof(FlashlightItem), typeof(JetpackItem), typeof(LockPicker), typeof(RadarBoosterItem),
                                                typeof(Shovel), typeof(SprayPaintItem), typeof(StunGrenadeItem), typeof(TetraChemicalItem), typeof(WalkieTalkie), typeof(PatcherTool) };
            var validToolStrings = string.Join(", ", "All", validToolTypes.ToArray());

            MLS = Logger;

            // Extra monitors
            string numDesc = "0 = DISABLED. 1-4 are the top row monitors above the ship starting lever, from left to right. 5-6 are below 3 and 4, respectively.";
            ShowBlueMonitorBackground = Config.Bind(ExtraMonitorsSection, nameof(ShowBlueMonitorBackground), true, "If set to true, keeps the vanilla blue backgrounds on the extra monitors. Set to false for black.");
            ShipTotalMonitorNum = Config.Bind(ExtraMonitorsSection, nameof(ShipTotalMonitorNum), 0, new ConfigDescription($"Displays the sum of all scrap value on the ship on the specified monitor. {numDesc}", new AcceptableValueRange<int>(0, 6)));
            ShipTimeMonitorNum = Config.Bind(ExtraMonitorsSection, nameof(ShipTimeMonitorNum), 0, new ConfigDescription($"Displays current time on the specified monitor. {numDesc}", new AcceptableValueRange<int>(0, 6)));
            ShipWeatherMonitorNum = Config.Bind(ExtraMonitorsSection, nameof(ShipWeatherMonitorNum), 0, new ConfigDescription($"Displays the current moon's weather on the specified monitor. {numDesc}", new AcceptableValueRange<int>(0, 6)));
            ShipSalesMonitorNum = Config.Bind(ExtraMonitorsSection, nameof(ShipSalesMonitorNum), 0, new ConfigDescription($"Displays info about current sales on the specified monitor. {numDesc}", new AcceptableValueRange<int>(0, 6)));
            FancyWeatherMonitor = Config.Bind(ExtraMonitorsSection, nameof(FancyWeatherMonitor), true, "If set to true and paired with ShowShipWeatherMonitor, the weather monitor will display ASCII art instead of text descriptions.");
            SyncExtraMonitorsPower = Config.Bind(ExtraMonitorsSection, nameof(SyncExtraMonitorsPower), true, "If set to true, The smaller monitors above the map screen will turn off and on when the map screen power is toggled.");

            // Fixes
            FixInternalFireExits = Config.Bind(FixesSection, nameof(FixInternalFireExits), true, "If set to true, the player will face the interior of the facility when entering through a fire entrance.");
            FixItemsFallingThrough = Config.Bind(FixesSection, nameof(FixItemsFallingThrough), true, "Fixes items falling through furniture on the ship when loading the game.");
            FixPersonalScanner = Config.Bind(FixesSection, nameof(FixPersonalScanner), false, "If set to true, will tweak the behavior of the scan action and more reliably ping items closer to you, and the ship/main entrance.");

            // Game Launch
            SkipStartupScreen = Config.Bind(GameLaunchSection, nameof(SkipStartupScreen), true, "Skips the main menu loading screen bootup animation.");
            AutoSelectLaunchMode = Config.Bind(GameLaunchSection, nameof(AutoSelectLaunchMode), string.Empty, new ConfigDescription("If set to 'ONLINE' or 'LAN', will automatically launch the correct mode, saving you from having to click the menu option when the game loads.", new AcceptableValueList<string>(string.Empty, "ONLINE", "LAN")));

            // Inventory
            PickupInOrder = Config.Bind(InventorySection, nameof(PickupInOrder), true, "When picking up items, will always put them in left - right order.");
            RearrangeOnDrop = Config.Bind(InventorySection, nameof(RearrangeOnDrop), true, "When dropping items, will rearrange other inventory items to ensure slots are filled left - right.");
            TwoHandedInSlotOne = Config.Bind(InventorySection, nameof(TwoHandedInSlotOne), true, $"When picking up a two handed item, it will always place it in slot 1 and shift things to the right if needed. Makes selling quicker when paired with RearrangeOnDrop.");
            ScrollDelay = Config.Bind(InventorySection, nameof(ScrollDelay), 0.1f, new ConfigDescription("The minimum time you must wait to scroll to another item in your inventory. Vanilla: 0.3.", new AcceptableValueRange<float>(0.05f, 0.3f)));

            // Mechanics
            StartingMoneyPerPlayer = Config.Bind(MechanicsSection, nameof(StartingMoneyPerPlayer), -1, new ConfigDescription("[Host Only] How much starting money the group gets per player. Set to -1 to disable. Adjusts money as players join and leave, until the game starts.", new AcceptableValueRange<int>(-1, 1000)));
            MinimumStartingMoney = Config.Bind(MechanicsSection, nameof(MinimumStartingMoney), 30, new ConfigDescription("[Host Only] When paired with StartingMoneyPerPlayer, will ensure a group always starts with at least this much money. Must be at least the value of StartingMoneyPerPlayer.", new AcceptableValueRange<int>(-1, 1000)));
            AllowQuotaRollover = Config.Bind(MechanicsSection, nameof(AllowQuotaRollover), false, "[Host Required] If set to true, will keep the surplus money remaining after selling things to the company, and roll it over to the next quota. If clients do not set this, they will see visual desyncs.");
            AddHealthRechargeStation = Config.Bind(MechanicsSection, nameof(AddHealthRechargeStation), false, "[Host Only] If set to true, a medical charging station will be above the ship's battery charger, and can be used to heal to full.");

            // Ship
            HideClipboardAndStickyNote = Config.Bind(ShipSection, nameof(HideClipboardAndStickyNote), false, "If set to true, the game will not show the clipboard or sticky note when the game loads.");
            SnapObjectsByDegrees = Config.Bind(ShipSection, nameof(SnapObjectsByDegrees), 45, new ConfigDescription("Build mode will switch to snap turning (press instead of hold) by this many degrees at a time. Setting it to 0 uses vanilla behavior.", new AcceptableValueList<int>(validSnapRotations)));
            FreeRotateKey = Config.Bind(ShipSection, nameof(FreeRotateKey), Key.LeftAlt.ToString(), new ConfigDescription("If SnapObjectsByDegrees > 0, configures which modifer key activates free rotation.", new AcceptableValueList<string>(validKeys)));
            CounterClockwiseKey = Config.Bind(ShipSection, nameof(CounterClockwiseKey), Key.LeftShift.ToString(), new ConfigDescription("If SnapObjectsByDegrees > 0, configures which modifier key spins it CCW.", new AcceptableValueList<string>(validKeys)));
            ShipMapCamDueNorth = Config.Bind(ShipSection, nameof(ShipMapCamDueNorth), false, "If set to true, the ship's map camera will rotate so that it faces north evenly, instead of showing everything at an angle.");

            // Teleporters
            RegularTeleporterCooldown = Config.Bind(TeleportersSection, nameof(RegularTeleporterCooldown), 10, new ConfigDescription("How many seconds to wait in between button presses for the REGULAR teleporter. Vanilla = 10.", new AcceptableValueRange<int>(0, 300)));
            InverseTeleporterCooldown = Config.Bind(TeleportersSection, nameof(InverseTeleporterCooldown), 10, new ConfigDescription("How many seconds to wait in between button presses for the INVERSE teleporter. Vanilla = 210.", new AcceptableValueRange<int>(0, 300)));

            // Terminal
            TerminalHistoryItemCount = Config.Bind(TerminalSection, nameof(TerminalHistoryItemCount), 20, new ConfigDescription("How many items to keep in your terminal's command history. Previous terminal commands may be navigated by using the up/down arrow keys.", new AcceptableValueRange<int>(0, 100)));
            TerminalFastCamSwitch = Config.Bind(TerminalSection, nameof(TerminalFastCamSwitch), true, "If set to true, will allow use of the left/right arrow keys to quickly cycle through radar cameras while using the terminal.");

            // Tools
            ScannableTools = Config.Bind(ToolsSection, nameof(ScannableTools), string.Empty, $"A comma separated list of which tools, if any, should be scannable. Accepted values: {validToolStrings}");
            ToolsDoNotAttractLightning = Config.Bind(ToolsSection, nameof(ToolsDoNotAttractLightning), false, "[Host Only] If set to true, all useful tools (jetpacks, keys, radar boosters, shovels & signs, tzp inhalant, and zap guns) will no longer attract lighning.");

            // UI
            HideEmptySubtextOfScanNodes = Config.Bind(UISection, nameof(HideEmptySubtextOfScanNodes), true, "If set to true, will hide the subtext section of scannables that do not have subtext or scrap value.");
            ShowUIReticle = Config.Bind(UISection, nameof(ShowUIReticle), false, "If set to true, the HUD will display a small dot so you can see exactly where you are pointing at all times.");

            // Sanitize where needed
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

            MLS.LogDebug("Configuration Initialized.");

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
            GameNetworkManagerPatch.PatchNetcode();

            MLS.LogInfo($"{Metadata.PLUGIN_NAME} v{Metadata.VERSION} fully loaded.");
        }
    }
}