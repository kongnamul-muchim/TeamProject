using UnityEngine;
using System;
using System.Collections.Generic;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 적 AI 시야 감지 + 의심도 연동 관리자
    /// VisionSensor로 Player 감지 → SuspicionMeter 상승
    /// </summary>
    public sealed class VisionBasedSuspicionManager : MonoBehaviour
    {
        [Header("시야 감지")]
        [SerializeField] private ConeVisionSensor visionSensor;

        [Header("의심도 설정")]
        [SerializeField] private float baseDetectionIntensity = 1f;  // 기본 감지 강도
        [SerializeField] private float patrolDetectionMultiplier = 1.5f;  // 순찰형 감지 배율
        [SerializeField] private float observeDetectionMultiplier = 1.0f;  // 관찰형 감지 배율
        [SerializeField] private float guardDetectionMultiplier = 1.2f;  // 경계형 감지 배율

        [Header("거리 기반 감지 배율")]
        [SerializeField] private float closeRangeDistance = 2f;   // 가까이 범위 (2m 이내)
        [SerializeField] private float closeRangeMultiplier = 2f;   // 가까이 배율
        [SerializeField] private float midRangeDistance = 4f;      // 중간 범위 (4m 이내)
        [SerializeField] private float midRangeMultiplier = 1.5f; // 중간 배율
        [SerializeField] private float farRangeMultiplier = 1f;    // 먼 거리 배율
        [SerializeField] private float veryFarRangeMultiplier = 0.75f; // 매우 먼 거리 배율

        [Header("의태 감지 배율")]
        [SerializeField] private float normalDetectionMultiplier = 1f;    // 미의태
        [SerializeField] private float partialCamouflageMultiplier = 0.6f; // Partial 의태
        [SerializeField] private float perfectCamouflageMultiplier = 0.3f; // Perfect 의태

        [Header("근접 감지 설정")]
        [SerializeField] private float nearbyDistanceMultiplier = 1.5f; // 시야 반경의 1.5배
        [SerializeField] [Range(0f, 1f)] private float nearbyDetectionIntensity = 0.2f;  // 근접 시 증가량 (너무 강하면 하락이 따라잡지 못함)
        [SerializeField] [Range(0f, 1f)] private float nearbyMinIntensityFactor = 0.3f; // 근접 시 최소 강도 배율

        [Header("AI 기억 설정")]
        [SerializeField] private float memoryDuration = 3f;  // 마지막으로 보고 나서 기억하는 시간
        [SerializeField] [Range(0f, 1f)] private float memoryDetectionMultiplier = 0.4f;  // 기억 중일 때 감지 강도 배율

        [Header("추적 설정")]
        [SerializeField] private float trackingThreshold = 0.5f;  // 추적 모드 진입 의심도 임계값 (0~1, 50%)
        [SerializeField] private float trackingMemoryMultiplier = 1.5f;  // 추적 모드 시 기억 시간 배율
        [SerializeField] private float lostTargetThreshold = 0.2f;  // 추적 포기 의심도 임계값

        [Header("연동할 의심도 계량기 (Player의 SuspicionMeter)")]
        [SerializeField] private SuspicionMeter suspicionMeter;

        // 상태
        private HashSet<GameObject> _currentlyDetectedTargets;
        private bool _wasInVision;  // 이전 프레임에서 시야에 있었는지
        private float _nearbyCooldown;  // 근처 감지 쿨다운
        private const float NEARBY_COOLDOWN_TIME = 0.5f;  // 0.5초 쿨다운

        // AI 기억 시스템
        private Vector3 _lastKnownTargetPosition;  // 마지막으로 본 플레이어 위치
        private float _lastSeenTime;  // 마지막으로 본 시점
        private bool _hasLastKnownPosition;  // 기억이 있는지 여부

        // AI 추적 시스템
        private EnemyAlertState _currentAlertState;  // 현재 적의 경계 상태
        private EnemyAlertState _previousAlertState;  // 이전 상태 (변화 감지용)
        private float _trackingMemoryDuration;  // 추적 모드 시 기억 시간 (배율 적용)

        // 이벤트
        public event Action<EnemyAlertState> OnAlertStateChanged;

        private void Awake()
        {
            _currentlyDetectedTargets = new HashSet<GameObject>();
            _wasInVision = false;
            _nearbyCooldown = 0f;
            _lastKnownTargetPosition = Vector3.zero;
            _lastSeenTime = 0f;
            _hasLastKnownPosition = false;
            _currentAlertState = EnemyAlertState.Idle;
            _previousAlertState = EnemyAlertState.Idle;
            _trackingMemoryDuration = memoryDuration;
        }

        private void OnEnable()
        {
            // Coordinator에 등록
            if (SuspicionCoordinator.Instance != null)
            {
                SuspicionCoordinator.Instance.RegisterEnemy(this);
            }
        }

        private void OnDisable()
        {
            // Coordinator에서 해제
            if (SuspicionCoordinator.Instance != null)
            {
                SuspicionCoordinator.Instance.UnregisterEnemy(this);
            }
        }

        private void Update()
        {
            if (suspicionMeter == null) return;

            // 쿨다운 감소
            if (_nearbyCooldown > 0f)
                _nearbyCooldown -= Time.deltaTime;

            // 0. 경계 상태 업데이트
            UpdateAlertState();

            // 시야 내 감지된 대상 확인
            var visibleTargets = visionSensor.GetAllVisibleTargets();
            bool isInVision = visibleTargets.Count > 0;

            // 1. 시야 내 감지 → 의심도 증가
            if (visibleTargets.Count > 0)
            {
                // 가장 가까운 대상 기준 거리 계산
                GameObject nearestTarget = GetNearestTarget(visibleTargets);
                float distance = Vector3.Distance(transform.position, nearestTarget.transform.position);

                // 감지 강도 계산
                float intensity = CalculateDetectionIntensity(distance, isInVision);

                suspicionMeter.OnDetectedTarget(intensity);
                LogModule.Instance.Log($"Target in view, distance={distance:F1}, intensity={intensity:F2}", "INFO");

                // 마지막으로 본 위치 기억 업데이트
                _lastKnownTargetPosition = nearestTarget.transform.position;
                _lastSeenTime = Time.time;
                _hasLastKnownPosition = true;

                // 최근 감지된 대상 기억 (근처 감지용)
                _currentlyDetectedTargets.Clear();
                foreach (var target in visibleTargets)
                {
                    _currentlyDetectedTargets.Add(target);
                }
            }
            // 2. 시야에서 벗어남 → 기억/근처 체크
            else
            {
                // 의태 중이면 근처에서도 감지 안 한 걸로 처리 (의심도 하락 허용)
                if (suspicionMeter.IsCamouflaging)
                {
                    // 의태 중에는 근처 감지 무시 → 자연 하락 ↑
                    LogModule.Instance.Log("Target camouflaged and out of vision, allowing suspicion decay", "INFO");
                    _currentlyDetectedTargets.Clear();
                }
                // 의태가 아닌 경우
                else
                {
                    // 기억 기간 내인지 확인 (기억 중이면 마지막 위치 기준 감지)
                    float timeSinceLastSeen = Time.time - _lastSeenTime;
                    bool isInMemoryDuration = _hasLastKnownPosition && timeSinceLastSeen < memoryDuration;

                    if (isInMemoryDuration)
                    {
                        // 기억 중: 마지막으로 본 위치 기준 근처 감지
                        if (_nearbyCooldown <= 0f)
                        {
                            float intensity = CalculateMemoryIntensity();

                            if (intensity > 0f)
                            {
                                suspicionMeter.OnDetectedTarget(intensity);
                                LogModule.Instance.Log($"Target in memory, distance={Vector3.Distance(transform.position, _lastKnownTargetPosition):F1}, intensity={intensity:F2}", "INFO");
                                _nearbyCooldown = NEARBY_COOLDOWN_TIME;
                            }
                        }
                    }
                    else
                    {
                        // 기억 만료: 일반 근처 감지 (기존 로직)
                        if (_nearbyCooldown <= 0f && _currentlyDetectedTargets.Count > 0)
                        {
                            float intensity = CalculateNearbyIntensity();

                            if (intensity > 0f)
                            {
                                suspicionMeter.OnDetectedTarget(intensity);
                                LogModule.Instance.Log($"Target left vision but nearby, intensity={intensity:F2}", "INFO");
                                _nearbyCooldown = NEARBY_COOLDOWN_TIME;
                            }
                        }

                        // 기억 만료된 경우 기억 초기화
                        if (timeSinceLastSeen >= memoryDuration)
                        {
                            _hasLastKnownPosition = false;
                        }
                    }
                }
            }

            _wasInVision = isInVision;
        }

        /// <summary>
        /// 감지 강도 계산 (패턴별 + 거리별 + 의태 상태)
        /// </summary>
        private float CalculateDetectionIntensity(float distanceToTarget, bool isInVision)
        {
            // 1. 패턴 배율
            float patternMultiplier = visionSensor.PatternType switch
            {
                VisionPatternType.Patrol => patrolDetectionMultiplier,
                VisionPatternType.Observe => observeDetectionMultiplier,
                VisionPatternType.Guard => guardDetectionMultiplier,
                _ => 1f
            };

            // 2. 거리 배율
            float distanceMultiplier = GetDistanceMultiplier(distanceToTarget);

            // 3. 의태 상태 배율
            float camouflageMultiplier = GetCamouflageMultiplier();

            return baseDetectionIntensity * patternMultiplier * distanceMultiplier * camouflageMultiplier;
        }

        /// <summary>
        /// 거리 기반 배율 계산
        /// </summary>
        private float GetDistanceMultiplier(float distance)
        {
            if (distance <= closeRangeDistance)
                return closeRangeMultiplier;
            if (distance <= midRangeDistance)
                return midRangeMultiplier;
            if (distance <= visionSensor.ViewRadius)
                return farRangeMultiplier;
            return veryFarRangeMultiplier;
        }

        /// <summary>
        /// 의태 상태 기반 배율 계산
        /// </summary>
        private float GetCamouflageMultiplier()
        {
            if (!suspicionMeter.IsCamouflaging)
                return normalDetectionMultiplier;

            if (suspicionMeter.IsPerfectCamouflage)
                return perfectCamouflageMultiplier;

            return partialCamouflageMultiplier;
        }

        /// <summary>
        /// 근처 감지 시 감지 강도 계산
        /// </summary>
        private float CalculateNearbyIntensity()
        {
            // 감지된 적과의 거리 체크
            if (_currentlyDetectedTargets.Count == 0)
                return 0f;

            float nearestDistance = float.MaxValue;
            GameObject nearestTarget = null;
            foreach (var target in _currentlyDetectedTargets)
            {
                if (target == null) continue;
                float dist = Vector3.Distance(transform.position, target.transform.position);
                if (dist < nearestDistance)
                {
                    nearestDistance = dist;
                    nearestTarget = target;
                }
            }

            if (nearestTarget == null)
                return 0f;

            // 근처 범위 내에 있으면 소폭 증가
            float nearbyDistance = visionSensor.ViewRadius * nearbyDistanceMultiplier;
            if (nearestDistance <= nearbyDistance)
            {
                // 거리에 반비례하여 강도 조절 (가까울수록 강함)
                float distanceFactor = 1f - (nearestDistance / nearbyDistance);
                float minIntensity = nearbyDetectionIntensity * nearbyMinIntensityFactor;
                float maxIntensity = nearbyDetectionIntensity;
                return Mathf.Lerp(minIntensity, maxIntensity, distanceFactor);
            }

            return 0f;
        }

        /// <summary>
        /// 기억 기반 감지 시 감지 강도 계산 (마지막으로 본 위치 기준)
        /// </summary>
        private float CalculateMemoryIntensity()
        {
            if (!_hasLastKnownPosition)
                return 0f;

            float distanceToLastKnown = Vector3.Distance(transform.position, _lastKnownTargetPosition);
            float memoryRange = visionSensor.ViewRadius * nearbyDistanceMultiplier;

            // 기억 범위 내인지 확인
            if (distanceToLastKnown <= memoryRange)
            {
                // 거리에 반비례하여 강도 조절
                float distanceFactor = 1f - (distanceToLastKnown / memoryRange);
                float baseIntensity = nearbyDetectionIntensity * memoryDetectionMultiplier;
                float minIntensity = baseIntensity * nearbyMinIntensityFactor;
                float maxIntensity = baseIntensity;
                return Mathf.Lerp(minIntensity, maxIntensity, distanceFactor);
            }

            return 0f;
        }

        /// <summary>
        /// 가장 가까운 대상 찾기
        /// </summary>
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

        /// <summary>
        /// 경계 상태 업데이트
        /// </summary>
        private void UpdateAlertState()
        {
            float suspicionNormalized = suspicionMeter.CurrentValue / 100f;  // 0~1로 정규화
            var visibleTargets = visionSensor.GetAllVisibleTargets();
            bool isInVision = visibleTargets.Count > 0;
            float timeSinceLastSeen = _hasLastKnownPosition ? Time.time - _lastSeenTime : float.MaxValue;
            bool isInMemoryDuration = _hasLastKnownPosition && timeSinceLastSeen < _trackingMemoryDuration;

            // 상태 전이 로직
            EnemyAlertState newState = _currentAlertState;

            if (suspicionNormalized >= 1f)
            {
                // 100% 발각
                newState = EnemyAlertState.Alert;
            }
            else if (isInVision)
            {
                // 시야内有 → 즉시 추적
                newState = EnemyAlertState.Tracking;
            }
            else if (suspicionNormalized >= trackingThreshold)
            {
                // 의심도가 높음 + 시야外 → 추적 모드
                newState = EnemyAlertState.Tracking;
            }
            else if (suspicionNormalized > 0f && suspicionNormalized < trackingThreshold)
            {
                // 의심도 있음 but 추적 아니함 → 의심 상태
                newState = EnemyAlertState.Suspicious;
            }
            else if (suspicionNormalized <= 0f)
            {
                // 의심도 0 → 평소
                newState = EnemyAlertState.Idle;
            }

            // 상태가 변했을 때
            if (newState != _currentAlertState)
            {
                _previousAlertState = _currentAlertState;
                _currentAlertState = newState;

                // 추적 모드 진입 시 기억 시간 연장
                if (newState == EnemyAlertState.Tracking)
                {
                    _trackingMemoryDuration = memoryDuration * trackingMemoryMultiplier;
                }
                else
                {
                    _trackingMemoryDuration = memoryDuration;
                }

                // Alert 상태 진입 시 다른 적들에게 브로드캐스트
                if (newState == EnemyAlertState.Alert && _hasLastKnownPosition)
                {
                    BroadcastAlertToOthers();
                }

                LogModule.Instance.Log($"AlertState changed: {_previousAlertState} -> {_currentAlertState}", "INFO");
                OnAlertStateChanged?.Invoke(_currentAlertState);
            }
        }

        /// <summary>
        /// 의심도 계량기 설정 (외부에서 호출)
        /// </summary>
        public void SetSuspicionMeter(SuspicionMeter meter)
        {
            suspicionMeter = meter;
        }

        /// <summary>
        /// 현재 감지 중인 대상 수
        /// </summary>
        public int CurrentlyDetectedCount => _currentlyDetectedTargets.Count;

        /// <summary>
        /// 마지막으로 본 플레이어 위치 (기억 중이 아닐 경우 의미 없음)
        /// </summary>
        public Vector3 LastKnownTargetPosition => _lastKnownTargetPosition;

        /// <summary>
        /// 플레이어를 기억 중인지 여부
        /// </summary>
        public bool IsRememberingTarget => _hasLastKnownPosition && (Time.time - _lastSeenTime < _trackingMemoryDuration);

        /// <summary>
        /// 기억 지속 시간 (Inspector용, 외부 참조 가능)
        /// </summary>
        public float MemoryDuration => memoryDuration;

        /// <summary>
        /// 마지막 감지 후 경과 시간
        /// </summary>
        public float TimeSinceLastSeen => _hasLastKnownPosition ? Time.time - _lastSeenTime : float.MaxValue;

        /// <summary>
        /// 현재 적의 경계 상태
        /// </summary>
        public EnemyAlertState CurrentAlertState => _currentAlertState;

        /// <summary>
        /// 이전 경계 상태
        /// </summary>
        public EnemyAlertState PreviousAlertState => _previousAlertState;

        /// <summary>
        /// 추적 중인지 여부 (외부 AI가 이 값으로 이동 결정)
        /// </summary>
        public bool IsTracking => _currentAlertState == EnemyAlertState.Tracking || _currentAlertState == EnemyAlertState.Alert;

        /// <summary>
        /// 추적 대상 위치 (추적 중이 아닐 경우 마지막 기억 위치)
        /// </summary>
        public Vector3 TrackingTargetPosition => _currentAlertState == EnemyAlertState.Tracking || _currentAlertState == EnemyAlertState.Alert
            ? _lastKnownTargetPosition
            : Vector3.zero;

        /// <summary>
        /// 다른 적들에게 Alert 정보 브로드캐스트
        /// </summary>
        private void BroadcastAlertToOthers()
        {
            if (SuspicionCoordinator.Instance != null && _hasLastKnownPosition)
            {
                float alertIntensity = suspicionMeter != null ? suspicionMeter.CurrentValue / 100f : 1f;
                SuspicionCoordinator.Instance.BroadcastAlert(this, _lastKnownTargetPosition, alertIntensity);
            }
        }

        /// <summary>
        /// 다른 적으로부터 공유받은 Alert 정보를 처리
        /// </summary>
        public void ReceiveSharedAlert(Vector3 alertPosition, float intensity)
        {
            // 공유받은 정보로 마지막 위치 업데이트
            if (!_hasLastKnownPosition || Vector3.Distance(alertPosition, _lastKnownTargetPosition) > 0.1f)
            {
                _lastKnownTargetPosition = alertPosition;
                _lastSeenTime = Time.time;
                _hasLastKnownPosition = true;
            }

            // 의심도 상승 (적에게서 직접 감지된 것처럼)
            if (suspicionMeter != null)
            {
                suspicionMeter.OnDetectedTarget(intensity);
            }

            // 추적 모드로 전환 (의심도가 낮더라도)
            if (_currentAlertState == EnemyAlertState.Idle || _currentAlertState == EnemyAlertState.Suspicious)
            {
                _currentAlertState = EnemyAlertState.Tracking;
                _trackingMemoryDuration = memoryDuration * trackingMemoryMultiplier;
                LogModule.Instance.Log($"{name} switched to Tracking due to shared alert", "INFO");
                OnAlertStateChanged?.Invoke(_currentAlertState);
            }
        }
    }
}
