using System.Collections.Generic;
using Unity.Profiling;
#if ENABLE_PROFILER
using UnityEngine;
#endif

namespace GeneralImprovements.Utilities
{
    internal static class ProfilerHelper
    {
        private static readonly HashSet<ProfilerMarker> _activeProfileMarkers = new HashSet<ProfilerMarker>();

#if ENABLE_PROFILER
        static ProfilerHelper()
        {
            // Disable overhead of stack trace in dev build
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Error, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Assert, StackTraceLogType.None);
        }
#endif

        public static void BeginProfilingSafe(ProfilerMarker pm)
        {
#if ENABLE_PROFILER
            // Only start if it's not already running
            if (!_activeProfileMarkers.Contains(pm))
            {
                pm.Begin();
                _activeProfileMarkers.Add(pm);
            }
#endif
        }

        public static void EndProfilingSafe(ProfilerMarker pm)
        {
#if ENABLE_PROFILER
            // Only end if it's already running
            if (_activeProfileMarkers.Contains(pm))
            {
                pm.End();
                _activeProfileMarkers.Remove(pm);
            }
#endif
        }
    }
}