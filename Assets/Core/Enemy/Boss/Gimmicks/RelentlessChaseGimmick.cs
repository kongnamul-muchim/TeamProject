using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Enemy.Boss.Gimmicks;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// Ch.2 곰치: 집요한 추격 기믹
    /// - 의심도 하락률 감소 (집요함 표현)
    /// - 수색 반경 확대
    /// - 집중 순찰 영역 (중심점 + 반경)
    /// </summary>
    [CreateAssetMenu(fileName = "RelentlessChaseGimmick", menuName = "HideAndInk/Enemy/Gimmicks/RelentlessChase")]
    public class RelentlessChaseGimmick : ScriptableObject, IEnemyGimmick
    {
        public GimmickType Type => GimmickType.RelentlessChase;

        [Header("의심도 설정")]
        [Tooltip("Chase 중 의심도 하락 배율 (1=기본, 0.3=30% 속도로 하락)")]
        [SerializeField] [Range(0f, 1f)] private float suspicionDecayMultiplier = 0.3f;

        [Header("순찰 설정")]
        [Tooltip("집중 순찰 중심점 (로컬 좌표)")]
        [SerializeField] private Vector3 patrolCenter = Vector3.zero;

        [Tooltip("집중 순찰 반경 (m)")]
        [SerializeField] private float patrolRadius = 5f;

        [Header("수색 설정")]
        [Tooltip("수색 반경 배율 (1=기본, 2=2배)")]
        [SerializeField] [Range(1f, 3f)] private float searchRadiusMultiplier = 1.5f;

        [Header("속도 설정")]
        [Tooltip("기본 순찰 속도")]
        [SerializeField] private float originalSpeed = 2f;

        // 콜백 (BossEnemyController에서 설정)
        public System.Action<float> OnSuspicionDecayRateOverride;
        public System.Action<float> OnSearchRadiusOverride;
        public System.Action<Vector3, float> OnPatrolAreaOverride;

        // 내부 상태
        private Transform _bossTransform;
        private bool _isActivated;

        public bool HasMovementOverride => true;

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _isActivated = true;

#if UNITY_EDITOR
            Debug.Log($"[RelentlessChaseGimmick] Activated on {bossTransform.name}");
#endif
        }

        public void OnDeactivate()
        {
            _isActivated = false;
            _bossTransform = null;
        }

        public void OnPatrolEnter()
        {
            // 집중 순찰 영역 설정
            if (_bossTransform != null)
            {
                Vector3 worldCenter = _bossTransform.position + patrolCenter;
                OnPatrolAreaOverride?.Invoke(worldCenter, patrolRadius);
            }
        }

        public void OnPatrolUpdate(float deltaTime)
        {
            // 집요한 순찰: 좁은 영역에서 반복 순회
        }

        public void OnPatrolExit()
        {
        }

        public void OnChaseEnter()
        {
            // 의심도 하락률 감소
            OnSuspicionDecayRateOverride?.Invoke(suspicionDecayMultiplier);

#if UNITY_EDITOR
            Debug.Log($"[RelentlessChaseGimmick] Chase started. Suspicion decay multiplier: {suspicionDecayMultiplier}");
#endif
        }

        public void OnChaseUpdate(float deltaTime)
        {
            // 집요한 추적: Player와 거리 유지하며 따라감
        }

        public void OnChaseExit()
        {
            // 의심도 하락률 복원은 BossEnemyController에서 처리
        }

        public void OnSearchEnter()
        {
            // 수색 반경 확대
            OnSearchRadiusOverride?.Invoke(searchRadiusMultiplier);

#if UNITY_EDITOR
            Debug.Log($"[RelentlessChaseGimmick] Search started. Search radius multiplier: {searchRadiusMultiplier}");
#endif
        }

        public void OnSearchUpdate(float deltaTime)
        {
            // 집요한 수색: 넓은 반경으로 탐색
        }

        public void OnSearchExit()
        {
        }

        /// <summary>
        /// Patrol 상태 이동 목표 계산 (집중 순찰 영역 내)
        /// </summary>
        public Vector3? GetPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            if (_bossTransform == null) return null;

            // 집중 순찰 중심점 (월드 좌표)
            Vector3 center = _bossTransform.position + patrolCenter;

            // 중심점 기준 랜덤 방향 + 반경 내 거리
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float distance = Random.Range(patrolRadius * 0.3f, patrolRadius);

            Vector3 target = new Vector3(
                center.x + randomDir.x * distance,
                currentPos.y,
                center.z + randomDir.y * distance
            );

            // Ground 범위 내로 제한
            return bounds.ClampXZ(target);
        }

        /// <summary>
        /// Search 상태 이동 목표 계산 (수색 반경 확대 적용)
        /// </summary>
        public Vector3? GetSearchTarget(Vector3 currentPos, Vector3 lastKnownPos, GroundBounds bounds)
        {
            // 마지막 발견 위치 기준 확대된 반경 내 랜덤 탐색
            float searchRadius = 3f * searchRadiusMultiplier;

            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float distance = Random.Range(0f, searchRadius);

            Vector3 target = new Vector3(
                lastKnownPos.x + randomDir.x * distance,
                currentPos.y,
                lastKnownPos.z + randomDir.y * distance
            );

            return bounds.ClampXZ(target);
        }

        /// <summary>
        /// 기본 속도 설정 (BossEnemyController에서 호출)
        /// </summary>
        public void SetOriginalSpeed(float speed)
        {
            originalSpeed = speed;
        }
    }
}
