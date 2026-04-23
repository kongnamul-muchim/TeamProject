using UnityEngine;
using UnityEngine.UI;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의심도 게이지 UI 표시
    /// BossSuspicionSystem의 이벤트를 구독하여 UI 업데이트
    /// </summary>
    public class SuspicionMeterUI : MonoBehaviour
    {
        [Header("의심도 시스템 참조")]
        [Tooltip("보스의 BossSuspicionSystem 컴포넌트")]
        [SerializeField] private BossSuspicionSystem bossSuspicionSystem;

        [Header("UI 참조")]
        [SerializeField] private Image suspicionFillImage;  // 의심도 게이지 바
        [SerializeField] private UnityEngine.UI.Text suspicionText;        // 텍스트 (0% ~ 100%)
        [SerializeField] private UnityEngine.UI.Text suspicionLevelText;   // 레벨 텍스트

        [Header("색상 설정")]
        [SerializeField] private Color safeColor = Color.green;
        [SerializeField] private Color cautionColor = Color.yellow;
        [SerializeField] private Color dangerColor = new Color(1f, 0.5f, 0f);
        [SerializeField] private Color criticalColor = Color.red;
        [SerializeField] private Color detectedColor = Color.magenta;

        private void OnEnable()
        {
            if (bossSuspicionSystem != null)
            {
                bossSuspicionSystem.OnLevelChanged += OnLevelChanged;
                bossSuspicionSystem.OnDetected += OnDetected;
                bossSuspicionSystem.OnValueChanged += OnValueChanged;
            }
        }

        private void OnDisable()
        {
            if (bossSuspicionSystem != null)
            {
                bossSuspicionSystem.OnLevelChanged -= OnLevelChanged;
                bossSuspicionSystem.OnDetected -= OnDetected;
                bossSuspicionSystem.OnValueChanged -= OnValueChanged;
            }
        }

        private void OnValueChanged(float value)
        {
            // 게이지 바 업데이트
            if (suspicionFillImage != null)
            {
                suspicionFillImage.fillAmount = value / 100f;
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
                suspicionLevelText.color = GetColorForLevel(level);
            }

            if (suspicionFillImage != null)
            {
                suspicionFillImage.color = GetColorForLevel(level);
            }
        }

        private void OnDetected()
        {
            if (suspicionLevelText != null)
            {
                suspicionLevelText.text = "DETECTED!";
                suspicionLevelText.color = detectedColor;
            }
        }

        private void OnClear()
        {
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
    }
}
