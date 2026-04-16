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

        [Header("연동할 의심도 계량기 (Player의 SuspicionMeter)")]
        [SerializeField] private SuspicionMeter suspicionMeter;

        // 상태
        private HashSet<GameObject> _currentlyDetectedTargets;

        private void Awake()
        {
            _currentlyDetectedTargets = new HashSet<GameObject>();
        }

        private void Update()
        {
            // 시야 내 감지된 대상 확인
            var visibleTargets = visionSensor.GetAllVisibleTargets();

            // 감지된 대상이 있으면 의심도 상승 (매 프레임)
            if (visibleTargets.Count > 0)
            {
                float intensity = CalculateDetectionIntensity();
                suspicionMeter?.OnDetectedTarget(intensity);
                Debug.Log($"[VisionSuspicion] Target in view, adding suspicion. Count: {visibleTargets.Count}");
            }

            // 시야에서 사라진 대상 처리
            _currentlyDetectedTargets.Clear();
            foreach (var target in visibleTargets)
            {
                _currentlyDetectedTargets.Add(target);
            }
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
