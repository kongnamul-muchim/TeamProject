using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// Ch.5 청새치 보스 기믹 (ScriptableObject)
    /// Aim→Charge 2단계 돌진 + 예측 이동 + 스턴 처리
    /// 스태미나 기반 3종 패턴: 기본 돌진 / 연속 돌진 / 광역 돌격
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy Gimmicks/Swordfish Gimmick", fileName = "SwordfishGimmick")]
    public sealed class SwordfishGimmick : ScriptableObject, IEnemyGimmick
    {
        public GimmickType Type => GimmickType.Swordfish;

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

        #endregion

        #region State

        private enum ChargeType { Basic, Double, Wide }
        private enum State { Idle, Aiming, Charging, Cooldown, PostChargePatrol }

        private Transform _bossTransform;
        private State _currentState = State.Idle;
        private ChargeType _currentChargeType = ChargeType.Basic;
        private float _stateTimer;
        private float _currentStamina;

        // 돌진 관련
        private Vector3 _chargeTarget;
        private Vector3 _chargeDirection;
        private Vector3 _chargeStartPos;
        private float _chargeDistanceTraveled;
        private bool _hasHitWall;

        // 연속 돌진
        private bool _isDoubleChargeFirst;
        private bool _isWideCharge;

        // PostCharge
        private Vector3 _lastChargeTarget;
        private bool _hasPostChargeTarget;

        // Player Transform 캐싱
        private Transform _playerTransform;

        #endregion

        #region Callbacks

        /// <summary>
        /// 돌진 속도 제어 (chargeSpeed 적용/해제)
        /// </summary>
        public System.Action<float> OnSpeedOverride;

        /// <summary>
        /// 돌진 시작 시 호출 (돌진 방향 전달)
        /// </summary>
        public System.Action<Vector3> OnDashStarted;

        /// <summary>
        /// 돌진 완료 시 호출
        /// </summary>
        public System.Action OnDashCompleted;

        /// <summary>
        /// 스턴 상태 진입 시 호출 (스턴 지속 시간 전달)
        /// </summary>
        public System.Action<float> OnStun;

        /// <summary>
        /// 이동 목표 설정 (MoveTo 호출)
        /// </summary>
        public System.Action<Vector3> OnMoveTo;

        /// <summary>
        /// 이동 정지
        /// </summary>
        public System.Action OnMovementStop;

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

        #endregion

        #region IEnemyGimmick

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _currentStamina = maxStamina;
            _currentState = State.Idle;
            CachePlayerTransform();
        }

        public void OnDeactivate()
        {
            _currentState = State.Idle;
        }

        #endregion

        #region Patrol

        public void OnPatrolEnter()
        {
            // 순찰 중 idle 상태 — Player 접근 시 OnChaseEnter에서 조준 시작
            if (_currentState == State.PostChargePatrol)
            {
                _currentState = State.Idle;
                _hasPostChargeTarget = false;
            }
        }

        public void OnPatrolUpdate(float deltaTime)
        {
            // 순찰 중 스태미나 회복
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
            // 스태미나 회복 (항상)
            RegenStamina(deltaTime);

            switch (_currentState)
            {
                case State.Idle:
                    // Chase 중 Idle이면 조준 시작
                    if (_currentState == State.Idle)
                    {
                        StartAiming();
                    }
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
            // Chase 종료 시 돌진/조준 중단
            if (_currentState == State.Aiming || _currentState == State.Charging)
            {
                _currentState = State.Idle;
                OnMovementStop?.Invoke();
                OnSpeedOverride?.Invoke(0f);
            }
        }

        #endregion

        #region Search

        public void OnSearchEnter()
        {
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

            if (_stateTimer <= 0f)
            {
                // 조준 완료 → 돌진
                StartCharge();
            }
        }

        private void UpdateCharging(float deltaTime)
        {
            _stateTimer -= deltaTime;

            // 이동 거리 누적 (실제 이동은 BossEnemyController가 처리)
            _chargeDistanceTraveled += chargeSpeed * deltaTime;

            // 연속 돌진 1차 완료 체크
            if (_currentChargeType == ChargeType.Double && _isDoubleChargeFirst && _stateTimer <= 0f)
            {
                // 1차 완료 → 잠시 대기 후 2차
                _isDoubleChargeFirst = false;
                _stateTimer = doubleChargeInterval;
                OnMovementStop?.Invoke();
                return;
            }

            // 2차 돌진 대기 중 → 2차 시작
            if (_currentChargeType == ChargeType.Double && !_isDoubleChargeFirst && _stateTimer > 0f && _chargeDistanceTraveled < 0.5f)
            {
                if (_playerTransform != null)
                {
                    StartDoubleChargeSecond();
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
                // 쿨타임 종료 → PostChargePatrol 또는 Idle
                if (_hasPostChargeTarget)
                {
                    StartPostChargePatrol();
                }
                else
                {
                    _currentState = State.Idle;
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

            // 목표 도달 확인
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
        /// 조준 시작 — Player 방향으로 aimDuration초간 경고 후 돌진
        /// </summary>
        private void StartAiming()
        {
            if (_playerTransform == null || _bossTransform == null) return;

            if (!IsPlayerStillInRange()) return;

            // 돌진 타입 결정 (스태미나 기반)
            ChargeType selectedType = SelectChargeType();

            if (selectedType == ChargeType.Basic && _currentStamina < basicChargeCost)
            {
                // 스태미나 부족 → 조준 안 함
                return;
            }

            _currentChargeType = selectedType;
            _currentState = State.Aiming;
            _stateTimer = aimDuration;

            // 정지 (조준 중에는 움직이지 않음)
            OnMovementStop?.Invoke();
            OnSpeedOverride?.Invoke(0f);

#if UNITY_EDITOR
            Debug.Log($"[SwordfishGimmick] 조준 시작! 타입: {_currentChargeType}, 스태미나: {_currentStamina:F0}/{maxStamina}");
#endif
        }

        /// <summary>
        /// 스태미나 상황에 따라 돌진 타입 결정
        /// </summary>
        private ChargeType SelectChargeType()
        {
            // 광역 돌격 (스태미나 충분할 때만)
            if (enableWideCharge && _currentStamina >= wideChargeCost && _currentStamina >= maxStamina * 0.8f)
            {
                return ChargeType.Wide;
            }

            // 연속 돌진 (스태미나 50% 이상)
            if (enableDoubleCharge && _currentStamina >= doubleChargeCost)
            {
                return ChargeType.Double;
            }

            // 기본 돌진
            return ChargeType.Basic;
        }

        /// <summary>
        /// 돌진 시작 — 예측 위치로 고속 이동
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
            _chargeStartPos = _bossTransform.position;
            _chargeDistanceTraveled = 0f;
            _hasHitWall = false;

            float duration = chargeDuration;
            float speed = chargeSpeed;

            // 광역 돌격 설정
            _isWideCharge = _currentChargeType == ChargeType.Wide;

            // 연속 돌진: 1차는 duration 단축
            if (_currentChargeType == ChargeType.Double)
            {
                _isDoubleChargeFirst = true;
                duration = chargeDuration * 0.6f;
            }
            else
            {
                _isDoubleChargeFirst = false;
            }

            _stateTimer = duration;

            // Player 예측 위치 계산
            Vector3 playerPos = _playerTransform.position;
            float timeToReach = Vector3.Distance(_bossTransform.position, playerPos) / Mathf.Max(speed, 0.1f);
            Vector3 predictedPos = playerPos; // 기본: 현재 위치

            // predictionFactor 기반 예측 (PlayerMovementAdapter는 보스 컨트롤러에서 접근)
            // 예측 위치는 GetPatrolTarget에서 chargeDirection 기반으로 사용
            _chargeTarget = predictedPos;
            _chargeDirection = (predictedPos - _bossTransform.position).normalized;
            _chargeDirection.y = 0f;

            // 속도 오버라이드 + 이동 시작
            OnSpeedOverride?.Invoke(speed);
            OnDashStarted?.Invoke(_chargeDirection);

#if UNITY_EDITOR
            Debug.Log($"[SwordfishGimmick] {_currentChargeType} 돌진! 방향: {_chargeDirection}, 목표: {_chargeTarget}, 남은스태미나: {_currentStamina:F0}");
#endif
        }

        /// <summary>
        /// 연속 돌진 2차 시작 — Player 재예측
        /// </summary>
        private void StartDoubleChargeSecond()
        {
            if (_playerTransform == null || _bossTransform == null) return;

            // 스태미나 추가 차감 (2차 비용)
            float additionalCost = doubleChargeCost * 0.5f;
            _currentStamina = Mathf.Max(0f, _currentStamina - additionalCost);

            Vector3 playerPos = _playerTransform.position;

            // 2차는 짧은 예측 (반응성 높임)
            float predictionTime = 0.3f;
            Vector3 predictedPos = playerPos; // 예측 생략 (단순화)

            _chargeTarget = predictedPos;
            _chargeDirection = (predictedPos - _bossTransform.position).normalized;
            _chargeDirection.y = 0f;
            _stateTimer = chargeDuration * 0.5f;
            _chargeDistanceTraveled = 0f;
            _hasHitWall = false;
            _chargeStartPos = _bossTransform.position;

            OnSpeedOverride?.Invoke(chargeSpeed * 1.1f); // 2차는 약간 빠르게
            OnDashStarted?.Invoke(_chargeDirection);

#if UNITY_EDITOR
            Debug.Log($"[SwordfishGimmick] 연속 돌진 2차! 방향: {_chargeDirection}, 남은스태미나: {_currentStamina:F0}");
#endif
        }

        /// <summary>
        /// 돌진 종료 — 정지 + 쿨타임/스턴
        /// </summary>
        private void EndCharge()
        {
            // 충돌 이펙트는 BossEnemyController에서 처리
            if (_hasHitWall)
            {
                // 스턴 상태
                _stateTimer = stunDuration;
                OnStun?.Invoke(stunDuration);
                OnSpeedOverride?.Invoke(0f);
                OnMovementStop?.Invoke();
            }
            else
            {
                _stateTimer = GetChargeCooldown();
            }

            _currentState = State.Cooldown;

            // 돌진 위치 기억
            _lastChargeTarget = _chargeTarget;
            _hasPostChargeTarget = true;

            // 정지
            OnMovementStop?.Invoke();

#if UNITY_EDITOR
            Debug.Log($"[SwordfishGimmick] 돌진 종료 → {( _hasHitWall ? "스턴" : "쿨타임" )} ({_stateTimer:F1}초)");
#endif
        }

        #endregion

        #region Helpers

        private void RegenStamina(float deltaTime)
        {
            _currentStamina = Mathf.Min(maxStamina, _currentStamina + staminaRegen * deltaTime);
        }

        private float GetChargeCost(ChargeType type)
        {
            return type switch
            {
                ChargeType.Basic => basicChargeCost,
                ChargeType.Double => doubleChargeCost,
                ChargeType.Wide => wideChargeCost,
                _ => basicChargeCost
            };
        }

        private float GetChargeCooldown()
        {
            return _currentChargeType switch
            {
                ChargeType.Wide => wideChargeCooldown,
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
            float checkDistance = chargeSpeed * Time.deltaTime * 2f;

            Vector3 checkOrigin = _bossTransform.position + _chargeDirection * 0.5f;
            Collider[] hits = Physics.OverlapSphere(checkOrigin, checkRadius * 0.5f);

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player")) continue;
                if (hit.transform == _bossTransform) continue;
                if (hit.transform.IsChildOf(_bossTransform)) continue;

                int layer = hit.gameObject.layer;
                if (layer == LayerMask.NameToLayer("Obstacle") || layer == LayerMask.NameToLayer("Ground"))
                {
                    _hasHitWall = true;
#if UNITY_EDITOR
                    Debug.Log($"[SwordfishGimmick] 돌진 충돌! 대상: {hit.name}, 레이어: {LayerMask.LayerToName(layer)}");
#endif
                    return;
                }
            }
        }

        private bool IsPlayerStillInRange()
        {
            if (_playerTransform == null || _bossTransform == null) return false;
            float distance = Vector3.Distance(_bossTransform.position, _playerTransform.position);

            // 기본 감지 범위 (BossEnemyController의 visionSensor 범위와 유사)
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

        #endregion

        #region PostChargePatrol

        /// <summary>
        /// 돌진 후 기억된 위치로 이동 (재정비)
        /// </summary>
        private void StartPostChargePatrol()
        {
            if (!_hasPostChargeTarget || _bossTransform == null)
            {
                _currentState = State.Idle;
                return;
            }

            _currentState = State.PostChargePatrol;
            _stateTimer = 5f;

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
                    // 돌진 방향 유지
                    Vector3 target = currentPos + _chargeDirection * 5f;
                    target.y = currentPos.y;
                    return ClampToBounds(target, bounds);
                }

                case State.PostChargePatrol:
                {
                    // 기억된 위치로 이동
                    if (_hasPostChargeTarget)
                    {
                        Vector3 target = new Vector3(
                            _lastChargeTarget.x,
                            currentPos.y,
                            currentPos.z);
                        return ClampToBounds(target, bounds);
                    }
                    return currentPos;
                }

                case State.Aiming:
                case State.Cooldown:
                    // 조준/쿨다운 중에는 정지
                    return currentPos;

                default:
                    return null; // 기본 PatrolBehavior 로직 사용
            }
        }

        /// <summary>
        /// Search 상태 이동 목표: 돌진 종료 위치 수색
        /// </summary>
        public Vector3? GetSearchTarget(Vector3 currentPos, Vector3 lastKnownPos, GroundBounds bounds)
        {
            // 기본 SearchBehavior 로직 사용
            return null;
        }

        /// <summary>
        /// Ground 경계 내로 위치 클램핑
        /// </summary>
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
