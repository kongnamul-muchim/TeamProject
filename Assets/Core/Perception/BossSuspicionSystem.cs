using UnityEngine;
using System;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Enemy.Boss.Gimmicks;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 보스 전용 독립 의심도 시스템
    /// - 시야 기반 의심도 + 근접 기반 의심도 듀얼 채널
    /// - 의태 상태 연동 (감속/정지)
    /// - 보스 프리팹에 붙여 사용 (인스펙터 설정 가능)
    /// </summary>
    public class BossSuspicionSystem : MonoBehaviour
    {
        [Header("의심도 임계값")]
        [Tooltip("주의 상태 임계값")]
        [SerializeField] private float cautionThreshold = 30f;
        [Tooltip("위험 상태 임계값")]
        [SerializeField] private float dangerThreshold = 60f;
        [Tooltip("발각 상태 임계값")]
        [SerializeField] private float detectedThreshold = 100f;

        [Header("상승 속도 (초당)")]
        [Tooltip("시야 기반 의심도 상승 속도")]
        [SerializeField] private float visionIncreaseSpeed = 10f;

        [Header("하락 속도 (초당)")]
        [Tooltip("일반 의심도 하락 속도")]
        [SerializeField] private float normalDecreaseSpeed = 5f;
        [Tooltip("의태 중 의심도 하락 속도")]
        [SerializeField] private float camouflageDecreaseSpeed = 15f;

        [Header("Gizmos 시각화 (AmbushGimmick 연동)")]
        [Tooltip("시각화용 기믹 (에디터에서 Gizmos 업데이트용)")]
        [SerializeField] public AmbushGimmick linkedGimmick;

        // 상태
        private float _currentValue;
        private SuspicionLevel _currentLevel;
        private bool _isCamouflaging;
        private bool _isPerfectCamouflage;
        private bool _wasDetected;
        private float _lastDetectedTime;

        // Gizmos 표시용 반경 (AmbushGimmick에서 설정)
        private float _farSuspicionRadius = 10f;
        private float _nearSuspicionRadius = 3f;

        /// <summary>
        /// 의심도 범위 설정 (AmbushGimmick에서 호출)
        /// </summary>
        public void SetSuspicionRadius(float farRadius, float nearRadius)
        {
            _farSuspicionRadius = farRadius;
            _nearSuspicionRadius = nearRadius;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (linkedGimmick != null)
            {
                _farSuspicionRadius = linkedGimmick.FarSuspicionRadius;
                _nearSuspicionRadius = linkedGimmick.NearSuspicionRadius;
            }
        }
#endif

        // 이벤트
        public event Action<SuspicionLevel> OnLevelChanged;
        public event Action OnDetected;
        public event Action<float> OnValueChanged;

        // 프로퍼티
        public float CurrentValue => Mathf.Clamp(_currentValue, 0f, 100f);
        public SuspicionLevel CurrentLevel => _currentLevel;
        public bool IsCamouflaging => _isCamouflaging;

        private void Update()
        {
            // 발각 상태 체크
            if (_currentValue >= detectedThreshold && !_wasDetected)
            {
                _wasDetected = true;
                _lastDetectedTime = Time.time;
                OnDetected?.Invoke();
            }

            // 의심도 하락 처리
            if (_currentValue > 0f)
            {
                // 의태 중이면 빠른 하락
                float decreaseSpeed = _isCamouflaging ? camouflageDecreaseSpeed : normalDecreaseSpeed;
                _currentValue -= decreaseSpeed * Time.deltaTime;
                _currentValue = Mathf.Max(_currentValue, 0f);
            }

            // 레벨 체크
            CheckLevelChange();

            // 이벤트 발생
            OnValueChanged?.Invoke(CurrentValue);
        }

        /// <summary>
        /// 시야 기반 의심도 보고 (Player가 시야각 내에 있을 때)
        /// 의태 중이면 상승 안 함
        /// </summary>
        public void ReportVisionDetection(float intensity = 1f)
        {
            if (_isCamouflaging) return; // 의태 중이면 시야 기반 상승 무시

            _currentValue += intensity * visionIncreaseSpeed * Time.deltaTime;
            _currentValue = Mathf.Clamp(_currentValue, 0f, 100f);
            CheckLevelChange();
        }

        /// <summary>
        /// 의심도 직접 추가 (기믹에서 호출)
        /// </summary>
        public void AddSuspicion(float rate, float deltaTime)
        {
            _currentValue += rate * deltaTime;
            _currentValue = Mathf.Clamp(_currentValue, 0f, 100f);
            CheckLevelChange();
        }

        /// <summary>
        /// 의태 상태 설정
        /// </summary>
        public void SetCamouflageState(bool isCamouflaging, bool isPerfect = false)
        {
            _isCamouflaging = isCamouflaging;
            _isPerfectCamouflage = isPerfect;
        }

        /// <summary>
        /// 의심도 리셋 (챕터 전환 등)
        /// </summary>
        public void ResetSuspicion()
        {
            float previousValue = _currentValue;
            _currentValue = 0f;
            _currentLevel = SuspicionLevel.Safe;
            _wasDetected = false;
            _lastDetectedTime = 0f;

            if (previousValue > 0f)
            {
                OnValueChanged?.Invoke(0f);
            }
        }

        /// <summary>
        /// 의심도 강제 설정
        /// </summary>
        public void SetSuspicion(float value)
        {
            _currentValue = Mathf.Clamp(value, 0f, 100f);
            CheckLevelChange();
        }

        private void CheckLevelChange()
        {
            SuspicionLevel newLevel = CalculateLevel(_currentValue);
            if (newLevel != _currentLevel)
            {
                _currentLevel = newLevel;
                OnLevelChanged?.Invoke(_currentLevel);
            }
        }

        private SuspicionLevel CalculateLevel(float value)
        {
            if (value >= detectedThreshold) return SuspicionLevel.Detected;
            if (value >= dangerThreshold) return SuspicionLevel.Danger;
            if (value >= cautionThreshold) return SuspicionLevel.Caution;
            return SuspicionLevel.Safe;
        }

        /// <summary>
        /// 의심도 상승 범위 Gizmos 표시
        /// - 네모박스 (사각형 영역): 주황색 와이어프레임 (Far Radius 기준)
        /// - 바닥 원형: 붉은색 디스크 (Near Radius 기준, 위험 지역)
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            // 네모박스 (사각형 영역) - 주황색 와이어프레임 (Far Radius 기준)
            float boxSize = _farSuspicionRadius * 2f;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawWireCube(transform.position, new Vector3(boxSize, 0.1f, boxSize));

            // 바닥 원형 - 붉은색 디스크 (Near Radius 기준, 위험 지역)
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - 0.05f, transform.position.z), _nearSuspicionRadius);

            // 중심점 표시
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position, 0.15f);
        }
    }
}
