using HideAndInk.Core.Audio;
using HideAndInk.Core.Interfaces;
using HideAndInk.Core.Managers;
using HideAndInk.Core.Transition;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndInk.Scripts.UI
{
    public sealed class SettingsPopup : MonoBehaviour
    {
        public static bool IsFadeInTransitionActive { get; set; }

        [Header("타이틀 화면")]
        [SerializeField] private int titleSceneBuildIndex = 0;
        [SerializeField] private string titleSceneName = "0.TitleScene";

        [Header("출구 전환 설정")]
        [SerializeField] private GameObject fadeInObjPrefab;
        [SerializeField] private bool useSceneTransition = true;

        [Header("Audio UI")]
        [SerializeField] private UnityEngine.UI.Slider bgmVolumeSlider;
        [SerializeField] private UnityEngine.UI.Slider sfxVolumeSlider;
        [SerializeField] private UnityEngine.UI.Toggle bgmMuteToggle;
        [SerializeField] private UnityEngine.UI.Toggle sfxMuteToggle;

        private ISfxService _sfx;
        private IBgmService _bgm;

        private void OnEnable()
        {
            _sfx = SfxManager.Instance;
            _bgm = BgmManager.Instance;

            if (_bgm != null && _bgm.Volume <= 0f)
                _bgm.Volume = 0.5f;
            if (_sfx != null && _sfx.Volume <= 0f)
                _sfx.Volume = 0.5f;

            // Slider/Toggle 초기값 동기화
            if (_bgm != null && bgmVolumeSlider != null)
                bgmVolumeSlider.SetValueWithoutNotify(_bgm.Volume);
            if (_sfx != null && sfxVolumeSlider != null)
                sfxVolumeSlider.SetValueWithoutNotify(_sfx.Volume);
            if (_bgm != null && bgmMuteToggle != null)
                bgmMuteToggle.SetIsOnWithoutNotify(_bgm.Muted);
            if (_sfx != null && sfxMuteToggle != null)
                sfxMuteToggle.SetIsOnWithoutNotify(_sfx.Muted);

            // 코드에서 직접 이벤트 연결 (Inspector 연결 불필요)
            if (bgmVolumeSlider != null)
                bgmVolumeSlider.onValueChanged.AddListener(SetBgmVolume);
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.AddListener(SetSfxVolume);
            if (bgmMuteToggle != null)
                bgmMuteToggle.onValueChanged.AddListener(SetBgmMute);
            if (sfxMuteToggle != null)
                sfxMuteToggle.onValueChanged.AddListener(SetSfxMute);
        }

        private void OnDisable()
        {
            // 이벤트 해제 (메모리 누수 방지)
            if (bgmVolumeSlider != null)
                bgmVolumeSlider.onValueChanged.RemoveListener(SetBgmVolume);
            if (sfxVolumeSlider != null)
                sfxVolumeSlider.onValueChanged.RemoveListener(SetSfxVolume);
            if (bgmMuteToggle != null)
                bgmMuteToggle.onValueChanged.RemoveListener(SetBgmMute);
            if (sfxMuteToggle != null)
                sfxMuteToggle.onValueChanged.RemoveListener(SetSfxMute);
        }

        public void OnGoTitleClicked()
        {
            _sfx?.Play(SfxId.ButtonClick);
            Time.timeScale = 1f;

            if (fadeInObjPrefab != null)
            {
                var fadeObj = Instantiate(fadeInObjPrefab);
                var controller = fadeObj.GetComponent<FadeInObjController>();
                if (controller != null)
                {
                    IsFadeInTransitionActive = true;
                    controller.PlayCoverAndTransition(titleSceneBuildIndex);
                    return;
                }
                else
                {
                    Debug.LogWarning("[SettingsPopup] FadeInObj 프리팹에 FadeInObjController가 없습니다. PatternTransition으로 fallback.");
                    Destroy(fadeObj);
                }
            }

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

            LoadTitleScene();
        }

        private void LoadTitleScene()
        {
            if (titleSceneBuildIndex >= 0 && titleSceneBuildIndex < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(titleSceneBuildIndex);
                return;
            }

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

        public void SetBgmMute(bool isOn) { if (_bgm != null) _bgm.Muted = isOn; }
        public void SetSfxMute(bool isOn) { if (_sfx != null) _sfx.Muted = isOn; }
        public void SetBgmVolume(float value) { if (_bgm != null) _bgm.Volume = value; }
        public void SetSfxVolume(float value) { if (_sfx != null) _sfx.Volume = value; }
    }
}
