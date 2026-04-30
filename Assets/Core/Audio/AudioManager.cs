using System;
using UnityEngine;

namespace HideAndInk.Core.Audio
{
    /// <summary>
    /// 오디오 관리자 Singleton
    /// BGM/SFX 볼륨, 음소거를 관리하고 PlayerPrefs에 저장
    /// GameManager 오브젝트에 붙여서 사용 (DontDestroyOnLoad)
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<AudioManager>();
                    if (_instance == null)
                    {
                        var go = new GameObject("AudioManager");
                        _instance = go.AddComponent<AudioManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        [Header("오디오 소스")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("기본 볼륨")]
        [SerializeField][Range(0f, 1f)] private float defaultBgmVolume = 0.5f;
        [SerializeField][Range(0f, 1f)] private float defaultSfxVolume = 0.5f;

        // PlayerPrefs 키
        private const string PREFS_BGM_VOLUME = "Audio_BGM_Volume";
        private const string PREFS_SFX_VOLUME = "Audio_SFX_Volume";
        private const string PREFS_BGM_MUTE = "Audio_BGM_Mute";
        private const string PREFS_SFX_MUTE = "Audio_SFX_Mute";

        // 현재 값
        private float _bgmVolume;
        private float _sfxVolume;
        private bool _bgmMute;
        private bool _sfxMute;

        // 이벤트
        public event System.Action<float> OnBgmVolumeChanged;
        public event Action<float> OnSfxVolumeChanged;
        public event Action<bool> OnBgmMuteChanged;
        public event Action<bool> OnSfxMuteChanged;

        // 프로퍼티
        public float BgmVolume
        {
            get => _bgmVolume;
            set
            {
                _bgmVolume = Mathf.Clamp01(value);
                if (bgmSource != null)
                {
                    bgmSource.volume = _bgmVolume;
                }
                PlayerPrefs.SetFloat(PREFS_BGM_VOLUME, _bgmVolume);
                OnBgmVolumeChanged?.Invoke(_bgmVolume);
            }
        }

        public float SfxVolume
        {
            get => _sfxVolume;
            set
            {
                _sfxVolume = Mathf.Clamp01(value);
                if (sfxSource != null)
                {
                    sfxSource.volume = _sfxVolume;
                }
                PlayerPrefs.SetFloat(PREFS_SFX_VOLUME, _sfxVolume);
                OnSfxVolumeChanged?.Invoke(_sfxVolume);
            }
        }

        public bool BgmMute
        {
            get => _bgmMute;
            set
            {
                _bgmMute = value;
                if (bgmSource != null)
                {
                    bgmSource.mute = _bgmMute;
                }
                PlayerPrefs.SetInt(PREFS_BGM_MUTE, _bgmMute ? 1 : 0);
                OnBgmMuteChanged?.Invoke(_bgmMute);
            }
        }

        public bool SfxMute
        {
            get => _sfxMute;
            set
            {
                _sfxMute = value;
                if (sfxSource != null)
                {
                    sfxSource.mute = _sfxMute;
                }
                PlayerPrefs.SetInt(PREFS_SFX_MUTE, _sfxMute ? 1 : 0);
                OnSfxMuteChanged?.Invoke(_sfxMute);
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // AudioSource가 없으면 자동 생성
            if (bgmSource == null)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
            }
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
            }

            LoadSettings();
        }

        /// <summary>
        /// PlayerPrefs에서 저장된 설정 불러오기
        /// </summary>
        private void LoadSettings()
        {
            _bgmVolume = PlayerPrefs.GetFloat(PREFS_BGM_VOLUME, defaultBgmVolume);
            _sfxVolume = PlayerPrefs.GetFloat(PREFS_SFX_VOLUME, defaultSfxVolume);
            _bgmMute = PlayerPrefs.GetInt(PREFS_BGM_MUTE, 0) == 1;
            _sfxMute = PlayerPrefs.GetInt(PREFS_SFX_MUTE, 0) == 1;

            // AudioSource 적용
            if (bgmSource != null)
            {
                bgmSource.volume = _bgmVolume;
                bgmSource.mute = _bgmMute;
            }
            if (sfxSource != null)
            {
                sfxSource.volume = _sfxVolume;
                sfxSource.mute = _sfxMute;
            }
        }

        /// <summary>
        /// BGM 재생
        /// </summary>
        public void PlayBgm(AudioClip clip)
        {
            if (bgmSource == null || clip == null) return;
            bgmSource.clip = clip;
            bgmSource.Play();
        }

        /// <summary>
        /// SFX 재생 (겹침 재생 지원)
        /// </summary>
        public void PlaySfx(AudioClip clip)
        {
            if (sfxSource == null || clip == null) return;
            sfxSource.PlayOneShot(clip);
        }

        /// <summary>
        /// 설정 리셋 (기본값으로)
        /// </summary>
        public void ResetToDefaults()
        {
            BgmVolume = defaultBgmVolume;
            SfxVolume = defaultSfxVolume;
            BgmMute = false;
            SfxMute = false;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                PlayerPrefs.Save();
                _instance = null;
            }
        }
    }
}
