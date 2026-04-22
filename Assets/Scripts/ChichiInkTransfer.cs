using UnityEngine;

/// <summary>
/// [잉크 충전] 치치가 두두에게 빠른 충전을 넣는 역할만 담당한다.
/// - Charging 상태일 때 fullChargeDuration 초 동안 0→최대 충전
/// - 위협/연막/보스 의태 중에는 충전 중단
/// </summary>
public class ChichiInkTransfer : MonoBehaviour, IChichiStateListener
{
    [Header("🔗 References - 연결할 컴포넌트")]
    [Tooltip("ChichiStateMachine 컴포넌트 (비워두면 자동 탐색)")]
    [SerializeField] private ChichiStateMachine stateMachine;

    [Header("⚡ Charge - 충전 설정")]
    [Tooltip("0에서 최대 잉크까지 차오르는 데 걸리는 시간(초). 낮을수록 빠름")]
    [Min(0.1f)]
    [SerializeField] private float fullChargeDuration = 4f;

    [Header("📊 Debug - 현재 상태 (디버그용)")]
    [Tooltip("현재 급속 충전 중인지 여부")]
    [SerializeField] private bool isFastCharging = false;

    public bool IsFastCharging => isFastCharging;

    private void Awake()
    {
        // ChichiStateMachine 자동 탐색
        if (stateMachine == null)
        {
            stateMachine = GetComponent<ChichiStateMachine>();
        }

        if (stateMachine != null)
        {
            stateMachine.SetListener(this);
        }
    }

    private void Update()
    {
        if (stateMachine == null || stateMachine.PlayerInk == null)
            return;

        // Charging 상태일 때만 충전
        bool shouldCharge = stateMachine.CurrentState == ChichiStateMachine.ChichiState.Charging;

        if (isFastCharging != shouldCharge)
        {
            isFastCharging = shouldCharge;
            stateMachine.PlayerInk.SetContactCharging(isFastCharging);
        }

        if (!shouldCharge)
            return;

        float chargeRate = stateMachine.PlayerInk.MaxInk / Mathf.Max(0.01f, fullChargeDuration);
        float amount = Mathf.Min(chargeRate * Time.deltaTime, stateMachine.PlayerInk.MaxInk - stateMachine.PlayerInk.CurrentInk);
        if (amount > 0f)
        {
            stateMachine.PlayerInk.AddInk(amount);
        }
    }

    public bool CanReceiveInk()
    {
        if (stateMachine == null || stateMachine.PlayerInk == null)
            return false;

        return stateMachine.PlayerInk.NeedsInk()
            && !stateMachine.PlayerInk.IsUnderThreat
            && !stateMachine.PlayerInk.IsUsingSmoke
            && !stateMachine.PlayerInk.IsUsingBossMimic;
    }

    public bool IsChargeInteractionActive()
    {
        return stateMachine != null && stateMachine.PlayerInk != null && stateMachine.PlayerInk.CanReceiveContactCharge();
    }
}
