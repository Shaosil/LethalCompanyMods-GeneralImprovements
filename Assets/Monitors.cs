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
        private List<KeyValuePair<MeshRenderer, Material>> _originalScreenMaterials = new List<KeyValuePair<MeshRenderer, Material>>();
        private Dictionary<int, Action<TextMeshProUGUI>> _initialTextAssignments = new Dictionary<int, Action<TextMeshProUGUI>>();
        private Dictionary<int, Material> _initialMaterialAssignments = new Dictionary<int, Material>();
        private Dictionary<TextMeshProUGUI, Material> _textsToMats = new Dictionary<TextMeshProUGUI, Material>();
        private Camera _camera;
        private MeshRenderer _mapRenderer;

        // Set these before Start() is called
        public Material HullMaterial;
        public Material BlankScreenMat;
        public Material StartingMapMaterial;

        private void Start()
        {
            var structureL = transform.Find("Monitors/TopGroupL");
            var structureM = transform.Find("Monitors/TopGroupM");
            var structureR = transform.Find("Monitors/TopGroupR");
            var bigScreenL = transform.Find("Monitors/BigLeft");
            var bigScreenM = transform.Find("Monitors/BigMiddle");
            var bigScreenR = transform.Find("Monitors/BigRight");

            // Assign the correct material to the base structures
            structureL.GetComponent<MeshRenderer>().material = HullMaterial;
            structureM.GetComponent<MeshRenderer>().material = HullMaterial;
            structureR.GetComponent<MeshRenderer>().material = HullMaterial;
            bigScreenL.GetComponent<MeshRenderer>().material = HullMaterial;
            bigScreenM.GetComponent<MeshRenderer>().material = HullMaterial;
            _mapRenderer = bigScreenM.Find("MScreen").GetComponent<MeshRenderer>();
            _mapRenderer.material = StartingMapMaterial;
            bigScreenR.GetComponent<MeshRenderer>().material = HullMaterial;

            // Store the mesh renderers of all the screens for later
            var allScreens = new Transform[]
            {
                structureL.Find("Screen1"),
                structureL.Find("Screen2"),
                structureM.Find("Screen3"),
                structureM.Find("Screen4"),
                structureR.Find("Screen5"),
                structureR.Find("Screen6"),
                structureL.Find("Screen7"),
                structureL.Find("Screen8"),
                structureM.Find("Screen9"),
                structureM.Find("Screen10"),
                structureR.Find("Screen11"),
                structureR.Find("Screen12"),
                bigScreenL.Find("LScreen"),
                bigScreenR.Find("RScreen")
            };
            foreach (var screen in allScreens)
            {
                var renderer = screen.GetComponent<MeshRenderer>();
                _originalScreenMaterials.Add(new KeyValuePair<MeshRenderer, Material>(renderer, renderer.material));
            }

            // Get the text and camera objects
            var allTexts = transform.Find("Canvas/Texts").GetComponentsInChildren<TextMeshProUGUI>();
            _camera = transform.GetComponentInChildren<Camera>();
            _camera.enabled = false;

            // Adjust the background color
            transform.Find("Canvas/Background").GetComponent<Image>().color = Plugin.MonitorBackgroundColorVal;

            // Store the texts for later and finalize the assignments
            for (int i = 0; i < allTexts.Length; i++)
            {
                var screenText = allTexts[i];
                screenText.font = StartOfRound.Instance.profitQuotaMonitorText.font;
                screenText.spriteAsset = StartOfRound.Instance.profitQuotaMonitorText.spriteAsset;
                screenText.color = Plugin.MonitorTextColorVal;
                screenText.alignment = Plugin.CenterAlignMonitorText.Value ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;

                // Store the material associated with this object so we can snapshot it as needed, then call the invocation
                if (_initialMaterialAssignments.ContainsKey(i))
                {
                    // Overwrite original value
                    _originalScreenMaterials[i] = new KeyValuePair<MeshRenderer, Material>(_originalScreenMaterials[i].Key, _initialMaterialAssignments[i]);
                    _originalScreenMaterials[i].Key.material = _initialMaterialAssignments[i];
                }
                else if (_initialTextAssignments.ContainsKey(i) || Plugin.ShowBackgroundOnAllScreens.Value)
                {
                    _textsToMats[screenText] = _originalScreenMaterials[i].Value;

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
                    _originalScreenMaterials[i].Key.material = BlankScreenMat;
                }
            }
        }

        public void AssignTextMonitor(int index, Action<TextMeshProUGUI> textAssignment)
        {
            // Store the assignment for later when Start() runs
            _initialTextAssignments[index] = textAssignment;
        }

        public void AssignMaterialMonitor(int index, Material materialAssignment)
        {
            // Store the assignment for later when Start() runs
            _initialMaterialAssignments[index] = materialAssignment;
        }

        public void RenderCameraAfterTextChange(TextMeshProUGUI text)
        {
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
            }
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

                _originalScreenMaterials[i].Key.material = on ? _originalScreenMaterials[i].Value : BlankScreenMat;
            }
        }

        public void UpdateMapMaterial(Material newMaterial)
        {
            if (_mapRenderer != null)
            {
                _mapRenderer.material = newMaterial;
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