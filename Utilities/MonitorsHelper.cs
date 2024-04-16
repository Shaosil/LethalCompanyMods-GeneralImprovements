using GeneralImprovements.Assets;
using GeneralImprovements.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace GeneralImprovements.Utilities
{
    internal static class MonitorsHelper
    {
        public const int MonitorCount = 14;

        public static class MonitorNames
        {
            public const string ProfitQuota = "ProfitQuota";
            public const string Deadline = "Deadline";
            public const string ShipScrap = "ShipScrap";
            public const string ScrapLeft = "ScrapLeft";
            public const string Time = "Time";
            public const string Weather = "Weather";
            public const string FancyWeather = "FancyWeather";
            public const string Sales = "Sales";
            public const string Credits = "Credits";
            public const string DoorPower = "DoorPower";
            public const string TotalDays = "TotalDays";
            public const string TotalQuotas = "TotalQuotas";
            public const string TotalDeaths = "TotalDeaths";
            public const string DaysSinceDeath = "DaysSinceDeath";
            public const string InternalCam = "InternalCam";
            public const string ExternalCam = "ExternalCam";

            public static bool MonitorExists(string monitorName)
            {
                return GetMonitorIndex(monitorName) >= 0;
            }

            public static int GetMonitorIndex(string monitorName)
            {
                for (int i = 0; i < MonitorCount; i++)
                {
                    if (Plugin.ShipMonitorAssignments[i].Value == monitorName)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        private static Vector3 _originalProfitQuotaLocation = Vector3.zero;
        private static Vector3 _originalProfitQuotaRotation = Vector3.zero;
        private static Image _originalProfitQuotaBG;
        private static TextMeshProUGUI _originalProfitQuotaText;
        private static Image _originalDeadlineBG;
        private static TextMeshProUGUI _originalDeadlineText;

        private static List<Image> _profitQuotaBGs = new List<Image>();
        private static List<TextMeshProUGUI> _profitQuotaTexts = new List<TextMeshProUGUI>();
        private static List<Image> _deadlineBGs = new List<Image>();
        private static List<TextMeshProUGUI> _deadlineTexts = new List<TextMeshProUGUI>();
        private static List<Image> _shipScrapMonitorBGs = new List<Image>();
        private static List<TextMeshProUGUI> _shipScrapMonitorTexts = new List<TextMeshProUGUI>();
        private static List<Image> _scrapLeftMonitorBGs = new List<Image>();
        private static List<TextMeshProUGUI> _scrapLeftMonitorTexts = new List<TextMeshProUGUI>();
        private static List<Image> _timeMonitorBGs = new List<Image>();
        private static List<TextMeshProUGUI> _timeMonitorTexts = new List<TextMeshProUGUI>();
        private static List<Image> _weatherMonitorBGs = new List<Image>();
        private static List<TextMeshProUGUI> _weatherMonitorTexts = new List<TextMeshProUGUI>();
        private static List<Image> _fancyWeatherMonitorBGs = new List<Image>();
        private static List<TextMeshProUGUI> _fancyWeatherMonitorTexts = new List<TextMeshProUGUI>();
        private static List<Image> _salesMonitorBGs = new List<Image>();
        private static List<TextMeshProUGUI> _salesMonitorTexts = new List<TextMeshProUGUI>();
        private static List<Image> _creditsMonitorBGs = new List<Image>();
        private static List<TextMeshProUGUI> _creditsMonitorTexts = new List<TextMeshProUGUI>();
        private static List<Image> _doorPowerMonitorBGs = new List<Image>();
        private static List<TextMeshProUGUI> _doorPowerMonitorTexts = new List<TextMeshProUGUI>();
        private static List<Image> _totalDaysMonitorBGs = new List<Image>();
        private static List<TextMeshProUGUI> _totalDaysMonitorTexts = new List<TextMeshProUGUI>();
        private static List<Image> _totalQuotasMonitorBGs = new List<Image>();
        private static List<TextMeshProUGUI> _totalQuotasMonitorTexts = new List<TextMeshProUGUI>();
        private static List<Image> _totalDeathsMonitorBGs = new List<Image>();
        private static List<TextMeshProUGUI> _totalDeathsMonitorTexts = new List<TextMeshProUGUI>();
        private static List<Image> _daysSinceDeathMonitorBGs = new List<Image>();
        private static List<TextMeshProUGUI> _daysSinceDeathMonitorTexts = new List<TextMeshProUGUI>();
        private static List<Image> _extraBackgrounds = new List<Image>();

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

        private static float _curCreditsUpdateCounter = 0;

        private static Transform _oldMonitorsObject;
        private static Transform _oldBigMonitors;
        private static Transform _UIContainer;
        private static ScanNodeProperties _profitQuotaScanNode;

        public static void InitializeMonitors()
        {
            // Initialize a few necessary variables
            _originalProfitQuotaBG = StartOfRound.Instance.profitQuotaMonitorBGImage;
            _originalProfitQuotaText = StartOfRound.Instance.profitQuotaMonitorText;
            _originalDeadlineBG = StartOfRound.Instance.deadlineMonitorBGImage;
            _originalDeadlineText = StartOfRound.Instance.deadlineMonitorText;
            _UIContainer = _originalProfitQuotaBG?.transform.parent;

            if (Plugin.CenterAlignMonitorText.Value)
            {
                _originalProfitQuotaText.alignment = TextAlignmentOptions.Center;
                _originalDeadlineText.alignment = TextAlignmentOptions.Center;
            }

            var internalShipCamObj = StartOfRound.Instance.elevatorTransform.Find("Cameras/ShipCamera");
            var externalShipCamObj = StartOfRound.Instance.elevatorTransform.Find("Cameras/FrontDoorSecurityCam/SecurityCamera");
            if (Plugin.DisableShipCamPostProcessing.Value)
            {
                internalShipCamObj.GetComponent<HDAdditionalCameraData>().volumeLayerMask = 0;
                externalShipCamObj.GetComponent<HDAdditionalCameraData>().volumeLayerMask = 0;
            }

            // Do nothing else if none of these are true - that means the user basically hasn't changed any of the default monitor config options
            if (!(Plugin.UseBetterMonitors.Value || !Plugin.ShowBlueMonitorBackground.Value || Plugin.ShowBackgroundOnAllScreens.Value || Plugin.SyncMonitorsFromOtherHost.Value
                || Plugin.MonitorBackgroundColor.Value != Plugin.MonitorBackgroundColor.DefaultValue.ToString() || Plugin.MonitorTextColor.Value != Plugin.MonitorTextColor.DefaultValue.ToString()
                || Plugin.ShipInternalCamFPS.Value != (int)Plugin.ShipInternalCamFPS.DefaultValue || Plugin.ShipInternalCamSizeMultiplier.Value != (int)Plugin.ShipInternalCamSizeMultiplier.DefaultValue
                || Plugin.ShipExternalCamFPS.Value != (int)Plugin.ShipExternalCamFPS.DefaultValue || Plugin.ShipExternalCamSizeMultiplier.Value != (int)Plugin.ShipExternalCamSizeMultiplier.DefaultValue
                || Plugin.ShipMonitorAssignments.Any(m => m.Value != m.DefaultValue.ToString())))
            {
                return;
            }
            _usingAnyMonitorTweaks = true;

            // Load days since last death from the current save file
            if (StartOfRound.Instance.IsHost)
            {
                var anyDeaths = StartOfRound.Instance.gameStats.deaths > 0;
                StartOfRoundPatch.DaysSinceLastDeath = ES3.Load("Stats_DaysSinceLastDeath", GameNetworkManager.Instance.currentSaveFileName, anyDeaths ? 0 : -1);
            }

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
            _originalProfitQuotaText.fontSize = _originalProfitQuotaText.fontSize * 0.9f;
            _originalProfitQuotaText.margin = Vector4.one * 5;
            _originalDeadlineText.rectTransform.sizeDelta = _originalDeadlineBG.rectTransform.sizeDelta;
            _originalDeadlineText.transform.position = _originalDeadlineBG.transform.TransformPoint(Vector3.back);
            _originalDeadlineText.fontSize = _originalDeadlineText.fontSize * 0.9f;
            _originalDeadlineText.margin = Vector4.one * 5;

            // Find our monitor objects
            _oldMonitorsObject = _UIContainer.parent.parent;
            _oldBigMonitors = _oldMonitorsObject.parent.GetComponentInChildren<ManualCameraRenderer>().transform.parent;

            // Increase internal ship cam resolution and FPS if specified
            var newRT = UpdateSecurityCamFPSAndResolution(internalShipCamObj, Plugin.ShipInternalCamFPS.Value, Plugin.ShipInternalCamSizeMultiplier.Value);
            _oldMonitorsObject.GetComponent<MeshRenderer>().sharedMaterials[2].mainTexture = newRT;

            // Increase external ship cam resolution and FPS if specified
            newRT = UpdateSecurityCamFPSAndResolution(externalShipCamObj, Plugin.ShipExternalCamFPS.Value, Plugin.ShipExternalCamSizeMultiplier.Value);
            _oldBigMonitors.GetComponent<MeshRenderer>().sharedMaterials[2].mainTexture = newRT;

            if (Plugin.UseBetterMonitors.Value)
            {
                CreateNewStyleMonitors();
            }
            else
            {
                CreateOldStyleMonitors();
            }

            // Remove or update profit quota scan node
            _profitQuotaScanNode = StartOfRound.Instance.elevatorTransform.GetComponentsInChildren<ScanNodeProperties>().FirstOrDefault(s => s.headerText == "Quota");
            if (_profitQuotaScanNode != null)
            {
                // Remove scan node
                if (!MonitorNames.MonitorExists(MonitorNames.ProfitQuota))
                {
                    Object.Destroy(_profitQuotaScanNode.gameObject);
                }
                else if (!Plugin.UseBetterMonitors.Value && _profitQuotaTexts.Any())
                {
                    // Update scan node position immediately (better monitors do it delayed)
                    _profitQuotaScanNode.transform.parent = _profitQuotaTexts[0].transform;
                    _profitQuotaScanNode.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                }
            }
        }

        private static RenderTexture UpdateSecurityCamFPSAndResolution(Transform cam, int fps, int resMultiplier)
        {
            var shipCam = cam.GetComponent<Camera>();
            var camRenderer = cam.GetComponent<ManualCameraRenderer>();

            camRenderer.renderAtLowerFramerate = fps > 0;
            camRenderer.fps = fps;
            if (resMultiplier > 1)
            {
                int targetWidth = shipCam.targetTexture.width * (2 * resMultiplier);
                int targetHeight = shipCam.targetTexture.height * (2 * resMultiplier);
                var newCamRT = new RenderTexture(targetWidth, targetHeight, shipCam.targetTexture.depth, shipCam.targetTexture.format);
                shipCam.targetTexture = newCamRT;
            }

            return shipCam.targetTexture;
        }

        private static void RemoveExistingMonitors()
        {
            // Clears lists and removes game objects (for old style monitors). May be used if syncing from hosts, since we will need to delete and recreate monitors
            if (!Plugin.UseBetterMonitors.Value)
            {
                _profitQuotaBGs.ForEach(g => Object.Destroy(g));
                _profitQuotaTexts.ForEach(g => Object.Destroy(g));

                _deadlineBGs.ForEach(g => Object.Destroy(g));
                _deadlineTexts.ForEach(g => Object.Destroy(g));

                _shipScrapMonitorBGs.ForEach(g => Object.Destroy(g));
                _shipScrapMonitorTexts.ForEach(g => Object.Destroy(g));

                _scrapLeftMonitorBGs.ForEach(g => Object.Destroy(g));
                _scrapLeftMonitorTexts.ForEach(g => Object.Destroy(g));

                _timeMonitorBGs.ForEach(g => Object.Destroy(g));
                _timeMonitorTexts.ForEach(g => Object.Destroy(g));

                _weatherMonitorBGs.ForEach(g => Object.Destroy(g));
                _weatherMonitorTexts.ForEach(g => Object.Destroy(g));

                _fancyWeatherMonitorBGs.ForEach(g => Object.Destroy(g));
                _fancyWeatherMonitorTexts.ForEach(g => Object.Destroy(g));

                _salesMonitorBGs.ForEach(g => Object.Destroy(g));
                _salesMonitorTexts.ForEach(g => Object.Destroy(g));

                _creditsMonitorBGs.ForEach(g => Object.Destroy(g));
                _creditsMonitorTexts.ForEach(g => Object.Destroy(g));

                _doorPowerMonitorBGs.ForEach(g => Object.Destroy(g));
                _doorPowerMonitorTexts.ForEach(g => Object.Destroy(g));

                _totalDaysMonitorBGs.ForEach(g => Object.Destroy(g));
                _totalDaysMonitorTexts.ForEach(g => Object.Destroy(g));

                _totalQuotasMonitorBGs.ForEach(g => Object.Destroy(g));
                _totalQuotasMonitorTexts.ForEach(g => Object.Destroy(g));

                _totalDeathsMonitorBGs.ForEach(g => Object.Destroy(g));
                _totalDeathsMonitorTexts.ForEach(g => Object.Destroy(g));

                _daysSinceDeathMonitorBGs.ForEach(g => Object.Destroy(g));
                _daysSinceDeathMonitorTexts.ForEach(g => Object.Destroy(g));

                _extraBackgrounds.ForEach(g => Object.Destroy(g));
            }
            else if (_newMonitors != null)
            {
                Object.Destroy(_newMonitors.gameObject);
            }

            _profitQuotaBGs = new List<Image>();
            _profitQuotaTexts = new List<TextMeshProUGUI>();

            _deadlineBGs = new List<Image>();
            _deadlineTexts = new List<TextMeshProUGUI>();

            _shipScrapMonitorBGs = new List<Image>();
            _shipScrapMonitorTexts = new List<TextMeshProUGUI>();

            _scrapLeftMonitorBGs = new List<Image>();
            _scrapLeftMonitorTexts = new List<TextMeshProUGUI>();

            _timeMonitorBGs = new List<Image>();
            _timeMonitorTexts = new List<TextMeshProUGUI>();

            _weatherMonitorBGs = new List<Image>();
            _weatherMonitorTexts = new List<TextMeshProUGUI>();

            _fancyWeatherMonitorBGs = new List<Image>();
            _fancyWeatherMonitorTexts = new List<TextMeshProUGUI>();

            _salesMonitorBGs = new List<Image>();
            _salesMonitorTexts = new List<TextMeshProUGUI>();

            _creditsMonitorBGs = new List<Image>();
            _creditsMonitorTexts = new List<TextMeshProUGUI>();

            _doorPowerMonitorBGs = new List<Image>();
            _doorPowerMonitorTexts = new List<TextMeshProUGUI>();

            _totalDaysMonitorBGs = new List<Image>();
            _totalDaysMonitorTexts = new List<TextMeshProUGUI>();

            _totalQuotasMonitorBGs = new List<Image>();
            _totalQuotasMonitorTexts = new List<TextMeshProUGUI>();

            _totalDeathsMonitorBGs = new List<Image>();
            _totalDeathsMonitorTexts = new List<TextMeshProUGUI>();

            _daysSinceDeathMonitorBGs = new List<Image>();
            _daysSinceDeathMonitorTexts = new List<TextMeshProUGUI>();

            _extraBackgrounds = new List<Image>();
        }

        private static void CreateOldStyleMonitors()
        {
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
                string curAssignment = Plugin.ShipMonitorAssignments[i].Value;
                List<Image> curBGs = null;
                List<TextMeshProUGUI> curTexts = null;

                switch (curAssignment)
                {
                    case MonitorNames.ProfitQuota: curBGs = _profitQuotaBGs; curTexts = _profitQuotaTexts; break;
                    case MonitorNames.Deadline: curBGs = _deadlineBGs; curTexts = _deadlineTexts; break;
                    case MonitorNames.ShipScrap: curBGs = _shipScrapMonitorBGs; curTexts = _shipScrapMonitorTexts; break;
                    case MonitorNames.ScrapLeft: curBGs = _scrapLeftMonitorBGs; curTexts = _scrapLeftMonitorTexts; break;
                    case MonitorNames.Time: curBGs = _timeMonitorBGs; curTexts = _timeMonitorTexts; break;
                    case MonitorNames.Weather: curBGs = _weatherMonitorBGs; curTexts = _weatherMonitorTexts; break;
                    case MonitorNames.FancyWeather: curBGs = _fancyWeatherMonitorBGs; curTexts = _fancyWeatherMonitorTexts; break;
                    case MonitorNames.Sales: curBGs = _salesMonitorBGs; curTexts = _salesMonitorTexts; break;
                    case MonitorNames.Credits: curBGs = _creditsMonitorBGs; curTexts = _creditsMonitorTexts; break;
                    case MonitorNames.DoorPower: curBGs = _doorPowerMonitorBGs; curTexts = _doorPowerMonitorTexts; break;
                    case MonitorNames.TotalDays: curBGs = _totalDaysMonitorBGs; curTexts = _totalDaysMonitorTexts; break;
                    case MonitorNames.TotalQuotas: curBGs = _totalQuotasMonitorBGs; curTexts = _totalQuotasMonitorTexts; break;
                    case MonitorNames.TotalDeaths: curBGs = _totalDeathsMonitorBGs; curTexts = _totalDeathsMonitorTexts; break;
                    case MonitorNames.DaysSinceDeath: curBGs = _daysSinceDeathMonitorBGs; curTexts = _daysSinceDeathMonitorTexts; break;
                }

                if ((curBGs == null || curTexts == null) && Plugin.ShowBlueMonitorBackground.Value && Plugin.ShowBackgroundOnAllScreens.Value)
                {
                    // Prepare to create a blank background
                    curBGs = _extraBackgrounds;
                }
                else if (curTexts == null && !string.IsNullOrWhiteSpace(curAssignment))
                {
                    Plugin.MLS.LogError($"Could not find '{curAssignment}' for monitor assignment! Please check your config is using acceptable values.");
                }

                var positionOffset = offsets[i].Key;
                var rotationOffset = offsets[i].Value;

                // Only create a background if we have one assigned and we want to show the blue backgrounds
                if (curBGs != null && Plugin.ShowBlueMonitorBackground.Value)
                {
                    var newBG = Object.Instantiate(_originalProfitQuotaBG, _originalProfitQuotaBG.transform.parent);
                    newBG.enabled = true;
                    newBG.name = $"{(string.IsNullOrWhiteSpace(curAssignment) ? "ExtraBackground" : curAssignment)}BG{i + 1}";

                    newBG.transform.localPosition = _originalProfitQuotaLocation + positionOffset;
                    newBG.transform.localEulerAngles = _originalProfitQuotaRotation + rotationOffset;
                    curBGs.Add(newBG);
                }

                // Text will be null if this is a blank background
                if (curTexts != null)
                {
                    Plugin.MLS.LogInfo($"Creating {curAssignment} monitor at position {i + 1}");
                    var newText = Object.Instantiate(_originalProfitQuotaText, _originalProfitQuotaText.transform.parent);
                    newText.enabled = true;
                    newText.name = $"{curAssignment}Text{i + 1}";

                    newText.transform.localPosition = _originalProfitQuotaLocation + positionOffset + new Vector3(0, 0, -1);
                    newText.transform.localEulerAngles = _originalProfitQuotaRotation + rotationOffset;
                    curTexts.Add(newText);
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

            // Initialize everything's text (some are initialized elsewhere)
            CopyProfitQuotaAndDeadlineTexts();
            UpdateShipScrapMonitors();
            UpdateScrapLeftMonitors();
            UpdateTimeMonitors();
            UpdateWeatherMonitors();
            UpdateDoorPowerMonitors();
        }

        private static void CreateNewStyleMonitors()
        {
            Plugin.MLS.LogInfo("Overwriting monitors with new model");

            var newMonitorsObj = Object.Instantiate(AssetBundleHelper.MonitorsPrefab, _oldMonitorsObject.transform.parent);
            newMonitorsObj.transform.SetLocalPositionAndRotation(_oldMonitorsObject.localPosition, Quaternion.identity);

            _newMonitors = newMonitorsObj.AddComponent<Monitors>();
            _newMonitors.StartingMapMaterial = _oldBigMonitors.GetComponent<MeshRenderer>().sharedMaterials[1];
            _newMonitors.HullMaterial = _oldMonitorsObject.GetComponent<MeshRenderer>().materials[0];
            _newMonitors.BlankScreenMat = _oldMonitorsObject.GetComponent<MeshRenderer>().materials[1];

            // Assign specified TMP objects to the monitor indexes specified
            for (int i = 0; i < Plugin.ShipMonitorAssignments.Length; i++)
            {
                string curAssignment = Plugin.ShipMonitorAssignments[i].Value;
                Action<TextMeshProUGUI> curAction = null;
                Material targetMat = null;

                switch (curAssignment)
                {
                    case MonitorNames.ProfitQuota:
                        curAction = t =>
                    {
                        _profitQuotaTexts.Add(t);
                        _profitQuotaScanNode.transform.parent = _newMonitors.GetMonitorTransform(t);
                        _profitQuotaScanNode.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                        CopyProfitQuotaAndDeadlineTexts();
                    };
                        break;
                    case MonitorNames.Deadline: curAction = t => { _deadlineTexts.Add(t); CopyProfitQuotaAndDeadlineTexts(); }; break;
                    case MonitorNames.ShipScrap: curAction = t => { _shipScrapMonitorTexts.Add(t); UpdateShipScrapMonitors(); }; break;
                    case MonitorNames.ScrapLeft: curAction = t => { _scrapLeftMonitorTexts.Add(t); UpdateScrapLeftMonitors(); }; break;
                    case MonitorNames.Time: curAction = t => { _timeMonitorTexts.Add(t); UpdateTimeMonitors(); }; break;
                    case MonitorNames.Weather: curAction = t => { _weatherMonitorTexts.Add(t); UpdateWeatherMonitors(); }; break;
                    case MonitorNames.FancyWeather:
                        curAction = t =>
                    {
                        if (Plugin.CenterAlignMonitorText.Value)
                        {
                            t.alignment = TextAlignmentOptions.MidlineLeft;
                            t.margin += new Vector4(20, 10, 0, 0);
                        }
                        else
                        {
                            t.alignment = TextAlignmentOptions.TopLeft;
                            t.margin += new Vector4(10, 10, 0, 0);
                        }
                        _fancyWeatherMonitorTexts.Add(t);
                        UpdateWeatherMonitors();
                    };
                        break;
                    case MonitorNames.Sales: curAction = t => { t.overflowMode = TextOverflowModes.Ellipsis; _salesMonitorTexts.Add(t); UpdateSalesMonitors(); }; break;
                    case MonitorNames.Credits: curAction = t => { _creditsMonitorTexts.Add(t); UpdateCreditsMonitors(true); }; break;
                    case MonitorNames.DoorPower: curAction = t => { _doorPowerMonitorTexts.Add(t); UpdateDoorPowerMonitors(true); }; break;
                    case MonitorNames.TotalDays: curAction = t => { _totalDaysMonitorTexts.Add(t); UpdateTotalDaysMonitors(); }; break;
                    case MonitorNames.TotalQuotas: curAction = t => { _totalQuotasMonitorTexts.Add(t); UpdateTotalQuotasMonitors(); }; break;
                    case MonitorNames.TotalDeaths: curAction = t => { _totalDeathsMonitorTexts.Add(t); UpdateDeathMonitors(); }; break;
                    case MonitorNames.DaysSinceDeath: curAction = t => { _daysSinceDeathMonitorTexts.Add(t); UpdateDeathMonitors(); }; break;

                    case MonitorNames.InternalCam: targetMat = _oldMonitorsObject.GetComponent<MeshRenderer>().materials.FirstOrDefault(m => m.name.StartsWith("ShipScreen")); break;
                    case MonitorNames.ExternalCam: targetMat = _oldBigMonitors.GetComponent<MeshRenderer>().materials.FirstOrDefault(m => m.name.StartsWith("ShipScreen")); break;
                }

                if (curAction != null || targetMat != null)
                {
                    Plugin.MLS.LogInfo($"Creating {curAssignment} monitor at position {i + 1}");
                    if (curAction != null)
                    {
                        _newMonitors.AssignTextMonitor(i, curAction);
                    }
                    else
                    {
                        _newMonitors.AssignMaterialMonitor(i, targetMat);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(curAssignment))
                {
                    Plugin.MLS.LogError($"Could not find '{curAssignment}' for monitor assignment! Please check your config is using acceptable values.");
                }
            }

            var newMesh = newMonitorsObj.transform.Find("Monitors/BigMiddle").GetComponent<MeshRenderer>();
            StartOfRound.Instance.mapScreen.mesh = newMesh;
            StartOfRound.Instance.elevatorTransform.Find("Cameras/ShipCamera").GetComponent<ManualCameraRenderer>().mesh = newMesh;
            StartOfRound.Instance.elevatorTransform.Find("Cameras/FrontDoorSecurityCam/SecurityCamera").GetComponent<ManualCameraRenderer>().mesh = newMesh;
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
            if (_profitQuotaTexts.Any() || _deadlineTexts.Any())
            {
                if (UpdateGenericTextList(_profitQuotaTexts, StartOfRound.Instance.profitQuotaMonitorText?.text)
                    && UpdateGenericTextList(_deadlineTexts, StartOfRound.Instance.deadlineMonitorText?.text))
                {
                    Plugin.MLS.LogInfo("Updated profit quota and deadline monitors");
                }
            }
        }

        public static void UpdateShipScrapMonitors()
        {
            if (!_shipScrapMonitorTexts.Any())
            {
                return;
            }

            var allScrap = Object.FindObjectsOfType<GrabbableObject>().Where(o => o.itemProperties.isScrap && o.isInShipRoom && o.isInElevator && !o.isHeld).ToList();
            int shipLoot = allScrap.Sum(o => o.scrapValue);

            if (UpdateGenericTextList(_shipScrapMonitorTexts, $"SCRAP IN SHIP:\n${shipLoot}"))
            {
                Plugin.MLS.LogInfo($"Set ship scrap total to ${shipLoot} ({allScrap.Count} items).");
            }
        }

        public static void UpdateScrapLeftMonitors()
        {
            if (_scrapLeftMonitorTexts.Any())
            {
                var outsideScrap = GrabbableObjectsPatch.GetOutsideScrap(!Plugin.ScanCommandUsesExactAmount.Value);
                bool updatedText = false;
                if (outsideScrap.Key > 0)
                {
                    updatedText = UpdateGenericTextList(_scrapLeftMonitorTexts, $"SCRAP LEFT:\n{outsideScrap.Key} ITEMS\n${outsideScrap.Value}");
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
        }

        public static void UpdateTimeMonitors()
        {
            if (HUDManager.Instance?.clockNumber != null && _timeMonitorTexts.Any())
            {
                string time;
                if (TimeOfDay.Instance.movingGlobalTimeForward)
                {
                    time = $"TIME:\n{HUDManager.Instance.clockNumber.text.Replace('\n', ' ')}";
                }
                else
                {
                    time = "TIME:\nPENDING";
                }

                if (UpdateGenericTextList(_timeMonitorTexts, time))
                {
                    Plugin.MLS.LogDebug("Updated time display.");
                }
            }
        }

        public static void UpdateWeatherMonitors()
        {
            if (_weatherMonitorTexts.Any() || _fancyWeatherMonitorTexts.Any())
            {
                if (_weatherMonitorTexts.Any())
                {
                    if (UpdateGenericTextList(_weatherMonitorTexts, $"WEATHER:\n{(StartOfRound.Instance.currentLevel?.currentWeather.ToString() ?? string.Empty)}"))
                    {
                        Plugin.MLS.LogInfo("Updated basic weather monitors");
                    }
                }

                if (_fancyWeatherMonitorTexts.Any())
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
            if (_fancyWeatherMonitorTexts.Any() && _curWeatherAnimations.Length >= 2)
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

            if (_salesMonitorTexts.Any() && _curSalesAnimations.Count >= 2)
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
        }

        public static void UpdateSalesMonitors()
        {
            if (_salesMonitorTexts.Any() && TerminalPatch.Instance != null)
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
                            _curSalesAnimations.Add($"{100 - instance.itemSalesPercentages[i]}% OFF {item}");
                        }
                    }
                }

                bool updatedText = false;
                if (numSales <= 0)
                {
                    updatedText = UpdateGenericTextList(_salesMonitorTexts, "NO SALES TODAY");
                }
                else
                {
                    updatedText = UpdateGenericTextList(_salesMonitorTexts, $"{numSales} SALE{(numSales == 1 ? string.Empty : "S")}:\n{_curSalesAnimations[0]}");
                }

                if (updatedText)
                {
                    Plugin.MLS.LogInfo("Updated sales display.");
                }
            }
        }

        public static void UpdateCreditsMonitors(bool force = false)
        {
            // This is getting called every frame, so limit the check to a few times per second
            _curCreditsUpdateCounter += Time.deltaTime;
            if (_curCreditsUpdateCounter >= 0.25f)
            {
                _curCreditsUpdateCounter = 0;

                // Only update if there is a change
                var groupCredits = TerminalPatch.Instance?.groupCredits ?? -1;
                if (_creditsMonitorTexts.Any() && (force || groupCredits != _lastUpdatedCredits))
                {
                    _lastUpdatedCredits = groupCredits;

                    if (UpdateGenericTextList(_creditsMonitorTexts, $"CREDITS:\n${_lastUpdatedCredits}"))
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
            if (_doorPowerMonitorTexts.Any() && (force || (_lastUpdatedDoorPower != doorPower && _updateDoorPowerTimer <= 0)))
            {
                _lastUpdatedDoorPower = doorPower;
                UpdateGenericTextList(_doorPowerMonitorTexts, $"DOOR POWER:\n{Mathf.RoundToInt(_lastUpdatedDoorPower * 100)}%");

                // Limit FPS to 10
                _updateDoorPowerTimer = 0.1f;
            }
        }

        public static void UpdateTotalDaysMonitors()
        {
            if (_totalDaysMonitorTexts.Any() && StartOfRound.Instance.gameStats != null)
            {
                if (UpdateGenericTextList(_totalDaysMonitorTexts, $"DAY {StartOfRound.Instance.gameStats.daysSpent + 1}"))
                {
                    Plugin.MLS.LogInfo("Updated total days display.");
                }
            }
        }

        public static void UpdateTotalQuotasMonitors()
        {
            if (_totalQuotasMonitorTexts.Any() && TimeOfDay.Instance != null)
            {
                if (UpdateGenericTextList(_totalQuotasMonitorTexts, $"QUOTA {TimeOfDay.Instance.timesFulfilledQuota + 1}"))
                {
                    Plugin.MLS.LogInfo("Updated total quotas display.");
                }
            }
        }

        public static void UpdateDeathMonitors(bool? playersDied = null)
        {
            if (_totalDeathsMonitorTexts.Any() && StartOfRound.Instance?.gameStats != null)
            {
                if (UpdateGenericTextList(_totalDeathsMonitorTexts, $"DEATHS:\n{StartOfRound.Instance.gameStats.deaths}"))
                {
                    Plugin.MLS.LogInfo("Updated total deaths display.");
                }
            }

            if (_daysSinceDeathMonitorTexts.Any() && StartOfRound.Instance != null)
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

                    if (StartOfRound.Instance.IsHost)
                    {
                        ES3.Save("Stats_DaysSinceLastDeath", StartOfRoundPatch.DaysSinceLastDeath, GameNetworkManager.Instance.currentSaveFileName);
                    }
                }

                bool updatedText = false;
                if (StartOfRoundPatch.DaysSinceLastDeath >= 0)
                {
                    updatedText = UpdateGenericTextList(_daysSinceDeathMonitorTexts, $"{StartOfRoundPatch.DaysSinceLastDeath} DAY{(StartOfRoundPatch.DaysSinceLastDeath == 1 ? string.Empty : "S")} WITHOUT DEATHS");
                }
                else
                {
                    updatedText = UpdateGenericTextList(_daysSinceDeathMonitorTexts, "ZERO DEATHS (YET)");
                }

                if (updatedText)
                {
                    Plugin.MLS.LogInfo("Updated days since death display.");
                }
            }
        }

        private static bool UpdateGenericTextList(List<TextMeshProUGUI> textList, string text)
        {
            bool allSuccess = false;

            foreach (var t in textList)
            {
                t.text = text;
                if (_newMonitors != null)
                {
                    // If we are in orbit or in the ship (or set to always render), immediately draw the change. Otherwise, add it to the refresh queue (most recent will always override any old data)
                    if (StartOfRound.Instance.inShipPhase || Plugin.AlwaysRenderMonitors.Value || (StartOfRound.Instance.localPlayerController?.isInElevator ?? false))
                    {
                        if (_newMonitors.RenderCameraAfterTextChange(t))
                        {
                            allSuccess = true;
                        }
                    }
                    else
                    {
                        _queuedMonitorRefreshes[t] = () => _newMonitors.RenderCameraAfterTextChange(t);
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

            if (_queuedMonitorRefreshes.Any())
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