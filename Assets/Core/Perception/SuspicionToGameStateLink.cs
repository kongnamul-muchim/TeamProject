using UnityEngine;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 의심도 100% 도달 시 게임 상태 자동 전환 관리자
    /// SuspicionMeter의 OnDetected 이벤트 → GameStateMachine.Detected 전환
    /// </summary>
    public sealed class SuspicionToGameStateLink : MonoBehaviour
    {
        [Header("연동할 의심도 계량기")]
        [SerializeField] private SuspicionMeter suspicionMeter;

        private IGameStateMachine _gameStateMachine;

        private void Awake()
        {
            // GameManager에서 GameStateMachine 가져오기
            _gameStateMachine = GameManager.Instance.GetGameStateMachine();

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
        /// </summary>
        private void OnSuspicionMax()
        {
            Debug.Log("[SuspicionGameLink] Suspicion reached MAX! Player detected!");

            if (_gameStateMachine != null && _gameStateMachine.CanTransitionTo(GameState.Detected))
            {
                _gameStateMachine.TransitionTo(GameState.Detected);
            }
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
