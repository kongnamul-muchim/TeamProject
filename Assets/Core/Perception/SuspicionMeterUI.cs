using UnityEngine;
using UnityEngine.UI;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의심도 게이지 UI 표시
    /// </summary>
    public class SuspicionMeterUI : MonoBehaviour
    {
        [Header("연동할 의심도 계량기")]
        [SerializeField] private SuspicionMeter suspicionMeter;

        [Header("UI 참조")]
        [SerializeField] private Image suspicionFillImage;  // 의심도 게이지 바
        [SerializeField] private Text suspicionText;        // 텍스트 (0% ~ 100%)
        [SerializeField] private Text suspicionLevelText;   // 레벨 텍스트 (Safe, Caution, Danger, Critical, Detected)

        [Header("색상 설정")]
        [SerializeField] private Color safeColor = Color.green;
        [SerializeField] private Color cautionColor = Color.yellow;
        [SerializeField] private Color dangerColor = new Color(1f, 0.5f, 0f); // Orange
        [SerializeField] private Color criticalColor = Color.red;
        [SerializeField] private Color detectedColor = Color.magenta;

        private void Start()
        {
            if (suspicionMeter != null)
            {
                suspicionMeter.OnLevelChanged += OnLevelChanged;
                suspicionMeter.OnDetected += OnDetected;
                suspicionMeter.OnClear += OnClear;
            }
        }

        private void OnDestroy()
        {
            if (suspicionMeter != null)
            {
                suspicionMeter.OnLevelChanged -= OnLevelChanged;
                suspicionMeter.OnDetected -= OnDetected;
                suspicionMeter.OnClear -= OnClear;
            }
        }

        private void Update()
        {
            if (suspicionMeter == null) return;

            float value = suspicionMeter.CurrentValue;

            // 게이지 바 업데이트
            if (suspicionFillImage != null)
            {
                suspicionFillImage.fillAmount = value / 100f;
                suspicionFillImage.color = GetColorForLevel(suspicionMeter.CurrentLevel);
            }

            // 텍스트 업데이트
            if (suspicionText != null)
            {
                suspicionText.text = $"{value:F0}%";
            }
        }

        private void OnLevelChanged(SuspicionLevel level)
        {
            if (suspicionLevelText != null)
            {
                suspicionLevelText.text = level.ToString();
            }

            // 색상 변경
            if (suspicionFillImage != null)
            {
                suspicionFillImage.color = GetColorForLevel(level);
            }

            Debug.Log($"[SuspicionUI] Level changed: {level}");
        }

        private void OnDetected()
        {
            Debug.Log("[SuspicionUI] DETECTED!");
            if (suspicionLevelText != null)
            {
                suspicionLevelText.text = "DETECTED!";
                suspicionLevelText.color = detectedColor;
            }
        }

        private void OnClear()
        {
            Debug.Log("[SuspicionUI] Suspicion cleared!");
            if (suspicionLevelText != null)
            {
                suspicionLevelText.text = "Safe";
                suspicionLevelText.color = safeColor;
            }
        }

        private Color GetColorForLevel(SuspicionLevel level)
        {
            return level switch
            {
                SuspicionLevel.Safe => safeColor,
                SuspicionLevel.Caution => cautionColor,
                SuspicionLevel.Danger => dangerColor,
                SuspicionLevel.Critical => criticalColor,
                SuspicionLevel.Detected => detectedColor,
                _ => Color.white
            };
        }

        /// <summary>
        /// 의심도 계량기 설정
        /// </summary>
        public void SetSuspicionMeter(SuspicionMeter meter)
        {
            if (suspicionMeter != null)
            {
                suspicionMeter.OnLevelChanged -= OnLevelChanged;
                suspicionMeter.OnDetected -= OnDetected;
                suspicionMeter.OnClear -= OnClear;
            }

            suspicionMeter = meter;

            if (suspicionMeter != null)
            {
                suspicionMeter.OnLevelChanged += OnLevelChanged;
                suspicionMeter.OnDetected += OnDetected;
                suspicionMeter.OnClear += OnClear;
            }
        }
    }
}
