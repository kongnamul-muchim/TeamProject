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

        #region Callbacks

        public System.Action<float> OnSpeedOverride;
        public System.Action OnMovementStop;
        public System.Action OnMovementResume;
        public System.Action<bool> OnVisibilityToggle;
        public System.Action<Vector3> OnDashMoveTo;

        #endregion

        #region Properties

        public Vector2 SuspicionRadius => suspicionRadius;
        public float SuspicionRate => suspicionRate;
        public float SuspicionCurveExponent => suspicionCurveExponent;
        public float SuspicionDropThreshold => suspicionDropThreshold;

        #endregion

        #region IEnemyGimmick

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _isDashing = false;
            _isDashPreDelay = false;
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

            // Player 근처 매복 위치로 계속 이동
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
            if (_bossTransform == null || _playerTransform == null) return;

            Vector3 dirToPlayer = (_playerTransform.position - _bossTransform.position).normalized;
            dirToPlayer.y = 0f;

            float currentDist = Vector3.Distance(
                new Vector3(_bossTransform.position.x, 0f, _bossTransform.position.z),
                new Vector3(_playerTransform.position.x, 0f, _playerTransform.position.z));

            if (currentDist < ambushMinDistance)
            {
                // 너무 가까우면 반대 방향으로
                _ambushTarget = _bossTransform.position - dirToPlayer * ambushDistance;
            }
            else if (currentDist > ambushDistance * 1.5f)
            {
                // 너무 멀면 Player 쪽으로
                _ambushTarget = _playerTransform.position - dirToPlayer * 2f;
            }
            else
            {
                // 적정 거리 유지 (Player 주변을 맴돌도록 약간 옆으로)
                Vector3 perpendicular = Vector3.Cross(dirToPlayer, Vector3.up).normalized;
                float sideDir = Mathf.Sin(Time.time * 0.5f) > 0f ? 1f : -1f;
                _ambushTarget = _playerTransform.position + perpendicular * sideDir * ambushDistance * 0.5f;
            }

            _ambushTarget.y = _bossTransform.position.y;
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
        void IGimmickPlayerAware.SetCamouflageState(bool isCamouflaging) { }
        void IGimmickPlayerAware.SetPlayerVisible(bool isVisible) { }

        bool IGimmickViewDirection.OverridesViewDirection => false;
        Vector3 IGimmickViewDirection.GetViewDirectionVector() => Vector3.right;
        bool IGimmickViewDirection.ShowChargeIndicator => false;

        bool IGimmickCombatCycle.IsInCombatCycle => _isDashing;
        bool IGimmickCombatCycle.IsCharging => _isDashing;

        bool IGimmickTransitionOverride.ShouldSkipSearchOnLostPlayer(float normalizedSuspicion) => false;

        #endregion
    }
}
