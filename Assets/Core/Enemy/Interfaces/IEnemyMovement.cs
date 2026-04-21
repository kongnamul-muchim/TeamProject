using UnityEngine;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Enemy.Interfaces
{
    /// <summary>
    /// Enemy 이동 시스템 인터페이스
    /// IPlayerMovement와 유사한 구조로 일관성 유지
    /// </summary>
    public interface IEnemyMovement
    {
        /// <summary>
        /// 현재 속도 벡터 (2D 평면)
        /// </summary>
        Vector2 Velocity { get; }

        /// <summary>
        /// 현재 이동 방향
        /// </summary>
        MoveDirection Direction { get; }

        /// <summary>
        /// 이동 중인지 여부
        /// </summary>
        bool IsMoving { get; }

        /// <summary>
        /// 이동 속도 (인스펙터 설정 가능)
        /// </summary>
        float Speed { get; set; }

        /// <summary>
        /// 목표 위치로 이동
        /// </summary>
        /// <param name="targetPosition">목표 위치 (월드 좌표)</param>
        void MoveTo(Vector2 targetPosition);

        /// <summary>
        /// 이동 즉시 중지
        /// </summary>
        void Stop();

        /// <summary>
        /// 상태 업데이트 (매 프레임 호출)
        /// </summary>
        /// <param name="deltaTime">경과 시간</param>
        void Update(float deltaTime);
    }
}
