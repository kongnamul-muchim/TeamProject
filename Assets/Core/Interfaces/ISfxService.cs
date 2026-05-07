using System;
using HideAndInk.Core.Audio;

namespace HideAndInk.Core.Interfaces
{
    /// <summary>
    /// 효과음(SFX) 재생 서비스 인터페이스
    /// 컴포넌트에서 이 인터페이스를 주입받아 사운드를 재생
    /// </summary>
    public interface ISfxService
    {
        /// <summary>효과음 재생 (1회)</summary>
        void Play(SfxId id);

        /// <summary>효과음 재생 (볼륨 조절)</summary>
        void Play(SfxId id, float volumeScale);

        /// <summary>효과음 재생 (위치 기반 3D 사운드)</summary>
        void PlayAtPoint(SfxId id, UnityEngine.Vector3 position);

        /// <summary>효과음 재생 (위치 + 볼륨)</summary>
        void PlayAtPoint(SfxId id, UnityEngine.Vector3 position, float volumeScale);

        /// <summary>특정 SFX가 현재 재생 중인지</summary>
        bool IsPlaying(SfxId id);

        /// <summary>모든 SFX 정지</summary>
        void StopAll();

        /// <summary>SFX 볼륨 (0.0 ~ 1.0)</summary>
        float Volume { get; set; }

        /// <summary>SFX 음소거 여부</summary>
        bool Muted { get; set; }

        /// <summary>볼륨 변경 이벤트</summary>
        event Action<float> OnVolumeChanged;

        /// <summary>음소거 변경 이벤트</summary>
        event Action<bool> OnMutedChanged;
    }
}
