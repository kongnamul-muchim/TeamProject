using UnityEngine;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 플레이어 이동 방향
    /// </summary>
    public enum MoveDirection
    {
        Down,   // 기본/하
        Right,  // 우
        Up,     // 상
        Left    // 좌
    }

    /// <summary>
    /// 플레이어 이동 시스템 인터페이스
    /// </summary>
    public interface IPlayerMovement
    {
        /// <summary>
        /// 현재 속도 벡터
        /// </summary>
        Vector2 Velocity { get; }

        /// <summary>
        /// 현재 이동 방향
        /// </summary>
        MoveDirection Direction { get; }

        /// <summary>
        /// 이동 입력 처리
        /// </summary>
        /// <param name="direction">방향 벡터 (보통 (-1, 0, 1) 범위)</param>
        void Move(Vector2 direction);

        /// <summary>
        /// 이동 즉시 중지
        /// </summary>
        void Stop();

        /// <summary>
        /// 이동 중인지 여부
        /// </summary>
        bool IsMoving { get; }

        /// <summary>
        /// 상태 업데이트 (매 프레임 호출)
        /// </summary>
        /// <param name="deltaTime">경과 시간</param>
        void Update(float deltaTime);
    }
}