using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// Zone 3 곰치 (Moray Eel) — 오케스트레이터
    /// 
    /// Chase-only 보스. Patrol 없음, 항상 Player 추격.
    /// 의심도 자동 상승 → 100% → 돌진 시퀀스 → 리셋 루프.
    /// 실제 돌진 로직은 MorayChargeDirector가 처리.
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy Gimmicks/Relentless Chase Gimmick", fileName = "RelentlessChaseGimmick")]
    public sealed class RelentlessChaseGimmick : ScriptableObject, IEnemyGimmick,
        IGimmickPlayerAware, IGimmickViewDirection, IGimmickCombatCycle, IGimmickTransitionOverride
    {
        public GimmickType Type => GimmickType.RelentlessChase;

        [Header("의심도")]
        [SerializeField] private float suspicionAutoRate = 8f;      // 초당 자동 증가
        [SerializeField] private float suspicionMoveBonus = 12f;    // Player 이동 시 추가 증가
        [SerializeField] private float moveThreshold = 1f;          // 이동 감지 임계 속도
        [SerializeField] private float postChaseSuspicion = 0f;     // 돌진 후 리셋값

        [Header("돌진")]
        [SerializeField] private int maxChargesPerCycle = 5;
        public int MaxChargesPerCycle => maxChargesPerCycle;

        // ─── 콜백 (Controller 연결) ───
        /// <summary>의심도 증가 요청: rate * dt 만큼 AddSuspicion</summary>
        public System.Action<float, float> OnIncreaseSuspicion;

        // ─── 인터페이스 구현용 ───
        private Transform _playerTransform;
        private bool _isInChase;
        private bool _hasGroundBounds;
        private GroundBounds _groundBounds;

        public float PostChaseSuspicion => postChaseSuspicion;
        public float SuspicionAutoRate => suspicionAutoRate;
        public float SuspicionMoveBonus => suspicionMoveBonus;
        public float MoveThreshold => moveThreshold;

        public void OnActivate(Transform bossTransform)
        {
            _isInChase = false;
            _hasGroundBounds = false;
        }

        public void OnDeactivate()
        {
            _isInChase = false;
        }

        // Patrol 없음 (Chase-only)
        public void OnPatrolEnter() { }
        public void OnPatrolUpdate(float deltaTime) { }
        public void OnPatrolExit() { }

        public void OnChaseEnter()
        {
            _isInChase = true;
        }

        public void OnChaseUpdate(float deltaTime)
        {
            // Chase 중 의심도 증가: 자동 + zone + 이동
            if (_playerTransform == null || !_hasGroundBounds) return;

            // 항상 자동 증가
            OnIncreaseSuspicion?.Invoke(suspicionAutoRate, deltaTime);

            // Zone 내면 추가 증가
            if (IsPlayerInZone())
                OnIncreaseSuspicion?.Invoke(suspicionAutoRate * 0.5f, deltaTime);

            // Player 이동 시 추가 증가
            var movement = _playerTransform.GetComponent<HideAndInk.Player.PlayerMovementAdapter>();
            if (movement != null && movement.CurrentVelocity.sqrMagnitude >= moveThreshold * moveThreshold)
                OnIncreaseSuspicion?.Invoke(suspicionMoveBonus, deltaTime);
        }

        public void OnChaseExit()
        {
            _isInChase = false;
        }

        public void OnSearchEnter() { }
        public void OnSearchUpdate(float deltaTime) { }
        public void OnSearchExit() { }

        // 항상 Player 추격
        public bool HasMovementOverride => true;

        public Vector3? GetPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            if (!_hasGroundBounds)
            {
                _groundBounds = bounds;
                _hasGroundBounds = true;
            }
            if (_playerTransform != null)
                return _playerTransform.position;
            return null;
        }

        public Vector3? GetSearchTarget(Vector3 currentPos, Vector3 lastKnownPos, GroundBounds bounds)
        {
            return null; // Search 없음
        }

        public void SetGroundBounds(GroundBounds bounds)
        {
            _groundBounds = bounds;
            _hasGroundBounds = true;
        }

        private bool IsPlayerInZone()
        {
            if (_playerTransform == null || !_hasGroundBounds) return false;
            Vector3 p = _playerTransform.position;
            return p.x >= _groundBounds.MinX && p.x <= _groundBounds.MaxX
                && p.z >= _groundBounds.MinZ && p.z <= _groundBounds.MaxZ;
        }

        // ─── 인터페이스 구현 ───
        void IGimmickPlayerAware.SetPlayerTransform(Transform playerTransform) { _playerTransform = playerTransform; }
        void IGimmickPlayerAware.SetSuspicionLevel(float normalizedSuspicion) { }
        void IGimmickPlayerAware.SetCamouflageState(bool isCamouflaging) { }
        void IGimmickPlayerAware.SetPlayerVisible(bool isVisible) { }

        bool IGimmickViewDirection.OverridesViewDirection => false;
        Vector3 IGimmickViewDirection.GetViewDirectionVector() => Vector3.right;
        bool IGimmickViewDirection.ShowChargeIndicator => false;

        bool IGimmickCombatCycle.IsInCombatCycle => _isInChase;
        bool IGimmickCombatCycle.IsCharging => _isInChase;

        bool IGimmickTransitionOverride.ShouldSkipSearchOnLostPlayer(float normalizedSuspicion) => true;
    }
}
