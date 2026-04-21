using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Enemy.Movement;
using HideAndInk.Core.Enemy.AI;
using HideAndInk.Core.Interfaces;

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
        [Header("기본 설정")]
        [SerializeField] protected float moveSpeed = 3f;
        [SerializeField] protected float acceleration = 8f;
        [SerializeField] protected float friction = 0.9f;
        [SerializeField] protected float maxSpeed = 5f;

        [Header("시야 설정")]
        [SerializeField] protected Transform enemyForward; // Enemy_Forward 자식 Transform
        [SerializeField] protected float viewRotationSpeed = 5f;

        [Header("Ground 제한 설정")]
        [SerializeField] protected LayerMask groundLayer; // Ground 레이어
        [SerializeField] protected float groundCheckDistance = 0.5f; // 이동 방향 앞쪽 Ground 체크 거리
        [SerializeField] protected float groundCheckRadius = 0.3f; // Ground 체크 반경
        [SerializeField] protected float groundScanDistance = 50f; // Ground 경계 스캔 최대 거리
        [SerializeField] protected float groundScanStep = 1f; // Ground 경계 스캔 간격

        // 컴포넌트 참조
        protected IEnemyMovement _movement;
        protected Transform _playerTransform;

        // Ground 경계 정보
        protected GroundBounds _groundBounds;
        protected bool _isGroundBoundsScanned;

        // 상태
        protected bool _isInitialized;
        protected bool _isActive = true;

        // 방향 전환 쿨타임
        private float _directionChangeCooldown = 0.3f; // 방향 전환 후 고정 시간 (초)
        private float _directionChangeTimer = 0f;
        private MoveDirection _lastAppliedDirection = MoveDirection.Left;

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
            FindPlayer();
            InitializeMovement();
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
        /// 이동 시스템 초기화
        /// </summary>
        protected virtual void InitializeMovement()
        {
            _movement = new EnemyMovement(
                enemy: this,
                speed: moveSpeed,
                acceleration: acceleration,
                friction: friction,
                maxSpeed: maxSpeed,
                groundLayer: groundLayer,
                groundCheckDistance: groundCheckDistance,
                groundCheckRadius: groundCheckRadius);
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

            Debug.Log($"[EnemyAIController] Ground bounds scanned: X({_groundBounds.MinX:F1} ~ {_groundBounds.MaxX:F1}), Z({_groundBounds.MinZ:F1} ~ {_groundBounds.MaxZ:F1})");
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
            Debug.Log($"[EnemyAIController] Ground edge reached at {transform.position}");
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

            bool shouldFaceLeft = dir == MoveDirection.Left;

            // Enemy 본체 스프라이트 회전: 오른쪽=0, 왼쪽=180
            float spriteY = shouldFaceLeft ? 180f : 0f;
            transform.localEulerAngles = new Vector3(0f, spriteY, 0f);

            // enemyForward는 자식이므로 Enemy 본체 회전 시 자동으로 따라감
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

            // 2순위: 레이어 기반 탐색 (FindObjectsByType으로 최적화)
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
            {
                var transforms = FindObjectsByType<Transform>(FindObjectsSortMode.None);
                foreach (var t in transforms)
                {
                    if (t.gameObject.layer == playerLayer)
                    {
                        _playerTransform = t;
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
        /// IEnemy.Update 구현
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!_isActive) return;
            UpdateAI(deltaTime);
            UpdateMovement(deltaTime);
        }
    }
}
