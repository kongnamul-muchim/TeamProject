using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Player
{
    /// <summary>
    /// 플레이어 이동 설정 구현체
    /// PlayerMovementAdapter가 인스펙터 값으로 생성하여 DI 컨테이너에 등록
    /// </summary>
    public sealed class PlayerMovementConfig : IPlayerMovementConfig
    {
        public float HorizontalSpeed { get; }
        public float VerticalSpeed { get; }
        public float Acceleration { get; }
        public float Friction { get; }

        public PlayerMovementConfig(
            float horizontalSpeed,
            float verticalSpeed,
            float acceleration,
            float friction)
        {
            HorizontalSpeed = horizontalSpeed;
            VerticalSpeed = verticalSpeed;
            Acceleration = acceleration;
            Friction = friction;
        }
    }
}
