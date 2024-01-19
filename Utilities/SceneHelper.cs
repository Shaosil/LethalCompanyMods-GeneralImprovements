using GeneralImprovements.Patches;
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
    internal static class SceneHelper
    {
        private static Image _totalMonitorBg;
        private static TextMeshProUGUI _totalMonitorText;
        private static Image _timeMonitorBG;
        private static TextMeshProUGUI _timeMonitorText;
        private static Image _weatherMonitorBG;
        private static TextMeshProUGUI _weatherMonitorText;
        private static Image _salesMonitorBG;
        private static TextMeshProUGUI _salesMonitorText;

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

        public static GameObject MedStation;
        public static int MaxHealth;

        public static void CreateExtraMonitors()
        {
            // Resize the two extra monitor texts to be the same as their respective backgrounds, and give them padding
            var quotaBG = StartOfRound.Instance.profitQuotaMonitorBGImage;
            var quotaText = StartOfRound.Instance.profitQuotaMonitorText;
            var deadlineBG = StartOfRound.Instance.deadlineMonitorBGImage;
            var deadlineText = StartOfRound.Instance.deadlineMonitorText;
            quotaText.rectTransform.sizeDelta = quotaBG.rectTransform.sizeDelta;
            quotaText.transform.position = quotaBG.transform.TransformPoint(Vector3.back);
            quotaText.fontSize = quotaText.fontSize * 0.9f;
            quotaText.margin = Vector4.one * 5;
            deadlineText.rectTransform.sizeDelta = deadlineBG.rectTransform.sizeDelta;
            deadlineText.transform.position = deadlineBG.transform.TransformPoint(Vector3.back);
            deadlineText.fontSize = deadlineText.fontSize * 0.9f;
            deadlineText.margin = Vector4.one * 5;

            // Copy everything from the existing quota monitor
            if (Plugin.ShipTotalMonitorNum.Value > 0)
            {
                _totalMonitorBg = Object.Instantiate(quotaBG, quotaBG.transform.parent);
                _totalMonitorBg.name = "TotalMonitorBG";
                _totalMonitorText = Object.Instantiate(quotaText, quotaText.transform.parent);
                _totalMonitorText.name = "TotalMonitorText";
                _totalMonitorText.alignment = TextAlignmentOptions.Center;
            }
            if (Plugin.ShipTimeMonitorNum.Value > 0)
            {
                _timeMonitorBG = Object.Instantiate(quotaBG, quotaBG.transform.parent);
                _timeMonitorBG.name = "TimeMonitorBG";
                _timeMonitorText = Object.Instantiate(quotaText, quotaText.transform.parent);
                _timeMonitorText.name = "TimeMonitorText";
                _timeMonitorText.alignment = TextAlignmentOptions.Center;
            }
            if (Plugin.ShipWeatherMonitorNum.Value > 0)
            {
                _weatherMonitorBG = Object.Instantiate(quotaBG, quotaBG.transform.parent);
                _weatherMonitorBG.name = "WeatherMonitorBG";
                _weatherMonitorText = Object.Instantiate(quotaText, quotaText.transform.parent);
                _weatherMonitorText.name = "WeatherMonitorText";
                _weatherMonitorText.rectTransform.localPosition += new Vector3(10, 0, 0);
            }
            if (Plugin.ShipSalesMonitorNum.Value > 0)
            {
                _salesMonitorBG = Object.Instantiate(quotaBG, quotaBG.transform.parent);
                _salesMonitorBG.name = "SalesMonitorBG";
                _salesMonitorText = Object.Instantiate(quotaText, quotaText.transform.parent);
                _salesMonitorText.name = "SalesMonitorText";
            }

            // Store positions and rotations by offset based on monitor index
            var offsets = new List<KeyValuePair<Vector3, Vector3>>
            {
                new KeyValuePair<Vector3, Vector3>(new Vector3(0, 465, -22), new Vector3(-18, 0, 0)),       // Monitor 1
                new KeyValuePair<Vector3, Vector3>(new Vector3(470, 465, -22), new Vector3(-18, 0, 0)),     // Monitor 2
                new KeyValuePair<Vector3, Vector3>(new Vector3(970, 485, -128), new Vector3(-18, 25, 5)),   // Monitor 3
                new KeyValuePair<Vector3, Vector3>(new Vector3(1390, 525, -329), new Vector3(-18, 25, 5)),  // Monitor 4
                new KeyValuePair<Vector3, Vector3>(new Vector3(1025, 30, -115), new Vector3(-1, 25, 5)),    // Monitor 5
                new KeyValuePair<Vector3, Vector3>(new Vector3(1445, 72, -320), new Vector3(-1, 25, 5))     // Monitor 6
            };

            // Store the new monitor objects in a separate list
            var monitors = new List<Tuple<int, Component, Component>>
            {
                new Tuple<int, Component, Component>(Plugin.ShipTotalMonitorNum.Value, _totalMonitorBg, _totalMonitorText),
                new Tuple<int, Component, Component>(Plugin.ShipTimeMonitorNum.Value, _timeMonitorBG, _timeMonitorText),
                new Tuple<int, Component, Component>(Plugin.ShipWeatherMonitorNum.Value, _weatherMonitorBG, _weatherMonitorText),
                new Tuple<int, Component, Component>(Plugin.ShipSalesMonitorNum.Value, _salesMonitorBG, _salesMonitorText)
            };

            // Assign monitors to the positions that were specified, ensuring to not overlap
            var existingAssignments = new HashSet<int>();
            for (int i = 1; i <= offsets.Count; i++)
            {
                var targetMonitor = monitors.FirstOrDefault(m => m.Item1 == i);
                if (targetMonitor != null)
                {
                    if (!existingAssignments.Contains(i))
                    {
                        Plugin.MLS.LogInfo($"Creating monitor at position {i}");
                        existingAssignments.Add(i);

                        var positionOffset = offsets[i - 1].Key;
                        var rotationOffset = offsets[i - 1].Value;
                        targetMonitor.Item2.transform.localPosition += positionOffset;
                        targetMonitor.Item2.transform.localEulerAngles += rotationOffset;
                        targetMonitor.Item3.transform.localPosition += positionOffset + new Vector3(0, 0, -1);
                        targetMonitor.Item3.transform.localEulerAngles += rotationOffset;
                    }
                    else
                    {
                        Plugin.MLS.LogError($"Already created monitor at position {i} - skipping extra call! Check your config to ensure you do not have duplicate monitor assignments.");
                    }
                }
            }

            // Disable all backgrounds if needed
            if (!Plugin.ShowBlueMonitorBackground.Value)
            {
                if (quotaBG != null) quotaBG.gameObject.SetActive(false);
                if (deadlineBG != null) deadlineBG.gameObject.SetActive(false);
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

        public static void UpdateShipTotalMonitor()
        {
            if (Plugin.ShipTotalMonitorNum.Value == 0 || _totalMonitorText == null)
            {
                return;
            }

            var allScrap = Object.FindObjectsOfType<GrabbableObject>().Where(o => o.itemProperties.isScrap && o.isInShipRoom && o.isInElevator).ToList();
            int shipLoot = allScrap.Sum(o => o.scrapValue);
            _totalMonitorText.text = $"SCRAP IN SHIP:\n${shipLoot}";
            Plugin.MLS.LogInfo($"Set ship scrap total to ${shipLoot} ({allScrap.Count} items).");
        }

        public static void UpdateTimeMonitor()
        {
            if (Plugin.ShipTimeMonitorNum.Value > 0 && HUDManager.Instance?.clockNumber != null)
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
            }
        }

        public static void ToggleExtraMonitorPower(bool on)
        {
            if (Plugin.SyncExtraMonitorsPower.Value)
            {
                bool displayBackgrounds = Plugin.ShowBlueMonitorBackground.Value;
                if (_timeMonitorBG != null && displayBackgrounds) _timeMonitorBG.gameObject.SetActive(on);
                if (_timeMonitorText != null) _timeMonitorText.gameObject.SetActive(on);
                if (_weatherMonitorBG != null && displayBackgrounds) _weatherMonitorBG.gameObject.SetActive(on);
                if (_weatherMonitorText != null) _weatherMonitorText.gameObject.SetActive(on);
                if (_salesMonitorBG != null && displayBackgrounds) _salesMonitorBG.gameObject.SetActive(on);
                if (_salesMonitorText != null) _salesMonitorText.gameObject.SetActive(on);
            }
        }

        public static void CreateMedStation()
        {
            if (Plugin.AddHealthRechargeStation.Value && AssetBundleHelper.MedStationPrefab != null)
            {
                Plugin.MLS.LogInfo("Adding medical station to ship");
                MedStation = Object.Instantiate(AssetBundleHelper.MedStationPrefab, new Vector3(2.75f, 3.4f, -16.561f), Quaternion.Euler(-90, 0, 0), StartOfRound.Instance.elevatorTransform);

                // Interaction trigger
                var chargeStation = Object.FindObjectOfType<ItemCharger>().GetComponent<InteractTrigger>();
                var chargeTriggerCollider = chargeStation.GetComponent<BoxCollider>();
                chargeTriggerCollider.center = Vector3.zero;
                chargeTriggerCollider.size = new Vector3(1, 0.8f, 0.8f);
                var medTrigger = MedStation.transform.Find("Trigger");
                medTrigger.tag = chargeStation.tag;
                medTrigger.GetComponent<AudioSource>().outputAudioMixerGroup = chargeStation.GetComponent<AudioSource>().outputAudioMixerGroup;
                medTrigger.gameObject.layer = chargeStation.gameObject.layer;
                var interactScript = medTrigger.gameObject.AddComponent<InteractTrigger>();
                interactScript.hoverTip = "Heal";
                interactScript.disabledHoverTip = "(Health Full)";
                interactScript.hoverIcon = chargeStation.hoverIcon;
                interactScript.specialCharacterAnimation = true;
                interactScript.animationString = chargeStation.animationString;
                interactScript.lockPlayerPosition = true;
                interactScript.playerPositionNode = chargeStation.playerPositionNode;
                interactScript.onInteract = new InteractEvent();
                interactScript.onCancelAnimation = new InteractEvent();
                interactScript.onInteractEarly = new InteractEvent();
                interactScript.onInteractEarly.AddListener(_ => PlayerControllerBPatch.HealLocalPlayer());

                // Scan node
                var scanNode = MedStation.transform.Find("ScanNode");
            }
        }
    }
}