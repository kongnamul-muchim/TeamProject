using UnityEngine;
using HideAndInk.Core.Enemy.AI;
using HideAndInk.Core.Enemy.AI.Behaviors;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Perception;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Enemy.Boss.Gimmicks;

namespace HideAndInk.Core.Enemy.Boss
{
    /// <summary>
    /// 보스 몬스터 컨트롤러
    /// AI 상태 머신 (Patrol → Chase → Search) + 의심도 연동
    /// X-Z 평면 이동
    /// Ground 경계 검증 + 일반 몬스터 알림 연동 + 보스 기믹 시스템 포함
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

        [Header("탐색 설정")]
        [SerializeField] private float searchDistance = 3f;
        [SerializeField] private float searchDuration = 5f;

        [Header("기믹 설정")]
        [Tooltip("보스 기믹 ScriptableObject (우선 사용)")]
        [SerializeField] private ScriptableObject gimmickAsset;
        [Tooltip("직접 할당한 MonoBehaviour 기믹 (gimmickAsset가 없을 때 사용)")]
        [SerializeField] private MonoBehaviour customGimmick;

        // AI 상태 머신
        private EnemyAIStateMachine _stateMachine;
        private PatrolBehavior _patrolBehavior;
        private ChaseBehavior _chaseBehavior;
        private SearchBehavior _searchBehavior;

