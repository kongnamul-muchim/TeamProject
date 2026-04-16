using UnityEngine;
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
        [SerializeField] private float nearbyDetectionIntensity = 0.3f;  // 근접 시 소폭 증가

        [Header("연동할 의심도 계량기 (Player의 SuspicionMeter)")]
        [SerializeField] private SuspicionMeter suspicionMeter;

        // 상태
        private HashSet<GameObject> _currentlyDetectedTargets;
        private bool _wasInVision;  // 이전 프레임에서 시야에 있었는지

        private void Awake()
        {
            _currentlyDetectedTargets = new HashSet<GameObject>();
            _wasInVision = false;
        }

        private void Update()
        {
            if (suspicionMeter == null) return;

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
                Debug.Log($"[VisionSuspicion] Target in view, distance={distance:F1}, intensity={intensity:F2}");
            }
            // 2. 시야에서 벗어남 → 근처 체크
            else if (_wasInVision)
            {
                // 시야에서 벗어난 직후, 근처에 있으면 소폭 증가
                float nearbyDistance = visionSensor.ViewRadius * nearbyDistanceMultiplier;
                float intensity = CalculateNearbyIntensity(nearbyDistance);

                if (intensity > 0f)
                {
                    suspicionMeter.OnDetectedTarget(intensity);
                    Debug.Log($"[VisionSuspicion] Target left vision but nearby, intensity={intensity:F2}");
                }
            }

            _wasInVision = isInVision;

            // 시야에서 사라진 대상 처리
            _currentlyDetectedTargets.Clear();
            foreach (var target in visibleTargets)
            {
                _currentlyDetectedTargets.Add(target);
            }
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
        private float CalculateNearbyIntensity(float nearbyDistance)
        {
            // 감지된 적과의 거리 체크
            if (_currentlyDetectedTargets.Count == 0)
                return 0f;

            float nearestDistance = float.MaxValue;
            foreach (var target in _currentlyDetectedTargets)
            {
                float dist = Vector3.Distance(transform.position, target.transform.position);
                if (dist < nearestDistance)
                    nearestDistance = dist;
            }

            // 근처 범위 내에 있으면 소폭 증가
            if (nearestDistance <= visionSensor.ViewRadius * nearbyDistanceMultiplier)
            {
                // 거리에 반비례하여 강도 조절 (가까울수록 강함)
                float distanceFactor = 1f - (nearestDistance / (visionSensor.ViewRadius * nearbyDistanceMultiplier));
                return nearbyDetectionIntensity * (0.5f + distanceFactor * 0.5f);
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
    }
}
