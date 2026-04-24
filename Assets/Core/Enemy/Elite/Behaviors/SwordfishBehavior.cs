using UnityEngine;
using HideAndInk.Core.Enemy.Elite;

namespace HideAndInk.Core.Enemy.Elite.Behaviors
{
    /// <summary>
    /// 청새치 정예몬스터 행동 패턴 (개선 버전)
    /// - 2단계 돌진: 조준(Aim) → 돌진(Charge) — Player에게 회피 기회 제공
    /// - 스태미나 기반 3종 패턴: 기본 돌진 / 연속 돌진 / 광역 돌격
    /// - 시각적 피드백: 조준 경고선, 돌진 Trail, 충돌 이펙트
    /// </summary>
    public class SwordfishBehavior : MonoBehaviour, IEliteBehavior
    {
        public string BehaviorName => "Swordfish";

        /// <summary>
        /// 이동 제어권 보유 여부 (Aiming/Charging/PostChargePatrol 중)
        /// </summary>
        public bool IsControllingMovement =>
            _currentState == State.Aiming ||
            _currentState == State.Charging ||
            _currentState == State.PostChargePatrol;

        /// <summary>
        /// 현재 돌진 중인지 여부 (컨트롤러에서 flipX 고정용)
        /// </summary>
        public bool IsCharging => _currentState == State.Charging || _currentState == State.Aiming;

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

        [Header("시각 효과")]
        [Tooltip("조준 경고선 프리팹 (Sprite 또는 LineRenderer)")]
        [SerializeField] private GameObject aimIndicatorPrefab;
        [Tooltip("돌진 Trail 이펙트 프리팹")]
        [SerializeField] private GameObject trailEffectPrefab;
        [Tooltip("충돌 이펙트 프리팹")]
        [SerializeField] private GameObject impactEffectPrefab;
        [Tooltip("조준 경고선 색상")]
        [SerializeField] private Color aimIndicatorColor = new Color(1f, 0.2f, 0.2f, 0.7f);
        [Tooltip("조준 경고선 길이 (m)")]
        [SerializeField] private float aimIndicatorLength = 15f;

        [Header("애니메이션")]
        [Tooltip("애니메이터 (비워두면 자동 탐색)")]
        [SerializeField] private Animator enemyAnimator;
        [Tooltip("돌진 애니메이션 트리거 이름")]
        [SerializeField] private string chargeTriggerName = "OnCharge";
        [Tooltip("조준 애니메이션 트리거 이름")]
        [SerializeField] private string aimTriggerName = "OnAim";
        [Tooltip("돌진 애니메이션 길이 (초). 속도 계산에 사용됨")]
        [SerializeField] private float chargeAnimationLength = 1f;

        #endregion

        #region State

        private enum ChargeType { Basic, Double, Wide }
        private enum State { Idle, Aiming, Charging, Cooldown, PostChargePatrol }

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
        private bool _isDoubleChargeFirst; // true = 1차, false = 2차
        private bool _isWideCharge;

        // PostCharge
        private Vector3 _lastChargeTarget;
        private bool _hasPostChargeTarget;

        // 외부 참조
        private EliteEnemyController _controller;
        private Transform _playerTransform;
        private HideAndInk.Player.PlayerMovementAdapter _playerMovementAdapter;

        // 시각 효과 인스턴스
        private GameObject _aimIndicatorInstance;
        private LineRenderer _aimLineRenderer;
        private GameObject _trailInstance;

        #endregion

        #region IEliteBehavior

        public void OnActivate()
        {
            _controller = GetComponent<EliteEnemyController>();
            _currentStamina = maxStamina;
            CachePlayerTransform();
            CachePlayerMovementAdapter();
            CacheAnimator();
            _currentState = State.Idle;
        }

        public void OnDeactivate()
        {
            ClearAimIndicator();
            ClearTrailEffect();
            _currentState = State.Idle;
        }

        public void OnPlayerApproached(float distance, Vector3 playerPos, Vector3 directionToPlayer)
        {
            if (_playerTransform == null) return;

            switch (_currentState)
            {
                case State.Idle:
                    // Player가 범위 내에 있고 순찰 중이면 즉시 조준 시작
                    if (distance <= GetDetectionRadius())
                    {
                        StartAiming();
                    }
                    break;

                case State.Aiming:
                    // 이미 조준 중이면 아무것도 안 함
                    break;

                case State.Charging:
                    // 돌진 중이면 계속 돌진
                    break;

                case State.Cooldown:
                case State.PostChargePatrol:
                    // 쿨타임/회복 중에는 무시
                    break;
            }
        }

