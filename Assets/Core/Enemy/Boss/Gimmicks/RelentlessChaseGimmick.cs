using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// Zone 3 곰치 (Moray Eel) — 오케스트레이터
    /// 
    /// Patrol: 맵 배회 + 의심도 자동 상승
    /// Chase:  돌진 시퀀스
    /// 실제 돌진 로직은 MorayChargeDirector가 처리.
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy Gimmicks/Relentless Chase Gimmick", fileName = "RelentlessChaseGimmick")]
    public sealed class RelentlessChaseGimmick : ScriptableObject, IEnemyGimmick,
        IGimmickPlayerAware, IGimmickViewDirection, IGimmickCombatCycle, IGimmickTransitionOverride
    {
        public GimmickType Type => GimmickType.RelentlessChase;

        [Header("의심도")]
        [SerializeField, Tooltip("자동 의심도 상승률")] private float suspicionAutoRate = 8f;      // 초당 자동 증가
        [SerializeField, Tooltip("이동 시 의심도 추가 상승량")] private float suspicionMoveBonus = 12f;    // Player 이동 시 추가 증가
        [SerializeField, Tooltip("이동 감지 임계값")] private float moveThreshold = 1f;          // 이동 감지 임계 속도
        [SerializeField, Tooltip("추적 종료 후 의심도")] private float postChaseSuspicion = 0f;     // 돌진 후 리셋값

        [Header("Patrol 서성임")]
        [SerializeField, Tooltip("Player 주변 서성임 반경")]
        private float patrolStalkRadius = 8f;

        [Header("돌진")]
        [SerializeField, Tooltip("사이클당 최대 돌진 횟수")] private int maxChargesPerCycle = 5;
        public int MaxChargesPerCycle => maxChargesPerCycle;

        // ─── 콜백 (Controller 연결) ───
        public System.Action<int> OnDirectorBeginPrepare; // chargeCount → Director.BeginPrepare
        public System.Action OnDirectorReset;              // Chase 종료 → Director.ResetCharges
        public System.Action<float, float> OnIncreaseSuspicion;

        // ─── 인터페이스 구현용 ───
        private Transform _bossTransform;
        private Transform _playerTransform;
        private bool _isInChase;

        // Patrol stalk target
        private Vector3 _stalkTarget;
        private float _lastStalkPickTime;

        // Patrol 중 의심도가 100%가 되는 시점이 Chase 진입 시점
        // Controller의 suspicionSystem.OnDetected가 Chase 전환 처리

        public float PostChaseSuspicion => postChaseSuspicion;
        public float SuspicionAutoRate => suspicionAutoRate;
        public float SuspicionMoveBonus => suspicionMoveBonus;
        public float MoveThreshold => moveThreshold;

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _isInChase = false;
        }

        public void OnDeactivate()
        {
            _isInChase = false;
        }

        // ─── Patrol: 배회 + 의심도 자동 상승 ───
        public void OnPatrolEnter()
        {
            _isInChase = false;
        }

        public void OnPatrolUpdate(float deltaTime)
        {
            if (_playerTransform == null || _bossTransform == null) return;

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

        public void OnPatrolExit() { }

        // ─── Chase: 돌진 시퀀스 ───
        public void OnChaseEnter()
        {
            _isInChase = true;

            // Director에게 돌진 준비 요청
            // 실제 chargeCount는 Controller의 OnDirectorBeginPrepare 콜백이 결정
            int chargeCount = 0; // Controller가 재정의
            OnDirectorBeginPrepare?.Invoke(chargeCount);
        }

        public void OnChaseUpdate(float deltaTime)
        {
            // Chase 중 director가 모든 로직 처리
        }

        public void OnChaseExit()
        {
            _isInChase = false;
            OnDirectorReset?.Invoke();
        }

        public void OnSearchEnter() { }
        public void OnSearchUpdate(float deltaTime) { }
        public void OnSearchExit() { }

        // 항상 true: GetPatrolTarget으로 PatrolBehavior 제어
        public bool HasMovementOverride => true;

        public Vector3? GetPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            if (_playerTransform == null) return null;

            // Player 근처 랜덤 위치로 서성임 (일정 시간마다 새 목표)
            if (Time.time - _lastStalkPickTime > Random.Range(2f, 5f))
            {
                Vector2 offset = Random.insideUnitCircle * patrolStalkRadius;
                _stalkTarget = _playerTransform.position + new Vector3(offset.x, 0f, offset.y);
                _stalkTarget.y = currentPos.y;
                _lastStalkPickTime = Time.time;
            }
            return _stalkTarget;
        }

        public Vector3? GetSearchTarget(Vector3 currentPos, Vector3 lastKnownPos, GroundBounds bounds)
        {
            return null; // Search 없음
        }

        public void SetGroundBounds(GroundBounds bounds)
        {
            // 더 이상 사용하지 않음 (Player 중심 동적 범위로 대체)
        }

        /// <summary>
        /// Player가 보스 근처에 있는지 확인 (거리 기반)
        /// </summary>
        private bool IsPlayerInZone()
        {
            if (_playerTransform == null || _bossTransform == null) return false;
            float dist = Vector3.Distance(
                new Vector3(_playerTransform.position.x, 0f, _playerTransform.position.z),
                new Vector3(_bossTransform.position.x, 0f, _bossTransform.position.z)
            );
            // patrolStalkRadius(8m)의 2배 거리 이내 = 근처
            return dist <= patrolStalkRadius * 2f;
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
