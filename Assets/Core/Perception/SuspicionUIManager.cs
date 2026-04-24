using System;
using UnityEngine;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Utilities;

namespace HideAndInk.Core.Perception
{
    /// <summary>
    /// 보스 의심도 UI 중계 싱글톤
    /// BossSuspicionSystem의 자동 등록/해제를 관리하고 UI에 이벤트를 중계합니다.
    /// 보스 프리팹에 인스펙터 수동 연결 없이 자동으로 UI가 연결됩니다.
    /// </summary>
    public sealed class SuspicionUIManager : Singleton<SuspicionUIManager>
    {
        // 현재 활성화된 보스 의심도 시스템
        private BossSuspicionSystem _activeSystem;

        // 이벤트
        public event Action<SuspicionLevel> OnLevelChanged;
        public event Action OnDetected;
        public event Action<float> OnValueChanged;

        // 프로퍼티
        public bool HasActiveSystem => _activeSystem != null;
        public float CurrentValue => _activeSystem?.CurrentValue ?? 0f;
        public SuspicionLevel CurrentLevel => _activeSystem?.CurrentLevel ?? SuspicionLevel.Safe;

        /// <summary>
        /// BossSuspicionSystem 등록
        /// </summary>
        public void Register(BossSuspicionSystem system)
        {
            if (system == null) return;

            // 기존 등록 해제
            if (_activeSystem != null && _activeSystem != system)
            {
                UnsubscribeFrom(_activeSystem);
            }

            _activeSystem = system;
            SubscribeTo(_activeSystem);

#if UNITY_EDITOR
            Debug.Log($"[SuspicionUIManager] Registered: {system.name}");
#endif
        }

        /// <summary>
        /// BossSuspicionSystem 등록 해제
        /// </summary>
        public void Unregister(BossSuspicionSystem system)
        {
            if (system == null) return;
            if (_activeSystem != system) return;

            UnsubscribeFrom(_activeSystem);
            _activeSystem = null;

#if UNITY_EDITOR
            Debug.Log($"[SuspicionUIManager] Unregistered: {system.name}");
#endif
        }

        private void SubscribeTo(BossSuspicionSystem system)
        {
            if (system == null) return;
            system.OnLevelChanged += OnLevelChangedHandler;
            system.OnDetected += OnDetectedHandler;
            system.OnValueChanged += OnValueChangedHandler;
        }

        private void UnsubscribeFrom(BossSuspicionSystem system)
        {
            if (system == null) return;
            system.OnLevelChanged -= OnLevelChangedHandler;
            system.OnDetected -= OnDetectedHandler;
            system.OnValueChanged -= OnValueChangedHandler;
        }

        private void OnLevelChangedHandler(SuspicionLevel level)
        {
            OnLevelChanged?.Invoke(level);
        }

        private void OnDetectedHandler()
        {
            OnDetected?.Invoke();
        }

        private void OnValueChangedHandler(float value)
        {
            OnValueChanged?.Invoke(value);
        }
    }
}