        public void OnUpdate(float deltaTime)
        {
            // 스태미나 회복 (항상)
            RegenStamina(deltaTime);

            switch (_currentState)
            {
                case State.Idle:
                    UpdateIdle(deltaTime);
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

        #endregion

        #region State Updates

        private void UpdateIdle(float deltaTime)
        {
            // Idle: 아무것도 안 함. OnPlayerApproached가 조준을 시작시킴
        }

        private void UpdateAiming(float deltaTime)
        {
            _stateTimer -= deltaTime;

            // 조준 경고선 업데이트 (Player 방향 지속 갱신)
            UpdateAimIndicator();

            if (_stateTimer <= 0f)
            {
                // 조준 완료 → 돌진
                ClearAimIndicator();
                StartCharge();
            }
        }

        private void UpdateCharging(float deltaTime)
        {
            _stateTimer -= deltaTime;

            // 이동 거리 누적
            _chargeDistanceTraveled += chargeSpeed * deltaTime;

            // 연속 돌진 1차 완료 체크
            if (_currentChargeType == ChargeType.Double && _isDoubleChargeFirst && _stateTimer <= 0f)
            {
                // 1차 완료 → 잠시 대기 후 2차
                _isDoubleChargeFirst = false;
                _stateTimer = doubleChargeInterval;
                // 1차 종료 처리 (속도/정지)
                _controller?.Stop();
                return;
            }

            // 2차 돌진 대기 중
            if (_currentChargeType == ChargeType.Double && !_isDoubleChargeFirst && _stateTimer > 0f && _chargeDistanceTraveled < 0.5f)
            {
                // 대기 중 → 2차 돌진 시작 (Player 재예측)
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
                // 쿨타임 종료 → 돌진 위치로 이동
                if (_hasPostChargeTarget)
                {
                    StartPostChargePatrol();
                }
                else
                {
                    _currentState = State.Idle;
                    _controller?.SetSpeed(_controller.GetDefaultSpeed());
                }
            }
        }

        private void UpdatePostChargePatrol(float deltaTime)
        {
            if (_controller == null || !_hasPostChargeTarget)
            {
                _currentState = State.Idle;
                return;
            }

            // 목표 도달 확인
            Vector3 currentPos = transform.position;
            float distanceToTarget = Vector3.Distance(
                new Vector3(currentPos.x, 0, currentPos.z),
                new Vector3(_lastChargeTarget.x, 0, _lastChargeTarget.z));

            if (distanceToTarget < 1f)
            {
                _currentState = State.Idle;
                _hasPostChargeTarget = false;
                _controller.SetSpeed(_controller.GetDefaultSpeed());
            }
        }

        #endregion

        #region Core Actions

        /// <summary>
        /// 조준 시작 — Player 방향으로 0.5초간 경고 + 돌진 타입 결정
        /// </summary>
        private void StartAiming()
        {
            bool isPlayerInRange = IsPlayerStillInRange();
            if (!isPlayerInRange) return;

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

            // 순찰 중단
            _controller?.Stop();

            // 조준 경고선 표시
            ShowAimIndicator();

            // 조준 애니메이션
            PlayAimAnimation();

#if UNITY_EDITOR
            Debug.Log($"[SwordfishBehavior] 조준 시작! 타입: {_currentChargeType}, 스태미나: {_currentStamina:F0}/{maxStamina}");
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
        /// 돌진 시작 — 조준 방향으로 고속 이동
        /// </summary>
        private void StartCharge()
        {
            if (_playerTransform == null) return;

            if (!IsPlayerStillInRange())
            {
                _currentState = State.Idle;
                return;
            }

            // 스태미나 차감
            float cost = GetChargeCost(_currentChargeType);
            _currentStamina = Mathf.Max(0f, _currentStamina - cost);

            _currentState = State.Charging;
            _chargeStartPos = transform.position;
            _chargeDistanceTraveled = 0f;
            _hasHitWall = false;

            float duration = chargeDuration;
            float speed = chargeSpeed;

            // 광역 돌격 설정
            _isWideCharge = _currentChargeType == ChargeType.Wide;

            // 연속 돌진: 1차는 duration/2
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

            _controller?.Stop();

            // Player 예측 위치 계산
            Vector3 playerPos = _playerTransform.position;
            Vector3 playerVelocity = Vector3.zero;

            if (_playerMovementAdapter != null)
            {
                Vector2 vel2D = _playerMovementAdapter.CurrentVelocity;
                playerVelocity = new Vector3(vel2D.x, 0f, vel2D.y);
            }

            float timeToReach = Vector3.Distance(transform.position, playerPos) / Mathf.Max(speed, 0.1f);
            Vector3 predictedPos = playerPos + (playerVelocity * timeToReach * predictionFactor);

            _chargeTarget = predictedPos;
            _chargeDirection = (predictedPos - transform.position).normalized;

            // 이동 시작
            if (_controller != null)
            {
                _controller.SetSpeed(speed);
                _controller.MoveTo(_chargeTarget);
            }

            // Trail 효과
            ShowTrailEffect();

            // 애니메이션
            PlayChargeAnimation();

#if UNITY_EDITOR
            Debug.Log($"[SwordfishBehavior] {_currentChargeType} 돌진! 방향: {_chargeDirection}, 목표: {_chargeTarget}, 남은스태미나: {_currentStamina:F0}");
#endif
        }

        /// <summary>
        /// 연속 돌진 2차 시작 — 1차 방향과 반대/다른 방향으로 재돌진
        /// </summary>
        private void StartDoubleChargeSecond()
        {
            if (_playerTransform == null) return;

            // 스태미나 추가 차감 (1차에서 이미 일부 차감됨)
            // 2차는 나머지 비용 차감 (doubleChargeCost / 2)
            float additionalCost = doubleChargeCost * 0.5f;
            _currentStamina = Mathf.Max(0f, _currentStamina - additionalCost);

            Vector3 playerPos = _playerTransform.position;
            Vector3 playerVelocity = Vector3.zero;

            if (_playerMovementAdapter != null)
            {
                Vector2 vel2D = _playerMovementAdapter.CurrentVelocity;
                playerVelocity = new Vector3(vel2D.x, 0f, vel2D.y);
            }

            // 2차는 짧은 예측 (반응성 높임)
            float predictionTime = 0.3f;
            Vector3 predictedPos = playerPos + (playerVelocity * predictionTime);

            _chargeTarget = predictedPos;
            _chargeDirection = (predictedPos - transform.position).normalized;
            _stateTimer = chargeDuration * 0.5f;
            _chargeDistanceTraveled = 0f;
            _hasHitWall = false;
            _chargeStartPos = transform.position;

            if (_controller != null)
            {
                _controller.SetSpeed(chargeSpeed * 1.1f); // 2차는 약간 빠르게
                _controller.MoveTo(_chargeTarget);
            }

#if UNITY_EDITOR
            Debug.Log($"[SwordfishBehavior] 연속 돌진 2차! 방향: {_chargeDirection}, 남은스태미나: {_currentStamina:F0}");
#endif
        }

        /// <summary>
        /// 돌진 종료 — 정지 + 쿨타임
        /// </summary>
        private void EndCharge()
        {
            ClearTrailEffect();

            // 충돌 이펙트 (벽에 부딪혔으면 표시)
            if (_hasHitWall)
            {
                ShowImpactEffect();
                _stateTimer = stunDuration; // 스턴
            }
            else
            {
                _stateTimer = GetChargeCooldown();
            }

            _currentState = State.Cooldown;

            // 돌진 위치 기억
            _lastChargeTarget = _chargeTarget;
            _hasPostChargeTarget = true;

            // 정지 및 속도 복원
            if (_controller != null)
            {
                _controller.Stop();
                _controller.SetSpeed(_controller.GetDefaultSpeed());
            }

            ResetAnimation();

#if UNITY_EDITOR
            Debug.Log($"[SwordfishBehavior] 돌진 종료 → 쿨타임 ({_stateTimer:F1}초)");
#endif
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 스태미나 회복
        /// </summary>
        private void RegenStamina(float deltaTime)
        {
            _currentStamina = Mathf.Min(maxStamina, _currentStamina + staminaRegen * deltaTime);
        }

        /// <summary>
        /// 돌진 타입별 스태미나 소모량
        /// </summary>
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

        /// <summary>
        /// 돌진 타입별 쿨타임
        /// </summary>
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
        /// 광역 돌격은 넓은 판정 사용
        /// </summary>
        private void CheckChargeCollision()
        {
            float checkRadius = _isWideCharge ? chargeWidth * wideChargeRadiusMultiplier : chargeWidth;
            float checkDistance = chargeSpeed * Time.deltaTime * 2f;

            Vector3 checkOrigin = transform.position + _chargeDirection * 0.5f;
            Collider[] hits = Physics.OverlapSphere(checkOrigin, checkRadius * 0.5f);

            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player")) continue;
                if (hit.transform == transform) continue;
                if (hit.transform.IsChildOf(transform)) continue;

                // 충돌 감지 (Obstacle 레이어 또는 Ground 레이어)
                int layer = hit.gameObject.layer;
                if (layer == LayerMask.NameToLayer("Obstacle") || layer == LayerMask.NameToLayer("Ground"))
                {
                    _hasHitWall = true;
#if UNITY_EDITOR
                    Debug.Log($"[SwordfishBehavior] 돌진 충돌! 대상: {hit.name}, 레이어: {LayerMask.LayerToName(layer)}");
#endif
                    return;
                }
            }
        }

        /// <summary>
        /// Player가 아직 감지 범위 내에 있는지 확인
        /// </summary>
        private bool IsPlayerStillInRange()
        {
            if (_playerTransform == null) return false;
            float distance = Vector3.Distance(transform.position, _playerTransform.position);
            return distance <= GetDetectionRadius() * 1.2f; // 경계 약간 여유
        }

        /// <summary>
        /// 감지 반경 (컨트롤러에서 설정)
        /// </summary>
        private float GetDetectionRadius()
        {
            return _controller?.GetDetectionRadius() ?? 8f;
        }

        #endregion

        #region Visual Effects

        /// <summary>
        /// 조준 경고선 표시 (Player 방향 빨간선)
        /// </summary>
        private void ShowAimIndicator()
        {
            if (aimIndicatorPrefab != null)
            {
                _aimIndicatorInstance = Instantiate(aimIndicatorPrefab, transform.position, Quaternion.identity, transform);
                _aimLineRenderer = _aimIndicatorInstance.GetComponent<LineRenderer>();
            }
            else if (_aimLineRenderer == null)
            {
                // LineRenderer 자동 생성
                GameObject go = new GameObject("AimIndicator");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                _aimLineRenderer = go.AddComponent<LineRenderer>();
                _aimLineRenderer.startWidth = 0.1f;
                _aimLineRenderer.endWidth = 0.02f;
                _aimLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
                _aimLineRenderer.startColor = aimIndicatorColor;
                _aimLineRenderer.endColor = new Color(aimIndicatorColor.r, aimIndicatorColor.g, aimIndicatorColor.b, 0f);
            }

            if (_aimLineRenderer != null)
            {
                _aimLineRenderer.enabled = true;
            }
        }

        /// <summary>
        /// 조준 경고선 업데이트 (매 프레임 Player 방향 갱신)
        /// </summary>
        private void UpdateAimIndicator()
        {
            if (_aimLineRenderer == null || _playerTransform == null) return;

            Vector3 start = transform.position;
            Vector3 direction = (_playerTransform.position - start).normalized;
            Vector3 end = start + direction * aimIndicatorLength;

            // 바닥에 표시 (Y 고정)
            start.y = 0.05f;
            end.y = 0.05f;

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
        /// 돌진 Trail 효과 표시
        /// </summary>
        private void ShowTrailEffect()
        {
            if (trailEffectPrefab != null)
            {
                _trailInstance = Instantiate(trailEffectPrefab, transform.position, Quaternion.identity, transform);
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
        /// 충돌 이펙트 표시 (벽 충돌 시)
        /// </summary>
        private void ShowImpactEffect()
        {
            if (impactEffectPrefab != null)
            {
                GameObject impact = Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
                Destroy(impact, 2f);
            }
        }

        #endregion

        #region PostChargePatrol

        /// <summary>
        /// 돌진 후 기억된 위치로 이동 (재정비)
        /// </summary>
        private void StartPostChargePatrol()
        {
            if (!_hasPostChargeTarget)
            {
                _currentState = State.Idle;
                return;
            }

            _currentState = State.PostChargePatrol;
            _stateTimer = 5f;

            if (_controller != null)
            {
                _controller.SetSpeed(_controller.GetDefaultSpeed() * 1.2f);
                _controller.MoveTo(_lastChargeTarget);
            }
        }

        #endregion

        #region Caching

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
                _playerMovementAdapter = FindObjectOfType<HideAndInk.Player.PlayerMovementAdapter>();
            }
        }

        private void CacheAnimator()
        {
            if (enemyAnimator == null)
            {
                enemyAnimator = GetComponentInChildren<Animator>();
            }
        }

        #endregion

        #region Animation

        private void PlayAimAnimation()
        {
            if (enemyAnimator != null && !string.IsNullOrEmpty(aimTriggerName))
            {
                enemyAnimator.SetTrigger(aimTriggerName);
            }
        }

        private void PlayChargeAnimation()
        {
            if (enemyAnimator != null && !string.IsNullOrEmpty(chargeTriggerName))
            {
                enemyAnimator.speed = chargeAnimationLength / Mathf.Max(chargeDuration, 0.1f);
                enemyAnimator.SetTrigger(chargeTriggerName);
            }
        }

        private void ResetAnimation()
        {
            if (enemyAnimator != null)
            {
                enemyAnimator.speed = 1f;
                enemyAnimator.ResetTrigger(chargeTriggerName);
                enemyAnimator.ResetTrigger(aimTriggerName);
            }
        }

        #endregion

        #region Legacy Support (serialized fields kept for inspector)

        [Header("레거시 (호환용)")]
        [SerializeField] private float chargeCooldown = 2f;
        [SerializeField] private float chargeDelay = 0.5f;
        [SerializeField] private float chargeWidth = 1.5f;

        #endregion
    }
}
