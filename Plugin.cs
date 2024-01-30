using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GeneralImprovements.OtherMods;
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
using static GeneralImprovements.Utilities.MonitorsHelper;

namespace GeneralImprovements
{
    [BepInPlugin(Metadata.GUID, Metadata.PLUGIN_NAME, Metadata.VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource MLS { get; private set; }

        private const string ExtraMonitorsSection = "ExtraMonitors";
        public static ConfigEntry<bool> UseBetterMonitors { get; private set; }
        public static ConfigEntry<bool> ShowBlueMonitorBackground { get; private set; }
        public static ConfigEntry<string> MonitorBackgroundColor { get; private set; }
        public static Color MonitorBackgroundColorVal { get; private set; }
        public static ConfigEntry<string> MonitorTextColor { get; private set; }
        public static Color MonitorTextColorVal { get; private set; }
        public static ConfigEntry<bool> ShowBackgroundOnAllScreens { get; private set; }
        public static ConfigEntry<string>[] ShipMonitorAssignments { get; private set; }
        public static ConfigEntry<bool> SyncExtraMonitorsPower { get; private set; }
        public static ConfigEntry<bool> CenterAlignMonitorText { get; private set; }
        public static ConfigEntry<int> ShipInternalCamSizeMultiplier { get; private set; }
        public static ConfigEntry<int> ShipInternalCamFPS { get; private set; }
        public static ConfigEntry<int> ShipExternalCamSizeMultiplier { get; private set; }
        public static ConfigEntry<int> ShipExternalCamFPS { get; private set; }

        private const string FixesSection = "Fixes";
        public static ConfigEntry<bool> FixInternalFireExits { get; private set; }
        public static ConfigEntry<bool> FixItemsFallingThrough { get; private set; }
        public static ConfigEntry<bool> FixPersonalScanner { get; private set; }
        public static ConfigEntry<bool> ScanHeldPlayerItems { get; private set; }

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
        public static ConfigEntry<bool> SpeakerPlaysIntroVoice { get; private set; }

        private const string TeleportersSection = "Teleporters";
        public static ConfigEntry<int> RegularTeleporterCooldown { get; private set; }
        public static ConfigEntry<int> InverseTeleporterCooldown { get; private set; }

        private const string TerminalSection = "Terminal";
        public static ConfigEntry<int> TerminalHistoryItemCount { get; private set; }
        public static ConfigEntry<bool> TerminalFastCamSwitch { get; private set; }
        public static ConfigEntry<bool> LockCameraAtTerminal { get; private set; }

        private const string ToolsSection = "Tools";
        public static ConfigEntry<string> ScannableTools { get; private set; }
        public static List<Type> ScannableToolVals { get; private set; } = new List<Type>();
        public static ConfigEntry<bool> ToolsDoNotAttractLightning { get; private set; }

        private const string UISection = "UI";
        public static ConfigEntry<bool> ShowUIReticle { get; private set; }
        public static ConfigEntry<bool> HideEmptySubtextOfScanNodes { get; private set; }
        public static ConfigEntry<bool> ShowHitPoints { get; private set; }

        private void Awake()
        {
            string[] validMonitors = new[] { "" }.Concat(typeof(MonitorNames).GetFields(BindingFlags.Static | BindingFlags.Public).Select(f => f.Name)).ToArray();

            var validKeys = Enum.GetValues(typeof(Key)).Cast<int>().Where(e => e < (int)Key.OEM1).Select(e => Enum.GetName(typeof(Key), e))
                .Concat(Enum.GetValues(typeof(ShipBuildModeManagerPatch.MouseButton)).Cast<int>().Select(e => Enum.GetName(typeof(ShipBuildModeManagerPatch.MouseButton), e))).ToArray();

            var validSnapRotations = Enumerable.Range(0, 360 / 15).Select(n => n * 15).Where(n => n == 0 || 360 % n == 0).ToArray();

            var validToolTypes = new List<Type> { typeof(BoomboxItem), typeof(ExtensionLadderItem), typeof(FlashlightItem), typeof(JetpackItem), typeof(LockPicker), typeof(RadarBoosterItem),
                                                typeof(Shovel), typeof(SprayPaintItem), typeof(StunGrenadeItem), typeof(TetraChemicalItem), typeof(WalkieTalkie), typeof(PatcherTool) };
            var validToolStrings = string.Join(", ", new[] { "All" }.Concat(validToolTypes.Select(t => t.Name)));

            MLS = Logger;

            // Extra monitors
            UseBetterMonitors = Config.Bind(ExtraMonitorsSection, nameof(UseBetterMonitors), false, "If set to true, uses 12 fully customizable and integrated monitors instead of the 8 vanilla ones with overlays. If true, 1-6 are top, 7-12 are bottom, and 13-14 are the big ones beside the terminal. Otherwise, 1-4 are the top, and 5-8 are on the bottom.");
            ShowBlueMonitorBackground = Config.Bind(ExtraMonitorsSection, nameof(ShowBlueMonitorBackground), true, "If set to true and NOT using UseBetterMonitors, keeps the vanilla blue backgrounds on the extra monitors. Set to false to hide.");
            MonitorBackgroundColor = Config.Bind(ExtraMonitorsSection, nameof(MonitorBackgroundColor), "160959", "The hex color code of what the backgrounds of the monitors should be. A recommended value close to black is 050505.");
            MonitorTextColor = Config.Bind(ExtraMonitorsSection, nameof(MonitorTextColor), "00FF2C", "The hex color code of what the text on the monitors should be.");
            ShowBackgroundOnAllScreens = Config.Bind(ExtraMonitorsSection, nameof(ShowBackgroundOnAllScreens), false, "If set to true, will show the MonitorBackgroundColor on ALL monitors when they are on, not just used ones.");
            ShipMonitorAssignments = new ConfigEntry<string>[MonitorCount];
            for (int i = 0; i < ShipMonitorAssignments.Length; i++)
            {
                string defaultVal = i == 4 ? MonitorNames.ProfitQuota : i == 5 ? MonitorNames.Deadline : i == 11 ? MonitorNames.InternalCam : i == 14 ? MonitorNames.ExternalCam : string.Empty;
                ShipMonitorAssignments[i] = Config.Bind(ExtraMonitorsSection, $"ShipMonitor{i + 1}", defaultVal, new ConfigDescription($"What to display on the ship monitor at position {i + 1}, if anything.", new AcceptableValueList<string>(validMonitors)));
            }
            SyncExtraMonitorsPower = Config.Bind(ExtraMonitorsSection, nameof(SyncExtraMonitorsPower), true, "If set to true, The smaller monitors above the map screen will turn off and on when the map screen power is toggled.");
            CenterAlignMonitorText = Config.Bind(ExtraMonitorsSection, nameof(CenterAlignMonitorText), true, "If set to true, all small monitors in the ship will have their text center aligned, instead of left.");
            ShipInternalCamSizeMultiplier = Config.Bind(ExtraMonitorsSection, nameof(ShipInternalCamSizeMultiplier), 1, new ConfigDescription($"How many times to double the internal ship cam's resolution.", new AcceptableValueRange<int>(1, 5)));
            ShipInternalCamFPS = Config.Bind(ExtraMonitorsSection, nameof(ShipInternalCamFPS), 0, new ConfigDescription($"Limits the FPS of the internal ship cam for performance. 0 = Unrestricted.", new AcceptableValueRange<int>(0, 30)));
            ShipExternalCamSizeMultiplier = Config.Bind(ExtraMonitorsSection, nameof(ShipExternalCamSizeMultiplier), 1, new ConfigDescription($"How many times to double the external ship cam's resolution.", new AcceptableValueRange<int>(1, 5)));
            ShipExternalCamFPS = Config.Bind(ExtraMonitorsSection, nameof(ShipExternalCamFPS), 0, new ConfigDescription($"Limits the FPS of the external ship cam for performance. 0 = Unrestricted.", new AcceptableValueRange<int>(0, 30)));

            // Fixes
            FixInternalFireExits = Config.Bind(FixesSection, nameof(FixInternalFireExits), true, "If set to true, the player will face the interior of the facility when entering through a fire entrance.");
            FixItemsFallingThrough = Config.Bind(FixesSection, nameof(FixItemsFallingThrough), true, "Fixes items falling through furniture on the ship when loading the game.");
            FixPersonalScanner = Config.Bind(FixesSection, nameof(FixPersonalScanner), false, "If set to true, will tweak the behavior of the scan action and more reliably ping items closer to you, and the ship/main entrance.");
            ScanHeldPlayerItems = Config.Bind(FixesSection, nameof(ScanHeldPlayerItems), false, "If this and FixPersonalScanner are set to true, the scanner will also ping items in other players' hands.");

            // Game Launch
            SkipStartupScreen = Config.Bind(GameLaunchSection, nameof(SkipStartupScreen), true, "Skips the main menu loading screen bootup animation.");
            AutoSelectLaunchMode = Config.Bind(GameLaunchSection, nameof(AutoSelectLaunchMode), string.Empty, new ConfigDescription("If set to 'ONLINE' or 'LAN', will automatically launch the correct mode, saving you from having to click the menu option when the game loads.", new AcceptableValueList<string>(string.Empty, "ONLINE", "LAN")));

            // Inventory
            PickupInOrder = Config.Bind(InventorySection, nameof(PickupInOrder), false, "When picking up items, will always put them in left - right order.");
            RearrangeOnDrop = Config.Bind(InventorySection, nameof(RearrangeOnDrop), false, "When dropping items, will rearrange other inventory items to ensure slots are filled left - right.");
            TwoHandedInSlotOne = Config.Bind(InventorySection, nameof(TwoHandedInSlotOne), false, $"When picking up a two handed item, it will always place it in slot 1 and shift things to the right if needed. Makes selling quicker when paired with RearrangeOnDrop.");
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
            SpeakerPlaysIntroVoice = Config.Bind(ShipSection, nameof(SpeakerPlaysIntroVoice), true, "If set to true, the ship's speaker will play the introductory welcome audio on the first day.");

            // Teleporters
            RegularTeleporterCooldown = Config.Bind(TeleportersSection, nameof(RegularTeleporterCooldown), 10, new ConfigDescription("How many seconds to wait in between button presses for the REGULAR teleporter. Vanilla = 10.", new AcceptableValueRange<int>(0, 300)));
            InverseTeleporterCooldown = Config.Bind(TeleportersSection, nameof(InverseTeleporterCooldown), 10, new ConfigDescription("How many seconds to wait in between button presses for the INVERSE teleporter. Vanilla = 210.", new AcceptableValueRange<int>(0, 300)));

            // Terminal
            TerminalHistoryItemCount = Config.Bind(TerminalSection, nameof(TerminalHistoryItemCount), 20, new ConfigDescription("How many items to keep in your terminal's command history. Previous terminal commands may be navigated by using the up/down arrow keys.", new AcceptableValueRange<int>(0, 100)));
            TerminalFastCamSwitch = Config.Bind(TerminalSection, nameof(TerminalFastCamSwitch), true, "If set to true, will allow use of the left/right arrow keys to quickly cycle through radar cameras while using the terminal.");
            LockCameraAtTerminal = Config.Bind(TerminalSection, nameof(LockCameraAtTerminal), true, "If set to true, the camera will no longer move around when moving your mouse/controller while at the terminal.");

            // Tools
            ScannableTools = Config.Bind(ToolsSection, nameof(ScannableTools), string.Empty, $"A comma separated list of which tools, if any, should be scannable. Accepted values: {validToolStrings}");
            ToolsDoNotAttractLightning = Config.Bind(ToolsSection, nameof(ToolsDoNotAttractLightning), false, "[Host Only] If set to true, all useful tools (jetpacks, keys, radar boosters, shovels & signs, tzp inhalant, and zap guns) will no longer attract lighning.");

            // UI
            HideEmptySubtextOfScanNodes = Config.Bind(UISection, nameof(HideEmptySubtextOfScanNodes), true, "If set to true, will hide the subtext section of scannables that do not have subtext or scrap value.");
            ShowUIReticle = Config.Bind(UISection, nameof(ShowUIReticle), false, "If set to true, the HUD will display a small dot so you can see exactly where you are pointing at all times.");
            ShowHitPoints = Config.Bind(UISection, nameof(ShowHitPoints), true, "If set to true, the HUD will display your current remaining hitpoints.");

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

            MigrateOldConfigValues();
            MLS.LogDebug("Configuration Initialized.");

            Harmony.CreateAndPatchAll(typeof(DepositItemsDeskPatch));
            MLS.LogDebug("DepositItemsDesk patched.");

            Harmony.CreateAndPatchAll(typeof(EntranceTeleportPatch));
            MLS.LogDebug("EntranceTeleport patched.");

            Harmony.CreateAndPatchAll(typeof(GameNetworkManagerPatch));
            MLS.LogDebug("GameNetworkManager patched.");

            Harmony.CreateAndPatchAll(typeof(GrabbableObjectsPatch));
            MLS.LogDebug("GrabbableObjects patched.");

            Harmony.CreateAndPatchAll(typeof(HangarShipDoorPatch));
            MLS.LogDebug("HangarShipDoor patched.");

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
            TwoRadarCamsHelper.Initialize();
            AssetBundleHelper.Initialize();
            GameNetworkManagerPatch.PatchNetcode();

            MLS.LogInfo($"{Metadata.PLUGIN_NAME} v{Metadata.VERSION} fully loaded.");
        }

        private static Color HexToColor(string hex)
        {
            float r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber) / 255f;
            float g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber) / 255f;
            float b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber) / 255f;

