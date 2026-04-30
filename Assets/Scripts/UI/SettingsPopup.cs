using HideAndInk.Core.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HideAndInk.Scripts.UI
{
    /// <summary>
    /// 일시정지 팝업 내 설정
    /// Btn_GoTitle / Toggle / Slider → 인스펙터에서 직접 연결
    /// </summary>
    public sealed class SettingsPopup : MonoBehaviour
    {
        [Header("타이틀 화면")]
        [Tooltip("타이틀 씬 이름 (인스펙터에서 설정)")]
        [SerializeField] private string titleSceneName = "Title";

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

            if (string.IsNullOrEmpty(titleSceneName))
            {
                Debug.LogError("[SettingsPopup] titleSceneName이 설정되지 않았습니다.");
                return;
            }

            SceneManager.LoadScene(titleSceneName);
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
