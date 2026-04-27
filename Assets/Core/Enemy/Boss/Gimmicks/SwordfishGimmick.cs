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

        [Header("시각 효과 길이")]
        [Tooltip("조준 경고선 길이 (m)")]
        [SerializeField] private float aimIndicatorLength = 15f;

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

        #endregion

        #region Visual Callbacks

        /// <summary>
        /// [시각] 조준 시작 — BossEnemyController에서 경고선 생성
        /// </summary>
        public System.Action OnAimStarted;

        /// <summary>
        /// [시각] 조준 중 매 프레임 Player 방향 업데이트 (normalized)
        /// </summary>
        public System.Action<Vector3> OnAimUpdated;

        /// <summary>
        /// [시각] 조준 종료 — 경고선 제거
        /// </summary>
        public System.Action OnAimEnded;

        /// <summary>
        /// [시각] 돌진 시작 (타입 + 방향) — Trail/이펙트 생성
        /// </summary>
        public System.Action<SwordfishChargeType, Vector3> OnChargeStarted;

        /// <summary>
        /// [시각] 돌진 종료 (타입 + 충돌 여부) — Trail 제거 + 충돌 이펙트
        /// </summary>
        public System.Action<SwordfishChargeType, bool> OnChargeEnded;

        /// <summary>
        /// [시각] 스턴 시작 — 스턴 이펙트/애니메이션
        /// </summary>
        public System.Action<float> OnStunStarted;

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
            // 시각적 요소 정리
            OnAimEnded?.Invoke();
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
            // Chase 종료 시 돌진/조준 중단 + 시각 정리
            if (_currentState == State.Aiming)
            {
                OnAimEnded?.Invoke();
            }
            else if (_currentState == State.Charging)
            {
                OnChargeEnded?.Invoke(_currentChargeType, false);
            }

            _currentState = State.Idle;
            OnMovementStop?.Invoke();
            OnSpeedOverride?.Invoke(0f);
            OnChasePauseRequest?.Invoke(false); // ChaseBehavior 재개 (cleanup)
        }

        #endregion

        #region Search

        public void OnSearchEnter()
        {
            OnAimEnded?.Invoke();
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

            // 매 프레임 Player 방향 업데이트 → 경고선 갱신
            if (_playerTransform != null && _bossTransform != null)
            {
                Vector3 dirToPlayer = (_playerTransform.position - _bossTransform.position).normalized;
                dirToPlayer.y = 0f;
                OnAimUpdated?.Invoke(dirToPlayer);
            }

            if (_stateTimer <= 0f)
            {
                // 조준 완료 → 경고선 제거 후 돌진
                OnAimEnded?.Invoke();
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

            if (_stateTimer <= 0f)
            {
                // Player가 근처에 있으면 PostChargePatrol 스킵 (바로 재공격)
                if (_hasPostChargeTarget && !IsPlayerClose())
                {
                    StartPostChargePatrol();
                }
                else
                {
                    _currentState = State.Idle;
                    _hasPostChargeTarget = false;
                    OnSpeedOverride?.Invoke(0f);
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
            if (_playerTransform == null || _bossTransform == null) return;

            if (!IsPlayerStillInRange()) return;

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

            // [시각] 조준 경고선 표시
            OnAimStarted?.Invoke();

#if UNITY_EDITOR
            Debug.Log($"[SwordfishGimmick] 조준 시작! 타입: {_currentChargeType}, 스태미나: {_currentStamina:F0}/{maxStamina}");
#endif
        }

        /// <summary>
        /// 스태미나 상황에 따라 돌진 타입 결정
        /// </summary>
        private SwordfishChargeType SelectChargeType()
        {
            // 광역 돌격 (스태미나 충분할 때만)
            if (enableWideCharge && _currentStamina >= wideChargeCost && _currentStamina >= maxStamina * 0.8f)
            {
                return SwordfishChargeType.Wide;
            }

            // 연속 돌진 (스태미나 충분)
            if (enableDoubleCharge && _currentStamina >= doubleChargeCost)
            {
                return SwordfishChargeType.Double;
            }

            // 기본 돌진 (스태미나 부족해도 가능)
            return SwordfishChargeType.Basic;
        }

        /// <summary>
        /// 돌진 시작 — 예측 위치로 고속 이동 + 시각 효과
        /// </summary>
        private void StartCharge()
        {
            if (_playerTransform == null || _bossTransform == null) return;

            if (!IsPlayerStillInRange())
            {
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
            Vector3 playerVelocity = Vector3.zero;

            if (_playerMovementAdapter != null)
            {
                Vector2 vel2D = _playerMovementAdapter.CurrentVelocity;
                playerVelocity = new Vector3(vel2D.x, 0f, vel2D.y);
            }

            float timeToReach = Vector3.Distance(_bossTransform.position, playerPos) / Mathf.Max(speed, 0.1f);
            Vector3 predictedPos = playerPos + (playerVelocity * timeToReach * predictionFactor);
            _chargeDirection = (predictedPos - _bossTransform.position).normalized;
            _chargeDirection.y = 0f;

            // 속도 오버라이드
            OnSpeedOverride?.Invoke(speed);

            // [시각] 돌진 타입별 Trail/이펙트 시작
            OnChargeStarted?.Invoke(_currentChargeType, _chargeDirection);

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

            // [시각] 2차 돌진 Trail/이펙트
            OnChargeStarted?.Invoke(SwordfishChargeType.Double, _chargeDirection);

#if UNITY_EDITOR
            Debug.Log($"[SwordfishGimmick] 연속 돌진 2차! 방향: {_chargeDirection}, 예측위치: {predictedPos}, 남은스태미나: {_currentStamina:F0}");
#endif
        }

        /// <summary>
        /// 돌진 종료 — 정지 + 쿨타임/스턴 + 시각 정리
        /// </summary>
        private void EndCharge()
        {
            // [시각] 돌진 종료 (Trail 제거 + 충돌 이펙트)
            OnChargeEnded?.Invoke(_currentChargeType, _hasHitWall);

            if (_hasHitWall)
            {
                // 스턴 상태
                _stateTimer = stunDuration;
                OnStunStarted?.Invoke(stunDuration);
                OnSpeedOverride?.Invoke(0f);
                OnMovementStop?.Invoke();
            }
            else
            {
                _stateTimer = GetChargeCooldown();
            }

            _currentState = State.Cooldown;

            // 돌진 위치 기념
            _lastChargeTarget = _bossTransform != null ? _bossTransform.position : Vector3.zero;
            _hasPostChargeTarget = true;

            // 정지
            OnMovementStop?.Invoke();

#if UNITY_EDITOR
            Debug.Log($"[SwordfishGimmick] 돌진 종료 → {(_hasHitWall ? "스턴" : "쿨타임")} ({_stateTimer:F1}초)");
#endif
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

        #region PostChargePatrol

        /// <summary>
        /// 돌진 후 기념된 위치로 이동 (재정비)
        /// </summary>
        private void StartPostChargePatrol()
        {
            if (!_hasPostChargeTarget || _bossTransform == null)
            {
                _currentState = State.Idle;
                return;
            }

            _currentState = State.PostChargePatrol;
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
                    // Clamp된 위치와 원래 위치가 다르면 벽에 도달한 것 → 충돌 처리
                    if (clamped != target)
                    {
                        _hasHitWall = true;
                    }
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
                        return ClampToBounds(target, bounds);
                    }
                    return currentPos;
                }

                case State.Aiming:
                case State.Cooldown:
                    return currentPos;

                default:
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
