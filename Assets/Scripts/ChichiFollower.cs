using UnityEngine;

/// <summary>
/// [이동] 치치의 이동만 담당한다.
/// - Follow 상태: 두두를 부드럽게 따라감 (느린 속도, X/Y/Z 모두)
/// - CatchUp 상태: 두두를 빠르게 따라잡음 (빠른 속도)
/// - Charging 상태: 두두를 매우 부드럽게 따라감 (충전 중에도 위치 유지)
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
    [Tooltip("추가 가이드 오프셋 (X, Y)")]
    [SerializeField] private Vector2 guideOffset = new Vector2(0f, 0f);

    [Header("⚡ Move Speed - 이동 속도 설정")]
    [Tooltip("Follow 상태 부드러움 (높을수록 느리고 부드러움)")]
    [SerializeField] private float followSmoothTime = 0.4f;
    [Tooltip("CatchUp 상태 부드러움 (낮을수록 빠름)")]
    [SerializeField] private float catchUpSmoothTime = 0.15f;
    [Tooltip("충전 중 위치 보정 부드러움")]
    [SerializeField] private float chargeSmoothTime = 0.2f;
    [Tooltip("방향 전환 속도 (높을수록 빠르게 방향 전환)")]
    [SerializeField] private float turnSpeed = 8f;

    private Vector3 _lastTargetPosition;
    private Vector3 _lastMoveDirection = Vector3.forward;
    private Vector3 _smoothedMoveDirection = Vector3.forward;
    private Vector3 _moveVelocity;
    private Sprite _lastSprite;

    // 스프라이트 방향 전환용 실제 이동 delta
    private Vector3 _actualMoveDelta;

    // 스프라이트 토글링 방지
    private float _spriteChangeTimer;
    [SerializeField] private float minSpriteChangeInterval = 0.15f;
    [SerializeField] private float directionAngleThreshold = 30f; // 대각선 임계각 (도)

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

        // 이동 방향 업데이트
        UpdateMoveDirection(stateMachine.Target.position - _lastTargetPosition);

        // 목표 위치 계산 및 이동 (통합)
        UpdateMovement();

        // 스프라이트 방향 업데이트 (타이머로 토글링 방지)
        _actualMoveDelta = transform.position - prevPosition;
        _actualMoveDelta.y = 0f;

        _spriteChangeTimer -= Time.deltaTime;
        if (_spriteChangeTimer <= 0f)
        {
            if (_actualMoveDelta.sqrMagnitude > 0.001f)
            {
                // 이동 중이면 이동 방향으로 스프라이트 변경
                UpdateSpriteDirectionByMovement();
            }
            else
            {
                // 정지 상태면 두두를 바라보도록 스프라이트 변경
                UpdateSpriteDirectionByTarget();
            }
        }

        _lastTargetPosition = stateMachine.Target.position;
    }

    private void HandleStateChanged(ChichiStateMachine.ChichiState state)
    {
        // 상태 전환 시 SmoothDamp velocity 리셋 (위치 튀김 방지)
        _moveVelocity = Vector3.zero;
    }

    private void UpdateMoveDirection(Vector3 delta)
    {
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

    /// <summary>
    /// 통합 이동 시스템: 모든 상태에서 동일한 목표 위치로 이동, smoothTime만 다르게 적용
    /// </summary>
    private void UpdateMovement()
    {
        Vector3 targetPosition = GetFollowTargetPosition();

        // 상태별 smoothTime 선택
        float smoothTime;
        switch (stateMachine.CurrentState)
        {
            case ChichiStateMachine.ChichiState.Follow:
                smoothTime = followSmoothTime;
                break;
            case ChichiStateMachine.ChichiState.CatchUp:
                smoothTime = catchUpSmoothTime;
                break;
            case ChichiStateMachine.ChichiState.Charging:
                smoothTime = chargeSmoothTime;
                break;
            default:
                smoothTime = followSmoothTime;
                break;
        }

        // SmoothDamp로 부드러운 이동
        Vector3 nextPosition = Vector3.SmoothDamp(transform.position, targetPosition, ref _moveVelocity, smoothTime);
        nextPosition.y = targetPosition.y;
        transform.position = nextPosition;
    }

    /// <summary>
    /// Follow/Charging 상태: 두두를 바라보도록 스프라이트 변경
    /// - 두두가 좌/우 → Side (flipX로 좌우 반전)
    /// - 두두가 앞/뒤 → Front/Back
    /// - SpriteRenderer.flipX 사용 (transform 회전 아님)
    /// </summary>
    private void UpdateSpriteDirectionByTarget()
    {
        if (spriteRenderer == null || stateMachine.Target == null)
            return;

        Vector3 toTarget = stateMachine.Target.position - transform.position;
        toTarget.y = 0f;

        float absX = Mathf.Abs(toTarget.x);
        float absZ = Mathf.Abs(toTarget.z);

        // Deadzone: 너무 가까우면 변경 안 함
        const float deadzone = 0.3f;
        if (absX < deadzone && absZ < deadzone)
            return;

        // 임계각 기반 방향 판별 (대각선에서 토글 방지)
        float angle = Mathf.Atan2(absX, absZ) * Mathf.Rad2Deg;
        Sprite newSprite;
        bool flipX = false;

        if (angle > directionAngleThreshold)
        {
            // 좌/우 → Side
            newSprite = spriteSide;
            flipX = toTarget.x > 0f; // 두두가 오른쪽이면 flipX true
        }
        else
        {
            // 앞/뒤 → Front/Back
            newSprite = toTarget.z > 0f ? spriteBack : spriteFront;
            flipX = false;
        }

        if (newSprite != null && newSprite != _lastSprite)
        {
            spriteRenderer.sprite = newSprite;
            _lastSprite = newSprite;
            _spriteChangeTimer = minSpriteChangeInterval;
        }

        // Side 스프라이트일 때만 flipX 적용
        if (newSprite == spriteSide)
        {
            spriteRenderer.flipX = flipX;
        }
    }

    /// <summary>
    /// 이동 상태: 치치의 실제 이동 방향에 따라 스프라이트 변경
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
        const float deadzone = 0.02f;
        if (Mathf.Abs(moveX) < deadzone && Mathf.Abs(moveZ) < deadzone)
            return;

        // 임계각 기반 방향 판별 (대각선에서 토글 방지)
        float absX = Mathf.Abs(moveX);
        float absZ = Mathf.Abs(moveZ);
        float angle = Mathf.Atan2(absX, absZ) * Mathf.Rad2Deg;

        Sprite newSprite;
        bool flipX = false;

        if (angle > directionAngleThreshold)
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
            _spriteChangeTimer = minSpriteChangeInterval;
        }

        // Side 스프라이트일 때만 flipX 적용
        if (newSprite == spriteSide)
        {
            spriteRenderer.flipX = flipX;
        }
    }

    /// <summary>
    /// 따라가기 목표 위치 계산
    /// - 두두 위치 + 방향 기반 오프셋
    /// - Z는 두두 위치 정확히 사용
    /// </summary>
    private Vector3 GetFollowTargetPosition()
    {
        float followDistance = stateMachine.MaxFollowDistance;

        Vector3 behindDirection = -_smoothedMoveDirection;
        Vector3 sideDirection = new Vector3(-behindDirection.z, 0f, behindDirection.x);

        if (behindDirection.sqrMagnitude < 0.00001f)
        {
            behindDirection = Vector3.back;
            sideDirection = Vector3.right;
        }

        Vector3 targetPosition = stateMachine.Target.position;
        // X,Y만 오프셋 적용, Z는 두두 위치 정확히 사용
        Vector3 planarOffset = behindDirection * followDistance + sideDirection * sideOffset + new Vector3(guideOffset.x, 0f, 0f);

        return new Vector3(
            targetPosition.x + planarOffset.x,
            targetPosition.y + liftOffset.y + guideOffset.y,
            targetPosition.z);
    }
}
