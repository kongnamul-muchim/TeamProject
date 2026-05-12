using UnityEngine;
using HideAndInk.Core.Managers;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Events;
using HideAndInk.Scripts.UI;

namespace HideAndInk.Core.Player
{
    /// <summary>
    /// 플레이어 목숨 시스템
    /// 적에게 공격받으면 목숨 감소 → 0 되면 Dead 상태로 전환
    /// 피격 후 무적 시간 (Invincibility Frames) 적용
    /// </summary>
    public class PlayerLives : MonoBehaviour
    {
        /// <summary>
        /// 씬의 유일한 PlayerLives 인스턴스 (FindObjectOfType 회피)
        /// </summary>
        public static PlayerLives Instance { get; private set; }

        [Header("목숨 설정")]
        [Tooltip("최대 목숨 개수")]
        [SerializeField] private int maxLives = 3;

        [Tooltip("피격 후 무적 시간 (초)")]
        [SerializeField] private float invincibilityDuration = 1.5f;

        [Header("무적 깜빡임")]
        [SerializeField, Tooltip("깜빡임 주파수 (무적 시)")] private float blinkFrequency = 10f;
        [SerializeField, Tooltip("깜빡임 대상 오브젝트 (Visual)")] private GameObject blinkTarget;

        // 상태
        private int _currentLives;
        private float _invincibilityTimer;
        private bool _isInvincible;
        private bool _blinkTargetWasActive;
        private bool _isDashInvincibility; // 대시용 무적 여부 (깜빡임 없음)
        private bool _isDead; // 사망 상태 플래그 (중복 트리거 방지)

        // 마지막 사망 원인 (GameManager에서 읽어서 이벤트 발행)
        private DeathCause _lastDeathCause = DeathCause.Unknown;
        private string _lastDeathSourceName = "";
        private bool _lastWasCamouflaged;

        // 프로퍼티
        public int CurrentLives => _currentLives;
        public int MaxLives => maxLives;
        public bool IsInvincible => _isInvincible;

        // 이벤트
        public event System.Action<int> OnLifeChanged;       // (남은 목숨)
        public event System.Action OnPlayerDied;              // 목숨 0 도달
        public event System.Action OnDamageTaken;             // 피격 당했을 때 (UI 플래시 등)

        private void Awake()
        {
            Instance = this;
            _currentLives = maxLives;
        }

        private void Start()
        {
            OnLifeChanged?.Invoke(_currentLives);
        }

        private void Update()
        {
            // ── 안전장치: 사망 조건 체크 (UI 전부 비활성화 or 체력 0 이하) ──
            if (!_isDead)
            {
                bool allHeartsDisabled = PlayerHPUI.Instance != null && PlayerHPUI.Instance.AreAllHeartsDisabled();
                if (_currentLives <= 0 || allHeartsDisabled)
                {
                    TryTriggerDeath();
                }
            }

            if (_isInvincible)
            {
                _invincibilityTimer -= Time.deltaTime;

                // 깜빡임: 대시용 무적이 아닐 때만 Visual 오브젝트 켰다/껐다 반복
                if (blinkTarget != null && !_isDashInvincibility)
                {
                    float wave = Mathf.Sin(Time.time * blinkFrequency * Mathf.PI * 2);
                    blinkTarget.SetActive(wave > 0f);
                }

                if (_invincibilityTimer <= 0f)
                {
                    _isInvincible = false;
                    _isDashInvincibility = false;
                    if (blinkTarget != null)
                        blinkTarget.SetActive(true); // 복원
#if UNITY_EDITOR
                    Debug.Log("[PlayerLives] Invincibility ended.");
#endif
                }
            }
        }

        /// <summary>
        /// 사망 전환 공통 로직 (중복 방지)
        /// </summary>
        private void TryTriggerDeath()
        {
            if (_isDead) return;
            _isDead = true;

            OnPlayerDied?.Invoke();

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

        /// <summary>
        /// 외부에서 강제로 무적 시간 설정 (피격용 - 깜빡임 있음)
        /// </summary>
        public void SetInvincible(float duration)
        {
            _isInvincible = true;
            _isDashInvincibility = false;
            _invincibilityTimer = duration;

            // 현재 blinkTarget의 활성 상태 저장 (나중에 복원용)
            if (blinkTarget != null)
                _blinkTargetWasActive = blinkTarget.activeSelf;
        }

        /// <summary>
        /// 대시용 무적 시간 설정 (깜빡임 없음)
        /// </summary>
        public void SetDashInvincible(float duration)
        {
            _isInvincible = true;
            _isDashInvincibility = true;
            _invincibilityTimer = duration;
        }

        /// <summary>
        /// 데미지를 받습니다. 무적 중이면 무시됩니다.
        /// </summary>
        /// <param name="cause">사망 원인 (목숨 0이 될 때 사용)</param>
        /// <param name="sourceName">사망 원인 오브젝트 이름 (디버깅용)</param>
        public void TakeDamage(DeathCause cause = DeathCause.Unknown, string sourceName = "")
        {
            if (_isInvincible) return;
            if (_currentLives <= 0) return;

            _currentLives--;
            _isInvincible = true;
            _invincibilityTimer = invincibilityDuration;

            // 사망 원인 저장 (GameManager가 이벤트 발행 시 사용)
            _lastDeathCause = cause;
            _lastDeathSourceName = sourceName;

            // 의태 상태 확인 (같은 GameObject에 있는 CamouflageAdapter)
            _lastWasCamouflaged = false;
            var camouflage = GetComponent<HideAndInk.Player.CamouflageAdapter>();
            if (camouflage != null)
            {
                _lastWasCamouflaged = camouflage.IsCamouflaging;
            }

            OnLifeChanged?.Invoke(_currentLives);
            OnDamageTaken?.Invoke();

#if UNITY_EDITOR
            Debug.Log($"[PlayerLives] Damage taken! Cause: {cause}, Lives: {_currentLives}/{maxLives}");
#endif

            if (_currentLives <= 0)
            {
                TryTriggerDeath();
            }
        }

        /// <summary>
        /// 목숨 초기화 (게임 재시작 등)
        /// </summary>
        public void ResetLives()
        {
            _currentLives = maxLives;
            _isInvincible = false;
            _isDead = false;
            _invincibilityTimer = 0f;
            OnLifeChanged?.Invoke(_currentLives);

#if UNITY_EDITOR
            Debug.Log("[PlayerLives] Lives reset.");
#endif
        }

        /// <summary>
        /// 남은 목숨이 없는지 확인 (UI 전부 비활성화 or 체력 0 이하)
        /// </summary>
        public bool IsDead
        {
            get
            {
                if (_currentLives <= 0) return true;
                if (PlayerHPUI.Instance != null && PlayerHPUI.Instance.AreAllHeartsDisabled()) return true;
                return false;
            }
        }

        /// <summary>
        /// 마지막 사망 원인 (GameManager가 PlayerDeathEvent 발행 시 사용)
        /// </summary>
        public DeathCause LastDeathCause => _lastDeathCause;

        /// <summary>
        /// 마지막 사망 원인 오브젝트 이름
        /// </summary>
        public string LastDeathSourceName => _lastDeathSourceName;

        /// <summary>
        /// 사망 당시 의태 중이었는지 여부
        /// </summary>
        public bool LastWasCamouflaged => _lastWasCamouflaged;
    }
}
