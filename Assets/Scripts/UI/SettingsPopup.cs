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
    /// - titleSceneBuildIndex (우선) / titleSceneName (fallback)
    /// - PatternTransitionController.Instance 가 있으면 트랜지션 재생 후 로드
    /// - 없으면 즉시 로드
    /// </summary>
    public sealed class SettingsPopup : MonoBehaviour
    {
        [Header("타이틀 화면")]
        [Tooltip("타이틀 씬 Build Index (기본 0, 우선 사용)")]
        [SerializeField] private int titleSceneBuildIndex = 0;

        [Tooltip("타이틀 씬 이름 (Build Index 무효 시 fallback)")]
        [SerializeField] private string titleSceneName = "0.TitleScene";

        [Tooltip("타이틀 복귀 시 PatternTransition 사용")]
        [SerializeField] private bool useSceneTransition = true;

        private AudioManager _audio;

        private void Awake()
        {
            _audio = AudioManager.Instance;
        }

        /// <summary>
        /// Btn_GoTitle → 인스펙터 OnClick 연결
        /// </summary>
        public void OnGoTitleClicked()
        {
            Time.timeScale = 1f;

            // PatternTransitionController가 있으면 트랜지션 재생
            var transition = PatternTransitionController.Instance;
            if (useSceneTransition && transition != null)
            {
                transition.PlayIn(() =>
                {
                    DontDestroyOnLoad(transition.gameObject);

                    SceneManager.sceneLoaded += OnTitleSceneLoaded;
                    LoadTitleScene();
                });
            }
            else
            {
                LoadTitleScene();
            }
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
