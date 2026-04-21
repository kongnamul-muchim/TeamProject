using UnityEngine;

/// <summary>
/// 치치가 지금 따라갈지, 기다릴지, 충전 중인지 상태만 판단한다.
/// </summary>
public class ChichiStateMachine : MonoBehaviour
{
    public enum ChichiState
    {
        Follow,
        CatchUp,
        Charging
    }

    [Header("References")]
    [SerializeField] private Transform target;
    [SerializeField] private MonoBehaviour listenerTarget;

    [Header("Distance")]
    [SerializeField] private float maxFollowDistance = 7f;
    [SerializeField] private float chargeDistance = 1.8f;

    [Header("State")]
    [SerializeField] private ChichiState currentState = ChichiState.Follow;
    [SerializeField] private bool isTouchingTank = false;
    [SerializeField] private bool isUnderThreat = false;

    private ChichiState _previousState;

    public Transform Target => target;
    public ChichiState CurrentState => currentState;
    public bool IsTouchingTank => isTouchingTank;
    public bool IsUnderThreat => isUnderThreat;
    public float MaxFollowDistance => maxFollowDistance;
    public float ChargeDistance => chargeDistance;

    public System.Action<ChichiState> OnStateChanged;
    public System.Action OnChargingStarted;
    public System.Action OnChargingStopped;

    private void Awake()
    {
        _previousState = currentState;
    }

    private void Update()
    {
        if (target == null)
            return;

        ChichiState newState = EvaluateState();
        if (newState != currentState)
        {
            TransitionTo(newState);
        }
    }

    public void SetThreat(bool threat)
    {
        isUnderThreat = threat;
    }

    public void SetTouchingTank(bool touching)
    {
        isTouchingTank = touching;
    }

    public void SetListener(IChichiStateListener listener)
    {
        listenerTarget = listener as MonoBehaviour;
    }

    private ChichiState EvaluateState()
    {
        Vector3 chichiPosition = transform.position;
        Vector3 targetPosition = target.position;
        chichiPosition.y = 0f;
        targetPosition.y = 0f;

        float distance = Vector3.Distance(chichiPosition, targetPosition);
        IChichiStateListener listener = listenerTarget as IChichiStateListener;
        bool canCharge = listener != null && listener.CanReceiveInk();

        if (canCharge && isTouchingTank && listener.IsChargeInteractionActive())
            return ChichiState.Charging;

        if (distance > maxFollowDistance)
            return ChichiState.CatchUp;

        return ChichiState.Follow;
    }

    private void TransitionTo(ChichiState newState)
    {
        _previousState = currentState;
        currentState = newState;
        OnStateChanged?.Invoke(currentState);

        if (currentState == ChichiState.Charging && _previousState != ChichiState.Charging)
            OnChargingStarted?.Invoke();

        if (currentState != ChichiState.Charging && _previousState == ChichiState.Charging)
            OnChargingStopped?.Invoke();
    }
}
