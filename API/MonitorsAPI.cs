using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GeneralImprovements.API
{
    public static class MonitorsAPI
    {
        internal static Dictionary<int, MonitorInfo> AllMonitors = new Dictionary<int, MonitorInfo>();

        public class MonitorInfo
        {
            // Public properties for monitor information
            public MeshRenderer MeshRenderer { get; internal set; }
            public TextMeshProUGUI TextCanvas { get; internal set; } // Will only have a value if assigned a text-based monitor
            public int ScreenMaterialIndex { get; internal set; }
            public Material AssignedMaterial { get; internal set; } // The original material this mod is trying to use

            // Helper properties for internal use
            internal Material OverwrittenMaterial { get; set; } // If another mod overwrites the renderer's shared material, it will be stored here when detected
            internal Material TargetMaterial => OverwrittenMaterial ?? AssignedMaterial; // What material the monitor should be using at any given time. Prioritize overrides.
        }

        public static MonitorInfo GetMonitorAtIndex(int i) => AllMonitors?.GetValueOrDefault(i);

        public static bool NewMonitorMeshActive { get; internal set; }
    }
}