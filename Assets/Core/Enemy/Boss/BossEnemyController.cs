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
        [SerializeField] private BossSuspicionSystem suspicionSystem;
        [SerializeField] private HideAndInk.Core.Perception.VisionConeRenderer visionConeRenderer;

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

        // Player 의태 상태 캐싱 (매 프레임 FindObjectOfType 방지)
        private HideAndInk.Player.CamouflageAdapter _camouflageAdapter;

        protected override void Awake()
        {
            base.Awake();
        }

        protected override void Start()
        {
            base.Start();
            CacheCamouflageAdapter();
            InitializeGimmick();      // 기믹 먼저 초기화
            InitializeBehaviors();    // Behavior 생성 시 기믹 사용
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
                gimmick: _activeGimmick);

            _chaseBehavior = new ChaseBehavior(
                enemy: this,
                movement: _movement,
                playerTransform: _playerTransform,
                predictionTime: 0.5f);

            _searchBehavior = new SearchBehavior(
                enemy: this,
                movement: _movement,
                gimmick: _activeGimmick,
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
#if UNITY_EDITOR
                    Debug.Log($"[BossEnemyController] ScriptableObject gimmick loaded: {_activeGimmick.Type}");
#endif
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
#if UNITY_EDITOR
                    Debug.Log($"[BossEnemyController] MonoBehaviour gimmick loaded: {_activeGimmick.Type}");
#endif
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

                // AmbushGimmick일 경우 의심도 모듈을 BossSuspicionSystem에 주입
                if (_activeGimmick is AmbushGimmick ambush && suspicionSystem != null)
                {
                    suspicionSystem.SetSuspicionRadius(ambush.FarSuspicionRadius, ambush.NearSuspicionRadius);
#if UNITY_EDITOR
                    suspicionSystem.linkedGimmick = ambush; // 에디터에서 OnValidate용
#endif
                    // 의심도 모듈 주입 (거리 기반 계산)
                    var suspicionModule = new AmbushSuspicionModule(ambush);
                    suspicionSystem.SetSuspicionModule(suspicionModule);

                    // Ground Bounds 전달 (매복 위치 생성 시 사용)
                    if (_isGroundBoundsScanned)
                    {
                        ambush.SetGroundBounds(_groundBounds);
                    }
                }
            }
        }

        /// <summary>
        /// 기믹 콜백 연결
        /// </summary>
        private void ConnectGimmickCallbacks()
        {
            if (_activeGimmick == null) return;

            switch (_activeGimmick)
            {
                case AmbushGimmick ambush:
                    ConnectAmbushCallbacks(ambush);
                    break;
                case RelentlessChaseGimmick relentless:
                    ConnectRelentlessChaseCallbacks(relentless);
                    break;
                case ElectricZoneGimmick electric:
                    ConnectElectricZoneCallbacks(electric);
                    break;
                case LureBaitGimmick lure:
                    ConnectLureBaitCallbacks(lure);
                    break;
                case DashChargeGimmick dash:
                    ConnectDashChargeCallbacks(dash);
                    break;
            }
        }

        /// <summary>
        /// 가자미: 매복 기믹 콜백
        /// </summary>
        private void ConnectAmbushCallbacks(AmbushGimmick ambush)
        {
            // 속도 제어
            ambush.OnSpeedOverride = (speed) => _movement.Speed = speed;

            // 이동 멈춤/재개
            ambush.OnMovementStop = () => _movement.Stop();
            ambush.OnMovementResume = () => _movement.Speed = patrolSpeed;

            // 시야 모드 전환 (거리 전용 모드 ↔ 일반 시야)
            ambush.OnVisibilityToggle = (ambushMode) =>
            {
                if (visionSensor != null)
                {
                    visionSensor.SetDistanceOnlyMode(ambushMode);
                }
            };

            // 의심도 상승 (AmbushGimmick → BossSuspicionSystem 연동)
            // 참고: 의심도 계산은 AmbushSuspicionModule에서 처리됨 (하위 호환용 콜백 유지)
            ambush.OnSuspicionIncrease = (rate, deltaTime) =>
            {
                if (suspicionSystem != null)
                {
                    suspicionSystem.AddSuspicion(rate, deltaTime);
                }
            };

            // 매복 위치 재설정 (Player 근처 랜덤 위치로 이동)
            ambush.OnRelocateAmbush = (targetPosition) =>
            {
                Vector3 clampedTarget = ClampToGroundBounds(targetPosition);
                _movement.MoveTo(clampedTarget);
            };

            // PatrolBehavior 이동 제어권 토글
            ambush.OnPatrolBehaviorOverride = (isOverridden) =>
            {
                if (_patrolBehavior != null)
                {
                    _patrolBehavior.SetMovementOverride(isOverridden);
                }
            };

            // 돌진 모드 토글 (돌진 중에는 ChaseBehavior 우회)
            ambush.OnDashModeToggle = (isDashing) =>
            {
                // 돌진 중에는 ChaseBehavior의 예측 이동 대신 직선 돌진
                // ChaseBehavior가 MoveTo를 호출하지만, 속도가 dashSpeed로 오버라이드됨
            };

            // 돌진 완료 → 일반 ChaseBehavior로 복귀
            ambush.OnDashCompleted = (dashTarget) =>
            {
                // 돌진 완료 후 ChaseBehavior가 계속 Player 추적
                // 상태 전환 불필요 (이미 Chase 상태)
            };
        }

        /// <summary>
        /// 곰치: 집요한 추격 기믹 콜백
        /// </summary>
        private void ConnectRelentlessChaseCallbacks(RelentlessChaseGimmick relentless)
        {
            relentless.SetOriginalSpeed(patrolSpeed);
        }

        /// <summary>
        /// 전기뱀장어: 감전 구역 기믹 콜백
        /// </summary>
        private void ConnectElectricZoneCallbacks(ElectricZoneGimmick electric)
        {
        }

        /// <summary>
        /// 아귀: 발광 미끼 기믹 콜백
        /// </summary>
        private void ConnectLureBaitCallbacks(LureBaitGimmick lure)
        {
            if (_isGroundBoundsScanned)
            {
                lure.SetGroundBounds(_groundBounds);
            }
            lure.OnChaseTriggered = () =>
            {
                if (_stateMachine != null)
                {
                    _stateMachine.TryTransitionTo(EnemyAIState.Chase);
                }
            };
        }

        /// <summary>
        /// 백상아리: 초고속 돌진 기믹 콜백
        /// </summary>
        private void ConnectDashChargeCallbacks(DashChargeGimmick dash)
        {
            dash.OnSpeedOverride = (speed) => _movement.Speed = speed;
            dash.OnDashCompleted = () =>
            {
                if (_stateMachine != null)
                {
                    _stateMachine.TryTransitionTo(EnemyAIState.Patrol);
                }
            };
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

            // 이벤트 핸들러를 Initialize 전에 등록해야 초기 상태 설정이 적용됨
            _stateMachine.OnStateChanged += OnAIStateChanged;
            _stateMachine.Initialize(EnemyAIState.Patrol);

            // 초기 속도
            _movement.Speed = patrolSpeed;
        }

        protected override void UpdateAI(float deltaTime)
        {
            if (_stateMachine == null) return;

            // Player Transform 재확인 (null이면 재탐색)
            if (_playerTransform == null)
            {
                FindPlayer();
            }

            // ChaseBehavior의 PlayerTransform 업데이트 (매 프레임)
            if (_playerTransform != null && _chaseBehavior != null)
            {
                _chaseBehavior.SetPlayerTransform(_playerTransform);
            }

            // Player 감지
            bool canSeePlayer = CanSeePlayer();

            // 의태 상태 업데이트
            UpdateCamouflageState();

            // 의심도 업데이트 (시야/근접 기반)
            UpdateSuspicion(canSeePlayer);

            // 상태 전환 체크
            CheckStateTransitions(canSeePlayer);

            // AI 상태 업데이트
            _stateMachine.Update(deltaTime);

            // 기믹 상태 업데이트
            UpdateGimmick(deltaTime);
        }

        /// <summary>
        /// 의태 상태 업데이트
        /// </summary>
        private void UpdateCamouflageState()
        {
            if (suspicionSystem == null) return;

            bool isCamouflaging = IsPlayerCamouflaging();
            bool isPerfect = _camouflageAdapter != null && _camouflageAdapter.IsPerfect;
            suspicionSystem.SetCamouflageState(isCamouflaging, isPerfect);
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
        /// CamouflageAdapter 캐싱 (Start에서 한 번만 호출)
        /// </summary>
        private void CacheCamouflageAdapter()
        {
            _camouflageAdapter = FindObjectOfType<HideAndInk.Player.CamouflageAdapter>();
        }

        /// <summary>
        /// Player가 의태 중인지 확인
        /// </summary>
        private bool IsPlayerCamouflaging()
        {
            return _camouflageAdapter != null && _camouflageAdapter.IsCamouflaging;
        }

        /// <summary>
        /// 의심도 업데이트 (시야/근접 기반)
        /// AmbushGimmick일 경우 의심도 계산은 AmbushSuspicionModule에서 전담 (이중 상승 방지)
        /// </summary>
        private void UpdateSuspicion(bool canSeePlayer)
        {
            if (suspicionSystem == null) return;

            // 가자미 기믹이 활성화되어 있으면
            if (_activeGimmick is AmbushGimmick)
            {
                // 의심도 계산은 AmbushSuspicionModule에서 전담 (거리 기반, 모든 상태 커버)
                // 컨트롤러에서는 의태 상태만 전달
                return;
            }
            else
            {
                // 일반 보스: 시야 기반 의심도 보고 (의태 중이면 무시)
                if (canSeePlayer)
                {
                    suspicionSystem.ReportVisionDetection(1f);
                }
            }
        }

        /// <summary>
        /// 상태 전환 조건 체크
        /// </summary>
        private void CheckStateTransitions(bool canSeePlayer)
        {
            if (_stateMachine == null || suspicionSystem == null) return;

            var currentState = _stateMachine.CurrentState;
            var suspicionLevel = suspicionSystem.CurrentLevel;

            // Ambush 기믹 전용: 의심도 기반 상태 전환
            bool isAmbushGimmick = _activeGimmick is AmbushGimmick;
            float ambushDropThreshold = isAmbushGimmick ? (_activeGimmick as AmbushGimmick).SuspicionDropThreshold : 0f;
            float suspicionValue = suspicionSystem.CurrentValue;

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
                    // Chase → Search: Player 놓침 + 의심도 하락
                    // Ambush 기믹: 의심도가 임계값 이하로 떨어져야 Search로 전환
                    if (isAmbushGimmick)
                    {
                        if (suspicionValue < ambushDropThreshold)
                        {
#if UNITY_EDITOR
                            Debug.Log($"[BossEnemyController] Ambush: Suspicion dropped ({suspicionValue:F1} < {ambushDropThreshold:F1}) → Search");
#endif
                            if (_playerTransform != null)
                            {
                                _searchBehavior.SetLastKnownPosition(_playerTransform.position);
                            }
                            _stateMachine.TryTransitionTo(EnemyAIState.Search);
                        }
                    }
                    else
                    {
                        // 일반 보스: Player 놓치면 바로 Search
                        if (!canSeePlayer)
                        {
                            if (_playerTransform != null)
                            {
                                _searchBehavior.SetLastKnownPosition(_playerTransform.position);
                            }
                            _stateMachine.TryTransitionTo(EnemyAIState.Search);
                        }
                    }
                    break;

                case EnemyAIState.Search:
                    // Search → Chase: Player 재발견
                    if (canSeePlayer)
                    {
                        _stateMachine.TryTransitionTo(EnemyAIState.Chase);
                    }
                    // Ambush 전용: 의심도 하락으로 매복 복귀
                    else if (isAmbushGimmick && suspicionValue < ambushDropThreshold)
                    {
#if UNITY_EDITOR
                        Debug.Log($"[BossEnemyController] Ambush: Suspicion dropped in Search ({suspicionValue:F1} < {ambushDropThreshold:F1}) → Patrol (Re-ambush)");
#endif
                        _stateMachine.TryTransitionTo(EnemyAIState.Patrol);
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
                    // Patrol: 매복 모드 (거리 전용 360도)
                    if (_activeGimmick is AmbushGimmick && visionSensor != null)
                    {
                        visionSensor.SetDistanceOnlyMode(true);
#if UNITY_EDITOR
                        Debug.Log($"[BossEnemyController] Patrol: 360도 거리 전용 모드 활성화");
#endif
                    }
                    // 매복 중에는 시야각 표시 비활성화
                    if (_activeGimmick is AmbushGimmick && visionConeRenderer != null)
                    {
                        visionConeRenderer.enabled = false;
                    }
                    break;
                case EnemyAIState.Chase:
                    _movement.Speed = chaseSpeed;
                    // Chase: 360도 감지 (매복 보스는 Player 위치 이미 파악)
                    if (_activeGimmick is AmbushGimmick && visionSensor != null)
                    {
                        visionSensor.SetDistanceOnlyMode(true);
#if UNITY_EDITOR
                        Debug.Log($"[BossEnemyController] Chase: 360도 거리 전용 모드 활성화");
#endif
                    }
                    // Chase에서는 시야각 표시 복귀
                    if (visionConeRenderer != null)
                    {
                        visionConeRenderer.enabled = true;
                    }
                    break;
                case EnemyAIState.Search:
                    _movement.Speed = searchSpeed;
                    // Search: 부채꼴 모드 복귀 (일반 수색)
                    if (_activeGimmick is AmbushGimmick && visionSensor != null)
                    {
                        visionSensor.SetDistanceOnlyMode(false);
#if UNITY_EDITOR
                        Debug.Log($"[BossEnemyController] Search: 부채꼴 모드 복귀");
#endif
                    }
                    // Search에서는 시야각 표시 복귀
                    if (visionConeRenderer != null)
                    {
                        visionConeRenderer.enabled = true;
                    }
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
        /// Player 위치가 Ground 위에 있으면 해당 위치로 이동
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

#if UNITY_EDITOR
                Debug.Log($"[BossEnemy] Player position alerted: {playerPosition}");
#endif
            }
            else
            {
#if UNITY_EDITOR
                Debug.Log($"[BossEnemy] Alert ignored - Player position not on Ground: {playerPosition}");
#endif
            }
        }

        #endregion

        /// <summary>
        /// 이동 처리 오버라이드
        /// Chase 상태에서는 Ground 검증을 완화 (Player 추적 우선)
        /// </summary>
        protected override void UpdateMovement(float deltaTime)
        {
            if (_movement == null) return;

            _movement.Update(deltaTime);

            // Chase 상태에서는 Ground 검증을 스킵 (Player 추적 우선)
            bool isChasing = _stateMachine != null && _stateMachine.IsChase;

            if (_movement.IsMoving && !isChasing)
            {
                // 이동 방향 앞쪽에 Ground가 있는지 확인
                if (!_movement.IsGroundAhead())
                {
                    // Ground가 없으면 이동 중지 및 방향 전환
                    _movement.Stop();
                    OnGroundEdgeReached();
                    return;
                }
            }

            // 속도를 실제 Transform에 적용 (X-Z 평면, Y축 고정)
            Vector3 newPosition = transform.position;
            newPosition.x += _movement.Velocity.x * deltaTime;
            newPosition.z += _movement.Velocity.z * deltaTime;
            // Y축은 고정
            transform.position = newPosition;
        }

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
