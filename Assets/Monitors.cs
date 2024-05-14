using GeneralImprovements.API;
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
        private Camera _camera;
        private MeshRenderer _mapRenderer;
        private Material _blankScreenMaterial;

        public void Initialize(Material hullMaterial, Material startingMapMaterial, Material blankScreenMaterial)
        {
            // Store the blank screen material for later power toggling
            _blankScreenMaterial = blankScreenMaterial;

            // Get the transform of each monitor cluster
            var structureL = transform.Find("Monitors/TopGroupL");
            var structureM = transform.Find("Monitors/TopGroupM");
            var structureR = transform.Find("Monitors/TopGroupR");
            var bigScreenL = transform.Find("Monitors/BigLeft");
            var bigScreenM = transform.Find("Monitors/BigMiddle");
            var bigScreenR = transform.Find("Monitors/BigRight");

            // Assign the starting materials to the base structures
            structureL.GetComponent<MeshRenderer>().sharedMaterial = hullMaterial;
            structureM.GetComponent<MeshRenderer>().sharedMaterial = hullMaterial;
            structureR.GetComponent<MeshRenderer>().sharedMaterial = hullMaterial;
            bigScreenL.GetComponent<MeshRenderer>().sharedMaterial = hullMaterial;
            bigScreenM.GetComponent<MeshRenderer>().sharedMaterial = hullMaterial;
            _mapRenderer = bigScreenM.Find("MScreen").GetComponent<MeshRenderer>();
            _mapRenderer.sharedMaterial = startingMapMaterial;
            bigScreenR.GetComponent<MeshRenderer>().sharedMaterial = hullMaterial;
            transform.Find("Canvas/Background").GetComponent<Image>().color = Plugin.MonitorBackgroundColorVal;

            // Add the transforms of each monitor (in order) that we will be using, depending on which config settings are active
            var allMonitors = new List<Transform>();
            if (Plugin.AddMoreBetterMonitors.Value) allMonitors.AddRange(new[] { structureL.Find("Screen1"), structureL.Find("Screen2") });
            allMonitors.AddRange(new[] { structureM.Find("Screen3"), structureM.Find("Screen4"), structureR.Find("Screen5"), structureR.Find("Screen6") });
            if (Plugin.AddMoreBetterMonitors.Value) allMonitors.AddRange(new[] { structureL.Find("Screen7"), structureL.Find("Screen8") });
            allMonitors.AddRange(new[] { structureM.Find("Screen9"), structureM.Find("Screen10"), structureR.Find("Screen11"), structureR.Find("Screen12") });
            if (Plugin.AddMoreBetterMonitors.Value) allMonitors.Add(bigScreenL.Find("LScreen"));
            allMonitors.Add(bigScreenR.Find("RScreen"));

            // Get the text and camera objects (they are not children of our monitor objects above)
            var allTexts = transform.Find("Canvas/Texts").GetComponentsInChildren<TextMeshProUGUI>();
            _camera = transform.GetComponentInChildren<Camera>();
            _camera.enabled = false;

            // Store anything that may have been overwritten so we can keep using the same materials after initialization
            var overwrittenMaterials = MonitorsAPI.AllMonitors.Where(m => m.Value.OverwrittenMaterial != null).ToDictionary(k => k.Key, v => v.Value.OverwrittenMaterial);

            // Initialize the TMP objects and assign all information to our API dictionary
            MonitorsAPI.AllMonitors = new Dictionary<int, MonitorsAPI.MonitorInfo>();
            for (int i = 0; i < Math.Min(allTexts.Length, allMonitors.Count); i++)
            {
                var screenText = allTexts[i];
                screenText.font = StartOfRound.Instance.profitQuotaMonitorText.font;
                screenText.spriteAsset = StartOfRound.Instance.profitQuotaMonitorText.spriteAsset;
                screenText.color = Plugin.MonitorTextColorVal;
                screenText.alignment = Plugin.CenterAlignMonitorText.Value ? TextAlignmentOptions.Center : TextAlignmentOptions.TopLeft;

                // If there is no assignment here, either show a blank screen or clear the text in prep of the canvas render
                var renderer = allMonitors[i].GetComponent<MeshRenderer>();
                var curAssignment = Plugin.ShipMonitorAssignments[i].Value;
                if (curAssignment == Enums.eMonitorNames.None)
                {
                    if (Plugin.ShowBackgroundOnAllScreens.Value)
                    {
                        screenText.text = string.Empty;
                    }
                    else
                    {
                        renderer.sharedMaterial = _blankScreenMaterial;
                    }
                }

                MonitorsAPI.AllMonitors[i] = new MonitorsAPI.MonitorInfo
                {
                    MeshRenderer = renderer,
                    TextCanvas = (Plugin.ShowBackgroundOnAllScreens.Value || curAssignment > Enums.eMonitorNames.None) && curAssignment < Enums.eMonitorNames.InternalCam ? screenText : null,
                    ScreenMaterialIndex = 0, // Our screen meshes are separate from the surrounding meshes and always only have one material
                    AssignedMaterial = renderer.sharedMaterial,
                    OverwrittenMaterial = overwrittenMaterials.GetValueOrDefault(i)
                };
            }
        }

        private void Start()
        {
            // Render all text based monitors that do not have overrides
            foreach (var monitor in MonitorsAPI.AllMonitors.Values.Where(m => m.TextCanvas != null && m.MeshRenderer.sharedMaterial == m.AssignedMaterial))
            {
                RefreshMonitorAfterTextChange(monitor);
            }
        }

        public bool RefreshMonitorAfterTextChange(MonitorsAPI.MonitorInfo monitor)
        {
            // Only render if this monitor has a text canvas associated with it and is not overwritten
            if (monitor.TextCanvas != null && monitor.MeshRenderer.sharedMaterial == monitor.AssignedMaterial)
            {
                // Move the camera to be in front of the text component
                _camera.transform.parent = monitor.TextCanvas.transform;
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
                monitor.MeshRenderer.sharedMaterial.mainTexture = tex;

                Graphics.SetRenderTarget(oldRenderTex);

                return true;
            }

            return false;
        }

        public void TogglePower(bool on)
        {
            // Set the power of all in-use monitors
            foreach (var kvp in MonitorsAPI.AllMonitors)
            {
                var monitor = kvp.Value;

                // If we are turning the monitor off (or refreshing) and we have a new material (probably from another mod that started after us), overwrite our stored one
                if ((!on || monitor.MeshRenderer.sharedMaterial.name != _blankScreenMaterial.name) && monitor.MeshRenderer.sharedMaterial != monitor.TargetMaterial)
                {
                    Plugin.MLS.LogWarning($"Found an unexpected material on ship monitor {kvp.Key + 1} ({monitor.MeshRenderer.sharedMaterial.name}). Using it instead, since it was most likely purposefully assigned.");
                    monitor.OverwrittenMaterial = monitor.MeshRenderer.sharedMaterial;
                }

                monitor.MeshRenderer.sharedMaterial = on ? monitor.TargetMaterial : _blankScreenMaterial;
            }
        }

        public void UpdateMapMaterial(Material newMaterial)
        {
            if (_mapRenderer != null)
            {
                _mapRenderer.sharedMaterial = newMaterial;
            }
        }
    }
}