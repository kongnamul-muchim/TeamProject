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
    public sealed class DashChargeGimmick : ScriptableObject, IEnemyGimmick
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
    }
}
