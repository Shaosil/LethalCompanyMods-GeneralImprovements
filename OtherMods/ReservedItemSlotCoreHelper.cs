using GameNetcodeStuff;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace GeneralImprovements.OtherMods
{
    internal static class ReservedItemSlotCoreHelper
    {
        public static Assembly Assembly { get; private set; }

        // Lazy load and cache reflection info
        private static Type _playerPatcherType;
        private static Type _reservedPlayerDataType;
        private static FieldInfo _playerData;
        private static FieldInfo PlayerData => _playerData ?? (_playerData = _playerPatcherType.GetField("allPlayerData", BindingFlags.NonPublic | BindingFlags.Static));
        private static MethodInfo _isReservedSlot;
        private static MethodInfo IsReservedSlot => _isReservedSlot ?? (_isReservedSlot = _reservedPlayerDataType.GetMethod("IsReservedItemSlot"));

        public static void Initialize()
        {
            // Check for conflicting mods
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            Assembly = allAssemblies.FirstOrDefault(a => a.FullName.Contains("ReservedItemSlotCore,"));
            if (Assembly != null)
            {
                // Load reflection info
                _playerPatcherType = Assembly.GetType("ReservedItemSlotCore.Patches.PlayerPatcher");
                _reservedPlayerDataType = Assembly.GetType("ReservedItemSlotCore.ReservedPlayerData");
            }
        }

        public static bool IsReservedItemSlot(PlayerControllerB player, int slot)
        {
            if (Assembly == null)
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
    }
}