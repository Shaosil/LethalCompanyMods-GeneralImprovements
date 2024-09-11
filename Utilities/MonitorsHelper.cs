using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GeneralImprovements.API;
using GeneralImprovements.Assets;
using GeneralImprovements.Patches;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using static GeneralImprovements.Enums;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace GeneralImprovements.Utilities
{
    public static class MonitorsHelper
    {
        private static Vector3 _originalProfitQuotaLocation = Vector3.zero;
        private static Vector3 _originalProfitQuotaRotation = Vector3.zero;
        private static Image _originalProfitQuotaBG;
        private static TextMeshProUGUI _originalProfitQuotaText;
        private static Image _originalDeadlineBG;
        private static TextMeshProUGUI _originalDeadlineText;
        private static float _originalFontSize;

        private static List<TextMeshProUGUI> _profitQuotaTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _deadlineTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _averageDailyScrapMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _companyBuyRateMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _dailyProfitMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _shipScrapMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _scrapLeftMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _timeMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _weatherMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _fancyWeatherMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _salesMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _creditsMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _doorPowerMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _totalDaysMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _totalQuotasMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _totalDeathsMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _daysSinceDeathMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _dangerLevelMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _overtimeCalculatorMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _playerHealthMonitorTexts = new List<TextMeshProUGUI>();
        private static List<TextMeshProUGUI> _playerExactHealthMonitorTexts = new List<TextMeshProUGUI>();
        private static List<Image> _monitorBackgrounds = new List<Image>();

        private static bool _usingAnyMonitorTweaks = false;
        private static int _lastUpdatedCredits = -1;
        private static float _lastUpdatedDoorPower = -1, _updateDoorPowerTimer = 0;
        private static Monitors _newMonitors;
        private static Dictionary<TextMeshProUGUI, Action> _queuedMonitorRefreshes = new Dictionary<TextMeshProUGUI, Action>();

        // Weather animation
        private static int _curWeatherAnimIndex = 0;
        private static int _curWeatherOverlayIndex = 0;
        private static string[] _curWeatherAnimations = new string[0];
        private static string[] _curWeatherOverlays = new string[0];
        private static float _weatherAnimTimer = 0;
        private static float _weatherAnimCycle = 0.25f; // In seconds
        private static bool _weatherHasOverlays = false;
        private static float _weatherOverlayTimer = 0;
        private static bool _weatherShowingOverlay = false;
        private static float _weatherOverlayCycle; // In seconds, randomly assigned each time

        // Sales animation
        private static int _curSalesAnimIndex = 0;
        private static List<string> _curSalesAnimations = new List<string>();
        private static float _salesAnimTimer = 0;
        private static float _salesAnimCycle = 2f; // In seconds

        // Player health animation
        private static int _curPlayerHealthAnimIndex = 0;
        private static List<string> _curPlayerHealthAnimations = new List<string>();
        private static List<string> _curPlayerExactHealthAnimations = new List<string>();
        private static float _playerHealthAnimTimer = 0;
        private static float _playerHealthAnimCycle = 3f; // In seconds

        // Monitors on simple timers
        private static float _curCreditsUpdateCounter = 0;

        private static Transform _oldMonitorsObject;
        private static Transform _oldBigMonitors;
        private static Transform _UIContainer;
        private static ScanNodeProperties _profitQuotaScanNode;

        public static void InitializeMonitors(eMonitorNames[] monitorAssignments, bool includeCamSetup)
        {
            // Initialize a few necessary variables
            _originalProfitQuotaBG = StartOfRound.Instance.profitQuotaMonitorBGImage;
            _originalProfitQuotaText = StartOfRound.Instance.profitQuotaMonitorText;
            _originalDeadlineBG = StartOfRound.Instance.deadlineMonitorBGImage;
            _originalDeadlineText = StartOfRound.Instance.deadlineMonitorText;
            _originalFontSize = _originalProfitQuotaText.fontSize;
            _UIContainer = _originalProfitQuotaBG?.transform.parent;

            if (Plugin.CenterAlignMonitorText.Value)
            {
                _originalProfitQuotaText.alignment = TextAlignmentOptions.Center;
                _originalDeadlineText.alignment = TextAlignmentOptions.Center;
            }

            var internalShipCamObj = StartOfRound.Instance.elevatorTransform.Find("Cameras/ShipCamera")?.GetComponent<Camera>();
            var externalShipCamObj = StartOfRound.Instance.elevatorTransform.Find("Cameras/FrontDoorSecurityCam/SecurityCamera")?.GetComponent<Camera>();
            if (Plugin.DisableShipCamPostProcessing.Value)
            {
                if (internalShipCamObj != null) internalShipCamObj.GetComponent<HDAdditionalCameraData>().volumeLayerMask = 0;
                if (externalShipCamObj != null) externalShipCamObj.GetComponent<HDAdditionalCameraData>().volumeLayerMask = 0;
            }

            // Do nothing else if none of these are true - that means the user basically hasn't changed any of the default monitor config options
            if (!(Plugin.UseBetterMonitors.Value || !Plugin.ShowBlueMonitorBackground.Value || Plugin.ShowBackgroundOnAllScreens.Value || Plugin.SyncMonitorsFromOtherHost.Value
                || Plugin.MonitorBackgroundColor.Value != Plugin.MonitorBackgroundColor.DefaultValue.ToString() || Plugin.MonitorTextColor.Value != Plugin.MonitorTextColor.DefaultValue.ToString()
                || Plugin.ShipInternalCamFPS.Value != (int)Plugin.ShipInternalCamFPS.DefaultValue || Plugin.ShipInternalCamSizeMultiplier.Value != (int)Plugin.ShipInternalCamSizeMultiplier.DefaultValue
                || Plugin.ShipExternalCamFPS.Value != (int)Plugin.ShipExternalCamFPS.DefaultValue || Plugin.ShipExternalCamSizeMultiplier.Value != (int)Plugin.ShipExternalCamSizeMultiplier.DefaultValue
                || Plugin.ShipMonitorAssignments.Any(m => m.Value != (eMonitorNames?)m.DefaultValue)))
            {
                return;
            }
            _usingAnyMonitorTweaks = true;

            // Initialize things each time StartOfRound starts up
            _lastUpdatedCredits = -1;
            _lastUpdatedDoorPower = -1;
            RemoveExistingMonitors();

            // Resize the two extra monitor texts to be the same as their respective backgrounds, and give them padding
            _originalProfitQuotaBG.color = Plugin.MonitorBackgroundColorVal;
            _originalProfitQuotaText.color = Plugin.MonitorTextColorVal;
            _originalDeadlineBG.color = Plugin.MonitorBackgroundColorVal;
            _originalDeadlineText.color = Plugin.MonitorTextColorVal;
            _originalProfitQuotaText.rectTransform.sizeDelta = _originalProfitQuotaBG.rectTransform.sizeDelta;
            _originalProfitQuotaText.transform.position = _originalProfitQuotaBG.transform.TransformPoint(Vector3.back);
            _originalProfitQuotaText.fontSize = _originalFontSize * 0.9f;
            _originalProfitQuotaText.margin = Vector4.one * 5;
            _originalDeadlineText.rectTransform.sizeDelta = _originalDeadlineBG.rectTransform.sizeDelta;
            _originalDeadlineText.transform.position = _originalDeadlineBG.transform.TransformPoint(Vector3.back);
            _originalDeadlineText.fontSize = _originalFontSize * 0.9f;
            _originalDeadlineText.margin = Vector4.one * 5;

            // Find our monitor objects
            _oldMonitorsObject = _UIContainer.parent.parent;
            var oldMonitorsMeshRenderer = _oldMonitorsObject.GetComponent<MeshRenderer>();
            _oldBigMonitors = _oldMonitorsObject.parent.GetComponentInChildren<ManualCameraRenderer>().transform.parent;
            _profitQuotaScanNode = StartOfRound.Instance.elevatorTransform.GetComponentsInChildren<ScanNodeProperties>().FirstOrDefault(s => s.headerText == "Quota");

            // Increase internal and external ship cam resolution and FPS if specified
            if (includeCamSetup)
            {
                if (Plugin.ShipInternalCamFPS.Value != (int)Plugin.ShipInternalCamFPS.DefaultValue || Plugin.ShipInternalCamSizeMultiplier.Value != (int)Plugin.ShipInternalCamSizeMultiplier.DefaultValue)
                {
                    UpdateSecurityCamFPSAndResolution(internalShipCamObj, Plugin.ShipInternalCamFPS.Value, Plugin.ShipInternalCamSizeMultiplier.Value);
                }

                if (Plugin.ShipExternalCamFPS.Value != (int)Plugin.ShipExternalCamFPS.DefaultValue || Plugin.ShipExternalCamSizeMultiplier.Value != (int)Plugin.ShipExternalCamSizeMultiplier.DefaultValue)
                {
                    UpdateSecurityCamFPSAndResolution(externalShipCamObj, Plugin.ShipExternalCamFPS.Value, Plugin.ShipExternalCamSizeMultiplier.Value);
                }

                oldMonitorsMeshRenderer.sharedMaterials[2].mainTexture = internalShipCamObj.targetTexture;
                _oldBigMonitors.GetComponent<MeshRenderer>().sharedMaterials[2].mainTexture = externalShipCamObj.targetTexture;
            }

            // Initialize API items
            MonitorsAPI.AllMonitors = new Dictionary<int, MonitorsAPI.MonitorInfo>();
            MonitorsAPI.NewMonitorMeshActive = Plugin.AddMoreBetterMonitors.Value;
            MonitorsAPI.NumMonitorsActive = 0;

            if (Plugin.UseBetterMonitors.Value)
            {
                CreateNewStyleMonitors(monitorAssignments);
            }
            else
            {
                CreateOldStyleMonitors(monitorAssignments);

                // Disable the internal cam if we specified a custom monitor in that spot
                if (monitorAssignments[6] != eMonitorNames.None)
                {
                    var newMats = oldMonitorsMeshRenderer.sharedMaterials;
                    newMats[2] = newMats[1]; // Replace it with the blank screen texture
                    oldMonitorsMeshRenderer.sharedMaterials = newMats;
                }
            }

            // Initialize everything's text (some are initialized elsewhere)
            CopyProfitQuotaAndDeadlineTexts();
            UpdateAverageDailyScrapMonitors();
            UpdateCompanyBuyRateMonitors();
            UpdateDailyProfitMonitors();
            UpdateDangerLevelMonitors();
            UpdateDoorPowerMonitors();
            UpdateCalculatedScrapMonitors();
            UpdateTimeMonitors();
            UpdateWeatherMonitors();

            // Remove scan node if it no profit quota monitor exists
            if (_profitQuotaScanNode != null && !monitorAssignments.Any(a => a == eMonitorNames.ProfitQuota))
            {
                Object.Destroy(_profitQuotaScanNode.gameObject);
            }
        }

        private static void UpdateSecurityCamFPSAndResolution(Camera cam, int fps, int resMultiplier)
        {
            var camRenderer = cam.GetComponent<ManualCameraRenderer>();

            camRenderer.renderAtLowerFramerate = fps > 0;
            camRenderer.fps = fps;
            if (resMultiplier > 1)
            {
                int targetWidth = cam.targetTexture.width * (2 * resMultiplier);
                int targetHeight = cam.targetTexture.height * (2 * resMultiplier);
                var oldTex = cam.targetTexture;
                var newCamRT = new RenderTexture(targetWidth, targetHeight, cam.targetTexture.depth, cam.targetTexture.format);
                cam.targetTexture = newCamRT;
                Object.Destroy(oldTex);
            }
        }

        private static void RemoveExistingMonitors()
        {
            // Clears lists and removes game objects (for old style monitors). May be used if syncing from hosts, since we will need to delete and recreate monitors
            if (!Plugin.UseBetterMonitors.Value)
            {
                _profitQuotaTexts.ForEach(g => Object.Destroy(g));
                _deadlineTexts.ForEach(g => Object.Destroy(g));
                _averageDailyScrapMonitorTexts.ForEach(g => Object.Destroy(g));
                _companyBuyRateMonitorTexts.ForEach(g => Object.Destroy(g));
                _dailyProfitMonitorTexts.ForEach(g => Object.Destroy(g));
                _shipScrapMonitorTexts.ForEach(g => Object.Destroy(g));
                _scrapLeftMonitorTexts.ForEach(g => Object.Destroy(g));
                _timeMonitorTexts.ForEach(g => Object.Destroy(g));
                _weatherMonitorTexts.ForEach(g => Object.Destroy(g));
                _fancyWeatherMonitorTexts.ForEach(g => Object.Destroy(g));
                _salesMonitorTexts.ForEach(g => Object.Destroy(g));
                _creditsMonitorTexts.ForEach(g => Object.Destroy(g));
                _doorPowerMonitorTexts.ForEach(g => Object.Destroy(g));
                _totalDaysMonitorTexts.ForEach(g => Object.Destroy(g));
                _totalQuotasMonitorTexts.ForEach(g => Object.Destroy(g));
                _totalDeathsMonitorTexts.ForEach(g => Object.Destroy(g));
                _daysSinceDeathMonitorTexts.ForEach(g => Object.Destroy(g));
                _dangerLevelMonitorTexts.ForEach(g => Object.Destroy(g));
                _overtimeCalculatorMonitorTexts.ForEach(g => Object.Destroy(g));
                _playerHealthMonitorTexts.ForEach(g => Object.Destroy(g));
                _playerExactHealthMonitorTexts.ForEach(g => Object.Destroy(g));
                _monitorBackgrounds.ForEach(g => Object.Destroy(g));
            }
            else if (_newMonitors != null)
            {
                // Make sure any unknown overrides are up to date
                foreach (var overwritten in MonitorsAPI.AllMonitors.Where(m => m.Value.MeshRenderer.sharedMaterial != m.Value.TargetMaterial))
                {
                    Plugin.MLS.LogInfo($"Detected overwritten material on monitor {overwritten.Key + 1} ({overwritten.Value.MeshRenderer.sharedMaterial.name}). Saving before recreating monitors.");
                    overwritten.Value.OverwrittenMaterial = overwritten.Value.MeshRenderer.sharedMaterial;
                }

                Object.Destroy(_newMonitors.gameObject);
            }

            _profitQuotaTexts = new List<TextMeshProUGUI>();
            _deadlineTexts = new List<TextMeshProUGUI>();
            _averageDailyScrapMonitorTexts = new List<TextMeshProUGUI>();
            _companyBuyRateMonitorTexts = new List<TextMeshProUGUI>();
            _dailyProfitMonitorTexts = new List<TextMeshProUGUI>();
            _shipScrapMonitorTexts = new List<TextMeshProUGUI>();
            _scrapLeftMonitorTexts = new List<TextMeshProUGUI>();
            _timeMonitorTexts = new List<TextMeshProUGUI>();
            _weatherMonitorTexts = new List<TextMeshProUGUI>();
            _fancyWeatherMonitorTexts = new List<TextMeshProUGUI>();
            _salesMonitorTexts = new List<TextMeshProUGUI>();
            _creditsMonitorTexts = new List<TextMeshProUGUI>();
            _doorPowerMonitorTexts = new List<TextMeshProUGUI>();
            _totalDaysMonitorTexts = new List<TextMeshProUGUI>();
            _totalQuotasMonitorTexts = new List<TextMeshProUGUI>();
            _totalDeathsMonitorTexts = new List<TextMeshProUGUI>();
            _daysSinceDeathMonitorTexts = new List<TextMeshProUGUI>();
            _dangerLevelMonitorTexts = new List<TextMeshProUGUI>();
            _overtimeCalculatorMonitorTexts = new List<TextMeshProUGUI>();
            _playerHealthMonitorTexts = new List<TextMeshProUGUI>();
            _playerExactHealthMonitorTexts = new List<TextMeshProUGUI>();
            _monitorBackgrounds = new List<Image>();
        }

        private static void CreateOldStyleMonitors(eMonitorNames[] monitorAssignments)
        {
            Plugin.MLS.LogInfo("Creating old style monitor overlays.");

            // Copy everything from the existing quota monitor
            if (_originalProfitQuotaLocation == Vector3.zero)
            {
                _originalProfitQuotaLocation = _originalProfitQuotaBG.transform.localPosition;
            }
            if (_originalProfitQuotaRotation == Vector3.zero)
            {
                _originalProfitQuotaRotation = _originalProfitQuotaBG.transform.localEulerAngles;
            }
            _originalProfitQuotaBG.enabled = false;
            _originalProfitQuotaText.enabled = false;
            _originalDeadlineBG.enabled = false;
            _originalDeadlineText.enabled = false;
            _originalDeadlineBG.transform.localPosition = _originalProfitQuotaLocation;
            _originalDeadlineBG.transform.localEulerAngles = _originalProfitQuotaRotation;
            _originalDeadlineText.transform.localPosition = _originalProfitQuotaLocation;
            _originalDeadlineText.transform.localEulerAngles = _originalProfitQuotaRotation;

            // Store positions and rotations by offset based on monitor index
            var offsets = new List<KeyValuePair<Vector3, Vector3>>
            {
                new KeyValuePair<Vector3, Vector3>(new Vector3(0, 465, -22), new Vector3(-18, 0, 0)),       // Monitor 1
                new KeyValuePair<Vector3, Vector3>(new Vector3(470, 465, -22), new Vector3(-18, 0, 0)),     // Monitor 2
                new KeyValuePair<Vector3, Vector3>(new Vector3(970, 485, -128), new Vector3(-18, 25, 5)),   // Monitor 3
                new KeyValuePair<Vector3, Vector3>(new Vector3(1390, 525, -329), new Vector3(-18, 25, 5)),  // Monitor 4
                new KeyValuePair<Vector3, Vector3>(Vector3.zero, Vector3.zero),                             // Monitor 5
                new KeyValuePair<Vector3, Vector3>(new Vector3(470, 0, 0), Vector3.zero),                   // Monitor 6
                new KeyValuePair<Vector3, Vector3>(new Vector3(1025, 30, -115), new Vector3(-1, 25, 5)),    // Monitor 7
                new KeyValuePair<Vector3, Vector3>(new Vector3(1445, 72, -320), new Vector3(-1, 27, 5))     // Monitor 8
            };

            // Assign monitors to the positions that were specified, ensuring to not overlap
            for (int i = 0; i < offsets.Count; i++)
            {
                eMonitorNames curAssignment = monitorAssignments[i];
                List<TextMeshProUGUI> curTexts = null;

                switch (curAssignment)
                {
                    case eMonitorNames.AverageDailyScrap: curTexts = _averageDailyScrapMonitorTexts; break;
                    case eMonitorNames.CompanyBuyRate: curTexts = _companyBuyRateMonitorTexts; break;
                    case eMonitorNames.DailyProfit: curTexts = _dailyProfitMonitorTexts; break;
                    case eMonitorNames.Credits: curTexts = _creditsMonitorTexts; break;
                    case eMonitorNames.DangerLevel: curTexts = _dangerLevelMonitorTexts; break;
                    case eMonitorNames.DaysSinceDeath: curTexts = _daysSinceDeathMonitorTexts; break;
                    case eMonitorNames.Deadline: curTexts = _deadlineTexts; break;
                    case eMonitorNames.DoorPower: curTexts = _doorPowerMonitorTexts; break;
                    case eMonitorNames.FancyWeather: curTexts = _fancyWeatherMonitorTexts; break;
                    case eMonitorNames.OvertimeCalculator: curTexts = _overtimeCalculatorMonitorTexts; break;
                    case eMonitorNames.PlayerHealth: curTexts = _playerHealthMonitorTexts; break;
                    case eMonitorNames.PlayerHealthExact: curTexts = _playerExactHealthMonitorTexts; break;
                    case eMonitorNames.ProfitQuota: curTexts = _profitQuotaTexts; break;
                    case eMonitorNames.Sales: curTexts = _salesMonitorTexts; break;
                    case eMonitorNames.ScrapLeft: curTexts = _scrapLeftMonitorTexts; break;
                    case eMonitorNames.ShipScrap: curTexts = _shipScrapMonitorTexts; break;
                    case eMonitorNames.Time: curTexts = _timeMonitorTexts; break;
                    case eMonitorNames.TotalDays: curTexts = _totalDaysMonitorTexts; break;
                    case eMonitorNames.TotalDeaths: curTexts = _totalDeathsMonitorTexts; break;
                    case eMonitorNames.TotalQuotas: curTexts = _totalQuotasMonitorTexts; break;
                    case eMonitorNames.Weather: curTexts = _weatherMonitorTexts; break;
                }

                if (curTexts == null && curAssignment != eMonitorNames.None)
                {
                    Plugin.MLS.LogError($"Could not find '{curAssignment}' for monitor assignment! Please check your config is using acceptable values.");
                }

                var positionOffset = offsets[i].Key;
                var rotationOffset = offsets[i].Value;

                // Create a background if desired, and we either have a text assignment or want to show the blue backgrounds everywhere (except the internal cam spot)
                if (Plugin.ShowBlueMonitorBackground.Value && (curTexts != null || (Plugin.ShowBackgroundOnAllScreens.Value && i != 6)))
                {
                    var newBG = Object.Instantiate(_originalProfitQuotaBG, _originalProfitQuotaBG.transform.parent);
                    newBG.enabled = true;
                    newBG.name = $"{(curAssignment == eMonitorNames.None ? "ExtraBackground" : curAssignment.ToString())}BG{i + 1}";

                    newBG.transform.localPosition = _originalProfitQuotaLocation + positionOffset;
                    newBG.transform.localEulerAngles = _originalProfitQuotaRotation + rotationOffset;
                    _monitorBackgrounds.Add(newBG);
                }

                // Text will be null if this is a blank background
                if (curTexts != null)
                {
                    Plugin.MLS.LogInfo($"Creating {curAssignment} monitor at position {i + 1}");
                    var newText = Object.Instantiate(_originalProfitQuotaText, _originalProfitQuotaText.transform.parent);
                    newText.enabled = true;
                    newText.name = $"{curAssignment}Text{i + 1}";

                    newText.transform.localPosition = _originalProfitQuotaLocation + positionOffset + new Vector3(0, 0, -1);
                    newText.transform.localEulerAngles = _originalProfitQuotaRotation + rotationOffset + new Vector3(1, 0, 0); // Text needs a little rotational offset for some reason
                    curTexts.Add(newText);

                    if (curAssignment == eMonitorNames.ProfitQuota && _profitQuotaScanNode != null)
                    {
                        _profitQuotaScanNode.transform.parent = _profitQuotaTexts[0].transform;
                        _profitQuotaScanNode.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    }
                }
            }

            // Modify specialized text fields
            foreach (var sales in _salesMonitorTexts)
            {
                sales.overflowMode = TextOverflowModes.Ellipsis;
            }
            foreach (var fancyWeather in _fancyWeatherMonitorTexts)
            {
                fancyWeather.alignment = TextAlignmentOptions.MidlineLeft;
                fancyWeather.transform.localPosition += new Vector3(25, 0, -10);
                fancyWeather.transform.localEulerAngles += new Vector3(-2, 1, 0);
            }
            foreach (var healthMonitor in _playerHealthMonitorTexts.Concat(_playerExactHealthMonitorTexts))
            {
                healthMonitor.fontSize = 30f;
                healthMonitor.enableWordWrapping = false;
                healthMonitor.alignment = TextAlignmentOptions.MidlineLeft;
            }
        }

        private static void CreateNewStyleMonitors(eMonitorNames[] monitorAssignments)
        {
            Plugin.MLS.LogInfo($"Creating new style monitors (additional monitors: {Plugin.AddMoreBetterMonitors.Value}).");

            var newMonitorsObj = Object.Instantiate(AssetBundleHelper.MonitorsPrefab, _oldMonitorsObject.transform.parent);
            newMonitorsObj.transform.SetLocalPositionAndRotation(_oldMonitorsObject.localPosition, Quaternion.identity);

            _newMonitors = newMonitorsObj.AddComponent<Monitors>();
            MonitorsAPI.NewMonitors = _newMonitors;
            var oldMonitorsMeshRenderer = _oldMonitorsObject.GetComponent<MeshRenderer>();
            var oldBigMonitorsMeshRenderer = _oldBigMonitors.GetComponent<MeshRenderer>();
            _newMonitors.Initialize(oldMonitorsMeshRenderer.sharedMaterials[0], oldBigMonitorsMeshRenderer.sharedMaterials[1], oldMonitorsMeshRenderer.sharedMaterials[1]);

            var internalCamMat = oldMonitorsMeshRenderer.sharedMaterials.FirstOrDefault(m => m.name.StartsWith("ShipScreen"));
            var externalCamMat = oldBigMonitorsMeshRenderer.sharedMaterials.FirstOrDefault(m => m.name.StartsWith("ShipScreen"));

            // Assign specified TMP objects to the monitor indexes specified
            for (int i = 0; i < (Plugin.AddMoreBetterMonitors.Value ? 14 : 9) && i < monitorAssignments.Length; i++)
            {
                eMonitorNames curAssignment = monitorAssignments[i];
                Plugin.MLS.LogInfo($"Creating {curAssignment} monitor at position {i + 1}");
                var curMonitor = MonitorsAPI.AllMonitors[i];

                switch (curAssignment)
                {
                    case eMonitorNames.AverageDailyScrap: _averageDailyScrapMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.CompanyBuyRate: _companyBuyRateMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.DailyProfit: _dailyProfitMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.Credits: _creditsMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.DangerLevel: _dangerLevelMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.DaysSinceDeath: _daysSinceDeathMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.Deadline: _deadlineTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.DoorPower: _doorPowerMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.FancyWeather:
                        if (Plugin.CenterAlignMonitorText.Value)
                        {
                            curMonitor.TextCanvas.alignment = TextAlignmentOptions.MidlineLeft;
                            curMonitor.TextCanvas.margin += new Vector4(20, 10, 0, 0);
                        }
                        else
                        {
                            curMonitor.TextCanvas.alignment = TextAlignmentOptions.TopLeft;
                            curMonitor.TextCanvas.margin += new Vector4(10, 10, 0, 0);
                        }
                        _fancyWeatherMonitorTexts.Add(curMonitor.TextCanvas);
                        UpdateWeatherMonitors();
                        break;
                    case eMonitorNames.OvertimeCalculator: _overtimeCalculatorMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.PlayerHealth:
                    case eMonitorNames.PlayerHealthExact:
                        curMonitor.TextCanvas.fontSize = 20f;
                        curMonitor.TextCanvas.enableWordWrapping = false;
                        curMonitor.TextCanvas.alignment = TextAlignmentOptions.MidlineLeft;
                        curMonitor.TextCanvas.margin = new Vector4(20, 5, 20, 5);
                        if (curAssignment == eMonitorNames.PlayerHealth) _playerHealthMonitorTexts.Add(curMonitor.TextCanvas);
                        else _playerExactHealthMonitorTexts.Add(curMonitor.TextCanvas);
                        break;
                    case eMonitorNames.ProfitQuota:
                        _profitQuotaTexts.Add(curMonitor.TextCanvas);
                        if (_profitQuotaScanNode != null)
                        {
                            _profitQuotaScanNode.transform.parent = curMonitor.MeshRenderer.transform;
                            _profitQuotaScanNode.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                        }
                        CopyProfitQuotaAndDeadlineTexts();
                        break;
                    case eMonitorNames.Sales: curMonitor.TextCanvas.overflowMode = TextOverflowModes.Ellipsis; _salesMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.ScrapLeft: _scrapLeftMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.ShipScrap: _shipScrapMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.Time: _timeMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.TotalDays: _totalDaysMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.TotalDeaths: _totalDeathsMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.TotalQuotas: _totalQuotasMonitorTexts.Add(curMonitor.TextCanvas); break;
                    case eMonitorNames.Weather: _weatherMonitorTexts.Add(curMonitor.TextCanvas); break;

                    case eMonitorNames.ExternalCam:
                        // Reassign the external camera mesh to the most recent mesh that was applied
                        curMonitor.AssignedMaterial = externalCamMat;
                        curMonitor.MeshRenderer.sharedMaterial = externalCamMat;
                        StartOfRound.Instance.elevatorTransform.Find("Cameras/FrontDoorSecurityCam/SecurityCamera").GetComponent<ManualCameraRenderer>().mesh = curMonitor.MeshRenderer;
                        break;
                    case eMonitorNames.InternalCam:
                        // Reassign the internal camera mesh to the most recent mesh that was applied
                        curMonitor.AssignedMaterial = internalCamMat;
                        curMonitor.MeshRenderer.sharedMaterial = internalCamMat;
                        StartOfRound.Instance.elevatorTransform.Find("Cameras/ShipCamera").GetComponent<ManualCameraRenderer>().mesh = curMonitor.MeshRenderer;
                        break;
                }
            }

            // Hide the left side if we are not adding more monitors
            var topLeftGroup = newMonitorsObj.transform.Find("Monitors/TopGroupL");
            var bigLeft = newMonitorsObj.transform.Find("Monitors/BigLeft");
            if (topLeftGroup != null) topLeftGroup.gameObject.SetActive(Plugin.AddMoreBetterMonitors.Value);
            if (bigLeft != null) bigLeft.gameObject.SetActive(Plugin.AddMoreBetterMonitors.Value);

            // Assign our middle screen's mesh to the manual camera renderer script of the main screen
            var newMesh = newMonitorsObj.transform.Find("Monitors/BigMiddle").GetComponent<MeshRenderer>();
            StartOfRound.Instance.mapScreen.mesh = newMesh;

            // If we are not displaying the external cam anywhere but it's still over the door, use that as the mesh instead
            var doorCamMesh = StartOfRound.Instance.elevatorTransform.Find("ShipModels2b/MonitorWall/SingleScreen")?.GetComponent<MeshRenderer>();
            if (!MonitorsAPI.AllMonitors.Values.Any(m => m.AssignedMaterial == externalCamMat) && doorCamMesh != null && doorCamMesh.sharedMaterials.Any(m => m.name.StartsWith("ShipScreen2")))
            {
                StartOfRound.Instance.elevatorTransform.Find("Cameras/FrontDoorSecurityCam/SecurityCamera").GetComponent<ManualCameraRenderer>().mesh = doorCamMesh;
            }
        }

        public static void HideOldMonitors()
        {
            Plugin.MLS.LogInfo("Hiding old monitors");

            if (_oldBigMonitors != null && _oldMonitorsObject != null && _UIContainer != null)
            {
                // Hide the old monitor objects and update the ManualCameraRender meshes to null so the cams do not get disabled
                _oldBigMonitors.GetComponent<MeshRenderer>().enabled = false;
                _oldBigMonitors.GetComponent<Collider>().enabled = false;
                _oldMonitorsObject.GetComponent<MeshRenderer>().enabled = false;
                _oldMonitorsObject.GetComponent<Collider>().enabled = false;
                for (int i = 0; i < _UIContainer.childCount; i++)
                {
                    if (_UIContainer.GetChild(i).GetComponent<Image>() is Image img)
                    {
                        img.enabled = false;
                    }
                    else if (_UIContainer.GetChild(i).GetComponent<TextMeshProUGUI>() is TextMeshProUGUI txt)
                    {
                        txt.enabled = false;
                    }
                }
            }
        }

        public static void CopyProfitQuotaAndDeadlineTexts()
        {
            if (_profitQuotaTexts.Count > 0 || _deadlineTexts.Count > 0)
            {
                // Apply color to deadline text if possible
                string deadlineText = StartOfRound.Instance.deadlineMonitorText?.text;
                if (!string.IsNullOrWhiteSpace(deadlineText))
                {
                    // Just read the days remaining from the text to be sure the applied color makes sense
                    var match = Regex.Match(deadlineText, "(\\d+) DAYS?", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        int days = int.Parse(match.Groups[1].Value);
                        string color = days >= 3 ? "00ff00" : days == 2 ? "ffff00" : days == 1 ? "ff8800" : "ff0000";
                        deadlineText = deadlineText.Replace(match.Groups[0].Value, $"<color=#{color}>{match.Groups[0].Value}</color>");
                    }
                }

                if (UpdateGenericTextList(_profitQuotaTexts, StartOfRound.Instance.profitQuotaMonitorText?.text)
                    & UpdateGenericTextList(_deadlineTexts, deadlineText))
                {
                    Plugin.MLS.LogInfo("Updated profit quota and deadline monitors");
                }
            }
        }

        public static void UpdateAverageDailyScrapMonitors()
        {
            if (_averageDailyScrapMonitorTexts.Count > 0)
            {
                double avg = StartOfRoundPatch.DailyScrapCollected.Count > 0 ? StartOfRoundPatch.DailyScrapCollected.Values.Average() : 0;
                if (UpdateGenericTextList(_averageDailyScrapMonitorTexts, $"AVERAGE SCRAP:\n${Math.Round(avg, 0)}"))
                {
                    Plugin.MLS.LogInfo($"Updated average daily scrap monitors.");
                }
            }
        }

        public static void UpdateCompanyBuyRateMonitors()
        {
            if (_companyBuyRateMonitorTexts.Count > 0 && StartOfRound.Instance != null)
            {
                string color = StartOfRound.Instance.companyBuyingRate > 1 ? "0080ff"
                    : StartOfRound.Instance.companyBuyingRate == 1 ? "00ff00"
                    : StartOfRound.Instance.companyBuyingRate >= 0.75f ? "ffff00"
                    : StartOfRound.Instance.companyBuyingRate >= 0.5f ? "ff8800"
                    : "ff0000";

                if (UpdateGenericTextList(_companyBuyRateMonitorTexts, $"COMPANY RATE:\n<color=#{color}>{(int)Mathf.Round(StartOfRound.Instance.companyBuyingRate * 100f)}%</color>"))
                {
                    Plugin.MLS.LogInfo($"Updated company buy rate monitors.");
                }
            }
        }

        public static void UpdateCalculatedScrapMonitors(bool pending = false)
        {
            // Condense the scrap loading into a single call and use it down the chain if needed
            var allScrap = new List<GrabbableObject>();
            var shipScrap = new List<GrabbableObject>();
            var outsideScrap = new List<GrabbableObject>();
            if (_shipScrapMonitorTexts.Count > 0 || _scrapLeftMonitorTexts.Count > 0 || _overtimeCalculatorMonitorTexts.Count > 0)
            {
                allScrap = GrabbableObjectsPatch.GetAllScrap();
                foreach (var scrap in allScrap)
                {
                    if (scrap.isInShipRoom && scrap.isInElevator) shipScrap.Add(scrap);
                    else outsideScrap.Add(scrap);
                }
            }

            // Ship scrap
            if (_shipScrapMonitorTexts.Count > 0)
            {
                int scrapValue = shipScrap.Sum(s => s.scrapValue);

                if (UpdateGenericTextList(_shipScrapMonitorTexts, $"SHIP SCRAP:\n<color=#80ffff>${scrapValue}</color>\n({shipScrap.Count} ITEMS)"))
                {
                    Plugin.MLS.LogInfo($"Updated ship scrap total monitors to ${scrapValue} ({shipScrap.Count} items).");
                }
            }

            // Scrap left
            if (_scrapLeftMonitorTexts.Count > 0)
            {
                var outsideScrapKeyValues = GrabbableObjectsPatch.GetScrapAmountAndValue(!Plugin.ScanCommandUsesExactAmount.Value, outsideScrap);
                bool updatedText = false;

                if (outsideScrapKeyValues.Key > 0)
                {
                    string display = pending ? "(PENDING)" : $"{outsideScrapKeyValues.Key} ITEMS\n<color=#80ffff>${outsideScrapKeyValues.Value}</color>";
                    updatedText = UpdateGenericTextList(_scrapLeftMonitorTexts, $"SCRAP LEFT:\n{display}");
                }
                else
                {
                    updatedText = UpdateGenericTextList(_scrapLeftMonitorTexts, "NO EXTERNAL SCRAP DETECTED");
                }

                if (updatedText)
                {
                    Plugin.MLS.LogInfo("Updated remaining scrap display.");
                }
            }

            // Overtime estimate
            if (_overtimeCalculatorMonitorTexts.Count > 0)
            {
                int overtime = 0;
                if (TimeOfDay.Instance != null)
                {
                    // Count all scrap on the company, ship scrap otherwise
                    var scrapValue = (RoundManager.Instance?.currentLevel?.PlanetName == "71 Gordion" ? allScrap : shipScrap).Sum(s => s.scrapValue);
                    var remainingQuota = TimeOfDay.Instance.profitQuota - TimeOfDay.Instance.quotaFulfilled;
                    var profit = scrapValue - remainingQuota;
                    overtime = profit <= 0 ? 0 : (profit / 5) - 15;
                }

                if (UpdateGenericTextList(_overtimeCalculatorMonitorTexts, $"OVERTIME ESTIMATE:\n${Mathf.Max(overtime, 0)}"))
                {
                    Plugin.MLS.LogInfo("Updated overtime calculator display.");
                }
            }
        }

        public static void UpdateDailyProfitMonitors()
        {
            if (_dailyProfitMonitorTexts.Count > 0 && RoundManager.Instance != null)
            {
                int profit = RoundManager.Instance.scrapCollectedThisRound.Sum(s => s.scrapValue);
                if (UpdateGenericTextList(_dailyProfitMonitorTexts, $"DAY'S PROFIT:\n${profit}"))
                {
                    Plugin.MLS.LogInfo($"Updated daily profit monitors. ({RoundManager.Instance.scrapCollectedThisRound.Count} items, {profit} profit)");
                }
            }
        }

        public static void UpdateTimeMonitors()
        {
            if (_timeMonitorTexts.Count > 0)
            {
                string time;

                if (TimeOfDay.Instance.movingGlobalTimeForward && HUDManager.Instance?.clockNumber != null)
                {
                    time = $"TIME:\n{HUDManager.Instance.clockNumber.text.Replace('\n', ' ')}";
                }
                else
                {
                    time = "TIME:\n(PENDING)";
                }

                if (UpdateGenericTextList(_timeMonitorTexts, time))
                {
                    Plugin.MLS.LogDebug("Updated time display.");
                }
            }
        }

        public static void UpdateWeatherMonitors()
        {
            if (_weatherMonitorTexts.Count > 0 || _fancyWeatherMonitorTexts.Count > 0)
            {
                if (_weatherMonitorTexts.Count > 0)
                {
                    if (UpdateGenericTextList(_weatherMonitorTexts, $"WEATHER:\n{(StartOfRound.Instance.currentLevel?.currentWeather.ToString() ?? string.Empty)}"))
                    {
                        Plugin.MLS.LogInfo("Updated basic weather monitors");
                    }
                }

                if (_fancyWeatherMonitorTexts.Count > 0)
                {
                    // Change the animation we are currently referencing
                    _curWeatherAnimations = StartOfRound.Instance.currentLevel?.currentWeather switch
                    {
                        LevelWeatherType.None => WeatherASCIIArt.ClearAnimations,
                        LevelWeatherType.Rainy => WeatherASCIIArt.RainAnimations,
                        LevelWeatherType.Stormy => WeatherASCIIArt.RainAnimations,
                        LevelWeatherType.Foggy => WeatherASCIIArt.FoggyAnimations,
                        LevelWeatherType.Flooded => WeatherASCIIArt.FloodedAnimations,
                        LevelWeatherType.Eclipsed => WeatherASCIIArt.EclipsedAnimations,
                        _ => new string[] { string.Empty }
                    };

                    _weatherHasOverlays = StartOfRound.Instance.currentLevel?.currentWeather == LevelWeatherType.Stormy;
                    if (_weatherHasOverlays)
                    {
                        _weatherOverlayTimer = 0;
                        _weatherOverlayCycle = Random.Range(0.1f, 3);
                        _curWeatherOverlays = WeatherASCIIArt.LightningOverlays;
                        _curWeatherOverlayIndex = Random.Range(0, _curWeatherOverlays.Length);
                    }
                    _weatherShowingOverlay = false;

                    _curWeatherAnimIndex = 0;
                    _weatherAnimTimer = 0;

                    if (UpdateGenericTextList(_fancyWeatherMonitorTexts, _curWeatherAnimations[_curWeatherAnimIndex]))
                    {
                        Plugin.MLS.LogInfo("Updated fancy weather monitors");
                    }
                }
            }
        }

        public static void AnimateSpecialMonitors()
        {
            if (_fancyWeatherMonitorTexts.Count > 0 && _curWeatherAnimations.Length >= 2)
            {
                Action drawWeather = () =>
                {
                    var sb = new StringBuilder();
                    string[] animLines = _curWeatherAnimations[_curWeatherAnimIndex].Split(Environment.NewLine);
                    string[] overlayLines = (_weatherShowingOverlay ? _curWeatherOverlays[_curWeatherOverlayIndex] : string.Empty).Split(Environment.NewLine);

                    // Loop through each line of the current animation frame, overwriting any characters with a matching overlay character if one exists
                    for (int l = 0; l < animLines.Length; l++)
                    {
                        string curAnimLine = animLines[l];
                        string overlayLine = overlayLines.ElementAtOrDefault(l);

                        for (int c = 0; c < curAnimLine.Length; c++)
                        {
                            bool isOverlayChar = !string.IsNullOrWhiteSpace(overlayLine) && overlayLine.Length > c && overlayLine[c] != ' ';
                            sb.Append(isOverlayChar ? $"<color=#ffe100>{overlayLine[c]}</color>" : $"{curAnimLine[c]}");
                        }
                        sb.AppendLine();
                    }

                    UpdateGenericTextList(_fancyWeatherMonitorTexts, sb.ToString());
                };

                // Cycle through our current animation pattern 'sprites'
                _weatherAnimTimer += Time.deltaTime;
                if (_weatherAnimTimer >= _weatherAnimCycle)
                {
                    _curWeatherAnimIndex = (_curWeatherAnimIndex + 1) % _curWeatherAnimations.Length;
                    _weatherAnimTimer = 0;
                    drawWeather();
                }

                // Handle random overlays
                if (_weatherHasOverlays)
                {
                    _weatherOverlayTimer += Time.deltaTime;
                    if (_weatherOverlayTimer >= (_weatherShowingOverlay ? 0.5f : _weatherOverlayCycle))
                    {
                        _weatherOverlayTimer = 0;
                        _weatherShowingOverlay = !_weatherShowingOverlay;

                        if (!_weatherShowingOverlay)
                        {
                            // Reset the counter for the next overlay
                            _weatherOverlayCycle = Random.Range(0.1f, 3);
                            _curWeatherOverlayIndex = Random.Range(0, _curWeatherOverlays.Length);
                        }

                        drawWeather();
                    }
                }
            }

            // Sales monitors
            if (_salesMonitorTexts.Count > 0 && _curSalesAnimations.Count >= 2)
            {
                _salesAnimTimer += Time.deltaTime;
                if (_salesAnimTimer >= _salesAnimCycle)
                {
                    _salesAnimTimer = 0;
                    _curSalesAnimIndex = (1 + _curSalesAnimIndex) % _curSalesAnimations.Count;
                    string firstLine = _salesMonitorTexts.First().text.Split('\n')[0];
                    UpdateGenericTextList(_salesMonitorTexts, $"{firstLine}\n{_curSalesAnimations[_curSalesAnimIndex]}");
                }
            }

            // Player health monitors (only check _curPlayerHealthAnimations count because it will always have the same as the exact one)
            if ((_playerHealthMonitorTexts.Count > 0 || _playerExactHealthMonitorTexts.Count > 0) && _curPlayerHealthAnimations.Count >= 2)
            {
                _playerHealthAnimTimer += Time.deltaTime;
                if (_playerHealthAnimTimer >= _playerHealthAnimCycle)
                {
                    _playerHealthAnimTimer = 0;
                    _curPlayerHealthAnimIndex = (1 + _curPlayerHealthAnimIndex) % _curPlayerHealthAnimations.Count;
                    if (_playerHealthMonitorTexts.Count > 0) UpdateGenericTextList(_playerHealthMonitorTexts, _curPlayerHealthAnimations[_curPlayerHealthAnimIndex]);
                    if (_playerExactHealthMonitorTexts.Count > 0) UpdateGenericTextList(_playerExactHealthMonitorTexts, _curPlayerExactHealthAnimations[_curPlayerHealthAnimIndex]);
                }
            }
        }

        public static void UpdateSalesMonitors()
        {
            if (_salesMonitorTexts.Count > 0 && TerminalPatch.Instance != null)
            {
                var instance = TerminalPatch.Instance;
                int numSales = instance.itemSalesPercentages.Count(s => s < 100);
                _curSalesAnimIndex = 0;
                _curSalesAnimations = new List<string>();

                if (numSales > 0)
                {
                    for (int i = 0; i < instance.itemSalesPercentages.Length; i++)
                    {
                        if (instance.itemSalesPercentages[i] < 100 && instance.buyableItemsList.Length > i)
                        {
                            string item = instance.buyableItemsList[i]?.itemName ?? "???";
                            _curSalesAnimations.Add($"<color=#00ff00>{100 - instance.itemSalesPercentages[i]}% OFF {item}</color>");
                        }
                    }
                }

                bool updatedText = false;
                if (numSales <= 0)
                {
                    updatedText = UpdateGenericTextList(_salesMonitorTexts, "NO SALES TODAY");
                }
                else if (_curSalesAnimations.Count > 0)
                {
                    updatedText = UpdateGenericTextList(_salesMonitorTexts, $"{numSales} SALE{(numSales == 1 ? string.Empty : "S")}:\n{_curSalesAnimations[0]}");
                }

                if (updatedText)
                {
                    Plugin.MLS.LogInfo("Updated sales display.");
                }
            }
        }

        public static void UpdateCreditsMonitors()
        {
            // This is getting called every frame, so limit the check to a few times per second
            _curCreditsUpdateCounter -= Time.deltaTime;
            if (_curCreditsUpdateCounter <= 0)
            {
                _curCreditsUpdateCounter = 0.25f;

                // Only update if there is a change
                var groupCredits = TerminalPatch.Instance?.groupCredits ?? -1;
                if (_creditsMonitorTexts.Count > 0 && groupCredits != _lastUpdatedCredits)
                {
                    _lastUpdatedCredits = groupCredits;

                    if (UpdateGenericTextList(_creditsMonitorTexts, $"CREDITS:\n<color=#ffff00>${_lastUpdatedCredits}</color>"))
                    {
                        Plugin.MLS.LogInfo("Updated credits display.");
                    }
                }
            }
        }

        public static void UpdateDoorPowerMonitors(bool force = false)
        {
            if (_updateDoorPowerTimer > 0)
            {
                _updateDoorPowerTimer -= Time.deltaTime;
            }

            // Only update if there is a change
            float doorPower = HangarShipDoorPatch.Instance?.doorPower ?? 1;
            if (_doorPowerMonitorTexts.Count > 0 && (force || (_lastUpdatedDoorPower != doorPower && _updateDoorPowerTimer <= 0)))
            {
                _lastUpdatedDoorPower = doorPower;
                UpdateGenericTextList(_doorPowerMonitorTexts, $"DOOR POWER:\n{Mathf.RoundToInt(_lastUpdatedDoorPower * 100)}%");

                // Limit FPS to 10
                _updateDoorPowerTimer = 0.1f;
            }
        }

        public static void UpdateTotalDaysMonitors()
        {
            if (_totalDaysMonitorTexts.Count > 0 && StartOfRound.Instance.gameStats != null)
            {
                if (UpdateGenericTextList(_totalDaysMonitorTexts, $"DAY {StartOfRound.Instance.gameStats.daysSpent + 1}"))
                {
                    Plugin.MLS.LogInfo("Updated total days display.");
                }
            }
        }

        public static void UpdateTotalQuotasMonitors()
        {
            if (_totalQuotasMonitorTexts.Count > 0 && TimeOfDay.Instance != null)
            {
                if (UpdateGenericTextList(_totalQuotasMonitorTexts, $"QUOTA {TimeOfDay.Instance.timesFulfilledQuota + 1}"))
                {
                    Plugin.MLS.LogInfo("Updated total quotas display.");
                }
            }
        }

        public static void UpdateDeathMonitors(bool? playersDied = null)
        {
            if (_totalDeathsMonitorTexts.Count > 0 && StartOfRound.Instance?.gameStats != null)
            {
                int totalDeaths = StartOfRound.Instance.gameStats.deaths;
                if (UpdateGenericTextList(_totalDeathsMonitorTexts, $"TOTAL DEATHS:\n<color=#{(totalDeaths <= 0 ? "00ff00" : "ff0000")}>{totalDeaths}</color>"))
                {
                    Plugin.MLS.LogInfo("Updated total deaths display.");
                }
            }

            if (_daysSinceDeathMonitorTexts.Count > 0 && StartOfRound.Instance != null)
            {
                if (playersDied.HasValue)
                {
                    if (playersDied.GetValueOrDefault())
                    {
                        // There was a death - reset counter
                        StartOfRoundPatch.DaysSinceLastDeath = 0;
                    }
                    else if (StartOfRoundPatch.DaysSinceLastDeath >= 0)
                    {
                        // No death this time, but there have been at some point - increment counter
                        StartOfRoundPatch.DaysSinceLastDeath++;
                    }
                }

                bool updatedText = false;
                if (StartOfRoundPatch.DaysSinceLastDeath >= 0)
                {
                    updatedText = UpdateGenericTextList(_daysSinceDeathMonitorTexts, $"{StartOfRoundPatch.DaysSinceLastDeath} DAY{(StartOfRoundPatch.DaysSinceLastDeath == 1 ? string.Empty : "S")} WITHOUT DEATHS");
                }
                else
                {
                    updatedText = UpdateGenericTextList(_daysSinceDeathMonitorTexts, "<color=#00ff00>ZERO DEATHS (YET)</color>");
                }

                if (updatedText)
                {
                    Plugin.MLS.LogInfo("Updated days since death display.");
                }
            }
        }

        public static void UpdateDangerLevelMonitors()
        {
            if (_dangerLevelMonitorTexts.Count > 0 && RoundManager.Instance != null)
            {
                float totalMaxPower = RoundManager.Instance.currentMaxInsidePower + RoundManager.Instance.currentMaxOutsidePower;
                var dangerLevels = new[] { "SAFE", "WARNING", "HAZARDOUS", "DANGEROUS", "LETHAL" };
                var colorHexes = new[] { "00ff00", "c8ff00", "ffc400", "ff6a00", "ff0000" };
                int curDanger = EnemyAIPatch.CurTotalPowerLevel <= 0 || totalMaxPower <= 0 ? 0 : (int)Mathf.Ceil(Mathf.Clamp(EnemyAIPatch.CurTotalPowerLevel / totalMaxPower, 0, 1) * 4);

                if (UpdateGenericTextList(_dangerLevelMonitorTexts, $"DANGER LEVEL:\n<color=#{colorHexes[curDanger]}>{dangerLevels[curDanger]}</color>"))
                {
                    Plugin.MLS.LogDebug("Updated danger level display.");
                }
            }
        }

        public static void UpdatePlayerHealthMonitors()
        {
            if (_playerHealthMonitorTexts.Count > 0 || _playerExactHealthMonitorTexts.Count > 0)
            {
                // Set up animation stuff
                int playerPerPage = 5;
                var activePlayers = StartOfRound.Instance.allPlayerScripts.Where(p => p.isPlayerControlled || p.isPlayerDead).ToList();

                // Break early if there are no players
                if (activePlayers.Count <= 0) return;

                int numAnimationsNeeded = Mathf.CeilToInt(activePlayers.Count / (float)playerPerPage);
                if (numAnimationsNeeded != _curPlayerHealthAnimations.Count)
                {
                    _curPlayerHealthAnimIndex = 0;
                    _curPlayerHealthAnimations = new List<string>();
                    _curPlayerExactHealthAnimations = new List<string>();
                }

                // Create strings for both exact and non exact
                for (int a = 0; a < numAnimationsNeeded; a++)
                {
                    if (_curPlayerHealthAnimations.Count <= a)
                    {
                        _curPlayerHealthAnimations.Add(string.Empty);
                        _curPlayerExactHealthAnimations.Add(string.Empty);
                    }

                    var curScreenPlayersSb = new StringBuilder();
                    var curScreenPlayersExactSb = new StringBuilder();

                    // Process a page at a time
                    for (int p = 0; p < playerPerPage && (a * playerPerPage) + p < activePlayers.Count; p++)
                    {
                        var curPlayer = activePlayers[(a * playerPerPage) + p];

                        string displayName = new string(curPlayer.playerUsername.Take(12).Concat(new char[] { ':' }).ToArray()).PadRight(20);
                        int healthLevel = curPlayer.health >= 100 ? 3 : curPlayer.health >= 50 ? 2 : 1;
                        string healthColor = healthLevel == 3 ? "00ff00" : healthLevel == 2 ? "ffff00" : "ff0000";

                        curScreenPlayersSb.AppendLine($" {displayName} <color=#{healthColor}>{new string('=', healthLevel).PadRight(3)}</color> ");
                        curScreenPlayersExactSb.AppendLine($" {displayName} <color=#{healthColor}>{curPlayer.health,3}</color> ");
                    }

                    string header = $"{new string(' ', 10)}PLAYER HEALTH:\n\n";
                    string footer = numAnimationsNeeded > 1 ? $"\n\n{new string(' ', 12)}PAGE {a + 1} OF {numAnimationsNeeded}" : string.Empty;
                    _curPlayerHealthAnimations[a] = $"{header}{curScreenPlayersSb}{footer}";
                    _curPlayerExactHealthAnimations[a] = $"{header}{curScreenPlayersExactSb}{footer}";
                }

                // Update the text of the current animation cycle
                if ((_playerHealthMonitorTexts.Count > 0 && UpdateGenericTextList(_playerHealthMonitorTexts, _curPlayerHealthAnimations[_curPlayerHealthAnimIndex]))
                    | (_playerExactHealthMonitorTexts.Count > 0 && UpdateGenericTextList(_playerExactHealthMonitorTexts, _curPlayerExactHealthAnimations[_curPlayerHealthAnimIndex])))
                {
                    Plugin.MLS.LogInfo("Updated player health display.");
                }
            }
        }

        private static bool UpdateGenericTextList(List<TextMeshProUGUI> textList, string text)
        {
            bool allSuccess = _newMonitors == null && textList.Count > 0; // Default to true with old style if there are any text objects to update

            // If the text matches what the first entry currently has, do nothing
            if (textList.Count > 0 && textList[0].text == text)
            {
                return false;
            }

            foreach (var t in textList)
            {
                t.text = text;
                if (_newMonitors != null && MonitorsAPI.AllMonitors.FirstOrDefault(m => m.Value.TextCanvas == t).Value is MonitorsAPI.MonitorInfo monitor)
                {
                    // If we are orbiting, in the ship (or spectating someone in it), or set to always render, immediately update. Otherwise, add it to the refresh queue (most recent will always override any old data)
                    bool targetPlayerInShip = (StartOfRound.Instance.localPlayerController?.spectatedPlayerScript ?? StartOfRound.Instance.localPlayerController)?.isInElevator ?? false;
                    if (StartOfRound.Instance.inShipPhase || Plugin.AlwaysRenderMonitors.Value || targetPlayerInShip)
                    {
                        if (_newMonitors.RefreshMonitorAfterTextChange(monitor))
                        {
                            allSuccess = true;
                        }
                    }
                    else
                    {
                        _queuedMonitorRefreshes[t] = () => _newMonitors.RefreshMonitorAfterTextChange(monitor);
                    }
                }
            }

            return allSuccess;
        }

        public static void RefreshQueuedMonitorChanges()
        {
            // Call all of the queued monitor refreshes, if any, then clear it
            foreach (var text in _queuedMonitorRefreshes.Keys)
            {
                _queuedMonitorRefreshes[text]();
            }

            if (_queuedMonitorRefreshes.Count > 0)
            {
                Plugin.MLS.LogInfo($"Applied {_queuedMonitorRefreshes.Count} queued monitor changes.");
                _queuedMonitorRefreshes.Clear();
            }
        }

        public static void ToggleExtraMonitorsPower(bool on)
        {
            if (Plugin.SyncExtraMonitorsPower.Value)
            {
                if (Plugin.UseBetterMonitors.Value)
                {
                    if (_newMonitors != null)
                    {
                        _newMonitors.TogglePower(on);
                    }
                }
                else if (!_usingAnyMonitorTweaks)
                {
                    // Handle vanilla settings if needed
                    if (_originalProfitQuotaBG != null) _originalProfitQuotaBG.enabled = on;
                    if (_originalProfitQuotaText != null) _originalProfitQuotaText.enabled = on;
                    if (_originalDeadlineBG != null) _originalDeadlineBG.enabled = on;
                    if (_originalDeadlineText != null) _originalDeadlineText.enabled = on;
                }
                else if (_UIContainer != null)
                {
                    if (Plugin.ShowBlueMonitorBackground.Value)
                    {
                        foreach (var background in _UIContainer.GetComponentsInChildren<Image>(on))
                        {
                            background.gameObject.SetActive(on);
                        }
                    }

                    foreach (var text in _UIContainer.GetComponentsInChildren<TextMeshProUGUI>(on))
                    {
                        text.gameObject.SetActive(on);
                    }
                }
            }
        }

        public static void UpdateMapMaterial(Material newMaterial)
        {
            if (_newMonitors != null)
            {
                _newMonitors.UpdateMapMaterial(newMaterial);
            }
        }
    }
}