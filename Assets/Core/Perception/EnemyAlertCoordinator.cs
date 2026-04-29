using System;
using System.Collections.Generic;
using UnityEngine;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 적 등록 관리 및 경보 브로드캐스트 전담 순수 C# 클래스
    /// SuspicionManager에서 적 관리 책임을 분리
    /// MonoBehaviour 의존성 없음 — 테스트 가능
    /// </summary>
    public sealed class EnemyAlertCoordinator
    {
        // === Config (생성자 주입) ===
        private readonly float _alertBroadcastRadius;
        private readonly float _sharedSuspicionAmount;
        private readonly float _sharedSuspicionCooldown;

        // === State ===
        private readonly List<EnemyPerception> _registeredEnemies = new();
        private readonly Dictionary<GameObject, float> _lastBroadcastTime = new();

        // ===== Events =====

        /// <summary>
        /// 경보 브로드캐스트 발생 시 외부 알림
        /// </summary>
        public event Action<EnemyPerception, Vector3, float> OnAlertBroadcast;

        // ===== Properties =====

        public float AlertBroadcastRadius => _alertBroadcastRadius;
        public float SharedSuspicionAmount => _sharedSuspicionAmount;

        /// <summary>
        /// 현재 등록된 EnemyPerception 목록 (읽기 전용 복사본)
        /// </summary>
        public IReadOnlyList<EnemyPerception> RegisteredEnemies => _registeredEnemies.AsReadOnly();

        // ===== Constructor =====

        public EnemyAlertCoordinator(
            float alertBroadcastRadius,
            float sharedSuspicionAmount,
            float sharedSuspicionCooldown)
        {
            _alertBroadcastRadius = alertBroadcastRadius;
            _sharedSuspicionAmount = sharedSuspicionAmount;
            _sharedSuspicionCooldown = sharedSuspicionCooldown;
        }

        // ===== Enemy Registration =====

        /// <summary>
        /// EnemyPerception 등록
        /// </summary>
        public void RegisterEnemy(EnemyPerception enemy)
        {
            if (enemy == null) return;
            if (!_registeredEnemies.Contains(enemy))
            {
                _registeredEnemies.Add(enemy);
            }
        }

        /// <summary>
        /// EnemyPerception 등록 해제
        /// </summary>
        public void UnregisterEnemy(EnemyPerception enemy)
        {
            _registeredEnemies.Remove(enemy);
        }

        // ===== Alert Broadcasting =====

        /// <summary>
        /// 경보 브로드캐스트 (발각 시 다른 적들에게 공유)
        /// cooldown 적용 및 거리 기반 감쇠 포함
        /// </summary>
        public void BroadcastAlert(EnemyPerception sourceEnemy, Vector3 alertPosition, float alertIntensity)
        {
            if (sourceEnemy == null) return;

            // Cooldown 체크
            float currentTime = Time.time;
            if (_lastBroadcastTime.TryGetValue(sourceEnemy.gameObject, out float lastTime))
            {
                if (currentTime - lastTime < _sharedSuspicionCooldown) return;
            }
            _lastBroadcastTime[sourceEnemy.gameObject] = currentTime;

            // 등록된 적들에게 거리 기반 Alert 공유
            foreach (var enemy in _registeredEnemies)
            {
                if (enemy == null || enemy == sourceEnemy) continue;

                float distance = Vector3.Distance(enemy.transform.position, sourceEnemy.transform.position);
                if (distance <= _alertBroadcastRadius)
                {
                    float distanceFactor = 1f - (distance / _alertBroadcastRadius);
                    float sharedIntensity = _sharedSuspicionAmount * distanceFactor * alertIntensity;
                    enemy.ReceiveSharedAlert(alertPosition, sharedIntensity);
                }
            }

            OnAlertBroadcast?.Invoke(sourceEnemy, alertPosition, alertIntensity);
        }

        // ===== Cleanup =====

        /// <summary>
        /// 모든 등록 정보 초기화 (씬 전환 등)
        /// </summary>
        public void Clear()
        {
            _registeredEnemies.Clear();
            _lastBroadcastTime.Clear();
        }
    }
}
