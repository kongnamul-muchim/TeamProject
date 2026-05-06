using UnityEngine;
using HideAndInk.Core.Interfaces;
using HideAndInk.Player;

/// <summary>
/// [이동] 치치의 이동만 담당한다.
/// - 거리 이내: 가만히 있음 (스프라이트만 Player 방향에 따라 변경)
/// - 거리 초과: Player의 뒤로 부드럽게 이동
/// - 충전 중(Player가 Collider 안에 있음): 완전히 멈춤
/// </summary>
public class ChichiFollower : MonoBehaviour
{
    [Header("🔗 References - 연결할 컴포넌트")]
    [SerializeField] private ChichiStateMachine stateMachine;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private PlayerMovementAdapter playerMovement;

    [Header("🎨 Sprites - 방향별 스프라이트")]
    [SerializeField] private Sprite spriteFront;
    [SerializeField] private Sprite spriteBack;
    [SerializeField] private Sprite spriteSide;

    [Header("📍 Distance - 거리 설정")]
    [Tooltip("Player와 치치의 최대 거리. 이 거리 이내면 치치는 가만히 있음")]
    [SerializeField] private float maxFollowDistance = 5f;
    [Tooltip("Player가 최대 거리를 벗어났을 때 따라갈 Player 뒤의 거리")]
    [SerializeField] private float followBehindDistance = 2f;
    [Tooltip("측면으로 벗어난 거리 (Player 이동 시에만 적용)")]
    [SerializeField] private float sideOffset = 0.8f;
    [Tooltip("수직 (Y) 오프셋")]
    [SerializeField] private Vector3 liftOffset = new Vector3(0f, 0.5f, 0f);
    [Tooltip("추가 가이드 오프셋 (X, Y)")]
    [SerializeField] private Vector2 guideOffset = new Vector2(0f, 0f);

    [Header("⚡ Move Speed - 이동 속도 설정")]
    [Tooltip("따라가기 부드러움 (높을수록 느리고 부드러움)")]
    [SerializeField] private float followSmoothTime = 0.4f;
    [Tooltip("빠르게 따라잡기 부드러움 (낮을수록 빠름)")]
    [SerializeField] private float catchUpSmoothTime = 0.15f;
    [Tooltip("방향 전환 속도 (높을수록 빠르게 방향 전환)")]
    [SerializeField] private float turnSpeed = 8f;

    [Header("🧭 Sprite Direction - 스프라이트 방향 설정")]
    [Tooltip("측면(Side)으로 판정할 임계각 (도)")]
    [SerializeField] private float directionAngleThreshold = 45f;
    [Tooltip("스프라이트 변경 최소 간격 (초)")]
    [SerializeField] private float minSpriteChangeInterval = 0.15f;

    private Vector3 _smoothedPlayerDirection = Vector3.forward;
    private Vector3 _moveVelocity;
    private Sprite _lastSprite;
    private float _spriteChangeTimer;
    private int _currentDirectionZone = 0;
    private Vector3 _actualMoveDelta;
    private float _distanceX;
    private float _distanceZ;

    // Charging 상태 토글 방지용 히스테리시스
    private bool _isCurrentlyCharging = false;
    private float _chargeStateCooldown = 0f;
    [SerializeField] private float chargeStateCooldownTime = 0.5f;

    private void Awake()
    {
        if (stateMachine == null)
            stateMachine = GetComponent<ChichiStateMachine>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (playerMovement == null)
            playerMovement = FindObjectOfType<PlayerMovementAdapter>();
    }

    private void Start()
    {
        if (stateMachine == null || stateMachine.Target == null)
            return;

        if (playerMovement != null)
            _smoothedPlayerDirection = MoveDirectionToVector(playerMovement.Direction);

        stateMachine.OnStateChanged += HandleStateChanged;
    }

    private void OnDestroy()
    {
        if (stateMachine != null)
            stateMachine.OnStateChanged -= HandleStateChanged;
    }

