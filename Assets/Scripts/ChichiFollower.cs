using UnityEngine;

/// <summary>
/// [이동] 치치의 이동만 담당한다.
/// - Follow 상태: 움직이지 않음 (두두 근처에서 대기, Z축만 부드럽게 따라감)
/// - CatchUp 상태: 두두 뒤로 부드럽게 따라감
/// - Charging 상태: 제자리 유지
/// - 이동 방향에 따라 스프라이트 변경 (Front/Back/Side)
/// - Side 스프라이트는 좌/우에 따라 SpriteRenderer.flipX 반전
/// </summary>
public class ChichiFollower : MonoBehaviour
{
    [Header("🔗 References - 연결할 컴포넌트")]
    [Tooltip("ChichiStateMachine 컴포넌트 (비워두면 자동 탐색)")]
    [SerializeField] private ChichiStateMachine stateMachine;
    [Tooltip("치치 SpriteRenderer (비워두면 자동 탐색)")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("🎨 Sprites - 방향별 스프라이트")]
    [Tooltip("아래 방향 (Front) 스프라이트")]
    [SerializeField] private Sprite spriteFront;
    [Tooltip("위 방향 (Back) 스프라이트")]
    [SerializeField] private Sprite spriteBack;
    [Tooltip("좌/우 방향 (Side) 스프라이트")]
    [SerializeField] private Sprite spriteSide;

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
    private Sprite _lastSprite;

    // 스프라이트 방향 전환용 실제 이동 delta (CatchUp 상태일 때만 계산)
    private Vector3 _actualMoveDelta;

    // Follow 상태에서의 X,Y 고정 위치 (상태 전환 시 스냅용)
    private Vector3 _followFixedPosition;

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

        // 시작 시 두두 방향으로 _smoothedMoveDirection 초기화
        Vector3 initialDelta = stateMachine.Target.position - transform.position;
        if (initialDelta.sqrMagnitude > 0.00001f)
        {
            _smoothedMoveDirection = initialDelta.normalized;
            _lastMoveDirection = _smoothedMoveDirection;
        }

        _currentTarget = GetFollowTargetPosition();
        _followFixedPosition = transform.position;
        _followFixedPosition.z = stateMachine.Target.position.z;
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

        Vector3 prevPosition = transform.position;

        UpdateMoveDirection(stateMachine.Target.position - _lastTargetPosition);
        UpdateCurrentTarget(stateMachine.CurrentState);
        UpdateMovement();

        // CatchUp 상태일 때만 실제 이동 delta 기록 (스프라이트 방향 판별용)
        if (stateMachine.CurrentState == ChichiStateMachine.ChichiState.CatchUp)
        {
            _actualMoveDelta = transform.position - prevPosition;
            _actualMoveDelta.y = 0f;
            UpdateSpriteDirectionByMovement();
        }
        // Follow/Charging 상태: 마지막 스프라이트 유지 (변경 안 함)

        _lastTargetPosition = stateMachine.Target.position;
    }

    private void HandleStateChanged(ChichiStateMachine.ChichiState state)
    {
        // 상태 전환 시 SmoothDamp velocity 리셋 (위치 튀김 방지)
        _moveVelocity = Vector3.zero;
        _targetVelocity = Vector3.zero;

        if (state == ChichiStateMachine.ChichiState.Follow)
        {
            // Follow 진입 시 현재 X,Y 고정
            _followFixedPosition = transform.position;
            _followFixedPosition.z = stateMachine.Target.position.z;
            _currentTarget = _followFixedPosition;
        }
        else if (state == ChichiStateMachine.ChichiState.CatchUp)
        {
            _currentTarget = GetFollowTargetPosition();
        }
        // Charging: 제자리 유지 (_currentTarget 변경 안 함)
    }

    private void UpdateMoveDirection(Vector3 delta)
    {
        // Y축도 포함 (스프라이트 방향 판별용)
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
        if (state == ChichiStateMachine.ChichiState.CatchUp)
        {
            Vector3 desiredTarget = GetFollowTargetPosition();
            _currentTarget = Vector3.SmoothDamp(_currentTarget, desiredTarget, ref _targetVelocity, targetSmoothTime);
        }
        else if (state == ChichiStateMachine.ChichiState.Follow)
        {
            // Follow 상태: Z만 두두 위치로 부드럽게 이동 (X,Y는 _followFixedPosition 고정)
            if (stateMachine.Target != null)
            {
                Vector3 followZTarget = _followFixedPosition;
                followZTarget.z = stateMachine.Target.position.z;
                _currentTarget = Vector3.SmoothDamp(_currentTarget, followZTarget, ref _targetVelocity, targetSmoothTime);
            }
        }
        // Charging 상태: _currentTarget 변경 안 함 (제자리 유지)
    }

    private void UpdateMovement()
    {
        // Follow 상태에서는 X,Y는 고정, Z만 부드럽게 따라감
        if (stateMachine.CurrentState == ChichiStateMachine.ChichiState.Follow)
        {
            Vector3 pos = transform.position;
            pos.z = _currentTarget.z;
            transform.position = pos;
            return;
        }

        // CatchUp / Charging 상태: SmoothDamp로 위치 이동
        float smoothTime = stateMachine.CurrentState == ChichiStateMachine.ChichiState.Charging ? chargeSmoothTime : catchUpSmoothTime;
        Vector3 nextPosition = Vector3.SmoothDamp(transform.position, _currentTarget, ref _moveVelocity, smoothTime);
        nextPosition.y = _currentTarget.y;
        nextPosition.z = _currentTarget.z;
        transform.position = nextPosition;
    }

    /// <summary>
    /// CatchUp 상태: 치치의 실제 이동 방향에 따라 스프라이트 변경
    /// - X+ → Side (flipX: true), X- → Side (flipX: false)
    /// - Z+ → Back, Z- → Front
    /// - SpriteRenderer.flipX 사용 (transform 회전 아님)
    /// </summary>
    private void UpdateSpriteDirectionByMovement()
    {
        if (spriteRenderer == null)
            return;

        float moveX = _actualMoveDelta.x;
        float moveZ = _actualMoveDelta.z;

        // Deadzone: 이동량이 너무 작으면 변경 안 함
        const float deadzone = 0.01f;
        if (Mathf.Abs(moveX) < deadzone && Mathf.Abs(moveZ) < deadzone)
            return;

        Sprite newSprite;
        bool flipX = false;

        if (Mathf.Abs(moveX) > Mathf.Abs(moveZ))
        {
            newSprite = spriteSide;
            bool movingRight = moveX > 0f;
            flipX = !movingRight; // 오른쪽: flipX true, 왼쪽: flipX false
        }
        else
        {
            if (moveZ > 0f)
            {
                newSprite = spriteBack;
            }
            else
            {
                newSprite = spriteFront;
            }
            flipX = false; // Front/Back은 flipX 사용 안 함
        }

        if (newSprite != null && newSprite != _lastSprite)
        {
            spriteRenderer.sprite = newSprite;
            _lastSprite = newSprite;
        }

        // Side 스프라이트일 때만 flipX 적용
        if (newSprite == spriteSide)
        {
            spriteRenderer.flipX = flipX;
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
            targetPosition.z + planarOffset.z);
    }
}
