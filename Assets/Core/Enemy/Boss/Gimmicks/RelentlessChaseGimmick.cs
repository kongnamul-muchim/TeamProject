using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// Ch.2 곰치 집요한 추격 기믹 (ScriptableObject)
    /// 의태 감지 시 좁은 구역 집중 순찰
    /// Chase 상태 의심도 하락률 감소
    /// Search 수색 반경 확대
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy Gimmicks/Relentless Chase Gimmick", fileName = "RelentlessChaseGimmick")]
    public sealed class RelentlessChaseGimmick : ScriptableObject, IEnemyGimmick
    {
        public GimmickType Type => GimmickType.RelentlessChase;

        [Header("집요한 추격 설정")]
        [Tooltip("의심도 하락 배율 (1보다 작으면 느리게 하락)")]
        [Range(0.1f, 1f)]
        [SerializeField] private float suspicionDecayMultiplier = 0.3f;
        [Tooltip("수색 반경 배율")]
        [Range(1f, 3f)]
        [SerializeField] private float searchRadiusMultiplier = 1.25f;
        [Tooltip("집중 순찰 반경 (m)")]
        [SerializeField] private float patrolAreaRadius = 8f;

        // 상태
        private Transform _bossTransform;
        private Vector3 _lastCamouflagePosition;
        private bool _hasCamouflageTarget;
        private bool _isInFocusedPatrol;
        private float _originalSpeed;

        // 외부 연동 콜백
        public System.Action<float> OnSuspicionDecayRateOverride;
        public System.Action<float> OnSearchRadiusOverride;
        public System.Action<Vector3, float> OnPatrolAreaOverride;
        public System.Action<float> OnSpeedOverride;

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _hasCamouflageTarget = false;
            _isInFocusedPatrol = false;
        }

        public void OnDeactivate()
        {
            _isInFocusedPatrol = false;
            OnPatrolAreaOverride?.Invoke(Vector3.zero, 0f);
        }

        #region Patrol

        public void OnPatrolEnter()
        {
            if (_hasCamouflageTarget)
            {
                _isInFocusedPatrol = true;
                OnPatrolAreaOverride?.Invoke(_lastCamouflagePosition, patrolAreaRadius);
                Debug.Log("[RelentlessChaseGimmick] 집중 순찰 시작");
            }
            else
            {
                _isInFocusedPatrol = false;
                OnPatrolAreaOverride?.Invoke(Vector3.zero, 0f);
            }
        }

        public void OnPatrolUpdate(float deltaTime) { }

        public void OnPatrolExit() { }

        #endregion

        #region Chase

        public void OnChaseEnter()
        {
            OnSuspicionDecayRateOverride?.Invoke(suspicionDecayMultiplier);
            Debug.Log("[RelentlessChaseGimmick] 집요한 추격 시작 - 의심도 하락률 감소");
        }

        public void OnChaseUpdate(float deltaTime) { }

        public void OnChaseExit()
        {
            OnSuspicionDecayRateOverride?.Invoke(1f);
        }

        #endregion

        #region Search

        public void OnSearchEnter()
        {
            OnSearchRadiusOverride?.Invoke(searchRadiusMultiplier);
            if (_hasCamouflageTarget)
            {
                OnPatrolAreaOverride?.Invoke(_lastCamouflagePosition, patrolAreaRadius * searchRadiusMultiplier);
            }
            Debug.Log("[RelentlessChaseGimmick] 수색 반경 확대: " + searchRadiusMultiplier + "x");
        }

        public void OnSearchUpdate(float deltaTime) { }

        public void OnSearchExit()
        {
            OnSearchRadiusOverride?.Invoke(1f);
        }

        #endregion

        public void RecordCamouflagePosition(Vector3 position)
        {
            _lastCamouflagePosition = position;
            _hasCamouflageTarget = true;
        }

        public bool IsInFocusedPatrol => _isInFocusedPatrol;
        public bool HasCamouflageTarget => _hasCamouflageTarget;
        public void SetOriginalSpeed(float speed) => _originalSpeed = speed;
    }
}
