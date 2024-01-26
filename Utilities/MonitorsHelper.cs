using GeneralImprovements.Assets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace GeneralImprovements.Utilities
{
    internal static class MonitorsHelper
    {
        private static Image _profitQuotaBG;
        private static TextMeshProUGUI _profitQuotaText;
        private static Image _deadlineBG;
        private static TextMeshProUGUI _deadlineText;
        private static Image _totalMonitorBg;
        private static TextMeshProUGUI _totalMonitorText;
        private static Image _timeMonitorBG;
        private static TextMeshProUGUI _timeMonitorText;
        private static Image _weatherMonitorBG;
        private static TextMeshProUGUI _weatherMonitorText;
        private static Image _salesMonitorBG;
        private static TextMeshProUGUI _salesMonitorText;
        private static List<Image> _extraBackgrounds = new List<Image>();

        private static Monitors _newMonitors;

        private static int _curWeatherAnimIndex = 0;
        private static int _curWeatherOverlayIndex = 0;
        private static string[] _curWeatherAnimations = new string[0];
        private static string[] _curWeatherOverlays = new string[0];

        private static float _animTimer = 0;
        private static float _animCycle = 0.2f; // In seconds
        private static bool _hasOverlays = false;
        private static float _overlayTimer = 0;
        private static bool _showingOverlay = false;
        private static float _overlayCycle; // In seconds, randomly assigned each time
        private static Transform _oldMonitorsObject;
        private static Transform _oldBigMonitors;
        private static Transform _UIContainer;
        private static ScanNodeProperties _profitQuotaScanNode;

        public static void CreateExtraMonitors()
        {
            // Resize the two extra monitor texts to be the same as their respective backgrounds, and give them padding
            _profitQuotaBG = StartOfRound.Instance.profitQuotaMonitorBGImage;
            _profitQuotaBG.color = Plugin.MonitorBackgroundColorVal;
            _profitQuotaText = StartOfRound.Instance.profitQuotaMonitorText;
            _profitQuotaText.color = Plugin.MonitorTextColorVal;
            _deadlineBG = StartOfRound.Instance.deadlineMonitorBGImage;
            _deadlineBG.color = Plugin.MonitorBackgroundColorVal;
            _deadlineText = StartOfRound.Instance.deadlineMonitorText;
            _deadlineText.color = Plugin.MonitorTextColorVal;
            _profitQuotaText.rectTransform.sizeDelta = _profitQuotaBG.rectTransform.sizeDelta;
            _profitQuotaText.transform.position = _profitQuotaBG.transform.TransformPoint(Vector3.back);
            _profitQuotaText.fontSize = _profitQuotaText.fontSize * 0.9f;
            _profitQuotaText.margin = Vector4.one * 5;
            _deadlineText.rectTransform.sizeDelta = _deadlineBG.rectTransform.sizeDelta;
            _deadlineText.transform.position = _deadlineBG.transform.TransformPoint(Vector3.back);
            _deadlineText.fontSize = _deadlineText.fontSize * 0.9f;
            _deadlineText.margin = Vector4.one * 5;

            if (Plugin.CenterAlignMonitorText.Value)
            {
                _profitQuotaText.alignment = TextAlignmentOptions.Center;
                _deadlineText.alignment = TextAlignmentOptions.Center;
            }

            // Find our monitor objects
            _UIContainer = StartOfRound.Instance.profitQuotaMonitorBGImage.transform.parent;
            _oldMonitorsObject = _UIContainer.parent.parent;
            _oldBigMonitors = _oldMonitorsObject.parent.GetComponentInChildren<ManualCameraRenderer>().transform.parent;

            // Increase internal ship cam resolution and FPS if specified
            var internalShipCamObj = StartOfRound.Instance.elevatorTransform.Find("Cameras/ShipCamera");
            var newRT = UpdateSecurityCamFPSAndResolution(internalShipCamObj, Plugin.ShipInternalCamFPS.Value, Plugin.ShipInternalCamSizeMultiplier.Value);
            _oldMonitorsObject.GetComponent<MeshRenderer>().sharedMaterials[2].mainTexture = newRT;

            // Increase external ship cam resolution and FPS if specified
            var externalShipCamObj = StartOfRound.Instance.elevatorTransform.Find("Cameras/FrontDoorSecurityCam/SecurityCamera");
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
                if (Plugin.ShipProfitQuotaMonitorNum.Value == 0)
                {
                    Object.Destroy(_profitQuotaScanNode.gameObject);
                }
                else if (!Plugin.UseBetterMonitors.Value && _profitQuotaText != null)
                {
                    // Update scan node position immediately (better monitors do it delayed)
                    _profitQuotaScanNode.transform.parent = _profitQuotaText.transform;
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
                var newCamRT = new RenderTexture(shipCam.targetTexture);
                for (int i = 0; i < resMultiplier; i++)
                {
                    newCamRT.width *= 2;
                    newCamRT.height *= 2;
                }
                shipCam.targetTexture = newCamRT;
            }

            return shipCam.targetTexture;
        }

        private static void CreateOldStyleMonitors()
        {
            // Copy everything from the existing quota monitor
            if (Plugin.ShipProfitQuotaMonitorNum.Value == 0)
            {
                _profitQuotaBG.gameObject.SetActive(false);
                _profitQuotaText.gameObject.SetActive(false);
            }
            if (Plugin.ShipDeadlineMonitorNum.Value == 0)
            {
                _deadlineBG.gameObject.SetActive(false);
                _deadlineText.gameObject.SetActive(false);
            }
            else
            {
                _deadlineBG.transform.localPosition = _profitQuotaBG.transform.localPosition;
                _deadlineBG.transform.localPosition = _profitQuotaBG.transform.localPosition;
                _deadlineText.transform.localPosition = _profitQuotaText.transform.localPosition;
                _deadlineText.transform.localRotation = _profitQuotaText.transform.localRotation;
            }

            if (Plugin.ShipTotalMonitorNum.Value > 0)
            {
                _totalMonitorBg = Object.Instantiate(_profitQuotaBG, _profitQuotaBG.transform.parent);
                _totalMonitorBg.name = "TotalMonitorBG";
                _totalMonitorText = Object.Instantiate(_profitQuotaText, _profitQuotaText.transform.parent);
                _totalMonitorText.name = "TotalMonitorText";
            }
            if (Plugin.ShipTimeMonitorNum.Value > 0)
            {
                _timeMonitorBG = Object.Instantiate(_profitQuotaBG, _profitQuotaBG.transform.parent);
                _timeMonitorBG.name = "TimeMonitorBG";
                _timeMonitorText = Object.Instantiate(_profitQuotaText, _profitQuotaText.transform.parent);
                _timeMonitorText.name = "TimeMonitorText";
            }
            if (Plugin.ShipWeatherMonitorNum.Value > 0)
            {
                _weatherMonitorBG = Object.Instantiate(_profitQuotaBG, _profitQuotaBG.transform.parent);
                _weatherMonitorBG.name = "WeatherMonitorBG";
                _weatherMonitorText = Object.Instantiate(_profitQuotaText, _profitQuotaText.transform.parent);
                _weatherMonitorText.name = "WeatherMonitorText";
            }
            if (Plugin.ShipSalesMonitorNum.Value > 0)
            {
                _salesMonitorBG = Object.Instantiate(_profitQuotaBG, _profitQuotaBG.transform.parent);
                _salesMonitorBG.name = "SalesMonitorBG";
                _salesMonitorText = Object.Instantiate(_profitQuotaText, _profitQuotaText.transform.parent);
                _salesMonitorText.name = "SalesMonitorText";
            }

            // Store positions and rotations by offset based on monitor index
            var originalPos = _profitQuotaBG.transform.localPosition;
            var originalRot = _profitQuotaBG.transform.localEulerAngles;
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

            // Store the new monitor objects in a separate list
            var monitors = new List<Tuple<int, Image, TextMeshProUGUI>>
            {
                new Tuple<int, Image, TextMeshProUGUI>(Plugin.ShipProfitQuotaMonitorNum.Value, _profitQuotaBG, _profitQuotaText),
                new Tuple<int, Image, TextMeshProUGUI>(Plugin.ShipDeadlineMonitorNum.Value, _deadlineBG, _deadlineText),
                new Tuple<int, Image, TextMeshProUGUI>(Plugin.ShipTotalMonitorNum.Value, _totalMonitorBg, _totalMonitorText),
                new Tuple<int, Image, TextMeshProUGUI>(Plugin.ShipTimeMonitorNum.Value, _timeMonitorBG, _timeMonitorText),
                new Tuple<int, Image, TextMeshProUGUI>(Plugin.ShipWeatherMonitorNum.Value, _weatherMonitorBG, _weatherMonitorText),
                new Tuple<int, Image, TextMeshProUGUI>(Plugin.ShipSalesMonitorNum.Value, _salesMonitorBG, _salesMonitorText)
            };

            // Assign monitors to the positions that were specified, ensuring to not overlap
            for (int i = 1; i <= offsets.Count; i++)
            {
                var targetMonitors = monitors.Where(m => m.Item1 == i);
                Tuple<int, Image, TextMeshProUGUI> targetMonitor = null;
                if (targetMonitors.Any())
                {
                    if (targetMonitors.Count() == 1)
                    {
                        Plugin.MLS.LogInfo($"Creating overlay monitor at position {i}");
                        targetMonitor = targetMonitors.First();
                    }
                    else
                    {
                        Plugin.MLS.LogError($"Multiple monitors specified at position {i}! Check your config to ensure you do not have duplicate monitor assignments. Taking first found.");
                    }
                }
                else if (Plugin.ShowBlueMonitorBackground.Value && Plugin.ShowBackgroundOnAllScreens.Value)
                {
                    targetMonitor = new Tuple<int, Image, TextMeshProUGUI>(i, Object.Instantiate(_profitQuotaBG, _profitQuotaBG.transform.parent), null);
                    _extraBackgrounds.Add(targetMonitor.Item2);
                    targetMonitor.Item2.name = $"ExtraBG{_extraBackgrounds.Count}";
                }

                if (targetMonitor != null)
                {
                    var positionOffset = offsets[i - 1].Key;
                    var rotationOffset = offsets[i - 1].Value;
                    targetMonitor.Item2.transform.localPosition = originalPos + positionOffset;
                    targetMonitor.Item2.transform.localEulerAngles = originalRot + rotationOffset;
                    if (targetMonitor.Item3 != null)
                    {
                        targetMonitor.Item3.transform.localPosition = originalPos + positionOffset + new Vector3(0, 0, -1);
                        targetMonitor.Item3.transform.localEulerAngles = originalRot + rotationOffset;

                        if (targetMonitor.Item3 == _weatherMonitorText && Plugin.FancyWeatherMonitor.Value)
                        {
                            _weatherMonitorText.alignment = TextAlignmentOptions.MidlineLeft;
                            _weatherMonitorText.transform.localPosition += new Vector3(25, 0, -10);
                            _weatherMonitorText.transform.localEulerAngles += new Vector3(-2, 1, 0);
                        }
                    }
                }
            }

            // Disable all backgrounds if needed
            if (!Plugin.ShowBlueMonitorBackground.Value)
            {
                if (_profitQuotaBG != null) _profitQuotaBG.gameObject.SetActive(false);
                if (_deadlineBG != null) _deadlineBG.gameObject.SetActive(false);
                foreach (var t in monitors.Where(m => m.Item2 != null))
                {
                    t.Item2.gameObject.SetActive(false);
                }
            }

            UpdateShipTotalMonitor();
            UpdateTimeMonitor();
            UpdateWeatherMonitor();
            UpdateSalesMonitor();
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
            var monitorAssignments = new List<KeyValuePair<int, Action<TextMeshProUGUI>>>
            {
                new KeyValuePair<int, Action<TextMeshProUGUI>>(Plugin.ShipProfitQuotaMonitorNum.Value, o =>
                {
                    _profitQuotaText = o;
                    if (_profitQuotaScanNode != null)
                    {
                        _profitQuotaScanNode.transform.parent = _newMonitors.GetMonitorTransform(o);
                        _profitQuotaScanNode.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    }
                    CopyProfitQuotaAndDeadlineText();
                }),
                new KeyValuePair<int, Action<TextMeshProUGUI>>(Plugin.ShipDeadlineMonitorNum.Value, o => { _deadlineText = o; CopyProfitQuotaAndDeadlineText(); }),
                new KeyValuePair<int, Action<TextMeshProUGUI>>(Plugin.ShipTotalMonitorNum.Value, o => { _totalMonitorText = o; UpdateShipTotalMonitor(); }),
                new KeyValuePair<int, Action<TextMeshProUGUI>>(Plugin.ShipTimeMonitorNum.Value, o => { _timeMonitorText = o; UpdateTimeMonitor(); }),
                new KeyValuePair<int, Action<TextMeshProUGUI>>(Plugin.ShipWeatherMonitorNum.Value, o =>
                {
                    _weatherMonitorText = o;
                    if (Plugin.FancyWeatherMonitor.Value)
                    {
                        _weatherMonitorText.alignment = TextAlignmentOptions.MidlineLeft;
                        _weatherMonitorText.margin += new Vector4(25, 0, 0, 0);
                    }
                    UpdateWeatherMonitor();
                }),
                new KeyValuePair<int, Action<TextMeshProUGUI>>(Plugin.ShipSalesMonitorNum.Value, o => { _salesMonitorText = o; UpdateSalesMonitor(); })
            };
            var monitorAssignmentsRT = new List<KeyValuePair<int, Material>>
            {
                new KeyValuePair<int, Material>(Plugin.ShipInternalCamMonitorNum.Value, _oldMonitorsObject.GetComponent<MeshRenderer>().materials[2] ),
                new KeyValuePair<int, Material>(Plugin.ShipExternalCamMonitorNum.Value, _oldBigMonitors.GetComponent<MeshRenderer>().materials[2] )
            };

            for (int i = 1; i <= 14; i++)
            {
                var targetMonitors = monitorAssignments.Where(m => m.Key == i);
                var targetMatMonitors = monitorAssignmentsRT.Where(m => m.Key == i);

                if (targetMonitors.Any() || targetMatMonitors.Any())
                {
                    if (targetMonitors.Count() + targetMatMonitors.Count() == 1)
                    {
                        Plugin.MLS.LogInfo($"Assigning monitor to position {i}");

                        if (targetMonitors.Any())
                        {
                            _newMonitors.AssignTextMonitor(i - 1, targetMonitors.First().Value);
                        }
                        else
                        {
                            _newMonitors.AssignMaterialMonitor(i - 1, targetMatMonitors.First().Value);
                        }
                    }
                    else
                    {
                        Plugin.MLS.LogError($"Multiple monitors specified at position {i}! Check your config to ensure you do not have duplicate monitor assignments. Taking first found.");
                    }
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

        public static void CopyProfitQuotaAndDeadlineText()
        {
            if (Plugin.ShipProfitQuotaMonitorNum.Value > 0 && _profitQuotaText != null)
            {
                _profitQuotaText.text = StartOfRound.Instance.profitQuotaMonitorText?.text;
                if (_newMonitors != null)
                {
                    _newMonitors.RenderCameraAfterTextChange(_profitQuotaText);
                }
            }

            if (Plugin.ShipDeadlineMonitorNum.Value > 0 && _deadlineText != null)
            {
                _deadlineText.text = StartOfRound.Instance.deadlineMonitorText?.text;
                if (_newMonitors != null)
                {
                    _newMonitors.RenderCameraAfterTextChange(_deadlineText);
                }
            }
        }

        public static void UpdateShipTotalMonitor()
        {
            if (Plugin.ShipTotalMonitorNum.Value == 0 || _totalMonitorText == null)
            {
                return;
            }

            var allScrap = Object.FindObjectsOfType<GrabbableObject>().Where(o => o.itemProperties.isScrap && o.isInShipRoom && o.isInElevator).ToList();
            int shipLoot = allScrap.Sum(o => o.scrapValue);
            _totalMonitorText.text = $"SCRAP IN SHIP:\n${shipLoot}";
            if (_newMonitors != null)
            {
                _newMonitors.RenderCameraAfterTextChange(_totalMonitorText);
            }
            Plugin.MLS.LogInfo($"Set ship scrap total to ${shipLoot} ({allScrap.Count} items).");
        }

        public static void UpdateTimeMonitor()
        {
            if (Plugin.ShipTimeMonitorNum.Value > 0 && HUDManager.Instance?.clockNumber != null && _timeMonitorText != null)
            {
                Plugin.MLS.LogDebug("Updating time display.");
                if (TimeOfDay.Instance.movingGlobalTimeForward)
                {
                    _timeMonitorText.text = $"TIME:\n{HUDManager.Instance.clockNumber.text.Replace('\n', ' ')}";
                }
                else
                {
                    _timeMonitorText.text = "TIME:\nPENDING";
                }
                if (_newMonitors != null)
                {
                    _newMonitors.RenderCameraAfterTextChange(_timeMonitorText);
                }
            }
        }

        public static void UpdateWeatherMonitor()
        {
            if (Plugin.ShipWeatherMonitorNum.Value > 0 && _weatherMonitorText != null)
            {
                Plugin.MLS.LogInfo("Updating weather monitor");

                if (Plugin.FancyWeatherMonitor.Value)
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

                    _hasOverlays = StartOfRound.Instance.currentLevel?.currentWeather == LevelWeatherType.Stormy;
                    if (_hasOverlays)
                    {
                        _overlayTimer = 0;
                        _overlayCycle = Random.Range(0.1f, 3);
                        _curWeatherOverlays = WeatherASCIIArt.LightningOverlays;
                        _curWeatherOverlayIndex = Random.Range(0, _curWeatherOverlays.Length);
                    }
                    _showingOverlay = false;

                    _curWeatherAnimIndex = 0;
                    _animTimer = 0;
                    _weatherMonitorText.text = _curWeatherAnimations[_curWeatherAnimIndex];
                }
                else
                {
                    _weatherMonitorText.text = $"WEATHER:\n{(StartOfRound.Instance.currentLevel?.currentWeather.ToString() ?? string.Empty)}";
                }

                if (_newMonitors != null)
                {
                    _newMonitors.RenderCameraAfterTextChange(_weatherMonitorText);
                }
            }
        }

        public static void AnimateWeatherMonitor()
        {
            if (!Plugin.FancyWeatherMonitor.Value || _weatherMonitorText == null || _curWeatherAnimations.Length < 2)
            {
                return;
            }

            Action drawWeather = () =>
            {
                var sb = new StringBuilder();
                string[] animLines = _curWeatherAnimations[_curWeatherAnimIndex].Split(Environment.NewLine);
                string[] overlayLines = (_showingOverlay ? _curWeatherOverlays[_curWeatherOverlayIndex] : string.Empty).Split(Environment.NewLine);

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

                _weatherMonitorText.text = sb.ToString();
                if (_newMonitors != null)
                {
                    _newMonitors.RenderCameraAfterTextChange(_weatherMonitorText);
                }
            };

            // Cycle through our current animation pattern 'sprites'
            _animTimer += Time.deltaTime;
            if (_animTimer >= _animCycle)
            {
                _curWeatherAnimIndex = (_curWeatherAnimIndex + 1) % _curWeatherAnimations.Length;
                _animTimer = 0;
                drawWeather();
            }

            // Handle random overlays
            if (_hasOverlays)
            {
                _overlayTimer += Time.deltaTime;
                if (_overlayTimer >= (_showingOverlay ? 0.5f : _overlayCycle))
                {
                    _overlayTimer = 0;
                    _showingOverlay = !_showingOverlay;

                    if (!_showingOverlay)
                    {
                        // Reset the counter for the next overlay
                        _overlayCycle = Random.Range(0.1f, 3);
                        _curWeatherOverlayIndex = Random.Range(0, _curWeatherOverlays.Length);
                    }

                    drawWeather();
                }
            }
        }

        public static void UpdateSalesMonitor()
        {
            if (Plugin.ShipSalesMonitorNum.Value > 0 && _salesMonitorText != null)
            {
                Plugin.MLS.LogDebug("Updating sales display.");
                _salesMonitorText.text = "SALES COMING SOON!";

                if (_newMonitors != null)
                {
                    _newMonitors.RenderCameraAfterTextChange(_salesMonitorText);
                }
            }
        }

        public static void ToggleExtraMonitorPower(bool on)
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
                else
                {
                    bool displayBackgrounds = Plugin.ShowBlueMonitorBackground.Value;

                    if (_profitQuotaBG != null && displayBackgrounds) _profitQuotaBG.gameObject.SetActive(on);
                    if (_profitQuotaText != null) _profitQuotaText.gameObject.SetActive(on);
                    if (_deadlineBG != null && displayBackgrounds) _deadlineBG.gameObject.SetActive(on);
                    if (_deadlineText != null) _deadlineText.gameObject.SetActive(on);
                    if (_totalMonitorBg != null && displayBackgrounds) _totalMonitorBg.gameObject.SetActive(on);
                    if (_totalMonitorText != null) _totalMonitorText.gameObject.SetActive(on);
                    if (_timeMonitorBG != null && displayBackgrounds) _timeMonitorBG.gameObject.SetActive(on);
                    if (_timeMonitorText != null) _timeMonitorText.gameObject.SetActive(on);
                    if (_weatherMonitorBG != null && displayBackgrounds) _weatherMonitorBG.gameObject.SetActive(on);
                    if (_weatherMonitorText != null) _weatherMonitorText.gameObject.SetActive(on);
                    if (_salesMonitorBG != null && displayBackgrounds) _salesMonitorBG.gameObject.SetActive(on);
                    if (_salesMonitorText != null) _salesMonitorText.gameObject.SetActive(on);

                    foreach (var extraBG in _extraBackgrounds)
                    {
                        extraBG.gameObject.SetActive(on);
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