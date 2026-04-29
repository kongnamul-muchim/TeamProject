using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Movement
{
    /// <summary>
    /// EnemyMovement 설정 Config 구현체
    /// EnemyAIController.InitializeMovement()에서 인스펙터 값으로 생성
    /// </summary>
    public sealed class EnemyMovementConfig : IEnemyMovementConfig
    {
        public float Speed { get; }
        public float Acceleration { get; }
        public float Friction { get; }
        public float MaxSpeed { get; }
        public LayerMask GroundLayer { get; }
        public float GroundCheckDistance { get; }
        public float GroundCheckRadius { get; }

        public EnemyMovementConfig(
            float speed,
            float acceleration,
            float friction,
            float maxSpeed,
            LayerMask groundLayer,
            float groundCheckDistance,
            float groundCheckRadius)
        {
            Speed = speed;
            Acceleration = acceleration;
            Friction = friction;
            MaxSpeed = maxSpeed;
            GroundLayer = groundLayer;
            GroundCheckDistance = groundCheckDistance;
            GroundCheckRadius = groundCheckRadius;
        }
    }
}
