using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;
using HideAndInk.Core.Enemy.Movement;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// Ch.4 아귀 발광 미끼 기믹 (ScriptableObject)
    /// Patrol 중 맵에 랜덤으로 미끼 배치
    /// 10초마다 기존 미끼 제거 후 재생성
    /// Player가 미끼 접근 시 아귀가 위치 파악 → Chase 전환 (지연 시간 있음)
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy Gimmicks/Lure Bait Gimmick", fileName = "LureBaitGimmick")]
    public sealed class LureBaitGimmick : ScriptableObject, IEnemyGimmick
    {
        public GimmickType Type => GimmickType.LureBait;

        [Header("미끼 설정")]
        [Tooltip("동시 미끼 개수")]
        [SerializeField] private int baitCount = 5;
        [Tooltip("미끼 유지 시간 (초)")]
        [SerializeField] private float baitLifetime = 10f;
        [Tooltip("Chase 전환 지연 시간 (초)")]
        [SerializeField] private float chaseTransitionDelay = 2f;
        [Tooltip("미끼 감지 반경 (m)")]
        [SerializeField] private float baitTriggerRadius = 2f;

        [Header("프리팹")]
        [Tooltip("발광 미끼 프리팹 (LureBait 컴포넌트 포함)")]
        [SerializeField] private GameObject baitPrefab;

        // Ground 경계
        private GroundBounds _groundBounds;
        private bool _hasGroundBounds;

        // 상태
        private Transform _bossTransform;
        private GameObject[] _activeBaits;
        private float _refreshTimer;
        private bool _isChasePending;
        private float _chaseDelayTimer;
        private Vector3 _pendingPlayerPos;

        // 외부 연동 콜백
        public System.Action<GameObject> OnBaitCreated;
        public System.Action<GameObject> OnBaitDestroyed;
        public System.Action<Vector3> OnPlayerDetectedByBait;
        public System.Action OnChaseTriggered;

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _activeBaits = new GameObject[baitCount];
            _refreshTimer = 0f;
            _isChasePending = false;
        }

        public void OnDeactivate()
        {
            DestroyAllBaits();
        }

        #region Patrol

        public void OnPatrolEnter()
        {
            _refreshTimer = 0f;
            SpawnBaits();
        }

        public void OnPatrolUpdate(float deltaTime)
        {
            _refreshTimer -= deltaTime;
            if (_refreshTimer <= 0f)
            {
                DestroyAllBaits();
                SpawnBaits();
                _refreshTimer = baitLifetime;
            }
            CheckChaseTransition(deltaTime);
        }

        public void OnPatrolExit() { }

        #endregion

        #region Chase

        public void OnChaseEnter()
        {
            DestroyAllBaits();
            _isChasePending = false;
        }

        public void OnChaseUpdate(float deltaTime) { }

        public void OnChaseExit() { }

        #endregion

        #region Search

        public void OnSearchEnter() { }

        public void OnSearchUpdate(float deltaTime) { }

        public void OnSearchExit() { }

        #endregion

        public void SetGroundBounds(GroundBounds bounds)
        {
            _groundBounds = bounds;
            _hasGroundBounds = true;
        }

        private void SpawnBaits()
        {
            if (baitPrefab == null)
            {
                Debug.LogWarning("[LureBaitGimmick] BaitPrefab이 설정되지 않았습니다.", this);
                return;
            }

            for (int i = 0; i < baitCount; i++)
            {
                Vector3 spawnPos = GetRandomBaitPosition();
                GameObject bait = GameObject.Instantiate(baitPrefab, spawnPos, Quaternion.identity);
                bait.name = $"LureBait_{i}";

                var baitComponent = bait.GetComponent<LureBait>();
                if (baitComponent != null)
                {
                    baitComponent.Initialize(baitTriggerRadius, OnBaitTriggered);
                }

                _activeBaits[i] = bait;
                OnBaitCreated?.Invoke(bait);
            }

            Debug.Log($"[LureBaitGimmick] 미끼 {baitCount}개 배치", this);
        }

        private Vector3 GetRandomBaitPosition()
        {
            float x, z;
            if (_hasGroundBounds)
            {
                x = Random.Range(_groundBounds.MinX, _groundBounds.MaxX);
                z = Random.Range(_groundBounds.MinZ, _groundBounds.MaxZ);
            }
            else
            {
                x = _bossTransform.position.x + Random.Range(-20f, 20f);
                z = _bossTransform.position.z + Random.Range(-20f, 20f);
            }
            return new Vector3(x, 0f, z);
        }

        private void DestroyAllBaits()
        {
            for (int i = 0; i < _activeBaits.Length; i++)
            {
                if (_activeBaits[i] != null)
                {
                    OnBaitDestroyed?.Invoke(_activeBaits[i]);
                    GameObject.Destroy(_activeBaits[i]);
                    _activeBaits[i] = null;
                }
            }
        }

        private void OnBaitTriggered(Vector3 playerPosition)
        {
            _isChasePending = true;
            _chaseDelayTimer = chaseTransitionDelay;
            _pendingPlayerPos = playerPosition;
            OnPlayerDetectedByBait?.Invoke(playerPosition);
            Debug.Log($"[LureBaitGimmick] Player 감지! {chaseTransitionDelay}초 후 Chase 전환", this);
        }

        private void CheckChaseTransition(float deltaTime)
        {
            if (!_isChasePending) return;
            _chaseDelayTimer -= deltaTime;
            if (_chaseDelayTimer <= 0f)
            {
                _isChasePending = false;
                OnChaseTriggered?.Invoke();
                Debug.Log("[LureBaitGimmick] Chase 전환!", this);
            }
        }

        #region Movement Override (IEnemyGimmick 확장)

        /// <summary>
        /// LureBaitGimmick은 이동 제어권을 가짐 (미끼 순회 이동)
        /// </summary>
        public bool HasMovementOverride => true;

        /// <summary>
        /// Patrol 상태 이동 목표: 미끼 배치 위치 순회 (Z ±2m 제한)
        /// </summary>
        public Vector3? GetPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            // 활성 미끼 중 가장 가까운 미끼로 이동
            if (_activeBaits != null && _activeBaits.Length > 0)
            {
                GameObject nearestBait = null;
                float nearestDist = float.MaxValue;

                foreach (var bait in _activeBaits)
                {
                    if (bait != null)
                    {
                        float dist = Vector3.Distance(currentPos, bait.transform.position);
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            nearestBait = bait;
                        }
                    }
                }

                if (nearestBait != null)
                {
                    Vector3 target = nearestBait.transform.position;
                    target.y = currentPos.y;

                    // Z축 ±2m 제한 (현재 Z 기준)
                    target.z = Mathf.Clamp(target.z, currentPos.z - 2f, currentPos.z + 2f);

                    // Ground 범위 내로 제한
                    if (bounds.MinX != bounds.MaxX || bounds.MinZ != bounds.MaxZ)
                    {
                        target = bounds.ClampXZ(target);
                    }

                    return target;
                }
            }

            // 미끼가 없으면 현재 위치 기준 X-Z 랜덤 이동 (Z ±2m 제한)
            float xDistance = Random.Range(5f, 12f);
            float xDir = Random.value > 0.5f ? 1f : -1f;
            float zOffset = Random.Range(-2f, 2f);

            Vector3 fallbackTarget = new Vector3(
                currentPos.x + xDir * xDistance,
                currentPos.y,
                currentPos.z + zOffset
            );

            // Ground 범위 내로 제한
            if (bounds.MinX != bounds.MaxX || bounds.MinZ != bounds.MaxZ)
            {
                fallbackTarget = bounds.ClampXZ(fallbackTarget);
            }

            return fallbackTarget;
        }

        /// <summary>
        /// Search 상태 이동 목표: 미끼 감지 위치 수색 (Z ±2m 제한)
        /// </summary>
        public Vector3? GetSearchTarget(Vector3 currentPos, Vector3 lastKnownPos, GroundBounds bounds)
        {
            // Player가 감지된 위치 기준 수색
            float searchRadius = 3f;
            float angle = Random.Range(0f, 360f);
            float distance = Random.Range(1f, searchRadius);
            float zOffset = Mathf.Clamp(Mathf.Sin(angle * Mathf.Deg2Rad) * distance, -2f, 2f);

            Vector3 target = new Vector3(
                lastKnownPos.x + Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                currentPos.y,
                currentPos.z + zOffset
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

    /// <summary>
    /// 발광 미끼 컴포넌트 (프리팹에 부착)
    /// Player가 감지 반경 내에 들어오면 아귀에게 알림
    /// </summary>
    public class LureBait : MonoBehaviour
    {
        private float _triggerRadius;
        private System.Action<Vector3> _onTriggered;

        public void Initialize(float triggerRadius, System.Action<Vector3> onTriggered)
        {
            _triggerRadius = triggerRadius;
            _onTriggered = onTriggered;
        }

        private void Update()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance <= _triggerRadius)
                {
                    _onTriggered?.Invoke(player.transform.position);
                    Destroy(gameObject);
                }
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, _triggerRadius);
        }
    }
}