    private void LateUpdate()
    {
        if (stateMachine == null || stateMachine.Target == null)
            return;

        // Player의 스프라이트 방향 가져오기
        MoveDirection playerDir = playerMovement != null ? playerMovement.Direction : MoveDirection.Down;
        Vector3 playerForward = MoveDirectionToVector(playerDir);

        // Player 방향 부드럽게 업데이트 (Charging 중에는 업데이트 안 함)
        if (!stateMachine.IsTouchingTank)
        {
            _smoothedPlayerDirection = Vector3.Lerp(_smoothedPlayerDirection, playerForward, turnSpeed * Time.deltaTime);
            if (_smoothedPlayerDirection.sqrMagnitude > 0.00001f)
                _smoothedPlayerDirection.Normalize();
        }

        // Player와 치치 거리 계산 (X, Z 각각 독립 체크)
        _distanceX = Mathf.Abs(stateMachine.Target.position.x - transform.position.x);
        _distanceZ = Mathf.Abs(stateMachine.Target.position.z - transform.position.z);

        // 충전 상태 판단 (히스테리시로 토글 방지)
        bool shouldCharge = stateMachine.IsTouchingTank && CanReceiveInk();
        _chargeStateCooldown -= Time.deltaTime;

        if (shouldCharge && !_isCurrentlyCharging && _chargeStateCooldown <= 0f)
        {
            _isCurrentlyCharging = true;
            _chargeStateCooldown = chargeStateCooldownTime;
            _moveVelocity = Vector3.zero;
        }
        else if (!shouldCharge && _isCurrentlyCharging && _chargeStateCooldown <= 0f)
        {
            _isCurrentlyCharging = false;
            _chargeStateCooldown = chargeStateCooldownTime;
        }

        // 충전 중이면 완전히 멈춤 (스프라이트만 Player 방향으로 변경)
        if (_isCurrentlyCharging)
        {
            UpdateSpriteByPlayerDirection(playerDir);
            return;
        }

        // X 또는 Z 중 하나라도 maxFollowDistance를 벗어나면 따라옴
        if (_distanceX <= maxFollowDistance && _distanceZ <= maxFollowDistance)
        {
            UpdateSpriteByPlayerDirection(playerDir);
            return;
        }

        // 최대 거리를 벗어났을 때만 Player의 뒤로 이동
        Vector3 prevPosition = transform.position;
        UpdateMovement();

        // 이동 중 스프라이트 업데이트
        _actualMoveDelta = transform.position - prevPosition;
        _actualMoveDelta.y = 0f;

        bool isMoving = _actualMoveDelta.sqrMagnitude > 0.001f;
        _spriteChangeTimer -= Time.deltaTime;

        if (_spriteChangeTimer <= 0f)
        {
            if (isMoving)
            {
                int newZone = CalculateDirectionZone(_actualMoveDelta);
                if (newZone != _currentDirectionZone)
                {
                    _currentDirectionZone = newZone;
                    UpdateSpriteDirectionByZone(_currentDirectionZone);
                    _spriteChangeTimer = minSpriteChangeInterval;
                }
            }
            else
            {
                UpdateSpriteByPlayerDirection(playerDir);
                _spriteChangeTimer = minSpriteChangeInterval;
            }
        }
    }

    private bool CanReceiveInk()
    {
        var listener = stateMachine.GetComponent<IChichiStateListener>();
        return listener != null && listener.CanReceiveInk();
    }

    private Vector3 MoveDirectionToVector(MoveDirection dir)
    {
        return dir switch
        {
            MoveDirection.Down => Vector3.forward,
            MoveDirection.Up => Vector3.back,
            MoveDirection.Left => Vector3.left,
            MoveDirection.Right => Vector3.right,
            _ => Vector3.forward
        };
    }

    private void UpdateMovement()
    {
        Vector3 targetPosition = GetFollowTargetPosition();

        // 거리가 멀수록 빠르게 따라감 (X, Z 중 큰 값 기준)
        float maxDistance = Mathf.Max(_distanceX, _distanceZ);
        float smoothTime = maxDistance > maxFollowDistance * 1.5f ? catchUpSmoothTime : followSmoothTime;

        Vector3 nextPosition = Vector3.SmoothDamp(transform.position, targetPosition, ref _moveVelocity, smoothTime);
        nextPosition.y = targetPosition.y;
        transform.position = nextPosition;
    }

