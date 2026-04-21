using UnityEngine;

namespace HideAndInk.Core.Enemy.AI
{
    /// <summary>
    /// Enemy AI 상태
    /// </summary>
    public enum EnemyAIState
    {
        Patrol,     // 순찰 상태
        Chase,      // 추적 상태
        Search      // 탐색 상태
    }

    /// <summary>
    /// Enemy AI 상태 인터페이스
    /// 각 상태(Patrol, Chase, Search)는 이 인터페이스를 구현
    /// </summary>
    public interface IEnemyAIState
    {
        /// <summary>
        /// 현재 상태 타입
        /// </summary>
        EnemyAIState StateType { get; }

        /// <summary>
        /// 상태 진입 시 호출
        /// </summary>
        void OnEnter();

        /// <summary>
        /// 상태 업데이트 (매 프레임)
        /// </summary>
        /// <param name="deltaTime">경과 시간</param>
        void OnUpdate(float deltaTime);

        /// <summary>
        /// 상태 이탈 시 호출
        /// </summary>
        void OnExit();

        /// <summary>
        /// 특정 상태로 전환 가능한지 확인
        /// </summary>
        /// <param name="targetState">목표 상태</param>
        /// <returns>전환 가능 여부</returns>
        bool CanTransitionTo(EnemyAIState targetState);
    }
}
