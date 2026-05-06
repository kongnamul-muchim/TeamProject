using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// 청새치(Swordfish) Boss 기믹 (ScriptableObject)
    /// 
    /// 설계 기준: Swordfish_Design_v3.md
    /// 핵심 사이클: Idle → Aiming → Charging → Stunned → Idle
    /// 
    /// - 의심도 기반 난이도 스케일링 (예측/조준시간/돌진속도)
    /// - GroundBounds 기반 이탈 방지 + Wall Layer 충돌 감지
    /// - Player 놓치면 의심도 Safe 시 Patrol 직행, 아니면 Search
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy Gimmicks/Swordfish Gimmick", fileName = "SwordfishGimmick")]
    public sealed class SwordfishGimmick : ScriptableObject, IEnemyGimmick,
        IGimmickPlayerAware, IGimmickViewDirection, IGimmickCombatCycle, IGimmickTransitionOverride
    {
        public GimmickType Type => GimmickType.Swordfish;

        #region Inspector Parameters

        [Header("돌진 속도 (의심도 보간)")]
        [SerializeField, Tooltip("의심도 0%일 때 돌진 속도")]
        private float minChargeSpeed = 10f;
        [SerializeField, Tooltip("의심도 100%일 때 돌진 속도")]
        private float maxChargeSpeed = 15f;

        [Header("조준 시간 (의심도 보간)")]
        [SerializeField, Tooltip("의심도 0%일 때 조준 시간 (길게)")]
        private float minAimDuration = 0.2f;
        [SerializeField, Tooltip("의심도 100%일 때 조준 시간 (짧게)")]
        private float maxAimDuration = 0.8f;

        [Header("돌진 / 스턴 시간")]
        [SerializeField, Tooltip("돌진 지속 시간 (초)")]
        private float chargeDuration = 1f;
        [SerializeField, Tooltip("충돌 시 스턴 시간 (초)")]
        private float stunDuration = 0.5f;

        [Header("예측 이동 (의심도 보간)")]
        [SerializeField, Tooltip("의심도 0%일 때 예측 계수")]
        private float minPredictionFactor = 0.3f;
        [SerializeField, Tooltip("의심도 100%일 때 예측 계수")]
        private float maxPredictionFactor = 0.9f;

        [Header("돌진 목표 거리")]
        [SerializeField, Tooltip("돌진 목표까지 거리 (GroundBounds로 자동 제한)")]
        private float chargeTargetDistance = 20f;

        [Header("돌진 발동 범위")]
        [SerializeField, Tooltip("Player가 이 범위 안에 있을 때만 Aim/Charge 시작")]
        private float chargeRange = 12f;

        [Header("충돌 감지")]
        [SerializeField, Tooltip("Wall 충돌 판정 반경")]
        private float wallCheckRadius = 0.8f;
        [SerializeField, Tooltip("Wall 레이어 이름")]
        private string wallLayerName = "Wall";
        [SerializeField, Tooltip("Ground 레이어 이름 (돌진 이탈 감지용)")]
        private string groundLayerName = "Ground";
        [SerializeField, Tooltip("Wall 충돌 후 밀려날 거리")]
        private float wallPushbackDistance = 1.5f;
        [SerializeField, Tooltip("Wall 충돌 후 Aim 재진입 금지 시간")]
        private float wallCooldownDuration = 1.5f;

        #endregion

        #region State

        private enum Phase { Idle, Aiming, Charging, Stunned }

        private Transform _bossTransform;
        private Transform _playerTransform;
        private Phase _currentPhase = Phase.Idle;
        private float _phaseTimer;
        private Vector3 _chargeDirection;
        private bool _hasHitWall;
        private float _wallCooldownTimer;

        /// <summary>기절 상태 여부 (BossEnemyController에서 이동 차단용)</summary>
        public bool IsStunned => _currentPhase == Phase.Stunned;

        // 의심도 (0~1, IGimmickPlayerAware.SetSuspicionLevel에서 설정)
        private float _normalizedSuspicion;

        // 의태 상태 (Player 숨음 = 위치 업데이트 차단)
        private bool _isPlayerCamouflaged;

        // Player 시야 가시성 (true=현재 시야에 보임)
        private bool _isPlayerVisible;

        // Wall / Ground 레이어 캐싱
        private int _wallLayerIndex = -1;
        private int _groundLayerIndex = -1;

        #endregion

        #region Callbacks

        public System.Action<float> OnSpeedOverride;
        public System.Action<Vector3> OnMoveTo;
        public System.Action OnMovementStop;
        public System.Action<bool> OnChasePauseRequest;

        #endregion

        #region IEnemyGimmick

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _currentPhase = Phase.Idle;
            _normalizedSuspicion = 0f;
            _isPlayerCamouflaged = false;
            _isPlayerVisible = false;
            _wallLayerIndex = LayerMask.NameToLayer(wallLayerName);
            _groundLayerIndex = LayerMask.NameToLayer(groundLayerName);
            _wallCooldownTimer = 0f;
        }

        public void OnDeactivate()
        {
            _currentPhase = Phase.Idle;
            OnMovementStop?.Invoke();
            OnChasePauseRequest?.Invoke(false);
        }

        public void OnPatrolEnter() { }
        public void OnPatrolUpdate(float deltaTime) { }
        public void OnPatrolExit() { }

        public void OnChaseEnter()
        {
            // 즉시 Aim하지 않고 Idle로 시작 → Controller가 상태 전이(Search/Patrol)할 기회를 줌
            _currentPhase = Phase.Idle;
            _hasHitWall = false;
        }

        public void OnChaseUpdate(float deltaTime)
        {
            // Wall 충돌 쿨다운 감소 (모든 Phase에서 감소)
            if (_wallCooldownTimer > 0f)
                _wallCooldownTimer -= deltaTime;

            switch (_currentPhase)
            {
                case Phase.Idle:
                    // Wall 충돌 직후 쿨다운 중이면 Aim 금지
                    if (_wallCooldownTimer > 0f)
                        return;

                    // Player가 시야에 보이고 + 돌진 발동 범위 내에 있을 때만 Aim 시작
                    if (_isPlayerVisible && IsPlayerInChargeRange())
                        StartAiming();
                    break;
                case Phase.Aiming:
                    UpdateAiming(deltaTime);
                    break;
                case Phase.Charging:
                    UpdateCharging(deltaTime);
                    break;
                case Phase.Stunned:
                    UpdateStunned(deltaTime);
                    break;
            }
        }

        public void OnChaseExit()
        {
            _currentPhase = Phase.Idle;
            OnMovementStop?.Invoke();
            OnChasePauseRequest?.Invoke(false);
        }

        public void OnSearchEnter()
        {
            _currentPhase = Phase.Idle;
        }

        public void OnSearchUpdate(float deltaTime) { }
        public void OnSearchExit() { }

        public bool HasMovementOverride => _currentPhase != Phase.Idle;

        public Vector3? GetPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            switch (_currentPhase)
            {
                case Phase.Charging:
                {
                    Vector3 target = currentPos + _chargeDirection * 5f;
                    target.y = currentPos.y;
                    return target;
                }

                case Phase.Aiming:
                case Phase.Stunned:
                    return currentPos; // 정지

                default:
                    return null; // Idle: PatrolBehavior 기본 순찰 사용
            }
        }

        public Vector3? GetSearchTarget(Vector3 currentPos, Vector3 lastKnownPos, GroundBounds bounds)
        {
            return null; // Search는 일반 수색 로직 사용
        }

        #endregion

        #region IGimmickPlayerAware

        void IGimmickPlayerAware.SetPlayerTransform(Transform playerTransform)
        {
            if (_isPlayerCamouflaged) return; // 의태 중엔 위치 무시
            _playerTransform = playerTransform;
        }

        void IGimmickPlayerAware.SetSuspicionLevel(float normalizedSuspicion)
        {
            _normalizedSuspicion = normalizedSuspicion;
        }

        void IGimmickPlayerAware.SetCamouflageState(bool isCamouflaging)
        {
            _isPlayerCamouflaged = isCamouflaging;
            if (isCamouflaging)
            {
                _playerTransform = null; // 위치 정보 초기화 → 완전히 잊음
                if (_currentPhase == Phase.Idle)
                {
                    // Idle 중 의태 → Controller가 Patrol로 전이할 수 있도록 아무것도 안 함
                }
            }
        }

        void IGimmickPlayerAware.SetPlayerVisible(bool isVisible)
        {
            _isPlayerVisible = isVisible;
        }

        #endregion

        #region IGimmickViewDirection

        bool IGimmickViewDirection.OverridesViewDirection =>
            _currentPhase == Phase.Aiming || _currentPhase == Phase.Charging;

        Vector3 IGimmickViewDirection.GetViewDirectionVector()
        {
            Vector3 result;
            switch (_currentPhase)
            {
                case Phase.Aiming:
                    if (_chargeDirection.sqrMagnitude > 0.01f)
                        result = _chargeDirection;
                    else if (_playerTransform != null && _bossTransform != null)
                    {
                        Vector3 dir = _playerTransform.position - _bossTransform.position;
                        dir.y = 0f;
                        result = dir.normalized;
                    }
                    else
                        result = Vector3.right;
                    break;

                case Phase.Charging:
                    result = _chargeDirection;
                    break;

                default:
                    result = Vector3.right;
                    break;
            }
#if UNITY_EDITOR
            Debug.Log($"[SwordfishGimmick] GetViewDirectionVector phase={_currentPhase} result=({result.x:F2},{result.y:F2},{result.z:F2}) chargeDir=({_chargeDirection.x:F2},{_chargeDirection.y:F2})");
#endif
            return result;
        }

        bool IGimmickViewDirection.ShowChargeIndicator => _currentPhase == Phase.Aiming;

        #endregion

        #region IGimmickCombatCycle

        bool IGimmickCombatCycle.IsInCombatCycle =>
            _currentPhase == Phase.Aiming || _currentPhase == Phase.Charging || _currentPhase == Phase.Stunned;

        bool IGimmickCombatCycle.IsCharging => _currentPhase == Phase.Charging;

        #endregion

        #region IGimmickTransitionOverride

        /// <summary>
        /// 의심도가 20% 미만이면 Search 건너뛰고 Patrol 직행
        /// </summary>
        bool IGimmickTransitionOverride.ShouldSkipSearchOnLostPlayer(float normalizedSuspicion)
        {
            return normalizedSuspicion < 0.2f;
        }

        #endregion

        #region Phase Transitions

        private void StartAiming()
        {
            if (_bossTransform == null) return;

            _currentPhase = Phase.Aiming;
            _phaseTimer = GetScaledAimDuration();
            _hasHitWall = false;

            OnMovementStop?.Invoke();
            OnSpeedOverride?.Invoke(0f);
            OnChasePauseRequest?.Invoke(true);
        }

        private void StartCharge()
        {
            if (_bossTransform == null) return;

            _currentPhase = Phase.Charging;
            _phaseTimer = chargeDuration;
            _hasHitWall = false;

            float speed = GetScaledChargeSpeed();
            Vector3 chargeDir = CalculateChargeDirection();

            _chargeDirection = chargeDir;

            OnSpeedOverride?.Invoke(speed);
            OnMoveTo?.Invoke(_bossTransform.position + chargeDir * chargeTargetDistance);
        }

        private void EndCharge()
        {
            OnMovementStop?.Invoke();
            OnSpeedOverride?.Invoke(0f);

            if (_hasHitWall)
            {
                // Wall 충돌 / Bounds 이탈 → Stun + 밀어내기 + 쿨다운
                _currentPhase = Phase.Stunned;
                _phaseTimer = stunDuration;

                // 기절 중 ChaseBehavior 완전 차단
                OnChasePauseRequest?.Invoke(true);

                if (_bossTransform != null)
                {
                    Vector3 pushbackPos = _bossTransform.position + (-_chargeDirection) * wallPushbackDistance;
                    pushbackPos.y = _bossTransform.position.y;
                    if (!IsGroundAtPosition(pushbackPos))
                        pushbackPos = _bossTransform.position;
                    _bossTransform.position = pushbackPos;

                    _wallCooldownTimer = wallCooldownDuration;
                }
            }
            else
            {
                // 타이머 만료 (노히트) → Stun 없이 Idle 복귀
                _currentPhase = Phase.Idle;
                OnChasePauseRequest?.Invoke(false);
            }
        }

        #endregion

        #region Phase Updates

        private void UpdateAiming(float deltaTime)
        {
            _phaseTimer -= deltaTime;

            // Aim 중 매 프레임 돌진 방향 미리 계산 (Sprite 방향 전환 + 예측 갱신)
            _chargeDirection = CalculateChargeDirection();

            if (_phaseTimer <= 0f)
            {
                StartCharge();
            }
        }

        private void UpdateCharging(float deltaTime)
        {
            _phaseTimer -= deltaTime;

            // 돌진 중 충돌 체크: Ground 이탈 + Wall Layer 충돌
            if (!_hasHitWall && _bossTransform != null)
            {
                Vector3 currentPos = _bossTransform.position;
                Vector3 nextPos = currentPos + _chargeDirection * GetScaledChargeSpeed() * deltaTime;

                // Ground 이탈 체크 (다음 위치에 Ground가 없으면 충돌)
                if (!IsGroundAtPosition(nextPos))
                {
                    _hasHitWall = true;
                }

                // Wall Layer 충돌 체크 — 돌진 방향으로만 Raycast (정밀 판정, OverlapSphere 대체)
                if (!_hasHitWall && _wallLayerIndex >= 0)
                {
                    _hasHitWall = CheckWallCollision(nextPos);
                }
            }

            if (_phaseTimer <= 0f || _hasHitWall)
            {
                EndCharge();
            }
        }

        private void UpdateStunned(float deltaTime)
        {
            // 기절 중 매 프레임 이동/속도 강제 정지 (ChaseBehavior 우회 방어)
            OnMovementStop?.Invoke();
            OnSpeedOverride?.Invoke(0f);

            _phaseTimer -= deltaTime;
            if (_phaseTimer <= 0f)
            {
                _currentPhase = Phase.Idle;
                OnChasePauseRequest?.Invoke(false);
            }
        }

        #endregion

        #region Charge Logic

        /// <summary>
        /// Player 예측 위치 기반 돌진 방향 계산
        /// </summary>
        private Vector3 CalculateChargeDirection()
        {
            Vector3 chargeDir;

            if (_playerTransform != null && _bossTransform != null)
            {
                Vector3 bossPos = _bossTransform.position;
                Vector3 playerPos = _playerTransform.position;
                Vector3 playerVelocity = Vector3.zero;

                // PlayerMovementAdapter에서 속도 정보 획득
                var movementAdapter = _playerTransform.GetComponent<HideAndInk.Player.PlayerMovementAdapter>();
                if (movementAdapter != null)
                {
                    Vector2 vel2D = movementAdapter.CurrentVelocity;
                    playerVelocity = new Vector3(vel2D.x, 0f, vel2D.y);
                }

                float speed = GetScaledChargeSpeed();
                float timeToReach = Vector3.Distance(bossPos, playerPos) / Mathf.Max(speed, 0.1f);
                float prediction = GetScaledPredictionFactor();
                Vector3 predictedPos = playerPos + (playerVelocity * timeToReach * prediction);

                chargeDir = (predictedPos - bossPos).normalized;
                chargeDir.y = 0f; // Y축만 고정 (점프 높이 무시)
            }
            else
            {
                // Player 정보 없으면 보스 정면 방향 fallback
                chargeDir = _bossTransform != null ? _bossTransform.right : Vector3.right;
            }

            if (chargeDir.sqrMagnitude < 0.01f)
                chargeDir = Vector3.right;

            return chargeDir.normalized;
        }

        /// <summary>
        /// 지정 위치 아래에 Ground가 있는지 확인 (Physics Raycast)
        /// </summary>
        private bool IsGroundAtPosition(Vector3 pos)
        {
            if (_bossTransform == null) return true;
            if (_groundLayerIndex < 0) return true; // 레이어 미설정 시 일단 허용

            int layerMask = 1 << _groundLayerIndex;
            Vector3 origin = new Vector3(pos.x, pos.y + 0.5f, pos.z);
            return Physics.Raycast(origin, Vector3.down, out _, 2f, layerMask);
        }

        /// <summary>
        /// Wall Layer 충돌 체크 — 돌진 방향 Raycast (정밀 판정)
        /// OverlapSphere는 반경 내 모든 Wall을 감지하여 너무 쉽게 충돌했음
        /// </summary>
        private bool CheckWallCollision(Vector3 checkPos)
        {
            if (_wallLayerIndex < 0) return false;

            int layerMask = 1 << _wallLayerIndex;
            // 한 프레임 이동 거리만큼만 전방 체크 (불필요한 여유 제거)
            float checkDistance = GetScaledChargeSpeed() * Time.deltaTime + 0.1f;

            return Physics.Raycast(checkPos, _chargeDirection, checkDistance, layerMask);
        }

        #endregion

        #region Difficulty Scaling (의심도 기반)

        private float GetScaledChargeSpeed()
        {
            return Mathf.Lerp(minChargeSpeed, maxChargeSpeed, _normalizedSuspicion);
        }

        /// <summary>
        /// 의심도 높을수록 조준 시간 짧아짐 (회피 어려움)
        /// </summary>
        private float GetScaledAimDuration()
        {
            // 의심도 0% = maxAimDuration(0.8s), 의심도 100% = minAimDuration(0.2s)
            return Mathf.Lerp(maxAimDuration, minAimDuration, _normalizedSuspicion);
        }

        private float GetScaledPredictionFactor()
        {
            return Mathf.Lerp(minPredictionFactor, maxPredictionFactor, _normalizedSuspicion);
        }

        /// <summary>
        /// Player가 돌진 발동 범위 내에 있는지 확인
        /// 의태 중이거나 너무 멀면 false
        /// </summary>
        private bool IsPlayerInChargeRange()
        {
            if (_bossTransform == null || _playerTransform == null) return false;
            if (_isPlayerCamouflaged) return false; // 의태 중엔 돌진 안 함
            float dist = Vector3.Distance(_bossTransform.position, _playerTransform.position);
            return dist <= chargeRange;
        }

        #endregion
    }
}
