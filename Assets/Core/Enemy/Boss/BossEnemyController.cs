using UnityEngine;
using HideAndInk.Core.Enemy.AI;
using HideAndInk.Core.Enemy.AI.Behaviors;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Perception;
using HideAndInk.Core.Enemy.Boss.Gimmicks;
using HideAndInk.Core.Player;
using HideAndInk.Core.Enemy.Movement;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Events;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HideAndInk.Core.Enemy.Boss
{
    public class BossEnemyController : EnemyAIController
    {
        public override EnemyType Type => EnemyType.Boss;

        [Header("보스 설정")]
        [Tooltip("시야 감지 센서 (원뿔형)")]
        [SerializeField] private ConeVisionSensor visionSensor;
        [Tooltip("보스 전용 의심도 시스템")]
        [SerializeField] private BossSuspicionSystem suspicionSystem;
        [Tooltip("시야 원뿔 시각화 렌더러")]
        [SerializeField] private VisionConeRenderer visionConeRenderer;
        [Tooltip("시야 없이 거리만으로 Chase 진입하는 거리 (m). 0 이하이면 비활성화")]
        [SerializeField] private float proximityChaseDistance = 10f;

        [Header("상태별 속도")]
        [Tooltip("정찰 상태 이동 속도")]
        [SerializeField] private float patrolSpeed = 2f;
        [Tooltip("추적 상태 이동 속도")]
        [SerializeField] private float chaseSpeed = 5f;
        [Tooltip("수색 상태 이동 속도")]
        [SerializeField] private float searchSpeed = 3f;
        [Tooltip("수색 상태 탐색 거리")]
        [SerializeField] private float searchDistance = 3f;
        [Tooltip("수색 상태 지속 시간")]
        [SerializeField] private float searchDuration = 5f;

        [Header("기믹 설정")]
        [Tooltip("보스 기믹 에셋 (ScriptableObject)")]
        [SerializeField] private ScriptableObject gimmickAsset;
        [Tooltip("커스텀 기믹 컴포넌트")]
        [SerializeField] private MonoBehaviour customGimmick;

        [Header("의심도 설정")]
        [Tooltip("추적 중 의심도 감소 배율")]
        [SerializeField] private float chaseSuspicionDecayMultiplier = 0.5f;

        [Header("돌진 인디케이터")]
        [Tooltip("돌진 인디케이터 길이")]
        [SerializeField] private float chargeIndicatorLength = 12f;
        [Tooltip("돌진 인디케이터 너비")]
        [SerializeField] private float chargeIndicatorWidth = 1.5f;
        [Tooltip("타겟 인디케이터 프리팹")]
        [SerializeField] private GameObject targetIndicatorPrefab;

        [Header("스프라이트 (Animator-safe flipX)")]
        [Tooltip("Animator가 붙은 SpriteRenderer. flipX로 좌우 반전")]
        [SerializeField] private SpriteRenderer bossSprite;

        [Header("가자미 구덩이 (SandPit)")]
        [Tooltip("가자미 구덩이(SandPit) 프리팹")]
        [SerializeField] private SandPit sandPitPrefab;

        [Header("곰치 돌진 디렉터")]
        [Tooltip("곰치 돌진 디렉터")]
        [SerializeField] private MorayChargeDirector chargeDirector;

        [Header("데미지")]
        [Tooltip("넉백 힘")]
        [SerializeField] private float knockbackForce = 12f;

        private EnemyAIStateMachine _stateMachine;
        private PatrolBehavior _patrolBehavior;
        private ChaseBehavior _chaseBehavior;
        private SearchBehavior _searchBehavior;
        private IEnemyGimmick _activeGimmick;
        private Animator _animator;
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

        // 곰치 Chase 진입 횟수 (MonoBehaviour에서 직접 관리)
        private int _morayChaseEntryCount;
        // 돌진 시퀀스 활성화 플래그 (의심도 자동 증가 차단)
        private bool _isMorayCharging;
        // 곰치 렌더러/콜라이더 캐시 (숨김/표시 전환)
        private Renderer[] _bossRenderers;
        private Collider[] _bossColliders;
        private bool _isBossVisible;

        // Animator flipX override 방지용 캐시
        private Vector3 _lastFacingDir;
        private bool _hasFacingDir;

        // Ambush 근접 Chase 쿨타임 (강제 Patrol 후 재발견 방지)
        private float _ambushProximityCooldown;

        // Moray charge 방향 (Prepare/Charge 중 시야각 동기화용)
        private Vector3 _morayFacingDirection = Vector3.right;

        [Header("스프라이트 방향")]
        [SerializeField, Tooltip("Sprite 기본 방향이 왼쪽이면 true, 오른쪽이면 false")]
        private bool defaultFacingLeft = true;

        protected override void Start()
        {
            isDefaultFacingLeft = defaultFacingLeft;
            base.Start();
            _animator = GetComponent<Animator>(); // ★ InitializeStateMachine보다 먼저 할당
            CacheCamouflageAdapter();
            CacheBossPlayerComponents();
            SetupRigidbody();
            InitializeGimmick();
            InitializeBehaviors();
            InitializeStateMachine(); // ← 이제 _animator가 null 아님, SetBool 정상 동작
            InitializeChargeIndicator();

            // 렌더러/콜라이더 캐시
            _bossRenderers = GetComponentsInChildren<Renderer>();
            _bossColliders = GetComponentsInChildren<Collider>();
        }

        protected override void ScanGroundBounds()
        {
            base.ScanGroundBounds();
            // GroundBounds 확보 후 Director 재초기화
            if (_isGroundBoundsScanned && chargeDirector != null)
            {
                chargeDirector.Initialize(transform, _groundBounds);
            }
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
                // 거리 기반 감지 모듈 제거 — 근접 Chase(12m)로만 발각
                // 의심도는 Pit 밟을 때만 상승 (+30)
                suspicionSystem.SetSuspicionModule(null);

                // 바닥 원형 시각화를 근접 Chase 범위로 설정
                suspicionSystem.SetSuspicionRadius(Vector2.one * ambush.ProximityChaseDistance);

                // 의심도 100% 발각 → 강제 Chase 전환 (Pit 누적으로만 발동)
                suspicionSystem.OnDetected += () =>
                {
                    if (_stateMachine != null)
                    {
                        var cur = _stateMachine.CurrentState;
                        if (cur != EnemyAIState.Chase)
                            _stateMachine.TryTransitionTo(EnemyAIState.Chase);
                    }
                };

                // Ambush 전용 근접 Chase 거리 동기화 (인스펙터에서 조절 가능)
                proximityChaseDistance = ambush.ProximityChaseDistance;

                if (_isGroundBoundsScanned)
                    ambush.SetGroundBounds(_groundBounds);
            }

            if (_activeGimmick is RelentlessChaseGimmick relentless && suspicionSystem != null)
            {
                // 곰치는 ChaseBehavior 이동을 사용하지 않음 (돌진 중에만 움직임)
                _chaseBehavior?.SetPaused(true);

                // 의심도 100% → Patrol→Chase 전환
                suspicionSystem.OnDetected += () =>
                {
                    if (_stateMachine != null && _stateMachine.CurrentState != EnemyAIState.Chase)
                        _stateMachine.TryTransitionTo(EnemyAIState.Chase);
                };

                if (_isGroundBoundsScanned)
                    relentless.SetGroundBounds(_groundBounds);

                // Director 초기화 (GroundBounds 사용 가능 시에만)
                if (chargeDirector != null)
                {
                    if (_isGroundBoundsScanned)
                        chargeDirector.Initialize(transform, _groundBounds);
                    chargeDirector.OnPrepareTeleport += OnMorayPrepareTeleport;
                    chargeDirector.OnChargeExecute += OnMorayChargeExecute;
                    chargeDirector.OnChargesComplete += OnMorayChargesComplete;
                    chargeDirector.OnPlayerHit += OnMorayPlayerHit;
                    chargeDirector.OnSpeedOverride += OnMoraySpeedOverride;
                    chargeDirector.OnMovementStop += OnMorayMovementStop;
                }
            }

            if (_activeGimmick is DashChargeGimmick dash && suspicionSystem != null)
            {
                // 의심도 시스템 모듈 제거 (자체 의심도 상승 사용)
                suspicionSystem.SetSuspicionModule(null);

                // DashCharge: ChaseBehavior 정지 (기믹이 직접 Chase 제어)
                _chaseBehavior?.SetPaused(true);

                // 의심도 100% → Patrol→Chase 전환
                suspicionSystem.OnDetected += () =>
                {
                    if (_stateMachine != null && _stateMachine.CurrentState != EnemyAIState.Chase)
                        _stateMachine.TryTransitionTo(EnemyAIState.Chase);
                };

                // Player 사망 시 의심도 리셋 → Patrol 복귀
                var playerLives = _playerTransform != null
                    ? _playerTransform.GetComponent<PlayerLives>()
                    : FindObjectOfType<PlayerLives>();
                if (playerLives != null)
                {
                    playerLives.OnPlayerDied += () =>
                    {
                        suspicionSystem.ForceSetSuspicion(0f);
                        suspicionSystem.ResetDetected();
                        if (_stateMachine != null && _stateMachine.CurrentState == EnemyAIState.Chase)
                        {
                            _stateMachine.TryTransitionTo(EnemyAIState.Patrol);
                        }
                    };
                }
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
                    ambush.OnForcePatrol = () =>
                    {
                        _ambushProximityCooldown = 5f; // 강제 Patrol 후 5초 근접 Chase 쿨타임
                        if (_stateMachine != null && _stateMachine.CurrentState == EnemyAIState.Chase)
                            _stateMachine.TryTransitionTo(EnemyAIState.Patrol);
                    };
                    break;

                case RelentlessChaseGimmick relentless:
                    // Director 콜백: Chase 진입 시 돌진 준비
                    relentless.OnDirectorBeginPrepare = (_) =>
                    {
                        _chaseBehavior?.SetPaused(true);
                        if (_isMorayCharging) return;
                        _morayChaseEntryCount++;
                        int chargeCount = Mathf.Min(_morayChaseEntryCount, relentless.MaxChargesPerCycle);
                        _isMorayCharging = true;
                        chargeDirector?.BeginPrepare(chargeCount);
                    };
                    relentless.OnDirectorReset = () => chargeDirector?.ResetCharges();
                    // 의심도 증가
                    relentless.OnIncreaseSuspicion = (rate, dt) =>
                    {
                        if (_isMorayCharging) return; // 돌진 중엔 정지
                        suspicionSystem?.AddSuspicion(rate, dt);
                    };
                    break;

                case SwordfishGimmick swordfish:
                    swordfish.OnSpeedOverride = (s) => { _movement.Speed = s; if (_movement is EnemyMovement em) em.SetMaxSpeed(Mathf.Max(s, 1f)); };
                    swordfish.OnMoveTo = (t) => _movement.MoveTo(t);
                    swordfish.OnMovementStop = () => _movement.Stop();
                    swordfish.OnChasePauseRequest = (p) => _chaseBehavior?.SetPaused(p);
                    break;

                case DashChargeGimmick dash:
                    dash.OnSpeedOverride = (s) =>
                    {
                        _movement.Speed = s;
                        if (_movement is EnemyMovement em)
                            em.SetMaxSpeed(Mathf.Max(s, 1f));
                    };
                    dash.OnMovementStop = () => _movement.Stop();
                    dash.OnMoveTo = (t) => _movement.MoveTo(t);
                    dash.OnIncreaseSuspicion = (rate, dt) =>
                    {
                        if (suspicionSystem != null)
                            suspicionSystem.AddSuspicion(rate, dt);
                    };
                    dash.OnPlayerHit = () =>
                    {
                        if (_playerLives != null && !_playerLives.IsInvincible)
                            _playerLives.TakeDamage();
                    };
                    dash.OnObstacleDamaged = (obj) =>
                    {
                        // HP 2→1: 오브젝트 손상, Player가 안에 있었으면 강제 해제 + 무적
                        bool wasPlayerInside = false;
                        if (_camouflageAdapter != null)
                        {
                            wasPlayerInside = _camouflageAdapter.ForceCancelCamouflage(obj);
                        }
                        if (wasPlayerInside && _playerLives != null)
                        {
                            _playerLives.SetInvincible(0.5f);
                        }
                        Debug.Log("[DashChargeGimmick] 오브젝트 손상 (HP 2→1): " + obj.name
                            + (wasPlayerInside ? " (Player 강제 해제됨)" : ""), obj);
                    };
                    dash.OnObstacleDestroyed = (obj) =>
                    {
                        // HP 1→0: 오브젝트 파괴, Player가 안에 있었으면 데미지
                        bool wasPlayerInside = _camouflageAdapter != null &&
                            _camouflageAdapter.CurrentTarget == obj;
                        if (wasPlayerInside && _playerLives != null && !_playerLives.IsInvincible)
                        {
                            _playerLives.TakeDamage();
                        }
                        Debug.Log("[DashChargeGimmick] 오브젝트 파괴 (HP 1→0): " + obj.name
                            + (wasPlayerInside ? " (Player 데미지)" : ""), obj);
                    };
                    dash.OnLockOnTarget = (targetObj) =>
                    {
                        if (targetIndicatorPrefab != null)
                        {
                            // 타겟 오브젝트 위에 인디케이터 생성
                            Vector3 pos = targetObj.transform.position;
                            pos.y += 2f; // 머리 위
                            GameObject indicator = Instantiate(targetIndicatorPrefab, pos, Quaternion.identity);
                            indicator.transform.SetParent(targetObj.transform); // 타겟 따라다님
                            Destroy(indicator, 0.8f); // indicatorDuration과 동일
                        }
                    };
                    dash.OnObstacleHit = (obj) =>
                    {
                        Debug.Log("[DashChargeGimmick] 오브젝트 파괴 (legacy): " + obj.name, obj);
                    };
                    break;
            }
        }

        #endregion

        #region AI Initialization

        private void InitializeBehaviors()
        {
            _patrolBehavior = new PatrolBehavior(this, _movement, _activeGimmick);
            _chaseBehavior = new ChaseBehavior(this, _movement, _playerTransform, predictionTime: 0.5f, loseDistance: 100f);
            _searchBehavior = new SearchBehavior(this, _movement, _activeGimmick, searchDuration, searchDistance);

            if (_isGroundBoundsScanned)
            {
                _patrolBehavior.SetGroundBounds(_groundBounds);
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

        /// <summary>
        /// Animator가 flipX를 덮어쓰므로 LateUpdate에서 localScale로 재적용
        /// </summary>
        protected virtual void LateUpdate()
        {
            if (_hasFacingDir)
            {
                bool flip = isDefaultFacingLeft ? _lastFacingDir.x > 0f : _lastFacingDir.x < 0f;

                Vector3 scale = transform.localScale;
                scale.x = Mathf.Abs(scale.x) * (flip ? -1f : 1f);
                transform.localScale = scale;

                if (bossSprite != null)
                    bossSprite.flipX = flip;

                // localEulerAngles.y 재적용 (Animator가 Y회전을 덮어쓸 수 있으므로)
                float spriteY = isDefaultFacingLeft
                    ? (flip ? 180f : 0f)
                    : (flip ? 0f : 180f);
                transform.localEulerAngles = new Vector3(0f, spriteY, 0f);

                // 시야각 방향 동기화 — flip 기준으로 강제 설정 (ApplyFacingDirection 누락 방지)
                if (visionSensor != null)
                {
                    float facingX = isDefaultFacingLeft ? (flip ? 1f : -1f) : (flip ? -1f : 1f);
                    visionSensor.SetCustomViewDirection(new Vector3(facingX, 0f, 0f));
                }
            }
        }

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

            // DashChargeGimmick: Player 의태 타겟 정보 전달
            if (_activeGimmick is DashChargeGimmick dashGimmick)
            {
                dashGimmick.SetCamouflageTarget(
                    IsPlayerCamouflaging() ? _camouflageAdapter?.CurrentTarget : null
                );
            }
            if (_playerAware != null && suspicionSystem != null)
            {
                _playerAware.SetSuspicionLevel(Mathf.Clamp01(suspicionSystem.CurrentValue / 100f));
            }
            // Ambush 근접 Chase 쿨타임 감소
            if (_ambushProximityCooldown > 0f)
                _ambushProximityCooldown -= deltaTime;

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

            // Ambush: 시야각은 순수 시각 표시용 — 의심도에 영향 없음
            // DashCharge: 자체 의심도 시스템 사용 (단계별 가속)
            if (_activeGimmick is AmbushGimmick || _activeGimmick is DashChargeGimmick) return;

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
            bool isRelentlessGimmick = _activeGimmick is RelentlessChaseGimmick;
            bool isDashChargeGimmick = _activeGimmick is DashChargeGimmick;
            float ambushDropThreshold = isAmbushGimmick ? ((AmbushGimmick)_activeGimmick).SuspicionDropThreshold : 0f;

            switch (currentState)
            {
                case EnemyAIState.Patrol:
                    if (isRelentlessGimmick)
                    {
                        // Moray: 의심도 시스템(OnDetected)으로만 Chase 진입
                    }
                    else if (isAmbushGimmick)
                    {
                        // Ambush: 시야각 무시, 오직 근접 거리로만 Chase 진입
                        if (_ambushProximityCooldown <= 0f && IsPlayerCloseEnough())
                            _stateMachine.TryTransitionTo(EnemyAIState.Chase);
                    }
                    else if (_activeGimmick is DashChargeGimmick)
                    {
                        // DashCharge: 오직 의심도 100%(OnDetected)로만 Chase 진입
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
                        // 의심도 Safe + Player가 근접 범위 밖으로 나갔을 때만 Patrol 복귀
                        if (!inCombatCycle && suspicionValue < 30f && !IsPlayerCloseEnough())
                            _stateMachine.TryTransitionTo(EnemyAIState.Patrol);
                    }
                    else if (isRelentlessGimmick)
                    {
                        // Moray: 돌진 중이 아닐 때만 Player 이탈 체크
                        // (돌진 중엔 Boss가 멀리 이동하므로 ForceInterrupt 방지)
                        if (_playerTransform != null && chargeDirector != null && !_isMorayCharging)
                        {
                            float dist = Vector3.Distance(transform.position, _playerTransform.position);
                            if (dist > 40f)
                                chargeDirector.ForceInterrupt();
                        }
                    }
                    else if (isDashChargeGimmick)
                    {
                        // DashCharge Chase: 절대 Patrol/Search로 전환되지 않음
                        // (Player 사망 시 OnPlayerDied 핸들러로만 Patrol 복귀)
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
                    if (canSeePlayer && !isAmbushGimmick) // Ambush: 시야각 무시
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
                        bool isRelentless = _activeGimmick is RelentlessChaseGimmick;
                        bool isDashCharge = _activeGimmick is DashChargeGimmick;
                        if (isRelentless || isDashCharge)
                        {
                            // Moray / DashCharge: 자체 의심도 시스템 사용
                            suspicionSystem.SetVisionIncreaseSpeed(0f);
                            suspicionSystem.SetSuspicionDecayMultiplier(1f);
                            suspicionSystem.SetAutoDecayEnabled(false);
                            suspicionSystem.ResetDetected();
                        }
                        else
                        {
                            suspicionSystem.SetVisionIncreaseSpeed(10f);
                            suspicionSystem.SetAutoDecayEnabled(true);
                            float decayMul = _activeGimmick is AmbushGimmick ? 0.2f : 1f;
                            suspicionSystem.SetSuspicionDecayMultiplier(decayMul);
                            if (_activeGimmick is AmbushGimmick)
                                suspicionSystem.ResetDetected();
                        }
                    }
                    // 애니메이션: Patrol
                    if (_animator != null) _animator.SetBool("IsChase", false);
                    PlayerInk.Instance?.SetThreat(false);
                    break;
                case EnemyAIState.Chase:
                    _movement.Speed = chaseSpeed;
                    if (suspicionSystem != null)
                    {
                        bool isRelentless = _activeGimmick is RelentlessChaseGimmick;
                        bool isDashCharge = _activeGimmick is DashChargeGimmick;
                        if (isRelentless || isDashCharge)
                        {
                            // Moray / DashCharge Chase: 방해 금지
                            suspicionSystem.SetVisionIncreaseSpeed(0f);
                            suspicionSystem.SetSuspicionDecayMultiplier(0f);
                            suspicionSystem.SetAutoDecayEnabled(false);
                        }
                        else
                        {
                            suspicionSystem.SetVisionIncreaseSpeed(30f);
                            suspicionSystem.SetAutoDecayEnabled(true);
                            suspicionSystem.SetSuspicionDecayMultiplier(chaseSuspicionDecayMultiplier);
                        }
                    }
                    // 애니메이션: Chase
                    if (_animator != null) _animator.SetBool("IsChase", true);
                    PlayerInk.Instance?.SetThreat(true);
                    break;
                case EnemyAIState.Search:
                    _movement.Speed = searchSpeed;
                    if (suspicionSystem != null)
                    {
                        suspicionSystem.SetVisionIncreaseSpeed(15f);
                        suspicionSystem.SetSuspicionDecayMultiplier(1f);
                    }
                    PlayerInk.Instance?.SetThreat(false);
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
            bool isMorayChase = _activeGimmick is RelentlessChaseGimmick && _stateMachine != null && _stateMachine.IsChase;

            if (isMorayChase)
            {
                // Moray Chase: Ground edge 체크 건너뜀 (돌진 시작 위치가 경계에 있음)
                // _movement.Stop()이 호출되면 charge가 중단됨
                if (_movement != null)
                {
                    _movement.Update(deltaTime);
                    if (_movement.IsMoving)
                    {
                        Vector3 newPos = transform.position;
                        newPos.x += _movement.Velocity.x * deltaTime;
                        newPos.z += _movement.Velocity.z * deltaTime;

                        // ★ Safety Net: GroundBounds 이탈 시 강제 정지
                        if (_isGroundBoundsScanned)
                        {
                            float clampedX = _groundBounds.ClampX(newPos.x);
                            float clampedZ = _groundBounds.ClampZ(newPos.z);
                            if (Mathf.Abs(newPos.x - clampedX) > 0.01f || Mathf.Abs(newPos.z - clampedZ) > 0.01f)
                            {
                                _movement.Stop();
                                return;
                            }
                        }

                        transform.position = newPos;
                    }
                }
            }
            else
            {
                base.UpdateMovement(deltaTime);
            }

            // Patrol/Search일 때만 GroundBounds Clamping 유지
            // Chase 중에는 IsGroundAhead() + IsPositionOnGround() 물리 체크가 이동 제한
            if (_isGroundBoundsScanned && _stateMachine != null && !_stateMachine.IsChase)
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

                if (_movement == null) return;

                bool isMorayChase = _activeGimmick is RelentlessChaseGimmick && _stateMachine != null && _stateMachine.IsChase;

                if (!_movement.IsMoving)
                {
                    // Moray: Prepare/Charge 전환 중이면 저장된 돌진 방향 사용
                    if (isMorayChase && _morayFacingDirection != Vector3.zero)
                    {
                        ApplyFacingDirection(_morayFacingDirection);
                        return;
                    }
                    return;
                }

                if (!isMorayChase)
                {
                    // 일반 Chase: 쿨타임 적용
                    if (_directionChangeTimer > 0f) return;
                }

                float vx = _movement.Velocity.x;
                if (Mathf.Abs(vx) < 0.01f) return;

                facingDir = vx > 0f ? Vector3.right : Vector3.left;

                // 방향 변경 쿨타임 (Moray는 항상 통과)
                MoveDirection newDir = vx > 0f ? MoveDirection.Right : MoveDirection.Left;
                if (newDir != _lastAppliedDirection || isMorayChase)
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
            _lastFacingDir = dir;
            _hasFacingDir = true;

            // ★ Animator가 flipX를 덮어쓰므로, localScale로 flip하여 우회
            bool flip = isDefaultFacingLeft ? dir.x > 0f : dir.x < 0f;

            // localScale.x를 반전시켜 SpriteRenderer 방향 전환 (Animator가 건드리지 않음)
            Vector3 scale = transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (flip ? -1f : 1f);
            transform.localScale = scale;

            // flipX도 함께 설정 (다른 시스템 호환용)
            if (bossSprite != null)
            {
                bossSprite.flipX = flip;

#if UNITY_EDITOR
                Debug.Log($"[BossEnemyController] Facing: dir.x={dir.x:F2}, flipX={flip}, sprite={bossSprite.flipX}");
#endif
            }

            // localEulerAngles.y 동기화 (ConeVisionSensor의 viewDirectionRef.forward 방향 보정)
            float spriteY = isDefaultFacingLeft
                ? (flip ? 180f : 0f)
                : (flip ? 0f : 180f);
            transform.localEulerAngles = new Vector3(0f, spriteY, 0f);

            // Vision cone 방향 동기화 — flip 기준으로 강제 설정 (dir.x가 0이어도 안전)
            if (visionSensor != null)
            {
                float facingX = isDefaultFacingLeft ? (flip ? 1f : -1f) : (flip ? -1f : 1f);
                visionSensor.SetCustomViewDirection(new Vector3(facingX, 0f, 0f));
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

        /// <summary>
        /// Collider isTrigger=true 사용 (물리적 밀림 방지)
        /// OnTriggerEnter로 Player 피격 감지
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (_playerLives == null || _playerLives.IsInvincible) return;
            if (!CanBossDamagePlayer()) return;

            _playerLives.TakeDamage();
        }

        private bool CanBossDamagePlayer()
        {
            if (_stateMachine == null) return false;

            if (_activeGimmick != null)
            {
                // Ambush (가자미): Chase 상태(돌진)에서만 피격
                if (_activeGimmick is AmbushGimmick)
                    return _stateMachine.CurrentState == EnemyAIState.Chase;

                // RelentlessChase (곰치): Director가 OverlapBox로 직접 처리
                if (_activeGimmick is RelentlessChaseGimmick)
                    return false;

                // Swordfish (청새치): Charging 위상에서만 피격
                // DashCharge (백상아리): 자체 OverlapSphere + IsCharging에서만
                // → IGimmickCombatCycle.IsCharging으로 통일 판정
                if (_combatCycle != null)
                    return _combatCycle.IsCharging;
            }

            // 기믹 없으면 Chase 상태에서 일반 피격
            return _stateMachine.CurrentState == EnemyAIState.Chase;
        }

        private void ApplyKnockback()
        {
            if (_playerTransform == null || _playerRigidbody == null) return;
            Vector3 dir = (_playerTransform.position - transform.position).normalized;
            dir.y = 0f;
            _playerRigidbody.AddForce(dir * knockbackForce, ForceMode.Impulse);
        }

        #endregion

        #region Moray Charge Director Handlers

        private void OnMorayPrepareTeleport(Vector3 position)
        {
            // Prepare 시작 시 화면 밖 진입점으로 순간이동
            if (_movement is EnemyMovement em)
                em.TeleportTo(position);

            // 첫 번째 Charge 방향으로 시야각 설정
            if (chargeDirector != null)
                _morayFacingDirection = chargeDirector.GetPrepareDirection();
        }

        private void OnMorayChargeExecute(Vector3 start, Vector3 end)
        {
            // ChaseBehavior 정지 (방해 방지)
            _chaseBehavior?.SetPaused(true);
            // 돌진 방향 저장 (시야각 동기화용)
            Vector3 dir = (end - start);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                _morayFacingDirection = dir.normalized;
            // 순간이동 + 돌진
            if (_movement is EnemyMovement em)
            {
                em.TeleportTo(start);
                em.MoveTo(end);
            }
        }

        private void OnMorayChargesComplete()
        {
            // 모든 돌진 완료 → 의심도 리셋 → Patrol 복귀 (배회)
            if (_activeGimmick is RelentlessChaseGimmick relentless)
            {
                suspicionSystem?.ForceSetSuspicion(relentless.PostChaseSuspicion);
                suspicionSystem?.ResetDetected(); // 재발각 가능
                _isMorayCharging = false;         // 의심도 증가 재개

                // ★ Y 보정 + GroundBounds 재진입 (Charge 후 땅에 박히거나 Ground 밖에 있는 경우 방지)
                RestorePositionAfterCharge();

                if (_stateMachine != null && _stateMachine.CurrentState != EnemyAIState.Patrol)
                    _stateMachine.TryTransitionTo(EnemyAIState.Patrol);
            }
        }

        /// <summary>
        /// Moray Charge 종료 후 위치 보정:
        /// 1. XZ를 GroundBounds 내로 클램핑
        /// 2. 현재 위치에서 Ground Y를 Raycast로 탐색 후 적용
        /// </summary>
        private void RestorePositionAfterCharge()
        {
            if (_movement == null) return;
            _movement.Stop();

            Vector3 pos = transform.position;

            // 1단계: XZ → GroundBounds 내로 클램핑
            if (_isGroundBoundsScanned)
            {
                pos.x = _groundBounds.ClampX(pos.x);
                pos.z = _groundBounds.ClampZ(pos.z);
            }

            // 2단계: Y → Ground 높이로 보정 (Raycast)
            if (groundLayer.value != 0)
            {
                float checkHeight = pos.y + 10f;
                if (Physics.Raycast(new Vector3(pos.x, checkHeight, pos.z), Vector3.down,
                    out RaycastHit hit, 20f, groundLayer))
                {
                    pos.y = hit.point.y + 0.05f;
                }
                else
                {
                    // 아래쪽 실패 시 위쪽도 체크
                    float checkLow = pos.y - 0.1f;
                    if (checkLow > -100f && Physics.Raycast(new Vector3(pos.x, checkLow, pos.z), Vector3.up,
                        out hit, 20f, groundLayer))
                    {
                        pos.y = hit.point.y + 0.05f;
                    }
                }
            }

            transform.position = pos;
        }

        /// <summary>곰치 렌더러/콜라이더 전환 (돌진 중에만 보임)</summary>
        private void SetBossVisibility(bool visible)
        {
            if (_isBossVisible == visible) return;
            _isBossVisible = visible;
            foreach (var r in _bossRenderers)
                if (r != null) r.enabled = visible;
            foreach (var c in _bossColliders)
                if (c != null) c.enabled = visible;
        }

        private void OnMorayPlayerHit()
        {
            if (_playerLives != null && !_playerLives.IsInvincible)
            {
                _playerLives.TakeDamage();
                ApplyKnockback();
            }
        }

        private void OnMorayMovementStop()
        {
            _movement.Stop();
        }

        private void OnMoraySpeedOverride(float speed)
        {
            _movement.Speed = speed;
            if (_movement is EnemyMovement em)
            {
                em.SetMaxSpeed(Mathf.Max(speed, 1f));
                if (speed > 5f)
                    em.SetAcceleration(speed * 2f);
                else
                    em.SetAcceleration(8f);
            }
        }

        #endregion

        #region SandPit (가자미 구덩이)

        /// <summary>
        /// SandPit 클러스터 생성 (AmbushGimmick에서 Player 중심 위치 전달, GroundBounds 내로 클램프)
        /// </summary>
        private void SpawnSandPitCluster(Vector3 center)
        {
            if (sandPitPrefab == null) return;

            bool hasBounds = _groundBounds.MinX != _groundBounds.MaxX || _groundBounds.MinZ != _groundBounds.MaxZ;

            // 바닥 높이 찾기 (보스가 공중에 있을 수 있으므로 Raycast로 지면 고정)
            float groundY = transform.position.y;
            int floorLayer = groundLayer.value > 0
                ? groundLayer.value
                : (1 << LayerMask.NameToLayer("Default"));
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down,
                out RaycastHit floorHit, 5f, floorLayer))
            {
                groundY = floorHit.point.y;
            }

            // Z-fighting 방지: 바닥과 약간 이격 (0.05)
            groundY += 0.05f;

            // 메인 Pit (GroundBounds 클램프 + 지면 높이 + 프리팹 기본 회전 유지)
            Vector3 clampedCenter = center;
            clampedCenter.y = groundY;
            if (hasBounds)
            {
                clampedCenter.x = _groundBounds.ClampX(clampedCenter.x);
                clampedCenter.z = _groundBounds.ClampZ(clampedCenter.z);
            }
            SandPit mainPit = Instantiate(sandPitPrefab, clampedCenter, sandPitPrefab.transform.rotation);
            SubscribeSandPit(mainPit);

            if (!(_activeGimmick is AmbushGimmick ambush)) return;

            // 주변 랜덤 추가 Pit (각각 GroundBounds 클램프 + 지면 높이 + 프리팹 기본 회전)
            int extraCount = Random.Range(ambush.PitClusterCount.x, ambush.PitClusterCount.y + 1);
            for (int i = 0; i < extraCount; i++)
            {
                Vector3 offset = Random.insideUnitSphere * ambush.PitClusterRadius;
                offset.y = 0f;
                Vector3 pitPos = center + offset;
                pitPos.y = groundY;
                if (hasBounds)
                {
                    pitPos.x = _groundBounds.ClampX(pitPos.x);
                    pitPos.z = _groundBounds.ClampZ(pitPos.z);
                }

                SandPit extra = Instantiate(sandPitPrefab, pitPos, sandPitPrefab.transform.rotation);
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

                // Pit 슬로우 효과 (AmbushGimmick 전용: 이동 속도 감소)
                if (_activeGimmick is AmbushGimmick ambush)
                {
                    EnemyEvents.InvokePlayerSlowed(pos, ambush.PitSlowPercent, ambush.PitSlowDuration);
                }
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
                Destroy(_chargeIndicator.gameObject);
            }

            // 곰치 디렉터 이벤트 정리
            if (chargeDirector != null)
            {
                chargeDirector.OnPrepareTeleport -= OnMorayPrepareTeleport;
                chargeDirector.OnChargeExecute -= OnMorayChargeExecute;
                chargeDirector.OnChargesComplete -= OnMorayChargesComplete;
                chargeDirector.OnPlayerHit -= OnMorayPlayerHit;
                chargeDirector.OnSpeedOverride -= OnMoraySpeedOverride;
                chargeDirector.OnMovementStop -= OnMorayMovementStop;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            // AmbushGimmick 근접 Chase 범위 표시 (주황 원)
            // AmbushGimmick 에셋의 ProximityChaseDistance를 직접 읽어서 실시간 반영
            AmbushGimmick ambush = gimmickAsset as AmbushGimmick;
            if (ambush != null)
            {
                float range = ambush.ProximityChaseDistance;
                // 평면 원 (2D) — 3D 구체보다 시각적 왜곡 없음
                Handles.color = new Color(1f, 0.5f, 0f, 0.4f);
                Handles.DrawWireDisc(transform.position, Vector3.up, range);

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(transform.position, 0.15f);
            }
        }
#endif
    }
}
