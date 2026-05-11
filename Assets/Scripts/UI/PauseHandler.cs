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

        private UnityEngine.UI.Slider _draggingSlider; // Time.timeScale=0에서 드래그 중인 Slider 추적

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
            // Time.timeScale = 0일 때 EventSystem이 멈추므로 직접 입력 처리
            if (Time.timeScale == 0f && pausePopup != null && pausePopup.activeSelf)
            {
                HandlePausePopupInput();
            }
        }

        /// <summary>
        /// Time.timeScale = 0 상태에서 팝업 입력 처리 (클릭 + 드래그)
        /// </summary>
        private void HandlePausePopupInput()
        {
            // 드래그 중이면 계속 Slider 업데이트
            if (_draggingSlider != null)
            {
                if (Input.GetMouseButton(0))
                {
                    UpdateSliderValue(_draggingSlider);
                }
                else
                {
                    _draggingSlider = null; // 마우스 놓음
                }
                return;
            }

            // 클릭 시작 시에만 Raycast
            if (!Input.GetMouseButtonDown(0)) return;

            var canvas = pausePopup.GetComponentInParent<UnityEngine.Canvas>();
            if (canvas == null) return;
            
            var raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null) return;
            
            var pointerEventData = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            pointerEventData.position = Input.mousePosition;
            
            var results = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            raycaster.Raycast(pointerEventData, results);
            
            foreach (var result in results)
            {
                var go = result.gameObject;
                Debug.Log($"[PauseHandler] Raycast hit: {go.name}");
                
                if (go.name == "Btn_Continue")
                {
                    ResumeGame();
                    return;
                }
                else if (go.name == "Btn_GoTitle")
                {
                    GoToTitleScene();
                    return;
                }
                else if (go.name == "Btn_Close")
                {
                    ResumeGame();
                    return;
                }
                
                // Toggle 먼저 체크 (자식 오브젝트 클릭 시에도 parent Toggle 찾기)
                var toggle = go.GetComponentInParent<UnityEngine.UI.Toggle>();
                if (toggle != null)
                {
                    toggle.isOn = !toggle.isOn;
                    toggle.onValueChanged?.Invoke(toggle.isOn);
                    Debug.Log($"[PauseHandler] Toggle clicked: {toggle.gameObject.name}, isOn={toggle.isOn}");
                    return;
                }
                
                // Slider 체크 (자식 오브젝트 클릭 시에도 parent Slider 찾기)
                var slider = go.GetComponentInParent<UnityEngine.UI.Slider>();
                if (slider != null)
                {
                    _draggingSlider = slider;
                    UpdateSliderValue(slider);
                    Debug.Log($"[PauseHandler] Slider clicked: {slider.gameObject.name}, value={slider.value}");
                    return;
                }
            }
        }

        /// <summary>
        /// Time.timeScale = 0 상태에서 Slider 값 업데이트 (클릭/드래그 공용)
        /// </summary>
        private void UpdateSliderValue(UnityEngine.UI.Slider slider)
        {
            var rectTransform = slider.GetComponent<RectTransform>();
            if (rectTransform == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, Input.mousePosition, null, out Vector2 localPoint);

            float normalizedValue = Mathf.InverseLerp(
                rectTransform.rect.xMin, rectTransform.rect.xMax, localPoint.x);
            
            float newValue = Mathf.Clamp01(normalizedValue);
            if (Mathf.Abs(slider.value - newValue) > 0.001f)
            {
                slider.value = newValue;
                slider.onValueChanged?.Invoke(slider.value);
            }
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
        /// Button/Slider/Toggle과 연결되지 않은 Image는 RaycastTarget OFF로 설정
        /// </summary>
        private void DisablePopupRaycastBlockers()
        {
            var popupTransform = pausePopup.transform;
            var images = popupTransform.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            int blockedCount = 0;
            
            foreach (var img in images)
            {
                // 버튼과 연결된 Image는 제외
                var parentButton = img.GetComponentInParent<UnityEngine.UI.Button>();
                if (parentButton != null) continue;
                
                // Slider와 연결된 Image는 제외 (Handle, Fill, Background 등)
                var parentSlider = img.GetComponentInParent<UnityEngine.UI.Slider>();
                if (parentSlider != null) continue;
                
                // Toggle과 연결된 Image는 제외 (Background, Checkmark 등)
                var parentToggle = img.GetComponentInParent<UnityEngine.UI.Toggle>();
                if (parentToggle != null) continue;
                
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
