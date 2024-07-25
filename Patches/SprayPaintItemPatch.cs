using GeneralImprovements.Utilities;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class SprayPaintItemPatch
    {
        private static int _numSprayCansGenerated = 0;

        [HarmonyPatch(typeof(SprayPaintItem), nameof(Start))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Start_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Patch out the random color assignment lines
            var codeList = instructions.ToList();
            if (codeList.TryFindInstruction(i => i.Is(OpCodes.Initobj, typeof(RaycastHit)), out var found))
            {
                Plugin.MLS.LogDebug("Patching out old spray can random color assignment.");
                codeList = codeList.Take(6).Concat(new[] { codeList.Last() }).ToList();
            }
            else
            {
                Plugin.MLS.LogError($"Could not patch spray can item Start() method.");
            }
            return codeList;
        }

        [HarmonyPatch(typeof(SprayPaintItem), nameof(Start))]
        [HarmonyPostfix]
        private static void Start(SprayPaintItem __instance)
        {
            // On spawn if we are not in the ship phase, manually roll the spray paint color using better randomness
            if (!StartOfRound.Instance.inShipPhase)
            {
                int newMatIndex = new System.Random(StartOfRound.Instance.randomMapSeed + _numSprayCansGenerated).Next(0, __instance.sprayCanMats.Length);
                UpdateColor(__instance, newMatIndex);
            }

            _numSprayCansGenerated++;
        }

        public static void UpdateColor(SprayPaintItem instance, int matIndex)
        {
            if (!instance.isWeedKillerSprayBottle)
            {
                instance.sprayCanMatsIndex = matIndex;
                instance.sprayParticle.GetComponent<ParticleSystemRenderer>().material = instance.particleMats[matIndex];
                instance.sprayCanNeedsShakingParticle.GetComponent<ParticleSystemRenderer>().material = instance.particleMats[matIndex];
                Plugin.MLS.LogDebug($"Updated spray paint item color to material index {matIndex}");
            }
        }

        public static SprayPaintItem[] GetAllOrderedSprayPaintItemsInShip()
        {
            // Return all spray cans that are currently in the ship, ordered by distance to world center (useful for preservering order when doing RPCs)
            return Object.FindObjectsOfType<SprayPaintItem>().Where(s => s.isInShipRoom).OrderBy(s => Vector3.Distance(s.transform.position, Vector3.zero)).ToArray();
        }
    }
}