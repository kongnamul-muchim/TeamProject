using System.Collections;
using HideAndInk.Core.Audio;
using HideAndInk.Core.Events;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HideAndInk.Scripts.UI
{
    /// <summary>
    /// 사망 화면 UI — 사망 원인별 멘트 표시 + 재시작 버튼
    /// Canvas_Ingame에 부착하여 사용.
    /// EventBus의 PlayerDeathEvent를 구독하여 활성화.
    /// </summary>
    public sealed class DeathScreenUI : MonoBehaviour
    {
        [Header("사망 화면 설정")]
        [Tooltip("사망 후 화면 표시까지 딜레이 (초)")]
        [SerializeField] private float deathScreenDelay = 1.5f;

        [Tooltip("멘트 표시 시간 (초, 0이면 수동 dismiss)")]
        [SerializeField] private float messageDuration = 4f;

        [Tooltip("페이드 인/아웃 시간")]
        [SerializeField] private float fadeDuration = 0.5f;

        [Header("UI 요소 (인스펙터 연결 권장)")]
        [Tooltip("사망 화면 전체 패널 (Canvas_Ingame 자식)")]
        [SerializeField] private GameObject deathPanel;

        [Tooltip("사망 멘트 텍스트 (TMP)")]
        [SerializeField] private TextMeshProUGUI messageText;

        [Tooltip("재시작 버튼")]
        [SerializeField] private Button restartButton;

        [Tooltip("타이틀로 버튼")]
        [SerializeField] private Button titleButton;

        [Header("Canvas 그룹 (페이드용)")]
        [SerializeField] private CanvasGroup canvasGroup;

        // DI
        private ISfxService _sfxService;
        private IEventBus _eventBus;
        private IGameStateMachine _stateMachine;
        private Coroutine _showCoroutine;

        private void Awake()
        {
            // Canvas_Ingame에서 자식 찾기 (인스펙터 미연결 시)
            if (deathPanel == null)
                deathPanel = transform.Find("DeathPanel")?.gameObject;

            if (messageText == null)
                messageText = GetComponentInChildren<TextMeshProUGUI>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            // 버튼 리스너 연결
            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartClicked);

            if (titleButton != null)
                titleButton.onClick.AddListener(OnTitleClicked);

            // 초기 상태: 숨김
            if (deathPanel != null)
                deathPanel.SetActive(false);

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
        }

        private void Start()
        {
            // DI 서비스 해결
            var container = GameManager.Container;
            if (container != null)
            {
                if (container.IsRegistered<ISfxService>())
                    _sfxService = container.Resolve<ISfxService>();

                if (container.IsRegistered<IEventBus>())
                    _eventBus = container.Resolve<IEventBus>();

                if (container.IsRegistered<IGameStateMachine>())
                    _stateMachine = container.Resolve<IGameStateMachine>();
            }

            // EventBus 구독
            _eventBus?.Subscribe<PlayerDeathEvent>(OnPlayerDeath);
        }

        private void OnDestroy()
        {
            _eventBus?.Unsubscribe<PlayerDeathEvent>(OnPlayerDeath);

            if (_showCoroutine != null)
                StopCoroutine(_showCoroutine);
        }

        /// <summary>
        /// EventBus를 통해 사망 이벤트 수신
        /// </summary>
        private void OnPlayerDeath(PlayerDeathEvent evt)
        {
            if (_showCoroutine != null)
                StopCoroutine(_showCoroutine);

            _showCoroutine = StartCoroutine(ShowDeathScreen(evt));
        }

        private IEnumerator ShowDeathScreen(PlayerDeathEvent evt)
        {
            // 딜레이 (사망 연출 후 표시)
            if (deathScreenDelay > 0f)
                yield return new WaitForSecondsRealtime(deathScreenDelay);

            // 멘트 설정
            if (messageText != null)
            {
                messageText.text = evt.DeathMessage;
            }

            // 패널 활성화
            if (deathPanel != null)
                deathPanel.SetActive(true);

            // 페이드 인
            if (canvasGroup != null)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                    yield return null;
                }
                canvasGroup.alpha = 1f;
            }

            // 자동 dismiss (duration > 0)
            if (messageDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(messageDuration);

                // 페이드 아웃
                if (canvasGroup != null)
                {
                    float elapsed = 0f;
                    while (elapsed < fadeDuration)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        canvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                        yield return null;
                    }
                    canvasGroup.alpha = 0f;
                }

                if (deathPanel != null)
                    deathPanel.SetActive(false);
            }

            _showCoroutine = null;
        }

        /// <summary>
        /// Btn_Restart → 게임 재시작
        /// </summary>
        public void OnRestartClicked()
        {
            _sfxService?.Play(SfxId.ButtonClick2);
            HideDeathScreen();
            _stateMachine?.Restart();
        }

        /// <summary>
        /// Btn_Title → 타이틀 화면 복귀
        /// </summary>
        public void OnTitleClicked()
        {
            _sfxService?.Play(SfxId.ButtonClick2);
            HideDeathScreen();
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }

        /// <summary>
        /// 사망 화면 즉시 숨김
        /// </summary>
        public void HideDeathScreen()
        {
            if (_showCoroutine != null)
            {
                StopCoroutine(_showCoroutine);
                _showCoroutine = null;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (deathPanel != null)
                deathPanel.SetActive(false);
        }
    }
}
