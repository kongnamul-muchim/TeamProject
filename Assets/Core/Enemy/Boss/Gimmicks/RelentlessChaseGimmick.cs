using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// Zone 3 곰치 (Moray Eel) — 오케스트레이터
    /// 
    /// 역할: 의심도 관리 + Chase 사이클 카운팅
    /// 실제 돌진 로직은 MorayChargeDirector가 처리
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy Gimmicks/Relentless Chase Gimmick", fileName = "RelentlessChaseGimmick")]
    public sealed class RelentlessChaseGimmick : ScriptableObject, IEnemyGimmick,
        IGimmickPlayerAware, IGimmickViewDirection, IGimmickCombatCycle, IGimmickTransitionOverride
    {
        public GimmickType Type => GimmickType.RelentlessChase;

        [Header("의심도")]
        [SerializeField] private float suspicionIncreaseRate = 15f;
        [SerializeField] private float postChaseSuspicion = 30f;

        [Header("돌진")]
        [SerializeField] private int maxChargesPerCycle = 5;

        private Transform _bossTransform;
        private Transform _playerTransform;
        private GroundBounds _groundBounds;
        private bool _hasGroundBounds;
        private float _normalizedSuspicion;

        private int _chaseEntryCount;
        private bool _isInChase;
        private bool _hasTriggeredInitialChase;

        // ─── 콜백 (Controller 연결) ───
        public System.Action<int> OnDirectorBeginPrepare; // chargeCount
        public System.Action OnDirectorReset;
        public System.Action<float, float> OnIncreaseSuspicion;
        public System.Action OnForceInitialChase;
        public System.Action<float> OnPostChaseSuspicion; // postChaseSuspicion 값 전달

        public float PostChaseSuspicion => postChaseSuspicion;

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _chaseEntryCount = 0;
            _isInChase = false;
            _hasTriggeredInitialChase = false;
            _hasGroundBounds = false;
            _normalizedSuspicion = 0f;
        }

        public void OnDeactivate()
        {
            _isInChase = false;
            OnDirectorReset?.Invoke();
        }

        public void OnPatrolEnter()
        {
            _isInChase = false;
            if (!_hasTriggeredInitialChase)
            {
                _hasTriggeredInitialChase = true;
                OnForceInitialChase?.Invoke();
            }
        }

        public void OnPatrolUpdate(float deltaTime)
        {
            // 구역 내 Player → 의심도 상승
            if (_playerTransform == null || !_hasGroundBounds) return;
            if (IsPlayerInZone())
                OnIncreaseSuspicion?.Invoke(suspicionIncreaseRate, deltaTime);
        }

        public void OnPatrolExit() { }

        public void OnChaseEnter()
        {
            _chaseEntryCount++;
            _isInChase = true;

            // Director에게 돌진 준비 요청
            int chargeCount = Mathf.Min(_chaseEntryCount, maxChargesPerCycle);
            OnDirectorBeginPrepare?.Invoke(chargeCount);
        }

        public void OnChaseUpdate(float deltaTime)
        {
            // Chase 중 director가 모든 로직 처리
            // 여기서는 아무것도 안 함
        }

        public void OnChaseExit()
        {
            _isInChase = false;
            OnDirectorReset?.Invoke();
        }

        public void OnSearchEnter() { }
        public void OnSearchUpdate(float deltaTime) { }
        public void OnSearchExit() { }

        public bool HasMovementOverride => _isInChase;

        public Vector3? GetPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            if (!_hasGroundBounds)
            {
                _groundBounds = bounds;
                _hasGroundBounds = true;
            }
            return null; // 기본 PatrolBehavior 순찰 사용
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
        void IGimmickPlayerAware.SetSuspicionLevel(float normalizedSuspicion) { _normalizedSuspicion = normalizedSuspicion; }
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
