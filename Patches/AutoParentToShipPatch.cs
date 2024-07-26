using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GeneralImprovements.Patches
{
    internal static class AutoParentToShipPatch
    {
        private static IReadOnlyDictionary<string, int> _definedOffsets = new Dictionary<string, int>
        {
            { "RomanticTable", 7 }
        };

        private static Dictionary<AutoParentToShip, float> _offsets = new Dictionary<AutoParentToShip, float>();
        public static IReadOnlyDictionary<AutoParentToShip, float> Offsets => _offsets;

        [HarmonyPatch(typeof(AutoParentToShip), nameof(Awake))]
        [HarmonyPrefix]
        private static void Awake(AutoParentToShip __instance)
        {
            var foundOffset = _definedOffsets.FirstOrDefault(o => __instance.name.Contains(o.Key));

            if (foundOffset.Key != null)
            {
                // Apply and store this offset's difference so my snap code can use it as well
                __instance.rotationOffset += new Vector3(0, foundOffset.Value, 0);

                // Storing the initial mesh rotation offset allows snap building to work because ghost outlines use the mesh rotation value
                var meshOffset = __instance.GetComponentInChildren<PlaceableShipObject>().mainMesh.transform.localEulerAngles.y;
                _offsets.Add(__instance, meshOffset - __instance.rotationOffset.y);
            }

            if (!Plugin.ShipPlaceablesCollide.Value && __instance.GetComponentInChildren<PlaceableShipObject>() != null)
            {
                // Make sure all child objects have a mask of PlaceableShipObject
                var roomMask = LayerMask.NameToLayer("Room");
                ReplaceRoomMaskWithInteractableObjectRecursive(__instance.transform, roomMask);
            }
        }

        private static void ReplaceRoomMaskWithInteractableObjectRecursive(Transform curTransform, int roomMask)
        {
            if (curTransform.gameObject.layer == roomMask)
            {
                curTransform.gameObject.layer = 9; // InteractableObject layer
            }

            for (int i = 0; i < curTransform.childCount; i++)
            {
                ReplaceRoomMaskWithInteractableObjectRecursive(curTransform.GetChild(i), roomMask);
            }
        }
    }
}