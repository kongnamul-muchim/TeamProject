using UnityEngine;

/// <summary>
/// [상태 관리] 치치가 지금 따라갈지, 기다릴지, 충전 중인지 상태만 판단한다.
/// 거리 설정은 여기서统一管理한다.
/// </summary>
public class ChichiStateMachine : MonoBehaviour
{
    public enum ChichiState
    {
        Idle,      // Player가 가까워서 멈춤 (stopDistance 이내)
        Follow,    // Player를 따라감 (stopDistance ~ followDistance)
        CatchUp,   // Player가 멀어서 빠르게 따라감 (followDistance 초과)
        Charging   // Player와 접촉하여 잉크 충전 중
    }

    [Header("🔗 References")]
    [Tooltip("따라갈 대상 (Player) 의 Transform")]
    [SerializeField] private Transform target;
    [Tooltip("Player의 PlayerInk 컴포넌트 (비워두면 자동 탐색)")]
    [SerializeField] private PlayerInk playerInk;
    [Tooltip("충전 가능 여부 확인할 리스너 (자동 설정됨)")]
    [SerializeField] private MonoBehaviour listenerTarget;

    [Header("📏 Distance - 거리 설정")]
    [Tooltip("Player가 치치에게 다가올 때 멈추는 거리 (X/Z 각각)")]
    [SerializeField] private float stopDistance = 2f;
    [Tooltip("이 거리 이상 멀어지면 CatchUp 상태로 전환")]
    [SerializeField] private float followDistance = 7f;
    [Tooltip("Player와 접촉하여 충전 진입하는 거리")]
    [SerializeField] private float chargeDistance = 1.8f;

    [Header("📊 State - 현재 상태 (디버그용)")]
    [SerializeField] private ChichiState currentState = ChichiState.Idle;
    [SerializeField] private bool isTouchingTank = false;
    [SerializeField] private bool isUnderThreat = false;

    private ChichiState _previousState;

    public Transform Target => target;
    public PlayerInk PlayerInk => playerInk;
    public ChichiState CurrentState => currentState;
    public bool IsTouchingTank => isTouchingTank;
    public bool IsUnderThreat => isUnderThreat;
    public float StopDistance => stopDistance;
    public float FollowDistance => followDistance;
    public float ChargeDistance => chargeDistance;

    public System.Action<ChichiState> OnStateChanged;
    public System.Action OnChargingStarted;
    public System.Action OnChargingStopped;

    private void Awake()
    {
        _previousState = currentState;

        if (playerInk == null)
            playerInk = FindObjectOfType<PlayerInk>();
    }

    private void Update()
    {
        if (target == null)
            return;

        ChichiState newState = EvaluateState();
        if (newState != currentState)
            TransitionTo(newState);
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

        // X/Z 각각 거리 계산 (Follower와 동일한 기준)
        float distanceX = Mathf.Abs(targetPosition.x - chichiPosition.x);
        float distanceZ = Mathf.Abs(targetPosition.z - chichiPosition.z);
        float maxDistance = Mathf.Max(distanceX, distanceZ);

        IChichiStateListener listener = listenerTarget as IChichiStateListener;
        bool canCharge = listener != null && listener.CanReceiveInk();
        bool isInteractionActive = listener != null && listener.IsChargeInteractionActive();

        // 위협 중에는 충전 차단, Follow/CatchUp만 가능
        if (isUnderThreat)
            return maxDistance > followDistance ? ChichiState.CatchUp : ChichiState.Follow;

        // 충전 조건: 충전 가능 + 탱크 접촉 + 거리 이내 + 상호작용 활성
        if (canCharge && isTouchingTank && maxDistance <= chargeDistance && isInteractionActive)
            return ChichiState.Charging;

        // 거리 기반 상태 전환
        if (maxDistance > followDistance)
            return ChichiState.CatchUp;
        if (maxDistance > stopDistance)
            return ChichiState.Follow;

        return ChichiState.Idle;
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
