using UnityEngine;

/// <summary>
/// [이동] 치치의 이동만 담당한다.
/// - Follow 상태: 움직이지 않음 (두두 근처에서 대기)
/// - CatchUp 상태: 두두 뒤로 부드럽게 따라감
/// - Charging 상태: 제자리 유지
/// - 이동 방향에 따라 스프라이트 변경 (Front/Back/Side)
/// - Side 스프라이트는 좌/우에 따라 Y Rotation 0/180 반전
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
    private bool _facingRight = true;
    private Sprite _lastSprite;

    // 스프라이트 방향 전환용 실제 이동 delta
    private Vector3 _actualMoveDelta;

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

        Vector3 prevPosition = transform.position;

        UpdateMoveDirection(stateMachine.Target.position - _lastTargetPosition);
        UpdateCurrentTarget(stateMachine.CurrentState);
        UpdateMovement();

        // 치치의 실제 이동 delta 기록 (스프라이트 방향 판별용)
        _actualMoveDelta = transform.position - prevPosition;
        _actualMoveDelta.y = 0f; // Y축(화면 위아래) 이동은 스프라이트 방향에 영향 없음

        UpdateSpriteDirection();
        _lastTargetPosition = stateMachine.Target.position;
    }

    private void HandleStateChanged(ChichiStateMachine.ChichiState state)
    {
        UpdateCurrentTarget(state);
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
        Vector3 desiredTarget = transform.position;

        if (state == ChichiStateMachine.ChichiState.CatchUp)
        {
            desiredTarget = GetFollowTargetPosition();
        }
        else if (state == ChichiStateMachine.ChichiState.Charging)
        {
            desiredTarget = transform.position;
        }

        // Follow 상태에서도 Z는 두두 위치로 설정 (부드러운 Z축 이동용)
        if (stateMachine.Target != null)
        {
            desiredTarget.z = stateMachine.Target.position.z;
        }

        _currentTarget = Vector3.SmoothDamp(_currentTarget, desiredTarget, ref _targetVelocity, targetSmoothTime);
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

        float smoothTime = stateMachine.CurrentState == ChichiStateMachine.ChichiState.Charging ? chargeSmoothTime : catchUpSmoothTime;
        Vector3 nextPosition = Vector3.SmoothDamp(transform.position, _currentTarget, ref _moveVelocity, smoothTime);
        nextPosition.y = _currentTarget.y;
        nextPosition.z = _currentTarget.z;
        transform.position = nextPosition;
    }

    /// <summary>
    /// 이동 방향에 따라 스프라이트 변경 + Side는 Y Rotation 반전
    /// - Z+ (앞/위) → Back 스프라이트
    /// - Z- (뒤/아래) → Front 스프라이트
    /// - X+ (오른쪽) → Side 스프라이트 + Y Rotation 0
    /// - X- (왼쪽) → Side 스프라이트 + Y Rotation 180
    /// </summary>
    private void UpdateSpriteDirection()
    {
        if (spriteRenderer == null)
            return;

        // 치치의 실제 이동 delta 사용 (X-Z 평면, Y는 높이축이므로 무시)
        float moveX = _actualMoveDelta.x;
        float moveZ = _actualMoveDelta.z;

        // Deadzone: 이동량이 너무 작으면 변경 안 함 (흔들림 방지)
        const float deadzone = 0.001f;
        if (Mathf.Abs(moveX) < deadzone && Mathf.Abs(moveZ) < deadzone)
            return;

        // 방향 판별: |X| > |Z| → 좌/우, |Z| > |X| → 앞/뒤
        Sprite newSprite;
        bool flipY = false;

        if (Mathf.Abs(moveX) > Mathf.Abs(moveZ))
        {
            // 좌/우 이동 → Side 스프라이트
            newSprite = spriteSide;
            bool shouldFaceRight = moveX > 0f;
            flipY = !shouldFaceRight; // 오른쪽: 0, 왼쪽: 180
        }
        else
        {
            // 앞/뒤 이동
            if (moveZ > 0f)
            {
                newSprite = spriteBack; // 앞(위)
            }
            else
            {
                newSprite = spriteFront; // 뒤(아래)
            }
            flipY = false; // Front/Back은 반전 없음
        }

        // 스프라이트 변경
        if (newSprite != null && newSprite != _lastSprite)
        {
            spriteRenderer.sprite = newSprite;
            _lastSprite = newSprite;
        }

        // Side일 때만 Y Rotation 반전
        if (newSprite == spriteSide)
        {
            Vector3 rotation = spriteRenderer.transform.localEulerAngles;
            rotation.y = flipY ? 180f : 0f;
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
