using UnityEngine;
using HideAndInk.Core.Enemy.Interfaces;

namespace HideAndInk.Core.Enemy.Boss.Gimmicks
{
    /// <summary>
    /// Ch.3 전기뱀장어 감전 구역 생성 기믹 (ScriptableObject)
    /// 이동 중 일정 확률로 감전 구역 설치
    /// Player가 닿으면 3초간 행동 불가 (함정 시스템)
    /// </summary>
    [CreateAssetMenu(menuName = "Enemy Gimmicks/Electric Zone Gimmick", fileName = "ElectricZoneGimmick")]
    public sealed class ElectricZoneGimmick : ScriptableObject, IEnemyGimmick
    {
        public GimmickType Type => GimmickType.ElectricZone;

        [Header("감전 구역 설정")]
        [Tooltip("감전 구역 지속 시간 (초)")]
        [SerializeField] private float zoneDuration = 5f;
        [Tooltip("설치 쿨타임 (초)")]
        [SerializeField] private float installCooldown = 8f;
        [Tooltip("설치 확률 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] private float installProbability = 0.3f;
        [Tooltip("설치 시도 체크 간격 (초)")]
        [SerializeField] private float checkInterval = 1f;

        [Header("프리팹")]
        [Tooltip("감전 구역 프리팹 (ElectricZone 컴포넌트 포함)")]
        [SerializeField] private GameObject electricZonePrefab;

        // 상태
        private Transform _bossTransform;
        private float _cooldownTimer;
        private float _checkTimer;
        private bool _isChasing;

        // 외부 연동 콜백
        public System.Action<GameObject> OnZoneCreated;
        public System.Action<Vector3> OnPlayerTrapped;

        public void OnActivate(Transform bossTransform)
        {
            _bossTransform = bossTransform;
            _cooldownTimer = 0f;
            _checkTimer = 0f;
            _isChasing = false;
        }

        public void OnDeactivate() { }

        #region Patrol

        public void OnPatrolEnter() => _isChasing = false;

        public void OnPatrolUpdate(float deltaTime) => TryInstallZone(deltaTime);

        public void OnPatrolExit() { }

        #endregion

        #region Chase

        public void OnChaseEnter() => _isChasing = true;

        public void OnChaseUpdate(float deltaTime) => TryInstallZone(deltaTime, probabilityMultiplier: 2f);

        public void OnChaseExit() => _isChasing = false;

        #endregion

        #region Search

        public void OnSearchEnter() => _isChasing = false;

        public void OnSearchUpdate(float deltaTime) => TryInstallZone(deltaTime);

        public void OnSearchExit() { }

        #endregion

        private void TryInstallZone(float deltaTime, float probabilityMultiplier = 1f)
        {
            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= deltaTime;
                return;
            }

            _checkTimer -= deltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = checkInterval;

            float roll = Random.value;
            float threshold = installProbability * probabilityMultiplier;

            if (roll <= threshold)
            {
                InstallZone();
                _cooldownTimer = installCooldown;
            }
        }

        private void InstallZone()
        {
            if (electricZonePrefab == null)
            {
                Debug.LogWarning("[ElectricZoneGimmick] ElectricZonePrefab이 설정되지 않았습니다.", this);
                return;
            }

            Vector3 spawnPos = _bossTransform.position;
            spawnPos.y = 0f;

            GameObject zone = GameObject.Instantiate(electricZonePrefab, spawnPos, Quaternion.identity);
            zone.name = "ElectricZone";

            var zoneComponent = zone.GetComponent<ElectricZone>();
            if (zoneComponent != null)
            {
                zoneComponent.Initialize(zoneDuration, OnPlayerTrapped);
            }

            OnZoneCreated?.Invoke(zone);
            Debug.Log("[ElectricZoneGimmick] 감전 구역 설치!", this);
        }

        public void OnPlayerEnterZone(Vector3 playerPosition)
        {
            OnPlayerTrapped?.Invoke(playerPosition);
            Debug.Log("[ElectricZoneGimmick] Player 감전! 3초간 행동 불가", this);
        }

        #region Movement Override (IEnemyGimmick 확장)

        /// <summary>
        /// ElectricZoneGimmick은 이동 제어권을 가짐 (X-Z 순찰 + 구역 설치 위치 이동)
        /// </summary>
        public bool HasMovementOverride => true;

        /// <summary>
        /// Patrol 상태 이동 목표: X-Z 평면 순찰 (Z ±1m 제한)
        /// </summary>
        public Vector3? GetPatrolTarget(Vector3 currentPos, GroundBounds bounds)
        {
            // 현재 위치 기준 X-Z 랜덤 이동 (Z ±1m 제한)
            float xDistance = Random.Range(5f, 12f);
            float xDir = Random.value > 0.5f ? 1f : -1f;
            float zOffset = Random.Range(-1f, 1f); // Z ±1m 제한

            Vector3 target = new Vector3(
                currentPos.x + xDir * xDistance,
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

        /// <summary>
        /// Search 상태 이동 목표: 마지막 Player 위치 주변 수색 (Z ±1m 제한)
        /// </summary>
        public Vector3? GetSearchTarget(Vector3 currentPos, Vector3 lastKnownPos, GroundBounds bounds)
        {
            float searchRadius = 3f;
            float angle = Random.Range(0f, 360f);
            float distance = Random.Range(1f, searchRadius);
            float zOffset = Mathf.Clamp(Mathf.Sin(angle * Mathf.Deg2Rad) * distance, -1f, 1f);

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
    /// 감전 구역 컴포넌트 (프리팹에 부착)
    /// Player가 닿으면 즉시 사라짐 + Player 3초간 행동 불가
    /// </summary>
    public class ElectricZone : MonoBehaviour
    {
        private float _duration;
        private System.Action<Vector3> _onPlayerTrapped;
        private float _timer;

        public void Initialize(float duration, System.Action<Vector3> onPlayerTrapped)
        {
            _duration = duration;
            _onPlayerTrapped = onPlayerTrapped;
            _timer = duration;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _onPlayerTrapped?.Invoke(other.transform.position);
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                _onPlayerTrapped?.Invoke(other.transform.position);
                Destroy(gameObject);
            }
        }
    }
}
