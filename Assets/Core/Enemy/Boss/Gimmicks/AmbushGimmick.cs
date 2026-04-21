using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// Ch.1 가자미 매복 기믹 (ScriptableObject)
    /// 모래에 파묻혀 매복 → Player 감지 시 기습 돌진
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy Gimmicks/Ambush Gimmick", fileName = "AmbushGimmick")]
    public sealed class AmbushGimmick : ScriptableObject, IEnemyGimmick
    {
        public GimmickType Type => GimmickType.Ambush;

        [Header("매복 설정")]
        [Tooltip("매복 대기 시간 (초)")]
        [SerializeField] private float ambushDuration = 3f;
        [Tooltip("기습 돌진 속도")]
        [SerializeField] private float dashSpeed = 8f;
        [Tooltip("돌진 지속 시간 (초)")]
        [SerializeField] private float dashDuration = 1.5f;
        [Tooltip("매복 지점 복귀 속도")]
        [SerializeField] private float returnSpeed = 2f;

        // 상태
        private Transform _bossTransform;
        private Vector3 _ambushPoint;
        private bool _isAmbushing;
        private float _ambushTimer;
        private bool _isDashing;
        private float _dashTimer;
        private Vector3 _dashTarget;
        private float _originalSpeed;

        // 외부 연동 콜백
        public System.Action<float> OnSpeedOverride;
        public System.Action<bool> OnVisibilityToggle;
        public System.Action<Vector3> OnDashCompleted;

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _ambushPoint = bossTransform.position;
            _isAmbushing = true;
            _ambushTimer = ambushDuration;
            _isDashing = false;
            OnVisibilityToggle?.Invoke(false);
        }

        public void OnDeactivate()
        {
            _isAmbushing = false;
            _isDashing = false;
            OnVisibilityToggle?.Invoke(true);
        }

        #region Patrol

        public void OnPatrolEnter()
        {
            _ambushPoint = _bossTransform.position;
            _isAmbushing = true;
            _ambushTimer = ambushDuration;
            OnVisibilityToggle?.Invoke(false);
        }

        public void OnPatrolUpdate(float deltaTime)
        {
            if (!_isAmbushing) return;
            _ambushTimer -= deltaTime;
            if (_ambushTimer <= 0f)
            {
                OnVisibilityToggle?.Invoke(true);
                _isAmbushing = false;
                _ambushTimer = Random.Range(2f, 5f);
            }
        }

        public void OnPatrolExit()
        {
            _isAmbushing = true;
            _ambushTimer = ambushDuration;
            OnVisibilityToggle?.Invoke(false);
        }

        #endregion

        #region Chase

        public void OnChaseEnter()
        {
            _isAmbushing = false;
            _isDashing = true;
            _dashTimer = dashDuration;
            OnSpeedOverride?.Invoke(dashSpeed);
            OnVisibilityToggle?.Invoke(true);
            Debug.Log("[AmbushGimmick] 기습 돌진 시작!");
        }

        public void OnChaseUpdate(float deltaTime)
        {
            if (!_isDashing) return;
            _dashTimer -= deltaTime;
            if (_dashTimer <= 0f)
            {
                _isDashing = false;
                OnSpeedOverride?.Invoke(_originalSpeed);
                Debug.Log("[AmbushGimmick] 기습 돌진 종료");
            }
        }

        public void OnChaseExit()
        {
            _isDashing = false;
            OnSpeedOverride?.Invoke(_originalSpeed);
        }

        #endregion

        #region Search

        public void OnSearchEnter()
        {
            _isAmbushing = true;
            _ambushTimer = ambushDuration;
            OnVisibilityToggle?.Invoke(false);
        }

        public void OnSearchUpdate(float deltaTime) { }

        public void OnSearchExit()
        {
            _isAmbushing = false;
            OnVisibilityToggle?.Invoke(true);
        }

        #endregion

        public void SetDashTarget(Vector3 target) => _dashTarget = target;
        public bool IsDashing => _isDashing;
        public bool IsAmbushing => _isAmbushing;
        public void SetOriginalSpeed(float speed) => _originalSpeed = speed;
    }
}
