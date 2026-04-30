using UnityEngine;
using UnityEngine.UI;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의심도 게이지 UI 표시 (투명도 기반)
    /// fillImage의 alpha값만으로 의심도를 표현 (0 = 완전 투명, 1 = 완전 불투명)
    /// </summary>
    public class SuspicionMeterUI : MonoBehaviour
    {
        [Header("의심도 시스템 참조")]
        [Tooltip("보스의 BossSuspicionSystem 컴포넌트 (직접 할당 시 우선 사용, null이면 SuspicionUIManager 자동 연결)")]
        [SerializeField] private BossSuspicionSystem bossSuspicionSystem;

        [Header("UI 참조")]
        [Tooltip("의심도 게이지 이미지 (alpha값으로 의심도 표시)")]
        [SerializeField] private Image suspicionFillImage;

        private void OnEnable()
        {
            if (bossSuspicionSystem != null)
            {
                bossSuspicionSystem.OnValueChanged += OnValueChanged;
            }
            else if (SuspicionUIManager.Instance != null)
            {
                SuspicionUIManager.Instance.OnValueChanged += OnValueChanged;
            }
        }

        private void OnDisable()
        {
            if (bossSuspicionSystem != null)
            {
                bossSuspicionSystem.OnValueChanged -= OnValueChanged;
            }
            else if (SuspicionUIManager.Instance != null)
            {
                SuspicionUIManager.Instance.OnValueChanged -= OnValueChanged;
            }
        }

        private void OnValueChanged(float value)
        {
            if (suspicionFillImage == null) return;

            // 의심도에 따라 alpha값만 조절 (0~100 -> 0~200/255)
            // 최대 알파 200 (255 기준)으로 설정
            // 색상은 스프라이트 원본 유지
            float alpha = Mathf.Clamp(value / 100f, 0f, 200f / 255f);
            suspicionFillImage.color = new Color(
                suspicionFillImage.color.r,
                suspicionFillImage.color.g,
                suspicionFillImage.color.b,
                alpha
            );
        }
    }
}
