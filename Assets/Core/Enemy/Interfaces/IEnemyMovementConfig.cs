using UnityEngine;

namespace HideAndInk.Core.Enemy.Interfaces
{
    /// <summary>
    /// EnemyMovement 설정 Config 인터페이스
    /// 인스펙터 값들을 캡슐화하여 DI/test 가능하게 함
    /// </summary>
    public interface IEnemyMovementConfig
    {
        /// <summary>기본 이동 속도</summary>
        float Speed { get; }

        /// <summary>가속도 (값이 클수록 빠르게 최고속도 도달)</summary>
        float Acceleration { get; }

        /// <summary>마찰력 (0~1, 1에 가까울수록 미끄러짐)</summary>
        float Friction { get; }

        /// <summary>최대 이동 속도 제한</summary>
        float MaxSpeed { get; }

        /// <summary>Ground 레이어마스크</summary>
        LayerMask GroundLayer { get; }

        /// <summary>Ground 체크 거리 (이동 방향 앞쪽)</summary>
        float GroundCheckDistance { get; }

        /// <summary>Ground 체크 반경</summary>
        float GroundCheckRadius { get; }
    }
}
