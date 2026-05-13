using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Enemy.Movement;
using HideAndInk.Core.Enemy.AI;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using HideAndInk.ParallaxSystem;

namespace HideAndInk.Core.Enemy
{
    /// <summary>
    /// Enemy AI 공통 베이스 컨트롤러
    /// 3D 환경 (X-Z 평면 이동, Y축 고정)
    /// 보스/일반/정예 몬스터의 공통 기능 제공
    /// Ground 경계 캐싱 및 이동 제한 포함
    /// </summary>
    public abstract class EnemyAIController : MonoBehaviour, IEnemy
    {
        [Header("이동 물리 설정 (공통)")]
        [Tooltip("기본 이동 속도")]
        [SerializeField] protected float moveSpeed = 3f;
        [Tooltip("가속도 (값이 클수록 빠르게 최고속도 도달, MoveTowards 기준)")]
        [SerializeField] protected float acceleration = 20f;
        [Tooltip("마찰력 (0~1, 1에 가까울수록 미끄러짐)")]
        [SerializeField] protected float friction = 0.9f;
        [Tooltip("최대 이동 속도 제한")]
        [SerializeField] protected float maxSpeed = 5f;

        [Header("시야 설정")]
        [Tooltip("Enemy_Forward 자식 Transform (시야 방향 기준)")]
        [SerializeField] protected Transform enemyForward; // Enemy_Forward 자식 Transform
        [Tooltip("시야 회전 속도")]
        [SerializeField] protected float viewRotationSpeed = 5f;

        [Header("방향 설정")]
        [Tooltip("스프라이트 에셋이 Y=0(미반전) 상태에서 왼쪽을 보고 있는지 여부. true=왼쪽, false=오른쪽")]
        [System.NonSerialized] protected bool isDefaultFacingLeft;

        [Header("Ground 제한 설정")]
        [Tooltip("Ground 레이어 마스크")]
        [SerializeField] protected LayerMask groundLayer; // Ground 레이어
        [Tooltip("이동 방향 앞쪽 Ground 체크 거리")]
        [SerializeField] protected float groundCheckDistance = 0.5f; // 이동 방향 앞쪽 Ground 체크 거리
        [Tooltip("Ground 체크 반경")]
        [SerializeField] protected float groundCheckRadius = 0.3f; // Ground 체크 반경
        [Tooltip("Ground 경계 스캔 최대 거리")]
        [SerializeField] protected float groundScanDistance = 50f; // Ground 경계 스캔 최대 거리
        [Tooltip("Ground 경계 스캔 간격")]
        [SerializeField] protected float groundScanStep = 1f; // Ground 경계 스캔 간격

        // 컴포넌트 참조 (런타임 캐싱, 직렬화 불필요)
        [System.NonSerialized] protected IEnemyMovement _movement;
        [System.NonSerialized] protected Transform _playerTransform;
        [System.NonSerialized] protected HideAndInk.Player.CamouflageAdapter _camouflageAdapter;

        // 초기 위치 (이어하기 시 복원용)
        protected Vector3 _initialPosition;
        protected Quaternion _initialRotation;

        // Ground 경계 정보
        protected GroundBounds _groundBounds;
        protected bool _isGroundBoundsScanned;

        // 상태
        protected bool _isInitialized;
        protected bool _isActive = true;

        // 방향 전환 쿨타임
        protected float _directionChangeCooldown = 0.1f; // 방향 전환 후 고정 시간 (초, 0.3→0.1로 감소: 반응성 향상)
        protected float _directionChangeTimer = 0f;
        protected MoveDirection _lastAppliedDirection = MoveDirection.Left;

        #region IEnemy 구현

        public Transform Transform => transform;
        public Vector3 Position => transform.position;
        public abstract EnemyType Type { get; }
        public bool IsActive => _isActive && gameObject.activeInHierarchy;

