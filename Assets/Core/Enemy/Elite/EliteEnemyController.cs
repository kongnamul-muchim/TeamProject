using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Enemy.Movement;

namespace HideAndInk.Core.Enemy.Elite
{
    /// <summary>
    /// 정예 몬스터 컨트롤러
    /// 센서 없이 행동 패턴(IEliteBehavior) 기반 기믹 발동
    /// X-Z 평면 이동
    /// </summary>
    public class EliteEnemyController : EnemyAIController
    {
        public override EnemyType Type => EnemyType.Elite;

        [Header("정예 몬스터 설정")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private float moveInterval = 3f;
        [SerializeField] private float moveDistance = 3f;
        [SerializeField] private float idleTime = 2f;

        [Header("행동 패턴 설정")]
        [Tooltip("정예 몬스터 행동 패턴 (ScriptableObject 또는 MonoBehaviour)")]
        [SerializeField] private MonoBehaviour eliteBehavior;
        [Tooltip("Player 감지 반경 (m)")]
        [SerializeField] private float detectionRadius = 8f;

        // 상태
        private float _stateTimer;
        private Vector3 _targetPosition;
        private bool _isMoving;
        private IEliteBehavior _behavior;

        protected override void InitializeMovement()
        {
            _movement = new EnemyMovement(
                enemy: this,
                speed: moveSpeed,
                acceleration: 6f,
                friction: 0.85f,
                maxSpeed: moveSpeed,
                groundLayer: groundLayer,
                groundCheckDistance: groundCheckDistance,
                groundCheckRadius: groundCheckRadius);
        }

        protected override void Start()
        {
            base.Start();
            InitializeBehavior();
        }

        /// <summary>
        /// 행동 패턴 초기화
        /// </summary>
        private void InitializeBehavior()
        {
            if (eliteBehavior != null)
            {
                _behavior = eliteBehavior as IEliteBehavior;
                if (_behavior == null)
                {
                    Debug.LogError($"[EliteEnemyController] eliteBehavior가 IEliteBehavior를 구현하지 않았습니다: {eliteBehavior.GetType().Name}");
                }
                else
                {
                    _behavior.OnActivate();
#if UNITY_EDITOR
                    Debug.Log($"[EliteEnemyController] Elite behavior loaded: {_behavior.BehaviorName}");
#endif
                }
            }
            else
            {
                Debug.LogWarning("[EliteEnemyController] No elite behavior assigned. Set eliteBehavior in Inspector.");
            }
        }

        protected override void UpdateAI(float deltaTime)
        {
            // Player 감지 체크
            CheckPlayerDetection();

            // 행동 패턴 업데이트
            if (_behavior != null)
            {
                _behavior.OnUpdate(deltaTime);
            }

            // 이동 상태 머신 (행동 패턴이 제어하지 않을 때만)
            if (_behavior == null || !IsBehaviorControllingMovement())
            {
                UpdatePatrolMovement(deltaTime);
            }
        }

        /// <summary>
        /// Player 감지 체크 (거리 기반)
        /// </summary>
        private void CheckPlayerDetection()
        {
            if (_playerTransform == null) return;

            float distance = Vector3.Distance(transform.position, _playerTransform.position);

            if (distance <= detectionRadius)
            {
                _behavior?.OnPlayerApproached(distance, _playerTransform.position);
            }
        }

        /// <summary>
        /// 행동 패턴이 이동 제어권 가졌는지 확인
        /// </summary>
        private bool IsBehaviorControllingMovement()
        {
            // 향후 IEliteBehavior에 HasMovementOverride 속성 추가 시 활용
            return false;
        }

        /// <summary>
        /// 순찰 이동 업데이트
        /// </summary>
        private void UpdatePatrolMovement(float deltaTime)
        {
            _stateTimer -= deltaTime;

            if (_stateTimer <= 0f)
            {
                if (_isMoving)
                {
                    // 이동 완료 → 대기
                    _movement.Stop();
                    _isMoving = false;
                    _stateTimer = idleTime;
                }
                else
                {
                    // 대기 완료 → 이동
                    PickNewTarget();
                    _isMoving = true;
                    _stateTimer = moveInterval;
                }
            }

            if (_isMoving)
            {
                _movement.MoveTo(_targetPosition);
            }
        }

        /// <summary>
        /// 새로운 이동 목표 지점 선택 (X-Z 평면)
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

        private void OnDestroy()
        {
            if (_behavior != null)
            {
                _behavior.OnDeactivate();
            }
        }
    }
}
