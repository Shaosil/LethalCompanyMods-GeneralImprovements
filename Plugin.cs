using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GeneralImprovements.OtherMods;
using GeneralImprovements.Patches;
using HarmonyLib;
using System;

namespace GeneralImprovements
{
    [BepInPlugin(Metadata.GUID, Metadata.PLUGIN_NAME, Metadata.VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource MLS { get; private set; }

        private const string GeneralSection = "General";
        public static ConfigEntry<bool> SkipStartupScreen { get; private set; }
        public static ConfigEntry<string> AutoSelectLaunchMode { get; private set; }
        public static ConfigEntry<bool> PickupInOrder { get; private set; }
        public static ConfigEntry<bool> RearrangeOnDrop { get; private set; }
        public static ConfigEntry<bool> TwoHandedInSlotOne { get; private set; }
        public static ConfigEntry<float> ScrollDelay { get; private set; }
        public static ConfigEntry<int> TerminalHistoryItemCount { get; private set; }

        private const string TweaksSection = "Tweaks";
        public static ConfigEntry<int> StartingMoneyPerPlayer { get; private set; }
        public static int StartingMoneyPerPlayerVal => Math.Clamp(StartingMoneyPerPlayer.Value, -1, 1000);
        public static ConfigEntry<int> SnapObjectsByDegrees { get; private set; }
        public static ConfigEntry<bool> ShipMapCamDueNorth { get; private set; }
        public static ConfigEntry<bool> ToolsDoNotAttractLightning { get; private set; }

        private void Awake()
        {
            MLS = Logger;

            SkipStartupScreen = Config.Bind(GeneralSection, nameof(SkipStartupScreen), true, "Skips the main menu loading screen bootup animation.");
            AutoSelectLaunchMode = Config.Bind(GeneralSection, nameof(AutoSelectLaunchMode), string.Empty, "If set to 'ONLINE' or 'LAN', will automatically launch the correct mode, saving you from having to click the menu option when the game loads.");
            PickupInOrder = Config.Bind(GeneralSection, nameof(PickupInOrder), true, "When picking up items, will always put them in left - right order.");
            RearrangeOnDrop = Config.Bind(GeneralSection, nameof(RearrangeOnDrop), true, "When dropping items, will rearrange other inventory items to ensure slots are filled left - right.");
            TwoHandedInSlotOne = Config.Bind(GeneralSection, nameof(TwoHandedInSlotOne), true, $"When picking up a two handed item, it will always place it in slot 1 and shift things to the right if needed. Makes selling quicker when paired with {nameof(RearrangeOnDrop)}.");
            ScrollDelay = Config.Bind(GeneralSection, nameof(ScrollDelay), 0.1f, "The minimum time you must wait to scroll to another item in your inventory. Ignores values outside of 0.05 - 0.3. Vanilla: 0.3.");
            TerminalHistoryItemCount = Config.Bind(GeneralSection, nameof(TerminalHistoryItemCount), 10, "How many items to keep in your terminal's command history. Ignores values outside of 0 - 100. Previous terminal commands may be navigated by using the up/down arrow keys.");
            StartingMoneyPerPlayer = Config.Bind(TweaksSection, nameof(StartingMoneyPerPlayer), 30, "How much starting money the group gets per player. Set to -1 to disable. Ignores values outside of -1 - 1000. Adjusts money as players join and leave, until the game starts.");
            SnapObjectsByDegrees = Config.Bind(TweaksSection, nameof(SnapObjectsByDegrees), 45, "Build mode will switch to snap turning (press instead of hold) by this many degrees at a time. Setting it to 0 uses vanilla behavior. Must be an interval of 15 and go evenly into 360.");
            ShipMapCamDueNorth = Config.Bind(TweaksSection, nameof(ShipMapCamDueNorth), false, "If set to true, the ship's map camera will rotate so that it faces north evenly, instead of showing everything at an angle.");
            ToolsDoNotAttractLightning = Config.Bind(TweaksSection, nameof(ToolsDoNotAttractLightning), false, "If set to true, all useful tools (jetpacks, keys, radar boosters, shovels & signs, tzp inhalant, and zap guns) will no longer attract lighning.");
            MLS.LogDebug("Configuration Initialized.");

            Harmony.CreateAndPatchAll(GetType().Assembly);

            Harmony.CreateAndPatchAll(typeof(DepositItemsDeskPatch));
            MLS.LogDebug("DepositItemsDesk patched.");

            Harmony.CreateAndPatchAll(typeof(EntranceTeleportPatch));
            MLS.LogDebug("EntranceTeleport patched.");

            Harmony.CreateAndPatchAll(typeof(GrabbableObjectsPatch));
            MLS.LogDebug("GrabbableObjects patched.");

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

            Harmony.CreateAndPatchAll(typeof(TerminalPatch));
            MLS.LogDebug("Terminal patched.");

            // Load info about any external mods
            ReservedItemSlotCoreHelper.Initialize();
            AdvancedCompanyHelper.Initialize();

            MLS.LogInfo($"{Metadata.PLUGIN_NAME} v{Metadata.VERSION} fully loaded.");
        }
    }
}