using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GeneralImprovements.Patches;
using HarmonyLib;

namespace GeneralImprovements
{
    [BepInPlugin(Metadata.GUID, Metadata.PLUGIN_NAME, Metadata.VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource MLS { get; private set; }

        private const string GeneralSection = "General";
        public static ConfigEntry<bool> PickupInOrder { get; private set; }
        public static ConfigEntry<bool> RearrangeOnDrop { get; private set; }

        private void Awake()
        {
            MLS = Logger;

            PickupInOrder = Config.Bind(GeneralSection, nameof(PickupInOrder), true, "When picking up items, will always put them in left - right order.");
            RearrangeOnDrop = Config.Bind(GeneralSection, nameof(RearrangeOnDrop), true, "When dropping items, will rearrange other inventory items to ensure slots are filled left - right.");
            MLS.LogInfo("Configuration Initialized.");

            Harmony.CreateAndPatchAll(typeof(PlayerControllerBPatch));
            MLS.LogInfo("PlayerControllerB patched.");

            Harmony.CreateAndPatchAll(typeof(KeyItemPatch));
            MLS.LogInfo("KeyItem patched.");

            MLS.LogInfo($"{Metadata.PLUGIN_NAME} v{Metadata.VERSION} fully loaded.");
        }
    }
}