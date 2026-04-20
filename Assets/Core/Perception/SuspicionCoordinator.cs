using UnityEngine;
using System.Collections.Generic;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Utilities;
using HideAndInk.Core.Logging;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 적들 간 의심도/경계 상태 정보 공유 관리자
    /// 한 적이 플레이어를 발견하면 근처 적들도 경계/추적 모드로 전환
    /// </summary>
    public sealed class SuspicionCoordinator : Singleton<SuspicionCoordinator>
    {
        [Header("정보 공유 설정")]
        [SerializeField] private float alertBroadcastRadius = 10f;  // 경계 정보를 공유하는 반경
        [SerializeField] private float sharedSuspicionAmount = 0.3f;  // 공유되는 의심도 양 (0~1)
        [SerializeField] private float sharedSuspicionCooldown = 2f;  // 같은 적에게 정보 공유하는 간격

        // 등록된 적들
        private List<VisionBasedSuspicionManager> _registeredEnemies = new List<VisionBasedSuspicionManager>();

        // Cooldown 관리 (같은 적에게 반복 공유 방지)
        private Dictionary<GameObject, float> _lastBroadcastTime = new Dictionary<GameObject, float>();

        // 외부에서 참조할 때 사용할 프로퍼티
        public float AlertBroadcastRadius => alertBroadcastRadius;
        public float SharedSuspicionAmount => sharedSuspicionAmount;

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
                }
            }
        }

        /// <summary>
        /// 공유된 경고 정보를 수신 (VisionBasedSuspicionManager가 호출)
        /// </summary>
        public void ReceiveSharedAlert(VisionBasedSuspicionManager targetEnemy, Vector3 alertPosition, float intensity)
        {
            // VisionBasedSuspicionManager가 직접 처리
        }
    }
}
