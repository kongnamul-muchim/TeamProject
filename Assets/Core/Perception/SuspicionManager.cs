using UnityEngine;
using System;
using System.Collections.Generic;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using HideAndInk.Core.Utilities;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 전역 의심도 관리 시스템 (MonoBehaviour Shell)
    /// - 의심도 계산: SuspicionMeterService에 위임
    /// - 적 간 경보 상태 공유
    /// - Update() 타이머 기반 의심도 감소 처리
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

        // === SuspicionMeterService — 의심도 계산 순수 C# 서비스 ===
        private SuspicionMeterService _meterService;

        // 이벤트 핸들러 참조 (구독 해제용)
        private Action<SuspicionLevel> _onLevelChangedHandler;
        private Action _onDetectedHandler;
        private Action _onClearHandler;

        // 발각 cooldown 타이머 상태 (MonoBehaviour 전용)
        private float _lastDetectionTime;
        private float _lastDetectedTime;
        private bool _wasDetected;  // cooldown 상태 추적 (Service의 _wasDetected와 별개)

        // === EnemyAlertCoordinator — 적 관리·경보 공유 순수 C# 서비스 ===
        private EnemyAlertCoordinator _alertCoordinator;

        // ===== Events =====

        /// <summary>
        /// Service의 OnLevelChanged를 외부로 전달
        /// </summary>
        public event Action<SuspicionLevel> OnLevelChanged;

        /// <summary>
        /// Service의 OnDetected를 외부로 전달 (+ cooldown 타이머 기록)
        /// </summary>
        public event Action OnDetected;

        /// <summary>
        /// Service의 OnClear를 외부로 전달
        /// </summary>
        public event Action OnClear;

        /// <summary>
        /// 의심도 값 변경 시 발생 (MonoBehaviour Update 타이머 기반)
        /// </summary>
        public event Action<float> OnSuspicionValueChanged;

        public event Action<EnemyPerception, Vector3, float> OnAlertBroadcast;

        // ===== Properties (Service 위임) =====

        public float CurrentValue => _meterService?.CurrentValue ?? 0f;
        public SuspicionLevel CurrentLevel => _meterService?.CurrentLevel ?? SuspicionLevel.Safe;
        public bool IsCamouflaging => _meterService?.IsCamouflaging ?? false;
        public bool IsPerfectCamouflage => _meterService?.IsPerfectCamouflage ?? false;
        public bool IsInDetectedCooldown => _wasDetected && (Time.time - _lastDetectedTime < detectedStateDuration);

        // 인스펙터 설정 직접 노출 (Coordinator 위임)
        public float AlertBroadcastRadius => _alertCoordinator?.AlertBroadcastRadius ?? alertBroadcastRadius;
        public float SharedSuspicionAmount => _alertCoordinator?.SharedSuspicionAmount ?? sharedSuspicionAmount;

        // ===== MonoBehaviour Lifecycle =====

        protected override void Awake()
        {
            base.Awake();

            // SuspicionMeterService 생성 (인스펙터 값을 그대로 전달)
            _meterService = new SuspicionMeterService(
                cautionThreshold,
                dangerThreshold,
                criticalThreshold,
                detectedThreshold,
                camouflageReduceMultiplier,
                perfectCamouflageReducePerSec,
                increaseSpeed,
                decreaseSpeed
            );

            // Service 이벤트 → SuspicionManager 이벤트로 포워딩 (명시적 핸들러 저장)
            _onLevelChangedHandler = level => OnLevelChanged?.Invoke(level);
            _onDetectedHandler = () => HandleServiceDetected();
            _onClearHandler = () => OnClear?.Invoke();

            _meterService.OnLevelChanged += _onLevelChangedHandler;
            _meterService.OnDetected += _onDetectedHandler;
            _meterService.OnClear += _onClearHandler;

            // EnemyAlertCoordinator 생성 (인스펙터 값을 그대로 전달)
            _alertCoordinator = new EnemyAlertCoordinator(
                alertBroadcastRadius,
                sharedSuspicionAmount,
                sharedSuspicionCooldown
            );

            // Coordinator 이벤트 포워딩
            _alertCoordinator.OnAlertBroadcast += (source, pos, intensity) =>
                OnAlertBroadcast?.Invoke(source, pos, intensity);

            // DI 컨테이너에 ISuspicionMeter로 등록 (외부 소비자용)
            if (GameManager.Container != null)
            {
                GameManager.Container.RegisterInstance<ISuspicionMeter>(_meterService);
            }

            ResetSuspicion();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_meterService != null)
            {
                if (_onLevelChangedHandler != null)
                    _meterService.OnLevelChanged -= _onLevelChangedHandler;
                if (_onDetectedHandler != null)
                    _meterService.OnDetected -= _onDetectedHandler;
                if (_onClearHandler != null)
                    _meterService.OnClear -= _onClearHandler;
            }
        }

        private void Update()
        {
            if (_meterService == null) return;

            // Grace period 감소
            _lastDetectionTime -= Time.deltaTime;

            // 발각 후 추적 복귀 체크
            bool isInDetectedCooldown = _wasDetected && (Time.time - _lastDetectedTime < detectedStateDuration);

            // 발각 상태에서 벗어남 → Service의 Detected 플래그도 함께 클리어
            if (_wasDetected && _meterService.CurrentValue < detectedThreshold)
            {
                _wasDetected = false;
                _meterService.ClearDetectedFlag();
            }

            // 의심도 하락 처리
            if (isInDetectedCooldown && _meterService.CurrentValue > minSuspicionAfterDetected)
            {
                _meterService.ReduceSuspicion(1f, Time.deltaTime);
                _meterService.ApplyMinimumValue(minSuspicionAfterDetected);
            }
            else if (_lastDetectionTime <= 0f && _meterService.CurrentValue > 0f)
            {
                _meterService.ReduceSuspicion(1f, Time.deltaTime);
            }

            OnSuspicionValueChanged?.Invoke(_meterService.CurrentValue);
        }

        /// <summary>
        /// Service의 OnDetected 이벤트 핸들러
        /// </summary>
        private void HandleServiceDetected()
        {
            _lastDetectedTime = Time.time;
            OnDetected?.Invoke();
        }

        // ===== 의심도 관리 (Service 위임) =====

        /// <summary>
        /// 감지 보고 받음 (EnemyPerception에서 호출)
        /// </summary>
        public void ReportDetection(float detectionIntensity = 1f)
        {
            if (_meterService == null) return;
            _lastDetectionTime = detectionGracePeriod;
            _meterService.AddSuspicion(detectionIntensity, Time.deltaTime);
        }

        /// <summary>
        /// 의심도 증가
        /// </summary>
        public void AddSuspicion(float amount, float deltaTime)
        {
            _meterService?.AddSuspicion(amount, deltaTime);
        }

        /// <summary>
        /// 의심도 감소
        /// </summary>
        public void ReduceSuspicion(float amount, float deltaTime)
        {
            _meterService?.ReduceSuspicion(amount, deltaTime);
        }

        /// <summary>
        /// 의심도 리셋 (0, Safe)
        /// </summary>
        public void ResetSuspicion()
        {
            _meterService?.Reset();
            _wasDetected = false;
            _lastDetectedTime = 0f;
        }

        /// <summary>
        /// 의심도 직접 설정 (private) — Service에 위임
        /// </summary>
        private void SetSuspicion(float value)
        {
            _meterService?.SetSuspicion(value);
        }

        /// <summary>
        /// 상승 속도 설정
        /// </summary>
        public void SetIncreaseSpeed(float speed)
        {
            _meterService?.SetIncreaseSpeed(speed);
        }

        /// <summary>
        /// 하락 속도 설정
        /// </summary>
        public void SetDecreaseSpeed(float speed)
        {
            _meterService?.SetDecreaseSpeed(speed);
        }

        /// <summary>
        /// 의태 상태 설정
        /// </summary>
        public void SetCamouflageState(bool isCamouflaging, bool isPerfect = false)
        {
            _meterService?.SetCamouflageState(isCamouflaging, isPerfect);
        }

        /// <summary>
        /// 발각 cooldown 리셋
        /// </summary>
        public void ResetDetectedCooldown()
        {
            _wasDetected = false;
            _lastDetectedTime = 0f;
        }

        #region 경보 공유 (Coordinator 위임)

        /// <summary>
        /// EnemyPerception 등록
        /// </summary>
        public void RegisterEnemy(EnemyPerception enemy)
        {
            _alertCoordinator?.RegisterEnemy(enemy);
        }

        /// <summary>
        /// EnemyPerception 등록 해제
        /// </summary>
        public void UnregisterEnemy(EnemyPerception enemy)
        {
            _alertCoordinator?.UnregisterEnemy(enemy);
        }

        /// <summary>
        /// 경보 브로드캐스트 (발각 시 다른 적들에게 공유)
        /// </summary>
        public void BroadcastAlert(EnemyPerception sourceEnemy, Vector3 alertPosition, float alertIntensity)
        {
            _alertCoordinator?.BroadcastAlert(sourceEnemy, alertPosition, alertIntensity);
        }

        #endregion
    }
}
