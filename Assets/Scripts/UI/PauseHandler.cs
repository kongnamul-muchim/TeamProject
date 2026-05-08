using HideAndInk.Core.Audio;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using UnityEngine;

namespace HideAndInk.Scripts.UI
{
    /// <summary>
    /// 일시 정지 UI 제어
    /// Btn_Pause / Btn_Close → 인스펙터 OnClick에서 직접 호출
    /// </summary>
    public sealed class PauseHandler : MonoBehaviour
    {
        [Header("팝업")]
        [Tooltip("일시정지 팝업 오브젝트")]
        [SerializeField] private GameObject pausePopup;

        private ISfxService _sfxService;
        private IGameStateMachine _stateMachine;

        private void Awake()
        {
            // SFX 서비스 해결
            if (GameManager.Container != null && GameManager.Container.IsRegistered<ISfxService>())
            {
                _sfxService = GameManager.Container.Resolve<ISfxService>();
            }

            // DI 실패 시 Singleton fallback
            if (_sfxService == null)
            {
                _sfxService = SfxManager.Instance;
            }

            var gm = GameManager.Instance;
            if (gm != null)
                _stateMachine = gm.GetGameStateMachine();

            // GameManager 초기화보다 먼저 Awake가 실행될 수 있으므로
            // _stateMachine이 null이면 TogglePause/ResumeGame에서 EnsureStateMachine()이 재시도함
            if (_stateMachine == null)
            {
                Debug.LogWarning("[PauseHandler] GameStateMachine not ready yet. Will retry on button click.");
            }
            else
            {
                _stateMachine.OnStateChanged += OnGameStateChanged;
            }

            // 초기 상태: 팝업 닫힘
            if (pausePopup != null)
                pausePopup.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_stateMachine != null)
                _stateMachine.OnStateChanged -= OnGameStateChanged;
        }

        /// <summary>
        /// Btn_Pause → 인스펙터 OnClick 연결
        /// </summary>
        public void TogglePause()
        {
            _sfxService?.Play(SfxId.ButtonClick);
            EnsureStateMachine();
            _stateMachine?.TogglePause();
        }

        /// <summary>
        /// Btn_Close (게임재개) → 인스펙터 OnClick 연결
        /// </summary>
        public void ResumeGame()
        {
            _sfxService?.Play(SfxId.ButtonClick);
            EnsureStateMachine();
            if (_stateMachine == null) return;
            if (_stateMachine.CurrentState == GameState.Paused)
                _stateMachine.TogglePause();
        }

        private void EnsureStateMachine()
        {
            if (_stateMachine != null) return;
            var gm = GameManager.Instance;
            if (gm != null)
            {
                _stateMachine = gm.GetGameStateMachine();
                if (_stateMachine != null)
                    _stateMachine.OnStateChanged += OnGameStateChanged;
            }
        }

        private void OnGameStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Paused)
            {
                Time.timeScale = 0f;
                if (pausePopup != null) pausePopup.SetActive(true);
            }
            else if (previous == GameState.Paused)
            {
                Time.timeScale = 1f;
                if (pausePopup != null) pausePopup.SetActive(false);
            }
        }
    }
}
