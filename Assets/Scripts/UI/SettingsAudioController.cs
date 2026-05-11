using UnityEngine;
using UnityEngine.UI;
using HideAndInk.Core.Audio;

namespace HideAndInk.Scripts.UI
{
    /// <summary>
    /// 타이틀 씬 설정 팝업의 오디오 컨트롤러
    /// Popup_Setting 오브젝트에 추가하여 사용
    /// </summary>
    public class SettingsAudioController : MonoBehaviour
    {
        [Header("Slider")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("Toggle")]
        [SerializeField] private Toggle bgmMuteToggle;
        [SerializeField] private Toggle sfxMuteToggle;

        private BgmManager _bgm;
        private SfxManager _sfx;
        
        // 타이틀 씬의 실제 BGM AudioSource (BGM_Manager 오브젝트)
        private AudioSource _bgmAudioSource;

        private void Awake()
        {
            // Singleton 인스턴스 찾기
            _bgm = BgmManager.Instance;
            _sfx = SfxManager.Instance;
            
            // 타이틀 씬의 실제 BGM AudioSource 찾기 (BGM_Manager 오브젝트)
            var bgmManagerObj = GameObject.Find("BGM_Manager");
            if (bgmManagerObj != null)
                _bgmAudioSource = bgmManagerObj.GetComponent<AudioSource>();
            else
                _bgmAudioSource = FindObjectOfType<AudioSource>(); // fallback

            Debug.Log($"[SettingsAudioController] Awake: _bgm={_bgm != null}, _sfx={_sfx != null}, audioSource={_bgmAudioSource != null}");

            // Inspector 미연결 시 자동으로 찾기
            if (bgmSlider == null)
                bgmSlider = transform.Find("BGM_Slider")?.GetComponentInChildren<Slider>();
            if (sfxSlider == null)
                sfxSlider = transform.Find("FX_Slider")?.GetComponentInChildren<Slider>();
            if (bgmMuteToggle == null)
                bgmMuteToggle = transform.Find("BGMToggle")?.GetComponent<Toggle>();
            if (sfxMuteToggle == null)
                sfxMuteToggle = transform.Find("FXToggle")?.GetComponent<Toggle>();

            Debug.Log($"[SettingsAudioController] UI: bgmSlider={bgmSlider != null}, sfxSlider={sfxSlider != null}, bgmToggle={bgmMuteToggle != null}, sfxToggle={sfxMuteToggle != null}");

            // 이벤트 연결
            if (bgmSlider != null)
            {
                bgmSlider.onValueChanged.RemoveAllListeners();
                bgmSlider.onValueChanged.AddListener(OnBgmVolumeChanged);
                Debug.Log("[SettingsAudioController] BGM Slider 이벤트 연결 완료");
            }
            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveAllListeners();
                sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            }
            if (bgmMuteToggle != null)
            {
                bgmMuteToggle.onValueChanged.RemoveAllListeners();
                bgmMuteToggle.onValueChanged.AddListener(OnBgmMuteChanged);
            }
            if (sfxMuteToggle != null)
            {
                sfxMuteToggle.onValueChanged.RemoveAllListeners();
                sfxMuteToggle.onValueChanged.AddListener(OnSfxMuteChanged);
            }
        }

        private void OnEnable()
        {
            // 팝업이 열릴 때마다 현재 상태 동기화
            SyncUI();
        }

        private void SyncUI()
        {
            // BGM Slider 초기값: 실제 AudioSource 우선, 없으면 BgmManager
            float bgmVol = _bgmAudioSource != null ? _bgmAudioSource.volume : (_bgm != null ? _bgm.Volume : 0.5f);
            bool bgmMuted = _bgmAudioSource != null ? _bgmAudioSource.mute : (_bgm != null ? _bgm.Muted : false);
            
            if (bgmSlider != null)
                bgmSlider.SetValueWithoutNotify(bgmVol);
            if (_sfx != null && sfxSlider != null)
                sfxSlider.SetValueWithoutNotify(_sfx.Volume);
            if (bgmMuteToggle != null)
                bgmMuteToggle.SetIsOnWithoutNotify(bgmMuted);
            if (_sfx != null && sfxMuteToggle != null)
                sfxMuteToggle.SetIsOnWithoutNotify(_sfx.Muted);
        }

        private void OnBgmVolumeChanged(float value)
        {
            if (_bgm != null)
                _bgm.Volume = value;
            // 실제 BGM AudioSource도 함께 제어
            if (_bgmAudioSource != null)
                _bgmAudioSource.volume = value;
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (_sfx != null)
                _sfx.Volume = value;
        }

        private void OnBgmMuteChanged(bool isOn)
        {
            if (_bgm != null)
                _bgm.Muted = isOn;
            // 실제 BGM AudioSource도 함께 제어
            if (_bgmAudioSource != null)
                _bgmAudioSource.mute = isOn;
        }

        private void OnSfxMuteChanged(bool isOn)
        {
            if (_sfx != null)
                _sfx.Muted = isOn;
        }
    }
}
