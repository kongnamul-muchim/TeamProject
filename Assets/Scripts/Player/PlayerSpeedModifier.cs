using System.Collections.Generic;
using UnityEngine;
using HideAndInk.Core.Events;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;

namespace HideAndInk.Player
{
    /// <summary>
    /// [속도 둔화 관리] 성게 접촉 등 외부 효과로 인한 플레이어 속도 둔화를 관리한다.
    /// - EventBus를 통해 PlayerSlowedEvent 구독하여 스택 기반 둔화 적용
    /// - 각 스택은 독립적인 타이머를 가짐 (중첩 시 둔화율 곱연산)
    /// - 최대 스택 수 제한으로 과도한 둔화 방지
    /// </summary>
    public sealed class PlayerSpeedModifier : MonoBehaviour
    {
        [Header("🔗 References")]
        [Tooltip("PlayerMovementAdapter (비워두면 자동 탐색)")]
        [SerializeField] private PlayerMovementAdapter playerAdapter;

        [Header("⚡ 둔화 설정")]
        [Tooltip("최대 둔화 중첩 수 (SeaUrchinController.slowStackMax와 일치 권장)")]
        [SerializeField] private int maxStacks = 3;

        [Header("📊 Debug")]
        [Tooltip("현재 적용 중인 속도 배율 (읽기 전용)")]
        [SerializeField] private float currentMultiplier = 1f;

        // 활성 둔화 효과 목록
        private readonly List<SlowStack> _activeStacks = new List<SlowStack>();
        private bool _needsUpdate = false;
        private IEventBus _eventBus;

        private class SlowStack
        {
            public float SlowPercent;
            public float RemainingTime;
        }

        private void Awake()
        {
            if (playerAdapter == null)
                playerAdapter = GetComponent<PlayerMovementAdapter>();

            if (GameManager.Container != null && GameManager.Container.IsRegistered<IEventBus>())
            {
                _eventBus = GameManager.Container.Resolve<IEventBus>();
            }
        }

        private void OnEnable()
        {
            _eventBus?.Subscribe<PlayerSlowedEvent>(OnPlayerSlowedEvent);
        }

        private void OnDisable()
        {
            _eventBus?.Unsubscribe<PlayerSlowedEvent>(OnPlayerSlowedEvent);
            ClearAllStacks();
        }

        private void Update()
        {
            if (!_needsUpdate)
                return;

            // 만료된 스택 제거 (뒤에서부터 순회)
            for (int i = _activeStacks.Count - 1; i >= 0; i--)
            {
                _activeStacks[i].RemainingTime -= Time.deltaTime;
                if (_activeStacks[i].RemainingTime <= 0f)
                    _activeStacks.RemoveAt(i);
            }

            // 속도 배율 계산 및 적용
            ApplyMultiplier();
        }

        /// <summary>
        /// 성게 둔화 이벤트 처리
        /// </summary>
        private void OnPlayerSlowedEvent(PlayerSlowedEvent e)
        {
            // 새 둔화 스택 추가
            _activeStacks.Add(new SlowStack
            {
                SlowPercent = Mathf.Clamp01(e.SlowPercent),
                RemainingTime = Mathf.Max(0f, e.Duration)
            });

            // 최대 중첩 수 제한 (가장 오래된 것부터 제거)
            while (_activeStacks.Count > maxStacks)
                _activeStacks.RemoveAt(0);

            _needsUpdate = true;
            ApplyMultiplier();

#if UNITY_EDITOR
            Debug.Log($"[PlayerSpeedModifier] Slowed! Stacks: {_activeStacks.Count}, Multiplier: {currentMultiplier:F3}");
#endif
        }

        /// <summary>
        /// 현재 스택 기반 속도 배율 계산 및 적용
        /// </summary>
        private void ApplyMultiplier()
        {
            if (_activeStacks.Count == 0)
            {
                currentMultiplier = 1f;
                _needsUpdate = false;
            }
            else
            {
                float multiplier = 1f;
                foreach (var stack in _activeStacks)
                    multiplier *= (1f - stack.SlowPercent);

                currentMultiplier = Mathf.Max(multiplier, 0.05f); // 최소 5% 속도 보장
            }

            if (playerAdapter != null)
                playerAdapter.SetSpeedMultiplier(currentMultiplier);
        }

        /// <summary>
        /// 모든 둔화 효과 즉시 제거
        /// </summary>
        public void ClearAllStacks()
        {
            _activeStacks.Clear();
            _needsUpdate = false;
            ApplyMultiplier();
        }

        /// <summary>
        /// 현재 활성 스택 수
        /// </summary>
        public int ActiveStackCount => _activeStacks.Count;

        /// <summary>
        /// 현재 속도 배율 (외부 확인용)
        /// </summary>
        public float CurrentMultiplier => currentMultiplier;
    }
}