    private Vector3 GetFollowTargetPosition()
    {
        Vector3 behindDirection = -_smoothedPlayerDirection;
        bool playerIsMoving = playerMovement != null && playerMovement.IsMoving;
        float currentSideOffset = playerIsMoving ? sideOffset : 0f;
        Vector3 sideDirection = new Vector3(-behindDirection.z, 0f, behindDirection.x);

        if (behindDirection.sqrMagnitude < 0.00001f)
        {
            behindDirection = Vector3.back;
            sideDirection = Vector3.right;
        }

        Vector3 targetPosition = stateMachine.Target.position;
        Vector3 planarOffset = behindDirection * followBehindDistance + sideDirection * currentSideOffset + new Vector3(guideOffset.x, 0f, 0f);

        return new Vector3(
            targetPosition.x + planarOffset.x,
            targetPosition.y + liftOffset.y + guideOffset.y,
            targetPosition.z);
    }

    private int CalculateDirectionZone(Vector3 delta)
    {
        float moveX = delta.x;
        float moveZ = delta.z;

        if (Mathf.Abs(moveX) < 0.02f && Mathf.Abs(moveZ) < 0.02f)
            return _currentDirectionZone;

        float absX = Mathf.Abs(moveX);
        float absZ = Mathf.Abs(moveZ);
        float angle = Mathf.Atan2(absX, absZ) * Mathf.Rad2Deg;

        if (angle > directionAngleThreshold)
            return moveX > 0f ? 1 : 3; // 1=Right, 3=Left
        else
            return moveZ > 0f ? 2 : 0; // 2=Back, 0=Front
    }

    private void UpdateSpriteDirectionByZone(int zone)
    {
        if (spriteRenderer == null)
            return;

        Sprite newSprite;
        bool flipX = false;

        switch (zone)
        {
            case 0: newSprite = spriteFront; break;
            case 1: newSprite = spriteSide; flipX = true; break;
            case 2: newSprite = spriteBack; break;
            case 3: newSprite = spriteSide; flipX = false; break;
            default: newSprite = spriteFront; break;
        }

        if (newSprite != null && newSprite != _lastSprite)
        {
            spriteRenderer.sprite = newSprite;
            _lastSprite = newSprite;
        }

        spriteRenderer.flipX = flipX;
    }

    /// <summary>
    /// Player의 방향에 따라 치치 스프라이트 변경
    /// - Player가 치치 반대 방향으로 움직이면 → Player와 같은 방향 (Player의 뒤를 봄)
    /// - Player가 치치 방향으로 움직이면 → Player를 바라봄 (Player의 반대 방향)
    /// </summary>
    private void UpdateSpriteByPlayerDirection(MoveDirection playerDir)
    {
        if (spriteRenderer == null)
            return;

        // Player가 치치 방향으로 움직이는지 확인
        Vector3 toChichi = (transform.position - stateMachine.Target.position).normalized;
        toChichi.y = 0f;
        Vector3 playerMoveDir = MoveDirectionToVector(playerDir);
        float alignment = Vector3.Dot(playerMoveDir, toChichi);

        // alignment > 0: Player가 치치 방향으로 움직임 → 치치는 Player를 바라봄
        // alignment < 0: Player가 치치 반대 방향으로 움직임 → 치치는 Player와 같은 방향
        bool facingPlayer = alignment > 0f;

        Sprite newSprite;
        bool flipX = false;

        if (facingPlayer)
        {
            // Player를 바라봄 (Player의 반대 방향)
            switch (playerDir)
            {
                case MoveDirection.Down: newSprite = spriteFront; break;
                case MoveDirection.Up: newSprite = spriteBack; break;
                case MoveDirection.Left: newSprite = spriteSide; flipX = true; break;
                case MoveDirection.Right: newSprite = spriteSide; flipX = false; break;
                default: newSprite = spriteFront; break;
            }
        }
        else
        {
            // Player와 같은 방향 (Player의 뒤를 봄)
            switch (playerDir)
            {
                case MoveDirection.Down: newSprite = spriteBack; break;
                case MoveDirection.Up: newSprite = spriteFront; break;
                case MoveDirection.Left: newSprite = spriteSide; flipX = false; break;
                case MoveDirection.Right: newSprite = spriteSide; flipX = true; break;
                default: newSprite = spriteFront; break;
            }
        }

        if (newSprite != null && newSprite != _lastSprite)
        {
            spriteRenderer.sprite = newSprite;
            _lastSprite = newSprite;
        }

        spriteRenderer.flipX = flipX;
    }

    private void HandleStateChanged(ChichiStateMachine.ChichiState state)
    {
        _moveVelocity = Vector3.zero;
    }
}
