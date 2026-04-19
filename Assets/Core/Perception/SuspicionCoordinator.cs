using UnityEngine;
using System.Collections.Generic;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 적들 간 의심도/경계 상태 정보 공유 관리자
    /// 한 적이 플레이어를 발견하면 근처 적들도 경계/추적 모드로 전환
    /// </summary>
    public sealed class SuspicionCoordinator : MonoBehaviour
    {
        private static SuspicionCoordinator _instance;
        public static SuspicionCoordinator Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<SuspicionCoordinator>();
                    if (_instance == null)
                    {
                        var go = new GameObject("[SuspicionCoordinator]");
                        _instance = go.AddComponent<SuspicionCoordinator>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [Header("정보 공유 설정")]
        [SerializeField] private float alertBroadcastRadius = 10f;  // 경계 정보를 공유하는 반경
        [SerializeField] private float sharedSuspicionAmount = 0.3f;  // 공유되는 의심도 양 (0~1)
        [SerializeField] private float sharedSuspicionCooldown = 2f;  //同一个 적에게 정보 공유하는 간격

        // 등록된 적들
        private List<VisionBasedSuspicionManager> _registeredEnemies = new List<VisionBasedSuspicionManager>();

        // Cooldown 관리 (같은 적에게 반복 공유 방지)
        private Dictionary<GameObject, float> _lastBroadcastTime = new Dictionary<GameObject, float>();

        // 외부에서 참조할 때 사용할 프로퍼티
        public float AlertBroadcastRadius => alertBroadcastRadius;
        public float SharedSuspicionAmount => sharedSuspicionAmount;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// 적을 등록 (VisionBasedSuspicionManager가 시작 시 호출)
        /// </summary>
        public void RegisterEnemy(VisionBasedSuspicionManager enemy)
        {
            if (!_registeredEnemies.Contains(enemy))
            {
                _registeredEnemies.Add(enemy);
                LogModule.Instance.Log($"Enemy registered: {enemy.name}, total: {_registeredEnemies.Count}", "INFO");
            }
        }

        /// <summary>
        /// 적을 등록 해제 (적이 파괴될 때 호출)
        /// </summary>
        public void UnregisterEnemy(VisionBasedSuspicionManager enemy)
        {
            if (_registeredEnemies.Contains(enemy))
            {
                _registeredEnemies.Remove(enemy);
                LogModule.Instance.Log($"Enemy unregistered: {enemy.name}, remaining: {_registeredEnemies.Count}", "INFO");
            }
        }

        /// <summary>
        /// 경고 정보 브로드캐스트 (한 적이 Alert 상태일 때 호출)
        /// </summary>
        public void BroadcastAlert(VisionBasedSuspicionManager sourceEnemy, Vector3 alertPosition, float alertIntensity)
        {
            if (sourceEnemy == null) return;

            // 동일한 적에게 과도하게 공유하지 않도록 cooldown 체크
            float currentTime = Time.time;
            if (_lastBroadcastTime.TryGetValue(sourceEnemy.gameObject, out float lastTime))
            {
                if (currentTime - lastTime < sharedSuspicionCooldown)
                    return;
            }
            _lastBroadcastTime[sourceEnemy.gameObject] = currentTime;

            LogModule.Instance.Log($"Broadcasting alert from {sourceEnemy.name}, position={alertPosition}, intensity={alertIntensity}", "INFO");

            foreach (var enemy in _registeredEnemies)
            {
                if (enemy == null || enemy == sourceEnemy) continue;

                float distance = Vector3.Distance(enemy.transform.position, sourceEnemy.transform.position);

                // 공유 범위 내의 적에게만 정보 전달
                if (distance <= alertBroadcastRadius)
                {
                    // 거리 기반 공유 강도 (멀수록 약하게)
                    float distanceFactor = 1f - (distance / alertBroadcastRadius);
                    float sharedIntensity = sharedSuspicionAmount * distanceFactor * alertIntensity;

                    // 해당 적의 의심도 상승
                    enemy.ReceiveSharedAlert(alertPosition, sharedIntensity);

                    LogModule.Instance.Log($"Alert shared to {enemy.name}, distance={distance:F1}, intensity={sharedIntensity:F2}", "INFO");
                }
            }
        }

        /// <summary>
        /// 공유된 경고 정보를 수신 (VisionBasedSuspicionManager가 호출)
        /// </summary>
        public void ReceiveSharedAlert(VisionBasedSuspicionManager targetEnemy, Vector3 alertPosition, float intensity)
        {
            // 이건 VisionBasedSuspicionManager가 직접 처리하지만,
            // 여기서 추가적인 로직 (예: 사운드 기반 알림 등) 을 넣을 수 있음
            LogModule.Instance.Log($"{targetEnemy.name} received shared alert at {alertPosition}, intensity={intensity}", "INFO");
        }
    }
}
