using GeneralImprovements.Patches;
using System;
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
            // Copy the display and quota objects
            if (Plugin.ShowShipTimeMonitor.Value)
            {
                var existingProfitBG = StartOfRound.Instance.profitQuotaMonitorBGImage;
                var existingProfitText = StartOfRound.Instance.profitQuotaMonitorText;
                _timeMonitorBG = Object.Instantiate(existingProfitBG, existingProfitBG.transform.parent);
                _timeMonitorText = Object.Instantiate(existingProfitText, existingProfitText.transform.parent);
                _timeMonitorText.alignment = TextAlignmentOptions.Center;
                UpdateTimeMonitor();
            }
            if (Plugin.ShowShipWeatherMonitor.Value)
            {
                var existingDeadlineBG = StartOfRound.Instance.deadlineMonitorBGImage;
                var existingDeadlineText = StartOfRound.Instance.deadlineMonitorText;
                _weatherMonitorBG = Object.Instantiate(existingDeadlineBG, existingDeadlineBG.transform.parent);
                _weatherMonitorText = Object.Instantiate(existingDeadlineText, existingDeadlineText.transform.parent);
                _weatherMonitorText.rectTransform.localPosition += new Vector3(10, 0, 0);
                UpdateWeatherMonitor();
            }
            if (Plugin.ShowShipSalesMonitor.Value)
            {
                // TODO
            }

            // Position our new friends by offset in case things move around
            foreach (var newObj in new MonoBehaviour[] { _timeMonitorBG, _timeMonitorText, _weatherMonitorBG, _weatherMonitorText, _salesMonitorBG, _salesMonitorText }.Where(x => x != null))
            {
                newObj.transform.localPosition += new Vector3(0, 455, -28);
                newObj.transform.localEulerAngles += new Vector3(-18, 0, 0);
            }
        }

        public static void ToggleExtraMonitorPower(bool on)
        {
            if (Plugin.SyncLittleScreensPower.Value)
            {
                if (_timeMonitorBG != null) _timeMonitorBG.gameObject.SetActive(on);
                if (_timeMonitorText != null) _timeMonitorText.gameObject.SetActive(on);
                if (_weatherMonitorBG != null) _weatherMonitorBG.gameObject.SetActive(on);
                if (_weatherMonitorText != null) _weatherMonitorText.gameObject.SetActive(on);
            }
        }

        public static void UpdateWeatherMonitor()
        {
            if (Plugin.ShowShipWeatherMonitor.Value && _weatherMonitorText != null)
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

        public static void UpdateTimeMonitor(bool force = false)
        {
            if (Plugin.ShowShipTimeMonitor.Value && HUDManager.Instance?.clockNumber != null)
            {
                Plugin.MLS.LogInfo("Updating time display.");
                if ((force || StartOfRound.Instance.shipHasLanded) && (StartOfRound.Instance.currentLevel?.planetHasTime ?? false))
                {
                    _timeMonitorText.text = $"TIME:\n{HUDManager.Instance.clockNumber.text.Replace('\n', ' ')}";
                }
                else
                {
                    _timeMonitorText.text = "TIME:\nN/A";
                }
            }
        }

        public static void CreateMedStation()
        {
            if (Plugin.AllowHealthRecharge.Value && AssetBundleHelper.MedStationPrefab != null)
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