using System;
using System.Collections;
using System.Collections.Generic;
using HideAndInk.Core.Interfaces;
using UnityEngine;

namespace HideAndInk.Core.Audio
{
    [Serializable]
    public struct BgmEntry
    {
        public BgmId id;
        public AudioClip clip;
    }

    public sealed class BgmManager : MonoBehaviour, IBgmService
    {
        public static BgmManager Instance { get; private set; }

        [SerializeField] private BgmEntry[] bgmClips;
        [SerializeField][Range(0f, 1f)] private float defaultVolume = 0.8f;

        private AudioSource _source;
        private Dictionary<BgmId, AudioClip> _clipMap;
        private float _volume;
        private bool _muted;
        private Coroutine _fadeRoutine;

        private const string PREFS_VOLUME = "Audio_BGM_Volume";
        private const string PREFS_MUTE = "Audio_BGM_Mute";

        public BgmId CurrentBgm { get; private set; } = BgmId.None;

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = Mathf.Clamp01(value);
                if (_source != null)
                    _source.volume = _volume;
                PlayerPrefs.SetFloat(PREFS_VOLUME, _volume);
                OnVolumeChanged?.Invoke(_volume);
            }
        }

        public bool Muted
        {
            get => _muted;
            set
            {
                _muted = value;
                if (_source != null)
                    _source.mute = _muted;
                PlayerPrefs.SetInt(PREFS_MUTE, _muted ? 1 : 0);
                OnMutedChanged?.Invoke(_muted);
            }
        }

        public event Action<float> OnVolumeChanged;
        public event Action<bool> OnMutedChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 1. 기존 BGM_Manager 오브젝트의 AudioSource를 우선 찾기
            var bgmManagerObj = GameObject.Find("BGM_Manager");
            if (bgmManagerObj != null)
            {
                _source = bgmManagerObj.GetComponent<AudioSource>();
                if (_source != null)
                {
                    // 기존 AudioSource의 설정 유지
                    _source.loop = true;
                }
            }

            // 2. 못 찾으면 자기 오브젝트에 추가
            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
                _source.loop = true;
                _source.playOnAwake = false;
            }

            BuildClipMap();
            LoadSettings();
        }

        private void BuildClipMap()
        {
            _clipMap = new Dictionary<BgmId, AudioClip>(bgmClips.Length);
            foreach (var entry in bgmClips)
            {
                if (entry.clip != null && !_clipMap.ContainsKey(entry.id))
                    _clipMap.Add(entry.id, entry.clip);
            }
        }

        private void LoadSettings()
        {
            _volume = PlayerPrefs.GetFloat(PREFS_VOLUME, defaultVolume);
            _muted = PlayerPrefs.GetInt(PREFS_MUTE, 0) == 1;
            if (_source != null)
            {
                _source.volume = _volume;
                _source.mute = _muted;
            }
        }

        public void Play(BgmId id)
        {
            Play(id, 0f);
        }

        public void Play(BgmId id, float fadeDuration)
        {
            if (id == BgmId.None) return;
            if (!_clipMap.TryGetValue(id, out var clip) || clip == null) return;

            if (_fadeRoutine != null)
                StopCoroutine(_fadeRoutine);

            _fadeRoutine = StartCoroutine(PlayWithFade(clip, id, fadeDuration));
        }

        private IEnumerator PlayWithFade(AudioClip clip, BgmId id, float fadeDuration)
        {
            if (fadeDuration > 0f && _source.isPlaying)
            {
                float startVol = _source.volume;
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    _source.volume = Mathf.Lerp(startVol, 0f, elapsed / fadeDuration);
                    yield return null;
                }
            }

            _source.Stop();
            _source.clip = clip;
            CurrentBgm = id;
            _source.Play();

            if (fadeDuration > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    _source.volume = Mathf.Lerp(0f, _volume, elapsed / fadeDuration);
                    yield return null;
                }
                _source.volume = _volume;
            }

            _fadeRoutine = null;
        }

        public void Stop(float fadeDuration = 0)
        {
            if (_fadeRoutine != null)
                StopCoroutine(_fadeRoutine);

            if (fadeDuration > 0f && _source.isPlaying)
            {
                _fadeRoutine = StartCoroutine(StopWithFade(fadeDuration));
                return;
            }

            _source.Stop();
            CurrentBgm = BgmId.None;
        }

        private IEnumerator StopWithFade(float fadeDuration)
        {
            float startVol = _source.volume;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                _source.volume = Mathf.Lerp(startVol, 0f, elapsed / fadeDuration);
                yield return null;
            }
            _source.Stop();
            _source.volume = _volume;
            CurrentBgm = BgmId.None;
            _fadeRoutine = null;
        }

        public void Pause()
        {
            if (_source != null)
                _source.Pause();
        }

        public void Resume()
        {
            if (_source != null)
                _source.UnPause();
        }

        public void SetVolume(float volume)
        {
            Volume = volume;
        }

        public void ResetToDefaults()
        {
            Volume = defaultVolume;
            Muted = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                PlayerPrefs.Save();
                Instance = null;
            }
        }
    }
}
