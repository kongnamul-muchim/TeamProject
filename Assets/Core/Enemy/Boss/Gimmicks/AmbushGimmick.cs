using System.Collections.Generic;
using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// Zone 1 가자미 매복 기믹 (ScriptableObject)
    /// Player 근처에 매복 → Chase 시 1회 돌진 → 추적
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy Gimmicks/Ambush Gimmick", fileName = "AmbushGimmick")]
    public sealed class AmbushGimmick : ScriptableObject, IEnemyGimmick,
        IGimmickPlayerAware, IGimmickViewDirection, IGimmickCombatCycle, IGimmickTransitionOverride
    {
        public GimmickType Type => GimmickType.Ambush;

        [Header("매복 설정")]
        [SerializeField] private float ambushDistance = 6f;
        [SerializeField] private float ambushMinDistance = 3f;

        [Header("의심도 설정")]
        [SerializeField, Tooltip("Pit로만 상승한 의심도가 이 값 아래로 떨어지면 Search→Patrol 복귀")]
        private float suspicionDropThreshold = 20f;

        [Header("돌진 설정")]
        [SerializeField] private float dashSpeed = 18f;
        [SerializeField] private float dashDuration = 0.8f;
        [SerializeField] private float dashPreDelay = 0.15f;
        [SerializeField] private float restDuration = 2f;
        [SerializeField, Tooltip("연속 공격 가능 횟수 (0 = 무제한)")]
        private int maxConsecutiveAttacks = 3;

        [Header("Patrol 이동 설정")]
        [SerializeField, Tooltip("Patrol 중 이동 속도 (빠를수록 구덩이 생성도 빨라짐)")]
        private float patrolMoveSpeed = 4f;

        [Header("구덩이(Pit) 설정")]
        [SerializeField, Tooltip("Pit 생성 간격 (이동 거리 m)")]
        private float pitSpawnInterval = 3f;
        [SerializeField, Tooltip("Pit 생성 시 주변 랜덤 범위")]
        private float pitClusterRadius = 3f;
        [SerializeField, Tooltip("Pit 생성 시 추가 Pit 개수 (2~4)")]
        private Vector2Int pitClusterCount = new Vector2Int(2, 4);
        [SerializeField, Tooltip("목표 위치 선정 시 Pit 회피 반경")]
        private float positionAvoidRadius = 2f;
        [SerializeField, Tooltip("Pit 밟았을 때 이동 속도 감소 비율 (0.5 = 50% 감소)")]
        private float pitSlowPercent = 0.5f;
        [SerializeField, Tooltip("Pit 슬로우 지속 시간 (초)")]
        private float pitSlowDuration = 3f;

        [Header("근접 감지 설정")]
        [SerializeField, Tooltip("이 거리 이내로 Player가 접근하면 즉시 Chase 돌입 (의태 시 면역)")]
        private float proximityChaseDistance = 12f;

        private Transform _bossTransform;
        private Transform _playerTransform;
        private GroundBounds _groundBounds;
        private bool _hasGroundBounds;

        // 상태
        private bool _isDashing;
        private bool _isDashPreDelay;
        private bool _isResting;
        private float _dashTimer;
        private float _preDelayTimer;
        private float _restTimer;
        private Vector3 _dashDirection;
        private Vector3 _ambushTarget;
        private bool _isPlayerCamouflaged;

        // Pit 관련
        private Vector3 _lastPitSpawnPos;
        private float _movementSinceLastPit;
        private List<Vector3> _visitedPositions = new List<Vector3>();
        private const int MaxVisitedPositions = 30;

        // 의태 무작위 이동
        private float _randomWanderTimer;
        private Vector3 _randomWanderTarget;

        // Patrol 타이머
        private float _patrolTimer;

        // 연속 공격 카운터
        private int _attackCycleCount;

        #region Callbacks

        public System.Action<float> OnSpeedOverride;
        public System.Action OnMovementStop;
        public System.Action OnMovementResume;
        public System.Action<bool> OnVisibilityToggle;
        public System.Action<Vector3> OnDashMoveTo;
        public System.Action<Vector3> OnSpawnPit;
        public System.Action<bool> OnCombatStateChanged;
        public System.Action<bool> OnSetChasePaused;
        public System.Action OnDashTrigger;
        public System.Action OnForcePatrol;

        #endregion

        #region Properties

        public float SuspicionDropThreshold => suspicionDropThreshold;
        public Vector2Int PitClusterCount => pitClusterCount;
        public float PitClusterRadius => pitClusterRadius;
        public float PitSlowPercent => pitSlowPercent;
        public float PitSlowDuration => pitSlowDuration;
        public float ProximityChaseDistance => proximityChaseDistance;

        #endregion

        #region IEnemyGimmick

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _isDashing = false;
            _isDashPreDelay = false;
            _movementSinceLastPit = 0f;
            _lastPitSpawnPos = bossTransform.position;
            _visitedPositions.Clear();
            CachePlayerTransform();
        }

        public void OnDeactivate()
        {
            _isDashing = false;
            _isDashPreDelay = false;
            OnMovementResume?.Invoke();
            OnVisibilityToggle?.Invoke(false);
        }

        public void OnPatrolEnter()
        {
            _isDashing = false;
            // 시야 유지: AmbushSuspicionModule이 360도 거리 기반 감지,
            // Vision Cone은 방향성 시각 피드백용 (보너스 의심도)
            OnVisibilityToggle?.Invoke(false);
            // Patrol 속도 적용 (느린 속도 → 구덩이 생성 간격 증가 방지)
            OnSpeedOverride?.Invoke(patrolMoveSpeed);
            // 첫 Patrol 목표는 즉시 선정
            _patrolTimer = 0f;
        }

        public void OnPatrolUpdate(float deltaTime)
        {
            if (_bossTransform == null || _playerTransform == null)
            {
                CachePlayerTransform();
                return;
            }

            // 이동 거리 누적 → 일정 거리마다 Pit 생성
            float moved = Vector3.Distance(_bossTransform.position, _lastPitSpawnPos);
            _movementSinceLastPit += moved;
            _lastPitSpawnPos = _bossTransform.position;

            if (_movementSinceLastPit >= pitSpawnInterval)
            {
                _movementSinceLastPit = 0f;
                OnSpawnPit?.Invoke(_bossTransform.position);
                AddVisitedPosition(_bossTransform.position);
            }

            // Patrol 목표 갱신 (주기적: 과도한 이동 → pit 과다 생성 방지)
            _patrolTimer -= deltaTime;
            if (_patrolTimer <= 0f)
            {
                _patrolTimer = Random.Range(1.5f, 3.5f);
                UpdateAmbushTarget();
                if (_ambushTarget != Vector3.zero)
                {
                    OnDashMoveTo?.Invoke(_ambushTarget);
                }
            }
        }

        public void OnPatrolExit()
        {
            OnVisibilityToggle?.Invoke(false);
        }

        public void OnChaseEnter()
        {
            OnVisibilityToggle?.Invoke(false);
            _isDashing = false;
            _isDashPreDelay = false;
            _isResting = false;
            _attackCycleCount = 0; // 연속 공격 카운터 리셋
            OnDashMoveTo?.Invoke(_bossTransform != null ? _bossTransform.position : Vector3.zero);
            OnSetChasePaused?.Invoke(true); // ChaseBehavior 정지 → 가자미가 직접 이동 제어
            OnCombatStateChanged?.Invoke(true); // Chase 시작 → 의심도 상승 차단
            // 첫 공격 시작
            StartNextAttack();
        }

        public void OnChaseUpdate(float deltaTime)
        {
            if (_isDashPreDelay)
            {
                _preDelayTimer -= deltaTime;
                if (_preDelayTimer <= 0f) StartDash();
                return;
            }

            if (_isDashing)
            {
                _dashTimer -= deltaTime;
                // Dash 방향은 StartDash()에서 한 번만 고정 (청새치와 동일 패턴)
                if (_dashTimer <= 0f) EndDash();
                return;
            }

            if (_isResting)
            {
                _restTimer -= deltaTime;
                // 휴식 중: 정지 상태 유지 (움직임 없음)
                if (_restTimer <= 0f)
                {
                    // 다음 공격 시작
                    StartNextAttack();
                }
                return;
            }

            // 초기 상태 (OnChaseEnter 직후) → 첫 공격
            StartNextAttack();
        }

        /// <summary>
        /// 다음 공격(Predelay→Dash) 시작
        /// 최대 연속 공격 횟수 초과 시 강제 Patrol 복귀
        /// </summary>
        private void StartNextAttack()
        {
            _attackCycleCount++;
            if (maxConsecutiveAttacks > 0 && _attackCycleCount >= maxConsecutiveAttacks)
            {
                // 연속 공격 제한 도달 → Patrol 복귀
                _isResting = false;
                _isDashPreDelay = false;
                _isDashing = false;
                OnForcePatrol?.Invoke();
                return;
            }

            _isResting = false;
            _isDashPreDelay = true;
            _preDelayTimer = dashPreDelay;
            OnDashMoveTo?.Invoke(_bossTransform != null ? _bossTransform.position : Vector3.zero);
        }

        public void OnChaseExit()
        {
            _isDashing = false;
            _isDashPreDelay = false;
            _isResting = false;
            OnSetChasePaused?.Invoke(false); // ChaseBehavior 재개
            OnMovementResume?.Invoke();
            OnCombatStateChanged?.Invoke(false); // Chase 종료 → 의심도 상승 허용
        }

        public void OnSearchEnter()
        {
            _isDashing = false;
        }

        public void OnSearchUpdate(float deltaTime) { }

        public void OnSearchExit() { }

        #endregion

        #region Actions

        private void StartDash()
        {
            if (_bossTransform == null || _playerTransform == null)
            {
                _isDashPreDelay = false;
                return;
            }

            _isDashPreDelay = false;
            _isDashing = true;
            _dashTimer = dashDuration;

            // Dash 방향을 시작 시 한 번만 계산 (고정)
            Vector3 dir = (_playerTransform.position - _bossTransform.position).normalized;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
                dir = Vector3.right;
            _dashDirection = dir;

            OnSpeedOverride?.Invoke(dashSpeed);
            OnDashTrigger?.Invoke(); // 애니메이터 OnDash 트리거
            OnDashMoveTo?.Invoke(_bossTransform.position + _dashDirection * dashSpeed * dashDuration);

            // 애니메이션 클립(0.917s)을 dashDuration의 2배 시간에 맞춰 재생 (느리게)
            // speed = clipLength / (dashDuration * 2f) → 0.917 / 1.6 ≈ 0.573
            var anim = _bossTransform.GetComponent<Animator>();
            if (anim != null)
            {
                const float animClipLength = 0.9166666f; // Ch2_CommomEnemy_Move.anim
                anim.speed = animClipLength / (dashDuration * 2f);
            }
        }

        private void EndDash()
        {
            _isDashing = false;
            OnSpeedOverride?.Invoke(0f);
            OnMovementStop?.Invoke();

            // Animator 재생 속도 복원
            if (_bossTransform != null)
            {
                var anim = _bossTransform.GetComponent<Animator>();
                if (anim != null) anim.speed = 1f;
            }

            // 휴식 시작: Dash 후 잠시 정지 → 다음 공격 준비
            _isResting = true;
            _restTimer = restDuration;
        }

        private void UpdateAmbushTarget()
        {
            if (_bossTransform == null) return;

            if (_isPlayerCamouflaged || _playerTransform == null)
            {
                // 의태 중 or Player 없음 → 무작위 방황 (구덩이 생성으로 의태 오래 방지)
                UpdateRandomWander();
                return;
            }

            Vector3 dirToPlayer = (_playerTransform.position - _bossTransform.position).normalized;
            dirToPlayer.y = 0f;

            float currentDist = Vector3.Distance(
                new Vector3(_bossTransform.position.x, 0f, _bossTransform.position.z),
                new Vector3(_playerTransform.position.x, 0f, _playerTransform.position.z));

            Vector3 baseTarget;

            if (currentDist < ambushMinDistance)
            {
                // 너무 가까우면 반대 방향으로
                baseTarget = _bossTransform.position - dirToPlayer * ambushDistance;
            }
            else if (currentDist > ambushDistance * 1.5f)
            {
                // 너무 멀면 Player 쪽으로
                baseTarget = _playerTransform.position - dirToPlayer * 2f;
            }
            else
            {
                // 적정 거리 유지 (Player 주변을 넓게 맴돌도록 측면 오프셋)
                Vector3 perpendicular = Vector3.Cross(dirToPlayer, Vector3.up).normalized;
                float sideDir = Mathf.Sin(Time.time * 0.3f) > 0f ? 1f : -1f;
                baseTarget = _playerTransform.position + perpendicular * sideDir * ambushDistance * 0.8f;
            }

            baseTarget.y = _bossTransform.position.y;

            // 이미 방문한 위치(Pit)와 가까우면 살짝 회피
            _ambushTarget = AvoidVisitedPositions(baseTarget);
        }

        /// <summary>
        /// 의태 중 무작위 방황: GroundBounds 내 랜덤 지점으로 이동
        /// </summary>
        private void UpdateRandomWander()
        {
            _randomWanderTimer -= Time.deltaTime;

            if (_randomWanderTimer <= 0f || _randomWanderTarget == Vector3.zero)
            {
                // 새 무작위 목표 선정
                if (_hasGroundBounds)
                {
                    float x = Random.Range(_groundBounds.MinX + 1f, _groundBounds.MaxX - 1f);
                    float z = Random.Range(_groundBounds.MinZ + 1f, _groundBounds.MaxZ - 1f);
                    _randomWanderTarget = new Vector3(x, _bossTransform.position.y, z);
                }
                else
                {
                    _randomWanderTarget = _bossTransform.position + new Vector3(
                        Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));
                }
                _randomWanderTimer = Random.Range(2f, 5f); // 2~5초마다 새 목표
            }

            _ambushTarget = _randomWanderTarget;
        }

        /// <summary>
        /// 이미 방문한 위치(Pit)와 가까우면 목표를 살짝 비껴서 선정
        /// </summary>
        private Vector3 AvoidVisitedPositions(Vector3 target)
        {
            for (int i = 0; i < _visitedPositions.Count; i++)
            {
                float dist = Vector3.Distance(
                    new Vector3(target.x, 0f, target.z),
                    new Vector3(_visitedPositions[i].x, 0f, _visitedPositions[i].z));

                if (dist < positionAvoidRadius)
                {
                    // 방문 위치에서 멀어지는 방향으로 목표 회피
                    Vector3 away = (target - _visitedPositions[i]).normalized;
                    away.y = 0f;
                    if (away.sqrMagnitude < 0.01f)
                        away = Vector3.right;

                    target += away * (positionAvoidRadius - dist + 0.5f);
                    target.y = _bossTransform.position.y;
                }
            }
            return target;
        }

        private void AddVisitedPosition(Vector3 pos)
        {
            _visitedPositions.Add(pos);
            // 오래된 위치 제거 (최대 개수 유지)
            while (_visitedPositions.Count > MaxVisitedPositions)
                _visitedPositions.RemoveAt(0);
        }

        private void CachePlayerTransform()
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                _playerTransform = playerObj.transform;
        }

        #endregion

        #region IEnemyGimmick Movement Override

        public bool HasMovementOverride => true; // Patrol/Search에서 매복 위치로 이동

        public void SetGroundBounds(GroundBounds bounds)
        {
            _groundBounds = bounds;
            _hasGroundBounds = true;
        }

        public Vector3? GetPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            if (_ambushTarget == Vector3.zero) return null;
            Vector3 target = _ambushTarget;
            target.y = currentPos.y;
            return ClampToBounds(target, bounds);
        }

        public Vector3? GetSearchTarget(Vector3 currentPos, Vector3 lastKnownPos, GroundBounds bounds)
        {
            // Search: 매복 지점 복귀 (Patrol과 동일)
            return GetPatrolTarget(currentPos, bounds);
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

        #region Interface Implementations

        void IGimmickPlayerAware.SetPlayerTransform(Transform playerTransform)
        {
            _playerTransform = playerTransform;
        }

        void IGimmickPlayerAware.SetSuspicionLevel(float normalizedSuspicion) { }
        void IGimmickPlayerAware.SetCamouflageState(bool isCamouflaging)
        {
            _isPlayerCamouflaged = isCamouflaging;
            if (isCamouflaging)
            {
                // 의태 시작 → 즉시 새 무작위 목표 선정
                _randomWanderTimer = 0f;
            }
        }
        void IGimmickPlayerAware.SetPlayerVisible(bool isVisible) { }

        bool IGimmickViewDirection.OverridesViewDirection => true;

        Vector3 IGimmickViewDirection.GetViewDirectionVector()
        {
            // 1순위: Dash 중 → 돌진 방향 유지
            if (_isDashing && _dashDirection.sqrMagnitude > 0.01f)
                return _dashDirection;

            // 2순위: PreDelay → Player 방향 (다음 돌진 예고)
            if (_isDashPreDelay)
                return GetDirectionToPlayer();

            // 3순위: Rest → Player 방향 (휴식 중에도 Player 주시)
            if (_isResting)
                return GetDirectionToPlayer();

            // 4순위: Patrol → 매복 목표 이동 방향 (움직이는 방향으로 정면)
            if (_ambushTarget != Vector3.zero && _bossTransform != null)
            {
                Vector3 dir = _ambushTarget - _bossTransform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    return dir.normalized;
            }
            return GetDirectionToPlayer();
        }

        /// <summary>
        /// Player 방향 벡터 반환 (실패 시 Vector3.right)
        /// </summary>
        private Vector3 GetDirectionToPlayer()
        {
            if (_playerTransform != null && _bossTransform != null)
            {
                Vector3 dir = _playerTransform.position - _bossTransform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    return dir.normalized;
            }
            return Vector3.right;
        }

        bool IGimmickViewDirection.ShowChargeIndicator => false;

        bool IGimmickCombatCycle.IsInCombatCycle => _isDashPreDelay || _isDashing;
        bool IGimmickCombatCycle.IsCharging => _isDashing;

        bool IGimmickTransitionOverride.ShouldSkipSearchOnLostPlayer(float normalizedSuspicion) => false;

        #endregion
    }
}
