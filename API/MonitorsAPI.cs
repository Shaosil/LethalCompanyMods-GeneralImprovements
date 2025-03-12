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
            public TextMeshProUGUI TextCanvas { get; internal set; } // Not used if the monitor is displaying a camera
            public int ScreenMaterialIndex { get; internal set; }
            public Material AssignedMaterial { get; internal set; } // The original material this mod is trying to use

            // Helper properties for internal use
            internal Material OverwrittenMaterial { get; set; } // If another mod overwrites the renderer's shared material, it will be stored here when detected
            internal Material TargetMaterial => OverwrittenMaterial ? OverwrittenMaterial : AssignedMaterial; // What material the monitor should be using at any given time. Prioritize overrides.

            /// <summary>
            /// Attempt to refresh this monitor. Will apply any pending text changes. You may optionally provide text to use. No refresh will occur on consecutive identical custom text calls.
            /// </summary>
            /// <param name="customText">If supplied, the monitor's TMP object's text will be set to this value. Will be overwritten by GI if the monitor has a dynamic assignment.</param>
            /// <returns>True if the call resulted in the monitor being refreshed, false otherwise.</returns>
            public bool QueueRender(string customText = "")
            {
                if (!NewMonitors)
                {
                    Plugin.MLS.LogError("Attempted to queue a render on a monitor before they were initialized. This should not be able to happen!");
                    return false;
                }
                else if (!TextCanvas)
                {
                    Plugin.MLS.LogWarning("Attempted to queue a render on a non text monitor. No action taken.");
                    return false;
                }

                bool hasCustomText = !string.IsNullOrWhiteSpace(customText);
                bool hasNewText = hasCustomText && TextCanvas.text != customText;

                if (hasCustomText && !hasNewText)
                {
                    Plugin.MLS.LogWarning("Attempted to queue a render on a text monitor with no new custom text. No action taken.");
                    return false;
                }

                if (hasNewText)
                {
                    Plugin.MLS.LogInfo("Queuing a render on a text monitor with custom text.");
                    TextCanvas.text = customText;
                }
                else
                {
                    Plugin.MLS.LogInfo("Forcing a refresh on a text monitor.");
                }

                return NewMonitors.RefreshMonitorAfterTextChange(this);
            }
        }
    }
}