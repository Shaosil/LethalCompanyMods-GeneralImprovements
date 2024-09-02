using System.Collections.Generic;
using System.Linq;
using GeneralImprovements.API;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;

namespace GeneralImprovements.Assets
{
    internal class Monitors : MonoBehaviour
    {
        private MeshRenderer _mapRenderer;
        private Material _blankScreenMaterial;
        private Queue<MonitorsAPI.MonitorInfo> _queuedMonitorTextUpdates = new Queue<MonitorsAPI.MonitorInfo>();
        private bool _renderedLastFrame = false;

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

            // Disable all cameras and set them to have persistent history to reduce GC calls
            var allCameras = transform.GetComponentsInChildren<Camera>();
            foreach (var cam in allCameras)
            {
                cam.enabled = false;
                cam.GetComponent<HDAdditionalCameraData>().hasPersistentHistory = true;
            }

            // Add the transforms of each monitor (in order) that we will be using, depending on which config settings are active
            var allMonitors = new List<Transform>();
            if (Plugin.AddMoreBetterMonitors.Value) allMonitors.AddRange(new[] { structureL.Find("Screen1"), structureL.Find("Screen2") });
            allMonitors.AddRange(new[] { structureM.Find("Screen3"), structureM.Find("Screen4"), structureR.Find("Screen5"), structureR.Find("Screen6") });
            if (Plugin.AddMoreBetterMonitors.Value) allMonitors.AddRange(new[] { structureL.Find("Screen7"), structureL.Find("Screen8") });
            allMonitors.AddRange(new[] { structureM.Find("Screen9"), structureM.Find("Screen10"), structureR.Find("Screen11"), structureR.Find("Screen12") });
            if (Plugin.AddMoreBetterMonitors.Value) allMonitors.Add(bigScreenL.Find("LScreen"));
            allMonitors.Add(bigScreenR.Find("RScreen"));

            // Get the text objects (they are not children of our monitor objects above)
            var textObjects = transform.Find("Canvas/Texts").GetComponentsInChildren<TextMeshProUGUI>();
            var activeTexts = new List<TextMeshProUGUI>();
            for (int i = 0; i < textObjects.Length; i++)
            {
                // Skip the "more monitors" indexes if needed
                if (Plugin.AddMoreBetterMonitors.Value || !new[] { 0, 1, 6, 7, 12 }.Contains(i))
                {
                    activeTexts.Add(textObjects[i]);
                }
            }

            // Store anything that may have been overwritten so we can keep using the same materials after initialization
            var overwrittenMaterials = MonitorsAPI.AllMonitors.Where(m => m.Value.OverwrittenMaterial != null).ToDictionary(k => k.Key, v => v.Value.OverwrittenMaterial);

            // Initialize the TMP objects and assign all information to our API dictionary
            MonitorsAPI.AllMonitors = new Dictionary<int, MonitorsAPI.MonitorInfo>();
            for (int i = 0; i < allMonitors.Count; i++)
            {
                var screenText = activeTexts[i];
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
                    Camera = screenText.GetComponentInChildren<Camera>(),
                    MeshRenderer = renderer,
                    TextCanvas = (Plugin.ShowBackgroundOnAllScreens.Value || curAssignment > Enums.eMonitorNames.None) && curAssignment < Enums.eMonitorNames.ExternalCam ? screenText : null,
                    ScreenMaterialIndex = 0, // Our screen meshes are separate from the surrounding meshes and always only have one material
                    AssignedMaterial = renderer.sharedMaterial,
                    OverwrittenMaterial = overwrittenMaterials.GetValueOrDefault(i)
                };

                // No idea why, but left aligning the text needs an extra offset or it will be off screen
                if (!Plugin.CenterAlignMonitorText.Value)
                {
                    MonitorsAPI.AllMonitors[i].Camera.transform.localPosition = new Vector3(-10, 10, MonitorsAPI.AllMonitors[i].Camera.transform.localPosition.z);
                }
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

        private void Update()
        {
            // If we rendered last frame, make sure to disable the affected camera
            if (_renderedLastFrame)
            {
                foreach (var cam in MonitorsAPI.AllMonitors.Values.Select(m => m.Camera))
                {
                    if (cam.enabled)
                    {
                        cam.enabled = false;
                    }
                }
                _renderedLastFrame = false;
            }

            // If there is anything in the render queue, process one item
            if (_queuedMonitorTextUpdates.TryDequeue(out var monitor))
            {
                // Enable the camera - the render pipeline will pick it up later in the frame
                monitor.Camera.enabled = true;
                _renderedLastFrame = true;
            }
        }

        public bool RefreshMonitorAfterTextChange(MonitorsAPI.MonitorInfo monitor)
        {
            // Only render if this monitor has a text canvas associated with it and is not overwritten
            if (monitor.TextCanvas != null && monitor.MeshRenderer.sharedMaterial == monitor.AssignedMaterial)
            {
                // Add the change to the queue and let the next Update() handle one per frame
                _queuedMonitorTextUpdates.Enqueue(monitor);
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
                bool monitorOn = monitor.MeshRenderer.sharedMaterial != _blankScreenMaterial;

                // If the monitor is currently on and we have a new material (probably from another mod that started after us), overwrite our stored one
                if (monitorOn && monitor.MeshRenderer.sharedMaterial != monitor.TargetMaterial)
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