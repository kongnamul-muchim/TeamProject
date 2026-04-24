using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Enemy.Movement;
using HideAndInk.Core.Player;

namespace HideAndInk.Core.Enemy.Normal
{
    /// <summary>
    /// 일반 몬스터 컨트롤러 (게)
    /// Player 감지 시 접근 → 접촉 시 데미지 + 넉백
    /// X-Z 평면 이동
    /// </summary>
    public class NormalEnemyController : EnemyAIController
    {
        public override EnemyType Type => EnemyType.Normal;

        [Header("이동 설정")]
        [Tooltip("기본 이동 속도 (순찰)")]
        [SerializeField] private float patrolSpeed = 1.5f;
        [Tooltip("Player 추적 속도")]
        [SerializeField] private float chaseSpeed = 2f;
        [Tooltip("Player 감지 반경 (m)")]
        [SerializeField] private float detectionRadius = 4f;

        [Header("공격 설정")]
        [Tooltip("넉백 힘 (Player를 밀어내는 힘)")]
        [SerializeField] private float knockbackForce = 10f;
        [Tooltip("데미지 쿨타임 (초). 연속 피격 방지")]
        [SerializeField] private float damageCooldown = 1.5f;
        [Tooltip("데미지 판정 거리 (m)")]
        [SerializeField] private float attackRange = 1.5f;

        [Header("순찰 패턴")]
        [SerializeField] private float moveInterval = 2f;
        [SerializeField] private float moveDistance = 2f;
        [SerializeField] private float idleTime = 1f;

        [Header("스프라이트 방향")]
        [Tooltip("기본 에셋이 왼쪽을 보고 있는지 여부 (true: 왼쪽 기본, false: 오른쪽 기본)")]
        [SerializeField] private bool isDefaultFacingLeft = true;
        [SerializeField] private SpriteRenderer enemySpriteRenderer;
        [SerializeField] private Animator enemyAnimator;

        [Header("감지 범위 시각화")]
        [Tooltip("Scene에서 감지 범위 원 표시")]
        [SerializeField] private bool showDetectionRangeInScene = true;
        [Tooltip("시각화 색상")]
        [SerializeField] private Color detectionRangeColor = new Color(1f, 0.3f, 0f, 0.3f);

        // 상태
        private enum State { Patrol, Chase, Cooldown }
        private State _currentState = State.Patrol;
        private float _stateTimer;
        private Vector3 _targetPosition;
        private bool _isMoving;
        private float _damageTimer; // 공격 쿨타임

        // 컴포넌트
        private Rigidbody _rigidbody;
        private PlayerLives _playerLives;
        private Rigidbody _playerRigidbody;
        // _camouflageAdapter는 부모 클래스에 이미 있음

        protected override void InitializeMovement()
        {
            _movement = new EnemyMovement(
                enemy: this,
                speed: patrolSpeed,
                acceleration: 5f,
                friction: 0.8f,
                maxSpeed: patrolSpeed,
                groundLayer: groundLayer,
                groundCheckDistance: groundCheckDistance,
                groundCheckRadius: groundCheckRadius);
        }

        protected override void Start()
        {
            base.Start();

            // Rigidbody 확인/추가
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
                _rigidbody.useGravity = false;
                _rigidbody.freezeRotation = true;
                _rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            }

            // SpriteRenderer 자동 할당
            if (enemySpriteRenderer == null)
            {
                enemySpriteRenderer = GetComponent<SpriteRenderer>();
            }

            // Animator 자동 할당
            if (enemyAnimator == null)
            {
                enemyAnimator = GetComponent<Animator>();
            }

            // CamouflageAdapter 캐싱 (부모에서 제공)
            CacheCamouflageAdapter();

            // PlayerLives + Player Rigidbody 캐싱
            CachePlayerComponents();

            _damageTimer = 0f;
        }

