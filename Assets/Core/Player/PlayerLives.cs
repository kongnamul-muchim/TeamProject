using UnityEngine;
using HideAndInk.Core.Managers;
using HideAndInk.Core.Interfaces;

namespace HideAndInk.Core.Player
{
    /// <summary>
    /// 플레이어 목숨 시스템
    /// 적에게 공격받으면 목숨 감소 → 0 되면 Dead 상태로 전환
    /// 피격 후 무적 시간 (Invincibility Frames) 적용
    /// </summary>
    public class PlayerLives : MonoBehaviour
    {
        [Header("목숨 설정")]
        [Tooltip("최대 목숨 개수")]
        [SerializeField] private int maxLives = 3;

        [Tooltip("피격 후 무적 시간 (초)")]
        [SerializeField] private float invincibilityDuration = 1.5f;

        // 상태
        private int _currentLives;
        private float _invincibilityTimer;
        private bool _isInvincible;

        // 프로퍼티
        public int CurrentLives => _currentLives;
        public int MaxLives => maxLives;
        public bool IsInvincible => _isInvincible;

        // 이벤트
        public event System.Action<int> OnLifeChanged;       // (남은 목숨)
        public event System.Action OnPlayerDied;              // 목숨 0 도달
        public event System.Action OnDamageTaken;             // 피격 당했을 때 (UI 플래시 등)

        private void Start()
        {
            _currentLives = maxLives;
            OnLifeChanged?.Invoke(_currentLives);
        }

        private void Update()
        {
            if (_isInvincible)
            {
                _invincibilityTimer -= Time.deltaTime;
                if (_invincibilityTimer <= 0f)
                {
                    _isInvincible = false;
#if UNITY_EDITOR
                    Debug.Log("[PlayerLives] Invincibility ended.");
#endif
                }
            }
        }

        /// <summary>
        /// 데미지를 받습니다. 무적 중이면 무시됩니다.
        /// </summary>
        public void TakeDamage()
        {
            if (_isInvincible) return;
            if (_currentLives <= 0) return;

            _currentLives--;
            _isInvincible = true;
            _invincibilityTimer = invincibilityDuration;

            OnLifeChanged?.Invoke(_currentLives);
            OnDamageTaken?.Invoke();

#if UNITY_EDITOR
            Debug.Log($"[PlayerLives] Damage taken! Lives: {_currentLives}/{maxLives}");
#endif

            if (_currentLives <= 0)
            {
                OnPlayerDied?.Invoke();

                // GameManager를 통해 Dead 상태로 전환
                var gameManager = GameManager.Instance;
                if (gameManager != null)
                {
                    var stateMachine = gameManager.GetGameStateMachine();
                    if (stateMachine != null && stateMachine.CanTransitionTo(GameState.Dead))
                    {
                        stateMachine.TransitionTo(GameState.Dead);
                    }
                }
            }
        }

        /// <summary>
        /// 목숨 초기화 (게임 재시작 등)
        /// </summary>
        public void ResetLives()
        {
            _currentLives = maxLives;
            _isInvincible = false;
            _invincibilityTimer = 0f;
            OnLifeChanged?.Invoke(_currentLives);

#if UNITY_EDITOR
            Debug.Log("[PlayerLives] Lives reset.");
#endif
        }

        /// <summary>
        /// 남은 목숨이 없는지 확인
        /// </summary>
        public bool IsDead => _currentLives <= 0;
    }
}
