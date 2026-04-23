/// <summary>
/// 치치 상태머신이 충전 가능 여부만 물어볼 때 쓰는 인터페이스.
/// </summary>
public interface IChichiStateListener
{
    bool CanReceiveInk();
    bool IsChargeInteractionActive();
}
