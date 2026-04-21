using UnityEngine;

/// <summary>
/// [이동] 치치의 이동만 담당한다.
/// - Follow 상태: 움직이지 않음 (두두 근처에서 대기)
/// - CatchUp 상태: 두두 뒤로 부드럽게 따라감
/// - Charging 상태: 제자리 유지
/// - 이동 방향에 따라 스프라이트 좌/우 반전
/// </summary>
public class ChichiFollower : MonoBehaviour
{
    [Header("🔗 References - 연결할 컴포넌트")]
    [Tooltip("ChichiStateMachine 컴포넌트 (비워두면 자동 탐색)")]
    [SerializeField] private ChichiStateMachine stateMachine;
    [Tooltip("치치 SpriteRenderer (비워두면 자동 탐색)")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("📍 Guide Position - 따라가기 위치 설정")]
    [Tooltip("측면으로 벗어난 거리")]
    [SerializeField] private float sideOffset = 0.8f;
    [Tooltip("수직 (Y) 오프셋")]
    [SerializeField] private Vector3 liftOffset = new Vector3(0f, 0.5f, 0f);
    [Tooltip("추가 가이드 오프셋 (X, Y, Z)")]
    [SerializeField] private Vector3 guideOffset = new Vector3(0f, 0f, 0f);

    [Header("⚡ Move Speed - 이동 속도 설정")]
    [Tooltip("따라갈 때 부드러움 (낮을수록 느리고 부드러움)")]
    [SerializeField] private float catchUpSmoothTime = 0.25f;
    [Tooltip("충전 중 위치 보정 부드러움")]
    [SerializeField] private float chargeSmoothTime = 0.12f;
    [Tooltip("목표 위치 보정 부드러움")]
    [SerializeField] private float targetSmoothTime = 0.18f;
    [Tooltip("방향 전환 속도 (높을수록 빠르게 방향 전환)")]
    [SerializeField] private float turnSpeed = 6f;

    private Vector3 _lastTargetPosition;
    private Vector3 _lastMoveDirection = Vector3.forward;
    private Vector3 _smoothedMoveDirection = Vector3.forward;
    private Vector3 _currentTarget;
    private Vector3 _moveVelocity;
    private Vector3 _targetVelocity;
    private bool _facingRight = true;

    private void Awake()
    {
        // ChichiStateMachine 자동 탐색
        if (stateMachine == null)
        {
            stateMachine = GetComponent<ChichiStateMachine>();
        }

        // SpriteRenderer 자동 탐색
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void Start()
    {
        if (stateMachine == null || stateMachine.Target == null)
            return;

        _lastTargetPosition = stateMachine.Target.position;
        _currentTarget = GetFollowTargetPosition();
        stateMachine.OnStateChanged += HandleStateChanged;
    }

    private void OnDestroy()
    {
        if (stateMachine != null)
        {
            stateMachine.OnStateChanged -= HandleStateChanged;
        }
    }

    private void LateUpdate()
    {
        if (stateMachine == null || stateMachine.Target == null)
            return;

        UpdateMoveDirection(stateMachine.Target.position - _lastTargetPosition);
        UpdateCurrentTarget(stateMachine.CurrentState);
        UpdateMovement();
        UpdateSpriteDirection();
        _lastTargetPosition = stateMachine.Target.position;
    }

    private void HandleStateChanged(ChichiStateMachine.ChichiState state)
    {
        UpdateCurrentTarget(state);
    }

    private void UpdateMoveDirection(Vector3 delta)
    {
        delta.y = 0f;

        if (delta.sqrMagnitude > 0.00001f)
        {
            _lastMoveDirection = delta.normalized;
        }

        _smoothedMoveDirection = Vector3.Lerp(_smoothedMoveDirection, _lastMoveDirection, turnSpeed * Time.deltaTime);
        if (_smoothedMoveDirection.sqrMagnitude > 0.00001f)
        {
            _smoothedMoveDirection.Normalize();
        }
    }

    private void UpdateCurrentTarget(ChichiStateMachine.ChichiState state)
    {
        Vector3 desiredTarget = transform.position;

        if (state == ChichiStateMachine.ChichiState.CatchUp)
        {
            desiredTarget = GetFollowTargetPosition();
        }
        else if (state == ChichiStateMachine.ChichiState.Charging)
        {
            desiredTarget = transform.position;
        }

        _currentTarget = Vector3.SmoothDamp(_currentTarget, desiredTarget, ref _targetVelocity, targetSmoothTime);
    }

    private void UpdateMovement()
    {
        // Follow 상태에서는 X,Y는 고정, Z만 두두 따라감
        if (stateMachine.CurrentState == ChichiStateMachine.ChichiState.Follow)
        {
            Vector3 pos = transform.position;
            pos.z = stateMachine.Target.position.z;
            transform.position = pos;
            return;
        }

        float smoothTime = stateMachine.CurrentState == ChichiStateMachine.ChichiState.Charging ? chargeSmoothTime : catchUpSmoothTime;
        Vector3 nextPosition = Vector3.SmoothDamp(transform.position, _currentTarget, ref _moveVelocity, smoothTime);
        nextPosition.y = _currentTarget.y;
        nextPosition.z = stateMachine.Target.position.z;
        transform.position = nextPosition;
    }

    /// <summary>
    /// 이동 방향에 따라 스프라이트 Y Rotation을 0(오른쪽) 또는 180(왼쪽)으로 전환
    /// </summary>
    private void UpdateSpriteDirection()
    {
        if (spriteRenderer == null)
            return;

        // X축 이동 방향 확인
        float moveX = _smoothedMoveDirection.x;
        if (Mathf.Abs(moveX) < 0.1f)
            return; // 이동량이 작으면 방향 전환 안 함

        bool shouldFaceRight = moveX > 0f;

        if (shouldFaceRight != _facingRight)
        {
            _facingRight = shouldFaceRight;
            Vector3 rotation = spriteRenderer.transform.localEulerAngles;
            rotation.y = shouldFaceRight ? 0f : 180f;
            spriteRenderer.transform.localEulerAngles = rotation;
        }
    }

    private Vector3 GetFollowTargetPosition()
    {
        // 따라가기 거리는 StateMachine 의 maxFollowDistance 사용
        float followDistance = stateMachine.MaxFollowDistance;

        Vector3 behindDirection = -_smoothedMoveDirection;
        Vector3 sideDirection = new Vector3(-behindDirection.z, 0f, behindDirection.x);

        if (behindDirection.sqrMagnitude < 0.00001f)
        {
            behindDirection = Vector3.back;
            sideDirection = Vector3.right;
        }

        Vector3 targetPosition = stateMachine.Target.position;
        Vector3 planarOffset = behindDirection * followDistance + sideDirection * sideOffset + new Vector3(guideOffset.x, 0f, guideOffset.z);

        return new Vector3(
            targetPosition.x + planarOffset.x,
            targetPosition.y + liftOffset.y + guideOffset.y,
            targetPosition.z);
    }
}
