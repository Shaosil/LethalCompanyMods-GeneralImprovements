using HarmonyLib;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class RadarBoosterItemPatch
    {
        [HarmonyPatch(typeof(RadarBoosterItem), nameof(RadarBoosterItem.Start))]
        [HarmonyPostfix]
        private static void Start(RadarBoosterItem __instance)
        {
            if (Plugin.RadarBoostersCanBeTeleported.Value != Enums.eRadarBoosterTeleport.Disabled)
            {
                static void commonParameterSet(ParticleSystem ps)
                {
                    var shape = ps.shape;
                    var emission = ps.emission;

                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 0.5f;
                    emission.rateOverTimeMultiplier = 300;
                }

                // Add beam up and out particle effects by copying them from the local player
                var newPs = UnityEngine.Object.Instantiate(StartOfRound.Instance.allPlayerScripts[0].beamUpParticle, __instance.transform);
                commonParameterSet(newPs);
                newPs.name = "BeamUpEffects";

                newPs = UnityEngine.Object.Instantiate(StartOfRound.Instance.allPlayerScripts[0].beamOutBuildupParticle, __instance.transform);
                commonParameterSet(newPs);
                newPs.name = "BeamOutBuildupEffects";

                newPs = UnityEngine.Object.Instantiate(StartOfRound.Instance.allPlayerScripts[0].beamOutParticle, __instance.transform);
                commonParameterSet(newPs);
                newPs.name = "BeamOutEffects";
            }
        }
    }
}