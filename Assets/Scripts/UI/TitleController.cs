using HideAndInk.Core.Transition;
using System.Collections;
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
    /// - fadeInObj: FadeInObj 프리팹 인스턴스 (Animator 기반, 선택)
    /// 
    /// 버튼 OnClick 연결:
    /// - Btn_NewGame → TitleController.OnNewGameClicked
    /// - Btn_ExitGame → TitleController.OnExitGameClicked
    /// - Btn_Setting  → TitleController.OnSettingClicked
    /// - Btn_ExitPopup → TitleController.OnExitPopupClicked
    /// 
    /// 진입 트랜지션:
    /// - useEntryTransition = true: PatternTransitionController.PlayOut() (셰이더)
    /// - FadeInObj 할당 시: Animator 기반 스프라이트 트랜지션
    /// </summary>
    public sealed class TitleController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("설정 팝업 오브젝트 (Popup_Setting)")]
        [SerializeField] private GameObject popupSetting;

        [Tooltip("FadeInObj (Animator 기반 스프라이트 트랜지션, 선택 사항)")]
        [SerializeField] private GameObject fadeInObj;

        [Header("Transition Settings")]
        [Tooltip("씬 진입 시 PatternTransitionController로 PlayOut (권장)")]
        [SerializeField] private bool useEntryTransition = true;

        [Tooltip("씬 전환 시 Shader_PatternTransition 사용")]
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

            // FadeInObj가 있다면 처음에는 비활성화 (애니메이션 동기화 문제 방지)
            if (fadeInObj != null)
                fadeInObj.SetActive(false);
        }

        private void Start()
        {
            // PatternTransitionController 인스턴스 캐싱
            _transition = PatternTransitionController.Instance;

            // 씬 진입 트랜지션 실행
            StartCoroutine(PlayEntryTransition());
        }

        /// <summary>
        /// 씬 진입 시 트랜지션 효과를 재생합니다.
        /// useEntryTransition + FadeInObj 둘 다 설정 가능 (순차 재생).
        /// </summary>
        private IEnumerator PlayEntryTransition()
        {
            // 1프레임 대기 (모든 컴포넌트 초기화 완료)
            yield return null;

            // ---- Phase 1: PatternTransitionController 셰이더 트랜지션 ----
            if (useEntryTransition && _transition != null)
            {
                // 씬 로드 중에 이미 보일 수 있으므로 즉시 덮고 시작
                _transition.SetFull();
                yield return null;

                // 화면 걷기 (0.5 → 1.0)
                _transition.PlayOut();

                // 트랜지션 완료 대기 (duration + 여유)
                float waitTime = (_transition != null) ? 0.6f : 0f;
                yield return new WaitForSecondsRealtime(waitTime);
            }

            // ---- Phase 2: FadeInObj Animator 트랜지션 ----
            if (fadeInObj != null)
            {
                fadeInObj.SetActive(true);

                var animator = fadeInObj.GetComponent<Animator>();
                if (animator != null)
                {
                    // 애니메이터 컨트롤러가 제대로 설정되어 있으면
                    // SetActive(true) 시 자동으로 Default State(Transition_Enter) 재생
                    // 명시적으로 처음부터 재생
                    animator.Play("Transition_Enter", 0, 0f);

                    // 애니메이션 길이만큼 대기 후 오브젝트 정리
                    AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                    float animLength = stateInfo.length;
                    if (animLength <= 0f) animLength = 1f; // 안전장치

                    yield return new WaitForSecondsRealtime(animLength + 0.1f);
                }

                // 애니메이션 완료 후 FadeInObj 비활성화 (필요시 파괴)
                fadeInObj.SetActive(false);
            }
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