        // 기믹 시스템
        private IEnemyGimmick _activeGimmick;

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
            InitializeBehaviors();
            InitializeGimmick();
            InitializeStateMachine();
        }

        /// <summary>
        /// AI Behavior 초기화
        /// </summary>
        private void InitializeBehaviors()
        {
            _patrolBehavior = new PatrolBehavior(
                enemy: this,
                movement: _movement);

            _chaseBehavior = new ChaseBehavior(
                enemy: this,
                movement: _movement,
                playerTransform: _playerTransform,
                predictionTime: 0.5f);

            _searchBehavior = new SearchBehavior(
                enemy: this,
                movement: _movement,
                searchDuration: searchDuration,
                searchDistance: searchDistance);

            // Ground 경계 전달
            if (_isGroundBoundsScanned)
            {
                _patrolBehavior.SetGroundBounds(_groundBounds);
                _chaseBehavior.SetGroundBounds(_groundBounds);
                _searchBehavior.SetGroundBounds(_groundBounds);
            }
        }

        /// <summary>
        /// 기믹 초기화 (ScriptableObject 우선, 그 다음 customGimmick)
        /// </summary>
        private void InitializeGimmick()
        {
            // 1순위: ScriptableObject (gimmickAsset)
            if (gimmickAsset != null)
            {
                _activeGimmick = gimmickAsset as IEnemyGimmick;
                if (_activeGimmick == null)
                {
                    Debug.LogError($"[BossEnemyController] gimmickAsset이 IEnemyGimmick을 구현하지 않았습니다: {gimmickAsset.GetType().Name}");
                }
                else
                {
                    Debug.Log($"[BossEnemyController] ScriptableObject gimmick loaded: {_activeGimmick.Type}");
                }
            }
            // 2순위: MonoBehaviour (customGimmick)
            else if (customGimmick != null)
            {
                _activeGimmick = customGimmick as IEnemyGimmick;
                if (_activeGimmick == null)
                {
                    Debug.LogError($"[BossEnemyController] customGimmick이 IEnemyGimmick을 구현하지 않았습니다: {customGimmick.GetType().Name}");
                }
                else
                {
                    Debug.Log($"[BossEnemyController] MonoBehaviour gimmick loaded: {_activeGimmick.Type}");
                }
            }
            else
            {
                Debug.LogWarning("[BossEnemyController] No gimmick assigned. Set gimmickAsset or customGimmick.");
            }

            // 기믹 콜백 연결
            if (_activeGimmick != null)
            {
                ConnectGimmickCallbacks();
                _activeGimmick.OnActivate(transform);
            }
        }

        /// <summary>
        /// 기믹 콜백 연결
        /// </summary>
        private void ConnectGimmickCallbacks()
        {
            if (_activeGimmick == null) return;

            // 가자미: 매복 기믹
            if (_activeGimmick is AmbushGimmick ambush)
            {
                ambush.OnSpeedOverride = (speed) => _movement.Speed = speed;
                ambush.OnVisibilityToggle = (visible) =>
                {
                    if (visionSensor != null)
                        visionSensor.gameObject.SetActive(visible);
                };
                ambush.SetOriginalSpeed(patrolSpeed);
            }

            // 곰치: 집요한 추격 기믹
            if (_activeGimmick is RelentlessChaseGimmick relentless)
            {
                relentless.OnSuspicionDecayRateOverride = (rate) =>
                {
                    Debug.Log($"[BossEnemyController] Suspicion decay rate: {rate}");
                };
                relentless.OnSearchRadiusOverride = (multiplier) =>
                {
                    Debug.Log($"[BossEnemyController] Search radius multiplier: {multiplier}");
                };
                relentless.OnPatrolAreaOverride = (center, radius) =>
                {
                    Debug.Log($"[BossEnemyController] Patrol area: {center}, radius: {radius}");
                };
                relentless.SetOriginalSpeed(patrolSpeed);
            }

            // 전기뱀장어: 감전 구역 기믹
            if (_activeGimmick is ElectricZoneGimmick electric)
            {
                electric.OnZoneCreated = (zone) =>
                {
                    Debug.Log($"[BossEnemyController] Electric zone created: {zone.name}");
                };
                electric.OnPlayerTrapped = (pos) =>
                {
                    Debug.Log($"[BossEnemyController] Player trapped at: {pos}");
                };
            }

            // 아귀: 발광 미끼 기믹
            if (_activeGimmick is LureBaitGimmick lure)
            {
                if (_isGroundBoundsScanned)
                {
                    lure.SetGroundBounds(_groundBounds);
                }
                lure.OnBaitCreated = (bait) =>
                {
                    Debug.Log($"[BossEnemyController] Bait created: {bait.name}");
                };
                lure.OnBaitDestroyed = (bait) =>
                {
                    Debug.Log($"[BossEnemyController] Bait destroyed: {bait.name}");
                };
                lure.OnPlayerDetectedByBait = (pos) =>
                {
                    Debug.Log($"[BossEnemyController] Player detected by bait: {pos}");
                };
                lure.OnChaseTriggered = () =>
                {
                    if (_stateMachine != null)
                    {
                        _stateMachine.TryTransitionTo(EnemyAIState.Chase);
                    }
                };
            }

            // 백상아리: 초고속 돌진 기믹
            if (_activeGimmick is DashChargeGimmick dash)
            {
                dash.OnSpeedOverride = (speed) => _movement.Speed = speed;
                dash.OnDashStarted = (dir) =>
                {
                    Debug.Log($"[BossEnemyController] Dash started: {dir}");
                };
                dash.OnObstacleHit = (obj) =>
                {
                    Debug.Log($"[BossEnemyController] Obstacle hit: {obj.name}");
                };
                dash.OnDashCompleted = () =>
                {
                    if (_stateMachine != null)
                    {
                        _stateMachine.TryTransitionTo(EnemyAIState.Patrol);
                    }
                };
            }
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

            // 기믹 상태 업데이트
            UpdateGimmick(deltaTime);
        }

        /// <summary>
        /// 기믹 상태 업데이트 (현재 AI 상태에 따라 호출)
        /// </summary>
        private void UpdateGimmick(float deltaTime)
        {
            if (_activeGimmick == null) return;

            switch (_stateMachine.CurrentState)
            {
                case EnemyAIState.Patrol:
                    _activeGimmick.OnPatrolUpdate(deltaTime);
                    break;
                case EnemyAIState.Chase:
                    _activeGimmick.OnChaseUpdate(deltaTime);
                    break;
                case EnemyAIState.Search:
                    _activeGimmick.OnSearchUpdate(deltaTime);
                    break;
            }
        }

        /// <summary>
        /// Player가 시야 내에 있는지 확인
        /// 의태 중이면 감지되지 않음
        /// </summary>
        private bool CanSeePlayer()
        {
            if (visionSensor == null || _playerTransform == null) return false;

            // Player가 의태 중이면 감지 안 됨
            if (IsPlayerCamouflaging()) return false;

            return visionSensor.CanSee(_playerTransform.gameObject);
        }

        /// <summary>
        /// Player가 의태 중인지 확인
        /// </summary>
        private bool IsPlayerCamouflaging()
        {
            // CamouflageAdapter 찾기
            var camouflageAdapter = FindObjectOfType<HideAndInk.Player.CamouflageAdapter>();
            if (camouflageAdapter != null)
            {
                return camouflageAdapter.IsCamouflaging;
            }
            return false;
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

            // 기믹 상태 변경 알림
            if (_activeGimmick != null)
            {
                // 이전 상태 Exit
                switch (previous)
                {
                    case EnemyAIState.Patrol:
                        _activeGimmick.OnPatrolExit();
                        break;
                    case EnemyAIState.Chase:
                        _activeGimmick.OnChaseExit();
                        break;
                    case EnemyAIState.Search:
                        _activeGimmick.OnSearchExit();
                        break;
                }

                // 현재 상태 Enter
                switch (current)
                {
                    case EnemyAIState.Patrol:
                        _activeGimmick.OnPatrolEnter();
                        break;
                    case EnemyAIState.Chase:
                        _activeGimmick.OnChaseEnter();
                        break;
                    case EnemyAIState.Search:
                        _activeGimmick.OnSearchEnter();
                        break;
                }
            }
        }

        #region 일반 몬스터 알림 연동

        /// <summary>
        /// 일반 몬스터가 Player 위치를 알림
        /// Player 위치가 Ground 위에 있으면 Chase 상태로 전환
        /// </summary>
        public void AlertPlayerPosition(Vector3 playerPosition)
        {
            // Player 위치가 Ground 위에 있는지 검증
            if (_movement.IsPositionOnGround(playerPosition))
            {
                // 탐색 상태에 마지막 위치 전달
                _searchBehavior.SetLastKnownPosition(playerPosition);

                // Chase 상태로 전환
                if (_stateMachine != null)
                {
                    _stateMachine.TryTransitionTo(EnemyAIState.Chase);
                }

                Debug.Log($"[BossEnemy] Player position alerted: {playerPosition}");
            }
            else
            {
                Debug.Log($"[BossEnemy] Alert ignored - Player position not on Ground: {playerPosition}");
            }
        }

        #endregion

        protected virtual void OnDestroy()
        {
            if (_stateMachine != null)
            {
                _stateMachine.OnStateChanged -= OnAIStateChanged;
            }

            if (_activeGimmick != null)
            {
                _activeGimmick.OnDeactivate();
            }
        }
    }
}
