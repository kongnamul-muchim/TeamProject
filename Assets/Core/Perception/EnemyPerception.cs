using UnityEngine;
using System;
using System.Collections.Generic;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// Enemy 시야 감지 컴포넌트
    /// - ConeVisionSensor로 Player 감지
    /// - SuspicionManager에 감지 보고
    /// - 경보 상태 관리 및 공유
    /// </summary>
    public sealed class EnemyPerception : MonoBehaviour
    {
        [Header("시야 감지")]
        [SerializeField] private ConeVisionSensor visionSensor;

        [Header("의심도 설정")]
        [SerializeField] private float baseDetectionIntensity = 1f;
        [SerializeField] private float patrolDetectionMultiplier = 1.5f;
        [SerializeField] private float observeDetectionMultiplier = 1.0f;
        [SerializeField] private float guardDetectionMultiplier = 1.2f;

        [Header("그라데이션 기반 감지 배율")]
        [SerializeField] private float centerDetectionMultiplier = 1.2f;
        [SerializeField] private float edgeDetectionMultiplier = 0.2f;
        [SerializeField] private AnimationCurve distanceGradient = AnimationCurve.Linear(0f, 1f, 1f, 0.15f);

        [Header("근접 감지 설정")]
        [SerializeField] private float nearbyDistanceMultiplier = 1.5f;
        [SerializeField, Range(0f, 1f)] private float nearbyDetectionIntensity = 0.2f;
        [SerializeField, Range(0f, 1f)] private float nearbyMinIntensityFactor = 0.3f;

        [Header("AI 기억 설정")]
        [SerializeField] private float memoryDuration = 3f;
        [SerializeField, Range(0f, 1f)] private float memoryDetectionMultiplier = 0.4f;

        [Header("추적 설정")]
        [SerializeField] private float trackingThreshold = 0.5f;
        [SerializeField] private float trackingMemoryMultiplier = 1.5f;
        [SerializeField] private float lostTargetThreshold = 0.2f;

        // 상태
        private HashSet<GameObject> _currentlyDetectedTargets = new();
        private float _nearbyCooldown;
        private const float NEARBY_COOLDOWN_TIME = 0.5f;
        private bool _currentFrameInVision;

        // AI 기억 시스템
        private Vector3 _lastKnownTargetPosition;
        private float _lastSeenTime;
        private bool _hasLastKnownPosition;

        // AI 추적 시스템
        private EnemyAlertState _currentAlertState = EnemyAlertState.Idle;
        private EnemyAlertState _previousAlertState = EnemyAlertState.Idle;
        private float _trackingMemoryDuration;

        // 캐시
        private SuspicionManager _suspicionManager;

        // 이벤트
        public event Action<EnemyAlertState> OnAlertStateChanged;

        // 프로퍼티
        public EnemyAlertState CurrentAlertState => _currentAlertState;
        public EnemyAlertState PreviousAlertState => _previousAlertState;
        public bool IsTracking => _currentAlertState == EnemyAlertState.Tracking || _currentAlertState == EnemyAlertState.Alert;
        public Vector3 TrackingTargetPosition =>
            (_currentAlertState == EnemyAlertState.Tracking || _currentAlertState == EnemyAlertState.Alert)
                ? _lastKnownTargetPosition : Vector3.zero;
        public Vector3 LastKnownTargetPosition => _lastKnownTargetPosition;
        public bool IsRememberingTarget => _hasLastKnownPosition && (Time.time - _lastSeenTime < _trackingMemoryDuration);
        public float MemoryDuration => memoryDuration;
        public float TimeSinceLastSeen => _hasLastKnownPosition ? Time.time - _lastSeenTime : float.MaxValue;

        private void Awake()
        {
            _suspicionManager = SuspicionManager.Instance;
            _trackingMemoryDuration = memoryDuration;
        }

        private void OnEnable()
        {
            if (_suspicionManager != null)
            {
                _suspicionManager.RegisterEnemy(this);
            }
        }

        private void OnDisable()
        {
            if (_suspicionManager != null)
            {
                _suspicionManager.UnregisterEnemy(this);
            }
        }

        private void Update()
        {
            if (_suspicionManager == null) return;

            // 쿨다운 감소
            if (_nearbyCooldown > 0f) _nearbyCooldown -= Time.deltaTime;

            // 경계 상태 업데이트
            UpdateAlertState();

            // 시야 내 감지
            var visibleTargets = visionSensor != null ? visionSensor.GetAllVisibleTargets() : new List<GameObject>();
            _currentFrameInVision = visibleTargets.Count > 0;

            if (_currentFrameInVision)
            {
                // 가장 가까운 대상 기준 거리 계산
                GameObject nearestTarget = GetNearestTarget(visibleTargets);
                float distance = Vector3.Distance(transform.position, nearestTarget.transform.position);

                // 감지 강도 계산
                float intensity = CalculateDetectionIntensity(distance, true);

                // SuspicionManager에 보고
                _suspicionManager.ReportDetection(intensity);

                // 마지막으로 본 위치 기억
                _lastKnownTargetPosition = nearestTarget.transform.position;
                _lastSeenTime = Time.time;
                _hasLastKnownPosition = true;

                // 최근 감지된 대상 기억
                _currentlyDetectedTargets.Clear();
                foreach (var target in visibleTargets)
                {
                    _currentlyDetectedTargets.Add(target);
                }
            }
            else
            {
                // 시야에서 벗어남
                if (_suspicionManager.IsCamouflaging)
                {
                    // 의태 중에는 근처 감지 무시
                    _currentlyDetectedTargets.Clear();
                }
                else
                {
                    // 기억 기간 내인지 확인
                    float timeSinceLastSeen = Time.time - _lastSeenTime;
                    bool isInMemoryDuration = _hasLastKnownPosition && timeSinceLastSeen < memoryDuration;

                    if (isInMemoryDuration)
                    {
                        if (_nearbyCooldown <= 0f)
                        {
                            float intensity = CalculateMemoryIntensity();
                            if (intensity > 0f)
                            {
                                _suspicionManager.ReportDetection(intensity);
                                _nearbyCooldown = NEARBY_COOLDOWN_TIME;
                            }
                        }
                    }
                    else
                    {
                        if (_nearbyCooldown <= 0f && _currentlyDetectedTargets.Count > 0)
                        {
                            float intensity = CalculateNearbyIntensity();
                            if (intensity > 0f)
                            {
                                _suspicionManager.ReportDetection(intensity);
                                _nearbyCooldown = NEARBY_COOLDOWN_TIME;
                            }
                        }

                        if (timeSinceLastSeen >= memoryDuration)
                        {
                            _hasLastKnownPosition = false;
                        }
                    }
                }
            }
        }

        #region 감지 강도 계산

        private float CalculateDetectionIntensity(float distanceToTarget, bool isInVision)
        {
            float patternMultiplier = visionSensor != null ? visionSensor.PatternType switch
            {
                VisionPatternType.Patrol => patrolDetectionMultiplier,
                VisionPatternType.Observe => observeDetectionMultiplier,
                VisionPatternType.Guard => guardDetectionMultiplier,
                _ => 1f
            } : 1f;

            float distanceMultiplier = GetDistanceMultiplier(distanceToTarget);

            float camoMultiplier = GetCamouflageMultiplier();

            return baseDetectionIntensity * patternMultiplier * distanceMultiplier * camoMultiplier;
        }

        private float GetDistanceMultiplier(float distance)
        {
            float viewRadius = visionSensor != null ? visionSensor.ViewRadius : 5f;
            if (viewRadius <= 0f) return edgeDetectionMultiplier;

            float distanceRatio = Mathf.Clamp01(distance / viewRadius);
            float gradientValue = distanceGradient.Evaluate(distanceRatio);
            return Mathf.Lerp(centerDetectionMultiplier, edgeDetectionMultiplier, 1f - gradientValue);
        }

        private float GetCamouflageMultiplier()
        {
            if (_suspicionManager == null) return 1f;
            if (!_suspicionManager.IsCamouflaging) return 1f;
            if (_suspicionManager.IsPerfectCamouflage) return 0.3f;
            return 0.6f;
        }

        private float CalculateNearbyIntensity()
        {
            if (_currentlyDetectedTargets.Count == 0) return 0f;

            float nearestDistance = float.MaxValue;
            foreach (var target in _currentlyDetectedTargets)
            {
                if (target == null) continue;
                float dist = Vector3.Distance(transform.position, target.transform.position);
                if (dist < nearestDistance) nearestDistance = dist;
            }

            float nearbyDistance = (visionSensor != null ? visionSensor.ViewRadius : 5f) * nearbyDistanceMultiplier;
            if (nearestDistance <= nearbyDistance)
            {
                float distanceFactor = 1f - (nearestDistance / nearbyDistance);
                float minIntensity = nearbyDetectionIntensity * nearbyMinIntensityFactor;
                float maxIntensity = nearbyDetectionIntensity;
                return Mathf.Lerp(minIntensity, maxIntensity, distanceFactor);
            }

            return 0f;
        }

        private float CalculateMemoryIntensity()
        {
            if (!_hasLastKnownPosition) return 0f;

            float distanceToLastKnown = Vector3.Distance(transform.position, _lastKnownTargetPosition);
            float memoryRange = (visionSensor != null ? visionSensor.ViewRadius : 5f) * nearbyDistanceMultiplier;

            if (distanceToLastKnown <= memoryRange)
            {
                float distanceFactor = 1f - (distanceToLastKnown / memoryRange);
                float baseIntensity = nearbyDetectionIntensity * memoryDetectionMultiplier;
                float minIntensity = baseIntensity * nearbyMinIntensityFactor;
                float maxIntensity = baseIntensity;
                return Mathf.Lerp(minIntensity, maxIntensity, distanceFactor);
            }

            return 0f;
        }

        private GameObject GetNearestTarget(List<GameObject> targets)
        {
            GameObject nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var target in targets)
            {
                float dist = Vector3.Distance(transform.position, target.transform.position);
                if (dist < nearestDistance)
                {
                    nearestDistance = dist;
                    nearest = target;
                }
            }

            return nearest;
        }

        #endregion

        #region 경보 상태

        private void UpdateAlertState()
        {
            float suspicionNormalized = (_suspicionManager != null ? _suspicionManager.CurrentValue : 0f) / 100f;
            bool isInVision = _currentFrameInVision;
            float timeSinceLastSeen = _hasLastKnownPosition ? Time.time - _lastSeenTime : float.MaxValue;
            bool isInMemoryDuration = _hasLastKnownPosition && timeSinceLastSeen < _trackingMemoryDuration;

            EnemyAlertState newState = _currentAlertState;

            if (suspicionNormalized >= 1f)
            {
                newState = EnemyAlertState.Alert;
            }
            else if (isInVision)
            {
                newState = EnemyAlertState.Tracking;
            }
            else if (suspicionNormalized >= trackingThreshold)
            {
                newState = EnemyAlertState.Tracking;
            }
            else if (suspicionNormalized > 0f && suspicionNormalized < trackingThreshold)
            {
                newState = EnemyAlertState.Suspicious;
            }
            else if (suspicionNormalized <= 0f)
            {
                newState = EnemyAlertState.Idle;
            }

            if (newState != _currentAlertState)
            {
                _previousAlertState = _currentAlertState;
                _currentAlertState = newState;

                if (newState == EnemyAlertState.Tracking)
                {
                    _trackingMemoryDuration = memoryDuration * trackingMemoryMultiplier;
                }
                else
                {
                    _trackingMemoryDuration = memoryDuration;
                }

                // Alert 상태 진입 시 브로드캐스트
                if (newState == EnemyAlertState.Alert && _hasLastKnownPosition && _suspicionManager != null)
                {
                    _suspicionManager.BroadcastAlert(this, _lastKnownTargetPosition, suspicionNormalized);
                }

                OnAlertStateChanged?.Invoke(_currentAlertState);
            }
        }

        /// <summary>
        /// 다른 적으로부터 공유받은 Alert 정보 처리
        /// </summary>
        public void ReceiveSharedAlert(Vector3 alertPosition, float intensity)
        {
            if (!_hasLastKnownPosition || Vector3.Distance(alertPosition, _lastKnownTargetPosition) > 0.1f)
            {
                _lastKnownTargetPosition = alertPosition;
                _lastSeenTime = Time.time;
                _hasLastKnownPosition = true;
            }

            if (_suspicionManager != null)
            {
                _suspicionManager.ReportDetection(intensity);
            }

            if (_currentAlertState == EnemyAlertState.Idle || _currentAlertState == EnemyAlertState.Suspicious)
            {
                _currentAlertState = EnemyAlertState.Tracking;
                _trackingMemoryDuration = memoryDuration * trackingMemoryMultiplier;
                OnAlertStateChanged?.Invoke(_currentAlertState);
            }
        }

        #endregion
    }
}
