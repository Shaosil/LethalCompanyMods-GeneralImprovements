using GeneralImprovements.Utilities;
using HarmonyLib;
using System;

namespace GeneralImprovements.Patches
{
    internal static class EnemyAIPatch
    {
        public static float CurTotalPowerLevel = 0;

        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.Start))]
        [HarmonyPostfix]
        private static void Start(EnemyAI __instance)
        {
            if (__instance.enemyType != null && !__instance.enemyType.isDaytimeEnemy)
            {
                CurTotalPowerLevel += __instance.enemyType.PowerLevel;
                MonitorsHelper.UpdateDangerLevelMonitors();
            }
        }

        [HarmonyPatch(typeof(EnemyAI), nameof(SubtractFromPowerLevel))]
        [HarmonyPostfix]
        private static void SubtractFromPowerLevel(EnemyAI __instance)
        {
            // Some enemies may just be deactivated instead of destroyed
            if (__instance.enemyType != null && !__instance.enemyType.isDaytimeEnemy)
            {
                CurTotalPowerLevel = Math.Max(CurTotalPowerLevel - __instance.enemyType.PowerLevel, 0);
                MonitorsHelper.UpdateDangerLevelMonitors();
            }
        }
    }
}