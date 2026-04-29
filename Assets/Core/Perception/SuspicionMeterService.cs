using System;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의심도 계산 순수 C# 서비스
    /// SuspicionManager의 의심도 수학 로직을 분리하여 DI 가능하게 함
    /// MonoBehaviour 의존성 없음 — 테스트 가능
    /// </summary>
    public sealed class SuspicionMeterService : ISuspicionMeter
    {
        // 임계값 (생성자 주입)
        private readonly float _cautionThreshold;
        private readonly float _dangerThreshold;
        private readonly float _criticalThreshold;
        private readonly float _detectedThreshold;
        private readonly float _camouflageReduceMultiplier;
        private readonly float _perfectCamouflageReducePerSec;

        // 상태
        private float _currentValue;
        private SuspicionLevel _currentLevel;
        private bool _isCamouflaging;
        private bool _isPerfectCamouflage;
        private bool _wasDetected;  // OnDetected 중복 발생 방지
        private float _increaseSpeed;
        private float _decreaseSpeed;

        // ===== ISuspicionMeter Properties =====

        public float CurrentValue => MathfClamp(_currentValue, 0f, 100f);
        public SuspicionLevel CurrentLevel => _currentLevel;

        /// <summary>
        /// 의태 중인지 여부
        /// </summary>
        public bool IsCamouflaging => _isCamouflaging;

        /// <summary>
        /// 완벽 의태 중인지 여부
        /// </summary>
        public bool IsPerfectCamouflage => _isPerfectCamouflage;

        /// <summary>
        /// 발각 후 쿨다운 — SuspicionManager의 Update() 타이머에서 관리
        /// 이 서비스에서는 항상 false 반환
        /// </summary>
        public bool IsInDetectedCooldown => false;

        // ===== Events =====

        public event Action<SuspicionLevel> OnLevelChanged;
        public event Action OnDetected;
        public event Action OnClear;

        // ===== Constructor =====

        /// <summary>
        /// 생성자 — 모든 설정값을 직접 주입받음
        /// (Phase 3c에서 Config 객체로 대체 예정)
        /// </summary>
        public SuspicionMeterService(
            float cautionThreshold,
            float dangerThreshold,
            float criticalThreshold,
            float detectedThreshold,
            float camouflageReduceMultiplier,
            float perfectCamouflageReducePerSec,
            float increaseSpeed,
            float decreaseSpeed)
        {
            _cautionThreshold = cautionThreshold;
            _dangerThreshold = dangerThreshold;
            _criticalThreshold = criticalThreshold;
            _detectedThreshold = detectedThreshold;
            _camouflageReduceMultiplier = camouflageReduceMultiplier;
            _perfectCamouflageReducePerSec = perfectCamouflageReducePerSec;
            _increaseSpeed = Mathf.Max(0f, increaseSpeed);
            _decreaseSpeed = Mathf.Max(0f, decreaseSpeed);

            _currentValue = 0f;
            _currentLevel = SuspicionLevel.Safe;
            _wasDetected = false;
        }

        // ===== ISuspicionMeter Methods =====

        public void AddSuspicion(float amount, float deltaTime)
        {
            _currentValue += amount * _increaseSpeed * deltaTime;
            _currentValue = MathfClamp(_currentValue, 0f, 100f);

            CheckLevelChange();
            CheckDetected();
        }

        public void ReduceSuspicion(float amount, float deltaTime)
        {
            float decreaseAmount = amount * _decreaseSpeed * deltaTime;

            if (_isCamouflaging)
            {
                decreaseAmount *= (1f + _camouflageReduceMultiplier);
            }

            if (_isPerfectCamouflage)
            {
                decreaseAmount += _perfectCamouflageReducePerSec * deltaTime;
            }

            _currentValue -= decreaseAmount;
            _currentValue = MathfClamp(_currentValue, 0f, 100f);

            CheckLevelChange();
            CheckClear();
        }

        public void Reset()
        {
            float previousValue = _currentValue;
            _currentValue = 0f;
            _currentLevel = SuspicionLevel.Safe;
            _wasDetected = false;

            if (previousValue > 0f)
            {
                OnClear?.Invoke();
            }
        }

        public void SetSuspicion(float value)
        {
            float prev = _currentValue;
            _currentValue = MathfClamp(value, 0f, 100f);
            CheckLevelChange();
            CheckDetected();
            CheckClear();
        }

        public void SetIncreaseSpeed(float speed)
        {
            _increaseSpeed = MathfMax(0f, speed);
        }

        public void SetDecreaseSpeed(float speed)
        {
            _decreaseSpeed = MathfMax(0f, speed);
        }

        public void SetCamouflageState(bool isCamouflaging, bool isPerfect = false)
        {
            _isCamouflaging = isCamouflaging;
            _isPerfectCamouflage = isPerfect;
        }

        /// <summary>
        /// 감지 대상 발견 시 의심도 증가 (인터페이스 명세용, 현재는 AddSuspicion과 동일)
        /// </summary>
        public void OnDetectedTarget(float detectionIntensity = 1f)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[SuspicionMeterService] OnDetectedTarget: {detectionIntensity}");
#endif
        }

        /// <summary>
        /// Detected 플래그 강제 클리어 (SuspicionManager.Update()에서 cooldown 만료 시 호출)
        /// </summary>
        public void ClearDetectedFlag()
        {
            _wasDetected = false;
        }

        /// <summary>
        /// 현재 값이 최소값보다 낮으면 최소값으로 설정 (cooldown 중 의심도 유지)
        /// </summary>
        public void ApplyMinimumValue(float minValue)
        {
            if (_currentValue < minValue)
            {
                _currentValue = minValue;
            }
        }

        // ===== Internal =====

        private SuspicionLevel CalculateLevel(float value)
        {
            if (value >= _detectedThreshold) return SuspicionLevel.Detected;
            if (value >= _criticalThreshold) return SuspicionLevel.Critical;
            if (value >= _dangerThreshold) return SuspicionLevel.Danger;
            if (value >= _cautionThreshold) return SuspicionLevel.Caution;
            return SuspicionLevel.Safe;
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

        private void CheckDetected()
        {
            if (_currentValue >= _detectedThreshold && !_wasDetected)
            {
                _wasDetected = true;
                OnDetected?.Invoke();
            }
        }

        private void CheckClear()
        {
            if (_currentValue <= 0f)
            {
                OnClear?.Invoke();
            }
        }

        // ===== Math helpers (Unity Mathf 대체) =====

        private static float MathfClamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static float MathfMax(float a, float b)
        {
            return a > b ? a : b;
        }
    }
}
