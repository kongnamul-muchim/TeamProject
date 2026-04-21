using UnityEngine;

/// <summary>
/// 치치의 이동만 담당한다.
/// 멀어졌을 때만 따라가고, 가까우면 기다린다.
/// </summary>
public class ChichiFollower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ChichiStateMachine stateMachine;
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Guide Position")]
    [SerializeField] private float behindDistance = 7f;
    [SerializeField] private float sideOffset = 0.8f;
    [SerializeField] private Vector3 liftOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private Vector3 guideOffset = new Vector3(0f, 0f, 0f);

    [Header("Move Speed")]
    [SerializeField] private float catchUpSmoothTime = 0.25f;
    [SerializeField] private float chargeSmoothTime = 0.12f;
    [SerializeField] private float targetSmoothTime = 0.18f;
    [SerializeField] private float turnSpeed = 6f;

    private Vector3 _lastTargetPosition;
    private Vector3 _lastMoveDirection = Vector3.forward;
    private Vector3 _smoothedMoveDirection = Vector3.forward;
    private Vector3 _currentTarget;
    private Vector3 _moveVelocity;
    private Vector3 _targetVelocity;
    private bool _facingRight = true;

    private void Start()
    {
        if (stateMachine == null || stateMachine.Target == null)
            return;

        _lastTargetPosition = stateMachine.Target.position;
        _currentTarget = GetFollowTargetPosition();
        stateMachine.OnStateChanged += HandleStateChanged;

        // SpriteRenderer 자동 탐색
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
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
        // Follow 상태에서는 움직이지 않음
        if (stateMachine.CurrentState == ChichiStateMachine.ChichiState.Follow)
            return;

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
        Vector3 behindDirection = -_smoothedMoveDirection;
        Vector3 sideDirection = new Vector3(-behindDirection.z, 0f, behindDirection.x);

        if (behindDirection.sqrMagnitude < 0.00001f)
        {
            behindDirection = Vector3.back;
            sideDirection = Vector3.right;
        }

        Vector3 targetPosition = stateMachine.Target.position;
        Vector3 planarOffset = behindDirection * behindDistance + sideDirection * sideOffset + new Vector3(guideOffset.x, 0f, guideOffset.z);

        return new Vector3(
            targetPosition.x + planarOffset.x,
            targetPosition.y + liftOffset.y + guideOffset.y,
            targetPosition.z);
    }

    private Vector3 GetChargeTargetPosition()
    {
        return transform.position;
    }
}
