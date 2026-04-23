using UnityEngine;

namespace HideAndInk.Core.Enemy.Elite
{
    /// <summary>
    /// 정예 몬스터 행동 패턴 인터페이스
    /// 센서 없이 거리/상태 기반 행동 발동
    /// </summary>
    public interface IEliteBehavior
    {
        /// <summary>
        /// 행동 패턴 이름 (디버깅용)
        /// </summary>
        string BehaviorName { get; }

        /// <summary>
        /// Player 접근 시 호출 (매 프레임)
        /// </summary>
        /// <param name="distance">Player까지의 거리</param>
        /// <param name="playerPos">Player 위치</param>
        void OnPlayerApproached(float distance, Vector3 playerPos);

        /// <summary>
        /// 행동 패턴 업데이트 (매 프레임)
        /// </summary>
        /// <param name="deltaTime">경과 시간</param>
        void OnUpdate(float deltaTime);

        /// <summary>
        /// 행동 패턴 활성화 (정예 몬스터 시작 시)
        /// </summary>
        void OnActivate();

        /// <summary>
        /// 행동 패턴 비활성화 (정예 몬스터 종료 시)
        /// </summary>
        void OnDeactivate();
    }
}