            return new Color(r, g, b);
        }

        private void MigrateOldConfigValues()
        {
            try
            {
                string[] configLines = File.ReadAllLines(Config.ConfigFilePath);
                var ourEntries = new Dictionary<string, List<InternalConfigDef>> { { string.Empty, new List<InternalConfigDef>() } };
                string curSection = string.Empty;
                string curDescription = string.Empty;

                // Manually read sections and entries since the config classes don't provide a way to see unused values
                foreach (string line in configLines.Select(l => l.Trim()))
                {
                    if (line.StartsWith('#'))
                    {
                        if (line.StartsWith("##")) curDescription = line.Substring(2);
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
                        ourEntries[curSection].Add(new InternalConfigDef(curSection, curDescription, entry[0].Trim(), entry[1].Trim()));
                    }
                }

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

            Action<string> convertMonitor = s =>
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
                case "ShipProfitQuotaMonitorNum": convertMonitor(MonitorNames.ProfitQuota); break;
                case "ShipDeadlineMonitorNum": convertMonitor(MonitorNames.Deadline); break;
                case "ShipTotalMonitorNum": convertMonitor(MonitorNames.ShipScrap); break;
                case "ShipTimeMonitorNum": convertMonitor(MonitorNames.Time); break;
                case "ShipWeatherMonitorNum": convertMonitor(MonitorNames.Weather); break;
                case "FancyWeatherMonitor":
                    int existingWeatherIndex = MonitorNames.GetMonitorIndex(MonitorNames.Weather);
                    if (entry.Value.ToUpper() == "TRUE" && existingWeatherIndex >= 0)
                    {
                        // Unless manually modified, the new weather monitor would have been migrated at this point so it's safe to overwrite it here
                        MLS.LogInfo($"Migrating fancy weather to override weather at monitor position {existingWeatherIndex + 1}");
                        ShipMonitorAssignments[existingWeatherIndex].Value = MonitorNames.FancyWeather;
                    }
                    break;
                case "ShipSalesMonitorNum": convertMonitor(MonitorNames.Sales); break;
                case "ShipInternalCamMonitorNum": convertMonitor(MonitorNames.InternalCam); break;
                case "ShipExternalCamMonitorNum": convertMonitor(MonitorNames.ExternalCam); break;

                default:
                    MLS.LogDebug("No matching migration");
                    break;
            }
        }

        private class InternalConfigDef
        {
            public readonly string Section;
            public readonly string Description;
            public readonly string Name;
            public readonly string Value;

            public InternalConfigDef(string section, string description, string name, string value)
            {
                Section = section;
                Description = description;
                Name = name;
                Value = value;
            }
        }
    }
}