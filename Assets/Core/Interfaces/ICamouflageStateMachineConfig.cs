namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 의태 상태 머신 설정
    /// 인스펙터 값을 DI 컨테이너로 전달하기 위한 Config 인터페이스
    /// </summary>
    public interface ICamouflageStateMachineConfig
    {
        float AttachDelay { get; }
        float LockTime { get; }
        float BlendTime { get; }
        float PerfectTime { get; }
    }
}
