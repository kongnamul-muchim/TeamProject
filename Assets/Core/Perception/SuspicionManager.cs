using UnityEngine;
using System;
using System.Collections.Generic;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Utilities;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 전역 의심도 관리 시스템
    /// - Player 의심도 계산/관리
    /// - 적 간 경보 상태 공유
    /// - UI/게임 상태 연동 이벤트 제공
    /// </summary>
    public sealed class SuspicionManager : Singleton<SuspicionManager>
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
        [SerializeField] private float detectedStateDuration = 2f;
        [SerializeField] private float minSuspicionAfterDetected = 0.3f;

        [Header("경보 공유 설정")]
        [SerializeField] private float alertBroadcastRadius = 10f;
        [SerializeField] private float sharedSuspicionAmount = 0.3f;
        [SerializeField] private float sharedSuspicionCooldown = 2f;

        // 의심도 상태
        private float _currentValue;
        private SuspicionLevel _currentLevel;
        private bool _isCamouflaging;
        private bool _isPerfectCamouflage;
        private float _lastDetectionTime;
        private float _lastDetectedTime;
        private bool _wasDetected;

        // 경보 공유
        private List<EnemyPerception> _registeredEnemies = new();
        private Dictionary<GameObject, float> _lastBroadcastTime = new();

        // 이벤트
        public event Action<SuspicionLevel> OnLevelChanged;
        public event Action OnDetected;
        public event Action OnClear;
        public event Action<float> OnSuspicionValueChanged;
        public event Action<EnemyPerception, Vector3, float> OnAlertBroadcast;

        // 프로퍼티
        public float CurrentValue => Mathf.Clamp(_currentValue, 0f, 100f);
        public SuspicionLevel CurrentLevel => _currentLevel;
        public bool IsCamouflaging => _isCamouflaging;
        public bool IsPerfectCamouflage => _isPerfectCamouflage;
        public bool IsInDetectedCooldown => _wasDetected && (Time.time - _lastDetectedTime < detectedStateDuration);
        public float AlertBroadcastRadius => alertBroadcastRadius;
        public float SharedSuspicionAmount => sharedSuspicionAmount;

        protected override void Awake()
        {
            base.Awake();
            ResetSuspicion();
        }

        private void Update()
        {
            // Grace period 감소
            _lastDetectionTime -= Time.deltaTime;

            // 발각 후 추적 복귀 체크
            bool isInDetectedCooldown = _wasDetected && (Time.time - _lastDetectedTime < detectedStateDuration);

            // 발각 상태에서 벗어남
            if (_wasDetected && _currentValue < detectedThreshold)
            {
                _wasDetected = false;
            }

            // 의심도 하락 처리
            if (isInDetectedCooldown && _currentValue > minSuspicionAfterDetected)
            {
                ReduceSuspicion(1f, Time.deltaTime);
                _currentValue = Mathf.Max(_currentValue, minSuspicionAfterDetected);
            }
            else if (_lastDetectionTime <= 0f && _currentValue > 0f)
            {
                ReduceSuspicion(1f, Time.deltaTime);
            }

            OnSuspicionValueChanged?.Invoke(CurrentValue);
        }

        #region 의심도 관리

        /// <summary>
        /// 감지 보고 받음 (EnemyPerception에서 호출)
        /// </summary>
        public void ReportDetection(float detectionIntensity = 1f)
        {
            _lastDetectionTime = detectionGracePeriod;
            AddSuspicion(detectionIntensity, Time.deltaTime);
        }

        public void AddSuspicion(float amount, float deltaTime)
        {
            _currentValue += amount * increaseSpeed * deltaTime;
            _currentValue = Mathf.Clamp(_currentValue, 0f, 100f);

            CheckLevelChange();
            CheckDetected();
        }

        public void ReduceSuspicion(float amount, float deltaTime)
        {
            float decreaseAmount = amount * decreaseSpeed * deltaTime;

            if (_isCamouflaging)
            {
                decreaseAmount *= (1f + camouflageReduceMultiplier);
            }

            if (_isPerfectCamouflage)
            {
                decreaseAmount += perfectCamouflageReducePerSec * deltaTime;
            }

            _currentValue -= decreaseAmount;
            _currentValue = Mathf.Clamp(_currentValue, 0f, 100f);

            CheckLevelChange();
            CheckClear();
        }

        public void ResetSuspicion()
        {
            float previousValue = _currentValue;
            _currentValue = 0f;
            _currentLevel = SuspicionLevel.Safe;
            _wasDetected = false;
            _lastDetectedTime = 0f;

            if (previousValue > 0f)
            {
                OnClear?.Invoke();
            }
        }

        private void SetSuspicion(float value)
        {
            float prev = _currentValue;
            _currentValue = Mathf.Clamp(value, 0f, 100f);
            CheckLevelChange();
            CheckDetected();
            CheckClear();

#if UNITY_EDITOR
            if (Mathf.Abs(_currentValue - prev) > 1f)
                Debug.LogWarning($"[SuspicionManager] SetSuspicion: {prev:F1} → {_currentValue:F1} (점프 발생)");
#endif
        }

        public void SetIncreaseSpeed(float speed) => increaseSpeed = Mathf.Max(0f, speed);
        public void SetDecreaseSpeed(float speed) => decreaseSpeed = Mathf.Max(0f, speed);

        public void SetCamouflageState(bool isCamouflaging, bool isPerfect = false)
        {
            _isCamouflaging = isCamouflaging;
            _isPerfectCamouflage = isPerfect;
        }

        public void ResetDetectedCooldown()
        {
            _wasDetected = false;
            _lastDetectedTime = 0f;
        }

        private SuspicionLevel CalculateLevel(float value)
        {
            if (value >= detectedThreshold) return SuspicionLevel.Detected;
            if (value >= criticalThreshold) return SuspicionLevel.Critical;
            if (value >= dangerThreshold) return SuspicionLevel.Danger;
            if (value >= cautionThreshold) return SuspicionLevel.Caution;
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
            if (_currentValue >= detectedThreshold && !_wasDetected)
            {
                _wasDetected = true;
                _lastDetectedTime = Time.time;
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

        #endregion

        #region 경보 공유

        /// <summary>
        /// EnemyPerception 등록
        /// </summary>
        public void RegisterEnemy(EnemyPerception enemy)
        {
            if (!_registeredEnemies.Contains(enemy))
            {
                _registeredEnemies.Add(enemy);
            }
        }

        /// <summary>
        /// EnemyPerception 등록 해제
        /// </summary>
        public void UnregisterEnemy(EnemyPerception enemy)
        {
            _registeredEnemies.Remove(enemy);
        }

        /// <summary>
        /// 경보 브로드캐스트 (발각 시 다른 적들에게 공유)
        /// </summary>
        public void BroadcastAlert(EnemyPerception sourceEnemy, Vector3 alertPosition, float alertIntensity)
        {
            if (sourceEnemy == null) return;

            float currentTime = Time.time;
            if (_lastBroadcastTime.TryGetValue(sourceEnemy.gameObject, out float lastTime))
            {
                if (currentTime - lastTime < sharedSuspicionCooldown) return;
            }
            _lastBroadcastTime[sourceEnemy.gameObject] = currentTime;

            foreach (var enemy in _registeredEnemies)
            {
                if (enemy == null || enemy == sourceEnemy) continue;

                float distance = Vector3.Distance(enemy.transform.position, sourceEnemy.transform.position);
                if (distance <= alertBroadcastRadius)
                {
                    float distanceFactor = 1f - (distance / alertBroadcastRadius);
                    float sharedIntensity = sharedSuspicionAmount * distanceFactor * alertIntensity;
                    enemy.ReceiveSharedAlert(alertPosition, sharedIntensity);
                }
            }

            OnAlertBroadcast?.Invoke(sourceEnemy, alertPosition, alertIntensity);
        }

        #endregion
    }
}
