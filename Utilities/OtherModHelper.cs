using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using GameNetcodeStuff;
using GeneralImprovements.Patches.Other;
using HarmonyLib;

namespace GeneralImprovements.Utilities
{
    internal static class OtherModHelper
    {
        private static bool _initialized = false;

        public const string BuyRateSettingsGUID = "MoonJuice.BuyRateSettings";
        public const string CodeRebirthGUID = "CodeRebirth";
        public const string MimicsGUID = "x753.Mimics";
        public const string TwoRadarCamsGUID = "Zaggy1024.TwoRadarMaps";
        public const string WeatherRegistryGUID = "mrov.WeatherRegistry";

        public static bool AdvancedCompanyActive { get; private set; }
        public static bool CodeRebirthActive { get; private set; }
        public static bool BuyRateSettingsActive { get; private set; }
        public static bool FlashlightFixActive { get; private set; }
        public static bool MimicsActive { get; private set; }
        public static bool ReservedItemSlotCoreActive { get; private set; }
        public static bool TwoRadarCamsActive { get; private set; }
        public static bool WeatherRegistryActive { get; private set; }

        public static ManualCameraRenderer TwoRadarCamsMapRenderer { get; set; }

        // Reflection information for ReservedItemSlotCore
        private static Type _reservedPlayerPatcherType;
        private static Type _reservedPlayerDataType;
        private static FieldInfo _reservedPlayerData;
        private static FieldInfo PlayerData => _reservedPlayerData ?? (_reservedPlayerData = _reservedPlayerPatcherType.GetField("allPlayerData", BindingFlags.NonPublic | BindingFlags.Static));
        private static MethodInfo _isReservedSlot;
        private static MethodInfo IsReservedSlot => _isReservedSlot ?? (_isReservedSlot = _reservedPlayerDataType.GetMethod("IsReservedItemSlot"));

        // Reflection information for WeatherRegistry
        private static Type _weatherManager;
        private static MethodInfo WeatherGetCurrentName => _weatherManager.GetMethod("GetCurrentWeatherName");

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            // Load reflection info for ReservedItemSlotCore
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var reservedItemSlotCoreAssembly = allAssemblies.FirstOrDefault(a => a.FullName.Contains("ReservedItemSlotCore,"));
            if (reservedItemSlotCoreAssembly != null)
            {
                _reservedPlayerPatcherType = reservedItemSlotCoreAssembly.GetType("ReservedItemSlotCore.Patches.PlayerPatcher");
                _reservedPlayerDataType = reservedItemSlotCoreAssembly.GetType("ReservedItemSlotCore.ReservedPlayerData");
            }

            var WeatherRegistryAssembly = allAssemblies.FirstOrDefault(a => a.FullName.Contains("WeatherRegistry,"));
            if (WeatherRegistryAssembly != null)
            {
                _weatherManager = WeatherRegistryAssembly.GetType("WeatherRegistry.WeatherManager");
            }

            // Set active statuses
            AdvancedCompanyActive = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName.Contains("AdvancedCompany,"));
            BuyRateSettingsActive = Chainloader.PluginInfos.ContainsKey(BuyRateSettingsGUID);
            CodeRebirthActive = Chainloader.PluginInfos.ContainsKey(CodeRebirthGUID);
            // Detect flashlight fix as active only if they are not on the latest versio that removes all code
            var flashlightFixPlugin = TypeLoader.FindPluginTypes(Paths.PluginPath, Chainloader.ToPluginInfo)
                .FirstOrDefault(p => p.Value.FirstOrDefault()?.Metadata.GUID == "ShaosilGaming.FlashlightFix").Value?.FirstOrDefault();
            FlashlightFixActive = flashlightFixPlugin != null && flashlightFixPlugin.Metadata.Version.Minor < 2;
            MimicsActive = Chainloader.PluginInfos.ContainsKey(MimicsGUID);
            ReservedItemSlotCoreActive = reservedItemSlotCoreAssembly != null;
            TwoRadarCamsActive = Chainloader.PluginInfos.ContainsKey(TwoRadarCamsGUID);
            WeatherRegistryActive = Chainloader.PluginInfos.ContainsKey(WeatherRegistryGUID);

            // Print which were found to be active
            if (AdvancedCompanyActive) Plugin.MLS.LogDebug("Advanced Company Detected");
            if (BuyRateSettingsActive) Plugin.MLS.LogDebug("BuyRateSettings Detected");
            if (CodeRebirthActive) Plugin.MLS.LogDebug("CodeRebirth Detected");
            if (MimicsActive) Plugin.MLS.LogDebug("Mimics Detected");
            if (ReservedItemSlotCoreActive) Plugin.MLS.LogDebug("Reserved Item Slot Core Detected");
            if (TwoRadarCamsActive) Plugin.MLS.LogDebug("Two Radar Cams Detected");
            if (WeatherRegistryActive) Plugin.MLS.LogDebug("WeatherRegistry Detected");

