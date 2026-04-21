using UnityEngine;

/// <summary>
/// 치치가 두두에게 빠른 충전을 넣는 역할만 담당한다.
/// </summary>
public class ChichiInkTransfer : MonoBehaviour, IChichiStateListener
{
    [Header("References")]
    [SerializeField] private ChichiStateMachine stateMachine;
    [SerializeField] private PlayerInk targetInk;

    [Header("Charge")]
    [Min(0.1f)]
    [SerializeField] private float fullChargeDuration = 4f;
    [SerializeField] private float contactDistanceTolerance = 2f;

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