        /// <summary>
        /// 현재 바라보는 방향 (Enemy_Forward 기준)
        /// enemyForward가 자식이면 Enemy 본체 방향을 기준으로 forward 반환
        /// </summary>
        public Vector3 Forward
        {
            get
            {
                if (enemyForward != null)
                {
                    return enemyForward.forward;
                }
                return transform.forward; // enemyForward가 없으면 Enemy 본체 방향
            }
        }

        public float Speed => _movement?.Speed ?? moveSpeed;

        #endregion

        protected virtual void Awake()
        {
            // ★ Awake에서 초기 위치 저장 (Start보다 먼저 실행되며,
            //    ContinueZoneHandler/GameManager가 ResetToInitialState()를 호출해도 안전)
            _initialPosition = transform.position;
            _initialRotation = transform.rotation;
            FindPlayer();
            InitializeMovement();

            // ★ Enemy Sorting Layer를 midground로 설정
            //    배경 레이어(Default/Background/distant/distant02 = 0~3)보다 위,
            //    Player 레이어(6)보다 아래에서 렌더링되도록 함
            //    (Swordfish는 씬에서 이미 midground로 설정되어 있음)
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sortingLayerName != "midground" && sr.sortingLayerName != "Player")
            {
                sr.sortingLayerName = "midground";
                sr.sortingOrder = 0;

                // ★ 2차: Player와 동일한 SortingOrderUpdater 설정
                //    Player: baseOrder=0, precision=10, yOffset=0.5
                //    Enemy도 동일 설정 → 같은 Y에서 같은 order → Z depth로 판정
                if (GetComponent<SortingOrderUpdater>() == null)
                {
                    var updater = gameObject.AddComponent<SortingOrderUpdater>();
                    updater.SetTrackingTarget(transform);
                    updater.SetSortingReference(transform);
                    updater.SetBaseOrder(0);
                    updater.SetPrecision(10);
                    updater.SetYOffset(0.5f);
                    updater.SetUpdateMode(SortingOrderUpdater.UpdateMode.LateUpdate);
                }
            }
        }

        protected virtual void Start()
        {
            ScanGroundBounds();
            InitializeAI();
        }

        protected virtual void Update()
        {
            if (!_isActive) return;

            UpdateAI(Time.deltaTime);
            UpdateMovement(Time.deltaTime);
            UpdateViewDirection();
        }

        /// <summary>
        /// AI 초기화 (상속 클래스에서 구현)
        /// </summary>
        protected virtual void InitializeAI()
        {
            _isInitialized = true;
        }

        /// <summary>
        /// AI 업데이트 (상속 클래스에서 구현)
        /// </summary>
        protected virtual void UpdateAI(float deltaTime)
        {
        }

        /// <summary>
        /// 이동 시스템 초기화 (Config Object Pattern)
        /// </summary>
        protected virtual void InitializeMovement()
        {
            var config = new EnemyMovementConfig(
                speed: moveSpeed,
                acceleration: acceleration,
                friction: friction,
                maxSpeed: maxSpeed,
                groundLayer: groundLayer,
                groundCheckDistance: groundCheckDistance,
                groundCheckRadius: groundCheckRadius);

            _movement = new EnemyMovement(this, config);
        }

        /// <summary>
        /// Ground 경계 스캔
        /// Enemy 시작 시 한 번만 실행
        /// </summary>
        protected virtual void ScanGroundBounds()
        {
            if (_movement == null) return;

            _groundBounds = _movement.ScanGroundBounds(groundScanDistance, groundScanStep);
            _isGroundBoundsScanned = true;

#if UNITY_EDITOR
            Debug.Log($"[EnemyAIController] Ground bounds scanned: X({_groundBounds.MinX:F1} ~ {_groundBounds.MaxX:F1}), Z({_groundBounds.MinZ:F1} ~ {_groundBounds.MaxZ:F1})");
#endif
        }