            _initialized = true;
        }

        public static bool IsReservedItemSlot(PlayerControllerB player, int slot)
        {
            if (!ReservedItemSlotCoreActive)
            {
                return false;
            }

            // If we were unable to load the types, assume anything over 4 is a reserved item slot
            if (PlayerData == null || IsReservedSlot == null)
            {
                Plugin.MLS.LogWarning("Could not load one or more ReservedItemSlot types when checking slot is reserved type. Assuming >= 4 is reserved");
                return slot >= 4;
            }

            var playerData = ((IDictionary)PlayerData.GetValue(null))[player];
            return (bool)IsReservedSlot.Invoke(playerData, new object[] { slot });
        }

        internal static void PatchCodeRebirthIfNeeded(Harmony harmony)
        {
            // If we detect the specific postfix for key item and we have some key configs, transpile that method
            if (CodeRebirthActive && (Plugin.UnlockDoorsFromInventory.Value || Plugin.KeysHaveInfiniteUses.Value))
            {
                bool patched = false;
                var keyPatch = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith("CodeRebirth"))
                    ?.GetTypes().FirstOrDefault(t => t.Name == "KeyItemPatch");
                if (keyPatch != null)
                {
                    var targetPostfix = keyPatch.GetMethod("CustomPickableObjects", BindingFlags.Public | BindingFlags.Static);
                    if (targetPostfix != null)
                    {
                        var transpiler = typeof(CodeRebirthPatch).GetMethod(nameof(CodeRebirthPatch.CustomPickableObjectsTranspiler), BindingFlags.NonPublic | BindingFlags.Static);
                        harmony.Patch(targetPostfix, transpiler: new HarmonyMethod(transpiler));
                        patched = true;
                    }
                }

                if (patched) Plugin.MLS.LogDebug("Patched CodeRebirth.KeyItemPatch.CustomPickableObjects to work with custom key functionality.");
                else Plugin.MLS.LogWarning("CodeRebirth detected but could not patch KeyItemPatch.CustomPickableObjects to work with custom key functionality! Did a signature change?");
            }
        }

        internal static void PatchBuyRateSettingsIfNeeded(Harmony harmony)
        {
            // If we detect BuyRateSettings, hook a postfix into its methods that update the buy rate if possible
            if (BuyRateSettingsActive && AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith("BuyRateSettings")) is Assembly buyRateAssembly)
            {
                bool patched = false;
                var refresherClass = buyRateAssembly.GetTypes().FirstOrDefault(t => t.Name == "BuyRateRefresher");
                if (refresherClass != null)
                {
                    var refreshMethod = refresherClass.GetMethod("Refresh", BindingFlags.Public | BindingFlags.Static);
                    var buyRateSetterMethod = refresherClass.GetMethod("BuyRateSetter", BindingFlags.NonPublic | BindingFlags.Static);

                    if (refreshMethod != null && buyRateSetterMethod != null)
                    {
                        var refreshPostfix = typeof(BuyRateSettingsPatch).GetMethod(nameof(BuyRateSettingsPatch.RefreshPatch), BindingFlags.NonPublic | BindingFlags.Static);
                        var updateCompanyBuyRatePostfix = typeof(BuyRateSettingsPatch).GetMethod(nameof(BuyRateSettingsPatch.BuyRateSetterPatch), BindingFlags.NonPublic | BindingFlags.Static);
                        harmony.Patch(refreshMethod, postfix: new HarmonyMethod(refreshPostfix));
                        harmony.Patch(buyRateSetterMethod, postfix: new HarmonyMethod(updateCompanyBuyRatePostfix));
                        patched = true;
                    }
                }

                if (patched) Plugin.MLS.LogDebug("Patched MoonJuice.BuyRateSettings.Refresh() and BuyRateSetterPatch() to work with company buy rate monitor (if needed).");
                else Plugin.MLS.LogWarning("BuyRateSettings detected but could not patch MoonJuice.BuyRateSettings.Refresh() and BuyRateSetterPatch() to work with company buy rate monitor (if needed)! Did a signature change?");
            }
        }

        internal static string GetWeatherRegistryWeatherName(SelectableLevel level){

            if (!WeatherRegistryActive)
            {
                return string.Empty;
            }

            return (string)WeatherGetCurrentName.Invoke(null, new object[] { level, false });
        }
    }
}