using UnityEngine;
using System;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의심도 시스템 구현체 (MonoBehaviour)
    /// </summary>
    public sealed class SuspicionMeter : MonoBehaviour, ISuspicionMeter
    {
        [Header("임계값 설정")]
        [SerializeField] private float cautionThreshold = 30f;
        [SerializeField] private float dangerThreshold = 60f;
        [SerializeField] private float criticalThreshold = 80f;
        [SerializeField] private float detectedThreshold = 100f;

        [Header("상승/하락 속도 (초당)")]
        [SerializeField] private float increaseSpeed = 40f;
        [SerializeField] private float decreaseSpeed = 10f;

        [Header("의태 하락 보정치")]
        [SerializeField] private float camouflageReduceMultiplier = 0.5f;
        [SerializeField] private float perfectCamouflageReducePerSec = 6f;

        [Header("감지 Grace Period")]
        [SerializeField] private float detectionGracePeriod = 0.5f;

        [Header("발각 후 추적 복귀 설정")]
        [SerializeField] private float detectedStateDuration = 2f;  // 발각 후 추적 상태 유지 시간
        [SerializeField] private float minSuspicionAfterDetected = 0.3f;  // 발각 후 최소 의심도 (30%)

        // 현재 의심도 값
        private float _currentValue;

        // 상태
        private bool _isCamouflaging;
        private bool _isPerfectCamouflage;
        private SuspicionLevel _currentLevel;

        // 근접 감지 중 자연 하락 방지
        private float _lastDetectionTime;

        // 발각 후 추적 복귀 시스템
        private float _lastDetectedTime;  // 마지막 발각 시점
        private bool _wasDetected;  // 이전 프레임에서 발각 상태였는지

        // 이벤트
        public event Action<SuspicionLevel> OnLevelChanged;
        public event Action OnDetected;
        public event Action OnClear;

        /// <summary>
        /// 현재 의심도 값 (0~100)
        /// </summary>
        public float CurrentValue => Mathf.Clamp(_currentValue, 0f, 100f);

        /// <summary>
        /// 현재 의심도 레벨
        /// </summary>
        public SuspicionLevel CurrentLevel => _currentLevel;

        /// <summary>
        /// 의태 중 여부
        /// </summary>
        public bool IsCamouflaging => _isCamouflaging;

        /// <summary>
        /// 완벽 의태 여부
        /// </summary>
        public bool IsPerfectCamouflage => _isPerfectCamouflage;

        private void Awake()
        {
            _currentValue = 0f;
            _isCamouflaging = false;
            _isPerfectCamouflage = false;
            _currentLevel = SuspicionLevel.Safe;
            _lastDetectionTime = 0f;
            _lastDetectedTime = 0f;
            _wasDetected = false;
        }

        private void Update()
        {
            // 쿨다운 감소
            _lastDetectionTime -= Time.deltaTime;

            // 발각 후 추적 복귀 시간 체크
            bool isInDetectedCooldown = _wasDetected && (Time.time - _lastDetectedTime < detectedStateDuration);

            // 발각 상태에서 벗어났을 때 (추적 복귀 시작)
            if (_wasDetected && _currentValue < detectedThreshold)
            {
                _wasDetected = false;
            }

            // 발각 복귀 중이면 하락 제한 (minSuspicionAfterDetected 이상 유지)
            if (isInDetectedCooldown && _currentValue > minSuspicionAfterDetected)
            {
                ReduceSuspicion(1f, Time.deltaTime);
                // minSuspicionAfterDetected 이하로 떨어지지 않도록
                _currentValue = Mathf.Max(_currentValue, minSuspicionAfterDetected);
            }
            // 일반 하락
            else if (_lastDetectionTime <= 0f && _currentValue > 0f)
            {
                ReduceSuspicion(1f, Time.deltaTime);
            }
        }

        /// <summary>
        /// 감지 시 호출 (상승) - 근접 감지에서도 호출됨
        /// </summary>
        /// <param name="detectionIntensity">감지 강도 (0~1, 클수록 빠르게 상승)</param>
        public void OnDetectedTarget(float detectionIntensity = 1f)
        {
            _lastDetectionTime = detectionGracePeriod;  // Grace period 갱신
            AddSuspicion(detectionIntensity, Time.deltaTime);
        }

        /// <summary>
        /// 의심도 상승
        /// </summary>
        /// <param name="amount">상승량 (초당)</param>
        /// <param name="deltaTime">경과 시간 (프레임 독립적 계산)</param>
        public void AddSuspicion(float amount, float deltaTime)
        {
            _currentValue += amount * increaseSpeed * deltaTime;
            _currentValue = Mathf.Clamp(_currentValue, 0f, 100f);

            CheckLevelChange();
            CheckDetected();
        }

        /// <summary>
        /// 의심도 하락
        /// </summary>
        /// <param name="amount">하락량 (초당)</param>
        /// <param name="deltaTime">경과 시간 (프레임 독립적 계산)</param>
        public void ReduceSuspicion(float amount, float deltaTime)
        {
            float decreaseAmount = amount * decreaseSpeed * deltaTime;

            // 의태 중이면 추가 하락 적용
            if (_isCamouflaging)
            {
                decreaseAmount *= (1f + camouflageReduceMultiplier);
            }

            // 완벽 의태면 추가 하락
            if (_isPerfectCamouflage)
            {
                decreaseAmount += perfectCamouflageReducePerSec * deltaTime;
            }

            _currentValue -= decreaseAmount;
            _currentValue = Mathf.Clamp(_currentValue, 0f, 100f);

            CheckLevelChange();
            CheckClear();
        }

        /// <summary>
        /// 의심도 리셋
        /// </summary>
        public void Reset()
        {
            float previousValue = _currentValue;
            _currentValue = 0f;
            _currentLevel = SuspicionLevel.Safe;

            if (previousValue > 0f && OnClear != null)
            {
                OnClear.Invoke();
            }
        }

        /// <summary>
        /// 의심도 설정
        /// </summary>
        public void SetSuspicion(float value)
        {
            _currentValue = Mathf.Clamp(value, 0f, 100f);

            CheckLevelChange();
            CheckDetected();
            CheckClear();
        }

        /// <summary>
        /// 상승 속도 설정
        /// </summary>
        public void SetIncreaseSpeed(float speed)
        {
            increaseSpeed = Mathf.Max(0f, speed);
        }

        /// <summary>
        /// 하락 속도 설정
        /// </summary>
        public void SetDecreaseSpeed(float speed)
        {
            decreaseSpeed = Mathf.Max(0f, speed);
        }

        /// <summary>
        /// 의태 상태 설정
        /// </summary>
        /// <param name="isCamouflaging">의태 중 여부</param>
        /// <param name="isPerfect">완벽 의태 여부</param>
        public void SetCamouflageState(bool isCamouflaging, bool isPerfect = false)
        {
            _isCamouflaging = isCamouflaging;
            _isPerfectCamouflage = isPerfect;
        }

        /// <summary>
        /// 레벨 계산
        /// </summary>
        private SuspicionLevel CalculateLevel(float value)
        {
            if (value >= detectedThreshold) return SuspicionLevel.Detected;
            if (value >= criticalThreshold) return SuspicionLevel.Critical;
            if (value >= dangerThreshold) return SuspicionLevel.Danger;
            if (value >= cautionThreshold) return SuspicionLevel.Caution;
            return SuspicionLevel.Safe;
        }

        /// <summary>
        /// 레벨 변경 확인
        /// </summary>
        private void CheckLevelChange()
        {
            SuspicionLevel newLevel = CalculateLevel(_currentValue);
            if (newLevel != _currentLevel)
            {
                _currentLevel = newLevel;
                OnLevelChanged?.Invoke(_currentLevel);
            }
        }

        /// <summary>
        /// 100% 도달 확인
        /// </summary>
        private void CheckDetected()
        {
            if (_currentValue >= detectedThreshold)
            {
                // 발각 상태로 진입
                if (!_wasDetected)
                {
                    _wasDetected = true;
                    _lastDetectedTime = Time.time;
                }
                OnDetected?.Invoke();
            }
        }

        /// <summary>
        /// 0% 복귀 확인
        /// </summary>
        private void CheckClear()
        {
            if (_currentValue <= 0f)
            {
                OnClear?.Invoke();
            }
        }

        /// <summary>
        /// 발각 후 추적 복귀 중인지 여부
        /// </summary>
        public bool IsInDetectedCooldown => _wasDetected && (Time.time - _lastDetectedTime < detectedStateDuration);

        /// <summary>
        /// 발각 후 추적 복귀 남은 시간
        /// </summary>
        public float DetectedCooldownRemaining => _wasDetected ? Mathf.Max(0f, detectedStateDuration - (Time.time - _lastDetectedTime)) : 0f;

        /// <summary>
        /// 발각 후 추적 복귀 강제 종료 (예: 플레이어 잡혔을 때)
        /// </summary>
        public void ResetDetectedCooldown()
        {
            _wasDetected = false;
            _lastDetectedTime = 0f;
        }
    }
}