        /// <summary>
        /// 이동 처리 (X-Z 평면)
        /// Ground 체크를 통해 벗어나지 않도록 제한
        /// </summary>
        protected virtual void UpdateMovement(float deltaTime)
        {
            if (_movement == null) return;

            _movement.Update(deltaTime);

            // 속도를 실제 Transform에 적용 (X-Z 평면, Y축 고정)
            if (_movement.IsMoving)
            {
                // 이동 방향 앞쪽에 Ground가 있는지 확인
                if (!_movement.IsGroundAhead())
                {
                    // Ground가 없으면 이동 중지 및 방향 전환
                    _movement.Stop();
                    OnGroundEdgeReached();
                    return;
                }

                Vector3 newPosition = transform.position;
                newPosition.x += _movement.Velocity.x * deltaTime;
                newPosition.z += _movement.Velocity.z * deltaTime;
                // Y축은 고정
                transform.position = newPosition;
            }
        }

        /// <summary>
        /// Ground 끝에 도달했을 때 호출
        /// 상속 클래스에서 방향 전환 등의 처리 가능
        /// </summary>
        protected virtual void OnGroundEdgeReached()
        {
#if UNITY_EDITOR
            Debug.Log($"[EnemyAIController] Ground edge reached at {transform.position}");
#endif
        }

        /// <summary>
        /// 목표 위치를 Ground 범위 내로 제한
        /// </summary>
        protected Vector3 ClampToGroundBounds(Vector3 targetPosition)
        {
            if (!_isGroundBoundsScanned) return targetPosition;
            return _groundBounds.ClampXZ(targetPosition);
        }

        /// <summary>
        /// X 위치를 Ground 범위 내로 제한
        /// </summary>
        protected float ClampXToGroundBounds(float x)
        {
            if (!_isGroundBoundsScanned) return x;
            return _groundBounds.ClampX(x);
        }

        /// <summary>
        /// Z 위치를 Ground 범위 내로 제한
        /// </summary>
        protected float ClampZToGroundBounds(float z)
        {
            if (!_isGroundBoundsScanned) return z;
            return _groundBounds.ClampZ(z);
        }

        /// <summary>
        /// 시야 방향 업데이트
        /// Enemy 본체 스프라이트: 오른쪽(0) 또는 왼쪽(180)으로 즉시 전환
        /// enemyForward는 자식이므로 Enemy 본체만 회전하면 자동으로 따라감
        /// 방향 전환 쿨타임 적용 (플리커링 방지)
        /// </summary>
        protected virtual void UpdateViewDirection()
        {
            // 쿨타임 감소
            if (_directionChangeTimer > 0f)
            {
                _directionChangeTimer -= Time.deltaTime;
            }

            // 쿨타임 중이면 방향 전환 안 함 (현재 방향 유지)
            if (_directionChangeTimer > 0f) return;

            // 이동 중이 아니면 방향 전환 안 함
            if (_movement == null || !_movement.IsMoving) return;

            // 이동 방향에 따라 좌우 회전
            MoveDirection dir = _movement.Direction;

            // 방향이 바뀌었으면 쿨타임 시작
            if (dir != _lastAppliedDirection)
            {
                _lastAppliedDirection = dir;
                _directionChangeTimer = _directionChangeCooldown;
            }

            bool movingRight = dir == MoveDirection.Right;

            // isDefaultFacingLeft=true: 스프라이트 에셋이 Y=0에서 왼쪽을 향함
            //   오른쪽으로 이동 → Y=180(반전)으로 표시
            //   왼쪽으로 이동  → Y=0(기본)으로 표시
            // isDefaultFacingLeft=false: 스프라이트 에셋이 Y=0에서 오른쪽을 향함
            //   오른쪽으로 이동 → Y=0(기본)으로 표시
            //   왼쪽으로 이동  → Y=180(반전)으로 표시
            float spriteY = isDefaultFacingLeft
                ? (movingRight ? 180f : 0f)
                : (movingRight ? 0f : 180f);

            transform.localEulerAngles = new Vector3(0f, spriteY, 0f);

            // enemyForward는 자식이므로 Enemy 본체 회전 시 자동으로 따라감
        }

