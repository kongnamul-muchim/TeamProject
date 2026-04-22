namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 위협 상태를 외부에서 제어할 수 있게 하는 인터페이스.
    /// VisionBasedSuspicionManager가 Alert 상태일 때 이를 호출해서
    /// 충전 중단을 연동한다.
    /// </summary>
    public interface IThreatHandler
    {
        void SetThreat(bool threat);
    }
}