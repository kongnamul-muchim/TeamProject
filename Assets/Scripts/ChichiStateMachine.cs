using UnityEngine;

/// <summary>
/// [상태 관리] 치치가 지금 따라갈지, 기다릴지, 충전 중인지 상태만 판단한다.
/// Inspector 에서 "Distance" 섹션: 따라가기/충전 거리 설정
/// </summary>
public class ChichiStateMachine : MonoBehaviour
{
    public enum ChichiState
    {
        Follow,    // 두두 근처에서 대기
        CatchUp,   // 최대 거리 벗어나서 따라감
        Charging   // 두두에게 닿아서 잉크 충전 중
    }

    [Header("🔗 References - 연결할 오브젝트")]
    [Tooltip("따라갈 대상 (두두) 의 Transform")]
    [SerializeField] private Transform target;
    [Tooltip("충전 가능 여부 확인할 리스너 (자동 설정됨)")]
    [SerializeField] private MonoBehaviour listenerTarget;

    [Header("📏 Distance - 거리 설정")]
    [Tooltip("이 거리 이상 멀어지면 CatchUp 상태로 전환")]
    [SerializeField] private float maxFollowDistance = 7f;
    [Tooltip("두두와 이 거리 이내면 Charging 상태 진입 가능")]
    [SerializeField] private float chargeDistance = 1.8f;

    [Header("📊 State - 현재 상태 (디버그용)")]
    [Tooltip("현재 상태 (Follow / CatchUp / Charging)")]
    [SerializeField] private ChichiState currentState = ChichiState.Follow;
    [Tooltip("두두와 접촉 중인지 여부")]
    [SerializeField] private bool isTouchingTank = false;
    [Tooltip("위협 상태인지 여부")]
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
