using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GeneralImprovements.Assets
{
    internal class Monitors : MonoBehaviour
    {
        private Dictionary<MeshRenderer, Material> _originalScreenMaterials = new Dictionary<MeshRenderer, Material>();                             // The stored materials of each monitor. Used for toggling power.
        private Dictionary<int, Action<TextMeshProUGUI>> _initialTextAssignments = new Dictionary<int, Action<TextMeshProUGUI>>();                  // Set before Start(), the monitor index of each first TMP action to take.
        private Dictionary<int, Func<MeshRenderer, Material>> _initialMaterialAssignments = new Dictionary<int, Func<MeshRenderer, Material>>();    // Set before Start(), the monitor index of each first material action to take.
        private Dictionary<TextMeshProUGUI, Material> _textsToMats = new Dictionary<TextMeshProUGUI, Material>();                                   // A helper reference of TMP objects to monitor materials.

        private Camera _camera;
        private MeshRenderer _mapRenderer;

        // Set these before Start() is called
        public Material HullMaterial;
        public Material BlankScreenMat;
        public Material StartingMapMaterial;

        // Any materials that override ours will be stored here. This should be manually persisted in between monitor rebuilds.
        public Dictionary<int, Material> MaterialOverrides = new Dictionary<int, Material>();

        private void Start()
        {
            var structureL = transform.Find("Monitors/TopGroupL");
            var structureM = transform.Find("Monitors/TopGroupM");
            var structureR = transform.Find("Monitors/TopGroupR");
            var bigScreenL = transform.Find("Monitors/BigLeft");
            var bigScreenM = transform.Find("Monitors/BigMiddle");
            var bigScreenR = transform.Find("Monitors/BigRight");

            // Assign the correct material to the base structures
            structureL.GetComponent<MeshRenderer>().sharedMaterial = HullMaterial;
            structureM.GetComponent<MeshRenderer>().sharedMaterial = HullMaterial;
            structureR.GetComponent<MeshRenderer>().sharedMaterial = HullMaterial;
            bigScreenL.GetComponent<MeshRenderer>().sharedMaterial = HullMaterial;
            bigScreenM.GetComponent<MeshRenderer>().sharedMaterial = HullMaterial;
            _mapRenderer = bigScreenM.Find("MScreen").GetComponent<MeshRenderer>();
            _mapRenderer.sharedMaterial = StartingMapMaterial;
            bigScreenR.GetComponent<MeshRenderer>().sharedMaterial = HullMaterial;

            // Store the mesh renderers of all the screens for later based on how many monitors we have
            var allScreens = new List<Transform>();
            if (Plugin.AddMoreBetterMonitors.Value)
            {
                allScreens.AddRange(new[] { structureL.Find("Screen1"), structureL.Find("Screen2") });
            }
            allScreens.AddRange(new[] { structureM.Find("Screen3"), structureM.Find("Screen4"), structureR.Find("Screen5"), structureR.Find("Screen6") });
            if (Plugin.AddMoreBetterMonitors.Value)
            {
                allScreens.AddRange(new[] { structureL.Find("Screen7"), structureL.Find("Screen8") });
            }
            allScreens.AddRange(new[] { structureM.Find("Screen9"), structureM.Find("Screen10"), structureR.Find("Screen11"), structureR.Find("Screen12") });
            if (Plugin.AddMoreBetterMonitors.Value)
            {
                allScreens.Add(bigScreenL.Find("LScreen"));
            }
            allScreens.Add(bigScreenR.Find("RScreen"));
            foreach (var screen in allScreens)
            {
                var renderer = screen.GetComponent<MeshRenderer>();
                _originalScreenMaterials[renderer] = renderer.sharedMaterial;
            }

            // Get the text and camera objects
            var allTexts = transform.Find("Canvas/Texts").GetComponentsInChildren<TextMeshProUGUI>();
            _camera = transform.GetComponentInChildren<Camera>();
            _camera.enabled = false;

            // Adjust the background color
            transform.Find("Canvas/Background").GetComponent<Image>().color = Plugin.MonitorBackgroundColorVal;

            // Store the texts for later and finalize the assignments
            for (int i = 0; i < Math.Min(allTexts.Length, allScreens.Count); i++)
            {
                var screenText = allTexts[i];
                screenText.font = StartOfRound.Instance.profitQuotaMonitorText.font;
                screenText.spriteAsset = StartOfRound.Instance.profitQuotaMonitorText.spriteAsset;
                screenText.color = Plugin.MonitorTextColorVal;
                screenText.alignment = Plugin.CenterAlignMonitorText.Value ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;

                // Store the material associated with this object so we can snapshot it as needed, then call the invocation
                var curScreenMat = _originalScreenMaterials.ElementAt(i);
                if (MaterialOverrides.ContainsKey(i))
                {
                    // If we know something should override it, just use that
                    curScreenMat.Key.sharedMaterial = MaterialOverrides[i];
                }
                else if (_initialMaterialAssignments.ContainsKey(i))
                {
                    // Only overwrite original value if it doesn't contain something unexpected (if another mod put something there before we get here)
                    if (curScreenMat.Key.sharedMaterial.name.StartsWith("GIShipMonitor"))
                    {
                        var assignedMat = _initialMaterialAssignments[i](curScreenMat.Key);
                        _originalScreenMaterials[curScreenMat.Key] = assignedMat;
                        curScreenMat.Key.sharedMaterial = assignedMat;
                    }
                    else
                    {
                        // Store for later reference
                        MaterialOverrides[i] = curScreenMat.Key.sharedMaterial;
                    }
                }
                else if (_initialTextAssignments.ContainsKey(i) || Plugin.ShowBackgroundOnAllScreens.Value)
                {
                    _textsToMats[screenText] = curScreenMat.Value;

                    if (_initialTextAssignments.ContainsKey(i))
                    {
                        // Call the action (it should handle adding this screenText object to its own list and calling the update text method)
                        _initialTextAssignments[i](screenText);
                    }
                    else
                    {
                        // Clear the text of this monitor so the background color still shows
                        screenText.text = string.Empty;
                        RenderCameraAfterTextChange(screenText);
                    }
                }
                else
                {
                    // If there is no assignment, this is just a blank monitor
                    curScreenMat.Key.sharedMaterial = BlankScreenMat;
                }
            }
        }

        public void AssignTextMonitor(int index, Action<TextMeshProUGUI> textAssignment)
        {
            // Store the assignment for later when Start() runs
            _initialTextAssignments[index] = textAssignment;
        }

        public void AssignMaterialMonitor(int index, Func<MeshRenderer, Material> materialAssignment)
        {
            // Store the assignment for later when Start() runs
            _initialMaterialAssignments[index] = materialAssignment;
        }

        public bool RenderCameraAfterTextChange(TextMeshProUGUI text)
        {
            // Only render if we have matching textures
            if (_textsToMats.ContainsKey(text))
            {
                // Move the camera to be in front of the text component
                _camera.transform.parent = text.transform;
                if (Plugin.CenterAlignMonitorText.Value)
                {
                    _camera.transform.localPosition = new Vector3(0, 0, _camera.transform.localPosition.z);
                }
                else
                {
                    // No idea why, but left aligning the text needs an extra offset or it will be off screen
                    _camera.transform.localPosition = new Vector3(-10, 10, _camera.transform.localPosition.z);
                }

                // Render to render texture
                _camera.Render();

                // Apply the render texture to the associated material
                var oldRenderTex = RenderTexture.active;
                Graphics.SetRenderTarget(_camera.activeTexture);
                var tex = new Texture2D(_camera.activeTexture.width, _camera.activeTexture.height, TextureFormat.RGB24, false, true);
                tex.ReadPixels(new Rect(0, 0, _camera.activeTexture.width, _camera.activeTexture.height), 0, 0);
                tex.Apply();
                _textsToMats[text].mainTexture = tex;

                Graphics.SetRenderTarget(oldRenderTex);

                return true;
            }

            return false;
        }

        public void TogglePower(bool on)
        {
            // Set the power of all in-use monitors
            for (int i = 0; i < _originalScreenMaterials.Count; i++)
            {
                if (!Plugin.ShowBackgroundOnAllScreens.Value && !_initialTextAssignments.ContainsKey(i) && !_initialMaterialAssignments.ContainsKey(i))
                {
                    // Skip unassigned monitors
                    continue;
                }

                // If we are turning the monitor off (or refreshing) and we have a new material (probably from another mod that started after us), overwrite our stored one
                var curScreemMat = _originalScreenMaterials.ElementAt(i);
                if ((!on || curScreemMat.Key.sharedMaterial != BlankScreenMat) && curScreemMat.Key.sharedMaterial != curScreemMat.Value)
                {
                    Plugin.MLS.LogWarning($"Found an unexpected material on ship monitor {i + 1} ({curScreemMat.Key.sharedMaterial.name}). Using it instead, since it was most likely purposefully assigned.");
                    _originalScreenMaterials[curScreemMat.Key] = curScreemMat.Key.sharedMaterial;

                    // Store for later (probably don't need this at this point but just in case)
                    MaterialOverrides[i] = curScreemMat.Key.sharedMaterial;
                }

                curScreemMat.Key.sharedMaterial = on ? _originalScreenMaterials[curScreemMat.Key] : BlankScreenMat;
            }
        }

        public void UpdateMapMaterial(Material newMaterial)
        {
            if (_mapRenderer != null)
            {
                _mapRenderer.sharedMaterial = newMaterial;
            }
        }

        public Transform GetMonitorTransform(TextMeshProUGUI text)
        {
            Transform matchingTransform = null;

            if (_textsToMats.TryGetValue(text, out var mat))
            {
                matchingTransform = _originalScreenMaterials.FirstOrDefault(m => m.Value == mat).Key?.transform;
            }

            return matchingTransform;
        }
    }
}