        /// <summary>
        /// PlayerLives와 Player Rigidbody 캐싱
        /// </summary>
        private void CachePlayerComponents()
        {
            if (_playerTransform != null)
            {
                _playerLives = _playerTransform.GetComponent<PlayerLives>();
                _playerRigidbody = _playerTransform.GetComponent<Rigidbody>();
            }

            if (_playerLives == null)
            {
                _playerLives = FindObjectOfType<PlayerLives>();
            }

            if (_playerRigidbody == null && _playerTransform != null)
            {
                _playerRigidbody = _playerTransform.GetComponent<Rigidbody>();
            }
        }

        protected override void UpdateAI(float deltaTime)
        {
            // 공격 쿨타임 감소
            if (_damageTimer > 0f)
            {
                _damageTimer -= deltaTime;
            }

            // Player 감지 체크
            bool playerInRange = CheckPlayerDetection();

            // 상태 머신
            switch (_currentState)
            {
                case State.Patrol:
                    UpdatePatrol(deltaTime);
                    if (playerInRange)
                    {
                        StartChase();
                    }
                    break;

                case State.Chase:
                    UpdateChase(deltaTime);
                    if (!playerInRange)
                    {
                        StartCooldown();
                    }
                    break;

                case State.Cooldown:
                    _stateTimer -= deltaTime;
                    if (_stateTimer <= 0f)
                    {
                        StartPatrol();
                    }
                    break;
            }

            // 스프라이트 방향 업데이트
            UpdateSpriteDirection();
        }

        /// <summary>
        /// Player 감지 체크 (거리 기반)
        /// 의태 중이면 감지되지 않음
        /// </summary>
        private bool CheckPlayerDetection()
        {
            if (_playerTransform == null) return false;

            // Player가 의태 중이면 감지 안 됨
            if (IsPlayerCamouflaging()) return false;

            float distance = Vector3.Distance(transform.position, _playerTransform.position);
            return distance <= detectionRadius;
        }

        /// <summary>
        /// 순찰 상태 업데이트 (끊김 없이 지속 이동)
        /// </summary>
        private void UpdatePatrol(float deltaTime)
        {
            // 목표 도달 감지 (EnemyMovement 내부 distance < 0.5f 기준)
            // Velocity가 거의 0이고 목표가 설정되어 있으면 도달한 것으로 간주
            bool hasReachedTarget = _movement != null && 
                                    !_movement.IsMoving && 
                                    _movement.Velocity.sqrMagnitude < 0.01f;

            if (hasReachedTarget)
            {
                // 즉시 새 목표 설정 (대기 시간 없이)
                PickNewTarget();
                _movement.MoveTo(_targetPosition);
            }
            else if (!_movement.IsMoving)
            {
                // 초기 시작 시 목표 설정
                PickNewTarget();
                _movement.MoveTo(_targetPosition);
            }
        }

        /// <summary>
        /// Chase 상태 업데이트 (Player 방향으로 이동 + 근접 공격)
        /// 공격 후 쿨타임 동안은 정지하여 Player가 도망갈 시간을 확보
        /// </summary>
        private void UpdateChase(float deltaTime)
        {
            if (_playerTransform == null) return;

            // Chase 속도로 변경
            _movement.Speed = chaseSpeed;
            _movement.SetMaxSpeed(chaseSpeed);

            // 공격 쿨타임 중에는 이동 정지 (Player 넉백 후 도주 시간 확보)
            if (_damageTimer > 0f)
            {
                _movement.Stop();
                return;
            }

            // Player 방향으로 이동
            _movement.MoveTo(_playerTransform.position);

            // 근접 공격 체크 (범위 내 + 쿨타임)
            TryAttackPlayer(deltaTime);
        }

