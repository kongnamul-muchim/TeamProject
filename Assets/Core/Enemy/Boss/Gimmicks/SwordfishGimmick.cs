using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// Ch.5 청새치 보스 기믹 (ScriptableObject)
    /// Aim→Charge 2단계 돌진 + 예측 이동 + 스턴 처리
    /// 스태미나 기반 3종 패턴: 기본 돌진 / 연속 돌진 / 광역 돌격
    /// 
    /// 시각적 피드백:
    /// - Aim: 붉은 경고선이 Player 방향으로 표시
    /// - Charge: 돌진 타입별 Trail 효과 (기본=적색, 연속=주황, 광역=자주)
    /// - Collision: 충돌 이펙트 + 스턴
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy Gimmicks/Swordfish Gimmick", fileName = "SwordfishGimmick")]
    public sealed class SwordfishGimmick : ScriptableObject, IEnemyGimmick
    {
        public GimmickType Type => GimmickType.Swordfish;

        /// <summary>
        /// 돌진 타입 (BossEnemyController에서 시각 처리 구분용)
        /// </summary>
        public enum SwordfishChargeType
        {
            Basic,   // 기본 돌진
            Double,  // 연속 돌진 (2회)
            Wide     // 광역 돌격 (넓은 범위)
        }

        #region Inspector Parameters

        [Header("돌진 설정")]
        [Tooltip("돌진 속도")]
        [SerializeField] private float chargeSpeed = 12f;
        [Tooltip("조준(경고) 지속 시간 (초). 이 시간 동안 Player가 회피 가능")]
        [SerializeField] private float aimDuration = 0.5f;
        [Tooltip("돌진 지속 시간 (초)")]
        [SerializeField] private float chargeDuration = 1f;
        [Tooltip("충돌/벽 스턴 시간 (초)")]
        [SerializeField] private float stunDuration = 0.5f;
        [Tooltip("돌진 시 Player 위치 예측 계수 (0=현재위치, 1=완전예측)")]
        [Range(0f, 1f)]
        [SerializeField] private float predictionFactor = 0.7f;

        [Header("연속 돌진")]
        [Tooltip("연속 돌진 활성화")]
        [SerializeField] private bool enableDoubleCharge = true;
        [Tooltip("1차와 2차 돌진 사이 간격 (초)")]
        [SerializeField] private float doubleChargeInterval = 0.3f;

        [Header("스태미나")]
        [Tooltip("최대 스태미나")]
        [SerializeField] private float maxStamina = 100f;
        [Tooltip("초당 스태미나 회복량")]
        [SerializeField] private float staminaRegen = 10f;
        [Tooltip("기본 돌진 스태미나 소모")]
        [SerializeField] private float basicChargeCost = 20f;
        [Tooltip("연속 돌진 스태미나 소모 (총합)")]
        [SerializeField] private float doubleChargeCost = 50f;
        [Tooltip("광역 돌격 스태미나 소모")]
        [SerializeField] private float wideChargeCost = 80f;

        [Header("광역 돌격")]
        [Tooltip("광역 돌격 활성화")]
        [SerializeField] private bool enableWideCharge = true;
        [Tooltip("광역 돌격 판정 반경 배율 (기본 대비)")]
        [SerializeField] private float wideChargeRadiusMultiplier = 1.8f;
        [Tooltip("광역 돌격 후 긴 쿨타임 (초)")]
        [SerializeField] private float wideChargeCooldown = 5f;

        [Header("충돌 판정")]
        [Tooltip("돌진 충돌 판정 너비")]
        [SerializeField] private float chargeWidth = 1.5f;
        [Tooltip("기본 돌진 쿨타임 (초)")]
        [SerializeField] private float chargeCooldown = 2f;

        [Header("시각 효과")]
        [Tooltip("조준 경고선 길이 (m)")]
        [SerializeField] private float aimIndicatorLength = 15f;
        [Tooltip("조준 경고선 프리팹 (비우면 자동 생성)")]
        [SerializeField] private GameObject aimIndicatorPrefab;
        [Tooltip("조준 경고선 색상")]
        [SerializeField] private Color aimIndicatorColor = new Color(1f, 0.2f, 0.2f, 0.7f);
        [Tooltip("돌진 Trail 이펙트 프리팹")]
        [SerializeField] private GameObject trailEffectPrefab;
        [Tooltip("충돌 이펙트 프리팹")]
        [SerializeField] private GameObject impactEffectPrefab;

        #endregion

        #region State

        private enum State { Idle, Aiming, Charging, Cooldown, PostChargePatrol }

        private Transform _bossTransform;
        private State _currentState = State.Idle;
        private SwordfishChargeType _currentChargeType = SwordfishChargeType.Basic;
        private float _stateTimer;
        private float _currentStamina;

        // 돌진 관련
        private Vector3 _chargeDirection;
        private float _chargeDistanceTraveled;
        private bool _hasHitWall;

        // 연속 돌진
        private bool _isDoubleChargeFirst;
        private bool _isDoubleChargeSecondStarted; // 2차 돌진이 이미 시작됐는지 (무한루프 방지)
        private bool _isWideCharge;

        // PostCharge
        private Vector3 _lastChargeTarget;
        private bool _hasPostChargeTarget;

        // Player Transform 캐싱
        private Transform _playerTransform;

        // Player Movement Adapter (예측 이동용)
        private HideAndInk.Player.PlayerMovementAdapter _playerMovementAdapter;

        // 캐싱된 레이어 마스크
        private int _obstacleLayer;
        private int _groundLayer;

        // 충돌 체크용 NonAlloc 버퍼
        private Collider[] _collisionBuffer = new Collider[16];

        // 시각 효과 런타임 인스턴스
        private LineRenderer _aimLineRenderer;
        private GameObject _aimIndicatorInstance;
        private GameObject _trailInstance;

        #endregion

        #region Control Callbacks

        /// <summary>
        /// 돌진 속도 제어
        /// </summary>
        public System.Action<float> OnSpeedOverride;

        /// <summary>
        /// 이동 목표 설정 (MoveTo 호출)
        /// </summary>
        public System.Action<Vector3> OnMoveTo;

        /// <summary>
        /// 이동 정지
        /// </summary>
        public System.Action OnMovementStop;

        /// <summary>
        /// ChaseBehavior 정지/재개 요청 (true=정지, false=재개)
        /// 조준/돌진 중 Player 추적 방지용
        /// </summary>
        public System.Action<bool> OnChasePauseRequest;

        /// <summary>
        /// [Controller] 돌진 시작 알림 — ChaseBehavior 정지 + 애니메이션 트리거
        /// </summary>
        public System.Action<SwordfishChargeType, Vector3> OnChargeStarted;

        /// <summary>
        /// [Controller] 돌진 종료 알림 — ChaseBehavior 재개 + 애니메이션 리셋
        /// </summary>
        public System.Action<SwordfishChargeType, bool> OnChargeEnded;

        /// <summary>
        /// [Controller] 스턴 시작 알림 — ChaseBehavior 정지 + 애니메이션 정지
        /// </summary>
        public System.Action<float> OnStunStarted;

        #endregion

        #region Properties

        /// <summary>
        /// 현재 돌진 또는 조준 중인지 여부 (데미지 판정용)
        /// </summary>
        public bool IsCharging => _currentState == State.Charging || _currentState == State.Aiming;

        /// <summary>
        /// 현재 스턴 상태인지 여부
        /// </summary>
        public bool IsStunned => _currentState == State.Cooldown && _hasHitWall;

        /// <summary>
        /// 현재 선택된 돌진 타입 (BossEnemyController 시각 처리용)
        /// </summary>
        public SwordfishChargeType CurrentChargeType => _currentChargeType;

        /// <summary>
        /// 현재 전투 사이클 중인지 여부 (Idle 제외한 모든 상태)
        /// CheckStateTransitions에서 Chase → Search 전환 방지용
        /// </summary>
        public bool IsInCombatCycle => _currentState != State.Idle;

        /// <summary>
        /// 현재 Player 방향을 바라봐야 하는 상태인지 (조준 중)
        /// BossEnemyController.UpdateViewDirection에서 사용
        /// </summary>
        public bool ShouldFacePlayer => _currentState == State.Aiming;

        #endregion

        #region IEnemyGimmick

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _currentStamina = maxStamina;
            _currentState = State.Idle;
            CachePlayerTransform();
            CachePlayerMovementAdapter();
            CacheLayers();
        }

        public void OnDeactivate()
        {
            // 시각적 요소 정리 (기믹 내부)
            ClearAimIndicator();
            ClearTrailEffect();
            // Controller 알림
            OnChargeEnded?.Invoke(_currentChargeType, false);
            _currentState = State.Idle;
        }

        #endregion

        #region Patrol

        public void OnPatrolEnter()
        {
            if (_currentState == State.PostChargePatrol)
            {
                _currentState = State.Idle;
                _hasPostChargeTarget = false;
            }
        }

        public void OnPatrolUpdate(float deltaTime)
        {
            RegenStamina(deltaTime);
        }

        public void OnPatrolExit() { }

        #endregion

        #region Chase

        public void OnChaseEnter()
        {
            // Chase 진입 시 즉시 조준 시작
#if UNITY_EDITOR
            Debug.Log($"[SwordfishTrace] OnChaseEnter → StartAiming 호출 (위치: {(_bossTransform != null ? _bossTransform.position.ToString() : "null")})");
#endif
            StartAiming();
        }

        public void OnChaseUpdate(float deltaTime)
        {
            RegenStamina(deltaTime);

            switch (_currentState)
            {
                case State.Idle:
                    // Chase 중 Idle이면 조준 시작 (스태미나 회복 후 재공격)
                    StartAiming();
                    break;

                case State.Aiming:
                    UpdateAiming(deltaTime);
                    break;

                case State.Charging:
                    UpdateCharging(deltaTime);
                    break;

                case State.Cooldown:
                    UpdateCooldown(deltaTime);
                    break;

                case State.PostChargePatrol:
                    UpdatePostChargePatrol(deltaTime);
                    break;
            }
        }

        public void OnChaseExit()
        {
#if UNITY_EDITOR
            Debug.Log($"[SwordfishTrace] OnChaseExit (상태: {_currentState}, 위치: {(_bossTransform != null ? _bossTransform.position.ToString() : "null")})");
#endif
            // Chase 종료 시 돌진/조준 중단 + 시각 정리
            if (_currentState == State.Aiming)
            {
                ClearAimIndicator();
            }
            else if (_currentState == State.Charging)
            {
                ClearTrailEffect();
                OnChargeEnded?.Invoke(_currentChargeType, false);
            }

            _currentState = State.Idle;
            ClearAimIndicator();
            ClearTrailEffect();
            OnMovementStop?.Invoke();
            OnSpeedOverride?.Invoke(0f);
            OnChasePauseRequest?.Invoke(false); // ChaseBehavior 재개 (cleanup)
        }

        #endregion

        #region Search

        public void OnSearchEnter()
        {
            ClearAimIndicator();
            ClearTrailEffect();
            _currentState = State.Idle;
            _hasPostChargeTarget = false;
        }

        public void OnSearchUpdate(float deltaTime)
        {
            RegenStamina(deltaTime);
        }

        public void OnSearchExit() { }

        #endregion

        #region Core State Updates

        private void UpdateAiming(float deltaTime)
        {
            _stateTimer -= deltaTime;

            // 경고선 방향 업데이트 (기믹 내부 처리)
            UpdateAimIndicator();

            if (_stateTimer <= 0f)
            {
                // 조준 완료 → 경고선 제거 후 돌진
                ClearAimIndicator();
                StartCharge();
            }
        }

        private void UpdateCharging(float deltaTime)
        {
            _stateTimer -= deltaTime;

            // 이동 거리 누적 (실제 이동은 BossEnemyController가 처리)
            _chargeDistanceTraveled += chargeSpeed * deltaTime;

            // 연속 돌진 1차 완료 체크
            if (_currentChargeType == SwordfishChargeType.Double && _isDoubleChargeFirst && _stateTimer <= 0f)
            {
                // 1차 완료 → 잠시 대기 후 2차
                _isDoubleChargeFirst = false;
                _isDoubleChargeSecondStarted = false;
                _stateTimer = doubleChargeInterval;
                _chargeDistanceTraveled = 0f; // 2차 대비 초기화
                OnMovementStop?.Invoke();
                return;
            }

            // 2차 돌진 대기 중 (interval 동안) → 2차 시작 (한 번만)
            if (_currentChargeType == SwordfishChargeType.Double && !_isDoubleChargeFirst 
                && !_isDoubleChargeSecondStarted && _stateTimer > 0f)
            {
                if (_playerTransform != null)
                {
                    _isDoubleChargeSecondStarted = true;
                    StartDoubleChargeSecond();
                }
                return;
            }

            // 2차 돌진이 이미 시작됐으면 타이머가 끝날 때까지 대기
            if (_currentChargeType == SwordfishChargeType.Double && _isDoubleChargeSecondStarted)
            {
                CheckChargeCollision();
                if (_stateTimer <= 0f || _hasHitWall)
                {
                    EndCharge();
                }
                return;
            }

            // 돌진 방향으로 충돌 체크 (벽/장애물)
            CheckChargeCollision();

            if (_stateTimer <= 0f || _hasHitWall)
            {
                EndCharge();
            }
        }

        private void UpdateCooldown(float deltaTime)
        {
            _stateTimer -= deltaTime;

#if UNITY_EDITOR
            if (_bossTransform != null)
            {
                float distToPlayer = _playerTransform != null ? Vector3.Distance(_bossTransform.position, _playerTransform.position) : -1f;
                Debug.Log($"[SwordfishTrace] UpdateCooldown - timer:{_stateTimer:F2} 위치:{_bossTransform.position} Player거리:{distToPlayer:F1} IsPlayerClose:{IsPlayerClose()}");
            }
#endif

            if (_stateTimer <= 0f)
            {
#if UNITY_EDITOR
                Debug.Log($"[SwordfishTrace] UpdateCooldown 타이머만료! → {(IsPlayerClose() ? "Player근접 → StartAiming" : "Player원거리 → StartPostChargePatrol")}");
#endif
                // Player가 근처에 있으면 PostChargePatrol 스킵 (바로 재공격)
                if (_hasPostChargeTarget && !IsPlayerClose())
                {
                    StartPostChargePatrol();
                }
                else
                {
                    _hasPostChargeTarget = false;
                    // 바로 Aiming 시작 (1프레임 지연으로 ChaseBehavior가 움직이는 현상 방지)
                    StartAiming();
                }
            }
        }

        private void UpdatePostChargePatrol(float deltaTime)
        {
            if (_bossTransform == null || !_hasPostChargeTarget)
            {
                _currentState = State.Idle;
                return;
            }

            Vector3 currentPos = _bossTransform.position;
            float distanceToTarget = Vector3.Distance(
                new Vector3(currentPos.x, 0, currentPos.z),
                new Vector3(_lastChargeTarget.x, 0, _lastChargeTarget.z));

            if (distanceToTarget < 1f)
            {
                _currentState = State.Idle;
                _hasPostChargeTarget = false;
                OnSpeedOverride?.Invoke(0f);
            }
        }

        #endregion

        #region Core Actions

        /// <summary>
        /// 조준 시작 — Player 방향으로 aimDuration초간 경고선 표시 후 돌진
        /// </summary>
        private void StartAiming()
        {
            if (_playerTransform == null || _bossTransform == null)
            {
#if UNITY_EDITOR
                Debug.Log($"[SwordfishTrace] StartAiming 실패 - Transform null (player: {_playerTransform != null}, boss: {_bossTransform != null})");
#endif
                return;
            }

            if (!IsPlayerStillInRange())
            {
#if UNITY_EDITOR
                Debug.Log($"[SwordfishTrace] StartAiming 실패 - Player out of range (거리: {Vector3.Distance(_bossTransform.position, _playerTransform.position):F1}m, 한계: 15m)");
#endif
                return;
            }

            // 돌진 타입 결정 (스태미나 기반)
            SwordfishChargeType selectedType = SelectChargeType();

            // 선택된 타입의 스태미나가 부족하면 Basic으로 fallback
            float requiredCost = GetChargeCost(selectedType);
            if (_currentStamina < requiredCost)
            {
                // Basic도 부족하면 공격 불가
                if (_currentStamina < basicChargeCost)
                {
                    return;
                }
                selectedType = SwordfishChargeType.Basic;
            }

            _currentChargeType = selectedType;
            _currentState = State.Aiming;
            _stateTimer = aimDuration;

            // 정지 (조준 중에는 움직이지 않음)
            OnMovementStop?.Invoke();
            OnSpeedOverride?.Invoke(0f);
            OnChasePauseRequest?.Invoke(true); // ChaseBehavior 정지 (Player 추적 방지)

            // [시각] 조준 경고선 표시 (기믹 내부 처리)
            ShowAimIndicator();

#if UNITY_EDITOR
            Debug.Log($"[SwordfishGimmick] 조준 시작! 타입: {_currentChargeType}, 스태미나: {_currentStamina:F0}/{maxStamina}");
#endif
        }

        /// <summary>
        /// 스태미나 상황에 따라 돌진 타입 결정
        /// </summary>
        private SwordfishChargeType SelectChargeType()
        {
            // 사용 가능한 돌진 타입 수집
            var available = new System.Collections.Generic.List<SwordfishChargeType>();
            available.Add(SwordfishChargeType.Basic); // Basic은 항상 가능

            if (enableDoubleCharge && _currentStamina >= doubleChargeCost)
                available.Add(SwordfishChargeType.Double);

            if (enableWideCharge && _currentStamina >= wideChargeCost && _currentStamina >= maxStamina * 0.8f)
                available.Add(SwordfishChargeType.Wide);

            // 무작위 선택
            return available[Random.Range(0, available.Count)];
        }

        /// <summary>
        /// 돌진 시작 — 예측 위치로 고속 이동 + 시각 효과
        /// </summary>
        private void StartCharge()
        {
            if (_playerTransform == null || _bossTransform == null)
            {
#if UNITY_EDITOR
                Debug.Log($"[SwordfishTrace] StartCharge 실패 - Transform null");
#endif
                return;
            }

            if (!IsPlayerStillInRange())
            {
#if UNITY_EDITOR
                Debug.Log($"[SwordfishTrace] StartCharge 실패 - Player out of range");
#endif
                _currentState = State.Idle;
                return;
            }

            // 스태미나 차감
            float cost = GetChargeCost(_currentChargeType);
            _currentStamina = Mathf.Max(0f, _currentStamina - cost);

            _currentState = State.Charging;
            _chargeDistanceTraveled = 0f;
            _hasHitWall = false;

            float duration = chargeDuration;
            float speed = chargeSpeed;

            // 광역 돌격 설정
            _isWideCharge = _currentChargeType == SwordfishChargeType.Wide;

            // 연속 돌진: 1차는 duration 단축
            if (_currentChargeType == SwordfishChargeType.Double)
            {
                _isDoubleChargeFirst = true;
                _isDoubleChargeSecondStarted = false;
                duration = chargeDuration * 0.6f;
            }
            else
            {
                _isDoubleChargeFirst = false;
                _isDoubleChargeSecondStarted = false;
            }

            _stateTimer = duration;

            // Player 예측 위치 계산 (돌진 방향 결정)
            Vector3 playerPos = _playerTransform.position;
            Vector3 bossPos = _bossTransform.position;
            Vector3 playerVelocity = Vector3.zero;

            if (_playerMovementAdapter != null)
            {
                Vector2 vel2D = _playerMovementAdapter.CurrentVelocity;
                playerVelocity = new Vector3(vel2D.x, 0f, vel2D.y);
            }

            float timeToReach = Vector3.Distance(bossPos, playerPos) / Mathf.Max(speed, 0.1f);
            Vector3 predictedPos = playerPos + (playerVelocity * timeToReach * predictionFactor);
            _chargeDirection = (predictedPos - bossPos).normalized;
            _chargeDirection.y = 0f;

#if UNITY_EDITOR
            Debug.Log($"[SwordfishTrace] StartCharge 방향계산 - 보스위치:{bossPos} Player위치:{playerPos} Player속도:{playerVelocity} 예측위치:{predictedPos} 방향:{_chargeDirection}");
            Debug.Log($"[SwordfishTrace] StartCharge 이동명령 - OnSpeedOverride({speed}F) → maxSpeed={speed}F, OnChargeStarted 호출");
#endif

            // 속도 오버라이드
            OnSpeedOverride?.Invoke(speed);

            // [Controller] 돌진 시작 알림 (ChaseBehavior 정지 + 애니메이션)
            OnChargeStarted?.Invoke(_currentChargeType, _chargeDirection);

            // [시각] 돌진 타입별 Trail 효과 (기믹 내부 처리)
            ShowTrailEffect(_currentChargeType);

#if UNITY_EDITOR
            Debug.Log($"[SwordfishGimmick] {_currentChargeType} 돌진! 방향: {_chargeDirection}, 예측위치: {predictedPos}, 남은스태미나: {_currentStamina:F0}");
#endif
        }

        /// <summary>
        /// 연속 돌진 2차 시작 — Player 재예측
        /// </summary>
        private void StartDoubleChargeSecond()
        {
            if (_playerTransform == null || _bossTransform == null) return;

            float additionalCost = doubleChargeCost * 0.5f;
            _currentStamina = Mathf.Max(0f, _currentStamina - additionalCost);

            // 2차는 짧은 예측 (반응성 높임)
            Vector3 playerPos = _playerTransform.position;
            Vector3 playerVelocity = Vector3.zero;

            if (_playerMovementAdapter != null)
            {
                Vector2 vel2D = _playerMovementAdapter.CurrentVelocity;
                playerVelocity = new Vector3(vel2D.x, 0f, vel2D.y);
            }

            float predictionTime = 0.3f;
            Vector3 predictedPos = playerPos + (playerVelocity * predictionTime);
            _chargeDirection = (predictedPos - _bossTransform.position).normalized;
            _chargeDirection.y = 0f;
            _stateTimer = chargeDuration * 0.5f;
            _chargeDistanceTraveled = 0f;
            _hasHitWall = false;

            OnSpeedOverride?.Invoke(chargeSpeed * 1.1f); // 2차는 약간 빠르게

            // [Controller] 돌진 시작 알림 (ChaseBehavior 정지 + 애니메이션)
            OnChargeStarted?.Invoke(SwordfishChargeType.Double, _chargeDirection);

            // [시각] 2차 돌진 Trail 효과 (기믹 내부 처리)
            ShowTrailEffect(SwordfishChargeType.Double);

#if UNITY_EDITOR
            Debug.Log($"[SwordfishGimmick] 연속 돌진 2차! 방향: {_chargeDirection}, 예측위치: {predictedPos}, 남은스태미나: {_currentStamina:F0}");
#endif
        }

        /// <summary>
        /// 돌진 종료 — 정지 + 쿨타임/스턴 + 시각 정리
        /// </summary>
        private void EndCharge()
        {
            Vector3 endPos = _bossTransform != null ? _bossTransform.position : Vector3.zero;
#if UNITY_EDITOR
            Debug.Log($"[SwordfishTrace] EndCharge - 위치:{endPos} wallHit:{_hasHitWall} 이동거리:{_chargeDistanceTraveled:F1}m");
#endif

            // [시각] 돌진 Trail 제거 (기믹 내부 처리)
            ClearTrailEffect();

            // [시각] 충돌 이펙트 (기믹 내부 처리)
            if (_hasHitWall) ShowImpactEffect(_currentChargeType);

            // [Controller] 돌진 종료 알림 (ChaseBehavior 재개 + 애니메이션 리셋)
            OnChargeEnded?.Invoke(_currentChargeType, _hasHitWall);

            if (_hasHitWall)
            {
                // 스턴 상태
                _stateTimer = stunDuration;
                OnStunStarted?.Invoke(stunDuration);
                OnSpeedOverride?.Invoke(0f);
                OnMovementStop?.Invoke();
#if UNITY_EDITOR
                Debug.Log($"[SwordfishTrace] EndCharge → 스턴 (speed=0, stop)");
#endif
            }
            else
            {
                _stateTimer = GetChargeCooldown();
#if UNITY_EDITOR
                Debug.Log($"[SwordfishTrace] EndCharge → 쿨타임 ({_stateTimer:F1}초) speed 유지: chargeSpeed");
#endif
            }

            _currentState = State.Cooldown;

            // 돌진 위치 기념
            _lastChargeTarget = endPos;
            _hasPostChargeTarget = true;

            // 정지
            OnMovementStop?.Invoke();
        }

        #endregion

        #region Helpers

        private void RegenStamina(float deltaTime)
        {
            _currentStamina = Mathf.Min(maxStamina, _currentStamina + staminaRegen * deltaTime);
        }

        private float GetChargeCost(SwordfishChargeType type)
        {
            return type switch
            {
                SwordfishChargeType.Basic => basicChargeCost,
                SwordfishChargeType.Double => doubleChargeCost,
                SwordfishChargeType.Wide => wideChargeCost,
                _ => basicChargeCost
            };
        }

        private float GetChargeCooldown()
        {
            return _currentChargeType switch
            {
                SwordfishChargeType.Wide => wideChargeCooldown,
                _ => chargeCooldown
            };
        }

        /// <summary>
        /// 돌진 중 충돌 체크 (벽/장애물)
        /// </summary>
        private void CheckChargeCollision()
        {
            if (_bossTransform == null) return;

            float checkRadius = _isWideCharge ? chargeWidth * wideChargeRadiusMultiplier : chargeWidth;
            Vector3 checkOrigin = _bossTransform.position + _chargeDirection * 0.5f;

            int hitCount = Physics.OverlapSphereNonAlloc(checkOrigin, checkRadius * 0.5f, _collisionBuffer);

            for (int i = 0; i < hitCount; i++)
            {
                var hit = _collisionBuffer[i];
                if (hit.CompareTag("Player")) continue;
                if (hit.transform == _bossTransform) continue;
                if (hit.transform.IsChildOf(_bossTransform)) continue;

                int layer = hit.gameObject.layer;
                if (layer == _obstacleLayer || layer == _groundLayer)
                {
                    _hasHitWall = true;
#if UNITY_EDITOR
                    Debug.Log($"[SwordfishGimmick] 돌진 충돌! 대상: {hit.name}, 레이어: {LayerMask.LayerToName(layer)}");
#endif
                    return;
                }
            }
        }

        /// <summary>
        /// Player가 가까이 있는지 확인 (PostChargePatrol 스킵용)
        /// </summary>
        private bool IsPlayerClose()
        {
            if (_playerTransform == null || _bossTransform == null) return false;
            float distance = Vector3.Distance(_bossTransform.position, _playerTransform.position);
            return distance <= 8f;
        }

        private bool IsPlayerStillInRange()
        {
            if (_playerTransform == null || _bossTransform == null) return false;
            float distance = Vector3.Distance(_bossTransform.position, _playerTransform.position);
            return distance <= 15f;
        }

        private void CachePlayerTransform()
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                _playerTransform = playerObj.transform;
            }
        }

        private void CachePlayerMovementAdapter()
        {
            if (_playerTransform != null)
            {
                _playerMovementAdapter = _playerTransform.GetComponent<HideAndInk.Player.PlayerMovementAdapter>();
            }

            if (_playerMovementAdapter == null)
            {
                _playerMovementAdapter = GameObject.FindObjectOfType<HideAndInk.Player.PlayerMovementAdapter>();
            }
        }

        private void CacheLayers()
        {
            _obstacleLayer = LayerMask.NameToLayer("Obstacle");
            _groundLayer = LayerMask.NameToLayer("Ground");
        }

        /// <summary>
        /// Player Transform 갱신 (BossEnemyController에서 매 프레임 또는 Player 재생성 시 호출)
        /// </summary>
        public void RefreshPlayerTransform(Transform playerTransform)
        {
            _playerTransform = playerTransform;
            CachePlayerMovementAdapter();
        }

        #endregion

        #region Visual Effects (Gimmick Self-Managed)

        /// <summary>
        /// 조준 경고선 표시 (빨간 직선)
        /// </summary>
        private void ShowAimIndicator()
        {
            if (_bossTransform == null) return;

            if (aimIndicatorPrefab != null)
            {
                _aimIndicatorInstance = Instantiate(aimIndicatorPrefab, _bossTransform.position, Quaternion.identity, _bossTransform);
                _aimLineRenderer = _aimIndicatorInstance.GetComponent<LineRenderer>();
            }
            else if (_aimLineRenderer == null)
            {
                GameObject go = new GameObject("Swordfish_AimIndicator");
                go.transform.SetParent(_bossTransform);
                go.transform.localPosition = Vector3.zero;
                _aimLineRenderer = go.AddComponent<LineRenderer>();
                _aimLineRenderer.startWidth = 0.15f;
                _aimLineRenderer.endWidth = 0.03f;
                _aimLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                _aimLineRenderer.startColor = aimIndicatorColor;
                _aimLineRenderer.endColor = new Color(aimIndicatorColor.r, aimIndicatorColor.g, aimIndicatorColor.b, 0f);
                _aimLineRenderer.enabled = true;
                _aimIndicatorInstance = go;
            }

            if (_aimLineRenderer != null)
                _aimLineRenderer.enabled = true;
        }

        /// <summary>
        /// 조준 경고선 업데이트 (매 프레임 Player 방향 갱신)
        /// </summary>
        private void UpdateAimIndicator()
        {
            if (_aimLineRenderer == null || _playerTransform == null || _bossTransform == null) return;

            Vector3 start = _bossTransform.position + Vector3.up * 0.05f;
            Vector3 direction = (_playerTransform.position - _bossTransform.position).normalized;
            direction.y = 0f;
            Vector3 end = start + direction * aimIndicatorLength;
            _aimLineRenderer.SetPosition(0, start);
            _aimLineRenderer.SetPosition(1, end);
        }

        /// <summary>
        /// 조준 경고선 제거
        /// </summary>
        private void ClearAimIndicator()
        {
            if (_aimLineRenderer != null)
            {
                _aimLineRenderer.enabled = false;
            }
        }

        /// <summary>
        /// 돌진 타입별 Trail 효과 표시
        /// </summary>
        private void ShowTrailEffect(SwordfishChargeType chargeType)
        {
            ClearTrailEffect();
            if (_bossTransform == null) return;

            if (trailEffectPrefab != null)
            {
                _trailInstance = Instantiate(trailEffectPrefab, _bossTransform.position, Quaternion.identity, _bossTransform);
            }
            else
            {
                GameObject go = new GameObject("Swordfish_Trail");
                go.transform.SetParent(_bossTransform);
                go.transform.localPosition = Vector3.zero;

                TrailRenderer trail = go.AddComponent<TrailRenderer>();
                trail.time = 0.3f;
                trail.startWidth = chargeType == SwordfishChargeType.Wide ? 1.2f : 0.5f;
                trail.endWidth = 0f;

                Color trailColor = chargeType switch
                {
                    SwordfishChargeType.Basic => new Color(1f, 0.2f, 0.2f, 0.6f),
                    SwordfishChargeType.Double => new Color(1f, 0.6f, 0f, 0.6f),
                    SwordfishChargeType.Wide => new Color(0.8f, 0.2f, 1f, 0.6f),
                    _ => new Color(1f, 0.2f, 0.2f, 0.6f)
                };

                trail.material = new Material(Shader.Find("Sprites/Default"));
                trail.startColor = trailColor;
                trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
                _trailInstance = go;
            }
        }

        /// <summary>
        /// Trail 효과 제거
        /// </summary>
        private void ClearTrailEffect()
        {
            if (_trailInstance != null)
            {
                Destroy(_trailInstance);
                _trailInstance = null;
            }
        }

        /// <summary>
        /// 돌진 타입별 충돌 이펙트 표시
        /// </summary>
        private void ShowImpactEffect(SwordfishChargeType chargeType)
        {
            if (_bossTransform == null) return;

            if (impactEffectPrefab != null)
            {
                GameObject impact = Instantiate(impactEffectPrefab, _bossTransform.position, Quaternion.identity);
                Destroy(impact, 2f);
            }
            else
            {
                GameObject go = new GameObject("Swordfish_Impact");
                go.transform.position = _bossTransform.position;

                ParticleSystem ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.startLifetime = 0.5f;
                main.startSpeed = 5f;
                main.startSize = chargeType == SwordfishChargeType.Wide ? 1.5f : 0.8f;
                main.startColor = chargeType switch
                {
                    SwordfishChargeType.Basic => new Color(1f, 0.2f, 0.2f),
                    SwordfishChargeType.Double => new Color(1f, 0.6f, 0f),
                    SwordfishChargeType.Wide => new Color(0.8f, 0.2f, 1f),
                    _ => new Color(1f, 0.2f, 0.2f)
                };
                main.maxParticles = 20;

                var emission = ps.emission;
                emission.SetBurst(0, new ParticleSystem.Burst(0f, 15));

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.5f;

                Destroy(go, 1.5f);
            }
        }

        #endregion

        #region PostChargePatrol

        /// <summary>
        /// 돌진 후 기념된 위치로 이동 (재정비)
        /// </summary>
        private void StartPostChargePatrol()
        {
            if (!_hasPostChargeTarget || _bossTransform == null)
            {
#if UNITY_EDITOR
                Debug.Log($"[SwordfishTrace] StartPostChargePatrol 실패 - target:{_hasPostChargeTarget} boss:{_bossTransform != null}");
#endif
                _currentState = State.Idle;
                return;
            }

            _currentState = State.PostChargePatrol;
#if UNITY_EDITOR
            Debug.Log($"[SwordfishTrace] StartPostChargePatrol - 현재위치:{_bossTransform.position} 목표위치:{_lastChargeTarget} 거리:{Vector3.Distance(_bossTransform.position, _lastChargeTarget):F1}m");
#endif
            OnMoveTo?.Invoke(_lastChargeTarget);
        }

        #endregion

        #region IEnemyGimmick Movement Override

        /// <summary>
        /// SwordfishGimmick은 돌진/조준 중 이동 제어권을 가짐
        /// </summary>
        public bool HasMovementOverride => _currentState == State.Aiming ||
                                           _currentState == State.Charging ||
                                           _currentState == State.PostChargePatrol;

        /// <summary>
        /// Patrol 상태 이동 목표: 돌진 방향 유지 또는 PostChargePatrol 위치로 이동
        /// </summary>
        public Vector3? GetPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            switch (_currentState)
            {
                case State.Charging:
                {
                    Vector3 target = currentPos + _chargeDirection * 5f;
                    target.y = currentPos.y;
                    Vector3 clamped = ClampToBounds(target, bounds);
                    if (clamped != target)
                    {
                        _hasHitWall = true;
                    }
#if UNITY_EDITOR
                    Debug.Log($"[SwordfishTrace] GetPatrolTarget Charging - 현재:{currentPos} 방향:{_chargeDirection} 목표:{clamped}");
#endif
                    return clamped;
                }

                case State.PostChargePatrol:
                {
                    if (_hasPostChargeTarget)
                    {
                        Vector3 target = new Vector3(
                            _lastChargeTarget.x,
                            currentPos.y,
                            _lastChargeTarget.z);
                        Vector3 clamped = ClampToBounds(target, bounds);
#if UNITY_EDITOR
                        Debug.Log($"[SwordfishTrace] GetPatrolTarget PostCharge - 현재:{currentPos} 목표:{clamped} lastChargeTarget:{_lastChargeTarget}");
#endif
                        return clamped;
                    }
#if UNITY_EDITOR
                    Debug.Log($"[SwordfishTrace] GetPatrolTarget PostCharge - no target → 현재위치 유지");
#endif
                    return currentPos;
                }

                case State.Aiming:
                case State.Cooldown:
#if UNITY_EDITOR
                    Debug.Log($"[SwordfishTrace] GetPatrolTarget {_currentState} - 현재위치 유지:{currentPos}");
#endif
                    return currentPos;

                default:
#if UNITY_EDITOR
                    Debug.Log($"[SwordfishTrace] GetPatrolTarget → null (상태:{_currentState})");
#endif
                    return null;
            }
        }

        /// <summary>
        /// Search 상태 이동 목표: 기본 SearchBehavior 로직 사용
        /// </summary>
        public Vector3? GetSearchTarget(Vector3 currentPos, Vector3 lastKnownPos, GroundBounds bounds)
        {
            return null;
        }

        private Vector3 ClampToBounds(Vector3 pos, GroundBounds bounds)
        {
            if (bounds.MinX != bounds.MaxX || bounds.MinZ != bounds.MaxZ)
            {
                pos.x = bounds.ClampX(pos.x);
                pos.z = bounds.ClampZ(pos.z);
            }
            return pos;
        }

        #endregion
    }
}
