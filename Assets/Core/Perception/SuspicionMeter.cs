using UnityEngine;
using System;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의심도 시스템 구현체
    /// </summary>
    public sealed class SuspicionMeter : ISuspicionMeter
    {
        // 임계값
        private const float CAUTION_THRESHOLD = 30f;
        private const float DANGER_THRESHOLD = 60f;
        private const float CRITICAL_THRESHOLD = 80f;
        private const float DETECTED_THRESHOLD = 100f;

        // 기본 상승/하락 속도 (초당)
        private const float DEFAULT_INCREASE_SPEED = 20f;
        private const float DEFAULT_DECREASE_SPEED = 10f;

        // 의태 시 하락 속도 보정치
        private const float CAMOUFLAGE_REDUCE_MULTIPLIER = 0.5f;  // 50% 감소
        private const float PERFECT_CAMOUFLAGE_REDUCE_PER_SEC = 6f; // -6%/초

        // 현재 의심도 값
        private float _currentValue;
        private float _increaseSpeed;
        private float _decreaseSpeed;

        // 상태
        private bool _isCamouflaging;
        private bool _isPerfectCamouflage;
        private SuspicionLevel _currentLevel;

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
        /// 생성자
        /// </summary>
        /// <param name="initialValue">초기값 (기본: 0)</param>
        public SuspicionMeter(float initialValue = 0f)
        {
            _currentValue = Mathf.Clamp(initialValue, 0f, 100f);
            _increaseSpeed = DEFAULT_INCREASE_SPEED;
            _decreaseSpeed = DEFAULT_DECREASE_SPEED;
            _isCamouflaging = false;
            _isPerfectCamouflage = false;
            _currentLevel = CalculateLevel(_currentValue);
        }

        /// <summary>
        /// 의심도 상승
        /// </summary>
        /// <param name="amount">상승량 (초당)</param>
        public void AddSuspicion(float amount)
        {
            float previousValue = _currentValue;
            _currentValue += amount * _increaseSpeed * Time.deltaTime;
            _currentValue = Mathf.Clamp(_currentValue, 0f, 100f);

            CheckLevelChange();
            CheckDetected();
        }

        /// <summary>
        /// 의심도 하락
        /// </summary>
        /// <param name="amount">하락량 (초당)</param>
        public void ReduceSuspicion(float amount)
        {
            float decreaseAmount = amount * _decreaseSpeed * Time.deltaTime;

            // 의태 중이면 추가 하락 적용
            if (_isCamouflaging)
            {
                decreaseAmount *= (1f + CAMOUFLAGE_REDUCE_MULTIPLIER);
            }

            // 완벽 의태면 추가 하락
            if (_isPerfectCamouflage)
            {
                decreaseAmount += PERFECT_CAMOUFLAGE_REDUCE_PER_SEC * Time.deltaTime;
            }

            float previousValue = _currentValue;
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
            float previousValue = _currentValue;
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
            _increaseSpeed = Mathf.Max(0f, speed);
        }

        /// <summary>
        /// 하락 속도 설정
        /// </summary>
        public void SetDecreaseSpeed(float speed)
        {
            _decreaseSpeed = Mathf.Max(0f, speed);
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
            if (value >= DETECTED_THRESHOLD) return SuspicionLevel.Detected;
            if (value >= CRITICAL_THRESHOLD) return SuspicionLevel.Critical;
            if (value >= DANGER_THRESHOLD) return SuspicionLevel.Danger;
            if (value >= CAUTION_THRESHOLD) return SuspicionLevel.Caution;
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
            if (_currentValue >= DETECTED_THRESHOLD && OnDetected != null)
            {
                OnDetected.Invoke();
            }
        }

        /// <summary>
        /// 0% 복귀 확인
        /// </summary>
        private void CheckClear()
        {
            if (_currentValue <= 0f && OnClear != null)
            {
                OnClear.Invoke();
            }
        }

        /// <summary>
        /// 매 프레임 업데이트 (Time.deltaTime 자동 적용)
        /// </summary>
        public void Update()
        {
            if (_currentValue > 0f)
            {
                // 감지된 것이 없으면 자연 하락
                ReduceSuspicion(1f);
            }
        }

        /// <summary>
        /// 감지 시 호출 (상승)
        /// </summary>
        /// <param name="detectionIntensity">감지 강도 (0~1, 클수록 빠르게 상승)</param>
        public void OnDetectedTarget(float detectionIntensity = 1f)
        {
            AddSuspicion(detectionIntensity);
        }
    }
}
