using System.Collections.Generic;
using GeneralImprovements.Assets;
using TMPro;
using UnityEngine;

namespace GeneralImprovements.API
{
    public static class MonitorsAPI
    {
        internal static Monitors NewMonitors;

        /// <summary>
        /// Includes every ACTIVE monitor, not disabled or hidden ones
        /// </summary>
        internal static Dictionary<int, MonitorInfo> AllMonitors = new Dictionary<int, MonitorInfo>();

        /// <summary>
        /// Returns the amount of better monitors currently in use
        /// </summary>
        public static int NumMonitorsActive { get; internal set; }

        /// <summary>
        /// Returns the active better monitor at index i. Does NOT include disabled/hidden monitors.
        /// </summary>
        /// <returns></returns>
        public static MonitorInfo GetMonitorAtIndex(int i) => AllMonitors?.GetValueOrDefault(i);

        /// <summary>
        /// True if the extra better monitor mesh is currently active
        /// </summary>
        public static bool NewMonitorMeshActive { get; internal set; }

        public class MonitorInfo
        {
            // Public properties for monitor information
            public Camera Camera { get; internal set; } // The associated camera that handles text updates for text based monitors
            public MeshRenderer MeshRenderer { get; internal set; }
            public TextMeshProUGUI TextCanvas { get; internal set; } // Will only have a value if assigned a text-based monitor
            public int ScreenMaterialIndex { get; internal set; }
            public Material AssignedMaterial { get; internal set; } // The original material this mod is trying to use

            // Helper properties for internal use
            internal Material OverwrittenMaterial { get; set; } // If another mod overwrites the renderer's shared material, it will be stored here when detected
            internal Material TargetMaterial => OverwrittenMaterial ? OverwrittenMaterial : AssignedMaterial; // What material the monitor should be using at any given time. Prioritize overrides.

            public void QueueRender()
            {
                NewMonitors.RefreshMonitorAfterTextChange(this);
            }
        }
    }
}