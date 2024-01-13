using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GeneralImprovements
{
    internal static class SceneHelper
    {
        private static Image _salesMonitorBG;
        private static TextMeshProUGUI _salesMonitorText;
        private static Image _weatherMonitorBG;
        private static TextMeshProUGUI _weatherMonitorText;

        public static void CreateExtraMonitors()
        {
            if (!Plugin.ShowExtraShipMonitors.Value)
            {
                return;
            }

            // Copy the display and quota objects
            var existingProfitBG = StartOfRound.Instance.profitQuotaMonitorBGImage;
            var existingProfitText = StartOfRound.Instance.profitQuotaMonitorText;
            var existingDeadlineBG = StartOfRound.Instance.deadlineMonitorBGImage;
            var existingDeadlineText = StartOfRound.Instance.deadlineMonitorText;
            _salesMonitorBG = Object.Instantiate(existingProfitBG, existingProfitBG.transform.parent);
            _salesMonitorText = Object.Instantiate(existingProfitText, existingProfitText.transform.parent);
            _salesMonitorText.text = "SALES COMING SOON";
            _weatherMonitorBG = Object.Instantiate(existingDeadlineBG, existingDeadlineBG.transform.parent);
            _weatherMonitorText = Object.Instantiate(existingDeadlineText, existingDeadlineText.transform.parent);
            _weatherMonitorText.text = "WEATHER COMING SOON";

            // Position our new friends by offset in case things move around
            foreach (var newObj in new MonoBehaviour[] { _salesMonitorBG, _salesMonitorText, _weatherMonitorBG, _weatherMonitorText })
            {
                newObj.transform.localPosition += new Vector3(0, 455, -28);
                newObj.transform.localEulerAngles += new Vector3(-18, 0, 0);
            }
        }

        public static void ToggleExtraMonitorPower(bool on)
        {
            if (Plugin.SyncLittleScreensPower.Value)
            {
                if (_salesMonitorBG != null) _salesMonitorBG.gameObject.SetActive(on);
                if (_salesMonitorText != null) _salesMonitorText.gameObject.SetActive(on);
                if (_weatherMonitorBG != null) _weatherMonitorBG.gameObject.SetActive(on);
                if (_weatherMonitorText != null) _weatherMonitorText.gameObject.SetActive(on);
            }
        }
    }
}