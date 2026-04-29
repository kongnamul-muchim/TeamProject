namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 플레이어 이동 설정
    /// 인스펙터 값을 DI 컨테이너로 전달하기 위한 Config 인터페이스
    /// </summary>
    public interface IPlayerMovementConfig
    {
        float HorizontalSpeed { get; }
        float VerticalSpeed { get; }
        float Acceleration { get; }
        float Friction { get; }
    }
}
