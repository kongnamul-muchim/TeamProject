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
        [SerializeField] private Vector2 suspicionRadius = new Vector2(10f, 10f);
        [SerializeField] private float suspicionRate = 15f;
        [SerializeField, Range(1f, 5f)] private float suspicionCurveExponent = 2f;
        [SerializeField] private float suspicionDropThreshold = 20f;

        [Header("돌진 설정")]
        [SerializeField] private float dashSpeed = 8f;
        [SerializeField] private float dashDuration = 1.5f;
        [SerializeField] private float dashPreDelay = 0.3f;

        [Header("구덩이(Pit) 설정")]
        [SerializeField, Tooltip("Pit 생성 간격 (이동 거리 m)")]
        private float pitSpawnInterval = 3f;
        [SerializeField, Tooltip("Pit 생성 시 주변 랜덤 범위")]
        private float pitClusterRadius = 3f;
        [SerializeField, Tooltip("Pit 생성 시 추가 Pit 개수 (2~4)")]
        private Vector2Int pitClusterCount = new Vector2Int(2, 4);
        [SerializeField, Tooltip("목표 위치 선정 시 Pit 회피 반경")]
        private float positionAvoidRadius = 2f;

        private Transform _bossTransform;
        private Transform _playerTransform;
        private GroundBounds _groundBounds;
        private bool _hasGroundBounds;

        // 상태
        private bool _isDashing;
        private bool _isDashPreDelay;
        private float _dashTimer;
        private float _preDelayTimer;
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

        #region Callbacks

        public System.Action<float> OnSpeedOverride;
        public System.Action OnMovementStop;
        public System.Action OnMovementResume;
        public System.Action<bool> OnVisibilityToggle;
        public System.Action<Vector3> OnDashMoveTo;
        public System.Action<Vector3> OnSpawnPit;

        #endregion

        #region Properties

        public Vector2 SuspicionRadius => suspicionRadius;
        public float SuspicionRate => suspicionRate;
        public float SuspicionCurveExponent => suspicionCurveExponent;
        public float SuspicionDropThreshold => suspicionDropThreshold;
        public Vector2Int PitClusterCount => pitClusterCount;
        public float PitClusterRadius => pitClusterRadius;

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
            // 시야 숨김 (거리 전용 감지)
            OnVisibilityToggle?.Invoke(true);
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

            // Player 근처 매복 위치로 계속 이동 (Pit 회피 적용)
            UpdateAmbushTarget();
            if (_ambushTarget != Vector3.zero)
            {
                OnDashMoveTo?.Invoke(_ambushTarget);
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
            _isDashPreDelay = true;
            _preDelayTimer = dashPreDelay;
            OnDashMoveTo?.Invoke(_bossTransform != null ? _bossTransform.position : Vector3.zero);
        }

        public void OnChaseUpdate(float deltaTime)
        {
            if (_isDashPreDelay)
            {
                _preDelayTimer -= deltaTime;
                if (_preDelayTimer <= 0f)
                {
                    StartDash();
                }
                return;
            }

            if (_isDashing)
            {
                _dashTimer -= deltaTime;
                if (_dashTimer <= 0f)
                {
                    EndDash();
                }
                // OnDashMoveTo는 StartDash에서 한 번 호출, 직선 유지
                return;
            }
        }

        public void OnChaseExit()
        {
            _isDashing = false;
            _isDashPreDelay = false;
            OnMovementResume?.Invoke();
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

            // Player 방향으로 돌진
            _dashDirection = (_playerTransform.position - _bossTransform.position).normalized;
            _dashDirection.y = 0f;
            if (_dashDirection.sqrMagnitude < 0.01f)
                _dashDirection = Vector3.right;

            OnSpeedOverride?.Invoke(dashSpeed);
            OnDashMoveTo?.Invoke(_bossTransform.position + _dashDirection * dashSpeed * dashDuration);
        }

        private void EndDash()
        {
            _isDashing = false;
            OnSpeedOverride?.Invoke(0f);
            OnMovementStop?.Invoke();
            OnMovementResume?.Invoke();
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
                // 적정 거리 유지 (Player 주변을 맴돌도록 약간 옆으로)
                Vector3 perpendicular = Vector3.Cross(dirToPlayer, Vector3.up).normalized;
                float sideDir = Mathf.Sin(Time.time * 0.5f) > 0f ? 1f : -1f;
                baseTarget = _playerTransform.position + perpendicular * sideDir * ambushDistance * 0.5f;
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

        bool IGimmickViewDirection.OverridesViewDirection => false;
        Vector3 IGimmickViewDirection.GetViewDirectionVector() => Vector3.right;
        bool IGimmickViewDirection.ShowChargeIndicator => false;

        bool IGimmickCombatCycle.IsInCombatCycle => _isDashPreDelay || _isDashing;
        bool IGimmickCombatCycle.IsCharging => _isDashing;

        bool IGimmickTransitionOverride.ShouldSkipSearchOnLostPlayer(float normalizedSuspicion) => false;

        #endregion
    }
}
