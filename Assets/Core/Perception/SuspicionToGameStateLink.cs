using UnityEngine;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의심도 100% 도달 시 게임 상태 자동 전환 관리자
    /// SuspicionManager의 OnDetected 이벤트 → OnPlayerDetected 이벤트 발생
    /// GameManager가 이 이벤트를 구독해서 GameStateMachine.Detected 전환 처리
    /// </summary>
    public sealed class SuspicionToGameStateLink : MonoBehaviour
    {
        /// <summary>
        /// 플레이어 발각 시 발생하는 이벤트 (GameManager가 구독)
        /// </summary>
        public event System.Action OnPlayerDetected;

        private void OnEnable()
        {
            if (SuspicionManager.Instance != null)
            {
                SuspicionManager.Instance.OnDetected += OnSuspicionMax;
            }
        }

        private void OnDisable()
        {
            if (SuspicionManager.Instance != null)
            {
                SuspicionManager.Instance.OnDetected -= OnSuspicionMax;
            }
        }

        /// <summary>
        /// 의심도가 100% 도달했을 때 호출
        /// 직접 상태 전이를 수행하지 않고 이벤트만 발생시킴
        /// </summary>
        private void OnSuspicionMax()
        {
#if UNITY_EDITOR
            Debug.Log("[SuspicionGameLink] Suspicion reached MAX! Player detected!");
#endif
            OnPlayerDetected?.Invoke();
        }
    }
}
