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
                
            // 팝업 버튼 자동 연결
            ConnectPopupButtons();
        }
    
    /// <summary>
    /// Popup_Pause 안의 모든 Button을 찾아서 리스너 연결
    /// </summary>
    private void ConnectPopupButtons()
    {
        if (pausePopup == null) return;
        
        var buttons = pausePopup.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        foreach (var btn in buttons)
        {
            btn.onClick.RemoveAllListeners();
            
            if (btn.gameObject.name == "Btn_Continue")
            {
                btn.onClick.AddListener(ResumeGame);
                Debug.Log("[PauseHandler] Btn_Continue → ResumeGame 연결 완료");
            }
            else if (btn.gameObject.name == "Btn_GoTitle")
            {
                btn.onClick.AddListener(GoToTitleScene);
                Debug.Log("[PauseHandler] Btn_GoTitle → GoToTitleScene 연결 완료");
            }
        }
    }
    
    /// <summary>
    /// 타이틀 화면으로 돌아가기
    /// </summary>
    public void GoToTitleScene()
    {
        _sfxService?.Play(SfxId.ButtonClick);
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
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
                if (pausePopup != null)
                {
                    pausePopup.SetActive(true);
                    pausePopup.transform.SetAsLastSibling();
                    DisablePopupRaycastBlockers();
                }
            }
            else if (previous == GameState.Paused)
            {
                Time.timeScale = 1f;
                if (pausePopup != null) pausePopup.SetActive(false);
            }
        }

        private void Update()
        {
            // Time.timeScale = 0일 때 EventSystem이 멈추므로 직접 클릭 체크
            if (Time.timeScale == 0f && pausePopup != null && pausePopup.activeSelf)
            {
                CheckPopupButtonClicks();
            }
        }

        /// <summary>
        /// Time.timeScale = 0 상태에서 팝업 버튼 클릭을 직접 체크
        /// GraphicRaycaster 사용
        /// </summary>
        private void CheckPopupButtonClicks()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            // GraphicRaycaster로 정확히 어떤 UI가 클릭되는지 확인
            var canvas = pausePopup.GetComponentInParent<UnityEngine.Canvas>();
            if (canvas == null) return;
            
            var raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null) return;
            
            var pointerEventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            pointerEventData.position = Input.mousePosition;
            
            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            raycaster.Raycast(pointerEventData, results);
            
            Debug.Log($"[PauseHandler] Raycast 결과 수: {results.Count}");
            
                foreach (var result in results)
                {
                    var go = result.gameObject;
                    Debug.Log($"[PauseHandler] Raycast: {go.name}");
                    
                    if (go.name == "Btn_Continue")
                    {
                        Debug.Log("[PauseHandler] Btn_Continue GraphicRaycast 클릭 감지!");
                        ResumeGame();
                        return;
                    }
                    else if (go.name == "Btn_GoTitle")
                    {
                        Debug.Log("[PauseHandler] Btn_GoTitle GraphicRaycast 클릭 감지!");
                        GoToTitleScene();
                        return;
                    }
                    else if (go.name == "Btn_Close")
                    {
                        Debug.Log("[PauseHandler] Btn_Close GraphicRaycast 클릭 감지!");
                        ResumeGame();
                        return;
                    }
                    else if (go.name == "BGMToggle" || go.name == "FXToggle" || 
                             go.name == "Checkmark")
                    {
                        // Toggle 영역 클릭 (Background는 Slider와 겹칠 수 있으므로 제외)
                        var toggle = go.GetComponentInParent<UnityEngine.UI.Toggle>();
                        if (toggle == null) toggle = go.GetComponent<UnityEngine.UI.Toggle>();
                        if (toggle != null)
                        {
                            toggle.isOn = !toggle.isOn;
                            toggle.onValueChanged?.Invoke(toggle.isOn);
                            return;
                        }
                    }
                    else if (go.name == "BGM_Slider" || go.name == "FX_Slider" || 
                             go.name == "Handle" || go.name == "Fill" || go.name == "Background")
                    {
                        // Slider 영역 클릭
                        var slider = go.GetComponentInParent<UnityEngine.UI.Slider>();
                        if (slider == null) slider = go.GetComponent<UnityEngine.UI.Slider>();
                        if (slider != null)
                        {
                            HandleSliderClick(slider);
                            return;
                        }
                    }
                }
        }

        /// <summary>
        /// Time.timeScale = 0 상태에서 Slider 클릭 처리
        /// </summary>
        private void HandleSliderClick(UnityEngine.UI.Slider slider)
        {
            var rectTransform = slider.GetComponent<RectTransform>();
            if (rectTransform == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, Input.mousePosition, null, out Vector2 localPoint);

            float normalizedValue = Mathf.InverseLerp(
                rectTransform.rect.xMin, rectTransform.rect.xMax, localPoint.x);
            
            slider.value = Mathf.Clamp01(normalizedValue);
            slider.onValueChanged?.Invoke(slider.value);
        }

        /// <summary>
        /// 마우스가 버튼 RectTransform 영역 안에 있는지 체크
        /// </summary>
        private bool IsMouseOverButton(Transform buttonTransform)
        {
            var rectTransform = buttonTransform.GetComponent<RectTransform>();
            if (rectTransform == null) return false;

            Vector2 localMousePosition;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                Input.mousePosition,
                null,
                out localMousePosition);

            return rectTransform.rect.Contains(localMousePosition);
        }

        /// <summary>
        /// Popup_Pause 안의 모든 Image RaycastTarget을 확인하고,
        /// Button과 연결되지 않은 Image는 RaycastTarget OFF로 설정
        /// </summary>
        private void DisablePopupRaycastBlockers()
        {
            var popupTransform = pausePopup.transform;
            var images = popupTransform.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            int blockedCount = 0;
            
            foreach (var img in images)
            {
                // 버튼과 연결된 Image는 제외 (버튼 클릭을 막지 않도록)
                var parentButton = img.GetComponentInParent<UnityEngine.UI.Button>();
                if (parentButton != null) continue;
                
                // RaycastTarget이 켜져 있으면 OFF
                if (img.raycastTarget)
                {
                    img.raycastTarget = false;
                    blockedCount++;
                    Debug.Log($"[PauseHandler] RaycastTarget OFF: {img.gameObject.name}");
                }
            }
            
            Debug.Log($"[PauseHandler] 총 {blockedCount}개 Image의 RaycastTarget을 OFF로 설정");
        }
    }
}
