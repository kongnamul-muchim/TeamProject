using System;
using System.Collections.Generic;
using HideAndInk.Core.Interfaces;
using UnityEngine;

namespace HideAndInk.Core.Audio
{
    [Serializable]
    public struct SfxEntry
    {
        public SfxId id;
        public AudioClip[] clips;
    }

    public sealed class SfxManager : MonoBehaviour, ISfxService
    {
        public static SfxManager Instance { get; private set; }

        [SerializeField] private SfxEntry[] sfxClips;
        [SerializeField][Range(0f, 1f)] private float defaultVolume = 0.5f;

        private AudioSource _source;
        private Dictionary<SfxId, AudioClip[]> _clipMap;
        private float _volume;
        private bool _muted;

        private readonly HashSet<SfxId> _warnedMissingIds = new HashSet<SfxId>();
        private bool _warnedSourceNull;

        private const string PREFS_VOLUME = "Audio_SFX_Volume";
        private const string PREFS_MUTE = "Audio_SFX_Mute";

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
                // 기존 인스턴스가 있으면 자신을 파괴 (DontDestroyOnLoad 유지)
                Debug.LogWarning($"[SfxManager] Another SfxManager already exists on '{Instance.gameObject.name}'. Destroying duplicate on '{gameObject.name}'.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _source = gameObject.AddComponent<AudioSource>();
            _source.loop = false;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            if (sfxClips == null || sfxClips.Length == 0)
            {
                Debug.LogWarning($"[SfxManager] sfxClips is empty on '{gameObject.name}'. No sounds will play.");
            }

            BuildClipMap();
            LoadSettings();
        }

        private void BuildClipMap()
        {
            _clipMap = new Dictionary<SfxId, AudioClip[]>(sfxClips.Length);
            foreach (var entry in sfxClips)
            {
                if (entry.clips != null && entry.clips.Length > 0 && !_clipMap.ContainsKey(entry.id))
                    _clipMap.Add(entry.id, entry.clips);
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

        private AudioClip GetRandomClip(SfxId id)
        {
            if (!_clipMap.TryGetValue(id, out var clips) || clips == null || clips.Length == 0)
            {
                if (_warnedMissingIds.Add(id))
                {
                    Debug.LogWarning($"[SfxManager] No clips registered for SfxId.{id}. Check the sfxClips array in the inspector.");
                }
                return null;
            }
            return clips[UnityEngine.Random.Range(0, clips.Length)];
        }

        public void Play(SfxId id)
        {
            if (_source == null)
            {
                if (!_warnedSourceNull)
                {
                    _warnedSourceNull = true;
                    Debug.LogWarning("[SfxManager] AudioSource is null. SfxManager may have been destroyed or not initialized.");
                }
                return;
            }

            var clip = GetRandomClip(id);
            if (clip != null)
                _source.PlayOneShot(clip, _volume);
        }

        public void Play(SfxId id, float volumeScale)
        {
            if (_source == null)
            {
                if (!_warnedSourceNull)
                {
                    _warnedSourceNull = true;
                    Debug.LogWarning("[SfxManager] AudioSource is null. SfxManager may have been destroyed or not initialized.");
                }
                return;
            }

            var clip = GetRandomClip(id);
            if (clip != null)
                _source.PlayOneShot(clip, _volume * Mathf.Clamp01(volumeScale));
        }

        public void PlayAtPoint(SfxId id, Vector3 position)
        {
            var clip = GetRandomClip(id);
            if (clip != null)
                AudioSource.PlayClipAtPoint(clip, position, _volume);
        }

        public void PlayAtPoint(SfxId id, Vector3 position, float volumeScale)
        {
            var clip = GetRandomClip(id);
            if (clip != null)
                AudioSource.PlayClipAtPoint(clip, position, _volume * Mathf.Clamp01(volumeScale));
        }

        public bool IsPlaying(SfxId id)
        {
            return _source != null && _source.isPlaying;
        }

        public void StopAll()
        {
            if (_source != null)
                _source.Stop();
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