        /// <summary>
        /// 근접 공격 시도
        /// Player가 공격 범위 내에 있으면 데미지 + 넉백
        /// </summary>
        private void TryAttackPlayer(float deltaTime)
        {
            if (_playerTransform == null) return;
            if (_damageTimer > 0f) return;
            if (_playerLives == null) return;

            float distance = Vector3.Distance(transform.position, _playerTransform.position);
            if (distance > attackRange) return;

            // Player가 무적 상태면 공격 안 함
            if (_playerLives.IsInvincible) return;

            // 🔴 데미지
            _playerLives.TakeDamage();
            _damageTimer = damageCooldown;

            // 💥 넉백
            ApplyKnockback();

#if UNITY_EDITOR
            Debug.Log($"[Crab] Attack! Distance: {distance:F2}m, Lives left: {_playerLives.CurrentLives}");
#endif
        }

        /// <summary>
        /// Player를 밀어내는 넉백 적용
        /// </summary>
        private void ApplyKnockback()
        {
            if (_playerTransform == null || _playerRigidbody == null) return;

            Vector3 knockbackDir = (_playerTransform.position - transform.position).normalized;
            knockbackDir.y = 0f;

            _playerRigidbody.AddForce(knockbackDir * knockbackForce, ForceMode.Impulse);

#if UNITY_EDITOR
            Debug.Log($"[Crab] Knockback! Direction: {knockbackDir}, Force: {knockbackForce}");
#endif
        }

        /// <summary>
        /// 새로운 순찰 목표 지점 선택
        /// </summary>
        private void PickNewTarget()
        {
            _targetPosition = PickPatrolTarget(moveDistance);
        }

        /// <summary>
        /// Chase 시작
        /// </summary>
        private void StartChase()
        {
            _currentState = State.Chase;
            _movement.Stop(); // 순찰 이동 중지
        }

        /// <summary>
        /// Cooldown 시작 (Chase 후 잠시 대기)
        /// </summary>
        private void StartCooldown()
        {
            _currentState = State.Cooldown;
            _stateTimer = 2f; // 2초 대기 후 순찰 복귀
            _movement.Stop();
            _movement.Speed = patrolSpeed;
            _movement.SetMaxSpeed(patrolSpeed);
        }

        /// <summary>
        /// Patrol 시작 (또는 재개)
        /// </summary>
        private void StartPatrol()
        {
            _currentState = State.Patrol;
            _stateTimer = idleTime;
            _isMoving = false;
            _movement.Speed = patrolSpeed;
            _movement.SetMaxSpeed(patrolSpeed);
        }

        /// <summary>
        /// 물리 충돌 시 보조 공격 (OnCollisionEnter를 통한 접촉 데미지)
        /// TryAttackPlayer가 Chase 중 거리 기반으로 처리하지만,
        /// 물리적으로 부딪혔을 때도 데미지가 들어가도록 보장
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (_damageTimer > 0f) return;
            if (!collision.gameObject.CompareTag("Player")) return;
            if (_playerLives == null || _playerLives.IsInvincible) return;

            // 데미지
            _playerLives.TakeDamage();
            _damageTimer = damageCooldown;

            // 넉백
            ApplyKnockback();

#if UNITY_EDITOR
            Debug.Log($"[Crab] Collision Attack! Lives left: {_playerLives.CurrentLives}");
#endif
        }

        /// <summary>
        /// 이동 방향에 따라 스프라이트 좌우 반전
        /// </summary>
        private void UpdateSpriteDirection()
        {
            UpdateSpriteFlipX(enemySpriteRenderer, isDefaultFacingLeft, _movement?.Velocity.x ?? 0f);
        }

        private void OnDestroy()
        {
            // Rigidbody 정리 (추가한 경우)
            if (_rigidbody != null && !GetComponent<Rigidbody>())
            {
                Destroy(_rigidbody);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Scene에서 감지 범위 시각화
        /// </summary>
        private void OnDrawGizmos()
        {
            if (!showDetectionRangeInScene) return;

            // 감지 범위 원 그리기
            Gizmos.color = detectionRangeColor;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);

            // 중심점 강조
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
#endif
    }
}
