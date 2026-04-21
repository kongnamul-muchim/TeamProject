using UnityEngine;

namespace HideAndInk.Core.Enemy.Interfaces
{
    /// <summary>
    /// Enemy 타입
    /// </summary>
    public enum EnemyType
    {
        Normal,     // 일반 몬스터
        Elite,      // 정예 몬스터
        Boss        // 보스 몬스터
    }

    /// <summary>
    /// Enemy 공통 인터페이스
    /// </summary>
    public interface IEnemy
    {
        /// <summary>
        /// Enemy Transform (위치/회전 참조)
        /// </summary>
        Transform Transform { get; }

        /// <summary>
        /// 현재 위치
        /// </summary>
        Vector3 Position { get; }

        /// <summary>
        /// Enemy 타입
        /// </summary>
        EnemyType Type { get; }

        /// <summary>
        /// 활성화 여부
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// 현재 바라보는 방향 (정규화된 벡터)
        /// </summary>
        Vector3 Forward { get; }

        /// <summary>
        /// 이동 속도
        /// </summary>
        float Speed { get; }

        /// <summary>
        /// 매 프레임 업데이트
        /// </summary>
        void Update(float deltaTime);
    }
}
