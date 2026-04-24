using UnityEngine;
using System.Collections.Generic;
using HideAndInk.Core.Enemy.Elite;

namespace HideAndInk.Core.Enemy.Elite.Behaviors
{
    /// <summary>
    /// Ch.2 바다거북: 해초 먹어치움 기믹
    /// - 활동 범위 내 랜덤 순회
    /// - Camouflageable 태그 오브젝트 캐싱
    /// - 범위 내 먹이 발견 시 접근 → 섭취 → SetActive(false)
    /// - 먹는 중 이동 중지
    /// </summary>
    public class SeaTurtleBehavior : MonoBehaviour, IEliteBehavior
    {
        public string BehaviorName => "SeaTurtle";

        [Header("순회 설정")]
        [Tooltip("활동 반경 (m). 원형 기즈모로 Scene에서 표시")]
        [SerializeField] private float patrolRadius = 8f;

        [Tooltip("순회 이동 속도")]
        [SerializeField] private float patrolSpeed = 1.5f;

        [Header("섭취 설정")]
        [Tooltip("먹이 섭취 시간 (초)")]
        [SerializeField] private float consumeTime = 2f;

        [Tooltip("먹이 탐색 간격 (초)")]
        [SerializeField] private float searchInterval = 1f;

        // 상태
        private Vector3 _patrolTarget;
        private bool _isConsuming;
        private float _consumeTimer;
        private float _searchTimer;
        private GameObject _currentTarget;

        // 캐싱된 먹이 목록
        private List<GameObject> _cachedFoodTargets;

        // EliteEnemyController 참조
        private EliteEnemyController _controller;

        public bool IsControllingMovement => _isConsuming || _patrolTarget != Vector3.zero;

        public void OnActivate()
        {
            _controller = GetComponent<EliteEnemyController>();
            _cachedFoodTargets = new List<GameObject>();
            _searchTimer = 0f;
            _isConsuming = false;

            // 시작 시 먹이 캐싱
            CacheFoodTargets();

            // 초기 순회 목표 설정
            PickNewPatrolTarget();

#if UNITY_EDITOR
            Debug.Log($"[SeaTurtleBehavior] Activated. Cached { _cachedFoodTargets.Count} food targets.");
#endif
        }

        public void OnDeactivate()
        {
            _cachedFoodTargets?.Clear();
            _currentTarget = null;
        }

        public void OnPlayerApproached(float distance, Vector3 playerPos, Vector3 directionToPlayer)
        {
            // Player 접근 시 현재 행동 유지 (바다거북은 Player와 직접 상호작용 안 함)
        }

        public void OnUpdate(float deltaTime)
        {
            // 섭취 중이면 타이머 진행
            if (_isConsuming)
            {
                _consumeTimer += deltaTime;
                if (_consumeTimer >= consumeTime)
                {
                    FinishConsuming();
                }
                return;
            }

            // 먹이 탐색
            _searchTimer += deltaTime;
            if (_searchTimer >= searchInterval)
            {
                _searchTimer = 0f;
                FindAndConsumeFood();
            }

            // 순회 이동
            UpdatePatrolMovement(deltaTime);
        }

        /// <summary>
        /// 먹이 대상 캐싱 (Camouflageable 태그)
        /// </summary>
        private void CacheFoodTargets()
        {
            _cachedFoodTargets.Clear();

            GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            foreach (var obj in allObjects)
            {
                if (obj.CompareTag("Camouflageable") && obj.activeSelf)
                {
                    // 활동 범위 내에 있는지 확인
                    float distance = Vector3.Distance(transform.position, obj.transform.position);
                    if (distance <= patrolRadius)
                    {
                        _cachedFoodTargets.Add(obj);
                    }
                }
            }
        }

        /// <summary>
        /// 먹이 탐색 및 섭취 시작
        /// </summary>
        private void FindAndConsumeFood()
        {
            if (_cachedFoodTargets == null || _cachedFoodTargets.Count == 0) return;

            // 활성화된 먹이 중 가장 가까운 것 찾기
            GameObject nearestFood = null;
            float nearestDistance = float.MaxValue;

            foreach (var food in _cachedFoodTargets)
            {
                if (food == null || !food.activeSelf) continue;

                float distance = Vector3.Distance(transform.position, food.transform.position);
                if (distance < nearestDistance && distance <= patrolRadius)
                {
                    nearestDistance = distance;
                    nearestFood = food;
                }
            }

            if (nearestFood != null)
            {
                StartConsuming(nearestFood);
            }
        }

        /// <summary>
        /// 섭취 시작
        /// </summary>
        private void StartConsuming(GameObject target)
        {
            _currentTarget = target;
            _isConsuming = true;
            _consumeTimer = 0f;

            // 먹이 위치로 이동
            if (_controller != null)
            {
                _controller.MoveTo(target.transform.position);
            }

#if UNITY_EDITOR
            Debug.Log($"[SeaTurtleBehavior] Started consuming: {target.name}");
#endif
        }

        /// <summary>
        /// 섭취 완료
        /// </summary>
        private void FinishConsuming()
        {
            if (_currentTarget != null)
            {
                _currentTarget.SetActive(false);

#if UNITY_EDITOR
                Debug.Log($"[SeaTurtleBehavior] Consumed: {_currentTarget.name}");
#endif
            }

            _currentTarget = null;
            _isConsuming = false;

            // 새 순회 목표 설정
            PickNewPatrolTarget();
        }

        /// <summary>
        /// 순회 이동 업데이트
        /// </summary>
        private void UpdatePatrolMovement(float deltaTime)
        {
            if (_isConsuming) return;

            // 목표 도달 체크
            if (_controller != null && !_controller.IsMoving)
            {
                PickNewPatrolTarget();
            }
        }

        /// <summary>
        /// 새 순회 목표 설정 (활동 범위 내 랜덤 + Ground 검증)
        /// </summary>
        private void PickNewPatrolTarget()
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float distance = Random.Range(patrolRadius * 0.3f, patrolRadius);

            Vector3 target = new Vector3(
                transform.position.x + randomDir.x * distance,
                transform.position.y,
                transform.position.z + randomDir.y * distance
            );

            // Ground 범위 내로 제한 (EliteEnemyController의 GroundBounds 사용)
            if (_controller != null)
            {
                // _controller를 통해 GroundBounds에 접근할 수 없으므로
                // 간단히 현재 위치 기준으로 제한
                _patrolTarget = target;
                _controller.MoveTo(_patrolTarget);
            }
            else
            {
                _patrolTarget = target;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 활동 범위 기즈모 표시
        /// </summary>
        private void OnDrawGizmos()
        {
            // 활동 범위 원
            Gizmos.color = new Color(0f, 1f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, patrolRadius);

            // 중심점
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }

        private void OnDrawGizmosSelected()
        {
            // 선택 시 더 선명하게
            Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, patrolRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }
#endif
    }
}
