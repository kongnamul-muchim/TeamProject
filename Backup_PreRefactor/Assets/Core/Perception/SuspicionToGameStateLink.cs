using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의심도 100% 도달 시 게임 상태 자동 전환 관리자
    /// SuspicionMeter의 OnDetected 이벤트 → OnPlayerDetected 이벤트 발생
    /// GameManager가 이 이벤트를 구독해서 GameStateMachine.Detected 전환 처리
    /// </summary>
    public sealed class SuspicionToGameStateLink : MonoBehaviour
    {
        [Header("연동할 의심도 계량기")]
        [SerializeField] private SuspicionMeter suspicionMeter;

        /// <summary>
        /// 플레이어 발각 시 발생하는 이벤트 (GameManager가 구독)
        /// </summary>
        public event System.Action OnPlayerDetected;

        private void Awake()
        {
            if (suspicionMeter != null)
            {
                suspicionMeter.OnDetected += OnSuspicionMax;
            }
        }

        private void OnDestroy()
        {
            if (suspicionMeter != null)
            {
                suspicionMeter.OnDetected -= OnSuspicionMax;
            }
        }

        /// <summary>
        /// 의심도가 100% 도달했을 때 호출
        /// 직접 상태 전이를 수행하지 않고 이벤트만 발생시킴
        /// </summary>
        private void OnSuspicionMax()
        {
            Debug.Log("[SuspicionGameLink] Suspicion reached MAX! Player detected!");

            // 이벤트 발생 - 코어 시스템이 구독해서 처리
            OnPlayerDetected?.Invoke();
        }

        /// <summary>
        /// 의심도 계량기 설정
        /// </summary>
        public void SetSuspicionMeter(SuspicionMeter meter)
        {
            // 이전 이벤트 해제
            if (suspicionMeter != null)
            {
                suspicionMeter.OnDetected -= OnSuspicionMax;
            }

            suspicionMeter = meter;

            // 새 이벤트 등록
            if (suspicionMeter != null)
            {
                suspicionMeter.OnDetected += OnSuspicionMax;
            }
        }
    }
}
