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

    [Header("📏 Distance - 거리 설정 (X/Z 독립)")]
    [Tooltip("Player가 치치에게 다가올 때 멈추는 거리 (X=가로, Z=세로)")]
    [SerializeField] private Vector2 stopDistance = new Vector2(2f, 2f);
    [Tooltip("이 거리 이상 멀어지면 CatchUp 상태로 전환 (X=가로, Z=세로)")]
    [SerializeField] private Vector2 followDistance = new Vector2(7f, 7f);
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
    public Vector2 StopDistance => stopDistance;
    public Vector2 FollowDistance => followDistance;
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

        // X/Z 각각 거리 계산
        float distanceX = Mathf.Abs(targetPosition.x - chichiPosition.x);
        float distanceZ = Mathf.Abs(targetPosition.z - chichiPosition.z);

        IChichiStateListener listener = listenerTarget as IChichiStateListener;
        bool canCharge = listener != null && listener.CanReceiveInk();
        bool isInteractionActive = listener != null && listener.IsChargeInteractionActive();

        // 직사각형 영역 기반 판정: X는 X와, Z는 Z와 각각 비교
        bool withinStopX = distanceX <= stopDistance.x;
        bool withinStopZ = distanceZ <= stopDistance.y;
        bool withinFollowX = distanceX <= followDistance.x;
        bool withinFollowZ = distanceZ <= followDistance.y;

        // 위협 중에는 충전 차단, Follow/CatchUp만 가능
        if (isUnderThreat)
            return (!withinFollowX || !withinFollowZ) ? ChichiState.CatchUp : ChichiState.Follow;

        // 충전 조건: 충전 가능 + 탱크 접촉 + 거리 이내 + 상호작용 활성
        float maxDistance = Mathf.Max(distanceX, distanceZ);
        if (canCharge && isTouchingTank && maxDistance <= chargeDistance && isInteractionActive)
            return ChichiState.Charging;

        // 거리 기반 상태 전환 (직사각형: X와 Z 모두 이내여야 해당 영역)
        if (!withinFollowX || !withinFollowZ)
            return ChichiState.CatchUp;
        if (!withinStopX || !withinStopZ)
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
