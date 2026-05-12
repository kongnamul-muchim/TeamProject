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

            // 버튼 참조 확인 로그
            Debug.Log($"[GameOverUI] Awake - Continue: {(continueButton != null ? continueButton.name : "NULL")}, Restart: {(restartButton != null ? restartButton.name : "NULL")}, Title: {(titleButton != null ? titleButton.name : "NULL")}");

            // 버튼 리스너 등록
            if (continueButton != null)
                continueButton.onClick.AddListener(OnContinueClicked);
            else
                Debug.LogError("[GameOverUI] Continue button is NULL!");

            if (restartButton != null)
                restartButton.onClick.AddListener(OnRestartClicked);
            else
                Debug.LogError("[GameOverUI] Restart button is NULL!");

            if (titleButton != null)
                titleButton.onClick.AddListener(OnTitleClicked);
            else
                Debug.LogError("[GameOverUI] Title button is NULL!");

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
                    text.raycastTarget = false;
            }
            
            // Image 컴포넌트도 확인 (버튼 자신의 Image는 제외)
            var images = button.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject != button.gameObject && img.raycastTarget)
                    img.raycastTarget = false;
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

        private void Update()
        {
            // 게임 오버 패널이 활성화된 상태에서만 마우스 클릭 감지
            if (gameOverPanel == null || !gameOverPanel.activeInHierarchy) return;
            
            if (Input.GetMouseButtonDown(0))
            {
                TryInvokeButtonClick(Input.mousePosition);
            }
        }

        /// <summary>
        /// 마우스 위치가 버튼 RectTransform 내에 있으면 해당 버튼의 onClick을 직접 호출합니다.
        /// EventSystem raycast 문제 우회용.
        /// </summary>
        private void TryInvokeButtonClick(Vector2 screenPosition)
        {
            TryInvokeButton(continueButton, screenPosition);
            TryInvokeButton(restartButton, screenPosition);
            TryInvokeButton(titleButton, screenPosition);
        }

        private void TryInvokeButton(Button button, Vector2 screenPosition)
        {
            if (button == null || !button.interactable || !button.gameObject.activeInHierarchy) return;
            
            var rectTransform = button.GetComponent<RectTransform>();
            if (rectTransform == null) return;
            
            // Screen Space - Overlay Canvas 기준으로 RectTransform의 world bounds 계산
            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            
            // world corners를 screen space로 변환
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            
            Rect buttonRect = new Rect(min.x, min.y, max.x - min.x, max.y - min.y);
            
            if (buttonRect.Contains(screenPosition))
            {
                Debug.Log($"[GameOverUI] Direct click detected on: {button.name}");
                // onClick 리스너 우회 - 직접 메서드 호출
                if (button == continueButton) OnContinueClicked();
                else if (button == restartButton) OnRestartClicked();
                else if (button == titleButton) OnTitleClicked();
                else button.onClick?.Invoke();
            }
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
                // 다른 팝업이 열리있으면 닫기 (Pause 등)
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
                    dialogUI.SetActive(false);
            }

            // 게임 오버 패널 표시 및 최상위로 이동
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
                gameOverPanel.transform.SetAsLastSibling(); // 최상위로
            }

            // 버튼 클릭 가능하도록 강제 활성화
            EnableButtonInteraction(continueButton);
            EnableButtonInteraction(restartButton);
            EnableButtonInteraction(titleButton);

            // 버튼들을 gameOverPanel의 마지막 자식으로 이동 (raycast 우선순위 확보)
            if (continueButton != null) continueButton.transform.SetAsLastSibling();
            if (restartButton != null) restartButton.transform.SetAsLastSibling();
            if (titleButton != null) titleButton.transform.SetAsLastSibling();

            // CanvasGroup Raycast 차단 해제
            if (gameOverPanel != null)
            {
                var canvasGroup = gameOverPanel.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.blocksRaycasts = true;
                    canvasGroup.interactable = true;
                    canvasGroup.alpha = 1f;
                }
            }

            // gameOverPanel 내의 버튼이 아닌 모든 Image의 raycastTarget 비활성화
            DisableNonButtonRaycasts(gameOverPanel);

            // UI_SuspicionVinette alpha 강제 고정 (SuspicionMeterUI가 덮어쓰는 것 방지)
            ForceVignetteToMax();

            // 시간 복원 (버튼 클릭 가능하도록)
            Time.timeScale = 1f;

            // EventSystem에 StandaloneInputModule이 없으면 추가
            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem != null && eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null)
                eventSystem.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        /// <summary>
        /// 버튼의 interactable과 raycastTarget을 강제로 활성화합니다.
        /// </summary>
        private void EnableButtonInteraction(Button button)
        {
            if (button == null)
            {
                Debug.LogWarning("[GameOverUI] EnableButtonInteraction: button is NULL");
                return;
            }
            button.interactable = true;
            var img = button.GetComponent<Image>();
            if (img != null)
            {
                img.raycastTarget = true;
                if (img.color.a <= 0.01f)
                {
                    var color = img.color;
                    color.a = 0.01f;
                    img.color = color;
                }
            }
            else
            {
                Debug.LogWarning($"[GameOverUI] Button {button.name} has NO Image component!");
            }
            
            var rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                Debug.Log($"[GameOverUI] Button {button.name}: rect={rect.rect}, anchoredPosition={rect.anchoredPosition}, sizeDelta={rect.sizeDelta}");
            }
            
            Debug.Log($"[GameOverUI] Button enabled: {button.name}, interactable={button.interactable}, active={button.gameObject.activeInHierarchy}");
        }

        /// <summary>
        /// gameOverPanel 내에서 Button이 아닌 모든 Image의 raycastTarget을 비활성화합니다.
        /// 버튼 클릭을 가로채는 배경 이미지/패널 등을 방지합니다.
        /// </summary>
        private static void DisableNonButtonRaycasts(GameObject panel)
        {
            if (panel == null) return;
            var images = panel.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                // Button 컴포넌트가 없는 Image만 비활성화
                if (img.GetComponent<Button>() == null && img.raycastTarget)
                {
                    img.raycastTarget = false;
                    Debug.Log($"[GameOverUI] Raycast disabled on: {img.gameObject.name}");
                }
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

            // 게임 상태 초기화 (재시작 시 Dead 상태가 남아있는 문제 방지)
            _stateMachine?.Restart();

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
            Debug.Log("[GameOverUI] OnContinueClicked called!");
            _sfxService?.Play(SfxId.ButtonClick);

            if (SaveManager.HasSaveData())
            {
                var data = SaveManager.Load();
                if (data != null)
                {
                    Vector3 playerPos = data.GetPlayerPosition();
                    Vector3 squidPos = data.GetSquidPosition();

                    // 저장된 위치가 zero이면 현재 씬에서 플레이어 위치를 찾아 사용
                    if (playerPos == Vector3.zero)
                    {
                        var player = GameObject.FindGameObjectWithTag("Player");
                        if (player != null)
                        {
                            playerPos = player.transform.position;
                            Debug.Log($"[GameOverUI] 저장된 Player 위치가 zero → 현재 위치 사용: {playerPos}");
                        }
                    }

                    SaveManager.SetContinueZone(data.lastZoneIndex, playerPos, squidPos);
                    Debug.Log($"[GameOverUI] 이어하기: 저장 데이터 로드 - Zone_{data.lastZoneIndex}, PlayerPos={playerPos}");
                }
            }
            else
            {
                // 저장 데이터가 없으면 현재 활성 Zone을 찾아서 이어하기
                int currentZone = FindCurrentActiveZone();
                Debug.Log($"[GameOverUI] FindCurrentActiveZone returned: {currentZone}");
                if (currentZone > 0)
                {
                    // 현재 플레이어 위치도 함께 저장
                    var player = GameObject.FindGameObjectWithTag("Player");
                    Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
                    SaveManager.SetContinueZone(currentZone, playerPos, Vector3.zero);
                    Debug.Log($"[GameOverUI] 이어하기: 저장 데이터 없음 → 현재 Zone_{currentZone}에서 이어하기, PendingZoneIndex={SaveManager.PendingZoneIndex}, PlayerPos={playerPos}");
                }
                else
                {
                    Debug.LogWarning("[GameOverUI] 이어하기: 저장 데이터 없고 활성 Zone도 찾을 수 없음 → Zone_1에서 시작");
                    SaveManager.SetContinueZone(1);
                }
            }

            Debug.Log($"[GameOverUI] TransitionToScene called with PendingZoneIndex={SaveManager.PendingZoneIndex}");
            TransitionToScene(gameSceneIndex);
        }

        /// <summary>
        /// 씬에서 현재 활성화된 Zone 번호를 찾습니다.
        /// active 상태인 Zone 오브젝트 중 번호가 가장 큰 것을 반환합니다.
        /// </summary>
        private int FindCurrentActiveZone()
        {
            int maxZone = -1;
            var scene = SceneManager.GetActiveScene();
            
            Debug.Log($"[GameOverUI] FindCurrentActiveZone - Scene: {scene.name}");
            
            // 씬 내 모든 오브젝트를 검사 (비활성 포함)
            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (var go in allObjects)
            {
                if (go == null) continue;
                if (go.hideFlags != HideFlags.None) continue;
                if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
                if (!go.name.StartsWith("Zone_")) continue;
                if (!go.activeInHierarchy) continue;
                
                string[] parts = go.name.Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[1], out int zoneNum))
                {
                    Debug.Log($"[GameOverUI] Found active Zone: {go.name} → Zone {zoneNum}");
                    if (zoneNum > maxZone)
                        maxZone = zoneNum;
                }
            }
            
            Debug.Log($"[GameOverUI] FindCurrentActiveZone result: {maxZone}");
            return maxZone;
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
