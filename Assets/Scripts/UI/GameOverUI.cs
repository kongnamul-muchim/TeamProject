using HideAndInk.Core.Events;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using HideAndInk.Scripts.Save;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace HideAndInk.Scripts.UI
{
    /// <summary>
    /// 게임 오버 화면 (Popup_GameOver) 제어
    /// 사망 시 메시지 표시 + 버튼 동작 (이어하기/다시시작/그만하기)
    /// Canvas_Ingame에 부착하여 사용.
    /// </summary>
    public sealed class GameOverUI : MonoBehaviour
    {
        [Header("Game Over Panel (자동 탐색)")]
        [Tooltip("Popup_GameOver 루트 오브젝트")]
        [SerializeField] private GameObject gameOverPanel;

        [Tooltip("Text_GameOverLog - 사망 메시지")]
        [SerializeField] private TextMeshProUGUI logText;

        [Tooltip("Btn_Continue - 이어하기")]
        [SerializeField] private Button continueButton;

        [Tooltip("Btn_Restart - 다시 시작")]
        [SerializeField] private Button restartButton;

        [Tooltip("Btn_GoTitle - 그만하기")]
        [SerializeField] private Button titleButton;

        [Header("설정")]
        [Tooltip("페이드 인 시간")]
        [SerializeField] private float fadeInDuration = 0.5f;

        [Tooltip("타이틀 씬 인덱스")]
        [SerializeField] private int titleSceneIndex = 0;

        [Tooltip("첫 게임 씬 인덱스 (New Game)")]
        [SerializeField] private int gameSceneIndex = 1;

        // DI
        private IEventBus _eventBus;
        private IGameStateMachine _stateMachine;

        private void Awake()
        {
            // Canvas_Ingame 자식에서 Popup_GameOver 자동 탐색
            if (gameOverPanel == null)
            {
                var found = transform.Find("Popup_GameOver");
                if (found != null) gameOverPanel = found.gameObject;
            }

            if (gameOverPanel == null)
            {
                Debug.LogError("[GameOverUI] Popup_GameOver를 찾을 수 없습니다. Canvas_Ingame에 스크립트를 부착했는지 확인하세요.");
                return;
            }

            // 자식 UI 요소 자동 탐색
            if (logText == null)
                logText = gameOverPanel.transform.Find("Text_GameOverLog")?.GetComponent<TextMeshProUGUI>();

            if (continueButton == null)
            {
                var btn = gameOverPanel.transform.Find("Panel/Btn_Continue") ?? gameOverPanel.transform.Find("Btn_Continue");
                if (btn != null) continueButton = btn.GetComponent<Button>();
            }

            if (restartButton == null)
            {
                var btn = gameOverPanel.transform.Find("Panel/Btn_Restart") ?? gameOverPanel.transform.Find("Btn_Restart");
                if (btn != null) restartButton = btn.GetComponent<Button>();
            }

            if (titleButton == null)
            {
                var btn = gameOverPanel.transform.Find("Panel/Btn_GoTitle") ?? gameOverPanel.transform.Find("Btn_GoTitle");
                if (btn != null) titleButton = btn.GetComponent<Button>();
            }

            // 버튼 리스너 등록
            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);

            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartClicked);

            if (titleButton != null)
                titleButton.onClick.AddListener(OnTitleClicked);

            // 초기 상태: 숨김
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
        }

        private void Start()
        {
            var container = GameManager.Container;
            if (container != null)
            {
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
        }

        /// <summary>
        /// 사망 이벤트 수신 → 게임 오버 화면 표시
        /// </summary>
        private void OnPlayerDeath(PlayerDeathEvent evt)
        {
            // 로그 텍스트 설정 ("Log: 완전히 도망치기에는 먹물이 부족했나봐..." 형식)
            if (logText != null)
            {
                logText.text = $"Log: {evt.DeathMessage}";
            }

            // 게임 오버 패널 표시
            if (gameOverPanel != null)
                gameOverPanel.SetActive(true);
        }

        // =====================================================
        // Button Handlers
        // =====================================================

        /// <summary>
        /// [이어하기] 저장된 지점에서 재시작
        /// </summary>
        private void OnContinueClicked()
        {
            HideGameOver();

            // 저장 데이터가 있으면 로드
            if (SaveManager.HasSaveData())
            {
                var data = SaveManager.Load();
                if (data != null)
                {
                    SaveManager.SetContinueZone(data.lastZoneIndex,
                        data.GetPlayerPosition(), data.GetSquidPosition());
                }
            }

            // 게임 씬 로드 (ContinueZoneHandler가 자동 복원)
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameSceneIndex);
        }

        /// <summary>
        /// [다시 시작] 게임 전체 리셋 → 처음부터
        /// </summary>
        private void OnRestartClicked()
        {
            HideGameOver();

            // 저장 데이터 삭제
            SaveManager.DeleteSave();
            SaveManager.ClearContinueZone();

            // 게임 씬 로드 (처음부터)
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameSceneIndex);
        }

        /// <summary>
        /// [그만하기] 타이틀 화면으로 복귀
        /// </summary>
        private void OnTitleClicked()
        {
            HideGameOver();

            Time.timeScale = 1f;
            SceneManager.LoadScene(titleSceneIndex);
        }

        /// <summary>
        /// 게임 오버 화면 숨김
        /// </summary>
        private void HideGameOver()
        {
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
        }
    }
}
