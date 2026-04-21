using UnityEngine;

/// <summary>
/// [잉크 충전] 치치가 두두에게 빠른 충전을 넣는 역할만 담당한다.
/// - 접촉 시 fullChargeDuration 초 동안 0→최대 충전
/// - 위협/연막/보스 의태 중에는 충전 중단
/// </summary>
public class ChichiInkTransfer : MonoBehaviour, IChichiStateListener
{
    [Header("🔗 References - 연결할 컴포넌트")]
    [Tooltip("ChichiStateMachine 컴포넌트 참조")]
    [SerializeField] private ChichiStateMachine stateMachine;
    [Tooltip("두두의 PlayerInk 컴포넌트 참조")]
    [SerializeField] private PlayerInk targetInk;

    [Header("⚡ Charge - 충전 설정")]
    [Tooltip("0에서 최대 잉크까지 차오르는 데 걸리는 시간(초). 낮을수록 빠름")]
    [Min(0.1f)]
    [SerializeField] private float fullChargeDuration = 4f;
    [Tooltip("충전 가능 판정 거리 (두두와 이 거리 이내면 충전 시도)")]
    [SerializeField] private float contactDistanceTolerance = 2f;

    [Header("📊 Debug - 현재 상태 (디버그용)")]
    [Tooltip("현재 급속 충전 중인지 여부")]
    [SerializeField] private bool isFastCharging = false;

    public bool IsFastCharging => isFastCharging;

    private void Awake()
    {
        if (stateMachine != null)
        {
            stateMachine.SetListener(this);
        }
    }

    private void Update()
    {
        if (targetInk == null || stateMachine == null)
            return;

        bool isInContactRange = IsWithinContactRange();
        bool shouldCharge = (stateMachine.IsTouchingTank || isInContactRange) && CanReceiveInk() && IsChargeInteractionActive();

        if (isFastCharging != shouldCharge)
        {
            isFastCharging = shouldCharge;
            targetInk.SetContactCharging(isFastCharging);
        }

        if (!shouldCharge)
            return;

        float chargeRate = targetInk.MaxInk / Mathf.Max(0.01f, fullChargeDuration);
        float amount = Mathf.Min(chargeRate * Time.deltaTime, targetInk.MaxInk - targetInk.CurrentInk);
        if (amount > 0f)
        {
            targetInk.AddInk(amount);
        }
    }

    public bool CanReceiveInk()
    {
        if (targetInk == null)
            return false;

        return targetInk.NeedsInk() && !targetInk.IsUnderThreat && !targetInk.IsUsingSmoke && !targetInk.IsUsingBossMimic;
    }

    public bool IsChargeInteractionActive()
    {
        return targetInk != null && targetInk.CanReceiveContactCharge();
    }

    private bool IsWithinContactRange()
    {
        if (stateMachine.Target == null)
            return false;

        Vector3 chichiPosition = transform.position;
        Vector3 targetPosition = stateMachine.Target.position;
        chichiPosition.y = 0f;
        targetPosition.y = 0f;

        return Vector3.Distance(chichiPosition, targetPosition) <= contactDistanceTolerance;
    }
}
