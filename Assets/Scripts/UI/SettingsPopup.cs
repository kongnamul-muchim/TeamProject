using HideAndInk.Core.Audio;
using HideAndInk.Core.Transition;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndInk.Scripts.UI
{
    /// <summary>
    /// 일시정지 팝업 내 설정
    /// Btn_GoTitle / Toggle / Slider → 인스펙터에서 직접 연결
    /// 
    /// 타이틀 복귀:
    /// - FadeInObj 프리팹 (1순위) → TitleController와 동일한 2페이즈 전환
    /// - PatternTransitionController (2순위)
    /// - 바로 로드 (3순위)
    /// - titleSceneBuildIndex (우선) / titleSceneName (fallback)
    /// </summary>
    public sealed class SettingsPopup : MonoBehaviour
    {
        /// <summary>
        /// FadeInObj 트랜지션으로 타이틀 씬에 진입 중이면 true.
        /// TitleController가 entry transition을 중복 실행하지 않도록 스킵하는 용도.
        /// </summary>
        public static bool IsFadeInTransitionActive { get; set; }

        [Header("타이틀 화면")]
        [Tooltip("타이틀 씬 Build Index (기본 0, 우선 사용)")]
        [SerializeField] private int titleSceneBuildIndex = 0;

        [Tooltip("타이틀 씬 이름 (Build Index 무효 시 fallback)")]
        [SerializeField] private string titleSceneName = "0.TitleScene";

        [Header("출구 전환 설정")]
        [Tooltip("FadeInObj 프리팹 (지정 시 1순위 전환으로 사용)")]
        [SerializeField] private GameObject fadeInObjPrefab;

        [Tooltip("타이틀 복귀 시 PatternTransition 사용 (2순위)")]
        [SerializeField] private bool useSceneTransition = true;

        private AudioManager _audio;

        private void Awake()
        {
            _audio = AudioManager.Instance;
        }

        /// <summary>
        /// Btn_GoTitle → 인스펙터 OnClick 연결
        /// 전환 순서: FadeInObj 프리팹 > PatternTransition > 즉시 로드
        /// </summary>
        public void OnGoTitleClicked()
        {
            Time.timeScale = 1f;

            // 1순위: FadeInObj 프리팹 (TitleController와 동일한 2페이즈 전환)
            if (fadeInObjPrefab != null)
            {
                var fadeObj = Instantiate(fadeInObjPrefab);
                var controller = fadeObj.GetComponent<FadeInObjController>();
                if (controller != null)
                {
                    IsFadeInTransitionActive = true; // TitleController가 entry transition 스킵
                    controller.PlayCoverAndTransition(titleSceneBuildIndex);
                    return;
                }
                else
                {
                    Debug.LogWarning("[SettingsPopup] FadeInObj 프리팹에 FadeInObjController가 없습니다. PatternTransition으로 fallback.");
                    Destroy(fadeObj);
                }
            }

            // 2순위: PatternTransitionController
            var transition = PatternTransitionController.Instance;
            if (useSceneTransition && transition != null)
            {
                transition.PlayIn(() =>
                {
                    DontDestroyOnLoad(transition.gameObject);

                    SceneManager.sceneLoaded += OnTitleSceneLoaded;
                    LoadTitleScene();
                });
                return;
            }

            // 3순위: 바로 로드
            LoadTitleScene();
        }

        private void LoadTitleScene()
        {
            // Build Index 우선
            if (titleSceneBuildIndex >= 0 && titleSceneBuildIndex < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(titleSceneBuildIndex);
                return;
            }

            // fallback: 씬 이름
            if (!string.IsNullOrEmpty(titleSceneName))
            {
                SceneManager.LoadScene(titleSceneName);
                return;
            }

            Debug.LogError("[SettingsPopup] 타이틀 씬을 로드할 방법이 없습니다. Build Index 또는 Scene Name을 설정하세요.");
        }

        private void OnTitleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnTitleSceneLoaded;

            var transition = PatternTransitionController.Instance;
            if (transition != null)
                transition.PlayOut();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnTitleSceneLoaded;
        }

        // =====================================================
        // 아래 메서드들은 Toggle/Slider → 인스펙터 OnValueChanged 연결용
        // =====================================================

        public void SetBgmMute(bool isOn) { if (_audio != null) _audio.BgmMute = !isOn; }
        public void SetSfxMute(bool isOn) { if (_audio != null) _audio.SfxMute = !isOn; }
        public void SetBgmVolume(float value) { if (_audio != null) _audio.BgmVolume = value; }
        public void SetSfxVolume(float value) { if (_audio != null) _audio.SfxVolume = value; }
    }
}
