using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Enemy.Movement;

namespace HideAndInk.Core.Enemy.Normal
{
    /// <summary>
    /// 일반 몬스터 컨트롤러 (챕터 1 길막용)
    /// Player 감지 시 접근 → 충돌 시 Rigidbody로 밀어냄
    /// X-Z 평면 이동
    /// </summary>
    public class NormalEnemyController : EnemyAIController
    {
        public override EnemyType Type => EnemyType.Normal;

        [Header("일반 몬스터 설정")]
        [Tooltip("기본 이동 속도 (순찰)")]
        [SerializeField] private float patrolSpeed = 1.5f;
        [Tooltip("Player 추적 속도 (Player보다 느리게 설정 가능)")]
        [SerializeField] private float chaseSpeed = 2f;
        [Tooltip("Player 감지 반경 (m)")]
        [SerializeField] private float detectionRadius = 4f;
        [Tooltip("밀치기 힘 (근접 시 지속적으로 가하는 힘)")]
        [SerializeField] private float pushForce = 8f;
        [Tooltip("밀치기 시작 거리 (이 거리 내에 있으면 밀기 시작)")]
        [SerializeField] private float pushRange = 1.5f;
        [Tooltip("밀치기 쿨타임 (초)")]
        [SerializeField] private float pushCooldown = 0.5f;

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
        [SerializeField] private Color detectionRangeColor = new Color(0f, 1f, 0f, 0.3f);

        // 상태
        private enum State { Patrol, Chase, Cooldown }
        private State _currentState = State.Patrol;
        private float _stateTimer;
        private Vector3 _targetPosition;
        private bool _isMoving;
        private float _pushTimer; // 밀치기 쿨타임

        // 컴포넌트
        private Rigidbody _rigidbody;
        private HideAndInk.Player.CamouflageAdapter _camouflageAdapter;

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

            // CamouflageAdapter 캐싱
            _camouflageAdapter = FindObjectOfType<HideAndInk.Player.CamouflageAdapter>();

            _pushTimer = 0f;
        }

        protected override void UpdateAI(float deltaTime)
        {
            // 밀치기 쿨타임 감소
            if (_pushTimer > 0f)
            {
                _pushTimer -= deltaTime;
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
        /// Player가 의태 중인지 확인
        /// </summary>
        private bool IsPlayerCamouflaging()
        {
            return _camouflageAdapter != null && _camouflageAdapter.IsCamouflaging;
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
        /// Chase 상태 업데이트 (Player 방향으로 이동 + 근접 밀치기)
        /// </summary>
        private void UpdateChase(float deltaTime)
        {
            if (_playerTransform == null) return;

            // Chase 속도로 변경
            _movement.Speed = chaseSpeed;
            _movement.SetMaxSpeed(chaseSpeed);

            // Player 방향으로 이동
            _movement.MoveTo(_playerTransform.position);

            // 근접 밀치기 (충돌 없이도 밀어냄)
            ApplyPushForce(deltaTime);
        }

        /// <summary>
        /// 새로운 순찰 목표 지점 선택
        /// </summary>
        private void PickNewTarget()
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            Vector3 currentPos = transform.position;
            Vector3 target = new Vector3(
                currentPos.x + randomDirection.x * moveDistance,
                currentPos.y,
                currentPos.z + randomDirection.y * moveDistance);

            _targetPosition = ClampToGroundBounds(target);
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
        /// Player와 충돌 시 밀치기 (보조용)
        /// </summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (_pushTimer > 0f) return; // 쿨타임 중이면 무시

            // Player 태그 확인
            if (collision.gameObject.CompareTag("Player"))
            {
                Rigidbody playerRb = collision.gameObject.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    // Enemy → Player 방향으로 힘 가하기
                    Vector3 pushDirection = (collision.transform.position - transform.position).normalized;
                    pushDirection.y = 0f; // X-Z 평면만
                    playerRb.AddForce(pushDirection * pushForce * 0.5f, ForceMode.Impulse);

                    _pushTimer = pushCooldown;

#if UNITY_EDITOR
                    Debug.Log($"[NormalEnemy] Collision Push! Force: {pushForce * 0.5f}, Direction: {pushDirection}");
#endif
                }
            }
        }

        /// <summary>
        /// 근접 밀치기 (충돌 없이도 Player를 밀어냄)
        /// Chase 상태에서만 작동
        /// </summary>
        private void ApplyPushForce(float deltaTime)
        {
            if (_playerTransform == null) return;
            if (_pushTimer > 0f)
            {
                _pushTimer -= deltaTime;
                return;
            }

            // Player와의 거리 계산
            float distance = Vector3.Distance(transform.position, _playerTransform.position);

            // 밀치기 범위 내에 있으면
            if (distance <= pushRange)
            {
                Rigidbody playerRb = _playerTransform.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    // Enemy → Player 방향으로 힘 가하기
                    Vector3 pushDirection = (_playerTransform.position - transform.position).normalized;
                    pushDirection.y = 0f; // X-Z 평면만

                    // 거리에 비례한 힘 (가까울수록 강하게)
                    float forceMultiplier = 1f - (distance / pushRange);
                    float appliedForce = pushForce * forceMultiplier;

                    playerRb.AddForce(pushDirection * appliedForce, ForceMode.Force);

#if UNITY_EDITOR
                    if (Time.frameCount % 30 == 0) // 0.5초마다 로그
                    {
                        Debug.Log($"[NormalEnemy] Proximity Push! Distance: {distance:F2}m, Force: {appliedForce:F2}, Direction: {pushDirection}");
                    }
#endif
                }
            }
        }

        /// <summary>
        /// 이동 방향에 따라 스프라이트 좌우 반전
        /// </summary>
        private void UpdateSpriteDirection()
        {
            if (enemySpriteRenderer == null) return;

            // 이동 중일 때만 방향 전환 (정지 시 현재 방향 유지)
            if (_movement != null && _movement.Velocity.sqrMagnitude > 0.01f)
            {
                // X축 이동 방향 확인
                bool movingRight = _movement.Velocity.x > 0;
                bool movingLeft = _movement.Velocity.x < 0;

                // 기본이 왼쪽 Facing일 때:
                // - 왼쪽 이동: flipX = false (원래대로)
                // - 오른쪽 이동: flipX = true (반전)
                // 기본이 오른쪽 Facing일 때:
                // - 왼쪽 이동: flipX = true (반전)
                // - 오른쪽 이동: flipX = false (원래대로)
                
                if (isDefaultFacingLeft)
                {
                    enemySpriteRenderer.flipX = movingRight;
                }
                else
                {
                    enemySpriteRenderer.flipX = movingLeft;
                }
            }
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
