using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// Ch.5 백상아리 초고속 돌진 기믹 (ScriptableObject)
    /// Chase 전환 시 Player 위치 기록 → 그 방향으로 직선 돌진
    /// 돌진 경로상 엄폐물 비활성화
    /// 돌진 완료 후 2초 정지 → Patrol 복귀
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy Gimmicks/Dash Charge Gimmick", fileName = "DashChargeGimmick")]
    public sealed class DashChargeGimmick : ScriptableObject, IEnemyGimmick,
        IGimmickPlayerAware, IGimmickViewDirection, IGimmickCombatCycle, IGimmickTransitionOverride
    {
        public GimmickType Type => GimmickType.DashCharge;

        [Header("돌진 설정")]
        [Tooltip("돌진 속도 (매우 빠름)")]
        [SerializeField] private float dashSpeed = 15f;
        [Tooltip("돌진 후 정지 시간 (초)")]
        [SerializeField] private float dashCooldown = 2f;
        [Tooltip("돌진 판정 너비 (m)")]
        [SerializeField] private float chargeWidth = 2f;
        [Tooltip("최대 돌진 거리 (m)")]
        [SerializeField] private float maxDashDistance = 20f;

        // 상태
        private Transform _bossTransform;
        private Vector3 _dashStartPos;
        private Vector3 _dashDirection;
        private bool _isCharging;
        private float _chargeTimer;
        private float _cooldownTimer;
        private float _traveledDistance;

        // 외부 연동 콜백
        public System.Action<float> OnSpeedOverride;
        public System.Action<Vector3> OnDashStarted;
        public System.Action<GameObject> OnObstacleHit;
        public System.Action OnDashCompleted;

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _isCharging = false;
            _cooldownTimer = 0f;
        }

        public void OnDeactivate()
        {
            _isCharging = false;
        }

        #region Patrol

        public void OnPatrolEnter()
        {
            _isCharging = false;
            _cooldownTimer = 0f;
        }

        public void OnPatrolUpdate(float deltaTime) { }

        public void OnPatrolExit() { }

        #endregion

        #region Chase

        public void OnChaseEnter() => StartDash();

        public void OnChaseUpdate(float deltaTime)
        {
            if (_isCharging)
            {
                UpdateCharge(deltaTime);
            }
            else if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= deltaTime;
                if (_cooldownTimer <= 0f)
                {
                    OnDashCompleted?.Invoke();
                    Debug.Log("[DashChargeGimmick] 돌진 완료 → Patrol 복귀", this);
                }
            }
        }

        public void OnChaseExit() => _isCharging = false;

        #endregion

        #region Search

        public void OnSearchEnter() => _isCharging = false;

        public void OnSearchUpdate(float deltaTime) { }

        public void OnSearchExit() { }

        #endregion

        private void StartDash()
        {
            if (_isCharging) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;

            Vector3 playerPos = player.transform.position;
            _dashStartPos = _bossTransform.position;
            _dashDirection = (playerPos - _dashStartPos).normalized;
            _dashDirection.y = 0f;
            _isCharging = true;
            _traveledDistance = 0f;

            OnSpeedOverride?.Invoke(dashSpeed);
            OnDashStarted?.Invoke(_dashDirection);
            Debug.Log("[DashChargeGimmick] 초고속 돌진 시작!", this);
        }

        private void UpdateCharge(float deltaTime)
        {
            float moveAmount = dashSpeed * deltaTime;
            _traveledDistance += moveAmount;

            if (_traveledDistance >= maxDashDistance)
            {
                EndCharge();
                return;
            }

            Vector3 endPos = _bossTransform.position + _dashDirection * moveAmount;
            Collider[] hits = Physics.OverlapSphere(endPos, chargeWidth / 2f);
            foreach (var hit in hits)
            {
                if (hit.gameObject.layer == LayerMask.NameToLayer("CamouflageTarget"))
                {
                    hit.gameObject.SetActive(false);
                    OnObstacleHit?.Invoke(hit.gameObject);
                    Debug.Log("[DashChargeGimmick] 엄폐물 파괴: " + hit.gameObject.name, this);
                }
            }
        }

        private void EndCharge()
        {
            _isCharging = false;
            _cooldownTimer = dashCooldown;
            OnSpeedOverride?.Invoke(0f);
            Debug.Log("[DashChargeGimmick] 돌진 종료, 정지 상태 진입", this);
        }

        public bool IsCharging => _isCharging;
        public bool IsCooldown => _cooldownTimer > 0f && !_isCharging;

        #region Interface Implementations

        void IGimmickPlayerAware.SetPlayerTransform(Transform playerTransform) { }
        void IGimmickPlayerAware.SetSuspicionLevel(float normalizedSuspicion) { }
        void IGimmickPlayerAware.SetCamouflageState(bool isCamouflaging) { }
        void IGimmickPlayerAware.SetPlayerVisible(bool isVisible) { }

        bool IGimmickViewDirection.OverridesViewDirection => false;
        Vector3 IGimmickViewDirection.GetViewDirectionVector() => Vector3.right;
        bool IGimmickViewDirection.ShowChargeIndicator => false;

        bool IGimmickCombatCycle.IsInCombatCycle => _isCharging;

        bool IGimmickTransitionOverride.ShouldSkipSearchOnLostPlayer(float normalizedSuspicion) => false;

        #endregion

        #region Movement Override (IEnemyGimmick 확장)

        /// <summary>
        /// DashChargeGimmick은 이동 제어권을 가짐 (절벽 구간 순찰 + 돌진)
        /// </summary>
        public bool HasMovementOverride => true;

        /// <summary>
        /// Patrol 상태 이동 목표: 절벽 구간 X축 순찰 (Z 고정)
        /// </summary>
        public Vector3? GetPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            // 돌진 중이면 돌진 방향 유지
            if (_isCharging)
            {
                Vector3 target = currentPos + _dashDirection * 5f;
                target.y = currentPos.y;
                target.z = currentPos.z; // Z 고정
                return target;
            }

            // 돌진 쿨다운 중이면 정지
            if (_cooldownTimer > 0f)
            {
                return currentPos;
            }

            // 절벽 구간 X축 순찰 (Z 고정)
            float xDistance = Random.Range(8f, 18f);
            float xDir = Random.value > 0.5f ? 1f : -1f;

            Vector3 targetPos = new Vector3(
                currentPos.x + xDir * xDistance,
                currentPos.y,
                currentPos.z // Z 고정
            );

            // Ground 범위 내로 제한
            if (bounds.MinX != bounds.MaxX || bounds.MinZ != bounds.MaxZ)
            {
                targetPos.x = bounds.ClampX(targetPos.x);
                targetPos.z = bounds.ClampZ(targetPos.z);
            }

            return targetPos;
        }

        /// <summary>
        /// Search 상태 이동 목표: 돌진 종료 위치 수색 (Z 고정)
        /// </summary>
        public Vector3? GetSearchTarget(Vector3 currentPos, Vector3 lastKnownPos, GroundBounds bounds)
        {
            // 돌진 종료 위치 기준 X축 수색 (Z 고정)
            float searchRadius = 3f;
            float xDir = Random.value > 0.5f ? 1f : -1f;
            float xDistance = Random.Range(1f, searchRadius);

            Vector3 target = new Vector3(
                lastKnownPos.x + xDir * xDistance,
                currentPos.y,
                currentPos.z // Z 고정
            );

            // Ground 범위 내로 제한
            if (bounds.MinX != bounds.MaxX || bounds.MinZ != bounds.MaxZ)
            {
                target = bounds.ClampXZ(target);
            }

            return target;
        }

        #endregion
    }
}
