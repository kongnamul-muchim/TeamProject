using UnityEngine;
using HideAndInk.Core.Enemy.AI;
using HideAndInk.Core.Enemy.AI.Behaviors;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Perception;
using HideAndInk.Core.Enemy.Boss.Gimmicks;
using HideAndInk.Core.Player;
using HideAndInk.Core.Enemy.Movement;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Enemy.Boss
{
    public class BossEnemyController : EnemyAIController
    {
        public override EnemyType Type => EnemyType.Boss;

        [Header("보스 설정")]
        [SerializeField] private ConeVisionSensor visionSensor;
        [SerializeField] private BossSuspicionSystem suspicionSystem;
        [SerializeField] private VisionConeRenderer visionConeRenderer;
        [Tooltip("시야 없이 거리만으로 Chase 진입하는 거리 (m). 0 이하이면 비활성화")]
        [SerializeField] private float proximityChaseDistance = 10f;

        [Header("상태별 속도")]
        [SerializeField] private float patrolSpeed = 2f;
        [SerializeField] private float chaseSpeed = 5f;
        [SerializeField] private float searchSpeed = 3f;
        [SerializeField] private float searchDistance = 3f;
        [SerializeField] private float searchDuration = 5f;

        [Header("기믹 설정")]
        [SerializeField] private ScriptableObject gimmickAsset;
        [SerializeField] private MonoBehaviour customGimmick;

        [Header("의심도 설정")]
        [SerializeField] private float chaseSuspicionDecayMultiplier = 0.5f;

        [Header("돌진 인디케이터")]
        [SerializeField] private float chargeIndicatorLength = 12f;
        [SerializeField] private float chargeIndicatorWidth = 1.5f;

        [Header("스프라이트 (Animator-safe flipX)")]
        [Tooltip("Animator가 붙은 SpriteRenderer. flipX로 좌우 반전")]
        [SerializeField] private SpriteRenderer bossSprite;

        [Header("가자미 구덩이 (SandPit)")]
        [SerializeField] private SandPit sandPitPrefab;

        [Header("데미지")]
        [SerializeField] private float knockbackForce = 12f;

        private EnemyAIStateMachine _stateMachine;
        private PatrolBehavior _patrolBehavior;
        private ChaseBehavior _chaseBehavior;
        private SearchBehavior _searchBehavior;
        private IEnemyGimmick _activeGimmick;
        private PlayerLives _playerLives;
        private Rigidbody _playerRigidbody;

        // 기믹 인터페이스 캐싱 (SOLID - ISP)
        private IGimmickPlayerAware _playerAware;
        private IGimmickViewDirection _viewDir;
        private IGimmickCombatCycle _combatCycle;
        private IGimmickTransitionOverride _transitionOverride;

        // 돌진 인디케이터 (청새치 Aiming 시 붉은 사각형)
        private LineRenderer _chargeIndicator;

        // 가자미 구덩이 디버프
        private float _pitDebuffTimer;

        protected override void Start()
        {
            isDefaultFacingLeft = true; // 청새치 Sprite: Y=0에서 왼쪽 바라봄
            base.Start();
            CacheCamouflageAdapter(); // 의태 감지를 위해 반드시 필요
            CacheBossPlayerComponents();
            SetupRigidbody();
            InitializeGimmick();
            InitializeBehaviors();
            InitializeStateMachine();
            InitializeChargeIndicator(); // Indicator는 Start에서 미리 생성
        }

        #region Gimmick

        private void InitializeGimmick()
        {
            if (gimmickAsset != null)
                _activeGimmick = gimmickAsset as IEnemyGimmick;
            else if (customGimmick != null)
                _activeGimmick = customGimmick as IEnemyGimmick;

            if (_activeGimmick == null) return;

            ConnectGimmickCallbacks();
            _activeGimmick.OnActivate(transform);

            // 기믹 인터페이스 캐싱 (as 패턴: 미구현 시 null)
            _playerAware = _activeGimmick as IGimmickPlayerAware;
            _viewDir = _activeGimmick as IGimmickViewDirection;
            _combatCycle = _activeGimmick as IGimmickCombatCycle;
            _transitionOverride = _activeGimmick as IGimmickTransitionOverride;

            if (_activeGimmick is AmbushGimmick ambush && suspicionSystem != null)
            {
                suspicionSystem.SetSuspicionRadius(ambush.SuspicionRadius);
                suspicionSystem.linkedGimmick = ambush; // Editor OnValidate용
                var suspicionModule = new AmbushSuspicionModule(ambush);
                suspicionSystem.SetSuspicionModule(suspicionModule);

                // 의심도 100% 발각 → 강제 Chase 전환 (Patrol/Search 모두 대응)
                suspicionSystem.OnDetected += () =>
                {
                    if (_stateMachine != null)
                    {
                        var cur = _stateMachine.CurrentState;
                        // 이미 Chase 중이면 skip
                        if (cur != EnemyAIState.Chase)
                            _stateMachine.TryTransitionTo(EnemyAIState.Chase);
                    }
                };

                if (_isGroundBoundsScanned)
                    ambush.SetGroundBounds(_groundBounds);
            }
        }

        private void ConnectGimmickCallbacks()
        {
            switch (_activeGimmick)
            {
                case AmbushGimmick ambush:
                    ambush.OnSpeedOverride = (s) =>
                    {
                        _movement.Speed = s;
                        if (_movement is EnemyMovement em)
                        {
                            em.SetMaxSpeed(Mathf.Max(s, 1f));
                            // Dash 시 가속도도 함께 높여 순간적인 속도 도달 보장
                            if (s > 5f)
                                em.SetAcceleration(s * 2f); // dashSpeed=12 → accel=24
                            else
                                em.SetAcceleration(8f);     // 기본값 복원
                        }
                    };
                    ambush.OnMovementStop = () => _movement.Stop();
                    ambush.OnMovementResume = () => { _movement.Speed = chaseSpeed; if (_movement is EnemyMovement em) em.SetMaxSpeed(chaseSpeed); };
                    ambush.OnVisibilityToggle = (v) => visionSensor?.SetDistanceOnlyMode(v);
                    ambush.OnDashMoveTo = (t) => _movement.MoveTo(t);
                    ambush.OnSpawnPit = SpawnSandPitCluster;
                    ambush.OnSetChasePaused = (p) => _chaseBehavior?.SetPaused(p);
                    ambush.OnDashTrigger = () =>
                    {
                        var anim = GetComponent<Animator>();
                        if (anim != null) anim.SetTrigger("OnDash");
                    };
                    ambush.OnCombatStateChanged = (inCombat) =>
                    {
                        if (inCombat)
                            suspicionSystem?.BlockSuspicionIncrease();
                        else
                            suspicionSystem?.AllowSuspicionIncrease();
                    };
                    break;

                case RelentlessChaseGimmick relentless:
                    relentless.OnSuspicionDecayRateOverride = (m) => suspicionSystem?.SetSuspicionDecayMultiplier(m);
                    break;

                case SwordfishGimmick swordfish:
                    swordfish.OnSpeedOverride = (s) => { _movement.Speed = s; if (_movement is EnemyMovement em) em.SetMaxSpeed(Mathf.Max(s, 1f)); };
                    swordfish.OnMoveTo = (t) => _movement.MoveTo(t);
                    swordfish.OnMovementStop = () => _movement.Stop();
                    swordfish.OnChasePauseRequest = (p) => _chaseBehavior?.SetPaused(p);
                    break;

                case DashChargeGimmick dash:
                    dash.OnSpeedOverride = (s) => _movement.Speed = s;
                    dash.OnDashCompleted = () => _stateMachine?.TryTransitionTo(EnemyAIState.Patrol);
                    break;
            }
        }

        #endregion

        #region AI Initialization

        private void InitializeBehaviors()
        {
            _patrolBehavior = new PatrolBehavior(this, _movement, _activeGimmick);
            _chaseBehavior = new ChaseBehavior(this, _movement, _playerTransform, predictionTime: 0.5f);
            _searchBehavior = new SearchBehavior(this, _movement, _activeGimmick, searchDuration, searchDistance);

            if (_isGroundBoundsScanned)
            {
                _patrolBehavior.SetGroundBounds(_groundBounds);
                _chaseBehavior.SetGroundBounds(_groundBounds);
                _searchBehavior.SetGroundBounds(_groundBounds);
            }
        }

        private void InitializeStateMachine()
        {
            _stateMachine = new EnemyAIStateMachine(_patrolBehavior, _chaseBehavior, _searchBehavior);
            _stateMachine.OnStateChanged += OnAIStateChanged;
            _stateMachine.Initialize(EnemyAIState.Patrol);
            _movement.Speed = patrolSpeed;
        }

        #endregion

        #region AI Update

        protected override void UpdateAI(float deltaTime)
        {
            if (_stateMachine == null) return;

            if (_playerTransform == null) FindPlayer();
            if (_playerTransform == null) return;

            _chaseBehavior?.SetPlayerTransform(_playerTransform);

            bool canSeePlayer = CanSeePlayer();

            // 기믹에 Player Transform + 의심도 + 의태 + 시야 상태 전달 (IGimmickPlayerAware)
            _playerAware?.SetPlayerTransform(_playerTransform);
            _playerAware?.SetCamouflageState(IsPlayerCamouflaging());
            _playerAware?.SetPlayerVisible(canSeePlayer);
            if (_playerAware != null && suspicionSystem != null)
            {
                _playerAware.SetSuspicionLevel(Mathf.Clamp01(suspicionSystem.CurrentValue / 100f));
            }
            UpdateCamouflageState();
            UpdateSuspicion(canSeePlayer);
            UpdateVisionConeVisibility();
            CheckStateTransitions(canSeePlayer);
            _stateMachine.Update(deltaTime);
            UpdateGimmick(deltaTime);
            RestoreSpeedAfterGimmick();
            UpdatePitDebuff(deltaTime);
        }

        private void UpdateCamouflageState()
        {
            if (suspicionSystem == null) return;
            bool isCamouflaging = IsPlayerCamouflaging();
            bool isPerfect = _camouflageAdapter != null && _camouflageAdapter.IsPerfect;
            suspicionSystem.SetCamouflageState(isCamouflaging, isPerfect);
        }

        private void UpdateGimmick(float deltaTime)
        {
            if (_activeGimmick == null || _stateMachine == null) return;

            switch (_stateMachine.CurrentState)
            {
                case EnemyAIState.Patrol: _activeGimmick.OnPatrolUpdate(deltaTime); break;
                case EnemyAIState.Chase: _activeGimmick.OnChaseUpdate(deltaTime); break;
                case EnemyAIState.Search: _activeGimmick.OnSearchUpdate(deltaTime); break;
            }
        }

        /// <summary>
        /// 기믹이 제어권을 반납했는데 speed가 0인 상태면 현재 상태에 맞게 복원
        /// 모든 기믹의 speed 복원 누락을 안전하게 처리
        /// </summary>
        private void RestoreSpeedAfterGimmick()
        {
            if (_activeGimmick == null || _stateMachine == null) return;
            if (_activeGimmick.HasMovementOverride) return; // 기믹이 아직 제어 중

            // 현재 상태에 맞는 목표 속도
            float targetSpeed = _stateMachine.CurrentState switch
            {
                EnemyAIState.Patrol => patrolSpeed,
                EnemyAIState.Chase => chaseSpeed,
                EnemyAIState.Search => searchSpeed,
                _ => patrolSpeed
            };

            // 속도가 비정상적으로 낮으면 복원 (0.5f 이하는 기믹이 0으로 내려놓은 것으로 간주)
            if (_movement != null && _movement.Speed < targetSpeed * 0.5f && _movement.Speed < 1f)
            {
                float prevSpeed = _movement.Speed;
                _movement.Speed = targetSpeed;
#if UNITY_EDITOR
                Debug.Log($"[BossEnemyController] Gimmick speed restored: {prevSpeed:F1} → {targetSpeed:F1} ({_stateMachine.CurrentState})");
#endif
            }
        }

        #endregion

        #region Detection & Suspicion

        private bool CanSeePlayer()
        {
            return visionSensor != null && _playerTransform != null &&
                   visionSensor.CanSee(_playerTransform.gameObject);
        }

        /// <summary>
        /// 시야 없이 거리만으로 Player 감지 (Chase 진입용)
        /// AmbushGimmick 등 시야를 가리는 기믹에서도 동작하도록 보장
        /// </summary>
        private bool IsPlayerCloseEnough()
        {
            if (_playerTransform == null) return false;
            if (proximityChaseDistance <= 0f) return false;
            if (IsPlayerCamouflaging()) return false; // 의태 중엔 근접 감지 안 함

            float distance = Vector3.Distance(transform.position, _playerTransform.position);
            return distance <= proximityChaseDistance;
        }

        private void UpdateSuspicion(bool canSeePlayer)
        {
            if (suspicionSystem == null) return;

            if (_activeGimmick is AmbushGimmick)
            {
                // Ambush: 모듈(거리 기반)이 주 계산, 시야각은 추가 보너스
                if (canSeePlayer && !IsPlayerCamouflaging())
                {
                    suspicionSystem.ReportVisionDetection(0.5f);
                }
                return;
            }

            if (canSeePlayer && !IsPlayerCamouflaging() && visionSensor != null && visionSensor.RaisesSuspicion)
            {
                suspicionSystem.ReportVisionDetection(1f);
            }
        }

        private void UpdateVisionConeVisibility()
        {
            // Ambush도 Patrol에서 시각적 피드백: vision cone + 바닥 표시
            // Chase 중에는 바닥 숨김 (전투 중엔 범위 표시 불필요)
            bool isAmbush = _activeGimmick is AmbushGimmick;
            if (visionConeRenderer != null)
                visionConeRenderer.SetChasing(_stateMachine != null && _stateMachine.IsChase);

            if (suspicionSystem != null)
            {
                bool isPatrol = _stateMachine != null && _stateMachine.IsPatrol;
                SuspicionFloorVisibilityMode floorMode = isAmbush
                    ? (isPatrol ? SuspicionFloorVisibilityMode.AlwaysOn : SuspicionFloorVisibilityMode.Hidden)
                    : SuspicionFloorVisibilityMode.ChaseOnly;
                suspicionSystem.SetFloorVisibilityMode(floorMode);
            }
        }

        #endregion

        #region State Transitions

        private void CheckStateTransitions(bool canSeePlayer)
        {
            if (_stateMachine == null || suspicionSystem == null) return;

            var currentState = _stateMachine.CurrentState;
            float suspicionValue = suspicionSystem.CurrentValue;
            bool isAmbushGimmick = _activeGimmick is AmbushGimmick;
            float ambushDropThreshold = isAmbushGimmick ? ((AmbushGimmick)_activeGimmick).SuspicionDropThreshold : 0f;

            switch (currentState)
            {
                case EnemyAIState.Patrol:
                    if (isAmbushGimmick)
                    {
                        // Ambush: 오직 의심도 시스템(OnDetected)으로만 Chase 진입.
                        // 근접 자동 Chase 없음 → 의심도가 자연스럽게 쌓여야 발각
                    }
                    else if (canSeePlayer || IsPlayerCloseEnough())
                    {
                        _stateMachine.TryTransitionTo(EnemyAIState.Chase);
                    }
                    break;

                case EnemyAIState.Chase:
                    if (isAmbushGimmick)
                    {
                        // Ambush: PreDelay/Dash/Rest 중에는 강제로 Patrol 복귀하지 않음
                        bool inCombatCycle = _combatCycle != null && _combatCycle.IsInCombatCycle;
                        // 의심도가 Safe(30) 아래로 떨어지면 Patrol 복귀
                        if (!inCombatCycle && suspicionValue < 30f)
                            _stateMachine.TryTransitionTo(EnemyAIState.Patrol);
                    }
                    else
                    {
                        bool inCombatCycle = _combatCycle != null && _combatCycle.IsInCombatCycle;
                        bool shouldLosePlayer = _chaseBehavior != null && (
                            _chaseBehavior.IsPlayerOutOfRange() ||
                            (IsPlayerCamouflaging() && !inCombatCycle) // 의태 + 전투 사이클 無 → Player 놓침
                        );
                        if (!inCombatCycle && shouldLosePlayer)
                        {
                            float normalized = Mathf.Clamp01(suspicionValue / 100f);

                            if (_transitionOverride != null && _transitionOverride.ShouldSkipSearchOnLostPlayer(normalized))
                            {
                                // 의심도 Safe → Patrol 직행 (위치 기억 안 함)
                                _stateMachine.TryTransitionTo(EnemyAIState.Patrol);
                            }
                            else if (IsPlayerCamouflaging())
                            {
                                // 의태로 놓침 → 위치 정보 없이 Search (기억 안 함)
                                _stateMachine.TryTransitionTo(EnemyAIState.Search);
                            }
                            else
                            {
                                // 의심도 높음 → Search (마지막 위치 기억)
                                if (_playerTransform != null)
                                    _searchBehavior.SetLastKnownPosition(_playerTransform.position);
                                _stateMachine.TryTransitionTo(EnemyAIState.Search);
                            }
                        }
                    }
                    break;

                case EnemyAIState.Search:
                    if (canSeePlayer)
                        _stateMachine.TryTransitionTo(EnemyAIState.Chase);
                    else if (isAmbushGimmick && suspicionValue < ambushDropThreshold)
                        _stateMachine.TryTransitionTo(EnemyAIState.Patrol);
                    else if (_searchBehavior.IsSearchTimeout())
                        _stateMachine.TryTransitionTo(EnemyAIState.Patrol);
                    break;
            }
        }

        private void OnAIStateChanged(EnemyAIState previous, EnemyAIState current)
        {
            switch (current)
            {
                case EnemyAIState.Patrol:
                    _movement.Speed = patrolSpeed;
                    if (suspicionSystem != null)
                    {
                        suspicionSystem.SetVisionIncreaseSpeed(10f); // Patrol 중 기본 상승
                        // Ambush는 거리 기반 느린 증가 → decay를 낮춰야 의심도가 쌓임
                        float decayMul = _activeGimmick is AmbushGimmick ? 0.2f : 1f;
                        suspicionSystem.SetSuspicionDecayMultiplier(decayMul);
                        // Patrol 복귀 시 발각 상태 리셋 (재발각 가능)
                        if (_activeGimmick is AmbushGimmick)
                            suspicionSystem.ResetDetected();
                    }
                    break;
                case EnemyAIState.Chase:
                    _movement.Speed = chaseSpeed;
                    if (suspicionSystem != null)
                    {
                        // Chase 중 빠른 의심도 상승 (점프 없이 rate 기반)
                        suspicionSystem.SetVisionIncreaseSpeed(30f);
                        suspicionSystem.SetSuspicionDecayMultiplier(chaseSuspicionDecayMultiplier);
                    }
                    break;
                case EnemyAIState.Search:
                    _movement.Speed = searchSpeed;
                    if (suspicionSystem != null)
                    {
                        suspicionSystem.SetVisionIncreaseSpeed(15f); // Search 중 중간 상승
                        suspicionSystem.SetSuspicionDecayMultiplier(1f);
                    }
                    break;
            }

            // Gimmick enter/exit
            if (_activeGimmick != null)
            {
                switch (previous)
                {
                    case EnemyAIState.Patrol: _activeGimmick.OnPatrolExit(); break;
                    case EnemyAIState.Chase: _activeGimmick.OnChaseExit(); break;
                    case EnemyAIState.Search: _activeGimmick.OnSearchExit(); break;
                }
                switch (current)
                {
                    case EnemyAIState.Patrol: _activeGimmick.OnPatrolEnter(); break;
                    case EnemyAIState.Chase: _activeGimmick.OnChaseEnter(); break;
                    case EnemyAIState.Search: _activeGimmick.OnSearchEnter(); break;
                }
            }
        }

        protected override void OnGroundEdgeReached()
        {
            base.OnGroundEdgeReached();

            // PatrolBehavior가 같은 경계 목표를 반복하지 않도록 강제 전환
            if (_activeGimmick == null || !_activeGimmick.HasMovementOverride)
            {
                // 보스의 현재 이동 방향 기준 반대 방향으로 새 Patrol 목표 설정
                if (_movement != null)
                {
                    float reverseX = _movement.Velocity.x > 0f ? -1f : 1f;
                    Vector3 newTarget = transform.position + new Vector3(reverseX * 5f, 0f, 0f);
                    _movement.MoveTo(newTarget);
                }

                // PatrolBehavior의 현재 목표를 무효화 (다음 OnUpdate에서 PickNewTarget 실행 유도)
                if (_patrolBehavior != null)
                {
                    _patrolBehavior.InvalidateTarget();
                }
            }
        }

        #endregion

        #region Movement & Direction

        protected override void UpdateMovement(float deltaTime)
        {
            base.UpdateMovement(deltaTime);

            if (_isGroundBoundsScanned)
            {
                Vector3 clamped = _groundBounds.ClampXZ(transform.position);
                clamped.y = transform.position.y;
                transform.position = clamped;
            }
        }

        protected override void UpdateViewDirection()
        {
            // 쿨타임 감소 (base class 로직 유지)
            if (_directionChangeTimer > 0f)
                _directionChangeTimer -= Time.deltaTime;

            Vector3? facingDir = null;

            // 1순위: 기믹 방향 오버라이드
            if (_viewDir != null && _viewDir.OverridesViewDirection && _playerTransform != null)
            {
                facingDir = _viewDir.GetViewDirectionVector();

                if (_viewDir.ShowChargeIndicator)
                    UpdateChargeIndicator(facingDir.Value);
                else
                    HideChargeIndicator();
            }
            // 2순위: 이동 방향
            else
            {
                HideChargeIndicator();

                if (_movement == null || !_movement.IsMoving) return;
                if (_directionChangeTimer > 0f) return; // 쿨타임 중

                float vx = _movement.Velocity.x;
                if (Mathf.Abs(vx) < 0.01f) return;

                facingDir = vx > 0f ? Vector3.right : Vector3.left;

                // 방향 변경 쿨타임 (base class와 동일한 flicker 방지)
                MoveDirection newDir = vx > 0f ? MoveDirection.Right : MoveDirection.Left;
                if (newDir != _lastAppliedDirection)
                {
                    _lastAppliedDirection = newDir;
                    _directionChangeTimer = _directionChangeCooldown;
                }
            }

            if (facingDir.HasValue)
            {
                ApplyFacingDirection(facingDir.Value);
            }
        }

        /// <summary>
        /// SpriteRenderer.flipX로 방향 전환 (Animator-safe)
        /// vision cone 방향도 함께 업데이트
        /// </summary>
        private void ApplyFacingDirection(Vector3 dir)
        {
            if (bossSprite != null)
            {
                bool flip = isDefaultFacingLeft ? dir.x > 0f : dir.x < 0f;
                bossSprite.flipX = flip;

#if UNITY_EDITOR
                Debug.Log($"[BossEnemyController] Facing: dir.x={dir.x:F2}, flipX={flip}, sprite={bossSprite.flipX}");
#endif
            }

            // Vision cone 방향 동기화 (transform.right 대신 custom 방향 사용)
            if (visionSensor != null)
            {
                visionSensor.SetCustomViewDirection(new Vector3(dir.x, 0f, 0f).normalized);
            }
        }

        #endregion

        #region Charge Indicator (청새치 돌진 예고)

        /// <summary>
        /// 돌진 인디케이터 초기화 (LineRenderer 기반 붉은 사각형)
        /// </summary>
        private void InitializeChargeIndicator()
        {
            if (_chargeIndicator != null) return;

            GameObject ind = new GameObject("ChargeIndicator");
            ind.transform.SetParent(transform);
            ind.transform.localPosition = Vector3.zero;
            ind.layer = gameObject.layer;

            _chargeIndicator = ind.AddComponent<LineRenderer>();
            _chargeIndicator.useWorldSpace = true;
            _chargeIndicator.loop = true;
            _chargeIndicator.positionCount = 4;

            // 붉은색 반투명
            _chargeIndicator.startColor = new Color(1f, 0f, 0f, 0.6f);
            _chargeIndicator.endColor = new Color(1f, 0f, 0f, 0.6f);

            // 선 두께
            _chargeIndicator.startWidth = 0.1f;
            _chargeIndicator.endWidth = 0.1f;

            // Z-fighting 방지
            _chargeIndicator.sortingLayerName = "Default";
            _chargeIndicator.sortingOrder = -1;

            // 재질 (Unlit/Color or Standard)
            Material mat = new Material(Shader.Find("Unlit/Color"));
            if (mat != null)
            {
                mat.color = new Color(1f, 0f, 0f, 0.6f);
                _chargeIndicator.material = mat;
            }

            _chargeIndicator.enabled = false;
        }

        /// <summary>
        /// 돌진 방향으로 붉은 사각형 표시
        /// </summary>
        private void UpdateChargeIndicator(Vector3 facingDir)
        {
            if (_chargeIndicator == null) InitializeChargeIndicator(); // 안전장치
            if (_chargeIndicator == null) return;

            // 사각형 4개 꼭짓점 (로컬 좌표)
            // facingDir 방향으로 길게, 수직 방향으로 폭을 줌
            Vector3 perp = Vector3.Cross(facingDir, Vector3.up).normalized;
            float halfW = chargeIndicatorWidth * 0.5f;

            // 보스 월드 위치 기준으로 indicator 배치
            Vector3 bossWorldPos = transform.position;

            // 바닥 높이 찾기 (보스의 groundLayer 사용)
            float floorY = bossWorldPos.y + 0.05f;
            int floorLayer = groundLayer.value > 0 ? groundLayer.value : (1 << LayerMask.NameToLayer("Default"));
            if (Physics.Raycast(bossWorldPos + Vector3.up * 0.1f, Vector3.down,
                out RaycastHit floorHit, 5f, floorLayer))
            {
                floorY = floorHit.point.y + 0.05f;
            }

            Vector3[] corners = new Vector3[4];
            Vector3 origin = new Vector3(bossWorldPos.x, floorY, bossWorldPos.z);
            // 시작점 (보스 위치 약간 앞) — 월드 좌표
            corners[0] = origin + facingDir * 0.5f + perp * halfW;
            corners[1] = origin + facingDir * 0.5f - perp * halfW;
            // 끝점 (chargeIndicatorLength 앞)
            corners[2] = origin + facingDir * chargeIndicatorLength - perp * halfW;
            corners[3] = origin + facingDir * chargeIndicatorLength + perp * halfW;

            _chargeIndicator.SetPositions(corners);
            _chargeIndicator.enabled = true;
        }

        /// <summary>
        /// 돌진 인디케이터 숨김
        /// </summary>
        private void HideChargeIndicator()
        {
            if (_chargeIndicator != null)
                _chargeIndicator.enabled = false;
        }

        #endregion

        #region Player Damage

        /// <summary>
        /// Rigidbody 설정: kinematic + 회전/위치 고정
        /// (transform.position 직접 제어와 Rigidbody 중력 충돌 방지)
        /// </summary>
        private void SetupRigidbody()
        {
            var rb = GetComponent<Rigidbody>();
            if (rb == null) return;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.useGravity = false; // 중력 사용 안 함 (script가 직접 제어)
        }

        private void CacheBossPlayerComponents()
        {
            if (_playerTransform != null)
            {
                _playerLives = _playerTransform.GetComponent<PlayerLives>();
                _playerRigidbody = _playerTransform.GetComponent<Rigidbody>();
            }
            if (_playerLives == null)
                _playerLives = FindObjectOfType<PlayerLives>();
            if (_playerRigidbody == null && _playerTransform != null)
                _playerRigidbody = _playerTransform.GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!collision.gameObject.CompareTag("Player")) return;
            if (_playerLives == null || _playerLives.IsInvincible) return;
            if (!CanBossDamagePlayer()) return;

            _playerLives.TakeDamage();
            ApplyKnockback();
        }

        private bool CanBossDamagePlayer()
        {
            if (_stateMachine != null && _stateMachine.CurrentState == EnemyAIState.Chase)
                return true;

            if (_activeGimmick != null)
            {
                // Ambush는 HasMovementOverride로 판정 (항상 override라 dashing 여부와 무관)
                if (_activeGimmick is AmbushGimmick ambush && ambush.HasMovementOverride) return true;
                // 그 외 기믹은 IGimmickCombatCycle.IsCharging으로 판정
                if (_combatCycle != null && _combatCycle.IsCharging) return true;
            }
            return false;
        }

        private void ApplyKnockback()
        {
            if (_playerTransform == null || _playerRigidbody == null) return;
            Vector3 dir = (_playerTransform.position - transform.position).normalized;
            dir.y = 0f;
            _playerRigidbody.AddForce(dir * knockbackForce, ForceMode.Impulse);
        }

        #endregion

        #region SandPit (가자미 구덩이)

        /// <summary>
        /// 가자미가 떠난 자리에 SandPit 클러스터 생성 (GroundBounds 내로 클램프)
        /// </summary>
        private void SpawnSandPitCluster(Vector3 center)
        {
            if (sandPitPrefab == null) return;

            bool hasBounds = _groundBounds.MinX != _groundBounds.MaxX || _groundBounds.MinZ != _groundBounds.MaxZ;

            // 메인 Pit (GroundBounds 클램프)
            Vector3 clampedCenter = center;
            clampedCenter.y = transform.position.y;
            if (hasBounds)
            {
                clampedCenter.x = _groundBounds.ClampX(clampedCenter.x);
                clampedCenter.z = _groundBounds.ClampZ(clampedCenter.z);
            }
            SandPit mainPit = Instantiate(sandPitPrefab, clampedCenter, Quaternion.identity);
            SubscribeSandPit(mainPit);

            if (!(_activeGimmick is AmbushGimmick ambush)) return;

            // 주변 랜덤 추가 Pit (각각 GroundBounds 클램프)
            int extraCount = Random.Range(ambush.PitClusterCount.x, ambush.PitClusterCount.y + 1);
            for (int i = 0; i < extraCount; i++)
            {
                Vector3 offset = Random.insideUnitSphere * ambush.PitClusterRadius;
                offset.y = 0f;
                Vector3 pitPos = center + offset;
                pitPos.y = transform.position.y;
                if (hasBounds)
                {
                    pitPos.x = _groundBounds.ClampX(pitPos.x);
                    pitPos.z = _groundBounds.ClampZ(pitPos.z);
                }

                SandPit extra = Instantiate(sandPitPrefab, pitPos, Quaternion.identity);
                SubscribeSandPit(extra);
            }
        }

        /// <summary>
        /// SandPit의 Player 감지 이벤트 → 의심도 증가 + 디버프 (의심도 하락 차단)
        /// </summary>
        private void SubscribeSandPit(SandPit pit)
        {
            pit.OnPlayerEnterPit = (pos) =>
            {
                if (suspicionSystem != null)
                {
                    // 밟은 순간 1회 30 의심도 증가
                    suspicionSystem.AddSuspicion(30f, 1f);
                }
                // 5초간 의심도 하락 차단 디버프
                _pitDebuffTimer = 5f;
            };
        }

        /// <summary>
        /// 구덩이 디버프 업데이트: 의심도 하락을 90% 차단 (5초 지속)
        /// </summary>
        private void UpdatePitDebuff(float deltaTime)
        {
            if (_pitDebuffTimer > 0f && suspicionSystem != null)
            {
                _pitDebuffTimer -= deltaTime;
                // 의심도 하락을 거의 막음 (최소 0.1 → 90% 감소)
                suspicionSystem.SetSuspicionDecayMultiplier(0.1f);

                if (_pitDebuffTimer <= 0f)
                {
                    // 디버프 종료: 현재 상태에 맞는 배율 복원
                    float restoreMultiplier = _stateMachine?.CurrentState switch
                    {
                        EnemyAIState.Chase => chaseSuspicionDecayMultiplier,
                        _ => 1f
                    };
                    suspicionSystem.SetSuspicionDecayMultiplier(restoreMultiplier);
                }
            }
        }

        #endregion

        #region Alert (일반 몬스터 연동)

        public void AlertPlayerPosition(Vector3 playerPosition)
        {
            if (!_movement.IsPositionOnGround(playerPosition)) return;

            _searchBehavior.SetLastKnownPosition(playerPosition);
            _stateMachine?.TryTransitionTo(EnemyAIState.Chase);
        }

        #endregion

        protected virtual void OnDestroy()
        {
            if (_stateMachine != null)
                _stateMachine.OnStateChanged -= OnAIStateChanged;
            _activeGimmick?.OnDeactivate();

            if (_chargeIndicator != null && _chargeIndicator.gameObject != null)
            {
                Destroy(_chargeIndicator.gameObject); // GameObject Destroy 시 Material도 자동 해제
            }
        }
    }
}