        /// <summary>
        /// 이동 방향에 따라 스프라이트 좌우 반전 (공통 메서드)
        /// </summary>
        /// <param name="sr">SpriteRenderer</param>
        /// <param name="isDefaultFacingLeft">기본 에셋이 왼쪽을 보고 있는지 여부</param>
        /// <param name="velocityX">X축 이동 속도</param>
        protected void UpdateSpriteFlipX(SpriteRenderer sr, bool isDefaultFacingLeft, float velocityX)
        {
            if (sr == null) return;

            // 이동 중일 때만 방향 전환 (정지 시 현재 방향 유지)
            if (Mathf.Abs(velocityX) < 0.01f) return;

            bool movingRight = velocityX > 0;

            if (isDefaultFacingLeft)
            {
                sr.flipX = movingRight;
            }
            else
            {
                sr.flipX = !movingRight;
            }
        }

        /// <summary>
        /// CamouflageAdapter 캐싱 (Start에서 한 번만 호출)
        /// DI 우선, fallback: FindObjectOfType
        /// </summary>
        protected virtual void CacheCamouflageAdapter()
        {
            // 1순위: DI 컨테이너의 ICamouflageStateProvider
            if (GameManager.Container != null && GameManager.Container.IsRegistered<ICamouflageStateProvider>())
            {
                _camouflageAdapter = GameManager.Container.Resolve<ICamouflageStateProvider>() as HideAndInk.Player.CamouflageAdapter;
                if (_camouflageAdapter != null) return;
            }

            // 2순위: FindObjectOfType (legacy fallback)
            _camouflageAdapter = FindObjectOfType<HideAndInk.Player.CamouflageAdapter>();
        }

        /// <summary>
        /// Player가 의태 중인지 확인
        /// </summary>
        protected bool IsPlayerCamouflaging()
        {
            return _camouflageAdapter != null && _camouflageAdapter.IsCamouflaging;
        }

        /// <summary>
        /// 순찰 목표 지점 선택 (공통 메서드)
        /// Ground 범위 내로 제한된 목표 위치 반환
        /// </summary>
        /// <param name="distance">이동 거리</param>
        /// <returns>목표 위치</returns>
        protected Vector3 PickPatrolTarget(float distance)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            Vector3 currentPos = transform.position;
            Vector3 target = new Vector3(
                currentPos.x + randomDirection.x * distance,
                currentPos.y,
                currentPos.z + randomDirection.y * distance);

            return ClampToGroundBounds(target);
        }

        /// <summary>
        /// Player 찾기 (Tag "Player" 우선, 실패 시 레이어 기반 탐색)
        /// </summary>
        protected virtual void FindPlayer()
        {
            // 1순위: Tag 기반 탐색 (효율적)
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
                return;
            }

            // 2순위: 레이어 기반 탐색 (GameObject 기반으로 최적화)
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                var gameObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var go in gameObjects)
                {
                    if (go.layer == playerLayer)
                    {
                        _playerTransform = go.transform;
                        return;
                    }
                }
            }

            Debug.LogWarning($"[EnemyAIController] Player not found. Ensure Player has Tag 'Player' or Layer 'Player'.");
        }

        /// <summary>
        /// 활성화/비활성화
        /// </summary>
        public virtual void SetActive(bool active)
        {
            _isActive = active;
            if (!active && _movement != null)
            {
                _movement.Stop();
            }
        }

        /// <summary>
        /// 이어하기 시 Enemy를 초기 위치/상태로 복원합니다.
        /// BossEnemyController 등에서 오버라이드하여 추가 상태 초기화 가능.
        /// </summary>
        public virtual void ResetToInitialState()
        {
            transform.position = _initialPosition;
            transform.rotation = _initialRotation;
            _movement?.Stop();
            _isActive = true;
            gameObject.SetActive(true);
#if UNITY_EDITOR
            Debug.Log($"[EnemyAIController] 초기화: {name} → 위치 {_initialPosition}");
#endif
        }
    }
}
