using UnityEngine;
using HideAndInk.Core.Enemy.AI;
using HideAndInk.Core.Enemy.AI.Behaviors;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Perception;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Enemy.Boss
{
    /// <summary>
    /// 보스 몬스터 컨트롤러
    /// AI 상태 머신 (Patrol → Chase → Search) + 의심도 연동
    /// </summary>
    public class BossEnemyController : EnemyAIController
    {
        public override EnemyType Type => EnemyType.Boss;

        [Header("보스 설정")]
        [SerializeField] private ConeVisionSensor visionSensor;
        [SerializeField] private SuspicionMeter suspicionMeter;

        [Header("상태별 속도")]
        [SerializeField] private float patrolSpeed = 2f;
        [SerializeField] private float chaseSpeed = 5f;
        [SerializeField] private float searchSpeed = 3f;

        [Header("순찰 설정")]
        [SerializeField] private float patrolRadius = 5f;

        [Header("탐색 설정")]
        [SerializeField] private float searchRadius = 3f;
        [SerializeField] private float searchDuration = 5f;

        // AI 상태 머신
        private EnemyAIStateMachine _stateMachine;
        private PatrolBehavior _patrolBehavior;
        private ChaseBehavior _chaseBehavior;
        private SearchBehavior _searchBehavior;

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
            InitializeBehaviors();
            InitializeStateMachine();
        }

        /// <summary>
        /// AI Behavior 초기화
        /// </summary>
        private void InitializeBehaviors()
        {
            _patrolBehavior = new PatrolBehavior(
                enemy: this,
                movement: _movement,
                patrolRadius: patrolRadius);

            _chaseBehavior = new ChaseBehavior(
                enemy: this,
                movement: _movement,
                playerTransform: _playerTransform,
                predictionTime: 0.5f);

            _searchBehavior = new SearchBehavior(
                enemy: this,
                movement: _movement,
                searchRadius: searchRadius,
                searchDuration: searchDuration);
        }

        /// <summary>
        /// AI 상태 머신 초기화
        /// </summary>
        private void InitializeStateMachine()
        {
            _stateMachine = new EnemyAIStateMachine(
                patrolState: _patrolBehavior,
                chaseState: _chaseBehavior,
                searchState: _searchBehavior);

            _stateMachine.Initialize(EnemyAIState.Patrol);
            _stateMachine.OnStateChanged += OnAIStateChanged;

            // 초기 속도
            _movement.Speed = patrolSpeed;
        }

        protected override void UpdateAI(float deltaTime)
        {
            if (_stateMachine == null) return;

            // Player 감지
            bool canSeePlayer = CanSeePlayer();

            // 의심도 업데이트
            UpdateSuspicion(canSeePlayer);

            // 상태 전환 체크
            CheckStateTransitions(canSeePlayer);

            // AI 상태 업데이트
            _stateMachine.Update(deltaTime);
        }

        /// <summary>
        /// Player가 시야 내에 있는지 확인
        /// </summary>
        private bool CanSeePlayer()
        {
            if (visionSensor == null || _playerTransform == null) return false;
            return visionSensor.CanSee(_playerTransform.gameObject);
        }

        /// <summary>
        /// 의심도 업데이트
        /// </summary>
        private void UpdateSuspicion(bool canSeePlayer)
        {
            if (suspicionMeter == null) return;

            if (canSeePlayer)
            {
                suspicionMeter.OnDetectedTarget(detectionIntensity: 1f);
            }
        }

        /// <summary>
        /// 상태 전환 조건 체크
        /// </summary>
        private void CheckStateTransitions(bool canSeePlayer)
        {
            if (_stateMachine == null) return;

            var currentState = _stateMachine.CurrentState;
            var suspicionLevel = suspicionMeter?.CurrentLevel ?? SuspicionLevel.Safe;

            switch (currentState)
            {
                case EnemyAIState.Patrol:
                    // Patrol → Chase: Player 발견 + 의심도 Danger 이상
                    if (canSeePlayer && suspicionLevel >= SuspicionLevel.Danger)
                    {
                        _stateMachine.TryTransitionTo(EnemyAIState.Chase);
                    }
                    break;

                case EnemyAIState.Chase:
                    // Chase → Search: Player 놓침
                    if (!canSeePlayer)
                    {
                        // 마지막 위치 전달
                        if (_playerTransform != null)
                        {
                            _searchBehavior.SetLastKnownPosition(_playerTransform.position);
                        }
                        _stateMachine.TryTransitionTo(EnemyAIState.Search);
                    }
                    break;

                case EnemyAIState.Search:
                    // Search → Chase: Player 재발견
                    if (canSeePlayer)
                    {
                        _stateMachine.TryTransitionTo(EnemyAIState.Chase);
                    }
                    // Search → Patrol: 탐색 시간 초과
                    else if (_searchBehavior.IsSearchTimeout())
                    {
                        _stateMachine.TryTransitionTo(EnemyAIState.Patrol);
                    }
                    break;
            }
        }

        /// <summary>
        /// AI 상태 변경 시 호출
        /// </summary>
        private void OnAIStateChanged(EnemyAIState previous, EnemyAIState current)
        {
            // 상태별 속도 변경
            switch (current)
            {
                case EnemyAIState.Patrol:
                    _movement.Speed = patrolSpeed;
                    break;
                case EnemyAIState.Chase:
                    _movement.Speed = chaseSpeed;
                    break;
                case EnemyAIState.Search:
                    _movement.Speed = searchSpeed;
                    break;
            }
        }

        protected virtual void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= OnAIStateChanged;
            }
        }
    }
}
