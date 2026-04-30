using HideAndInk.Core.Transition;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndInk.Scripts.UI
{
    /// <summary>
    /// 타이틀 화면 전체를 제어하는 컨트롤러.
    /// Canvas 오브젝트에 부착하여 사용합니다.
    /// 
    /// 연결 가이드 (Inspector):
    /// - popupSetting: Popup_Setting 오브젝트
    /// - fadeInObj: FadeInObj 프리팹 인스턴스 (선택)
    /// 
    /// 버튼 OnClick 연결:
    /// - Btn_NewGame → TitleController.OnNewGameClicked
    /// - Btn_ExitGame → TitleController.OnExitGameClicked
    /// - Btn_Setting  → TitleController.OnSettingClicked
    /// - Btn_ExitPopup → TitleController.OnExitPopupClicked
    /// </summary>
    public sealed class TitleController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("설정 팝업 오브젝트 (Popup_Setting)")]
        [SerializeField] private GameObject popupSetting;

        [Tooltip("FadeInObj 프리팹 인스턴스 (씬 진입 시 1회 재생)")]
        [SerializeField] private GameObject fadeInObj;

        [Header("Transition Settings")]
        [Tooltip("씬 진입 시 FadeInObj 재생 여부")]
        [SerializeField] private bool useEntryFadeIn = true;

        [Tooltip("씬 전환 시 Shader_PatternTransition 사용 여부")]
        [SerializeField] private bool useSceneTransition = true;

        [Header("Scene Config")]
        [Tooltip("새 게임 시작 시 로드할 씬의 Build Index (기본: 1 = Title 다음 씬)")]
        [SerializeField] private int newGameSceneIndex = 1;

        private PatternTransitionController _transition;
        private bool _isTransitioning = false;

        private void Awake()
        {
            // 팝업 초기 상태: 닫힘
            if (popupSetting != null)
                popupSetting.SetActive(false);

            // 씬 진입 시 FadeInObj 재생
            if (useEntryFadeIn && fadeInObj != null)
            {
                var animator = fadeInObj.GetComponent<Animator>();
                if (animator != null)
                {
                    animator.Play("Transition_Enter", 0, 0f);
                }
            }
        }

        private void Start()
        {
            // PatternTransitionController 인스턴스 캐싱
            _transition = PatternTransitionController.Instance;
        }

        // =====================================================
        // Button Callbacks (Inspector OnClick 연결)
        // =====================================================

        /// <summary>
        /// Btn_NewGame → 인스펙터 OnClick 연결
        /// </summary>
        public void OnNewGameClicked()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            if (useSceneTransition && _transition != null)
            {
                _transition.PlayIn(() =>
                {
                    // 트랜지션 컨트롤러를 씬 전환 후에도 살려둠
                    DontDestroyOnLoad(_transition.gameObject);

                    // 씬 로드 완료 후 PlayOut
                    SceneManager.sceneLoaded += OnSceneLoaded;
                    SceneManager.LoadScene(newGameSceneIndex);
                });
            }
            else
            {
                SceneManager.LoadScene(newGameSceneIndex);
            }
        }

        /// <summary>
        /// Btn_ExitGame → 인스펙터 OnClick 연결
        /// </summary>
        public void OnExitGameClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// Btn_Setting → 인스펙터 OnClick 연결
        /// </summary>
        public void OnSettingClicked()
        {
            if (popupSetting != null)
            {
                bool isActive = popupSetting.activeSelf;
                popupSetting.SetActive(!isActive);
            }
        }

        /// <summary>
        /// Btn_ExitPopup → 인스펙터 OnClick 연결
        /// </summary>
        public void OnExitPopupClicked()
        {
            if (popupSetting != null)
                popupSetting.SetActive(false);
        }

        // =====================================================
        // Scene Transition Callbacks
        // =====================================================

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (_transition != null)
            {
                _transition.PlayOut();
            }

            _isTransitioning = false;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }
}
