using BepInEx;
using BepInEx.Bootstrap;
using GameNetcodeStuff;
using GeneralImprovements.Patches;
using HarmonyLib;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace GeneralImprovements.Utilities
{
    internal static class OtherModHelper
    {
        private static bool _initialized = false;

        public const string MimicsGUID = "x753.Mimics";
        public const string TwoRadarCamsGUID = "Zaggy1024.TwoRadarMaps";
        public const string WeatherTweaksGUID = "WeatherTweaks";
        public const string CodeRebirthGUID = "CodeRebirth";

        public static bool AdvancedCompanyActive { get; private set; }
        public static bool ReservedItemSlotCoreActive { get; private set; }
        public static bool FlashlightFixActive { get; private set; }
        public static bool MimicsActive { get; private set; }
        public static bool TwoRadarCamsActive { get; private set; }
        public static bool WeatherTweaksActive { get; private set; }
        public static bool CodeRebirthActive { get; private set; }

        public static ManualCameraRenderer TwoRadarCamsMapRenderer { get; set; }

        // Reflection information for ReservedItemSlotCore
        private static Type _reservedPlayerPatcherType;
        private static Type _reservedPlayerDataType;
        private static FieldInfo _reservedPlayerData;
        private static FieldInfo PlayerData => _reservedPlayerData ?? (_reservedPlayerData = _reservedPlayerPatcherType.GetField("allPlayerData", BindingFlags.NonPublic | BindingFlags.Static));
        private static MethodInfo _isReservedSlot;
        private static MethodInfo IsReservedSlot => _isReservedSlot ?? (_isReservedSlot = _reservedPlayerDataType.GetMethod("IsReservedItemSlot"));

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

            // Set active statuses
            AdvancedCompanyActive = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.FullName.Contains("AdvancedCompany,"));
            ReservedItemSlotCoreActive = reservedItemSlotCoreAssembly != null;
            // Detect flashlight fix as active only if they are not on the latest versio that removes all code
            var flashlightFixPlugin = TypeLoader.FindPluginTypes(Paths.PluginPath, Chainloader.ToPluginInfo)
                .FirstOrDefault(p => p.Value.FirstOrDefault()?.Metadata.GUID == "ShaosilGaming.FlashlightFix").Value?.FirstOrDefault();
            FlashlightFixActive = flashlightFixPlugin != null && flashlightFixPlugin.Metadata.Version.Minor < 2;
            MimicsActive = Chainloader.PluginInfos.ContainsKey(MimicsGUID);
            TwoRadarCamsActive = Chainloader.PluginInfos.ContainsKey(TwoRadarCamsGUID);
            WeatherTweaksActive = Chainloader.PluginInfos.ContainsKey(WeatherTweaksGUID);
            CodeRebirthActive = Chainloader.PluginInfos.ContainsKey(CodeRebirthGUID);

            // Print which were found to be active
            if (AdvancedCompanyActive) Plugin.MLS.LogDebug("Advanced Company Detected");
            if (ReservedItemSlotCoreActive) Plugin.MLS.LogDebug("Reserved Item Slot Core Detected");
            if (MimicsActive) Plugin.MLS.LogDebug("Mimics Detected");
            if (TwoRadarCamsActive) Plugin.MLS.LogDebug("Two Radar Cams Detected");
            if (WeatherTweaksActive) Plugin.MLS.LogDebug("Weather Tweaks Detected");
            if (CodeRebirthActive) Plugin.MLS.LogDebug("CodeRebirth Detected");

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

        internal static void PatchCodeRebirthIfNeeded(HarmonyLib.Harmony harmony)
        {
            // If we detect the specific postfix for key item and we have some key configs, transpile that method
            if (CodeRebirthActive && (Plugin.UnlockDoorsFromInventory.Value || Plugin.KeysHaveInfiniteUses.Value))
            {
                var keyPatch = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName.StartsWith("CodeRebirth"))
                    ?.GetTypes().FirstOrDefault(t => t.Name == "KeyItemPatch");
                if (keyPatch != null)
                {
                    var targetPostfix = keyPatch.GetMethod("CustomPickableObjects", BindingFlags.Public | BindingFlags.Static);
                    if (targetPostfix != null)
                    {
                        var transpiler = typeof(CodeRebirthPatch).GetMethod(nameof(CodeRebirthPatch.CustomPickableObjectsTranspiler), BindingFlags.NonPublic | BindingFlags.Static);
                        harmony.Patch(targetPostfix, transpiler: new HarmonyMethod(transpiler));
                    }
                }
            }
        }
    }
}