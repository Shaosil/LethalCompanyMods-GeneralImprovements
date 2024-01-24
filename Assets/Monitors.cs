﻿using System;
using System.Collections.Generic;
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
        public Color BackgroundColor;
        public Material HullMaterial;
        public Material BlankScreenMat;
        public Material StartingMapMaterial;

        private void Start()
        {
            var structureL = transform.Find("Monitors/StructureL");
            var structureM = transform.Find("Monitors/StructureM");
            var structureR = transform.Find("Monitors/StructureR");
            var bigScreenL = transform.Find("BigMonitors/Left");
            var bigScreenM = transform.Find("BigMonitors/Middle");
            var bigScreenR = transform.Find("BigMonitors/Right");

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
                _originalScreenMaterials.Add(new KeyValuePair<MeshRenderer, Material>(renderer, renderer.sharedMaterial));
            }

            // Get the text and camera objects
            var allTexts = transform.Find("Canvas/Texts").GetComponentsInChildren<TextMeshProUGUI>();
            _camera = transform.GetComponentInChildren<Camera>();

            // Adjust the background color
            transform.Find("Canvas/Background").GetComponent<Image>().color = BackgroundColor;

            // Store the texts for later and finalize the assignments
            for (int i = 0; i < allTexts.Length; i++)
            {
                var screenText = allTexts[i];
                screenText.font = StartOfRound.Instance.profitQuotaMonitorText.font;
                screenText.spriteAsset = StartOfRound.Instance.profitQuotaMonitorText.spriteAsset;
                screenText.color = StartOfRound.Instance.profitQuotaMonitorText.color;

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
            _initialTextAssignments.Add(index, textAssignment);
        }

        public void AssignMaterialMonitor(int index, Material materialAssignment)
        {
            // Store the assignment for later when Start() runs
            _initialMaterialAssignments.Add(index, materialAssignment);
        }

        public void RenderCameraAfterTextChange(TextMeshProUGUI text)
        {
            if (_textsToMats.ContainsKey(text))
            {
                // Move the camera to be in front of the text component
                _camera.transform.parent = text.transform;
                _camera.transform.localPosition = new Vector3(0, 0, -150);

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
    }
}