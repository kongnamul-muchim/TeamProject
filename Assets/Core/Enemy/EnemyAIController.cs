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
        [SerializeField] protected float directionChangeCooldown = 0.5f; // 방향 전환 쿨타임

        // 컴포넌트 참조
        protected IEnemyMovement _movement;
        protected Transform _playerTransform;

        // 상태
        protected bool _isInitialized;
        protected bool _isActive = true;
        protected float _directionChangeTimer; // 방향 전환 쿨타임 타이머

        #region IEnemy 구현

        public Transform Transform => transform;
        public Vector3 Position => transform.position;
        public abstract EnemyType Type { get; }
        public bool IsActive => _isActive && gameObject.activeInHierarchy;

        /// <summary>
        /// 현재 바라보는 방향 (Enemy_Forward 기준)
        /// </summary>
        public Vector3 Forward
        {
            get
            {
                if (enemyForward != null)
                {
                    return enemyForward.forward;
                }
                return -transform.right; // 기본 왼쪽
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
                maxSpeed: maxSpeed);
        }

        /// <summary>
        /// 이동 처리 (X-Z 평면)
        /// </summary>
        protected virtual void UpdateMovement(float deltaTime)
        {
            if (_movement == null) return;

            _movement.Update(deltaTime);

            // 속도를 실제 Transform에 적용 (X-Z 평면, Y축 고정)
            if (_movement.IsMoving)
            {
                Vector3 newPosition = transform.position;
                newPosition.x += _movement.Velocity.x * deltaTime;
                newPosition.z += _movement.Velocity.z * deltaTime;
                // Y축은 고정
                transform.position = newPosition;
            }
        }

        /// <summary>
        /// 시야 방향 업데이트
        /// Enemy 본체의 Y축 회전: 왼쪽(0) 또는 오른쪽(180)으로 전환
        /// 방향 전환 쿨타임 적용
        /// </summary>
        protected virtual void UpdateViewDirection()
        {
            if (_movement == null || !_movement.IsMoving) return;

            // 쿨타임 감소
            _directionChangeTimer -= Time.deltaTime;

            // 이동 방향에 따라 좌우 회전
            MoveDirection dir = _movement.Direction;
            bool shouldFaceRight = dir == MoveDirection.Right;

            // Y값 고정: 왼쪽=0, 오른쪽=180
            float targetY = shouldFaceRight ? 180f : 0f;
            float currentY = transform.localEulerAngles.y;

            // 현재 방향과 목표 방향이 다르고 쿨타임이 지났을 때만 전환
            bool isCurrentlyFacingRight = Mathf.Abs(currentY - 180f) < 90f;
            if (isCurrentlyFacingRight != shouldFaceRight && _directionChangeTimer <= 0f)
            {
                transform.localEulerAngles = new Vector3(0f, targetY, 0f);
                _directionChangeTimer = directionChangeCooldown; // 쿨타임 설정
            }
        }

        /// <summary>
        /// Player 찾기 (Tag "Player")
        /// </summary>
        protected virtual void FindPlayer()
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
            }
            else
            {
                // Layer "Player"로도 한 번 더 시도
                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer >= 0)
                {
                    var allObjects = FindObjectsOfType<GameObject>();
                    foreach (var go in allObjects)
                    {
                        if (go.layer == playerLayer)
                        {
                            _playerTransform = go.transform;
                            break;
                        }
                    }
                }
            }
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
