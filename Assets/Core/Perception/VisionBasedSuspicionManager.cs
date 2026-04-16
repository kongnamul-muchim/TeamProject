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

        [Header("연동할 의심도 계량기")]
        [SerializeField] private ISuspicionMeter suspicionMeter;

        [Header("감지 딜레이 (과도한 감지 방지)")]
        [SerializeField] private float detectionCooldown = 0.5f;

        // 상태
        private float _detectionCooldownTimer;
        private HashSet<GameObject> _currentlyDetectedTargets;

        private void Awake()
        {
            _currentlyDetectedTargets = new HashSet<GameObject>();
            _detectionCooldownTimer = 0f;
        }

        private void Update()
        {
            // 쿨다운 타이머
            if (_detectionCooldownTimer > 0f)
            {
                _detectionCooldownTimer -= Time.deltaTime;
                return;
            }

            // 시야 내 감지된 대상 확인
            var visibleTargets = visionSensor.GetAllVisibleTargets();

            // 새로 감지된 대상이 있으면 의심도 상승
            foreach (var target in visibleTargets)
            {
                if (!_currentlyDetectedTargets.Contains(target))
                {
                    // 새로 감지!
                    OnNewTargetDetected(target);
                }
            }

            // 시야에서 사라진 대상 처리
            if (visibleTargets.Count == 0 && _currentlyDetectedTargets.Count > 0)
            {
                // 모든 대상이 시야에서 사라짐 - 자연 하락은 SuspicionMeter에서 처리
                _currentlyDetectedTargets.Clear();
            }
            else
            {
                // 여전히 감지 중이면疑시도 유지 (감소 안 함)
                _currentlyDetectedTargets.Clear();
                foreach (var target in visibleTargets)
                {
                    _currentlyDetectedTargets.Add(target);
                }
            }
        }

        /// <summary>
        /// 새로운 대상 감지 시 호출
        /// </summary>
        private void OnNewTargetDetected(GameObject target)
        {
            Debug.Log($"[VisionSuspicion] New target detected: {target.name}");

            // 패턴별 감지 강도 계산
            float intensity = CalculateDetectionIntensity();

            // 의심도 상승
            suspicionMeter?.OnDetectedTarget(intensity);

            // 감지 딜레이 재설정
            _detectionCooldownTimer = detectionCooldown;
        }

        /// <summary>
        /// 감지 강도 계산 (패턴별)
        /// </summary>
        private float CalculateDetectionIntensity()
        {
            float patternMultiplier = visionSensor.PatternType switch
            {
                VisionPatternType.Patrol => patrolDetectionMultiplier,
                VisionPatternType.Observe => observeDetectionMultiplier,
                VisionPatternType.Guard => guardDetectionMultiplier,
                _ => 1f
            };

            return baseDetectionIntensity * patternMultiplier;
        }

        /// <summary>
        /// 의심도 계량기 설정 (외부에서 호출)
        /// </summary>
        public void SetSuspicionMeter(ISuspicionMeter meter)
        {
            suspicionMeter = meter;
        }

        /// <summary>
        /// 현재 감지 중인 대상 수
        /// </summary>
        public int CurrentlyDetectedCount => _currentlyDetectedTargets.Count;
    }
}
