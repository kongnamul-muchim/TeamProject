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
        [Tooltip("보스의 BossSuspicionSystem 컴포넌트 (직접 할당 시 우선 사용, null이면 SuspicionUIManager 자동 연결)")]
        [SerializeField] private BossSuspicionSystem bossSuspicionSystem;

        [Header("UI 참조")]
        [Tooltip("의심도 게이지 이미지")]
        [SerializeField] private Image suspicionFillImage;  // 의심도 게이지 바
        [Tooltip("의심도 수치 텍스트")]
        [SerializeField] private UnityEngine.UI.Text suspicionText;        // 텍스트 (0% ~ 100%)
        [Tooltip("의심도 단계 텍스트")]
        [SerializeField] private UnityEngine.UI.Text suspicionLevelText;   // 레벨 텍스트

        [Header("색상 설정")]
        [Tooltip("안전 단계 색상")]
        [SerializeField] private Color safeColor = Color.green;
        [Tooltip("주의 단계 색상")]
        [SerializeField] private Color cautionColor = Color.yellow;
        [Tooltip("위험 단계 색상")]
        [SerializeField] private Color dangerColor = new Color(1f, 0.5f, 0f);
        [Tooltip("심각 단계 색상")]
        [SerializeField] private Color criticalColor = Color.red;
        [Tooltip("발각 단계 색상")]
        [SerializeField] private Color detectedColor = Color.magenta;

        private void OnEnable()
        {
            if (bossSuspicionSystem != null)
            {
                bossSuspicionSystem.OnLevelChanged += OnLevelChanged;
                bossSuspicionSystem.OnDetected += OnDetected;
                bossSuspicionSystem.OnValueChanged += OnValueChanged;
            }
            else if (SuspicionUIManager.Instance != null)
            {
                SuspicionUIManager.Instance.OnLevelChanged += OnLevelChanged;
                SuspicionUIManager.Instance.OnDetected += OnDetected;
                SuspicionUIManager.Instance.OnValueChanged += OnValueChanged;
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
            else if (SuspicionUIManager.Instance != null)
            {
                SuspicionUIManager.Instance.OnLevelChanged -= OnLevelChanged;
                SuspicionUIManager.Instance.OnDetected -= OnDetected;
                SuspicionUIManager.Instance.OnValueChanged -= OnValueChanged;
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
