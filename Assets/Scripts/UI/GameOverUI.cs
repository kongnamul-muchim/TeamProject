using HideAndInk.Core.Audio;
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

        [Header("페이드 트랜지션")]
        [Tooltip("FadeInObj 프리팹 (씬 전환 시 페이드아웃)")]
        [SerializeField] private GameObject fadeInObjPrefab;

        [Header("설정")]
        [Tooltip("타이틀 씬 인덱스")]
        [SerializeField] private int titleSceneIndex = 0;

        [Tooltip("첫 게임 씬 인덱스 (New Game)")]
        [SerializeField] private int gameSceneIndex = 1;

        // DI
        private ISfxService _sfxService;
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
            {
                continueButton.onClick.AddListener(OnContinueClicked);
                Debug.Log($"[GameOverUI] Continue button listener registered. interactable={continueButton.interactable}, enabled={continueButton.enabled}");
            }
            else
            {
                Debug.LogError("[GameOverUI] Continue button is NULL!");
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestartClicked);
                Debug.Log($"[GameOverUI] Restart button listener registered. interactable={restartButton.interactable}, enabled={restartButton.enabled}");
            }
            else
            {
                Debug.LogError("[GameOverUI] Restart button is NULL!");
            }

            if (titleButton != null)
            {
                titleButton.onClick.AddListener(OnTitleClicked);
                Debug.Log($"[GameOverUI] Title button listener registered. interactable={titleButton.interactable}, enabled={titleButton.enabled}");
            }
            else
            {
                Debug.LogError("[GameOverUI] Title button is NULL!");
            }

            // 버튼 자식 텍스트들의 RaycastTarget 강제 비활성화 (클릭 가로채기 방지)
            DisableButtonTextRaycast(continueButton);
            DisableButtonTextRaycast(restartButton);
            DisableButtonTextRaycast(titleButton);

            // 버튼 Image alpha 강제 설정 (투명 Image는 Raycast 안 됨)
            FixButtonAlpha(continueButton);
            FixButtonAlpha(restartButton);
            FixButtonAlpha(titleButton);

            // 초기 상태: 숨김
            if (gameOverPanel != null)
                gameOverPanel.SetActive(false);
        }

        /// <summary>
        /// 버튼의 자식 텍스트 요소들의 RaycastTarget을 비활성화
        /// </summary>
        private void DisableButtonTextRaycast(Button button)
        {
            if (button == null) return;
            
            var texts = button.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                if (text.raycastTarget)
                {
                    text.raycastTarget = false;
                    Debug.Log($"[GameOverUI] Disabled RaycastTarget on {text.gameObject.name} (parent: {button.name})");
                }
            }
            
            // Image 컴포넌트도 확인 (버튼 자신의 Image는 제외)
            var images = button.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject != button.gameObject && img.raycastTarget)
                {
                    img.raycastTarget = false;
                    Debug.Log($"[GameOverUI] Disabled RaycastTarget on Image {img.gameObject.name} (parent: {button.name})");
                }
            }
        }

        /// <summary>
        /// 버튼 Image의 alpha를 0.01로 설정 (완전 투명하면 Raycast 안됨, 0.01이면 거의 투명 + 클릭 가능)
        /// </summary>
        private void FixButtonAlpha(Button button)
        {
            if (button == null) return;
            
            var img = button.GetComponent<Image>();
            if (img != null)
            {
                var color = img.color;
                if (color.a <= 0f)
                {
                    color.a = 0.01f;
                    img.color = color;
                    Debug.Log($"[GameOverUI] Fixed alpha on {button.name} Image: 0 -> 0.01 (near-transparent)");
                }
            }
        }

        private void Start()
        {
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

            // Canvas_Ingame 직접 찾기 (현재 스크립트 위치와 무관)
            var canvasIngame = GameObject.Find("Canvas_Ingame")?.transform;
            if (canvasIngame != null)
            {
                // 다른 팝업이 열려있으면 닫기 (Pause 등)
                var popupPause = canvasIngame.Find("Popup_Pause")?.gameObject;
                if (popupPause != null && popupPause.activeSelf)
                {
                    popupPause.SetActive(false);
                    Debug.Log("[GameOverUI] Popup_Pause force-closed");
                }

                // 의심도 비네트 비활성화 (Raycast 차단 방지)
                var vignette = canvasIngame.Find("UI_SuspicionVinette")?.gameObject;
                if (vignette != null)
                {
                    vignette.SetActive(false);
                    Debug.Log("[GameOverUI] UI_SuspicionVinette deactivated");
                }

                // DialogUI도 닫기
                var dialogUI = canvasIngame.Find("DialogUI")?.gameObject;
                if (dialogUI != null && dialogUI.activeSelf)
                {
                    dialogUI.SetActive(false);
                    Debug.Log("[GameOverUI] DialogUI force-closed");
                }
            }

            // 게임 오버 패널 표시 및 최상위로 이동
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                gameOverPanel.transform.SetAsLastSibling(); // 최상위로
                Debug.Log($"[GameOverUI] gameOverPanel active={gameOverPanel.activeInHierarchy}, eventSystem={UnityEngine.EventSystems.EventSystem.current != null}");
            }

            // UI_SuspicionVinette alpha 강제 고정 (SuspicionMeterUI가 덮어쓰는 것 방지)
            ForceVignetteToMax();

            // 시간 복원 (버튼 클릭 가능하도록)
            Time.timeScale = 1f;
            Debug.Log("[GameOverUI] Time.timeScale restored to 1 for UI interaction");

            // EventSystem 상태 확인 및 StandaloneInputModule 추가
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem != null)
            {
                var standaloneModule = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                if (standaloneModule == null)
                {
                    eventSystem.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                    Debug.Log("[GameOverUI] StandaloneInputModule added to EventSystem");
                }
                Debug.Log($"[GameOverUI] EventSystem enabled={eventSystem.enabled}, StandaloneInputModule={standaloneModule != null}");
            }
        }

        /// <summary>
        /// UI_SuspicionVinette의 alpha를 의심도 최대치(200/255)로 강제 고정
        /// OnPlayerDied 등으로 SuspicionMeterUI가 alpha를 0으로 만든 후에도 확실히 적용
        /// </summary>
        private static void ForceVignetteToMax()
        {
            var canvasObj = GameObject.Find("Canvas_Ingame");
            if (canvasObj == null) return;

            var vignette = canvasObj.transform.Find("UI_SuspicionVinette");
            if (vignette == null) return;

            var img = vignette.GetComponent<UnityEngine.UI.Image>();
            if (img == null) return;

            var color = img.color;
            if (Mathf.Abs(color.a - 200f / 255f) > 0.001f)
            {
                color.a = 200f / 255f;
                img.color = color;
                Debug.Log($"[GameOverUI] Vignette alpha fixed: → {color.a:F3}");
            }
        }

        // =====================================================
        // Button Handlers
        // =====================================================

        /// <summary>
        /// FadeInObj 페이드아웃 후 씬 전환 (기존 TitleController/SettingsPopup과 동일한 방식)
        /// </summary>
        private void TransitionToScene(int sceneIndex)
        {
            HideGameOver();

            // 시간 복원 (FadeInObj 애니메이션이 동작하도록)
            Time.timeScale = 1f;

            if (fadeInObjPrefab != null)
            {
                var fadeObj = Instantiate(fadeInObjPrefab);
                var controller = fadeObj.GetComponent<FadeInObjController>();
                if (controller != null)
                {
                    controller.PlayCoverAndTransition(sceneIndex);
                    return;
                }
            }

            // fallback: 바로 로드
            SceneManager.LoadScene(sceneIndex);
        }

        /// <summary>
        /// [이어하기] 저장된 지점에서 재시작
        /// </summary>
        private void OnContinueClicked()
        {
            Debug.Log("[GameOverUI] OnContinueClicked CALLED!");
            _sfxService?.Play(SfxId.ButtonClick);
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

            TransitionToScene(gameSceneIndex);
        }

        /// <summary>
        /// [다시 시작] 게임 전체 리셋 → 처음부터
        /// </summary>
        private void OnRestartClicked()
        {
            _sfxService?.Play(SfxId.ButtonClick);
            // 저장 데이터 삭제
            SaveManager.DeleteSave();
            SaveManager.ClearContinueZone();

            TransitionToScene(gameSceneIndex);
        }

        /// <summary>
        /// [그만하기] 타이틀 화면으로 복귀
        /// </summary>
        private void OnTitleClicked()
        {
            _sfxService?.Play(SfxId.ButtonClick);
            TransitionToScene(titleSceneIndex);
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
