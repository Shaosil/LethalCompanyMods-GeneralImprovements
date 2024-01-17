using System.IO;
using System.Reflection;
using UnityEngine;

namespace GeneralImprovements.Utilities
{
    internal static class AssetBundleHelper
    {
        private static AssetBundle _bundle;

        public static Sprite Reticle { get; private set; }
        public static GameObject MedStationPrefab { get; private set; }

        public static void Initialize()
        {
            if (_bundle == null)
            {
                // Find our directory
                string assetPath = Path.Combine(new FileInfo(Assembly.GetExecutingAssembly().Location).DirectoryName, "assets");
                if (!new FileInfo(assetPath).Exists)
                {
                    Plugin.MLS.LogError("Could not find asset bundle!");
                    return;
                }
                _bundle = AssetBundle.LoadFromFile(assetPath);

                // Load assets into memory
                Plugin.MLS.LogInfo("Loading assets...");
                Reticle = _bundle.LoadAsset<Sprite>("reticle");
                MedStationPrefab = _bundle.LoadAsset<GameObject>("MedStation.prefab");
            }
        }
    }
}