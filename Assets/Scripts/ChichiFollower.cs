using UnityEngine;
using HideAndInk.Core.Interfaces;
using HideAndInk.Player;

/// <summary>
/// [이동] 치치의 이동만 담당한다.
/// - StateMachine의 상태에 따라 동작: Idle(멈춤), Follow(따라감), CatchUp(빠르게 따라감), Charging(멈춤)
/// </summary>
public class ChichiFollower : MonoBehaviour
{
    [Header("🔗 References")]
    [SerializeField] private ChichiStateMachine stateMachine;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private PlayerMovementAdapter playerMovement;

    [Header("🎨 Sprites")]
    [SerializeField] private Sprite spriteFront;
    [SerializeField] private Sprite spriteBack;
    [SerializeField] private Sprite spriteSide;

    [Header("📍 Offset")]
    [SerializeField] private Vector3 liftOffset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private Vector2 guideOffset = new Vector2(0f, 0f);

    [Header("⚡ Move Speed")]
    [SerializeField] private float idleSmoothTime = 0.3f;
    [SerializeField] private float followSmoothTime = 0.4f;
    [SerializeField] private float catchUpSmoothTime = 0.15f;
    [SerializeField] private float turnSpeed = 8f;

    [Header("🧭 Sprite")]
    [SerializeField] private float directionAngleThreshold = 45f;
    [SerializeField] private float minSpriteChangeInterval = 0.15f;

    private Vector3 _smoothedPlayerDirection = Vector3.forward;
    private Vector3 _moveVelocity;
    private Sprite _lastSprite;
    private float _spriteChangeTimer;
    private int _currentDirectionZone = 0;
    private Vector3 _actualMoveDelta;

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

        MoveDirection playerDir = playerMovement != null ? playerMovement.Direction : MoveDirection.Down;
        Vector3 playerForward = MoveDirectionToVector(playerDir);

        // Player 방향 부드럽게 업데이트 (충전 중에는 업데이트 안 함)
        if (stateMachine.CurrentState != ChichiStateMachine.ChichiState.Charging)
        {
            _smoothedPlayerDirection = Vector3.Lerp(_smoothedPlayerDirection, playerForward, turnSpeed * Time.deltaTime);
            if (_smoothedPlayerDirection.sqrMagnitude > 0.00001f)
                _smoothedPlayerDirection.Normalize();
        }

        // 상태에 따른 동작 분기
        switch (stateMachine.CurrentState)
        {
            case ChichiStateMachine.ChichiState.Charging:
                // 충전 중: 완전히 멈춤, 스프라이트만 업데이트
                UpdateSpriteByPlayerDirection(playerDir);
                return;

            case ChichiStateMachine.ChichiState.Idle:
                // Player가 가까움: 멈춤, 스프라이트만 업데이트
                UpdateSpriteByPlayerDirection(playerDir);
                return;

            case ChichiStateMachine.ChichiState.Follow:
            case ChichiStateMachine.ChichiState.CatchUp:
                // 따라감: 이동 + 스프라이트 업데이트
                Vector3 prevPosition = transform.position;
                UpdateMovement();

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
                break;
        }
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
        float smoothTime = stateMachine.CurrentState == ChichiStateMachine.ChichiState.CatchUp
            ? catchUpSmoothTime
            : followSmoothTime;

        Vector3 nextPosition = Vector3.SmoothDamp(transform.position, targetPosition, ref _moveVelocity, smoothTime);
        nextPosition.y = targetPosition.y;
        transform.position = nextPosition;
    }

    private Vector3 GetFollowTargetPosition()
    {
        Vector3 targetPosition = stateMachine.Target.position;

        // CatchUp: Player 뒤로 followDistance만큼 떨어짐
        // Follow: Player 위치를 직접 타겟으로 (Idle 전환은 StateMachine이 담당)
        if (stateMachine.CurrentState == ChichiStateMachine.ChichiState.CatchUp)
        {
            Vector3 behindDirection = -_smoothedPlayerDirection;
            if (behindDirection.sqrMagnitude < 0.00001f)
                behindDirection = Vector3.back;

            Vector2 dist = stateMachine.FollowDistance;
            Vector3 planarOffset = new Vector3(
                behindDirection.x * dist.x,
                0f,
                behindDirection.z * dist.y
            );

            return new Vector3(
                targetPosition.x + planarOffset.x + guideOffset.x,
                targetPosition.y + liftOffset.y + guideOffset.y,
                targetPosition.z + planarOffset.z);
        }

        // Follow: Player 위치 + 작은 오프셋 (가이드만 적용)
        return new Vector3(
            targetPosition.x + guideOffset.x,
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
            return moveX > 0f ? 1 : 3;
        else
            return moveZ > 0f ? 2 : 0;
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

    private void UpdateSpriteByPlayerDirection(MoveDirection playerDir)
    {
        if (spriteRenderer == null)
            return;

        Vector3 toChichi = (transform.position - stateMachine.Target.position).normalized;
        toChichi.y = 0f;
        Vector3 playerMoveDir = MoveDirectionToVector(playerDir);
        float alignment = Vector3.Dot(playerMoveDir, toChichi);
        bool facingPlayer = alignment > 0f;

        Sprite newSprite;
        bool flipX = false;

        if (facingPlayer)
        {
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
